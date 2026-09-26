// CharacterDefinition — the SELECTIONS: what the future Unity Sims4Character knobs pick from the
// catalog. A definition references catalog option ids (bodyMesh / baseTone / detailLayers / eyeColor)
// for a given age + gender. exportchar reads it, looks the ids up in catalog.json, and applies them.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sims4UnityExport;

internal sealed class CharacterDefinition
{
    [JsonPropertyName("age")] public string Age { get; set; } = "Adult";
    [JsonPropertyName("gender")] public string Gender { get; set; } = "Female";

    // Catalog option ids (the knobs).
    [JsonPropertyName("bodyMesh")] public string? BodyMesh { get; set; } // render source (its head is shared)
    [JsonPropertyName("baseTone")] public string? BaseTone { get; set; }
    [JsonPropertyName("detailLayers")] public List<string> DetailLayers { get; set; } = new();
    [JsonPropertyName("eyeColor")] public string? EyeColor { get; set; }

    // Per-region body-slot defaults (which option starts ACTIVE in each slot). Fall back to BodyMesh /
    // the region's first option when null.
    [JsonPropertyName("topMesh")] public string? TopMesh { get; set; }
    [JsonPropertyName("bottomMesh")] public string? BottomMesh { get; set; }
    [JsonPropertyName("feetMesh")] public string? FeetMesh { get; set; }
    // Base skin (color albedo) default. Falls back to the first base skin when null.
    [JsonPropertyName("baseSkin")] public string? BaseSkin { get; set; }

    private static readonly JsonSerializerOptions LoadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static CharacterDefinition Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CharacterDefinition>(json, LoadOptions)
            ?? throw new InvalidDataException($"CharacterDefinition at '{path}' deserialized to null.");
    }
}
