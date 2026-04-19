using System.IO;
using System.Text;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Assets;

public sealed record Ts4SimInfoSeedMetadata(string DisplayName, string Description);

internal sealed record Ts4SimModifierEntry(byte ChannelId, float Value);
internal sealed record Ts4SimOutfitPart(uint BodyType, ulong PartInstance);
internal sealed record Ts4SimOutfit(uint CategoryValue, IReadOnlyList<Ts4SimOutfitPart> Parts)
{
    public string CategoryLabel => FormatCategory(CategoryValue);

    private static string FormatCategory(uint value) => value switch
    {
        0 => "Everyday",
        1 => "Formal",
        2 => "Athletic",
        3 => "Sleep",
        4 => "Party",
        5 => "Nude",
        6 => "Career",
        7 => "Situation",
        8 => "Uniform",
        9 => "Swimwear",
        10 => "Hot Weather",
        11 => "Cold Weather",
        _ => $"Category {value}"
    };
}
internal sealed record Ts4SimPeltLayer(ulong Instance, uint Variant);

internal sealed record Ts4SimInfo(
    uint Version,
    uint AgeFlags,
    uint GenderFlags,
    uint SpeciesValue,
    ulong SkintoneInstance,
    float? SkintoneShift,
    int PronounCount,
    int OutfitCategoryCount,
    int OutfitEntryCount,
    int OutfitPartCount,
    int TraitCount,
    int FaceModifierCount,
    int BodyModifierCount,
    int GeneticFaceModifierCount,
    int GeneticBodyModifierCount,
    int SculptCount,
    int GeneticSculptCount,
    int GeneticPartCount,
    int GrowthPartCount,
    int PeltLayerCount,
    IReadOnlyList<Ts4SimPeltLayer> PeltLayers,
    IReadOnlyList<byte> SculptChannels,
    IReadOnlyList<Ts4SimModifierEntry> FaceModifiers,
    IReadOnlyList<Ts4SimModifierEntry> BodyModifiers,
    IReadOnlyList<Ts4SimOutfit> Outfits,
    IReadOnlyList<Ts4SimOutfitPart> OutfitParts,
    IReadOnlyList<byte> GeneticSculptChannels,
    IReadOnlyList<Ts4SimModifierEntry> GeneticFaceModifiers,
    IReadOnlyList<Ts4SimModifierEntry> GeneticBodyModifiers,
    IReadOnlyList<uint> GeneticPartBodyTypes,
    IReadOnlyList<uint> GrowthPartBodyTypes)
{
    public string SpeciesLabel => FormatSpecies(SpeciesValue);
    public string AgeLabel => FormatAge(AgeFlags);
    public string GenderLabel => FormatGender(GenderFlags);

    public string BuildDisplayName(ulong fullInstance)
    {
        var descriptors = new List<string>();
        descriptors.Add(SpeciesLabel);

        if (!string.Equals(AgeLabel, "Unknown", StringComparison.Ordinal))
        {
            descriptors.Add(AgeLabel);
        }

        if (!string.Equals(GenderLabel, "Unknown", StringComparison.Ordinal))
        {
            descriptors.Add(GenderLabel);
        }

        if (OutfitCategoryCount > 0 || OutfitEntryCount > 0 || OutfitPartCount > 0)
        {
            descriptors.Add($"outfits {OutfitCategoryCount}/{OutfitEntryCount}/{OutfitPartCount}");
        }

        if (TraitCount > 0)
        {
            descriptors.Add($"traits {TraitCount}");
        }

        var templateLabel = string.Equals(SpeciesLabel, "Human", StringComparison.Ordinal)
            ? "SimInfo template"
            : "Character template";
        return $"{templateLabel}: {string.Join(" | ", descriptors)} [{(uint)(fullInstance & 0xFFFFFFFF):X8}]";
    }

    public string BuildDescription()
    {
        var parts = new List<string>
        {
            $"SimInfo v{Version}",
            $"species={SpeciesLabel}",
            $"age={AgeLabel}",
            $"gender={GenderLabel}",
            $"outfits={OutfitCategoryCount} categories / {OutfitEntryCount} entries / {OutfitPartCount} parts",
            $"traits={TraitCount}",
            $"sculpts={SculptCount}",
            $"faceMods={FaceModifierCount}",
            $"bodyMods={BodyModifierCount}",
            $"geneticSculpts={GeneticSculptCount}",
            $"geneticFaceMods={GeneticFaceModifierCount}",
            $"geneticBodyMods={GeneticBodyModifierCount}",
            $"geneticParts={GeneticPartCount}",
            $"growthParts={GrowthPartCount}",
            $"peltLayers={PeltLayerCount}"
        };

        if (PronounCount > 0)
        {
            parts.Add($"pronouns={PronounCount}");
        }

        if (SkintoneInstance != 0)
        {
            parts.Add($"skintone={SkintoneInstance:X16}");
        }

        if (SkintoneShift.HasValue)
        {
            parts.Add($"skintoneShift={SkintoneShift.Value:0.###}");
        }

        return string.Join(" | ", parts);
    }

    public SimInfoSummary ToSummary() =>
        new(
            Version,
            SpeciesLabel,
            AgeLabel,
            GenderLabel,
            PronounCount,
            OutfitCategoryCount,
            OutfitEntryCount,
            OutfitPartCount,
            TraitCount,
            FaceModifierCount,
            BodyModifierCount,
            GeneticFaceModifierCount,
            GeneticBodyModifierCount,
            SculptCount,
            GeneticSculptCount,
            GeneticPartCount,
            GrowthPartCount,
            PeltLayerCount,
            SkintoneInstance == 0 ? null : $"{SkintoneInstance:X16}",
            SkintoneShift);

    private static string FormatSpecies(uint value) => value switch
    {
        0 => "Human",
        1 => "Human",
        2 => "Dog",
        3 => "Cat",
        4 => "Little Dog",
        5 => "Fox",
        6 => "Horse",
        _ => $"Species 0x{value:X8}"
    };

    private static string FormatAge(uint flags)
    {
        var labels = new List<string>();
        AppendFlagLabel(labels, flags, 0x00000001u, "Baby");
        AppendFlagLabel(labels, flags, 0x00000080u, "Infant");
        AppendFlagLabel(labels, flags, 0x00000002u, "Toddler");
        AppendFlagLabel(labels, flags, 0x00000004u, "Child");
        AppendFlagLabel(labels, flags, 0x00000008u, "Teen");
        AppendFlagLabel(labels, flags, 0x00000010u, "Young Adult");
        AppendFlagLabel(labels, flags, 0x00000020u, "Adult");
        AppendFlagLabel(labels, flags, 0x00000040u, "Elder");
        return labels.Count == 0 ? "Unknown" : string.Join(" / ", labels);
    }

    private static string FormatGender(uint flags)
    {
        var hasMale = (flags & 0x00001000u) != 0;
        var hasFemale = (flags & 0x00002000u) != 0;
        return (hasMale, hasFemale) switch
        {
            (true, true) => "Unisex",
            (true, false) => "Male",
            (false, true) => "Female",
            _ => "Unknown"
        };
    }

    private static void AppendFlagLabel(List<string> labels, uint value, uint flag, string label)
    {
        if ((value & flag) != 0)
        {
            labels.Add(label);
        }
    }
}

internal static class Ts4SimInfoParser
{
    public static Ts4SimInfo Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var version = reader.ReadUInt32();
        var linkTableOffset = reader.ReadUInt32(); // trailing link-table offset relative to this position
        var payloadStart = stream.Position;
        var linkTable = TryReadLinkTable(reader, stream, payloadStart, linkTableOffset);

        SkipBytes(stream, 8L * sizeof(float), "SimInfo physique");
        var ageFlags = reader.ReadUInt32();
        var genderFlags = reader.ReadUInt32();

        var speciesValue = 1u;
        if (version > 18)
        {
            speciesValue = reader.ReadUInt32();
            _ = reader.ReadUInt32();
        }

        var pronounCount = 0;
        ulong skintoneInstance = 0;
        float? skintoneShift = null;
        var peltLayerCount = 0;
        var peltLayers = new List<Ts4SimPeltLayer>();
        var sculptCount = 0;
        var sculptChannels = new List<byte>();
        var faceModifierCount = 0;
        var faceModifiers = new List<Ts4SimModifierEntry>();
        var bodyModifierCount = 0;
        var bodyModifiers = new List<Ts4SimModifierEntry>();
        var outfitCategoryCount = 0;
        var outfitEntryCount = 0;
        var outfitPartCount = 0;
        var outfits = new List<Ts4SimOutfit>();
        var outfitParts = new List<Ts4SimOutfitPart>();
        var geneticSculptCount = 0;
        var geneticSculptChannels = new List<byte>();
        var geneticFaceModifierCount = 0;
        var geneticFaceModifiers = new List<Ts4SimModifierEntry>();
        var geneticBodyModifierCount = 0;
        var geneticBodyModifiers = new List<Ts4SimModifierEntry>();
        var geneticPartCount = 0;
        var geneticPartBodyTypes = new List<uint>();
        var growthPartCount = 0;
        var growthPartBodyTypes = new List<uint>();
        var traitCount = 0;

        try
        {
            if (version >= 32)
            {
                pronounCount = ReadCount(reader.ReadInt32(), 2048, "SimInfo pronoun count");
                for (var index = 0; index < pronounCount; index++)
                {
                    var grammaticalCase = reader.ReadUInt32();
                    if (grammaticalCase > 0)
                    {
                        _ = reader.ReadString();
                    }
                }
            }

            skintoneInstance = reader.ReadUInt64();
            if (version >= 28)
            {
                skintoneShift = reader.ReadSingle();
            }

            if (version > 19)
            {
                peltLayerCount = reader.ReadByte();
                for (var index = 0; index < peltLayerCount; index++)
                {
                    peltLayers.Add(new Ts4SimPeltLayer(reader.ReadUInt64(), reader.ReadUInt32()));
                }
            }

            sculptCount = reader.ReadByte();
            for (var index = 0; index < sculptCount; index++)
            {
                sculptChannels.Add(reader.ReadByte());
            }

            faceModifierCount = reader.ReadByte();
            for (var index = 0; index < faceModifierCount; index++)
            {
                faceModifiers.Add(new Ts4SimModifierEntry(reader.ReadByte(), reader.ReadSingle()));
            }

            bodyModifierCount = reader.ReadByte();
            for (var index = 0; index < bodyModifierCount; index++)
            {
                bodyModifiers.Add(new Ts4SimModifierEntry(reader.ReadByte(), reader.ReadSingle()));
            }

            SkipBytes(stream, 24L, "SimInfo voice and unknown block");

            outfitCategoryCount = ReadCount(reader.ReadUInt32(), 65535, "SimInfo outfit category count");
            for (var outfitIndex = 0; outfitIndex < outfitCategoryCount; outfitIndex++)
            {
                var categoryValue = reader.ReadByte();
                _ = reader.ReadUInt32();
                var outfitCount = ReadCount(reader.ReadUInt32(), 65535, "SimInfo outfit entry count");
                outfitEntryCount += outfitCount;
                for (var entryIndex = 0; entryIndex < outfitCount; entryIndex++)
                {
                    _ = reader.ReadUInt64();
                    _ = reader.ReadUInt64();
                    _ = reader.ReadUInt64();
                    _ = reader.ReadBoolean();
                    var partCount = ReadCount(reader.ReadUInt32(), 65535, "SimInfo outfit part count");
                    outfitPartCount += partCount;
                    var entryParts = new List<Ts4SimOutfitPart>(partCount);
                    for (var partIndex = 0; partIndex < partCount; partIndex++)
                    {
                        if (!TryReadByte(reader, out int linkIndex) ||
                            !TryReadUInt32(reader, out uint bodyType))
                        {
                            return BuildPartial();
                        }

                        if (version >= 32 && !TrySkipBytes(stream, sizeof(ulong)))
                        {
                            return BuildPartial();
                        }

                        if (TryResolveLink(linkTable, (byte)linkIndex, out var partKey))
                        {
                            var part = new Ts4SimOutfitPart(bodyType, partKey.FullInstance);
                            outfitParts.Add(part);
                            entryParts.Add(part);
                        }
                    }

                    outfits.Add(new Ts4SimOutfit(categoryValue, entryParts));
                }
            }

            if (!TryReadByte(reader, out geneticSculptCount))
            {
                return BuildPartial();
            }

            for (var index = 0; index < geneticSculptCount; index++)
            {
                if (!TryReadByte(reader, out int sculptChannel))
                {
                    return BuildPartial();
                }

                geneticSculptChannels.Add((byte)sculptChannel);
            }

            if (!TryReadByte(reader, out geneticFaceModifierCount))
            {
                return BuildPartial();
            }

            for (var index = 0; index < geneticFaceModifierCount; index++)
            {
                if (!TryReadByte(reader, out int channelId) ||
                    !TryReadSingle(reader, out float value))
                {
                    return BuildPartial();
                }

                geneticFaceModifiers.Add(new Ts4SimModifierEntry((byte)channelId, value));
            }

            if (!TryReadByte(reader, out geneticBodyModifierCount))
            {
                return BuildPartial();
            }

            for (var index = 0; index < geneticBodyModifierCount; index++)
            {
                if (!TryReadByte(reader, out int channelId) ||
                    !TryReadSingle(reader, out float value))
                {
                    return BuildPartial();
                }

                geneticBodyModifiers.Add(new Ts4SimModifierEntry((byte)channelId, value));
            }

            if (!TrySkipBytes(stream, 16L))
            {
                return BuildPartial();
            }

            if (!TryReadByte(reader, out geneticPartCount))
            {
                return BuildPartial();
            }

            for (var index = 0; index < geneticPartCount; index++)
            {
                if (!TryReadByte(reader, out _) ||
                    !TryReadUInt32(reader, out uint bodyType))
                {
                    return BuildPartial();
                }

                geneticPartBodyTypes.Add(bodyType);
            }

            if (version >= 32)
            {
                if (!TryReadByte(reader, out growthPartCount))
                {
                    return BuildPartial();
                }

                for (var index = 0; index < growthPartCount; index++)
                {
                    if (!TryReadByte(reader, out _) ||
                        !TryReadUInt32(reader, out uint bodyType))
                    {
                        return BuildPartial();
                    }

                    growthPartBodyTypes.Add(bodyType);
                }
            }

            if (!TrySkipBytes(stream, 17L))
            {
                return BuildPartial();
            }

            if (version >= 32 && !TrySkipBytes(stream, 3L))
            {
                return BuildPartial();
            }

            if (!TryReadByte(reader, out traitCount))
            {
                return BuildPartial();
            }

            if (!TrySkipBytes(stream, traitCount * 8L))
            {
                return BuildPartial();
            }
        }
        catch (InvalidDataException)
        {
            return BuildPartial();
        }
        catch (EndOfStreamException)
        {
            return BuildPartial();
        }
        catch (ArgumentOutOfRangeException)
        {
            return BuildPartial();
        }

        return BuildPartial();

        Ts4SimInfo BuildPartial() =>
            new(
                version,
                ageFlags,
                genderFlags,
                speciesValue,
                skintoneInstance,
                skintoneShift,
                pronounCount,
                outfitCategoryCount,
                outfitEntryCount,
                outfitPartCount,
                traitCount,
                faceModifierCount,
                bodyModifierCount,
                geneticFaceModifierCount,
                geneticBodyModifierCount,
                sculptCount,
                geneticSculptCount,
                geneticPartCount,
                growthPartCount,
                peltLayerCount,
                peltLayers,
                sculptChannels,
                faceModifiers,
                bodyModifiers,
                outfits,
                outfitParts,
                geneticSculptChannels,
                geneticFaceModifiers,
                geneticBodyModifiers,
                geneticPartBodyTypes,
                growthPartBodyTypes);
    }

    public static Ts4SimInfoSeedMetadata BuildSeedMetadata(ResourceMetadata resource, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var metadata = Parse(bytes);
        return new Ts4SimInfoSeedMetadata(
            metadata.BuildDisplayName(resource.Key.FullInstance),
            metadata.BuildDescription());
    }

    public static string? TryExtractSpeciesLabelFromSummary(string? description) =>
        TryExtractSummaryField(description, "species");

    public static int? TryExtractVersionFromSummary(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        const string prefix = "SimInfo v";
        if (!description.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var digits = new StringBuilder();
        foreach (var character in description[prefix.Length..])
        {
            if (!char.IsDigit(character))
            {
                break;
            }

            digits.Append(character);
        }

        return digits.Length > 0 && int.TryParse(digits.ToString(), out var version)
            ? version
            : null;
    }

    public static string? TryExtractAgeLabelFromSummary(string? description) =>
        TryExtractSummaryField(description, "age");

    public static string? TryExtractGenderLabelFromSummary(string? description) =>
        TryExtractSummaryField(description, "gender");

    public static string? TryExtractOutfitSummaryFromDescription(string? description) =>
        TryExtractSummaryField(description, "outfits");

    public static string? TryExtractTraitCountFromSummary(string? description) =>
        TryExtractSummaryField(description, "traits");

    public static (int? CategoryCount, int? EntryCount, int? PartCount) TryExtractOutfitCountsFromSummary(string? description)
    {
        var outfits = TryExtractOutfitSummaryFromDescription(description);
        if (string.IsNullOrWhiteSpace(outfits))
        {
            return (null, null, null);
        }

        const string categoriesMarker = " categories / ";
        const string entriesMarker = " entries / ";
        var categorySeparator = outfits.IndexOf(categoriesMarker, StringComparison.Ordinal);
        if (categorySeparator < 0)
        {
            return (null, null, null);
        }

        var entrySeparator = outfits.IndexOf(entriesMarker, categorySeparator + categoriesMarker.Length, StringComparison.Ordinal);
        if (entrySeparator < 0)
        {
            return (null, null, null);
        }

        var categoriesText = outfits[..categorySeparator].Trim();
        var entriesText = outfits[(categorySeparator + categoriesMarker.Length)..entrySeparator].Trim();
        var partsText = outfits[(entrySeparator + entriesMarker.Length)..].Trim();
        if (partsText.EndsWith(" parts", StringComparison.OrdinalIgnoreCase))
        {
            partsText = partsText[..^6].Trim();
        }

        return (
            int.TryParse(categoriesText, out var categoryCount) ? categoryCount : null,
            int.TryParse(entriesText, out var entryCount) ? entryCount : null,
            int.TryParse(partsText, out var partCount) ? partCount : null);
    }

    public static int? TryExtractOutfitPartCountFromSummary(string? description)
    {
        var (_, _, partCount) = TryExtractOutfitCountsFromSummary(description);
        return partCount;
    }

    public static int? TryExtractOutfitCategoryCountFromSummary(string? description)
    {
        var (categoryCount, _, _) = TryExtractOutfitCountsFromSummary(description);
        return categoryCount;
    }

    public static int? TryExtractOutfitEntryCountFromSummary(string? description)
    {
        var (_, entryCount, _) = TryExtractOutfitCountsFromSummary(description);
        return entryCount;
    }

    public static int? TryExtractFaceModifierCountFromSummary(string? description) =>
        TryExtractIntSummaryField(description, "faceMods");

    public static int? TryExtractBodyModifierCountFromSummary(string? description) =>
        TryExtractIntSummaryField(description, "bodyMods");

    public static int? TryExtractSculptCountFromSummary(string? description) =>
        TryExtractIntSummaryField(description, "sculpts");

    public static string? TryExtractSkintoneInstanceFromSummary(string? description) =>
        TryExtractSummaryField(description, "skintone");

    public static bool IsLegacyOpaqueSpeciesSummary(string? description)
    {
        var version = TryExtractVersionFromSummary(description);
        var species = TryExtractSpeciesLabelFromSummary(description);
        return version.HasValue &&
               version.Value <= 20 &&
               !string.IsNullOrWhiteSpace(species) &&
               species.StartsWith("Species 0x", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildDisplayNameFromSummary(string? description, ulong fullInstance, string? fallbackDisplayName = null)
    {
        var descriptors = new List<string>();
        var species = TryExtractSpeciesLabelFromSummary(description);
        var age = TryExtractAgeLabelFromSummary(description);
        var gender = TryExtractGenderLabelFromSummary(description);
        var outfits = TryExtractOutfitSummaryFromDescription(description);
        var traits = TryExtractTraitCountFromSummary(description);

        if (!string.IsNullOrWhiteSpace(species))
        {
            descriptors.Add(species);
        }

        if (!string.IsNullOrWhiteSpace(age) && !string.Equals(age, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            descriptors.Add(age);
        }

        if (!string.IsNullOrWhiteSpace(gender) && !string.Equals(gender, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            descriptors.Add(gender);
        }

        if (!string.IsNullOrWhiteSpace(outfits))
        {
            descriptors.Add($"outfits {CompressOutfitSummary(outfits)}");
        }

        if (!string.IsNullOrWhiteSpace(traits))
        {
            descriptors.Add($"traits {traits}");
        }

        if (descriptors.Count == 0)
        {
            return fallbackDisplayName ?? $"SimInfo template [{(uint)(fullInstance & 0xFFFFFFFF):X8}]";
        }

        var templateLabel = string.Equals(species, "Human", StringComparison.OrdinalIgnoreCase)
            ? "SimInfo template"
            : "Character template";
        return $"{templateLabel}: {string.Join(" | ", descriptors)} [{(uint)(fullInstance & 0xFFFFFFFF):X8}]";
    }

    public static string BuildArchetypeDisplayName(string species, string age, string gender)
    {
        var descriptors = new List<string>();
        if (!string.IsNullOrWhiteSpace(species))
        {
            descriptors.Add(species);
        }

        if (!string.IsNullOrWhiteSpace(age) && !string.Equals(age, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            descriptors.Add(age);
        }

        if (!string.IsNullOrWhiteSpace(gender) && !string.Equals(gender, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            descriptors.Add(gender);
        }

        var label = string.Equals(species, "Human", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(species, "Sim", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(species, "Unknown", StringComparison.OrdinalIgnoreCase)
            ? "Sim archetype"
            : "Character archetype";
        return descriptors.Count == 0
            ? $"{label}"
            : $"{label}: {string.Join(" | ", descriptors)}";
    }

    public static string BuildArchetypeLogicalKey(string species, string age, string gender)
    {
        var normalizedSpecies = NormalizeArchetypePart(species);
        var normalizedAge = NormalizeArchetypePart(age);
        var normalizedGender = NormalizeArchetypePart(gender);
        return $"sim-archetype:{normalizedSpecies}|{normalizedAge}|{normalizedGender}";
    }

    private static string? TryExtractSummaryField(string? description, string key)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var prefix = $"{key}=";
        foreach (var part in description.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return part[prefix.Length..].Trim();
            }
        }

        return null;
    }

    private static int? TryExtractIntSummaryField(string? description, string key)
    {
        var value = TryExtractSummaryField(description, key);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string CompressOutfitSummary(string summary)
    {
        var normalized = summary.Trim();
        const string suffix = " categories / ";
        var firstSeparator = normalized.IndexOf(suffix, StringComparison.Ordinal);
        if (firstSeparator < 0)
        {
            return normalized;
        }

        var entriesMarker = " entries / ";
        var secondSeparator = normalized.IndexOf(entriesMarker, firstSeparator + suffix.Length, StringComparison.Ordinal);
        if (secondSeparator < 0)
        {
            return normalized;
        }

        var categories = normalized[..firstSeparator].Trim();
        var entries = normalized[(firstSeparator + suffix.Length)..secondSeparator].Trim();
        var partsText = normalized[(secondSeparator + entriesMarker.Length)..].Trim();
        if (partsText.EndsWith(" parts", StringComparison.Ordinal))
        {
            partsText = partsText[..^" parts".Length].Trim();
        }

        return $"{categories}/{entries}/{partsText}";
    }

    private static string NormalizeArchetypePart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        return value
            .Trim()
            .ToLowerInvariant()
            .Replace(" / ", "-", StringComparison.Ordinal)
            .Replace(' ', '-');
    }

    private static bool TryReadByte(BinaryReader reader, out int value)
    {
        if (reader.BaseStream.Position >= reader.BaseStream.Length)
        {
            value = 0;
            return false;
        }

        value = reader.ReadByte();
        return true;
    }

    private static bool TryReadSingle(BinaryReader reader, out float value)
    {
        if (reader.BaseStream.Position > reader.BaseStream.Length - sizeof(float))
        {
            value = default;
            return false;
        }

        value = reader.ReadSingle();
        return true;
    }

    private static bool TryReadUInt32(BinaryReader reader, out uint value)
    {
        if (reader.BaseStream.Position > reader.BaseStream.Length - sizeof(uint))
        {
            value = default;
            return false;
        }

        value = reader.ReadUInt32();
        return true;
    }

    private static bool TrySkipBytes(Stream stream, long count)
    {
        if (count < 0 || stream.Position > stream.Length - count)
        {
            return false;
        }

        stream.Position += count;
        return true;
    }

    private static int ReadCount(int value, int max, string label)
    {
        if (value < 0 || value > max)
        {
            throw new InvalidDataException($"{label} {value} is outside the supported range.");
        }

        return value;
    }

    private static int ReadCount(uint value, int max, string label)
    {
        if (value > max)
        {
            throw new InvalidDataException($"{label} {value} is outside the supported range.");
        }

        return (int)value;
    }

    private static void SkipBytes(Stream stream, long count, string label)
    {
        if (count < 0)
        {
            throw new InvalidDataException($"{label} declared a negative size.");
        }

        if (stream.Position > stream.Length - count)
        {
            throw new EndOfStreamException($"{label} extends beyond the SimInfo payload.");
        }

        stream.Position += count;
    }

    private static IReadOnlyList<ResourceKeyRecord> TryReadLinkTable(BinaryReader reader, Stream stream, long payloadStart, uint linkTableOffset)
    {
        if (linkTableOffset == 0)
        {
            return [];
        }

        var linkTablePosition = payloadStart + linkTableOffset;
        if (linkTablePosition < payloadStart || linkTablePosition > stream.Length - sizeof(byte))
        {
            return [];
        }

        var returnPosition = stream.Position;
        try
        {
            stream.Position = linkTablePosition;
            if (!TryReadByte(reader, out int linkCount))
            {
                return [];
            }

            var links = new List<ResourceKeyRecord>(linkCount);
            for (var index = 0; index < linkCount; index++)
            {
                if (stream.Position > stream.Length - (sizeof(ulong) + (2L * sizeof(uint))))
                {
                    return [];
                }

                var instance = reader.ReadUInt64();
                var group = reader.ReadUInt32();
                var type = reader.ReadUInt32();
                links.Add(new ResourceKeyRecord(type, group, instance, GuessResourceTypeName(type)));
            }

            return links;
        }
        finally
        {
            stream.Position = returnPosition;
        }
    }

    private static bool TryResolveLink(IReadOnlyList<ResourceKeyRecord> linkTable, byte linkIndex, out ResourceKeyRecord key)
    {
        if (linkIndex >= linkTable.Count)
        {
            key = default!;
            return false;
        }

        key = linkTable[linkIndex];
        return key.Type != 0 || key.Group != 0 || key.FullInstance != 0;
    }

    private static string GuessResourceTypeName(uint type) => type switch
    {
        0x015A1849u => "Geometry",
        0x034AEECBu => "CASPart",
        0x0354796Au => "Sculpt",
        0xB52F5055u => "SimModifier",
        _ => $"0x{type:X8}"
    };
}
