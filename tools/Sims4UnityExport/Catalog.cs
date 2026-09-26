// Catalog — the data model + loader for catalog.json, the registry of VETTED selectable options
// for one ageGender (here: Adult/Female). The catalog is authored data (tools/Sims4UnityExport/
// catalog.json) that the exporter READS; the CharacterDefinition (the selections) references option
// ids from it. This realizes the user's parameter system: vetted assets -> a catalog of options ->
// a CharacterDefinition picks them -> exportchar applies the selections.
//
// Design notes for the future Unity Sims4Character knobs + runtime skin shader:
//   * Each category is a flat list of options with a stable `id`. A knob is "pick an id".
//   * bodyMesh options carry the override TGIs (top/bottom/feet GEOM instances) the ModOverride
//     decorator needs; "ea_af" carries none (EA default, no override).
//   * baseTone options carry the TONE resource instance (toneInstance) the skin resolver consumes.
//   * detailLayer options carry the grayscale-relief texture TGIs (LRLE) + a blend mode; these are
//     the per-layer relief maps the runtime shader multiplies/overlays over the base.
//   * eyeColor options carry the EyeColor CAS part overlay instance (overlayInstance).

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sims4UnityExport;

internal sealed class Catalog
{
    [JsonPropertyName("ageGender")] public string AgeGender { get; set; } = string.Empty;
    [JsonPropertyName("bodyMesh")] public List<BodyMeshOption> BodyMesh { get; set; } = new();
    [JsonPropertyName("baseTone")] public List<BaseToneOption> BaseTone { get; set; } = new();
    [JsonPropertyName("detailLayer")] public List<DetailLayerOption> DetailLayer { get; set; } = new();
    [JsonPropertyName("eyeColor")] public List<EyeColorOption> EyeColor { get; set; } = new();
    // Full color SKIN sets (the base albedo): "game" = EA default; a package = a default-skin mod
    // whose color diffuse is served for the reused EA skintone instance.
    [JsonPropertyName("skinBase")] public List<SkinBaseOption> SkinBase { get; set; } = new();
    // Skin-detail NORMAL maps: each a real EA Sculpt bumpmap (or CC normalMapKey) DST, height→normal.
    [JsonPropertyName("skinNormals")] public List<SkinNormalOption> SkinNormals { get; set; } = new();
    // Body/face MORPH sliders: each is an SMOD whose BGEO is applied at weight 1 → a Unity blend shape.
    [JsonPropertyName("bodyMorphs")] public List<BodyMorphOption> BodyMorphs { get; set; } = new();

    private static readonly JsonSerializerOptions LoadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Catalog Load(string path)
    {
        var json = File.ReadAllText(path);
        var catalog = JsonSerializer.Deserialize<Catalog>(json, LoadOptions)
            ?? throw new InvalidDataException($"catalog.json at '{path}' deserialized to null.");
        return catalog;
    }

    public BodyMeshOption? FindBodyMesh(string? id) =>
        BodyMesh.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));

    public BaseToneOption? FindBaseTone(string? id) =>
        BaseTone.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));

    public DetailLayerOption? FindDetailLayer(string? id) =>
        DetailLayer.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));

    public EyeColorOption? FindEyeColor(string? id) =>
        EyeColor.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
}

internal sealed class BodyMeshOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    // Override GEOM TGIs (Type:Group:Instance hex) for top/bottom/feet. Null/empty = EA default.
    [JsonPropertyName("overrides")] public BodyMeshOverrides? Overrides { get; set; }
}

internal sealed class BodyMeshOverrides
{
    [JsonPropertyName("top")] public string? Top { get; set; }
    [JsonPropertyName("bottom")] public string? Bottom { get; set; }
    [JsonPropertyName("feet")] public string? Feet { get; set; }
    // The EA default-female part GEOM instances the override REDIRECTS from (EA recipe asks these).
    // Group is ignored on the request side, so only Type:Instance matters; full TGI kept for clarity.
    [JsonPropertyName("eaTop")] public string? EaTop { get; set; }
    [JsonPropertyName("eaBottom")] public string? EaBottom { get; set; }
    [JsonPropertyName("eaFeet")] public string? EaFeet { get; set; }
    // The mod package files that define the override GEOMs (relative to repo ModsFromDev root).
    [JsonPropertyName("topPackage")] public string? TopPackage { get; set; }
    [JsonPropertyName("bottomPackage")] public string? BottomPackage { get; set; }
    [JsonPropertyName("feetPackage")] public string? FeetPackage { get; set; }
}

internal sealed class BaseToneOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    // The TONE resource full instance id (e.g. "0x0000000000005545"); consumed by the skin resolver.
    [JsonPropertyName("toneInstance")] public string ToneInstance { get; set; } = string.Empty;

    public ulong ToneInstanceValue => ParseHexU64(ToneInstance);

    internal static ulong ParseHexU64(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0ul;
        var hex = s.Trim().Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase).TrimStart('#');
        return ulong.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0ul;
    }
}

internal sealed class DetailLayerOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    // Grayscale-relief texture TGIs (Type:Group:Instance hex). The exporter resolves + exports the
    // FIRST one as the layer PNG; the rest are recorded for provenance / future per-region selection.
    [JsonPropertyName("textureTgis")] public List<string> TextureTgis { get; set; } = new();
    // The mod package file that defines the relief textures (under ModsFromDev).
    [JsonPropertyName("package")] public string? Package { get; set; }
    [JsonPropertyName("blend")] public string Blend { get; set; } = "overlay";
}

internal sealed class BodyMorphOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    // The SMOD (SimModifier) TGI "Type:Group:Instance" whose morph this applies.
    [JsonPropertyName("smod")] public string Smod { get; set; } = string.Empty;
    // "bgeo" (FACE, keyed by head vertex IDs) or "dmap" (BODY, keyed by UV1+tags). Default bgeo.
    [JsonPropertyName("kind")] public string Kind { get; set; } = "bgeo";
    // Bake weight: scales the morph so the 0..1 Unity slider's full value is a sane deformation.
    // Raw DMap body morphs over-shoot at weight 1 (EA applies a per-slider scale we don't read), so
    // big regions (hips/thighs/waist/weight) use < 1. Default 1.0.
    [JsonPropertyName("scale")] public float Scale { get; set; } = 1f;
}

internal sealed class SkinBaseOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    // "game" = EA default skin (no override). Otherwise a package path under ModsFromDev whose color
    // diffuse replaces the EA skintone base (Form-1 instance reuse).
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    [JsonPropertyName("package")] public string? Package { get; set; }
    // Color-overlay skins (Obscurus/PsBoss) carry their diffuse LRLE TGI(s) — resolved DIRECTLY as the
    // albedo. EA ("game", no tgis) uses the resolver's tone-tinted base instead.
    [JsonPropertyName("textureTgis")] public List<string> TextureTgis { get; set; } = new();
}

internal sealed class SkinNormalOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    // DST normal/bump source "Type:Instance" (group resolved from the index). Empty = Flat (no detail).
    // A real EA Sculpt bumpmap (or CC normalMapKey) — decoded as grayscale height, then height→normal.
    [JsonPropertyName("dst")] public string? Dst { get; set; }
    // Sobel strength for the height→normal conversion (EA bumps are subtle). Default 6.
    [JsonPropertyName("strength")] public float Strength { get; set; } = 6f;
}

internal sealed class EyeColorOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    // The EyeColor CAS part full instance id; the resolver reads its diffuse as the eye overlay.
    [JsonPropertyName("overlayInstance")] public string OverlayInstance { get; set; } = string.Empty;

    public ulong OverlayInstanceValue => BaseToneOption.ParseHexU64(OverlayInstance);
}
