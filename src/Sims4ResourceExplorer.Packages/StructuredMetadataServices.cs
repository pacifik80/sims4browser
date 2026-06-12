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

/// <summary>
/// One TONE skin-set entry (v10+ layout). Index 0 is the base/default state; indexes 1..2 are
/// the tan / burn state variants per TS4SimRipper <c>TONE.SkinSetDesc</c> (TONE.cs:326-369):
/// <c>{ textureInstance:u64, overlayInstance:u64, overlayMultiplier:f32, makeupOpacity:f32,
/// makeupOpacity2:f32 }</c>.
/// </summary>
public sealed record Ts4SkintoneSkinSet(
    ulong TextureInstance,
    ulong OverlayInstance,
    float OverlayMultiplier,
    float MakeupOpacity,
    float MakeupOpacity2);

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
    float? MakeupOpacity2,
    // v10+ structured fields (TS4SimRipper TONE.cs layout). For v<10 a single synthesized
    // skin set mirrors SimRipper's upgrade behavior. Sliders are the CAS skintone-shift
    // range that SimInfo.SkintoneShift is applied against (v11+ only).
    IReadOnlyList<Ts4SkintoneSkinSet>? SkinSets = null,
    ulong TuningInstance = 0,
    ushort SkinPanel = 0,
    float? SliderLow = null,
    float? SliderHigh = null,
    float? SliderIncrement = null,
    int TrailingByteCount = 0);

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

        // Layout source: TS4SimRipper TONE.cs `TONE(BinaryReader)` (docs/references/external/
        // TS4SimRipper/src/TONE.cs:214-276), which reads versions 6, 7, 8, 10, 11. Game
        // libraries observed in 2026 ship v6, v10, and v12; v12 follows the v11 layout with a
        // (currently unidentified) trailing block we surface as TrailingByteCount.
        var version = reader.ReadUInt32();
        if (version is not (6 or 7 or 8 or 10 or 11 or 12))
        {
            throw new InvalidDataException($"Unsupported Skintone version {version}.");
        }

        // v10+ replaces the single base-texture instance with a skin-set list:
        // { textureInstance:u64, overlayInstance:u64, overlayMultiplier:f32, makeupOpacity:f32,
        // makeupOpacity2:f32 } × count. Set 0 is the base state; sets 1..2 are tan/burn.
        var skinSets = new List<Ts4SkintoneSkinSet>(3);
        ulong legacyTextureInstance = 0;
        if (version >= 10)
        {
            var skinSetCount = reader.ReadByte();
            for (var index = 0; index < skinSetCount; index++)
            {
                skinSets.Add(new Ts4SkintoneSkinSet(
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()));
            }
        }
        else
        {
            legacyTextureInstance = reader.ReadUInt64();
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
        // version >= 7.
        var tagValueSize = version >= 7 ? 4 : 2;
        for (var index = 0; index < tagCount; index++)
        {
            _ = reader.ReadUInt16();   // flagCategory
            for (var b = 0; b < tagValueSize; b++) _ = reader.ReadByte();
        }

        // v<10 carries a top-level `makeupOpacity:Float` here; v10+ moved it into the
        // skin-set entries (SimRipper exposes skinSets[0].MakeupOpacity as the value).
        var legacyMakeupOpacity = version < 10 ? reader.ReadSingle() : 0f;
        var swatchColorCount = reader.ReadByte();
        var swatchColors = new uint[swatchColorCount];
        for (var index = 0; index < swatchColorCount; index++)
        {
            swatchColors[index] = reader.ReadUInt32();
        }

        var displayIndex = reader.ReadSingle();   // SimRipper field name: sortOrder
        float? legacyMakeupOpacity2 = null;
        if (version < 10 && stream.Position + 4 <= stream.Length)
        {
            legacyMakeupOpacity2 = reader.ReadSingle();
        }

        var tuningInstance = 0ul;
        if (version >= 8 && stream.Position + 8 <= stream.Length)
        {
            tuningInstance = reader.ReadUInt64();
        }

        // v11+ trailing block: skinPanel:u16 + sliderLow:f32 + sliderHigh:f32 +
        // sliderIncrement:f32 — the CAS skintone-shift slider range that the SimInfo
        // SkintoneShift value is applied within (SkinBlender's `shift` input).
        ushort skinPanel = 0;
        float? sliderLow = null, sliderHigh = null, sliderIncrement = null;
        if (version >= 11 && stream.Position + 14 <= stream.Length)
        {
            skinPanel = reader.ReadUInt16();
            sliderLow = reader.ReadSingle();
            sliderHigh = reader.ReadSingle();
            sliderIncrement = reader.ReadSingle();
        }

        var trailingByteCount = checked((int)(stream.Length - stream.Position));

        if (version < 10)
        {
            // Mirror SimRipper's legacy upgrade: synthesize skin set 0 from the flat fields.
            skinSets.Add(new Ts4SkintoneSkinSet(
                legacyTextureInstance,
                0ul,
                1f,
                legacyMakeupOpacity,
                legacyMakeupOpacity2 ?? legacyMakeupOpacity));
        }

        var baseTextureInstance = skinSets.Count > 0 ? skinSets[0].TextureInstance : legacyTextureInstance;
        var makeupOpacity = version < 10
            ? legacyMakeupOpacity
            : (skinSets.Count > 0 ? skinSets[0].MakeupOpacity : 0f);

        return new Ts4Skintone(
            version,
            baseTextureInstance,
            overlays,
            colorize,
            overlayOpacity,
            checked((int)tagCount),
            makeupOpacity,
            swatchColors,
            displayIndex,
            legacyMakeupOpacity2,
            skinSets,
            tuningInstance,
            skinPanel,
            sliderLow,
            sliderHigh,
            sliderIncrement,
            trailingByteCount);
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

        if (skintone.SkinSets is { Count: > 0 } skinSets)
        {
            parts.Add($"skinSets={skinSets.Count}");
            for (var index = 0; index < skinSets.Count; index++)
            {
                var set = skinSets[index];
                parts.Add($"set{index}=tex 0x{set.TextureInstance:X16} overlay 0x{set.OverlayInstance:X16} mult {set.OverlayMultiplier.ToString("0.###", CultureInfo.InvariantCulture)}");
            }
        }

        if (skintone.TuningInstance != 0)
        {
            parts.Add($"tuning=0x{skintone.TuningInstance:X16}");
        }

        if (skintone.SliderLow.HasValue && skintone.SliderHigh.HasValue)
        {
            parts.Add($"shiftSlider=[{skintone.SliderLow.Value.ToString("0.###", CultureInfo.InvariantCulture)}, {skintone.SliderHigh.Value.ToString("0.###", CultureInfo.InvariantCulture)}] step {(skintone.SliderIncrement ?? 0).ToString("0.###", CultureInfo.InvariantCulture)} panel {skintone.SkinPanel}");
        }

        if (skintone.TrailingByteCount > 0)
        {
            parts.Add($"trailingBytes={skintone.TrailingByteCount}");
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
