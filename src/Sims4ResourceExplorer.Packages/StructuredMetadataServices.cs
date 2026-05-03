using System.Globalization;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Packages;

public sealed record StructuredResourceMetadata(string? Description, string? SuggestedName = null, string? Diagnostic = null);

public sealed record Ts4RegionMapEntry(
    uint RegionValue,
    string RegionLabel,
    float LayerValue,
    bool IsReplacement,
    IReadOnlyList<ResourceKeyRecord> LinkedKeys);

public sealed record Ts4RegionMap(
    uint ContextVersion,
    uint Version,
    int PublicKeyCount,
    int ExternalKeyCount,
    int DelayLoadKeyCount,
    IReadOnlyList<Ts4RegionMapEntry> Entries);

public sealed record Ts4SkintoneOverlay(
    uint TypeValue,
    ulong TextureInstance);

public sealed record Ts4Skintone(
    uint Version,
    ulong BaseTextureInstance,
    IReadOnlyList<Ts4SkintoneOverlay> OverlayTextures,
    uint Colorize,
    uint OverlayOpacity,
    int TagCount,
    float MakeupOpacity,
    IReadOnlyList<uint> SwatchColors,
    float DisplayIndex,
    float? MakeupOpacity2);

public static class Ts4StructuredResourceMetadataExtractor
{
    public static bool RequiresStructuredDescription(string typeName) =>
        typeName is "CASPreset" or "RegionMap" or "Skintone";

    public static Ts4RegionMap ParseRegionMap(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream);

        var contextVersion = reader.ReadUInt32();
        if (contextVersion != 3)
        {
            throw new InvalidDataException($"Unsupported RegionMap context version {contextVersion}.");
        }

        var publicKeyCount = reader.ReadUInt32();
        var externalKeyCount = reader.ReadUInt32();
        var delayLoadKeyCount = reader.ReadUInt32();
        var objectCount = reader.ReadUInt32();
        if (objectCount < publicKeyCount)
        {
            throw new InvalidDataException("RegionMap object count is smaller than the public key count.");
        }

        SkipBytes(stream, publicKeyCount * 16L, "RegionMap public key table");
        SkipBytes(stream, (objectCount - publicKeyCount) * 16L, "RegionMap private key table");
        SkipBytes(stream, externalKeyCount * 16L, "RegionMap external key table");
        SkipBytes(stream, delayLoadKeyCount * 16L, "RegionMap delay-load key table");
        SkipBytes(stream, objectCount * 8L, "RegionMap object-data table");

        var version = reader.ReadUInt32();
        if (version != 1)
        {
            throw new InvalidDataException($"Unsupported RegionMap version {version}.");
        }

        var entryCount = reader.ReadUInt32();
        var entries = new List<Ts4RegionMapEntry>(checked((int)entryCount));
        for (var index = 0; index < entryCount; index++)
        {
            var regionValue = reader.ReadUInt32();
            var layerValue = reader.ReadSingle();
            var isReplacement = reader.ReadByte() != 0;
            var keyCount = reader.ReadUInt32();
            var linkedKeys = new List<ResourceKeyRecord>(checked((int)keyCount));
            for (var keyIndex = 0; keyIndex < keyCount; keyIndex++)
            {
                var type = reader.ReadUInt32();
                var group = reader.ReadUInt32();
                var instance = reader.ReadUInt64();
                linkedKeys.Add(new ResourceKeyRecord(type, group, instance, $"0x{type:X8}"));
            }

            entries.Add(new Ts4RegionMapEntry(
                regionValue,
                FormatCasPartRegion(regionValue),
                layerValue,
                isReplacement,
                linkedKeys));
        }

        return new Ts4RegionMap(
            contextVersion,
            version,
            checked((int)publicKeyCount),
            checked((int)externalKeyCount),
            checked((int)delayLoadKeyCount),
            entries);
    }

    public static Ts4Skintone ParseSkintone(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream);

        var version = reader.ReadUInt32();
        if (version != 6 && version != 12)
        {
            throw new InvalidDataException($"Unsupported Skintone version {version}.");
        }

        // v12 prepends a sub-texture block: a single byte count + N × 28-byte entries.
        // Each entry is { instance:UInt64, reserved:UInt64, weights:Float[3] }. The first
        // entry's instance equals what v6 stores in `baseTextureInstance` for the same
        // skintone, so we treat subTextures[0].instance as the base. See
        // docs/workflows/v12-skintone-format.md (Plan 3.3).
        ulong textureInstance;
        if (version == 12)
        {
            var subTextureCount = reader.ReadByte();
            ulong firstSubTextureInstance = 0;
            for (var index = 0; index < subTextureCount; index++)
            {
                var subInstance = reader.ReadUInt64();
                _ = reader.ReadUInt64();    // reserved (typically 0)
                _ = reader.ReadSingle();    // weight 1
                _ = reader.ReadSingle();    // weight 2
                _ = reader.ReadSingle();    // weight 3
                if (index == 0) firstSubTextureInstance = subInstance;
            }
            textureInstance = firstSubTextureInstance;
        }
        else
        {
            textureInstance = reader.ReadUInt64();
        }

        var overlayTextureCount = reader.ReadUInt32();
        var overlays = new List<Ts4SkintoneOverlay>(checked((int)overlayTextureCount));
        for (var index = 0; index < overlayTextureCount; index++)
        {
            overlays.Add(new Ts4SkintoneOverlay(
                reader.ReadUInt32(),
                reader.ReadUInt64()));
        }

        // Per TS4SimRipper TONE.cs: the four bytes after the overlay block are
        // `saturation:UInt16` + `hue:UInt16` (HSL adjustments, NOT an ARGB color), followed by
        // `opacity:UInt32`. We previously read these as a single "colorize" UInt32 — the byte
        // count matched, but treating that value as a color produced garbage tints downstream.
        var saturation = reader.ReadUInt16();
        var hue = reader.ReadUInt16();
        var colorize = ((uint)hue << 16) | saturation;
        var overlayOpacity = reader.ReadUInt32();
        var tagCount = reader.ReadUInt32();
        // Per TS4SimRipper CASP.PartTag: total tag size is 2 bytes (UInt16 flagCategory) plus
        // 2 bytes (UInt16 flagValue) for version < 7, OR plus 4 bytes (UInt32 flagValue) for
        // version >= 7. Skintone version 6 uses the 4-byte total; v12 uses the 6-byte total.
        var tagValueSize = version >= 7 ? 4 : 2;
        for (var index = 0; index < tagCount; index++)
        {
            _ = reader.ReadUInt16();   // flagCategory
            for (var b = 0; b < tagValueSize; b++) _ = reader.ReadByte();
        }

        // v6 has a `makeupOpacity:Float` field here; v12 omits it entirely (the byte
        // immediately after the tag block is `swatchColorCount:Byte`).
        var makeupOpacity = version == 6 ? reader.ReadSingle() : 0f;
        var swatchColorCount = reader.ReadByte();
        var swatchColors = new uint[swatchColorCount];
        for (var index = 0; index < swatchColorCount; index++)
        {
            swatchColors[index] = reader.ReadUInt32();
        }

        var displayIndex = reader.ReadSingle();
        // v6 may have an optional trailing `makeupOpacity2:Float`. v12 has a different
        // trailing block (extraHash:UInt32 + UInt16 + 6 floats — semantics TBD per Plan 3.3).
        // We don't surface those v12 trailing fields yet; the renderer doesn't need them.
        float? makeupOpacity2 = null;
        if (version == 6 && stream.Position + 4 <= stream.Length)
        {
            makeupOpacity2 = reader.ReadSingle();
        }

        return new Ts4Skintone(
            version,
            textureInstance,
            overlays,
            colorize,
            overlayOpacity,
            checked((int)tagCount),
            makeupOpacity,
            swatchColors,
            displayIndex,
            makeupOpacity2);
    }

    public static StructuredResourceMetadata Describe(string typeName, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(bytes);

        if (!RequiresStructuredDescription(typeName))
        {
            return new StructuredResourceMetadata(null);
        }

        try
        {
            return typeName switch
            {
                "CASPreset" => DescribeCasPreset(bytes),
                "RegionMap" => DescribeRegionMap(bytes),
                "Skintone" => DescribeSkintone(bytes),
                _ => new StructuredResourceMetadata(null)
            };
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentOutOfRangeException)
        {
            return new StructuredResourceMetadata(null, Diagnostic: ex.Message);
        }
    }

    private static StructuredResourceMetadata DescribeCasPreset(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream);

        var version = reader.ReadUInt32();
        if (version < 7 || version > 12)
        {
            throw new InvalidDataException($"Unsupported CASPreset version {version}.");
        }

        var ageGender = reader.ReadUInt32();
        uint? bodyFrameGender = version >= 11 ? reader.ReadUInt32() : null;
        uint? species = version >= 8 ? reader.ReadUInt32() : null;
        var region = reader.ReadUInt32();
        uint? bodySubType = version >= 9 ? reader.ReadUInt32() : null;
        var archetype = reader.ReadUInt32();
        var displayIndex = reader.ReadSingle();
        var presetNameKey = reader.ReadUInt32();
        var presetDescriptionKey = reader.ReadUInt32();

        var sculptCount = reader.ReadUInt32();
        for (var index = 0; index < sculptCount; index++)
        {
            _ = reader.ReadUInt64();
            if (version < 9)
            {
                _ = reader.ReadUInt32();
            }
        }

        var modifierCount = reader.ReadUInt32();
        for (var index = 0; index < modifierCount; index++)
        {
            _ = reader.ReadUInt64();
            _ = reader.ReadSingle();
            if (version < 9)
            {
                _ = reader.ReadUInt32();
            }
        }

        var isPhysiqueSet = reader.ReadByte() == 1;
        if (isPhysiqueSet)
        {
            _ = reader.ReadSingle();
            _ = reader.ReadSingle();
            _ = reader.ReadSingle();
            _ = reader.ReadSingle();
        }

        ulong? partSetInstance = null;
        uint? partSetBodyType = null;
        var isPartSet = reader.ReadByte() == 1;
        if (isPartSet)
        {
            partSetInstance = reader.ReadUInt64();
            partSetBodyType = reader.ReadUInt32();
        }

        var chanceForRandom = reader.ReadSingle();
        var tagCount = reader.ReadUInt32();
        for (var index = 0; index < tagCount; index++)
        {
            _ = reader.ReadUInt16();
            _ = version >= 10 ? reader.ReadUInt32() : reader.ReadUInt16();
        }

        var parts = new List<string>
        {
            $"CAS preset v{version}",
            $"region={FormatSimRegion(region)}",
            $"ageGender=0x{ageGender:X8}",
            $"archetype=0x{archetype:X8}",
            $"displayIndex={displayIndex.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"sculpts={sculptCount}",
            $"modifiers={modifierCount}",
            $"physique={(isPhysiqueSet ? "yes" : "no")}",
            $"partSet={(isPartSet ? "yes" : "no")}",
            $"tags={tagCount}",
            $"chanceForRandom={chanceForRandom.ToString("0.###", CultureInfo.InvariantCulture)}"
        };

        if (bodyFrameGender.HasValue)
        {
            parts.Add($"bodyFrame=0x{bodyFrameGender.Value:X8}");
        }

        if (species.HasValue)
        {
            parts.Add($"species=0x{species.Value:X8}");
        }

        if (bodySubType.HasValue)
        {
            parts.Add($"bodySubType=0x{bodySubType.Value:X8}");
        }

        if (presetNameKey != 0)
        {
            parts.Add($"nameKey=0x{presetNameKey:X8}");
        }

        if (presetDescriptionKey != 0)
        {
            parts.Add($"descriptionKey=0x{presetDescriptionKey:X8}");
        }

        if (partSetInstance.HasValue && partSetBodyType.HasValue)
        {
            parts.Add($"partSetBodyType={partSetBodyType.Value}");
            parts.Add($"partSetInstance=0x{partSetInstance.Value:X16}");
        }

        return new StructuredResourceMetadata(string.Join(" | ", parts));
    }

    private static StructuredResourceMetadata DescribeRegionMap(byte[] bytes)
    {
        var regionMap = ParseRegionMap(bytes);
        var replacementEntries = regionMap.Entries.Count(static entry => entry.IsReplacement);
        var totalLinkedKeys = regionMap.Entries.Sum(static entry => entry.LinkedKeys.Count);
        var distinctRegions = regionMap.Entries
            .Select(static entry => entry.RegionLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var parts = new List<string>
        {
            $"Region map v{regionMap.Version}",
            $"entries={regionMap.Entries.Count}",
            $"replacementEntries={replacementEntries}",
            $"linkedKeys={totalLinkedKeys}",
            $"publicKeys={regionMap.PublicKeyCount}",
            $"externalKeys={regionMap.ExternalKeyCount}",
            $"delayLoadKeys={regionMap.DelayLoadKeyCount}",
            $"regions={FormatList(distinctRegions, 6)}"
        };

        return new StructuredResourceMetadata(string.Join(" | ", parts));
    }

    private static StructuredResourceMetadata DescribeSkintone(byte[] bytes)
    {
        var skintone = ParseSkintone(bytes);

        var parts = new List<string>
        {
            $"Skintone v{skintone.Version}",
            $"baseTexture=0x{skintone.BaseTextureInstance:X16}",
            $"overlays={skintone.OverlayTextures.Count}",
            $"tags={skintone.TagCount}",
            $"swatches={skintone.SwatchColors.Count}",
            $"displayIndex={skintone.DisplayIndex.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"makeupOpacity={skintone.MakeupOpacity.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"overlayOpacity={skintone.OverlayOpacity}",
            $"colorize=0x{skintone.Colorize:X8}"
        };

        if (skintone.MakeupOpacity2.HasValue)
        {
            parts.Add($"makeupOpacity2={skintone.MakeupOpacity2.Value.ToString("0.###", CultureInfo.InvariantCulture)}");
        }

        if (skintone.SwatchColors.Count > 0)
        {
            parts.Add($"swatchColors={FormatList(skintone.SwatchColors.Select(static color => $"#{color:X8}"), 4)}");
        }

        return new StructuredResourceMetadata(string.Join(" | ", parts));
    }

    private static string FormatSimRegion(uint value) => value switch
    {
        0 => "Eyes",
        1 => "Nose",
        2 => "Mouth",
        3 => "Cheeks",
        4 => "Chin",
        5 => "Jaw",
        6 => "Forehead",
        8 => "Brows",
        9 => "Ears",
        10 => "Head",
        12 => "FullFace",
        14 => "Chest",
        15 => "UpperChest",
        16 => "Neck",
        17 => "Shoulders",
        18 => "UpperArm",
        19 => "LowerArm",
        20 => "Hands",
        21 => "Waist",
        22 => "Hips",
        23 => "Belly",
        24 => "Butt",
        25 => "Thighs",
        26 => "LowerLeg",
        27 => "Feet",
        28 => "Body",
        29 => "UpperBody",
        30 => "LowerBody",
        31 => "All",
        32 => "Invalid",
        _ => $"0x{value:X8}"
    };

    private static string FormatCasPartRegion(uint value) => value switch
    {
        0 => "Base",
        1 => "Ankle",
        2 => "Calf",
        3 => "Knee",
        4 => "HandL",
        5 => "WristL",
        6 => "BicepL",
        7 => "BeltLow",
        8 => "BeltHigh",
        9 => "HairHatA",
        10 => "HairHatB",
        11 => "HairHatC",
        12 => "HairHatD",
        13 => "Neck",
        14 => "Chest",
        15 => "Stomach",
        16 => "HandR",
        17 => "WristR",
        18 => "BicepR",
        19 => "NecklaceShadow",
        _ => $"0x{value:X8}"
    };

    private static string FormatList(IEnumerable<string> values, int maxVisible)
    {
        var array = values.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (array.Length == 0)
        {
            return "(none)";
        }

        if (array.Length <= maxVisible)
        {
            return string.Join(", ", array);
        }

        return $"{string.Join(", ", array.Take(maxVisible))}, +{array.Length - maxVisible} more";
    }

    private static void SkipBytes(Stream stream, long byteCount, string context)
    {
        if (byteCount < 0 || stream.Position + byteCount > stream.Length)
        {
            throw new InvalidDataException($"{context} extends beyond the payload.");
        }

        stream.Position += byteCount;
    }
}
