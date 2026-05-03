using System.Buffers.Binary;

namespace Sims4ResourceExplorer.Packages;

/// <summary>
/// Parsed SimModifier (SMOD) resource. EA's SimInfo references SMODs from FaceModifier and
/// BodyModifier entries — each SMOD wraps the actual BOND (bone-pose), DMap shape, and DMap
/// normal TGIs that should be applied to the Sim's rig and meshes when the modifier is active.
///
/// Format mirrored from TS4SimRipper <c>SMOD.cs</c>. Resource type ID is <c>0xC5F6763E</c>.
/// </summary>
public sealed record Ts4SimModifierResource(
    uint ContextVersion,
    uint Version,
    uint AgeGender,
    uint Region,
    uint? SubRegion,                       // present when Version >= 144
    uint LinkTag,
    Ts4BondResourceKey BonePoseKey,        // BOND to apply (zero if none)
    Ts4BondResourceKey DeformerMapShapeKey, // DMap shape (zero if none)
    Ts4BondResourceKey DeformerMapNormalKey,// DMap normals (zero if none)
    IReadOnlyList<Ts4BondResourceKey> PublicKeys,
    IReadOnlyList<Ts4BondResourceKey> ExternalKeys,
    IReadOnlyList<Ts4BondResourceKey> BgeoKeys)
{
    public bool HasBondReference => BonePoseKey.Instance != 0;
    public bool HasShapeDeformerMap => DeformerMapShapeKey.Instance != 0;
    public bool HasNormalDeformerMap => DeformerMapNormalKey.Instance != 0;

    public static Ts4SimModifierResource Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var span = (ReadOnlySpan<byte>)bytes;
        if (span.Length < 32)
        {
            throw new InvalidDataException($"SMOD payload too short: {span.Length} bytes.");
        }

        var offset = 0;
        var contextVersion = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var publicKeyCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var externalKeyCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var delayLoadKeyCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var objectKeyCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

        var publicKeys = ReadKeys(span, ref offset, checked((int)publicKeyCount));
        var externalKeys = ReadKeys(span, ref offset, checked((int)externalKeyCount));
        var bgeoKeys = ReadKeys(span, ref offset, checked((int)delayLoadKeyCount));

        // Skip ObjectData entries — 8 bytes each (position UInt32 + length UInt32).
        var objectDataBytes = checked((int)objectKeyCount * 8);
        if (offset + objectDataBytes > span.Length)
        {
            throw new InvalidDataException($"SMOD ObjectData block truncated.");
        }
        offset += objectDataBytes;

        if (offset + 16 > span.Length)
        {
            throw new InvalidDataException("SMOD payload truncated before fixed-fields block.");
        }
        var version = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var ageGender = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var region = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        uint? subRegion = null;
        if (version >= 144)
        {
            if (offset + 4 > span.Length) throw new InvalidDataException("SMOD payload truncated before subRegion.");
            subRegion = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        }
        if (offset + 4 + (3 * 16) > span.Length)
        {
            throw new InvalidDataException("SMOD payload truncated before BOND/DMap key block.");
        }
        var linkTag = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var bonePoseKey = ReadOneKey(span, ref offset);
        var deformerMapShapeKey = ReadOneKey(span, ref offset);
        var deformerMapNormalKey = ReadOneKey(span, ref offset);
        // boneEntryList follows but we don't need its contents to apply morphs.

        return new Ts4SimModifierResource(
            contextVersion, version, ageGender, region, subRegion, linkTag,
            bonePoseKey, deformerMapShapeKey, deformerMapNormalKey,
            publicKeys, externalKeys, bgeoKeys);
    }

    private static Ts4BondResourceKey ReadOneKey(ReadOnlySpan<byte> span, ref int offset)
    {
        var instance = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)); offset += 8;
        var type = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        var group = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        return new Ts4BondResourceKey(type, group, instance);
    }

    private static IReadOnlyList<Ts4BondResourceKey> ReadKeys(ReadOnlySpan<byte> span, ref int offset, int count)
    {
        if (count == 0) return [];
        if (offset + (count * 16) > span.Length)
        {
            throw new InvalidDataException($"SMOD key block truncated.");
        }
        var keys = new Ts4BondResourceKey[count];
        for (var i = 0; i < count; i++)
        {
            keys[i] = ReadOneKey(span, ref offset);
        }
        return keys;
    }
}
