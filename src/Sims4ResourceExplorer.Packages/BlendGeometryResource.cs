using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Sims4ResourceExplorer.Packages;

/// <summary>
/// One LOD entry inside a BGEO. Each LOD covers a contiguous range of vertex IDs in the
/// underlying GEOM and contributes <see cref="NumberDeltaVectors"/> entries to the global
/// <c>VectorData</c> array.
/// </summary>
public sealed record Ts4BlendGeometryLod(uint IndexBase, uint NumberVertices, uint NumberDeltaVectors);

/// <summary>
/// One <c>Blend</c> entry in a BGEO blend map: per-vertex flags + a packed offset into the
/// global <c>VectorData</c> array. The absolute index into <c>VectorData</c> is stored in
/// <see cref="Index"/> (computed during parse so application code never needs to walk the
/// running-index ladder).
/// </summary>
public sealed record Ts4BlendGeometryBlend(bool PositionDelta, bool NormalDelta, short Offset, int Index);

/// <summary>
/// One delta vector inside a BGEO. The on-disk format packs each axis as a UInt16 with the
/// sign bit XOR'd into the high bit; <see cref="ToVector3"/> decodes back to a real
/// <see cref="Vector3"/> divided by the standard 8000 scale factor (per TS4SimRipper BGEO.cs:368-378).
/// </summary>
public sealed record Ts4BlendGeometryVector(ushort RawX, ushort RawY, ushort RawZ)
{
    public Vector3 ToVector3()
    {
        const float scaleFactor = 8000f;
        return new Vector3(DecodeAxis(RawX), DecodeAxis(RawY), DecodeAxis(RawZ));

        static float DecodeAxis(ushort raw)
        {
            // Flip sign bit, sign-extend to 32 bits, then divide.
            var tmp = ((raw ^ 0x8000) << 16) >> 16;
            return tmp / scaleFactor;
        }
    }
}

/// <summary>
/// One resolved BGEO morph entry: a parsed BGEO resource plus the modifier weight that should
/// be applied at every covered vertex. Region indicates which face/body area the SMOD targets
/// (informational; the morpher applies to all vertices the BGEO LOD covers).
/// </summary>
public sealed record Ts4SimBlendGeometryMorph(
    Ts4BlendGeometryResource Bgeo,
    float Weight,
    uint Region);

/// <summary>
/// A parsed BlendGeometry (BGEO) resource. BGEOs drive per-vertex shape and normal deltas
/// indexed by GEOM vertex ID — a third morph type alongside BOND (bone-delta) and DMap
/// (UV1-sampled). Most face-shape modifier SMODs reference BGEOs (verified for the v21
/// Adult Female SimInfo: all 38 modifiers point to BGEO-bearing SMODs).
///
/// Format mirrored from TS4SimRipper <c>BGEO.cs:96-163</c>. Resource type ID is <c>0x067CAA11</c>.
/// </summary>
public sealed record Ts4BlendGeometryResource(
    uint ContextVersion,
    uint Version,
    IReadOnlyList<Ts4BondResourceKey> PublicKeys,
    IReadOnlyList<Ts4BondResourceKey> ExternalKeys,
    IReadOnlyList<Ts4BondResourceKey> DelayLoadKeys,
    IReadOnlyList<Ts4BlendGeometryLod> Lods,
    IReadOnlyList<Ts4BlendGeometryBlend> BlendMap,
    IReadOnlyList<Ts4BlendGeometryVector> VectorData)
{
    public bool HasData => BlendMap.Count > 0 && VectorData.Count > 0;

    public static Ts4BlendGeometryResource Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 28)
        {
            throw new InvalidDataException($"BGEO payload too short: {bytes.Length} bytes.");
        }

        var span = (ReadOnlySpan<byte>)bytes;
        var offset = 0;
        var contextVersion = ReadUInt32(span, ref offset);
        var publicKeyCount = ReadUInt32(span, ref offset);
        var externalKeyCount = ReadUInt32(span, ref offset);
        var delayLoadKeyCount = ReadUInt32(span, ref offset);
        var objectCount = ReadUInt32(span, ref offset);

        var publicKeys = ReadKeys(span, ref offset, checked((int)publicKeyCount));
        var externalKeys = ReadKeys(span, ref offset, checked((int)externalKeyCount));
        var delayLoadKeys = ReadKeys(span, ref offset, checked((int)delayLoadKeyCount));

        // ObjectData is 8 bytes each (position UInt32 + length UInt32). We don't surface it.
        var objectDataBytes = checked((int)objectCount * 8);
        if (offset + objectDataBytes > span.Length)
        {
            throw new InvalidDataException($"BGEO ObjectData block truncated: need {objectDataBytes} bytes.");
        }
        offset += objectDataBytes;

        // Magic tag must read as ASCII "BGEO" = 0x4F454742 LE.
        var tag = ReadUInt32(span, ref offset);
        if (tag != 0x4F454742u)
        {
            throw new InvalidDataException($"BGEO magic mismatch: got 0x{tag:X8}, expected 0x4F454742 ('BGEO').");
        }
        var version = ReadUInt32(span, ref offset);
        if (version != 0x00000600u)
        {
            throw new InvalidDataException($"Unsupported BGEO version 0x{version:X8} (expected 0x00000600).");
        }

        var lodCount = ReadUInt32(span, ref offset);
        var totalVertexCount = ReadUInt32(span, ref offset);
        var totalVectorCount = ReadUInt32(span, ref offset);

        var lods = new Ts4BlendGeometryLod[lodCount];
        for (var i = 0; i < lodCount; i++)
        {
            var indexBase = ReadUInt32(span, ref offset);
            var numVerts = ReadUInt32(span, ref offset);
            var numDeltaVectors = ReadUInt32(span, ref offset);
            lods[i] = new Ts4BlendGeometryLod(indexBase, numVerts, numDeltaVectors);
        }

        // Blend map walks LOD boundaries: the running index advances by NumberDeltaVectors when
        // we cross into the next LOD. Each entry is a UInt16 packing flags (low 2 bits) +
        // signed offset (upper 14 bits, arithmetic-shifted right by 2).
        var blendMap = new Ts4BlendGeometryBlend[totalVertexCount];
        var runningIndex = 0;
        var lodCounter = 0;
        var previousLodVerts = 0u;
        for (var i = 0; i < totalVertexCount; i++)
        {
            if (lodCount > 0 && i == lods[Math.Min(lodCounter, (int)lodCount - 1)].NumberVertices + previousLodVerts)
            {
                runningIndex += (int)lods[Math.Min(lodCounter, (int)lodCount - 1)].NumberDeltaVectors;
                previousLodVerts += lods[Math.Min(lodCounter, (int)lodCount - 1)].NumberVertices;
                lodCounter++;
                if (lodCounter > 3) lodCounter = 3;
            }
            if (offset + 2 > span.Length)
            {
                throw new InvalidDataException($"BGEO BlendMap truncated at vertex {i}.");
            }
            var packed = (short)BinaryPrimitives.ReadInt16LittleEndian(span.Slice(offset, 2));
            offset += 2;
            var positionDelta = (packed & 0x1) > 0;
            var normalDelta = (packed & 0x2) > 0;
            var entryOffset = (short)(packed >> 2);
            var index = entryOffset + runningIndex;
            blendMap[i] = new Ts4BlendGeometryBlend(positionDelta, normalDelta, entryOffset, index);
            runningIndex += entryOffset;
        }

        var vectorData = new Ts4BlendGeometryVector[totalVectorCount];
        for (var i = 0; i < totalVectorCount; i++)
        {
            if (offset + 6 > span.Length)
            {
                throw new InvalidDataException($"BGEO VectorData truncated at index {i}.");
            }
            var x = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));
            var y = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset + 2, 2));
            var z = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset + 4, 2));
            offset += 6;
            vectorData[i] = new Ts4BlendGeometryVector(x, y, z);
        }

        return new Ts4BlendGeometryResource(
            contextVersion, version,
            publicKeys, externalKeys, delayLoadKeys,
            lods, blendMap, vectorData);
    }

    private static IReadOnlyList<Ts4BondResourceKey> ReadKeys(ReadOnlySpan<byte> span, ref int offset, int count)
    {
        if (count == 0) return [];
        var bytes = checked(count * 16);
        if (offset + bytes > span.Length)
        {
            throw new InvalidDataException($"BGEO key block truncated: need {bytes} bytes.");
        }
        var keys = new Ts4BondResourceKey[count];
        for (var i = 0; i < count; i++)
        {
            var instance = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)); offset += 8;
            var type = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
            var group = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
            keys[i] = new Ts4BondResourceKey(type, group, instance);
        }
        return keys;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset + 4 > span.Length) throw new InvalidDataException("BGEO header truncated.");
        var v = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4));
        offset += 4;
        return v;
    }
}
