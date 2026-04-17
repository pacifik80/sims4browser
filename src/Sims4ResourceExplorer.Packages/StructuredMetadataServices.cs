using System.Globalization;

namespace Sims4ResourceExplorer.Packages;

public sealed record StructuredResourceMetadata(string? Description, string? SuggestedName = null, string? Diagnostic = null);

public static class Ts4StructuredResourceMetadataExtractor
{
    public static bool RequiresStructuredDescription(string typeName) =>
        typeName is "CASPreset" or "RegionMap" or "Skintone";

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
        var replacementEntries = 0;
        var totalLinkedKeys = 0;
        var regionNames = new List<string>();
        for (var index = 0; index < entryCount; index++)
        {
            var region = reader.ReadUInt32();
            _ = reader.ReadSingle();
            var isReplacement = reader.ReadByte() != 0;
            var keyCount = reader.ReadUInt32();
            SkipBytes(stream, keyCount * 16L, "RegionMap entry key list");

            if (isReplacement)
            {
                replacementEntries++;
            }

            totalLinkedKeys += checked((int)keyCount);
            regionNames.Add(FormatCasPartRegion(region));
        }

        var distinctRegions = regionNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var parts = new List<string>
        {
            $"Region map v{version}",
            $"entries={entryCount}",
            $"replacementEntries={replacementEntries}",
            $"linkedKeys={totalLinkedKeys}",
            $"publicKeys={publicKeyCount}",
            $"externalKeys={externalKeyCount}",
            $"delayLoadKeys={delayLoadKeyCount}",
            $"regions={FormatList(distinctRegions, 6)}"
        };

        return new StructuredResourceMetadata(string.Join(" | ", parts));
    }

    private static StructuredResourceMetadata DescribeSkintone(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream);

        var version = reader.ReadUInt32();
        if (version != 6)
        {
            throw new InvalidDataException($"Unsupported Skintone version {version}.");
        }

        var textureInstance = reader.ReadUInt64();
        var overlayTextureCount = reader.ReadUInt32();
        for (var index = 0; index < overlayTextureCount; index++)
        {
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt64();
        }

        var colorize = reader.ReadUInt32();
        var overlayOpacity = reader.ReadUInt32();
        var tagCount = reader.ReadUInt32();
        for (var index = 0; index < tagCount; index++)
        {
            _ = reader.ReadUInt16();
            _ = reader.ReadUInt16();
        }

        var makeupOpacity = reader.ReadSingle();
        var swatchColorCount = reader.ReadByte();
        var swatchColors = new uint[swatchColorCount];
        for (var index = 0; index < swatchColorCount; index++)
        {
            swatchColors[index] = reader.ReadUInt32();
        }

        var displayIndex = reader.ReadSingle();
        var makeupOpacity2 = stream.Position + 4 <= stream.Length
            ? reader.ReadSingle()
            : (float?)null;

        var parts = new List<string>
        {
            $"Skintone v{version}",
            $"baseTexture=0x{textureInstance:X16}",
            $"overlays={overlayTextureCount}",
            $"tags={tagCount}",
            $"swatches={swatchColorCount}",
            $"displayIndex={displayIndex.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"makeupOpacity={makeupOpacity.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"overlayOpacity={overlayOpacity}",
            $"colorize=0x{colorize:X8}"
        };

        if (makeupOpacity2.HasValue)
        {
            parts.Add($"makeupOpacity2={makeupOpacity2.Value.ToString("0.###", CultureInfo.InvariantCulture)}");
        }

        if (swatchColors.Length > 0)
        {
            parts.Add($"swatchColors={FormatList(swatchColors.Select(static color => $"#{color:X8}"), 4)}");
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
