// CharacterDefinitionExporter — the exportchar pipeline. Realizes the user's parameter system:
//   catalog.json (registry of VETTED options) + CharacterDefinition (the SELECTIONS) -> a Sim
//   assembled with the chosen bodyMesh override, whose skin is exported as SEPARATE UV-aligned
//   layer PNGs (base / detail / eye) for the runtime layered shader, PLUS a baked preview atlas
//   for the current Unity builder.
//
// Steps:
//   1. Load catalog.json + the CharacterDefinition; resolve the picked option records.
//   2. If the bodyMesh option carries overrides, ARM the ModOverride decorator (Better Body GEOMs
//      win by TGI) BEFORE resolving the Sim services — exactly as exportsimhd does.
//   3. Resolve the skintone render summary for the chosen baseTone (base texture + detail rows +
//      face CAS overlays), and assemble the unified Sim scene (the exportsim/exportsimhd path).
//   4. Resolve + write the SKIN LAYERS as separate UV-aligned PNGs into <slug>/Textures/:
//        - layer_base.png       : the base skin color (baseTone's resolved base texture)
//        - layer_detail_<id>.png: each selected detailLayer's relief map (resolved by catalog TGI)
//        - layer_eye.png        : the eye overlay (the chosen EyeColor CAS part diffuse)
//      Each diffuse-style layer runs through the transparent-pixel cleanup.
//   5. Bake a preview skin_atlas.png + skin_normal.png with the existing CORRECT compositor
//      (SkinAtlasComposer: base + selected detail relief rows + eye overlay, warm albedo chain).
//   6. Write character.json: mesh/skeleton/parts AS TODAY + the NEW layered "skin" section.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Packages;
using Sims4ResourceExplorer.Preview.SimRender;

namespace Sims4UnityExport;

// --- character.json skin section (NEW: layered, for the runtime shader) --------------------------
internal sealed class CharacterSkinLayer
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;   // base | detail | eye
    [JsonPropertyName("id")] public string? Id { get; set; }                       // detail option id
    [JsonPropertyName("texture")] public string Texture { get; set; } = string.Empty;
    [JsonPropertyName("blend")] public string? Blend { get; set; }                 // detail blend mode
}

internal sealed class CharacterSkinSection
{
    [JsonPropertyName("layers")] public List<CharacterSkinLayer> Layers { get; set; } = new();
    [JsonPropertyName("previewAtlas")] public string? PreviewAtlas { get; set; }
    [JsonPropertyName("previewNormal")] public string? PreviewNormal { get; set; }
}

// One selectable full SKIN set's base albedo. Normal/smoothness/SSS are the shared body material
// (set on the Unity side); only the albedo differs per skin. RAW — no detail/eye/tone compositing.
internal sealed class CharacterSkinBaseEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("albedo")] public string Albedo { get; set; } = string.Empty;
}

internal sealed class CharacterSkinBaseCatalog
{
    [JsonPropertyName("default")] public string? Default { get; set; }
    [JsonPropertyName("options")] public List<CharacterSkinBaseEntry> Options { get; set; } = new();
}

// One selectable skin-detail NORMAL map (REAL surface relief, height→normal from an EA Sculpt bumpmap
// or a CC normalMapKey DST). "normal" empty = the Flat (no detail) option. Shared body skin material.
internal sealed class CharacterSkinNormalEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("normal")] public string Normal { get; set; } = string.Empty;
}

internal sealed class CharacterSkinNormalCatalog
{
    [JsonPropertyName("default")] public string? Default { get; set; }
    [JsonPropertyName("options")] public List<CharacterSkinNormalEntry> Options { get; set; } = new();
}

// character.json contract for exportchar: same mesh/skeleton/parts as the exportsim manifest, PLUS
// the layered skin section. (Mirrors CharacterManifest field-for-field so the Unity builder reads it
// unchanged; the extra "selection"/"skin" fields are additive.)
internal sealed class CharacterDefManifest
{
    [JsonPropertyName("asset")] public string Asset { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; set; } = "sim";
    [JsonPropertyName("age")] public string Age { get; set; } = string.Empty;
    [JsonPropertyName("gender")] public string Gender { get; set; } = string.Empty;
    [JsonPropertyName("selection")] public CharacterSelection Selection { get; set; } = new();
    [JsonPropertyName("skin")] public CharacterSkinSection Skin { get; set; } = new();
    // The FULL skin catalog (every baseTone / detailLayer / eyeColor option's texture) so Unity can
    // switch them at runtime. Per the shared contract; null when no options resolved.
    [JsonPropertyName("skinCatalog")] public SkinCatalogSection? SkinCatalog { get; set; }
    // Body-mesh variants (top/bottom/feet) skinned to the SAME skeleton; null when single-mesh.
    [JsonPropertyName("meshCatalog")] public CharacterMeshCatalog? MeshCatalog { get; set; }
    // HAIR styles skinned to the SAME skeleton; each style has N colour variants (separate baked
    // diffuse PNGs — a runtime texture swap on one shared hair mesh). Appended by `exporthair`; null
    // until hair is exported.
    [JsonPropertyName("hairCatalog")] public CharacterHairCatalog? HairCatalog { get; set; }
    // CLOTHING (top/bottom/full/shoes) skinned to the SAME skeleton; each garment carries its own
    // opaque material + colour swatches and hides the nude regions it covers. Appended by `exportcloth`.
    [JsonPropertyName("clothingCatalog")] public CharacterClothingCatalog? ClothingCatalog { get; set; }
    // Base SKIN sets (full color albedos) — a Base Skin dropdown; normal/smoothness/SSS shared.
    [JsonPropertyName("skinBaseCatalog")] public CharacterSkinBaseCatalog? SkinBaseCatalog { get; set; }
    // Skin-detail NORMAL maps (real EA surface relief) — a "Skin detail (normal)" dropdown; null when none.
    [JsonPropertyName("skinNormalCatalog")] public CharacterSkinNormalCatalog? SkinNormalCatalog { get; set; }
    // BONE components of the CAS sliders (SMOD→BOND BoneAdjusts): per slider (name == blend-shape /
    // catalog id) the per-bone offset (parent-frame add, m), scale DELTA (0 = none; effective =
    // 1 + delta·weight, per TS4SimRipper RIG.Bone.UpdateLocalData) and rotation delta. Unity applies
    // these on TOP of the mesh blend shapes; ~63 sliders have both, 6 are bone-only (Chest/Head size).
    [JsonPropertyName("boneMorphs")] public List<CharacterBoneMorph>? BoneMorphs { get; set; }
    [JsonPropertyName("skeleton")] public List<CharacterBone> Skeleton { get; set; } = new();
    [JsonPropertyName("parts")] public List<CharacterPart> Parts { get; set; } = new();
}

internal sealed class CharacterBoneMorph
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty; // == BodyMorphOption.Id
    [JsonPropertyName("entries")] public List<CharacterBoneMorphEntry> Entries { get; set; } = new();
}

internal sealed class CharacterBoneMorphEntry
{
    [JsonPropertyName("bone")] public string Bone { get; set; } = string.Empty;
    [JsonPropertyName("ox")] public float OX { get; set; }
    [JsonPropertyName("oy")] public float OY { get; set; }
    [JsonPropertyName("oz")] public float OZ { get; set; }
    // Scale DELTA per axis (0 = no change). Effective localScale factor = 1 + s·weight.
    [JsonPropertyName("sx")] public float SX { get; set; }
    [JsonPropertyName("sy")] public float SY { get; set; }
    [JsonPropertyName("sz")] public float SZ { get; set; }
    [JsonPropertyName("qx")] public float QX { get; set; }
    [JsonPropertyName("qy")] public float QY { get; set; }
    [JsonPropertyName("qz")] public float QZ { get; set; }
    [JsonPropertyName("qw")] public float QW { get; set; } = 1f;
}

// One body source's mesh for a SINGLE region slot (top/bottom/feet), skinned to the shared skeleton.
internal sealed class CharacterMeshRegionOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("part")] public CharacterPart? Part { get; set; }
}

// One region SLOT: its independently-selectable options + the id that starts active.
internal sealed class CharacterMeshRegion
{
    [JsonPropertyName("default")] public string? Default { get; set; }
    [JsonPropertyName("options")] public List<CharacterMeshRegionOption> Options { get; set; } = new();
}

// The body is THREE independent slots (top = upper torso, bottom = legs, feet), each swappable on
// the one shared skeleton. (Explicit top/bottom/feet fields, not a dictionary, so Unity's JsonUtility
// can read it.)
internal sealed class CharacterMeshCatalog
{
    [JsonPropertyName("top")] public CharacterMeshRegion Top { get; set; } = new();
    [JsonPropertyName("bottom")] public CharacterMeshRegion Bottom { get; set; } = new();
    [JsonPropertyName("feet")] public CharacterMeshRegion Feet { get; set; } = new();
}

// One colour swatch of a hairstyle: a baked diffuse (RGBA, alpha = strand silhouette). The style's
// hair mesh is shared across colours; selecting a colour just swaps material._BaseColorMap.
internal sealed class CharacterHairColor
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;       // e.g. "Black"
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty; // display name
    [JsonPropertyName("diffuse")] public string Diffuse { get; set; } = string.Empty; // rel path e.g. Hair/xxx.png
}

// One hairstyle: the hair mesh (skinned to the shared skeleton) + its colour swatches.
internal sealed class CharacterHairStyle
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;       // internal name e.g. yfHair_GP12LongNatural
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty; // friendly e.g. "Long Natural"
    [JsonPropertyName("part")] public CharacterPart? Part { get; set; }           // the hair SkinnedMeshRenderer source
    [JsonPropertyName("colors")] public List<CharacterHairColor> Colors { get; set; } = new();
    [JsonPropertyName("defaultColor")] public string? DefaultColor { get; set; }  // id of the colour to start on
}

// Hair styles available to pick, plus the id that starts active (or empty for "no hair").
internal sealed class CharacterHairCatalog
{
    [JsonPropertyName("default")] public string? Default { get; set; }            // starting style id ("" = bald)
    [JsonPropertyName("options")] public List<CharacterHairStyle> Options { get; set; } = new();
}

// One garment: its mesh(es) skinned to the shared skeleton + colour swatches (diffuse swap). A garment
// covers one or more nude body regions (top/bottom/feet) which the runtime hides while it is worn.
internal sealed class CharacterClothingItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;       // internal name e.g. yfTop_TshirtCrew
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty; // friendly e.g. "Tshirt Crew"
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty; // top | bottom | full | shoes
    [JsonPropertyName("covers")] public List<string> Covers { get; set; } = new();      // nude regions hidden: top/bottom/feet
    [JsonPropertyName("parts")] public List<CharacterPart> Parts { get; set; } = new(); // garment mesh(es)
    [JsonPropertyName("colors")] public List<CharacterHairColor> Colors { get; set; } = new(); // {id,label,diffuse}
    [JsonPropertyName("defaultColor")] public string? DefaultColor { get; set; }
}

// One clothing SLOT (top/bottom/full/shoes): the garments in it + the id that starts active ("" = none).
internal sealed class CharacterClothingSlot
{
    [JsonPropertyName("default")] public string? Default { get; set; }
    [JsonPropertyName("options")] public List<CharacterClothingItem> Options { get; set; } = new();
}

// Wardrobe: four independent slots. Full-body outfits (dresses/jumpsuits) live in `full` and hide the
// nude top+bottom; a `full` selection is mutually exclusive with `top`/`bottom` at runtime.
internal sealed class CharacterClothingCatalog
{
    [JsonPropertyName("top")] public CharacterClothingSlot Top { get; set; } = new();
    [JsonPropertyName("bottom")] public CharacterClothingSlot Bottom { get; set; } = new();
    [JsonPropertyName("full")] public CharacterClothingSlot Full { get; set; } = new();
    [JsonPropertyName("shoes")] public CharacterClothingSlot Shoes { get; set; } = new();
    [JsonPropertyName("socks")] public CharacterClothingSlot Socks { get; set; } = new(); // layers under shoes
    [JsonPropertyName("bra")] public CharacterClothingSlot Bra { get; set; } = new(); // underwear LAYER: composites under tops/full
    [JsonPropertyName("panties")] public CharacterClothingSlot Panties { get; set; } = new(); // underwear LAYER: composites under bottoms/full
    [JsonPropertyName("tights")] public CharacterClothingSlot Tights { get; set; } = new(); // SKIN texture layer (legs; no mesh)
    [JsonPropertyName("glasses")] public CharacterClothingSlot Glasses { get; set; } = new();
    [JsonPropertyName("earrings")] public CharacterClothingSlot Earrings { get; set; } = new();
    [JsonPropertyName("necklace")] public CharacterClothingSlot Necklace { get; set; } = new();
    [JsonPropertyName("gloves")] public CharacterClothingSlot Gloves { get; set; } = new();
    [JsonPropertyName("wristl")] public CharacterClothingSlot WristL { get; set; } = new();
    [JsonPropertyName("wristr")] public CharacterClothingSlot WristR { get; set; } = new();
    [JsonPropertyName("lipstick")] public CharacterClothingSlot Lipstick { get; set; } = new();  // SKIN layer (face)
    [JsonPropertyName("eyeshadow")] public CharacterClothingSlot Eyeshadow { get; set; } = new(); // SKIN layer (face)
    [JsonPropertyName("eyeliner")] public CharacterClothingSlot Eyeliner { get; set; } = new();   // SKIN layer (face)
    [JsonPropertyName("blush")] public CharacterClothingSlot Blush { get; set; } = new();         // SKIN layer (face)
    [JsonPropertyName("brows")] public CharacterClothingSlot Brows { get; set; } = new();         // SKIN layer (face; "yu" unisex parts)
    [JsonPropertyName("eyelashes")] public CharacterClothingSlot Eyelashes { get; set; } = new(); // SKIN layer (face)
}

internal sealed class CharacterSelection
{
    [JsonPropertyName("bodyMesh")] public string? BodyMesh { get; set; }
    [JsonPropertyName("baseTone")] public string? BaseTone { get; set; }
    [JsonPropertyName("detailLayers")] public List<string> DetailLayers { get; set; } = new();
    [JsonPropertyName("eyeColor")] public string? EyeColor { get; set; }
}

internal static class CharacterDefinitionExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private const string ModsRoot = @"c:\Users\stani\PROJECTS\#GAMES\Sims4Browser\ModsFromDev";
    internal const string CatalogPath = @"c:\Users\stani\PROJECTS\#GAMES\Sims4Browser\tools\Sims4UnityExport\catalog.json";

    public static async Task RunAsync(
        IServiceProvider services,
        string unityAssetsDir,
        IIndexStore indexStore,
        string definitionPath,
        string? slugOverride,
        Func<IHost> hostFactory,
        bool scanMorphs,
        CancellationToken ct)
    {
        // --- 1) Load catalog + definition; resolve selections. ----------------------------------
        if (!File.Exists(definitionPath))
        {
            Console.Error.WriteLine($"CharacterDefinition not found: {definitionPath}");
            return;
        }
        if (!File.Exists(CatalogPath))
        {
            Console.Error.WriteLine($"catalog.json not found: {CatalogPath}");
            return;
        }

        var catalog = Catalog.Load(CatalogPath);
        var def = CharacterDefinition.Load(definitionPath);
        var slug = string.IsNullOrWhiteSpace(slugOverride) ? "af_char" : slugOverride!;

        var bodyMesh = catalog.FindBodyMesh(def.BodyMesh);
        var baseTone = catalog.FindBaseTone(def.BaseTone);
        var eyeColor = catalog.FindEyeColor(def.EyeColor);
        var detailOptions = def.DetailLayers
            .Select(id => (id, opt: catalog.FindDetailLayer(id)))
            .ToList();

        Console.WriteLine("============================================================");
        Console.WriteLine($"exportchar: def='{definitionPath}' slug='{slug}'");
        Console.WriteLine($"  age={def.Age} gender={def.Gender}");
        Console.WriteLine($"  bodyMesh='{def.BodyMesh}' -> {(bodyMesh is null ? "UNKNOWN (falling back to EA default)" : bodyMesh.Label)}");
        Console.WriteLine($"  baseTone='{def.BaseTone}' -> {(baseTone is null ? "UNKNOWN (falling back to 0x5545)" : baseTone.Label)}");
        Console.WriteLine($"  eyeColor='{def.EyeColor}' -> {(eyeColor is null ? "UNKNOWN (resolver default)" : eyeColor.Label)}");
        Console.WriteLine($"  detailLayers=[{string.Join(", ", def.DetailLayers)}]");
        foreach (var (id, opt) in detailOptions)
        {
            Console.WriteLine($"    detail '{id}' -> {(opt is null ? "UNKNOWN (skipped)" : opt.Label)}");
        }
        Console.WriteLine("============================================================");

        var skintoneInstance = baseTone?.ToneInstanceValue is { } v && v != 0ul
            ? v
            : 0x0000000000005545ul;

        // --- 2) Arm the ModOverride for the chosen bodyMesh (if it has overrides). ---------------
        ModOverrideResourceCatalogService? modOverride = null;
        if (bodyMesh?.Overrides is not null)
        {
            Console.WriteLine($"exportchar: arming bodyMesh override '{bodyMesh.Id}' ({bodyMesh.Label})...");
            var realCatalog = services.GetRequiredService<LlamaResourceCatalogService>();
            modOverride = await ModOverrideResourceCatalogService
                .BuildFromBodyMeshAsync(realCatalog, indexStore, bodyMesh, ModsRoot, ct)
                .ConfigureAwait(false);
            if (modOverride is not null)
            {
                foreach (var line in modOverride.Log) Console.WriteLine(line);
                ModOverrideHolder.Current = modOverride;
            }
        }
        else
        {
            Console.WriteLine("exportchar: bodyMesh has no overrides — using EA default body (no override armed).");
        }

        try
        {
            // Resolve Sim services AFTER the holder is armed so they wrap the decorator.
            var synthetic = services.GetRequiredService<ISyntheticSimService>();
            var simRenderer = services.GetRequiredService<ISimAssetGraphRenderer>();
            var catalogSvc = services.GetRequiredService<IResourceCatalogService>();

            // --- 3) Resolve the skintone render summary + assemble the unified Sim scene. --------
            var skin = await synthetic
                .ResolveSkintoneRenderAsync(def.Age, def.Gender, skintoneInstance, ct)
                .ConfigureAwait(false);
            if (skin is null)
            {
                Console.Error.WriteLine("exportchar: ResolveSkintoneRenderAsync returned null; aborting.");
                return;
            }
            Console.WriteLine(
                $"[skin] tone=0x{skintoneInstance:X16} base={skin.BaseTextureResourceTgi ?? "(none)"} " +
                $"baseBytes={skin.BaseTexturePngBytes?.Length ?? 0} faceCas={skin.FaceCasOverlayPngBytes?.Count ?? 0}.");

            var seed = synthetic.CreateHumanSeed(def.Age, def.Gender, skintoneInstance);
            var graph = await synthetic.BuildHumanAssetGraphAsync(seed, ct).ConfigureAwait(false);
            if (graph.SimGraph is null)
            {
                Console.Error.WriteLine("exportchar: no SimGraph produced; aborting.");
                return;
            }
            var renderResult = await simRenderer.BuildSimSceneAsync(graph, ct).ConfigureAwait(false);
            if (renderResult.Scene is not { } scene)
            {
                Console.Error.WriteLine("exportchar: render produced no Scene; aborting.");
                return;
            }
            Console.WriteLine($"[scene] meshes={scene.Meshes.Count} materials={scene.Materials.Count} bones={scene.Bones.Count}.");
            foreach (var m in scene.Meshes)
            {
                Console.WriteLine($"[scene]   mesh '{m.Name}' verts={m.Positions.Count / 3} tris={m.Indices.Count / 3}.");
            }

            // NOTE (2026-06-21): only the HEAD carries vertex IDs in our pipeline — EA's body GEOMs (and
            // CC bodies like Better Body) ship WITHOUT usage-0x0A IDs, so BGEO morphs reach the FACE only.
            // A vertex-ID snap was tried and proved moot (no body IDs exist to copy from). Body-shape
            // sliders need the BOND (bone) morph path (SimBondSceneMorpher), which deforms via the skeleton
            // and works on any skinned mesh — that's the next body-morph track.

            var assetFolder = Path.Combine(unityAssetsDir, slug);
            var texturesFolder = Path.Combine(assetFolder, "Textures");
            Directory.CreateDirectory(texturesFolder);

            var skinSection = new CharacterSkinSection();

            // --- 4a) layer_base.png — the base skin color from the chosen baseTone. -------------
            if (skin.BaseTexturePngBytes is { Length: > 0 } baseBytes)
            {
                var baseFile = "layer_base.png";
                var basePath = Path.Combine(texturesFolder, baseFile);
                await File.WriteAllBytesAsync(basePath, baseBytes, ct).ConfigureAwait(false);
                // The base is a COLOR substrate (same UV gutters as the atlas), so DILATE the opaque
                // skin color outward into the transparent gutters rather than blackening — blackening
                // would bake black seams that the runtime shader's edge filtering pulls across UV
                // islands. (This is the same cure the preview atlas uses.)
                DilateIfTransparent(basePath, baseFile);
                var (bw, bh) = ProbeDims(basePath);
                Console.WriteLine($"[layer] base   -> {baseFile} ({bw}x{bh}, {baseBytes.Length:N0} B).");
                skinSection.Layers.Add(new CharacterSkinLayer { Type = "base", Texture = $"Textures/{baseFile}" });
            }
            else
            {
                Console.Error.WriteLine("[layer] base: baseTone summary carried no BaseTexturePngBytes; base layer omitted.");
            }

            // --- 4b) layer_detail_<id>.png — each selected detail relief map. --------------------
            // Track which detail layers actually resolved to bytes (for the preview compositor).
            var resolvedDetailReliefs = new List<byte[]>();
            foreach (var (id, opt) in detailOptions)
            {
                if (opt is null)
                {
                    Console.Error.WriteLine($"[layer] detail '{id}': not in catalog; skipped.");
                    continue;
                }
                var png = await ResolveDetailReliefAsync(catalogSvc, opt, ct).ConfigureAwait(false);
                if (png is not { Length: > 0 })
                {
                    Console.Error.WriteLine($"[layer] detail '{id}': could not resolve any of its texture TGIs; skipped.");
                    continue;
                }
                var detailFile = $"layer_detail_{Sanitize(id)}.png";
                var detailPath = Path.Combine(texturesFolder, detailFile);
                await File.WriteAllBytesAsync(detailPath, png, ct).ConfigureAwait(false);
                var (dw, dh) = ProbeDims(detailPath);
                Console.WriteLine($"[layer] detail '{id}' -> {detailFile} ({dw}x{dh}, {png.Length:N0} B, blend={opt.Blend}).");
                skinSection.Layers.Add(new CharacterSkinLayer
                {
                    Type = "detail", Id = id, Texture = $"Textures/{detailFile}", Blend = opt.Blend,
                });
                resolvedDetailReliefs.Add(png);
            }

            // --- 4c) layer_eye.png — the chosen EyeColor CAS overlay. ----------------------------
            byte[]? eyePng = null;
            if (eyeColor is not null && eyeColor.OverlayInstanceValue != 0ul)
            {
                try
                {
                    eyePng = await synthetic
                        .ResolveCasPartDiffusePngAsync(eyeColor.OverlayInstanceValue, ct)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[layer] eye '{eyeColor.Id}': ResolveCasPartDiffusePngAsync FAILED ({ex.GetType().Name}: {ex.Message}).");
                }
            }
            // Fallback: use the resolver's already-resolved face CAS overlay (the default brown eye).
            if (eyePng is not { Length: > 0 } && skin.FaceCasOverlayPngBytes is { Count: > 0 } casList)
            {
                eyePng = casList[^1]; // EyeColor is resolved last in the summary's CAS overlay list.
                Console.WriteLine("[layer] eye: using resolver's face-CAS overlay as the eye layer (definition eyeColor unresolved or default).");
            }
            if (eyePng is { Length: > 0 })
            {
                var eyeFile = "layer_eye.png";
                var eyePath = Path.Combine(texturesFolder, eyeFile);
                await File.WriteAllBytesAsync(eyePath, eyePng, ct).ConfigureAwait(false);
                var (ew, eh) = ProbeDims(eyePath);
                Console.WriteLine($"[layer] eye -> {eyeFile} ({ew}x{eh}, {eyePng.Length:N0} B).");
                skinSection.Layers.Add(new CharacterSkinLayer { Type = "eye", Texture = $"Textures/{eyeFile}" });
            }
            else
            {
                Console.Error.WriteLine("[layer] eye: no eye overlay resolved; eye layer omitted.");
            }

            // --- 5) Bake the preview atlas + normal with the existing CORRECT compositor. --------
            // Inject the selected detail reliefs as additional physique-detail rows so the preview
            // reflects the chosen detail layers (the compositor overlays each by weight 1.0). The EA
            // base + the resolver's neutral/overlay detail + the eye CAS overlay are all already
            // carried by `skin`; we only need to add the user-selected reliefs.
            var (previewAtlasRel, previewNormalRel) = await BakePreviewAtlasAsync(
                skin, resolvedDetailReliefs, texturesFolder, ct).ConfigureAwait(false);
            skinSection.PreviewAtlas = previewAtlasRel;
            skinSection.PreviewNormal = previewNormalRel;

            // --- 5b) Emit the FULL skin catalog (ALL options) so Unity can switch them at runtime. --
            // For EVERY catalog baseTone/detailLayer/eyeColor: resolve + write its UV-aligned texture
            // into <slug>/Skin/, and bake a preview skin_atlas.png with the EXACT contract compose
            // formula (base + default details + default eye). Returns the "skinCatalog" section.
            SkinCatalogSection? skinCatalog = null;
            try
            {
                skinCatalog = await SkinCatalogExporter.ExportAsync(
                    catalog, def, synthetic, catalogSvc, assetFolder, ModsRoot, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[skinCatalog] ExportAsync FAILED ({ex.GetType().Name}: {ex.Message}); skinCatalog omitted.");
            }

            // --- 6) Write character.json (mesh/skeleton/parts as today + layered skin). ----------
            var manifest = new CharacterDefManifest
            {
                Asset = slug,
                Kind = "sim",
                Age = Capitalize(def.Age),
                Gender = Capitalize(def.Gender),
                Selection = new CharacterSelection
                {
                    BodyMesh = def.BodyMesh,
                    BaseTone = def.BaseTone,
                    DetailLayers = def.DetailLayers,
                    EyeColor = def.EyeColor,
                },
                Skin = skinSection,
                SkinCatalog = skinCatalog,
                Skeleton = SceneToSkeleton(scene),
            };

            // Renders ONE bodyMesh option's Sim in a FRESH host (the IResourceCatalogService
            // singleton binds the ModOverride decorator on first resolution, so each variant needs
            // its own host). Returns the variant's CanonicalScene (we keep only its body geometry).
            async Task<CanonicalScene?> RenderBodyVariant(BodyMeshOption opt)
            {
                using var vhost = hostFactory();
                var vindex = vhost.Services.GetRequiredService<IIndexStore>();
                await vindex.InitializeAsync(ct).ConfigureAwait(false);

                ModOverrideResourceCatalogService? ov = null;
                if (opt.Overrides is not null)
                {
                    var realCat = vhost.Services.GetRequiredService<LlamaResourceCatalogService>();
                    ov = await ModOverrideResourceCatalogService
                        .BuildFromBodyMeshAsync(realCat, vindex, opt, ModsRoot, ct).ConfigureAwait(false);
                }
                ModOverrideHolder.Current = ov; // arm BEFORE resolving the Sim services in this host
                try
                {
                    var synth2 = vhost.Services.GetRequiredService<ISyntheticSimService>();
                    var rend2 = vhost.Services.GetRequiredService<ISimAssetGraphRenderer>();
                    var seed2 = synth2.CreateHumanSeed(def.Age, def.Gender, skintoneInstance);
                    var graph2 = await synth2.BuildHumanAssetGraphAsync(seed2, ct).ConfigureAwait(false);
                    if (graph2.SimGraph is null) return null;
                    var rr2 = await rend2.BuildSimSceneAsync(graph2, ct).ConfigureAwait(false);
                    return rr2.Scene;
                }
                finally { ModOverrideHolder.Current = null; }
            }

            // SHARED parts (head/eyes/brows) → parts[] (always built). BODY parts (top/bottom/feet)
            // → the DEFAULT entry of a meshCatalog; alternate bodyMesh options are rendered below and
            // added as extra entries so the runtime can swap body meshes on the one shared skeleton.
            var sharedMat = new CharacterMaterial { Diffuse = previewAtlasRel, Normal = previewNormalRel };

            // Per-region body-mesh catalog: top (upper torso) / bottom (legs) / feet are INDEPENDENT
            // slots. Each body source contributes a mesh to the region(s) it overrides; EA is the
            // baseline option in all three.
            var meshCatalog = new CharacterMeshCatalog();
            CharacterMeshRegion? RegionObj(string r) => r switch
            {
                "top" => meshCatalog.Top, "bottom" => meshCatalog.Bottom, "feet" => meshCatalog.Feet, _ => null
            };

            // A body mesh is named "Mesh_<EAinstance>" regardless of which mod served it (the renderer
            // requests the EA default-body GEOM by instance), so classify the slot by that instance.
            var regionByInstance = BuildRegionByInstance(catalog);

            // scanmorphs mode: enumerate + score EVERY adult-female BGEO SMOD against this scene, rank by
            // symmetry/coherence, then stop (no character.json written). The curation tool that finds
            // clean, meaningful sliders instead of arbitrary SMODs — and reveals which mesh each reaches.
            if (scanMorphs)
            {
                await ScanAllMorphsAsync(scene, indexStore, catalogSvc, ct).ConfigureAwait(false);
                return;
            }

            // --- Body/face MORPH sliders: apply each catalog morph to a scene at its scale, diff vs base →
            // per-mesh SPARSE blend-shape deltas. The bake helper is reused for the DEFAULT scene AND every
            // body variant, so morphs work on ANY current/future body mesh (BGEO=face via vertex IDs;
            // DMap=body via UV1+tags). Resolvers/weightMode are hoisted here for both passes. ---
            var bgeoResolver = new BlendGeometryResolver(indexStore, catalogSvc);
            var dmapResolver = new DeformerMapResolver(indexStore, catalogSvc);
            // Reference-correct default: every mesh the morpher touches carries NATIVE vertex IDs, so
            // TS4SimRipper's copyFaceMorphs is false → full delta (Ignore). Faithful/Legacy = experiments.
            var weightMode = (Environment.GetEnvironmentVariable("MORPH_WEIGHT")?.ToLowerInvariant()) switch
            {
                "faithful" => Sims4ResourceExplorer.Preview.FaceMorphWeighting.Faithful,
                "legacy" => Sims4ResourceExplorer.Preview.FaceMorphWeighting.Legacy,
                _ => Sims4ResourceExplorer.Preview.FaceMorphWeighting.Ignore,
            };
            // DMap (body) subset — variants carry no head, so the BGEO face morphs would no-op there anyway.
            var bodyMorphs = new List<BodyMorphOption>();
            foreach (var m in catalog.BodyMorphs)
            {
                if (string.Equals(m.Kind, "dmap", StringComparison.OrdinalIgnoreCase)) bodyMorphs.Add(m);
            }
            Console.WriteLine($"[morph] weighting={weightMode}; {catalog.BodyMorphs.Count} morph(s) ({bodyMorphs.Count} body/DMap apply to every body variant).");
            var morphDeltas = catalog.BodyMorphs.Count > 0
                ? await BakeMorphsAsync(scene, catalog.BodyMorphs, bgeoResolver, dmapResolver, weightMode, verbose: true, ct).ConfigureAwait(false)
                : new Dictionary<int, List<CharacterBlendShape>>();

            // DEFAULT source (def.BodyMesh): shared head/eyes/brows → parts[]; body meshes → their
            // region's options, and this source becomes each region's default.
            for (var i = 0; i < scene.Meshes.Count; i++)
            {
                var mesh = scene.Meshes[i];
                var mat = mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.Materials.Count
                    ? scene.Materials[mesh.MaterialIndex] : null;
                var region = InferRegion(mesh.Name, mat);
                var cmesh = SceneToMesh(mesh, scene.Bones.Count);
                if (morphDeltas.TryGetValue(i, out var meshMorphs)) cmesh.BlendShapes = meshMorphs;
                if (IsBodyRegion(region))
                {
                    // Instance map is harvested from the catalog's (female-derived) eaTop/eaBottom/eaFeet.
                    // The EA MALE body (and any future body whose GEOM instances aren't catalogued) won't
                    // match, so fall back to a vertical-extent classifier — feet sit at the ankle-down, the
                    // torso/top starts above the hip, the legs span between.
                    var br = RegionOf(mesh.Name, regionByInstance) ?? RegionByGeometry(cmesh);
                    if (br is null) { Console.Error.WriteLine($"[meshCatalog] default: body mesh '{mesh.Name}' unclassified; skipped."); continue; }
                    if (RegionOf(mesh.Name, regionByInstance) is null)
                        Console.WriteLine($"[meshCatalog] default: body mesh '{mesh.Name}' classified by geometry → {br}.");
                    var ro = RegionObj(br)!;
                    ro.Options.Add(new CharacterMeshRegionOption
                    {
                        Id = def.BodyMesh ?? "default",
                        Label = bodyMesh?.Label ?? def.BodyMesh ?? "default",
                        Part = new CharacterPart
                        {
                            Name = string.IsNullOrWhiteSpace(mesh.Name) ? $"{def.BodyMesh}_{br}" : mesh.Name,
                            Region = CapitalizeRegion(br),
                            Mesh = cmesh,
                            Material = sharedMat,
                        },
                    });
                    ro.Default ??= def.BodyMesh ?? "default";
                }
                else
                {
                    manifest.Parts.Add(new CharacterPart
                    {
                        Name = string.IsNullOrWhiteSpace(mesh.Name) ? $"part_{i}" : mesh.Name,
                        Region = region,
                        Mesh = cmesh,
                        Material = sharedMat,
                    });
                }
            }
            Console.WriteLine($"[meshCatalog] default '{def.BodyMesh}': top={meshCatalog.Top.Options.Count} bottom={meshCatalog.Bottom.Options.Count} feet={meshCatalog.Feet.Options.Count}; shared parts[]={manifest.Parts.Count}.");

            // Bone-name → shared-skeleton index (variants may emit bones in a different order).
            var sharedIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < scene.Bones.Count; i++)
            {
                var bn = scene.Bones[i].Name;
                if (!string.IsNullOrEmpty(bn) && !sharedIndexByName.ContainsKey(bn)) sharedIndexByName[bn] = i;
            }

            // Every OTHER source: render once, keep ONLY the regions it overrides (EA baseline already
            // covers the rest). A source with no overrides (EA) contributes all three regions.
            foreach (var opt in catalog.BodyMesh)
            {
                if (string.Equals(opt.Id, def.BodyMesh, StringComparison.OrdinalIgnoreCase)) continue;
                var contributed = ContributedRegions(opt);
                Console.WriteLine($"[meshCatalog] rendering source '{opt.Id}' ({opt.Label}) for [{string.Join(",", contributed)}] in a fresh host...");
                CanonicalScene? vscene;
                try { vscene = await RenderBodyVariant(opt).ConfigureAwait(false); }
                catch (Exception ex) { Console.Error.WriteLine($"[meshCatalog] source '{opt.Id}' FAILED ({ex.GetType().Name}: {ex.Message}); skipped."); continue; }
                if (vscene is null) { Console.Error.WriteLine($"[meshCatalog] source '{opt.Id}': produced no scene; skipped."); continue; }

                // Bake the body (DMap) morphs onto THIS variant's meshes too, so the sliders work on it.
                var vDeltas = bodyMorphs.Count > 0
                    ? await BakeMorphsAsync(vscene, bodyMorphs, bgeoResolver, dmapResolver, weightMode, verbose: false, ct).ConfigureAwait(false)
                    : new Dictionary<int, List<CharacterBlendShape>>();

                var vbones = vscene.Bones;
                int Remap(int oldIdx) =>
                    oldIdx >= 0 && oldIdx < vbones.Count && sharedIndexByName.TryGetValue(vbones[oldIdx].Name, out var ni) ? ni : 0;

                var added = 0;
                for (var i = 0; i < vscene.Meshes.Count; i++)
                {
                    var vmesh = vscene.Meshes[i];
                    var vmat = vmesh.MaterialIndex >= 0 && vmesh.MaterialIndex < vscene.Materials.Count
                        ? vscene.Materials[vmesh.MaterialIndex] : null;
                    var region = InferRegion(vmesh.Name, vmat);
                    if (!IsBodyRegion(region)) continue;
                    var br = RegionOf(vmesh.Name, regionByInstance);
                    if (br is null || !contributed.Contains(br)) continue; // keep only overridden slot(s)
                    var ro = RegionObj(br)!;
                    var vcmesh = SceneToMeshRemapped(vmesh, scene.Bones.Count, Remap);
                    if (vDeltas.TryGetValue(i, out var vbs)) vcmesh.BlendShapes = vbs; // morphs for this variant
                    ro.Options.Add(new CharacterMeshRegionOption
                    {
                        Id = opt.Id,
                        Label = opt.Label,
                        Part = new CharacterPart
                        {
                            Name = string.IsNullOrWhiteSpace(vmesh.Name) ? $"{opt.Id}_{br}" : vmesh.Name,
                            Region = CapitalizeRegion(br),
                            Mesh = vcmesh,
                            Material = sharedMat,
                        },
                    });
                    if (opt.Overrides is null) ro.Default ??= opt.Id; // EA baseline fills any missing default
                    added++;
                }
                Console.WriteLine($"[meshCatalog]   source '{opt.Id}' added {added} region option(s).");
            }

            // Region defaults: prefer the definition's per-slot pick (def.TopMesh/BottomMesh/FeetMesh),
            // else whatever was set during build, else the region's first option.
            var regionWants = new (string rname, CharacterMeshRegion ro, string? want)[]
            {
                ("top", meshCatalog.Top, def.TopMesh),
                ("bottom", meshCatalog.Bottom, def.BottomMesh),
                ("feet", meshCatalog.Feet, def.FeetMesh),
            };
            foreach (var (rname, ro, want) in regionWants)
            {
                if (!string.IsNullOrEmpty(want) && ro.Options.Any(o => string.Equals(o.Id, want, StringComparison.OrdinalIgnoreCase)))
                    ro.Default = want;
                else if (string.IsNullOrEmpty(ro.Default) && ro.Options.Count > 0)
                    ro.Default = ro.Options[0].Id;
                Console.WriteLine($"[meshCatalog] region {rname}: {ro.Options.Count} option(s), default='{ro.Default}'.");
            }

            manifest.MeshCatalog = meshCatalog;

            // --- 6b) Base SKIN sets: each skin's AF color albedo (RAW — no detail/eye/tone compositing). --
            // Sims skin LRLEs are grayscale (color = the tone), EXCEPT premium COLOR overlays (Obscurus,
            // PsBoss) whose diffuse is fully colored. So: an entry with textureTgis -> resolve that color
            // LRLE DIRECTLY as the albedo; "game" (no tgis) -> the resolver's tone-tinted EA base.
            if (catalog.SkinBase.Count > 0)
            {
                var skinSetFolder = Path.Combine(assetFolder, "Skin");
                Directory.CreateDirectory(skinSetFolder);
                var skinBaseCatalog = new CharacterSkinBaseCatalog();
                foreach (var sb in catalog.SkinBase)
                {
                    byte[]? albedo = null;
                    if (sb.TextureTgis is { Count: > 0 })
                    {
                        var pkg = string.IsNullOrWhiteSpace(sb.Package) ? string.Empty : Path.Combine(ModsRoot, sb.Package);
                        foreach (var tgi in sb.TextureTgis)
                        {
                            if (!TryParseTgi(tgi, out var t, out var g, out var inst)) continue;
                            var key = new ResourceKeyRecord(t, g, inst, TypeNameForType(t));
                            try { albedo = await catalogSvc.GetTexturePngAsync(pkg, key, ct).ConfigureAwait(false); }
                            catch (Exception ex) { Console.Error.WriteLine($"[skinBase] '{sb.Id}' tgi {tgi} FAILED ({ex.GetType().Name}: {ex.Message})."); }
                            if (albedo is { Length: > 0 }) break;
                        }
                    }
                    else
                    {
                        albedo = skin.BaseTexturePngBytes; // EA default — the resolver's tone-tinted base
                    }
                    if (albedo is not { Length: > 0 }) { Console.Error.WriteLine($"[skinBase] '{sb.Id}': no albedo resolved; skipped."); continue; }
                    var file = $"skinbase_{Sanitize(sb.Id)}.png";
                    var path = Path.Combine(skinSetFolder, file);
                    await File.WriteAllBytesAsync(path, albedo, ct).ConfigureAwait(false);
                    DilateIfTransparent(path, file);
                    var (sw, sh) = ProbeDims(path);
                    Console.WriteLine($"[skinBase]   '{sb.Id}' -> Skin/{file} ({sw}x{sh}, {albedo.Length:N0} B).");
                    skinBaseCatalog.Options.Add(new CharacterSkinBaseEntry { Id = sb.Id, Label = sb.Label, Albedo = $"Skin/{file}" });
                }
                skinBaseCatalog.Default =
                    (!string.IsNullOrEmpty(def.BaseSkin) && skinBaseCatalog.Options.Any(o => string.Equals(o.Id, def.BaseSkin, StringComparison.OrdinalIgnoreCase)))
                        ? def.BaseSkin
                        : skinBaseCatalog.Options.FirstOrDefault()?.Id;
                manifest.SkinBaseCatalog = skinBaseCatalog;
                Console.WriteLine($"[skinBase] wrote {skinBaseCatalog.Options.Count} base-skin albedo(s), default='{skinBaseCatalog.Default}'.");
            }

            // --- Skin-detail NORMAL maps: each catalog entry's DST (a real EA Sculpt bumpmap or CC
            // normalMapKey) decodes to a GRAYSCALE height → NormalGen height→normal → a real tangent-space
            // normal (no albedo features). Empty dst = the "Flat" option. Drives a Unity dropdown. ---
            if (catalog.SkinNormals.Count > 0)
            {
                var normalFolder = Path.Combine(assetFolder, "Skin");
                Directory.CreateDirectory(normalFolder);
                var tmpDir = Path.Combine(Path.GetTempPath(), "exportnormals");
                Directory.CreateDirectory(tmpDir);
                var normalCatalog = new CharacterSkinNormalCatalog();
                foreach (var sn in catalog.SkinNormals)
                {
                    if (string.IsNullOrWhiteSpace(sn.Dst))
                    {
                        normalCatalog.Options.Add(new CharacterSkinNormalEntry { Id = sn.Id, Label = sn.Label, Normal = "" });
                        Console.WriteLine($"[skinNormal]   '{sn.Id}' = Flat (no normal).");
                        continue;
                    }
                    var parts = sn.Dst.Split(':');
                    if (parts.Length < 2
                        || !uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out var ntype)
                        || !ulong.TryParse(parts[^1], System.Globalization.NumberStyles.HexNumber, null, out var ninst))
                    { Console.Error.WriteLine($"[skinNormal] '{sn.Id}': bad dst '{sn.Dst}'."); continue; }
                    var ress = await indexStore.GetResourcesByFullInstanceAsync(ninst, ct).ConfigureAwait(false);
                    var res = ress.FirstOrDefault(r => r.Key.Type == ntype);
                    if (res is null) { Console.Error.WriteLine($"[skinNormal] '{sn.Id}': DST {sn.Dst} not in index."); continue; }
                    byte[]? height;
                    try { height = await catalogSvc.GetTexturePngAsync(res.PackagePath, res.Key, ct).ConfigureAwait(false); }
                    catch (Exception ex) { Console.Error.WriteLine($"[skinNormal] '{sn.Id}' decode FAILED ({ex.GetType().Name}: {ex.Message})."); continue; }
                    if (height is not { Length: > 0 }) { Console.Error.WriteLine($"[skinNormal] '{sn.Id}': empty height."); continue; }
                    var tmpHeight = Path.Combine(tmpDir, $"h_{Sanitize(sn.Id)}.png");
                    await File.WriteAllBytesAsync(tmpHeight, height, ct).ConfigureAwait(false);
                    var file = $"skinnormal_{Sanitize(sn.Id)}.png";
                    var outPath = Path.Combine(normalFolder, file);
                    // EA DST normals are 2-channel (X=alpha, Y=green) — channel-swap to a real Unity normal
                    // (NOT a Sobel/height pass, which mangled them). sn.Strength = intensity scale.
                    try { NormalGen.ConvertDstNormal(tmpHeight, outPath, sn.Strength); }
                    catch (Exception ex) { Console.Error.WriteLine($"[skinNormal] '{sn.Id}' convert FAILED ({ex.GetType().Name}: {ex.Message})."); continue; }
                    var (nw, nh) = ProbeDims(outPath);
                    Console.WriteLine($"[skinNormal]   '{sn.Id}' -> Skin/{file} ({nw}x{nh}, strength {sn.Strength}).");
                    normalCatalog.Options.Add(new CharacterSkinNormalEntry { Id = sn.Id, Label = sn.Label, Normal = $"Skin/{file}" });
                }
                normalCatalog.Default = normalCatalog.Options.FirstOrDefault()?.Id;
                manifest.SkinNormalCatalog = normalCatalog;
                Console.WriteLine($"[skinNormal] wrote {normalCatalog.Options.Count} normal(s), default='{normalCatalog.Default}'.");
            }

            // --- BONE components of the sliders (SMOD→BOND): 63 sliders carry bone offsets/scales/
            // rotations on top of their mesh morph; 6 (Chest/Head size, Chin_Forward) are bone-ONLY.
            manifest.BoneMorphs = await ResolveBoneMorphsAsync(catalog.BodyMorphs, indexStore, catalogSvc, ct).ConfigureAwait(false);

            var characterPath = Path.Combine(assetFolder, "character.json");
            await File.WriteAllTextAsync(characterPath, JsonSerializer.Serialize(manifest, JsonOptions), ct).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine($"exportchar: wrote {characterPath}");
            Console.WriteLine($"  skin.layers ({manifest.Skin.Layers.Count}):");
            foreach (var l in manifest.Skin.Layers)
            {
                Console.WriteLine($"    - type={l.Type}{(l.Id is null ? "" : $" id={l.Id}")} texture={l.Texture}{(l.Blend is null ? "" : $" blend={l.Blend}")}");
            }
            Console.WriteLine($"  previewAtlas={manifest.Skin.PreviewAtlas} previewNormal={manifest.Skin.PreviewNormal}");
            Console.WriteLine($"  parts={manifest.Parts.Count} skeletonBones={manifest.Skeleton.Count}.");
            if (manifest.SkinCatalog is { } sc)
            {
                Console.WriteLine($"  skinCatalog: baseTones={sc.BaseTones.Count} detailLayers={sc.DetailLayers.Count} eyeColors={sc.EyeColors.Count} " +
                                  $"default(base='{sc.Default.BaseTone}', details=[{string.Join(",", sc.Default.DetailLayers)}], eye='{sc.Default.EyeColor}').");
            }

            // Surface the mod-override SERVED log so the report shows the Better Body GEOM wins.
            if (modOverride is not null)
            {
                Console.WriteLine();
                Console.WriteLine("Mod-override SERVED log:");
                var served = modOverride.Log.Where(l => l.Contains("SERVED", StringComparison.Ordinal)).ToList();
                if (served.Count == 0) Console.WriteLine("  (none served — no overridden TGI requested!)");
                foreach (var l in served) Console.WriteLine(l);
            }
        }
        finally
        {
            ModOverrideHolder.Current = null;
        }
    }

    // Resolve the FIRST resolvable relief texture for a detail option (its TGIs are tried in order).
    private static async Task<byte[]?> ResolveDetailReliefAsync(
        IResourceCatalogService catalog, DetailLayerOption opt, CancellationToken ct)
    {
        var pkg = string.IsNullOrWhiteSpace(opt.Package) ? null : Path.Combine(ModsRoot, opt.Package);
        foreach (var tgi in opt.TextureTgis)
        {
            if (!TryParseTgi(tgi, out var type, out var group, out var inst)) continue;
            var key = new ResourceKeyRecord(type, group, inst, TypeNameForType(type));
            // The mod package path is the authoritative source; the override decorator (if armed)
            // ignores it for non-overridden TGIs and reads the named package directly.
            var path = pkg ?? string.Empty;
            try
            {
                var png = await catalog.GetTexturePngAsync(path, key, ct).ConfigureAwait(false);
                if (png is { Length: > 0 }) return png;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[detail] {tgi}: GetTexturePngAsync FAILED ({ex.GetType().Name}: {ex.Message}).");
            }
        }
        return null;
    }

    // Bake the preview atlas: run the SAME SkinAtlasComposer chain exportsim uses, but inject the
    // selected detail reliefs as extra physique-detail rows (weight 1.0 each) so the chosen detail
    // layers ARE composited into the preview. Returns (atlasRel, normalRel).
    private static async Task<(string?, string?)> BakePreviewAtlasAsync(
        SimSkintoneRenderSummary skin,
        IReadOnlyList<byte[]> extraDetailReliefs,
        string texturesFolder,
        CancellationToken ct)
    {
        // Default fit-leaning physique so the resolver's own muscle/relief rows contribute.
        var physiqueWeights = skin.PhysiqueWeights is { Count: 4 } w &&
                              (w[0] > 0f || w[1] > 0f || w[2] > 0f || w[3] > 0f)
            ? w.ToList()
            : new List<float> { 0f, 0.6f, 0.4f, 0f };

        // Inject the selected detail reliefs by APPENDING them as additional physique-detail rows at
        // weight 1.0. The composer iterates physiqueDetailPngs by index against physiqueWeights, so
        // we extend both lists in lockstep. (Overlay rows for the injected reliefs stay absent.)
        SimSkintoneRenderSummary composeInput = skin;
        if (extraDetailReliefs.Count > 0)
        {
            var detailRows = new List<byte[]?>(skin.PhysiqueDetailPngBytes ?? Array.Empty<byte[]?>());
            var overlayRows = new List<byte[]?>(skin.PhysiqueOverlayPngBytes ?? Array.Empty<byte[]?>());
            var weights = new List<float>(physiqueWeights);
            foreach (var relief in extraDetailReliefs)
            {
                detailRows.Add(relief);
                overlayRows.Add(null);
                weights.Add(1.0f);
            }
            composeInput = skin with
            {
                PhysiqueDetailPngBytes = detailRows,
                PhysiqueOverlayPngBytes = overlayRows,
            };
            physiqueWeights = weights;
            Console.WriteLine($"[preview] injected {extraDetailReliefs.Count} selected detail relief row(s) into the compositor at weight 1.0.");
        }

        byte[]? atlas;
        try
        {
            atlas = SkinAtlasComposer.ComposeAtlas(composeInput, physiqueWeights);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[preview] ComposeAtlas FAILED ({ex.GetType().Name}: {ex.Message}); no preview baked.");
            return (null, null);
        }
        if (atlas is not { Length: > 0 })
        {
            Console.Error.WriteLine("[preview] ComposeAtlas produced no atlas; no preview baked.");
            return (null, null);
        }

        const string atlasFile = "skin_atlas.png";
        var atlasPath = Path.Combine(texturesFolder, atlasFile);
        await File.WriteAllBytesAsync(atlasPath, atlas, ct).ConfigureAwait(false);
        try
        {
            if (TextureCleanup.HasTransparentPixels(atlasPath))
            {
                var filled = TextureCleanup.DilateOpaque(atlasPath, 16);
                Console.WriteLine($"[preview] dilated {filled} gutter pixel(s) on {atlasFile}.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[preview] dilate FAILED ({ex.GetType().Name}: {ex.Message}).");
        }
        var (aw, ah) = ProbeDims(atlasPath);
        Console.WriteLine($"[preview] wrote {atlasFile} ({aw}x{ah}, {atlas.Length:N0} B).");
        ReportWarmCheck(atlasPath);

        string? normalRel = null;
        try
        {
            var normal = SkinAtlasComposer.DeriveNormalMap(atlas, 3.0f);
            if (normal is { Length: > 0 })
            {
                const string normalFile = "skin_normal.png";
                await File.WriteAllBytesAsync(Path.Combine(texturesFolder, normalFile), normal, ct).ConfigureAwait(false);
                Console.WriteLine($"[preview] wrote {normalFile} ({normal.Length:N0} B).");
                normalRel = $"Textures/{normalFile}";
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[preview] DeriveNormalMap FAILED ({ex.GetType().Name}: {ex.Message}).");
        }

        return ($"Textures/{atlasFile}", normalRel);
    }

    // ---- skeleton/mesh builders (mirror CharacterExporter so the Unity builder reads identically) -
    private static float[] Identity() => new float[]
    { 1f,0f,0f,0f, 0f,1f,0f,0f, 0f,0f,1f,0f, 0f,0f,0f,1f };

    private static List<CharacterBone> SceneToSkeleton(CanonicalScene scene)
    {
        var bones = new List<CharacterBone>(scene.Bones.Count);
        foreach (var b in scene.Bones)
        {
            bones.Add(new CharacterBone
            {
                Name = b.Name,
                ParentName = string.IsNullOrEmpty(b.ParentName) ? null : b.ParentName,
                BindPose = b.BindPoseMatrix is { Length: 16 } bind ? bind : Identity(),
                InverseBindPose = b.InverseBindPoseMatrix is { Length: 16 } inv ? inv : Identity(),
            });
        }
        return bones;
    }

    // Transfer EA-body canonical vertex IDs onto ID-less (CC) body meshes by nearest point, so BGEO
    // morphs (which look up each vertex's EA ID) can reach them. Builds an EA-default reference scene with
    // NO mod override — the renderer names body meshes by their EA instance, so the CC mesh and its EA
    // counterpart share a name and we can pair them up. Higher-poly CC verts simply share the nearest EA
    // ID (the morph delta is the same per ID), which is exactly how the game/TS4SimRipper snap meshes.
    private static async Task<CanonicalScene> SnapBodyVertexIdsAsync(
        CanonicalScene scene, CharacterDefinition def, ulong skintoneInstance, Func<IHost> hostFactory, CancellationToken ct)
    {
        var needy = new List<int>();
        for (var i = 0; i < scene.Meshes.Count; i++)
        {
            if (scene.Meshes[i].VertexIds is null || scene.Meshes[i].VertexIds!.Count == 0) needy.Add(i);
        }
        if (needy.Count == 0) return scene;
        Console.WriteLine($"[snapids] {needy.Count} mesh(es) lack vertex IDs; building EA-default reference to transfer IDs by nearest point...");

        CanonicalScene? eaScene = null;
        var saved = ModOverrideHolder.Current;
        try
        {
            using var eaHost = hostFactory();
            var eaIndex = eaHost.Services.GetRequiredService<IIndexStore>();
            await eaIndex.InitializeAsync(ct).ConfigureAwait(false);
            ModOverrideHolder.Current = null; // EA default — no mod override armed
            var eaSynth = eaHost.Services.GetRequiredService<ISyntheticSimService>();
            var eaRend = eaHost.Services.GetRequiredService<ISimAssetGraphRenderer>();
            var eaSeed = eaSynth.CreateHumanSeed(def.Age, def.Gender, skintoneInstance);
            var eaGraph = await eaSynth.BuildHumanAssetGraphAsync(eaSeed, ct).ConfigureAwait(false);
            if (eaGraph.SimGraph is not null)
            {
                var eaRr = await eaRend.BuildSimSceneAsync(eaGraph, ct).ConfigureAwait(false);
                eaScene = eaRr.Scene;
            }
        }
        finally { ModOverrideHolder.Current = saved; }

        if (eaScene is null)
        {
            Console.Error.WriteLine("[snapids] EA reference build failed; body meshes keep no IDs (body morphs disabled).");
            return scene;
        }

        var eaByName = new Dictionary<string, CanonicalMesh>(StringComparer.Ordinal);
        foreach (var em in eaScene.Meshes)
        {
            if (em.VertexIds is { Count: > 0 } && !eaByName.ContainsKey(em.Name)) eaByName[em.Name] = em;
        }
        Console.WriteLine($"[snapids] EA reference: {eaByName.Count} mesh(es) carry vertex IDs ({string.Join(", ", eaByName.Keys)}).");

        var newMeshes = scene.Meshes.ToArray();
        foreach (var idx in needy)
        {
            var bb = scene.Meshes[idx];
            if (!eaByName.TryGetValue(bb.Name, out var ea))
            {
                Console.Error.WriteLine($"[snapids] '{bb.Name}': no EA reference with IDs by name; skipped (no body morphs on it).");
                continue;
            }
            var ids = SnapNearest(bb.Positions, ea.Positions, ea.VertexIds!);
            newMeshes[idx] = bb with { VertexIds = ids };
            Console.WriteLine($"[snapids] '{bb.Name}': transferred {ids.Count} IDs from EA ref ({ea.Positions.Count / 3} ref verts, {ea.VertexIds!.Count} ref ids).");
        }
        return scene with { Meshes = newMeshes };
    }

    // For each target vertex, copy the canonical ID of the nearest reference vertex (brute force; meshes
    // are ~2k verts so this is a few million ops — trivial, and exact).
    private static IReadOnlyList<uint> SnapNearest(IReadOnlyList<float> tgtPos, IReadOnlyList<float> refPos, IReadOnlyList<uint> refIds)
    {
        var tn = tgtPos.Count / 3;
        var rn = Math.Min(refPos.Count / 3, refIds.Count);
        var ids = new uint[tn];
        for (var t = 0; t < tn; t++)
        {
            float x = tgtPos[t * 3], y = tgtPos[(t * 3) + 1], z = tgtPos[(t * 3) + 2];
            var best = 0; var bestD = float.MaxValue;
            for (var r = 0; r < rn; r++)
            {
                var dx = refPos[r * 3] - x; var dy = refPos[(r * 3) + 1] - y; var dz = refPos[(r * 3) + 2] - z;
                var d = (dx * dx) + (dy * dy) + (dz * dz);
                if (d < bestD) { bestD = d; best = r; }
            }
            ids[t] = rn > 0 ? refIds[best] : 0u;
        }
        return ids;
    }

    // Curation tool (scanmorphs): score EVERY adult-female BGEO SMOD against the assembled scene and rank
    // by symmetry/coherence, so we pick clean, meaningful sliders instead of arbitrary SMODs. Also prints
    // each scene mesh's vertex-id coverage (so we can see whether body morphs can reach the body mesh).
    private static async Task ScanAllMorphsAsync(
        CanonicalScene scene, IIndexStore indexStore, IResourceCatalogService catalogSvc, CancellationToken ct)
    {
        Console.WriteLine("============================================================");
        for (var i = 0; i < scene.Meshes.Count; i++)
        {
            var m = scene.Meshes[i];
            Console.WriteLine($"[scanmorphs] mesh[{i}] '{m.Name}' verts={m.Positions.Count / 3} vertexIds={(m.VertexIds?.Count ?? 0)} tris={m.Indices.Count / 3}");
        }

        var bgeoResolver = new BlendGeometryResolver(indexStore, catalogSvc);
        var dmapResolver = new DeformerMapResolver(indexStore, catalogSvc);
        var smods = await indexStore.GetResourcesByTypeNameAsync("SimModifier", ct).ConfigureAwait(false);
        Console.WriteLine($"[scanmorphs] {smods.Count} SMOD resource(s) indexed; scoring adult-female BGEO (face) + DMap (body) morphs...");

        var rows = new List<(string Kind, string Tgi, uint Region, string Mesh, int Moved, float Sym, float Coh, int Singletons, int Miss, float Mm)>();
        var seen = new HashSet<ulong>();
        int scanned = 0, afDistinct = 0, failed = 0;

        // Diff a morphed scene vs base, pick the mesh that moved the most, score it, add a ranked row.
        void ScoreAndAdd(string kind, ResourceMetadata r, uint region, CanonicalScene morphed)
        {
            int bestMesh = -1, bestMoved = 0; List<int>? bIdx = null; List<float>? bDel = null; var bMax = 0f;
            for (var i = 0; i < scene.Meshes.Count && i < morphed.Meshes.Count; i++)
            {
                var bp = scene.Meshes[i].Positions; var mp = morphed.Meshes[i].Positions;
                if (mp.Count != bp.Count) continue;
                var idx = new List<int>(); var del = new List<float>(); var mx = 0f;
                for (var vi = 0; (vi * 3) + 2 < bp.Count; vi++)
                {
                    var dx = mp[vi * 3] - bp[vi * 3]; var dy = mp[(vi * 3) + 1] - bp[(vi * 3) + 1]; var dz = mp[(vi * 3) + 2] - bp[(vi * 3) + 2];
                    var mag = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
                    if (mag < 1e-5f) continue;
                    idx.Add(vi); del.Add(dx); del.Add(dy); del.Add(dz); if (mag > mx) mx = mag;
                }
                if (idx.Count > bestMoved) { bestMoved = idx.Count; bestMesh = i; bIdx = idx; bDel = del; bMax = mx; }
            }
            if (bestMesh < 0 || bIdx is null || bDel is null) return;
            var sc = ScoreMorphCore(scene.Meshes[bestMesh].Positions, scene.Meshes[bestMesh].Indices, bIdx, bDel);
            rows.Add((kind, $"{r.Key.Type:X8}:00000000:{r.Key.FullInstance:X16}", region, scene.Meshes[bestMesh].Name,
                bestMoved, sc.symPct, sc.coherence, sc.singletons, sc.mirrorMissing, bMax * 1000f));
        }

        foreach (var r in smods)
        {
            ct.ThrowIfCancellationRequested();
            byte[] bytes;
            try { bytes = await catalogSvc.GetResourceBytesAsync(r.PackagePath, r.Key, false, ct, null).ConfigureAwait(false); }
            catch { failed++; continue; }
            Sims4ResourceExplorer.Packages.Ts4SimModifierResource smod;
            try { smod = Sims4ResourceExplorer.Packages.Ts4SimModifierResource.Parse(bytes); }
            catch { failed++; continue; }
            scanned++;
            if ((smod.AgeGender & 0x2000u) == 0 || (smod.AgeGender & 0x20u) == 0) continue; // adult-female only
            if (!seen.Add(r.Key.FullInstance)) continue;
            afDistinct++;

            // BGEO (face — keyed by head vertex IDs).
            if (smod.BgeoKeys.Count > 0)
            {
                try
                {
                    var bm = await bgeoResolver.ResolveSmodMorphsAsync(r.Key.Type, r.Key.FullInstance, 1f, ct).ConfigureAwait(false);
                    if (bm.Count > 0)
                        ScoreAndAdd("BGEO", r, smod.Region,
                            Sims4ResourceExplorer.Preview.BlendGeometryMorpher.MorphScene(scene, bm, null, Sims4ResourceExplorer.Preview.FaceMorphWeighting.Ignore));
                }
                catch { failed++; }
            }
            // DMap shape (body — keyed by UV1 + tags, so it reaches the body meshes that lack vertex IDs).
            if (smod.HasShapeDeformerMap)
            {
                try
                {
                    var dm = await dmapResolver.ResolveSmodAsync(r.Key.Type, r.Key.FullInstance, 1f, ct).ConfigureAwait(false);
                    if (dm.Count > 0)
                        ScoreAndAdd("DMAP", r, smod.Region,
                            Sims4ResourceExplorer.Preview.DeformerMapMorpher.MorphScene(scene, dm));
                }
                catch { failed++; }
            }
        }

        Console.WriteLine($"[scanmorphs] scanned={scanned} af-distinct={afDistinct} failed={failed} scored={rows.Count}");
        var ranked = rows.OrderByDescending(x => x.Sym).ThenByDescending(x => x.Coh).ToList();
        Console.WriteLine("[scanmorphs] rank kind sym%  coh%  singl  miss  moved  maxMm  mesh / region / smod");
        var rank = 0;
        foreach (var x in ranked)
        {
            rank++;
            var mesh = x.Mesh.Length > 18 ? x.Mesh.Substring(0, 18) : x.Mesh;
            Console.WriteLine($"[scanmorphs] {rank,4} {x.Kind,-4} {x.Sym,4:0}  {x.Coh,4:0}  {x.Singletons,4}  {x.Miss,4}  {x.Moved,5}  {x.Mm,5:0.0}  {mesh,-18} r0x{x.Region:X}  {x.Tgi}");
        }
        var tsv = Path.Combine(Path.GetTempPath(), "scanmorphs.tsv");
        await File.WriteAllLinesAsync(tsv, ranked.Select(x =>
            $"{x.Kind}\t{x.Sym:0}\t{x.Coh:0}\t{x.Singletons}\t{x.Miss}\t{x.Moved}\t{x.Mm:0.0}\t{x.Mesh}\t0x{x.Region:X}\t{x.Tgi}"), ct).ConfigureAwait(false);
        Console.WriteLine($"[scanmorphs] wrote {ranked.Count} rows -> {tsv}");
    }

    // Apply each catalog morph to a scene (BGEO via vertex IDs for face / DMap via UV1+tags for body),
    // diff vs base, and collect per-mesh sparse blend-shape deltas. Reused for the default scene and every
    // body variant so morphs reach any mesh. 'verbose' logs per-morph stats (default pass only).
    internal static async Task<Dictionary<int, List<CharacterBlendShape>>> BakeMorphsAsync(
        CanonicalScene scene, IReadOnlyList<BodyMorphOption> morphs,
        BlendGeometryResolver bgeoResolver, DeformerMapResolver dmapResolver,
        Sims4ResourceExplorer.Preview.FaceMorphWeighting weightMode, bool verbose, CancellationToken ct)
    {
        var morphDeltas = new Dictionary<int, List<CharacterBlendShape>>();
        foreach (var bm in morphs)
        {
            if (!TryParseTgi(bm.Smod, out var smType, out _, out var smInst))
            {
                if (verbose) Console.Error.WriteLine($"[morph] '{bm.Id}': bad smod tgi '{bm.Smod}'.");
                continue;
            }
            var weight = bm.Scale <= 0f ? 1f : bm.Scale; // per-morph scale tames DMap over-shoot at weight 1
            CanonicalScene morphed;
            try
            {
                if (string.Equals(bm.Kind, "dmap", StringComparison.OrdinalIgnoreCase))
                {
                    var dmaps = await dmapResolver.ResolveSmodAsync(smType, smInst, weight, ct).ConfigureAwait(false);
                    if (dmaps.Count == 0) continue;
                    morphed = Sims4ResourceExplorer.Preview.DeformerMapMorpher.MorphScene(scene, dmaps);
                }
                else
                {
                    var bgeo = await bgeoResolver.ResolveSmodMorphsAsync(smType, smInst, weight, ct).ConfigureAwait(false);
                    if (bgeo.Count == 0) continue;
                    morphed = Sims4ResourceExplorer.Preview.BlendGeometryMorpher.MorphScene(scene, bgeo, null, weightMode);
                }
            }
            catch (Exception ex)
            {
                if (verbose) Console.Error.WriteLine($"[morph] '{bm.Id}' resolve/morph FAILED ({ex.GetType().Name}: {ex.Message}).");
                continue;
            }

            var totalMoved = 0; var maxDisp = 0f; var meshesHit = 0;
            for (var i = 0; i < scene.Meshes.Count && i < morphed.Meshes.Count; i++)
            {
                var basePos = scene.Meshes[i].Positions;
                var morphPos = morphed.Meshes[i].Positions;
                if (morphPos.Count != basePos.Count) continue;
                var indices = new List<int>(); var deltas = new List<float>();
                for (var vi = 0; (vi * 3) + 2 < basePos.Count; vi++)
                {
                    var dx = morphPos[vi * 3] - basePos[vi * 3];
                    var dy = morphPos[(vi * 3) + 1] - basePos[(vi * 3) + 1];
                    var dz = morphPos[(vi * 3) + 2] - basePos[(vi * 3) + 2];
                    var mag = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
                    if (mag < 1e-5f) continue;
                    indices.Add(vi); deltas.Add(dx); deltas.Add(dy); deltas.Add(dz);
                    if (mag > maxDisp) maxDisp = mag;
                }
                if (indices.Count == 0) continue;
                meshesHit++; totalMoved += indices.Count;
                if (!morphDeltas.TryGetValue(i, out var list)) { list = new List<CharacterBlendShape>(); morphDeltas[i] = list; }
                list.Add(new CharacterBlendShape { Name = bm.Id, Indices = indices, DeltaPositions = deltas });
            }
            if (verbose && meshesHit > 0)
            {
                Console.WriteLine($"[morph] '{bm.Id}' ({bm.Label}): {meshesHit} mesh(es), {totalMoved} vert(s), max {maxDisp * 1000f:0.0}mm.");
            }
        }
        if (verbose) Console.WriteLine($"[morph] baked {morphs.Count} morph(s) into blend shapes on {morphDeltas.Count} mesh(es).");
        return morphDeltas;
    }

    // Resolve each slider's BONE component: SMOD → BonePoseKey → BOND → BoneAdjust list, bone hashes
    // mapped to names via auRig. Near-identity adjustments are filtered (BOND rosters carry many no-op
    // entries). Scale stays a DELTA (0 = none) exactly as stored — Unity applies 1 + delta·weight
    // (TS4SimRipper RIG.Bone.UpdateLocalData semantics); offsets add in the parent frame; a zero
    // quaternion means identity (SimRipper: isEmpty → Identity).
    private static async Task<List<CharacterBoneMorph>> ResolveBoneMorphsAsync(
        IReadOnlyList<BodyMorphOption> options, IIndexStore index, IResourceCatalogService catalog, CancellationToken ct)
    {
        // auRig bone-hash → name.
        var boneNames = new Dictionary<uint, string>();
        var auInst = Ts4CanonicalRigCatalog.ComputeFnv64("auRig");
        foreach (var rr in (await index.GetResourcesByFullInstanceAsync(auInst, ct).ConfigureAwait(false))
                 .Where(r => string.Equals(r.Key.TypeName, "Rig", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var rb = await catalog.GetResourceBytesAsync(rr.PackagePath, rr.Key, raw: false, ct).ConfigureAwait(false);
                foreach (var bn in Sims4ResourceExplorer.Preview.Ts4RigResource.Parse(rb).Bones) boneNames[bn.NameHash] = bn.Name;
            }
            catch { /* rig variants are duplicates; any one suffices */ }
        }

        var result = new List<CharacterBoneMorph>();
        int withBones = 0, boneOnly = 0;
        foreach (var bm in options)
        {
            if (!TryParseTgi(bm.Smod, out var smType, out _, out var smInst)) continue;
            Sims4ResourceExplorer.Packages.Ts4SimModifierResource smod;
            try
            {
                var smodRes = (await index.GetResourcesByFullInstanceAsync(smInst, ct).ConfigureAwait(false))
                    .FirstOrDefault(r => r.Key.Type == smType);
                if (smodRes is null) continue;
                var sb = await catalog.GetResourceBytesAsync(smodRes.PackagePath, smodRes.Key, raw: false, ct).ConfigureAwait(false);
                smod = Sims4ResourceExplorer.Packages.Ts4SimModifierResource.Parse(sb);
            }
            catch { continue; }
            if (!smod.HasBondReference) continue;

            Sims4ResourceExplorer.Packages.Ts4BondResource bond;
            try
            {
                var bondRes = (await index.GetResourcesByFullInstanceAsync(smod.BonePoseKey.Instance, ct).ConfigureAwait(false))
                    .FirstOrDefault(r => r.Key.Type == smod.BonePoseKey.Type);
                if (bondRes is null) continue;
                var bb = await catalog.GetResourceBytesAsync(bondRes.PackagePath, bondRes.Key, raw: false, ct).ConfigureAwait(false);
                bond = Sims4ResourceExplorer.Packages.Ts4BondResource.Parse(bb);
            }
            catch { continue; }

            var entries = new List<CharacterBoneMorphEntry>();
            foreach (var a in bond.Adjustments)
            {
                if (!boneNames.TryGetValue(a.SlotHash, out var boneName)) continue;
                // Zero quaternion = identity (per SimRipper); normalize otherwise.
                float qx = a.QuatX, qy = a.QuatY, qz = a.QuatZ, qw = a.QuatW;
                var qLen = MathF.Sqrt((qx * qx) + (qy * qy) + (qz * qz) + (qw * qw));
                if (qLen < 1e-6f) { qx = qy = qz = 0f; qw = 1f; }
                else { qx /= qLen; qy /= qLen; qz /= qLen; qw /= qLen; }
                var offMag = MathF.Max(MathF.Abs(a.OffsetX), MathF.Max(MathF.Abs(a.OffsetY), MathF.Abs(a.OffsetZ)));
                var sclMag = MathF.Max(MathF.Abs(a.ScaleX), MathF.Max(MathF.Abs(a.ScaleY), MathF.Abs(a.ScaleZ)));
                var rotDev = MathF.Sqrt((qx * qx) + (qy * qy) + (qz * qz)); // sin(angle/2)
                if (offMag < 2e-4f && sclMag < 2e-3f && rotDev < 5e-4f) continue; // no-op roster entry
                entries.Add(new CharacterBoneMorphEntry
                {
                    Bone = boneName,
                    OX = a.OffsetX, OY = a.OffsetY, OZ = a.OffsetZ,
                    SX = a.ScaleX, SY = a.ScaleY, SZ = a.ScaleZ,
                    QX = qx, QY = qy, QZ = qz, QW = qw,
                });
            }
            if (entries.Count == 0) continue;
            result.Add(new CharacterBoneMorph { Name = bm.Id, Entries = entries });
            withBones++;
            if (string.Equals(bm.Kind, "bond", StringComparison.OrdinalIgnoreCase)) boneOnly++;
        }
        Console.WriteLine($"[boneMorph] {withBones}/{options.Count} sliders carry bone adjustments ({boneOnly} bone-only); auRig bones mapped: {boneNames.Count}.");
        return result;
    }

    // Diagnostic for the morph investigation: scores a baked morph against the user's two complaints —
    // "single vertex without neighbors" (coherence/singletons) and "asymmetric". Calibrate on a known-good
    // morph (cheeks=morph_d): expect coherence>85%, singletons≈0, symmetric>90%. A scattered/contaminated
    // morph shows low coherence + many singletons; a one-sided/mis-targeted morph shows low symmetry.
    private static string ScoreMorph(IReadOnlyList<float> positions, IReadOnlyList<int> tris, List<int> moved, List<float> deltas)
    {
        var s = ScoreMorphCore(positions, tris, moved, deltas);
        if (s.movedCount == 0) return "no moved verts";
        return $"moved={s.movedCount} coherence={s.coherence:0}% (largest {s.largest}) singletons={s.singletons} | mirror={s.axis} symmetric={s.symPct:0}% (of {s.withPartner}) mirrorMissing={s.mirrorMissing}";
    }

    private static (int movedCount, float coherence, int largest, int singletons, char axis, float symPct, int withPartner, int mirrorMissing)
        ScoreMorphCore(IReadOnlyList<float> positions, IReadOnlyList<int> tris, List<int> moved, List<float> deltas)
    {
        var vertCount = positions.Count / 3;
        if (moved.Count == 0) return (0, 0f, 0, 0, '-', 0f, 0, 0);
        var movedSet = new HashSet<int>(moved);
        var dmap = new Dictionary<int, (float X, float Y, float Z)>(moved.Count);
        for (var k = 0; k < moved.Count; k++)
        {
            dmap[moved[k]] = (deltas[k * 3], deltas[(k * 3) + 1], deltas[(k * 3) + 2]);
        }

        // --- Coherence: triangle adjacency restricted to the moved set ---
        var adj = new Dictionary<int, HashSet<int>>(moved.Count);
        void Link(int a, int b)
        {
            if (a == b || !movedSet.Contains(a) || !movedSet.Contains(b)) return;
            if (!adj.TryGetValue(a, out var sa)) { sa = new HashSet<int>(); adj[a] = sa; }
            sa.Add(b);
            if (!adj.TryGetValue(b, out var sb)) { sb = new HashSet<int>(); adj[b] = sb; }
            sb.Add(a);
        }
        for (var t = 0; t + 2 < tris.Count; t += 3)
        {
            Link(tris[t], tris[t + 1]);
            Link(tris[t + 1], tris[t + 2]);
            Link(tris[t + 2], tris[t]);
        }
        var singletons = 0;
        foreach (var v in moved) if (!adj.ContainsKey(v)) singletons++;
        var visited = new HashSet<int>();
        var largest = 0;
        foreach (var v in moved)
        {
            if (!visited.Add(v)) continue;
            var comp = 1;
            var stack = new Stack<int>();
            stack.Push(v);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (adj.TryGetValue(cur, out var nb))
                {
                    foreach (var w in nb) if (visited.Add(w)) { comp++; stack.Push(w); }
                }
            }
            if (comp > largest) largest = comp;
        }
        var coherence = 100f * largest / moved.Count;

        // --- Symmetry: detect the mesh mirror plane from ALL geometry, then score moved deltas ---
        const float q = 0.0005f; // 0.5 mm quantization
        static string Key(float x, float y, float z) =>
            $"{(long)MathF.Round(x / q)}|{(long)MathF.Round(y / q)}|{(long)MathF.Round(z / q)}";
        var posKey = new Dictionary<string, int>(vertCount);
        for (var v = 0; v < vertCount; v++)
        {
            posKey[Key(positions[v * 3], positions[(v * 3) + 1], positions[(v * 3) + 2])] = v;
        }
        var axis = 'X';
        var bestPairs = -1;
        foreach (var ax in new[] { 'X', 'Y', 'Z' })
        {
            var pairs = 0;
            for (var v = 0; v < vertCount; v++)
            {
                float x = positions[v * 3], y = positions[(v * 3) + 1], z = positions[(v * 3) + 2];
                var k = ax == 'X' ? Key(-x, y, z) : ax == 'Y' ? Key(x, -y, z) : Key(x, y, -z);
                if (posKey.TryGetValue(k, out var m) && m != v) pairs++;
            }
            if (pairs > bestPairs) { bestPairs = pairs; axis = ax; }
        }
        var withPartner = 0;
        var symmetric = 0;
        var mirrorMissing = 0;
        foreach (var v in moved)
        {
            float x = positions[v * 3], y = positions[(v * 3) + 1], z = positions[(v * 3) + 2];
            var k = axis == 'X' ? Key(-x, y, z) : axis == 'Y' ? Key(x, -y, z) : Key(x, y, -z);
            if (!posKey.TryGetValue(k, out var m) || m == v) continue;
            withPartner++;
            if (!movedSet.Contains(m)) { mirrorMissing++; continue; }
            var (dx, dy, dz) = dmap[v];
            var (mx, my, mz) = dmap[m];
            float ex = axis == 'X' ? -mx : mx, ey = axis == 'Y' ? -my : my, ez = axis == 'Z' ? -mz : mz;
            var diff = MathF.Sqrt(((dx - ex) * (dx - ex)) + ((dy - ey) * (dy - ey)) + ((dz - ez) * (dz - ez)));
            var scale = MathF.Max(MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz)), MathF.Sqrt((mx * mx) + (my * my) + (mz * mz)));
            if (diff <= MathF.Max(5e-5f, 0.10f * scale)) symmetric++;
        }
        var symPct = withPartner > 0 ? 100f * symmetric / withPartner : 0f;

        return (moved.Count, coherence, largest, singletons, axis, symPct, withPartner, mirrorMissing);
    }

    private static CharacterMesh SceneToMesh(CanonicalMesh mesh, int skeletonBoneCount)
    {
        var vertexCount = mesh.Positions.Count / 3;
        var (boneIndices, boneWeights) = BuildBlendWeights(mesh, vertexCount, skeletonBoneCount);
        var uv0 = mesh.Uvs is { Count: > 0 } ? mesh.Uvs : (mesh.Uv0s ?? (IReadOnlyList<float>)Array.Empty<float>());
        var uv1 = mesh.Uv1s ?? (IReadOnlyList<float>)Array.Empty<float>();
        var normals = mesh.Normals ?? (IReadOnlyList<float>)Array.Empty<float>();
        return new CharacterMesh
        {
            VertexCount = vertexCount,
            Positions = mesh.Positions.ToList(),
            Normals = normals.ToList(),
            Uv0 = uv0.ToList(),
            Uv1 = uv1.ToList(),
            Triangles = mesh.Indices.ToList(),
            BoneIndices = boneIndices,
            BoneWeights = boneWeights,
        };
    }

    private static (List<int>, List<float>) BuildBlendWeights(CanonicalMesh mesh, int vertexCount, int skeletonBoneCount)
    {
        var slots = Math.Max(0, vertexCount) * 4;
        var boneIndices = new int[slots];
        var boneWeights = new float[slots];
        var byVertex = new Dictionary<int, List<VertexWeight>>();
        foreach (var weight in mesh.SkinWeights)
        {
            if (weight.VertexIndex < 0 || weight.VertexIndex >= vertexCount) continue;
            if (!byVertex.TryGetValue(weight.VertexIndex, out var list))
            {
                list = new List<VertexWeight>();
                byVertex[weight.VertexIndex] = list;
            }
            list.Add(weight);
        }
        int Clamp(int b) => skeletonBoneCount <= 0 ? 0 : Math.Clamp(b, 0, skeletonBoneCount - 1);
        for (var v = 0; v < vertexCount; v++)
        {
            var baseSlot = v * 4;
            if (!byVertex.TryGetValue(v, out var inf) || inf.Count == 0)
            {
                boneIndices[baseSlot] = 0;
                boneWeights[baseSlot] = 1f;
                continue;
            }
            var top = inf.OrderByDescending(w => w.Weight).Take(4).ToList();
            var sum = 0f;
            for (var i = 0; i < top.Count; i++)
            {
                boneIndices[baseSlot + i] = Clamp(top[i].BoneIndex);
                boneWeights[baseSlot + i] = top[i].Weight;
                sum += top[i].Weight;
            }
            if (sum > 0f)
            {
                for (var i = 0; i < top.Count; i++) boneWeights[baseSlot + i] /= sum;
            }
            else
            {
                boneIndices[baseSlot] = Clamp(top[0].BoneIndex);
                boneWeights[baseSlot] = 1f;
            }
        }
        return (boneIndices.ToList(), boneWeights.ToList());
    }

    private static bool IsBodyRegion(string? region) => region switch
    {
        "Top" or "Bottom" or "Feet" or "Shoes" or "Body" => true,
        _ => false,
    };

    // Like SceneToMesh, but remaps each vertex's bone indices from the VARIANT's bone order to the
    // SHARED skeleton (by name, via `remap`) so a variant's body parts skin to the customizer's skeleton.
    private static CharacterMesh SceneToMeshRemapped(CanonicalMesh mesh, int sharedBoneCount, Func<int, int> remap)
    {
        var vertexCount = mesh.Positions.Count / 3;
        var (boneIndices, boneWeights) = BuildBlendWeightsRemapped(mesh, vertexCount, sharedBoneCount, remap);
        var uv0 = mesh.Uvs is { Count: > 0 } ? mesh.Uvs : (mesh.Uv0s ?? (IReadOnlyList<float>)Array.Empty<float>());
        var uv1 = mesh.Uv1s ?? (IReadOnlyList<float>)Array.Empty<float>();
        var normals = mesh.Normals ?? (IReadOnlyList<float>)Array.Empty<float>();
        return new CharacterMesh
        {
            VertexCount = vertexCount,
            Positions = mesh.Positions.ToList(),
            Normals = normals.ToList(),
            Uv0 = uv0.ToList(),
            Uv1 = uv1.ToList(),
            Triangles = mesh.Indices.ToList(),
            BoneIndices = boneIndices,
            BoneWeights = boneWeights,
        };
    }

    private static (List<int>, List<float>) BuildBlendWeightsRemapped(
        CanonicalMesh mesh, int vertexCount, int sharedBoneCount, Func<int, int> remap)
    {
        var slots = Math.Max(0, vertexCount) * 4;
        var boneIndices = new int[slots];
        var boneWeights = new float[slots];
        var byVertex = new Dictionary<int, List<VertexWeight>>();
        foreach (var weight in mesh.SkinWeights)
        {
            if (weight.VertexIndex < 0 || weight.VertexIndex >= vertexCount) continue;
            if (!byVertex.TryGetValue(weight.VertexIndex, out var list))
            {
                list = new List<VertexWeight>();
                byVertex[weight.VertexIndex] = list;
            }
            list.Add(weight);
        }
        int Clamp(int b) => sharedBoneCount <= 0 ? 0 : Math.Clamp(b, 0, sharedBoneCount - 1);
        for (var v = 0; v < vertexCount; v++)
        {
            var baseSlot = v * 4;
            if (!byVertex.TryGetValue(v, out var inf) || inf.Count == 0)
            {
                boneIndices[baseSlot] = 0;
                boneWeights[baseSlot] = 1f;
                continue;
            }
            var top = inf.OrderByDescending(w => w.Weight).Take(4).ToList();
            var sum = 0f;
            for (var i = 0; i < top.Count; i++)
            {
                boneIndices[baseSlot + i] = Clamp(remap(top[i].BoneIndex));
                boneWeights[baseSlot + i] = top[i].Weight;
                sum += top[i].Weight;
            }
            if (sum > 0f)
            {
                for (var i = 0; i < top.Count; i++) boneWeights[baseSlot + i] /= sum;
            }
            else
            {
                boneIndices[baseSlot] = Clamp(remap(top[0].BoneIndex));
                boneWeights[baseSlot] = 1f;
            }
        }
        return (boneIndices.ToList(), boneWeights.ToList());
    }

    // ---- small helpers ----------------------------------------------------------------------------
    private static void DilateIfTransparent(string path, string label)
    {
        try
        {
            if (TextureCleanup.HasTransparentPixels(path))
            {
                var n = TextureCleanup.DilateOpaque(path, 16);
                Console.WriteLine($"[layer]   {label}: dilated {n} gutter pixel(s) with skin color.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[layer]   {label}: cleanup FAILED ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    // Mean opaque RGB warm-check: skin should read warm (R > G > B). Prints mean + verdict so the
    // report can confirm the baked atlas is not washed gray/green.
    private static void ReportWarmCheck(string pngPath)
    {
        try
        {
            using var src = System.Drawing.Image.FromFile(pngPath);
            using var bmp = new System.Drawing.Bitmap(src);
            var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            double sr = 0, sg = 0, sb = 0; long n = 0;
            try
            {
                var stride = data.Stride;
                var buf = new byte[stride * bmp.Height];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, buf.Length);
                // Sample on a coarse grid for speed (every 8th pixel).
                for (var y = 0; y < bmp.Height; y += 8)
                {
                    var rowBase = y * stride;
                    for (var x = 0; x < bmp.Width; x += 8)
                    {
                        var i = rowBase + x * 4;
                        if (buf[i + 3] < 250) continue; // opaque only
                        sb += buf[i]; sg += buf[i + 1]; sr += buf[i + 2]; n++;
                    }
                }
            }
            finally { bmp.UnlockBits(data); }
            if (n == 0) { Console.WriteLine("[preview] warm-check: no opaque pixels sampled."); return; }
            var mr = sr / n; var mg = sg / n; var mb = sb / n;
            var warm = mr > mb && mr >= mg;
            Console.WriteLine(FormattableString.Invariant(
                $"[preview] warm-check: mean opaque RGB = ({mr:0},{mg:0},{mb:0}) over {n} samples -> {(warm ? "WARM (R>G>=... and R>B: skin-like)" : "NOT warm (R<=B — investigate)")}."));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[preview] warm-check FAILED ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    private static (int, int) ProbeDims(string pngPath)
    {
        try
        {
            using var img = System.Drawing.Image.FromFile(pngPath);
            return (img.Width, img.Height);
        }
        catch { return (0, 0); }
    }

    private static bool TryParseTgi(string? tgi, out uint type, out uint group, out ulong instance)
    {
        type = 0; group = 0; instance = 0;
        if (string.IsNullOrWhiteSpace(tgi)) return false;
        var parts = tgi.Split(':');
        if (parts.Length != 3) return false;
        var ci = CultureInfo.InvariantCulture;
        var ns = NumberStyles.HexNumber;
        return uint.TryParse(parts[0], ns, ci, out type)
            && uint.TryParse(parts[1], ns, ci, out group)
            && ulong.TryParse(parts[2], ns, ci, out instance);
    }

    private static string TypeNameForType(uint type) => type switch
    {
        0x2BC04EDFu => "LRLEImage",
        0x3453CF95u => "RLE2Image",
        _ => "Texture",
    };

    private static string Sanitize(string id) =>
        new string(id.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    private static string Capitalize(string s) =>
        string.IsNullOrWhiteSpace(s) ? s : char.ToUpperInvariant(s.Trim()[0]) + s.Trim().Substring(1);

    // Sim CAS meshes are named "Mesh_<hex>", so the NAME alone is always Unknown — the region lives
    // in the material's Approximation token (Body-shell / Head-shell / …), exactly as exportsim reads it.
    // "Top"/"Bottom"/"Feet" pretty form for a region key.
    private static string CapitalizeRegion(string r) => r switch
    {
        "top" => "Top", "bottom" => "Bottom", "feet" => "Feet", _ => r
    };

    // Map each EA default-body GEOM instance hash → region slot, harvested from the catalog's
    // eaTop/eaBottom/eaFeet TGIs (e.g. B6DCAB99F33C43EE → top). Body meshes are named by the EA
    // instance the renderer requested, so this classifies them no matter which mod served the GEOM.
    private static Dictionary<string, string> BuildRegionByInstance(Catalog catalog)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bm in catalog.BodyMesh)
        {
            var ov = bm.Overrides;
            if (ov is null) continue;
            AddInst(map, ov.EaTop, "top");
            AddInst(map, ov.EaBottom, "bottom");
            AddInst(map, ov.EaFeet, "feet");
        }
        return map;
    }

    private static void AddInst(Dictionary<string, string> map, string? tgi, string region)
    {
        if (string.IsNullOrWhiteSpace(tgi)) return;
        var inst = tgi.Contains(':') ? tgi[(tgi.LastIndexOf(':') + 1)..] : tgi;
        if (!string.IsNullOrEmpty(inst)) map[inst] = region;
    }

    // Which region slot a body mesh belongs to, by the EA instance embedded in its name.
    private static string? RegionOf(string? meshName, Dictionary<string, string> regionByInstance)
    {
        var n = meshName ?? string.Empty;
        foreach (var kv in regionByInstance)
            if (n.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) return kv.Value;
        return null;
    }

    // Fallback region classifier by vertical extent, for body meshes whose GEOM instance isn't in the
    // (female-derived) EA-instance map — chiefly the EA MALE body. Positions are in character space
    // (metres, Y-up, feet ≈ 0, head ≈ 1.8). Feet sit entirely at/below the ankle; the torso/top (incl.
    // arms) starts above the hip; everything between is the legs/bottom. Never returns null.
    private static string RegionByGeometry(CharacterMesh mesh)
    {
        var p = mesh?.Positions;
        if (p is null || p.Count < 3) return "bottom";
        float minY = float.MaxValue, maxY = float.MinValue;
        for (var i = 1; i < p.Count; i += 3)
        {
            var y = p[i];
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        if (maxY < 0.35f) return "feet"; // entirely at/below the ankle
        if (minY > 0.60f) return "top";  // starts above the hip → torso + arms
        return "bottom";                 // spans the legs
    }

    // The region slots a source contributes: a source with NO overrides is the EA baseline (all
    // three); otherwise the regions it declares (redirect TGI or Form-1 package).
    private static List<string> ContributedRegions(BodyMeshOption opt)
    {
        var ov = opt.Overrides;
        if (ov is null) return new List<string> { "top", "bottom", "feet" };
        var r = new List<string>();
        if (!string.IsNullOrWhiteSpace(ov.Top) || !string.IsNullOrWhiteSpace(ov.TopPackage)) r.Add("top");
        if (!string.IsNullOrWhiteSpace(ov.Bottom) || !string.IsNullOrWhiteSpace(ov.BottomPackage)) r.Add("bottom");
        if (!string.IsNullOrWhiteSpace(ov.Feet) || !string.IsNullOrWhiteSpace(ov.FeetPackage)) r.Add("feet");
        return r;
    }

    private static string InferRegion(string? meshName, CanonicalMaterial? material)
    {
        var name = (meshName ?? string.Empty).ToLowerInvariant();
        var note = (material?.Approximation ?? string.Empty).ToLowerInvariant();
        var hay = name + " " + note;
        if (Contains(hay, "eyebrow", "brow")) return "Brows";
        if (Contains(hay, "eye", "iris", "sclera", "eyeball", "cornea")) return "Eyes";
        if (Contains(hay, "head", "face", "scalp")) return "Head";
        if (Contains(hay, "hand", "glove")) return "Hands";
        if (Contains(hay, "foot", "feet")) return "Feet";
        if (Contains(hay, "shoe", "boot", "sock")) return "Shoes";
        if (Contains(hay, "top", "shirt", "torso", "chest", "upper")) return "Top";
        if (Contains(hay, "bottom", "pant", "leg", "skirt", "lower")) return "Bottom";
        if (Contains(hay, "body", "nude", "skin")) return "Body";
        return "Unknown";
    }

    private static bool Contains(string haystack, params string[] needles)
    {
        foreach (var n in needles) if (haystack.Contains(n)) return true;
        return false;
    }
}
