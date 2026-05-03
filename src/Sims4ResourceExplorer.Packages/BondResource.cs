using System.Buffers.Binary;

namespace Sims4ResourceExplorer.Packages;

/// <summary>
/// A bone-delta morph entry inside a BOND resource: a per-bone offset, scale, and rotation
/// that gets applied to the rig at a given weight, deforming any meshes skinned to that bone.
/// </summary>
public sealed record Ts4BondBoneAdjust(
    uint SlotHash,
    float OffsetX, float OffsetY, float OffsetZ,
    float ScaleX, float ScaleY, float ScaleZ,
    float QuatX, float QuatY, float QuatZ, float QuatW);

/// <summary>
/// A reference to another resource carried in a BOND header. BOND files use the ITG (Instance,
/// Type, Group) ordering for these, not the standard TGI order.
/// </summary>
public sealed record Ts4BondResourceKey(uint Type, uint Group, ulong Instance)
{
    public string FullTgi => $"{Type:X8}:{Group:X8}:{Instance:X16}";
}

/// <summary>
/// A parsed BOND (Bone Delta) resource. BONDs are referenced by SimInfo body modifiers and
/// sculpts; applying them transforms specific bones in the canonical rig, which in turn
/// deforms any skinned mesh that uses those bones. Adult Female waist gap, child face
/// proportions, and other shape-driven artifacts all need this pipeline before they render
/// correctly.
///
/// Format mirrored from TS4SimRipper <c>BOND.cs</c>:
/// <list type="number">
///   <item>contextVersion (UInt32)</item>
///   <item>publicKeyCount, externalKeyCount, delayLoadKeyCount, objectKeyCount (UInt32 each)</item>
///   <item>publicKey TGI[count] in ITG order</item>
///   <item>(privateKey TGIs if objectKeyCount &gt; publicKeyCount)</item>
///   <item>externalKey TGI[externalKeyCount] in ITG order</item>
///   <item>delayLoadKey TGI[delayLoadKeyCount] in ITG order</item>
///   <item>objectKey ObjectData[objectKeyCount] (skipped here — only needed for write-back)</item>
///   <item>internalVersion (UInt32)</item>
///   <item>boneAdjustCount (UInt32)</item>
///   <item>BoneAdjust[count] each = (UInt32 slotHash, 7×Float offset/scale, 4×Float quaternion)</item>
/// </list>
/// </summary>
public sealed record Ts4BondResource(
    uint ContextVersion,
    uint InternalVersion,
    IReadOnlyList<Ts4BondResourceKey> PublicKeys,
    IReadOnlyList<Ts4BondResourceKey> ExternalKeys,
    IReadOnlyList<Ts4BondResourceKey> DelayLoadKeys,
    IReadOnlyList<Ts4BondBoneAdjust> Adjustments)
{
    private const int BoneAdjustByteCount = 4 + (4 * 3) + (4 * 3) + (4 * 4); // slotHash + offset[3] + scale[3] + quat[4] = 44

    public static Ts4BondResource Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var span = (ReadOnlySpan<byte>)bytes;
        if (span.Length < 24)
        {
            throw new InvalidDataException($"BOND payload too short: {span.Length} bytes (need 24+ for header).");
        }

        var offset = 0;
        var contextVersion = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var publicKeyCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var externalKeyCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var delayLoadKeyCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var objectKeyCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

        var publicKeys = ReadKeys(span, ref offset, checked((int)publicKeyCount));

        // Private keys (objectKeyCount - publicKeyCount entries) are read but not surfaced.
        if (objectKeyCount > publicKeyCount)
        {
            ReadKeys(span, ref offset, checked((int)(objectKeyCount - publicKeyCount)));
        }

        var externalKeys = ReadKeys(span, ref offset, checked((int)externalKeyCount));
        var delayLoadKeys = ReadKeys(span, ref offset, checked((int)delayLoadKeyCount));

        // ObjectData entries: each is `position UInt32 + length UInt32` per TS4SimRipper
        // BOND.cs:163-167. We don't need their contents to apply BONDs, but we DO need to
        // skip past them — 8 bytes per entry.
        var objectDataBytes = checked((int)objectKeyCount * 8);
        if (offset + objectDataBytes > span.Length)
        {
            throw new InvalidDataException($"BOND ObjectData block truncated: need {objectDataBytes} bytes at offset {offset}.");
        }
        offset += objectDataBytes;

        if (offset + 8 > span.Length)
        {
            throw new InvalidDataException("BOND payload truncated before BoneAdjust block.");
        }
        var internalVersion = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var boneAdjustCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        if (boneAdjustCount > 4096)
        {
            throw new InvalidDataException($"BOND boneAdjustCount {boneAdjustCount} is implausibly large.");
        }

        var requiredBytes = checked((long)boneAdjustCount * BoneAdjustByteCount);
        if (offset + requiredBytes > span.Length)
        {
            throw new InvalidDataException($"BOND payload truncated: need {requiredBytes} more bytes for {boneAdjustCount} adjustments.");
        }

        var adjustments = new Ts4BondBoneAdjust[boneAdjustCount];
        for (var i = 0; i < boneAdjustCount; i++)
        {
            var slotHash = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
            var ox = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4)); offset += 4;
            var oy = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4)); offset += 4;
            var oz = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4)); offset += 4;
            var sx = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4)); offset += 4;
            var sy = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4)); offset += 4;
            var sz = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4)); offset += 4;
            var qx = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4)); offset += 4;
            var qy = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4)); offset += 4;
            var qz = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4)); offset += 4;
            var qw = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4)); offset += 4;
            adjustments[i] = new Ts4BondBoneAdjust(slotHash, ox, oy, oz, sx, sy, sz, qx, qy, qz, qw);
        }

        return new Ts4BondResource(contextVersion, internalVersion, publicKeys, externalKeys, delayLoadKeys, adjustments);
    }

    /// <summary>BOND key blocks use ITG ordering: instance (UInt64), type (UInt32), group (UInt32).</summary>
    private static IReadOnlyList<Ts4BondResourceKey> ReadKeys(ReadOnlySpan<byte> span, ref int offset, int count)
    {
        if (count == 0) return [];
        var bytesNeeded = checked(count * 16);
        if (offset + bytesNeeded > span.Length)
        {
            throw new InvalidDataException($"BOND key block truncated: need {bytesNeeded} bytes at offset {offset}.");
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
}
