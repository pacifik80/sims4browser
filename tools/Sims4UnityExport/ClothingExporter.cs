// ClothingExporter — the `exportcloth` pipeline. Adds CLOTHING to an already-exported character.json,
// following the proven HAIR template (HairExporter.cs) but for garments.
//
// A garment in EA CAS = a family of CASParts named yf<Slot>_<Style>_<Colour> where Slot is
// Top (body_type 6) / Bottom (7) / Body=full outfit (5) / Shoes (8). Colours are separate CASPs
// sharing the garment GEOM, differing only in the baked diffuse — a runtime texture swap, exactly
// like hair. A garment covers one or more nude body regions (top/bottom/feet) which the runtime hides
// while it is worn (EA garment meshes bake the exposed skin, so the nude body must be culled).
//
// Steps mirror HairExporter: enumerate colours from the index, resolve a representative colour to a
// scene via the fast synthesised-AssetSummary path (NOT the slow paged resolve), take the garment
// mesh(es), re-skin bone indices to the character's shared skeleton by name, write a diffuse per
// colour, and append/replace this garment in character.json's clothingCatalog (JsonNode, nothing else
// touched). The first run prints RICH material diagnostics (transparency, alpha slot, texture
// semantics) so we can see how exposed skin is handled.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;

namespace Sims4UnityExport;

internal static class ClothingExporter
{
    private const string IndexDb =
        @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";

    private static readonly JsonSerializerOptions NodeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    // Slot metadata keyed by internal-name prefix. UNDERWEAR gets its own LAYER slots (bra/panties):
    // worn under an outer garment its mesh hides and its fabric composites as a texture layer between
    // the skin and the outer fabric (the game's texture-only "Base Layers" model); worn alone it renders
    // as a normal garment. Specific prefixes must match BEFORE the generic yfTop_/yfBottom_ ones.
    private static (string category, int bodyType, string[] covers, string slotKey)? Classify(string internalName)
    {
        // Normalize the leading age/gender/frame code so the classification below is written ONCE and works
        // for either gender: yf = adult/young-adult female, ym = male, yu = unisex. Every CAS part name
        // begins with this 2-letter code; a name that doesn't (custom CC) falls through to the unknown case.
        // body_type ids (6 top, 7 bottom, 5 full, 8 shoes, …) are gender-independent in EA's data.
        string n = internalName;
        foreach (var g in new[] { "yf", "ym", "yu" })
            if (n.StartsWith(g, StringComparison.OrdinalIgnoreCase)) { n = n.Substring(g.Length); break; }

        // UNDERWEAR gets its own LAYER slots (bra/panties): worn under an outer garment its mesh hides and
        // its fabric composites as a texture layer between skin and outer fabric (the game's "Base Layers"
        // model); worn alone it renders as a normal garment. Specific prefixes match BEFORE the generic ones.
        if (n.StartsWith("Top_Bra", StringComparison.OrdinalIgnoreCase)) return ("bra", 6, new[] { "top" }, "bra");
        if (n.StartsWith("Bottom_Underwear", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Bottom_Panty", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Bottom_Panties", StringComparison.OrdinalIgnoreCase))
            return ("panties", 7, new[] { "bottom" }, "panties");
        if (n.StartsWith("Top_", StringComparison.OrdinalIgnoreCase)) return ("top", 6, new[] { "top" }, "top");
        if (n.StartsWith("Bottom_", StringComparison.OrdinalIgnoreCase)) return ("bottom", 7, new[] { "bottom" }, "bottom");
        if (n.StartsWith("Body_", StringComparison.OrdinalIgnoreCase)) return ("full", 5, new[] { "top", "bottom" }, "full");
        if (n.StartsWith("Shoes_", StringComparison.OrdinalIgnoreCase)) return ("shoes", 8, new[] { "feet" }, "shoes");
        // SKIN TEXTURE LAYERS — socks (36) and tights (42) are texture-only parts (their CASPs carry no
        // real mesh; the old mesh path produced a degenerate 1-tri blob). They composite into the LIVE
        // skin atlas (legs region) between the skin and any garment, and hide NO geometry (covers=[]).
        if (n.StartsWith("Acc_Socks", StringComparison.OrdinalIgnoreCase)) return ("socks", 36, Array.Empty<string>(), "socks");
        if (n.StartsWith("Acc_Tights", StringComparison.OrdinalIgnoreCase)) return ("tights", 42, Array.Empty<string>(), "tights");
        // MESH ACCESSORIES — small bone-anchored meshes; hide nothing, layer with everything.
        if (n.StartsWith("Acc_Glasses", StringComparison.OrdinalIgnoreCase)) return ("glasses", 11, Array.Empty<string>(), "glasses");
        if (n.StartsWith("Acc_Ear", StringComparison.OrdinalIgnoreCase)) return ("earrings", 10, Array.Empty<string>(), "earrings");
        if (n.StartsWith("Acc_Neck", StringComparison.OrdinalIgnoreCase)) return ("necklace", 12, Array.Empty<string>(), "necklace");
        // Gloves are a SKIN TEXTURE LAYER like tights/socks (EA glove GEOMs are a degenerate placeholder tri;
        // the garment is painted onto the body-atlas arm/hand region) — routed via ExportSkinLayerAsync.
        if (n.StartsWith("Acc_Gloves", StringComparison.OrdinalIgnoreCase)) return ("gloves", 13, Array.Empty<string>(), "gloves");
        if (n.StartsWith("Acc_WristLeft", StringComparison.OrdinalIgnoreCase)) return ("wristl", 14, Array.Empty<string>(), "wristl");
        if (n.StartsWith("Acc_WristRight", StringComparison.OrdinalIgnoreCase)) return ("wristr", 15, Array.Empty<string>(), "wristr");
        // MAKEUP — face-region SKIN TEXTURE LAYERS (no mesh), composited into the live skin atlas.
        if (n.StartsWith("MakeupLipstick_", StringComparison.OrdinalIgnoreCase)) return ("lipstick", 29, Array.Empty<string>(), "lipstick");
        if (n.StartsWith("MakeupEyeshadow_", StringComparison.OrdinalIgnoreCase)) return ("eyeshadow", 30, Array.Empty<string>(), "eyeshadow");
        if (n.StartsWith("MakeupEyeLiner_", StringComparison.OrdinalIgnoreCase)) return ("eyeliner", 31, Array.Empty<string>(), "eyeliner");
        if (n.StartsWith("MakeupBlush_", StringComparison.OrdinalIgnoreCase)) return ("blush", 32, Array.Empty<string>(), "blush");
        // Lashes ship as "EyeLashes" AND "Eyelashes" (OrdinalIgnoreCase catches both). SKIN TEXTURE LAYERS.
        if (n.StartsWith("MakeupEyebrows_", StringComparison.OrdinalIgnoreCase)) return ("brows", 34, Array.Empty<string>(), "brows");
        if (n.StartsWith("MakeupEyelashes_", StringComparison.OrdinalIgnoreCase)) return ("eyelashes", 37, Array.Empty<string>(), "eyelashes");
        return null;
    }

    public static async Task RunAsync(
        string unityAssetsDir, string charSlug, string internalName, int maxColors,
        IIndexStore indexStore, IAssetGraphBuilder graphBuilder, ISceneBuildService sceneBuilder,
        IResourceCatalogService catalogSvc, CancellationToken ct)
    {
        var cls = Classify(internalName);
        if (cls is null)
        {
            Console.Error.WriteLine($"'{internalName}' is not a recognised part ([yf/ym/yu]Top_/Bottom_/Body_/Shoes_/Acc_*/Makeup*_).");
            return;
        }
        var (category, bodyType, covers, slotKey) = cls.Value;
        Console.WriteLine($"Garment '{internalName}': category={category} body_type={bodyType} covers=[{string.Join(",", covers)}].");

        Console.WriteLine("Initializing index...");
        await indexStore.InitializeAsync(ct);

        var charDir = Path.Combine(unityAssetsDir, charSlug);
        var manifestPath = Path.Combine(charDir, "character.json");
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"character.json not found at '{manifestPath}'. Export the character first.");
            return;
        }

        var colors = EnumerateColors(internalName, bodyType);
        if (colors.Count == 0)
        {
            Console.Error.WriteLine($"No CAS parts found for '{internalName}' (body_type {bodyType}).");
            return;
        }
        Console.WriteLine($"Found {colors.Count} colour variant(s).");

        // Shared skeleton (bone name -> index) from the existing character.json.
        var root = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();

        // TEXTURE-LAYER parts (tights/socks) have no mesh — export diffuses only and return.
        if (slotKey is "tights" or "socks" or "gloves" or "lipstick" or "eyeshadow" or "eyeliner" or "blush" or "brows" or "eyelashes")
        {
            await ExportSkinLayerAsync(root, manifestPath, charDir, internalName, category, slotKey,
                colors, maxColors, indexStore, graphBuilder, catalogSvc, ct).ConfigureAwait(false);
            return;
        }

        var skel = root["skeleton"]?.AsArray();
        if (skel is null || skel.Count == 0) { Console.Error.WriteLine("character.json has no skeleton."); return; }
        var boneIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < skel.Count; i++)
        {
            var bn = skel[i]?["name"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(bn) && !boneIndexByName.ContainsKey(bn)) boneIndexByName[bn] = i;
        }
        // Stray-bone fallback is SLOT-AWARE: body garments ride the pelvis, but accessories must
        // ride their mount region — a glasses frame welded to the pelvis stays put when the head
        // turns (CatEye bug: b__CAS_Glasses__ verts fell back to pelvis).
        var fallbackBone = slotKey switch
        {
            "glasses" or "earrings" => "b__Head__",
            "necklace" => "b__Neck__",
            "wristl" => "b__L_ForearmTwist__",
            "wristr" => "b__R_ForearmTwist__",
            _ => "b__Pelvis__",
        };
        var fallbackIdx = boneIndexByName.TryGetValue(fallbackBone, out var pi) ? pi
                        : boneIndexByName.TryGetValue("b__Pelvis__", out var pb) ? pb
                        : boneIndexByName.TryGetValue("b__ROOT__", out var ri) ? ri : 0;
        Console.WriteLine($"Target skeleton: {skel.Count} bones; stray-bone fallback = index {fallbackIdx}.");

        // Pick a PLAIN base colour for the MESH (single-token colour name). Compound names like
        // 'Additions_SolidBlack' are sub-style variants that can carry a DIFFERENT (partial) mesh — avoid
        // them as the representative so we get the full base garment.
        bool Plain(ClothVariant c) => !c.Color.Contains('_');
        var rep = colors.FirstOrDefault(c => Plain(c) && c.Color.Equals("Black", StringComparison.OrdinalIgnoreCase))
                  ?? colors.FirstOrDefault(Plain)
                  ?? colors.FirstOrDefault(c => c.Color.IndexOf("Black", StringComparison.OrdinalIgnoreCase) >= 0)
                  ?? colors[0];

        Console.WriteLine($"Resolving representative colour '{rep.Color}' ({rep.FullTgi}) for the garment MESH...");
        var repScene = await ResolveSceneAsync(rep, indexStore, graphBuilder, sceneBuilder, ct);
        if (repScene is null || repScene.Meshes.Count == 0) { Console.Error.WriteLine("Representative colour built no scene/mesh; aborting."); return; }

        // ---- RICH DIAGNOSTICS: how is the garment textured / exposed skin handled? ----
        Console.WriteLine($"Garment scene: {repScene.Meshes.Count} mesh(es), {repScene.Bones.Count} bone(s), {repScene.Materials.Count} material(s).");
        for (var mi = 0; mi < repScene.Meshes.Count; mi++)
        {
            var m = repScene.Meshes[mi];
            Console.WriteLine($"  mesh[{mi}] '{m.Name}' verts={m.Positions.Count / 3} tris={m.Indices.Count / 3} mat={m.MaterialIndex}");
        }
        for (var ci = 0; ci < repScene.Materials.Count; ci++)
        {
            var mat = repScene.Materials[ci];
            Console.WriteLine($"  material[{ci}] '{mat.Name}' transparent={mat.IsTransparent} alphaMode={mat.AlphaMode ?? "-"} alphaSlot={mat.AlphaTextureSlot ?? "-"} shader={mat.ShaderName ?? "-"} textures={mat.Textures.Count}");
            foreach (var t in mat.Textures)
                Console.WriteLine($"      tex slot='{t.Slot}' semantic={t.Semantic} file='{t.FileName}' bytes={t.PngBytes.Length} src={(t.SourceKey is { } k ? k.FullTgi : "-")}");
        }

        // GAME-FAITHFUL model (proven by our RenderDoc capture: albedo = lerp(skin, outfit.rgb, outfit.a)).
        // The garment GEOM is a COMPLETE body-region mesh (fabric + baked exposed-skin faces: arms/neck/legs),
        // all in the shared body-UV atlas. We emit it WHOLE (no face stripping) with the diffuse's authored
        // alpha kept as a per-texel SKIN-vs-FABRIC MASK (opaque=fabric, transparent=show skin). Unity composites
        // it over the shared live skin atlas per-texel (lerp) on ONE opaque mesh, and hides the nude regions the
        // garment covers wholesale — so exposed skin is the LIVE skin, the edge is smooth, and nothing z-fights.
        // We do dilate the fabric RGB under the transparent margin so the anti-aliased alpha band lerps
        // skin<->fabric cleanly (never skin<->EA-green), but we NEVER delete geometry and NEVER bake skin.

        // GARMENT MORPHS: bake the same slider catalog the body uses into per-mesh sparse blendshapes,
        // so face/neck morphs (jaw soft mods...) and body sliders deform the garment's baked skin and
        // fabric in lockstep with the head/nude parts (with only the head morphing, the neck stepped).
        var morphDeltas = new Dictionary<int, List<CharacterBlendShape>>();
        try
        {
            var morphCatalog = Catalog.Load(CharacterDefinitionExporter.CatalogPath);
            if (morphCatalog.BodyMorphs.Count > 0)
            {
                var bgeoResolver = new BlendGeometryResolver(indexStore, catalogSvc);
                var dmapResolver = new DeformerMapResolver(indexStore, catalogSvc);
                morphDeltas = await CharacterDefinitionExporter.BakeMorphsAsync(
                    repScene, morphCatalog.BodyMorphs, bgeoResolver, dmapResolver,
                    Sims4ResourceExplorer.Preview.FaceMorphWeighting.Ignore, verbose: false, ct).ConfigureAwait(false);
                Console.WriteLine($"  [morphs] baked {morphDeltas.Values.Sum(v => v.Count)} blendshape(s) across {morphDeltas.Count} mesh(es).");
            }
        }
        catch (Exception ex) { Console.Error.WriteLine($"  [morphs] bake failed ({ex.GetType().Name}: {ex.Message}); garment exports without morphs."); }

        // ---- Build the garment part(s): every mesh, re-skinned to the shared skeleton. ----
        var clothFolder = Path.Combine(charDir, "Clothing");
        Directory.CreateDirectory(clothFolder);

        // Order meshes largest-first; the primary (largest) part carries the per-colour diffuse swap.
        var meshOrder = Enumerable.Range(0, repScene.Meshes.Count)
            .OrderByDescending(i => repScene.Meshes[i].Positions.Count).ToList();

        var parts = new List<CharacterPart>();
        for (var rank = 0; rank < meshOrder.Count; rank++)
        {
            var mesh = repScene.Meshes[meshOrder[rank]];
            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            int Remap(int oldIdx)
            {
                if (oldIdx < 0 || oldIdx >= repScene.Bones.Count) return fallbackIdx;
                var nm = repScene.Bones[oldIdx].Name ?? "";
                used[nm] = used.TryGetValue(nm, out var c) ? c + 1 : 1;
                if (boneIndexByName.TryGetValue(nm, out var ni)) return ni;
                // CAS accessory mount bones (b__CAS_Glasses__/b__CAS_Hat__...) are head children in
                // the game rig — ride the head so the accessory follows head turns.
                if (nm.StartsWith("b__CAS_", StringComparison.OrdinalIgnoreCase)
                    && boneIndexByName.TryGetValue("b__Head__", out var hi)) return hi;
                // Skirt physics bones (absent from the shared skeleton) swing WITH THE LEGS in-game.
                // Bind them to the matching thigh — welding them to the pelvis tears the hem band when
                // the idle animation moves the legs (verified: DressKneeBelt, 52 hem verts at Y 0.57-0.73).
                if (nm.Contains("Skirt"))
                {
                    var thigh = nm.Contains("_L_") ? "b__L_Thigh__" : nm.Contains("_R_") ? "b__R_Thigh__" : null;
                    if (thigh != null && boneIndexByName.TryGetValue(thigh, out var ti)) return ti;
                }
                return fallbackIdx;
            }
            var cmesh = BuildMeshRemapped(mesh, skel.Count, Remap);
            if (morphDeltas.TryGetValue(meshOrder[rank], out var meshMorphs)) cmesh.BlendShapes = meshMorphs;
            // StripBlockerCaps DISABLED (user decision 2026-07-08): the aggressive cone-ring pass nicked
            // visible skirt geometry. EA meshes now export UNMODIFIED (incl. their up-skirt blocker caps).
            // Revisit only for vetted modded assets: StripBlockerCaps(cmesh, label, aggressive: ...).
            var missing = used.Keys.Where(k => !boneIndexByName.ContainsKey(k)).ToList();

            // Representative diffuse for this mesh (primary swaps per colour; extras keep this one).
            var mat = mesh.MaterialIndex >= 0 && mesh.MaterialIndex < repScene.Materials.Count ? repScene.Materials[mesh.MaterialIndex] : null;
            var repDiff = PickTexture(mat, CanonicalTextureSemantic.BaseColor);
            // Keep the WHOLE garment mesh (fabric + baked exposed-skin faces). No face stripping — the alpha
            // is a per-texel skin/fabric mask consumed by the Unity composite shader, not a delete signal.
            var normalRel = WriteTexIfAny(clothFolder, internalName, rank == 0 ? "primary" : $"part{rank}", PickTexture(mat, CanonicalTextureSemantic.Normal), "_normal");
            string? diffRel = null;
            if (repDiff is { PngBytes.Length: > 0 })
            {
                var f = $"{Sanitize(internalName)}_{(rank == 0 ? "primary" : $"part{rank}")}_rep.png";
                File.WriteAllBytes(Path.Combine(clothFolder, f), DilateFabricRgb(repDiff.PngBytes, 12)); // fabric RGB bled under the alpha margin; alpha kept
                diffRel = $"Clothing/{f}";
            }
            Console.WriteLine($"  part[{rank}] verts={cmesh.VertexCount} bones-used={used.Count} missing->fallback={missing.Count} " +
                              $"[{string.Join(",", missing.Take(8))}]");
            parts.Add(new CharacterPart
            {
                Name = rank == 0 ? internalName : $"{internalName}_part{rank}",
                Region = "Clothing",
                Mesh = cmesh,
                Material = new CharacterMaterial { Diffuse = diffRel, Normal = normalRel },
            });
        }

        // ---- Per-colour diffuse for the PRIMARY mesh (largest). ----
        var outColors = new List<CharacterHairColor>();
        var n = 0;
        foreach (var c in colors.OrderBy(c => c.Color, StringComparer.OrdinalIgnoreCase))
        {
            if (n >= maxColors) { Console.WriteLine($"maxColors={maxColors} reached; stopping at {n}."); break; }
            var scene = ReferenceEquals(c, rep) ? repScene : await ResolveSceneAsync(c, indexStore, graphBuilder, sceneBuilder, ct);
            if (scene is null) { Console.Error.WriteLine($"  colour '{c.Color}': no scene; skipped."); continue; }
            var primary = scene.Meshes.OrderByDescending(m => m.Positions.Count).FirstOrDefault();
            var pmat = primary != null && primary.MaterialIndex >= 0 && primary.MaterialIndex < scene.Materials.Count ? scene.Materials[primary.MaterialIndex] : null;
            var diff = PickTexture(pmat, CanonicalTextureSemantic.BaseColor);
            if (diff is null || diff.PngBytes.Length == 0) { Console.Error.WriteLine($"  colour '{c.Color}': no diffuse; skipped."); continue; }
            var file = $"{Sanitize(internalName)}_{Sanitize(c.Color)}.png";
            File.WriteAllBytes(Path.Combine(clothFolder, file), DilateFabricRgb(diff.PngBytes, 12)); // RGB=fabric (bled under alpha margin); alpha kept as skin/fabric mask
            outColors.Add(new CharacterHairColor { Id = c.Color, Label = Spaced(c.Color), Diffuse = $"Clothing/{file}" });
            n++;
        }
        if (outColors.Count == 0) { Console.Error.WriteLine("No colour diffuses written; aborting."); return; }

        // Point the primary part's material at the representative colour's diffuse.
        var repColorRel = outColors.FirstOrDefault(x => x.Id.Equals(rep.Color, StringComparison.OrdinalIgnoreCase))?.Diffuse ?? outColors[0].Diffuse;
        if (parts.Count > 0) parts[0].Material.Diffuse = repColorRel;

        var item = new CharacterClothingItem
        {
            Id = internalName,
            Label = Spaced(StripPrefix(internalName)),
            Category = category,
            Covers = covers.ToList(),
            Parts = parts,
            Colors = outColors,
            DefaultColor = outColors.Any(x => x.Id.Equals(rep.Color, StringComparison.OrdinalIgnoreCase)) ? rep.Color : outColors[0].Id,
        };

        // ---- Merge into clothingCatalog[slot] (accumulate garments across runs). ----
        CharacterClothingCatalog catalog = root["clothingCatalog"] is JsonObject existing
            ? existing.Deserialize<CharacterClothingCatalog>(NodeOptions) ?? new CharacterClothingCatalog()
            : new CharacterClothingCatalog();
        var slot = slotKey switch
        {
            "top" => catalog.Top, "bottom" => catalog.Bottom, "full" => catalog.Full, "shoes" => catalog.Shoes, "socks" => catalog.Socks, "bra" => catalog.Bra, "panties" => catalog.Panties,
            "glasses" => catalog.Glasses, "earrings" => catalog.Earrings, "necklace" => catalog.Necklace, "gloves" => catalog.Gloves, "wristl" => catalog.WristL, "wristr" => catalog.WristR, _ => catalog.Top,
        };
        slot.Options.RemoveAll(o => string.Equals(o.Id, internalName, StringComparison.OrdinalIgnoreCase));
        slot.Options.Add(item);
        slot.Default = internalName; // start wearing the just-exported garment so it's visible immediately

        root["clothingCatalog"] = JsonSerializer.SerializeToNode(catalog, NodeOptions);
        File.WriteAllText(manifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine($"DONE. clothingCatalog.{slotKey} now has '{internalName}' = '{item.Label}' with {parts.Count} part(s), " +
                          $"{outColors.Count} colour(s), covers=[{string.Join(",", covers)}], default '{item.DefaultColor}'. Wrote {manifestPath}.");
        Console.WriteLine("Rebuild the character in Unity (Sims4 Creator > Character > Build CAS Editor Scene) to pick up clothing.");
    }

    // ---- SKIN TEXTURE LAYERS (tights/socks): no mesh — per-colour diffuse only ------------------

    private static async Task ExportSkinLayerAsync(
        JsonObject root, string manifestPath, string charDir, string internalName, string category,
        string slotKey, List<ClothVariant> colors, int maxColors,
        IIndexStore indexStore, IAssetGraphBuilder graphBuilder, IResourceCatalogService catalogSvc,
        CancellationToken ct)
    {
        var clothFolder = Path.Combine(charDir, "Clothing");
        Directory.CreateDirectory(clothFolder);
        var outColors = new List<CharacterHairColor>();
        var n = 0;
        foreach (var c in colors.OrderBy(x => x.Color, StringComparer.OrdinalIgnoreCase))
        {
            if (n >= maxColors) break;
            try
            {
                var graph = await ResolveGraphAsync(c, indexStore, graphBuilder, ct).ConfigureAwait(false);
                if (graph is null) { Console.Error.WriteLine($"  colour '{c.Color}': no graph; skipped."); continue; }
                byte[]? png = null;
                ResourceMetadata? used = null;
                // Try every plausible diffuse until one decodes — makeup ships odd texture encodings that
                // can blow up a single-candidate decode (EndOfStreamException on some RLE variants).
                foreach (var candidate in PickLayerDiffuseCandidates(graph))
                {
                    // DELTA > FULL: the same texture instance can exist as a broken/stub copy in a
                    // FullBuild package and the real one in a Delta package (and sometimes as an LRLE
                    // sibling). Expand the candidate to every indexed row of its instance, Delta first.
                    var rows = await indexStore.GetResourcesByFullInstanceAsync(candidate.Key.FullInstance, ct).ConfigureAwait(false);
                    var attempts = rows
                        .Where(r => r.Key.TypeName is "RLE2Image" or "LRLEImage" or "RLESImage" or "DSTImage" or "_IMG")
                        .OrderByDescending(r => r.PackagePath.Contains("Delta", StringComparison.OrdinalIgnoreCase))
                        .ThenByDescending(r => r.Key.TypeName == candidate.Key.TypeName)
                        .ToList();
                    if (attempts.Count == 0) attempts.Add(candidate);
                    foreach (var attempt in attempts)
                    {
                        try
                        {
                            var bytes = await catalogSvc.GetTexturePngAsync(attempt.PackagePath, attempt.Key, ct).ConfigureAwait(false);
                            if (bytes is { Length: > 0 }) { png = bytes; used = attempt; break; }
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"  colour '{c.Color}': {attempt.Key.TypeName} {attempt.Key.FullTgi} ({Path.GetFileName(attempt.PackagePath)}) decode failed ({ex.GetType().Name}); trying next.");
                        }
                    }
                    if (png != null) break;
                }
                if (png is null || used is null) { Console.Error.WriteLine($"  colour '{c.Color}': no decodable diffuse; skipped."); continue; }
                var file = $"{Sanitize(internalName)}_{Sanitize(c.Color)}.png";
                // Hair-safe dilation: sheer layers live in MID alpha — never overwrite visible texels.
                File.WriteAllBytes(Path.Combine(clothFolder, file), DilateFabricRgb(png, 12, seedMinAlpha: 26, fillMaxAlpha: 26));
                outColors.Add(new CharacterHairColor { Id = c.Color, Label = Spaced(c.Color), Diffuse = $"Clothing/{file}" });
                Console.WriteLine($"  colour '{c.Color,-18}' layer diffuse {png.Length,8} B  src={used.Key.FullTgi}");
                n++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  colour '{c.Color}': FAILED ({ex.GetType().Name}: {ex.Message}); skipped.");
            }
        }
        if (outColors.Count == 0) { Console.Error.WriteLine("No layer diffuses written; aborting."); return; }

        var item = new CharacterClothingItem
        {
            Id = internalName,
            Label = Spaced(StripPrefix(internalName)),
            Category = category,
            Covers = new List<string>(),           // texture layer: hides nothing
            Parts = new List<CharacterPart>(),     // texture layer: no mesh
            Colors = outColors,
            DefaultColor = outColors[0].Id,
        };
        CharacterClothingCatalog catalog = root["clothingCatalog"] is JsonObject existing
            ? existing.Deserialize<CharacterClothingCatalog>(NodeOptions) ?? new CharacterClothingCatalog()
            : new CharacterClothingCatalog();
        var slot = slotKey switch
        {
            "tights" => catalog.Tights, "socks" => catalog.Socks, "gloves" => catalog.Gloves,
            "lipstick" => catalog.Lipstick, "eyeshadow" => catalog.Eyeshadow,
            "eyeliner" => catalog.Eyeliner, "blush" => catalog.Blush,
            "brows" => catalog.Brows, "eyelashes" => catalog.Eyelashes,
            _ => catalog.Socks,
        };
        slot.Options.RemoveAll(o => string.Equals(o.Id, internalName, StringComparison.OrdinalIgnoreCase));
        slot.Options.Add(item);
        slot.Default = internalName;
        root["clothingCatalog"] = JsonSerializer.SerializeToNode(catalog, NodeOptions);
        File.WriteAllText(manifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"DONE. clothingCatalog.{slotKey} now has '{internalName}' (texture layer) with {outColors.Count} colour(s). Wrote {manifestPath}.");
    }

    // Graph only (no scene): used by texture-layer parts whose CASPs carry no geometry.
    private static async Task<CasAssetGraph?> ResolveGraphAsync(
        ClothVariant v, IIndexStore indexStore, IAssetGraphBuilder graphBuilder, CancellationToken ct)
    {
        var resources = await indexStore.GetResourcesByFullInstanceAsync(v.Instance, ct);
        if (resources.Count == 0) return null;
        var rootRes = resources.FirstOrDefault(r => string.Equals(r.Key.FullTgi, v.FullTgi, StringComparison.OrdinalIgnoreCase))
                      ?? resources.FirstOrDefault(r => string.Equals(r.Key.TypeName, "CASPart", StringComparison.OrdinalIgnoreCase));
        if (rootRes is null) return null;
        var summary = new AssetSummary(
            Id: Guid.NewGuid(), DataSourceId: Guid.Empty, SourceKind: rootRes.SourceKind, AssetKind: AssetKind.Cas,
            DisplayName: v.InternalName, Category: "CAS Part", PackagePath: rootRes.PackagePath, RootKey: rootRes.Key,
            ThumbnailTgi: null, VariantCount: 1, LinkedResourceCount: resources.Count, Diagnostics: string.Empty,
            PackageName: Path.GetFileName(rootRes.PackagePath), RootTypeName: rootRes.Key.TypeName, IdentityType: rootRes.Key.TypeName);
        var graph = await graphBuilder.BuildAssetGraphAsync(summary, resources, ct);
        return graph.CasGraph;
    }

    // Ordered diffuse candidates: material-manifest BaseColor first, then every colour-capable
    // texture resource by size. The caller tries each until one decodes.
    private static IEnumerable<ResourceMetadata> PickLayerDiffuseCandidates(CasAssetGraph graph)
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in graph.Materials)
            foreach (var t in m.Textures)
                if (t.Semantic == CanonicalTextureSemantic.BaseColor && t.SourceKey is { } key)
                {
                    var hit = graph.TextureResources.FirstOrDefault(r => r.Key.FullTgi == key.FullTgi);
                    if (hit != null && yielded.Add(hit.Key.FullTgi)) yield return hit;
                }
        foreach (var r in graph.TextureResources
                     .Where(r => r.Key.TypeName is "RLE2Image" or "LRLEImage" or "RLESImage" or "DSTImage" or "_IMG")
                     .OrderByDescending(r => r.UncompressedSize ?? r.CompressedSize ?? 0))
            if (yielded.Add(r.Key.FullTgi)) yield return r;
    }

    // ---- resource enumeration -----------------------------------------------------------------

    private sealed record ClothVariant(string InternalName, string Color, string FullTgi, ulong Instance, string PackagePath);

    private static List<ClothVariant> EnumerateColors(string internalName, int bodyType)
    {
        var list = new List<ClothVariant>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Sharded catalog: rows live in exactly one of index.sqlite / index.shardNN.sqlite — scan all.
        foreach (var dbPath in HairExporter.IndexShardPaths())
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT internal_name, root_tgi, package_path
                FROM cas_part_facts
                WHERE body_type = $bt AND internal_name LIKE $like ESCAPE '!'
                ORDER BY internal_name
                """;
            cmd.Parameters.AddWithValue("$bt", bodyType);
            cmd.Parameters.AddWithValue("$like", internalName.Replace("_", "!_") + "!_%");
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.IsDBNull(0) ? "" : reader.GetString(0);
                var tgi = reader.GetString(1);
                var pkg = reader.IsDBNull(2) ? "" : reader.GetString(2);
                if (name.Length <= internalName.Length + 1) continue;
                var color = name.Substring(internalName.Length + 1);
                if (!seen.Add(color)) continue;
                if (!TryParseInstance(tgi, out var inst)) continue;
                list.Add(new ClothVariant(name, color, tgi, inst, pkg));
            }
        }
        return list;
    }

    // ---- scene resolution (fast synthesised-summary path, same as HairExporter) ----------------

    private static async Task<CanonicalScene?> ResolveSceneAsync(
        ClothVariant v, IIndexStore indexStore, IAssetGraphBuilder graphBuilder, ISceneBuildService sceneBuilder, CancellationToken ct)
    {
        var resources = await indexStore.GetResourcesByFullInstanceAsync(v.Instance, ct);
        if (resources.Count == 0) { Console.Error.WriteLine($"  '{v.Color}': no resources for 0x{v.Instance:X16}."); return null; }
        var rootRes = resources.FirstOrDefault(r => string.Equals(r.Key.FullTgi, v.FullTgi, StringComparison.OrdinalIgnoreCase))
                      ?? resources.FirstOrDefault(r => string.Equals(r.Key.TypeName, "CASPart", StringComparison.OrdinalIgnoreCase));
        if (rootRes is null) { Console.Error.WriteLine($"  '{v.Color}': no CASPart root."); return null; }

        var summary = new AssetSummary(
            Id: Guid.NewGuid(), DataSourceId: Guid.Empty, SourceKind: rootRes.SourceKind, AssetKind: AssetKind.Cas,
            DisplayName: v.InternalName, Category: "CAS Part", PackagePath: rootRes.PackagePath, RootKey: rootRes.Key,
            ThumbnailTgi: null, VariantCount: 1, LinkedResourceCount: resources.Count, Diagnostics: string.Empty,
            PackageName: Path.GetFileName(rootRes.PackagePath), RootTypeName: rootRes.Key.TypeName, IdentityType: rootRes.Key.TypeName);

        var graph = await graphBuilder.BuildAssetGraphAsync(summary, resources, ct);
        if (graph.CasGraph is not { } casGraph)
        {
            Console.Error.WriteLine($"  '{v.Color}': no CasGraph ({string.Join("; ", graph.Diagnostics.Take(2))}).");
            return null;
        }
        var sceneResult = await sceneBuilder.BuildSceneAsync(casGraph, ct);
        if (!sceneResult.Success || sceneResult.Scene is null) { Console.Error.WriteLine($"  '{v.Color}': scene build failed ({sceneResult.Status})."); return null; }
        return sceneResult.Scene;
    }

    private static CanonicalTexture? PickTexture(CanonicalMaterial? mat, CanonicalTextureSemantic sem)
    {
        if (mat is null || mat.Textures.Count == 0) return null;
        var bySem = mat.Textures.FirstOrDefault(t => t.Semantic == sem);
        if (bySem != null) return bySem;
        var key = sem switch
        {
            CanonicalTextureSemantic.BaseColor => new[] { "diff", "albedo", "base" },
            CanonicalTextureSemantic.Normal => new[] { "norm", "bump" },
            CanonicalTextureSemantic.Specular => new[] { "spec", "gloss" },
            _ => Array.Empty<string>(),
        };
        var bySlot = mat.Textures.FirstOrDefault(t => key.Any(k => t.Slot.Contains(k, StringComparison.OrdinalIgnoreCase)));
        if (bySlot != null) return bySlot;
        return sem == CanonicalTextureSemantic.BaseColor ? mat.Textures[0] : null;
    }

    // Drop every triangle whose 3 verts all sample the diffuse's TRANSPARENT (skin-cutout) region, so the
    // garment mesh keeps only its fabric. The diffuse PNG (top-left origin) and the mesh UV (bottom-left)
    // differ by a V-flip. Logs faces dropped + the X-span of vertices STILL USED before/after — a top's
    // arms (wide X) or a sock's exposed leg should disappear, shrinking/retaining the fabric extent.
    // EA skirts/dresses ship an INTERIOR BLOCKER CAP at the hem that blacks out the up-skirt view. Two
    // authored shapes exist: perfectly FLAT plates (Bermuda: 38 faces, ny≈-0.96, <4mm planar) and shallow
    // CONES (Miniskirt: 38 faces, ny≈-0.7..-0.8, per-face Y spread >8mm — planarity tests miss them). For
    // a character CREATOR we want layered underwear visible from below, so drop both:
    //  (1) flat plates: per-face planar (<4mm) AND straight down (ny < -0.9) — safe everywhere;
    //  (2) cone RINGS (aggressive=true, bottoms/fulls only): down-facing faces (ny < -0.65) in the HEM/LEG
    //      zone (avgY < 0.95, safely below dress under-breast geometry) bucketed into 1.5cm Y bands — a
    //      band with ≥8 faces spanning ≥15cm of X is a blocker ring; no real skirt fabric matches that.
    private static void StripBlockerCaps(CharacterMesh mesh, string label, bool aggressive = false)
    {
        if (mesh.Triangles.Count == 0 || mesh.Normals.Count < mesh.Positions.Count) return;
        var tris = mesh.Triangles;
        var faceCount = tris.Count / 3;
        var drop = new bool[faceCount];
        var dropped = 0;

        // Pass 1: flat plates.
        for (var f = 0; f < faceCount; f++)
        {
            int a = tris[f * 3], b = tris[f * 3 + 1], c = tris[f * 3 + 2];
            float ya = mesh.Positions[a * 3 + 1], yb = mesh.Positions[b * 3 + 1], yc = mesh.Positions[c * 3 + 1];
            var planar = Math.Max(ya, Math.Max(yb, yc)) - Math.Min(ya, Math.Min(yb, yc)) < 0.004f;
            var ny = (mesh.Normals[a * 3 + 1] + mesh.Normals[b * 3 + 1] + mesh.Normals[c * 3 + 1]) / 3f;
            if (planar && ny < -0.9f) { drop[f] = true; dropped++; }
        }

        // Pass 2: cone rings (hem zone, wide X, one narrow Y band).
        if (aggressive)
        {
            var band = new Dictionary<int, List<int>>(); // Y-band (1.5cm) -> face indices
            for (var f = 0; f < faceCount; f++)
            {
                if (drop[f]) continue;
                int a = tris[f * 3], b = tris[f * 3 + 1], c = tris[f * 3 + 2];
                var ny = (mesh.Normals[a * 3 + 1] + mesh.Normals[b * 3 + 1] + mesh.Normals[c * 3 + 1]) / 3f;
                var avgY = (mesh.Positions[a * 3 + 1] + mesh.Positions[b * 3 + 1] + mesh.Positions[c * 3 + 1]) / 3f;
                if (ny >= -0.65f || avgY >= 0.95f) continue;
                var key = (int)Math.Floor(avgY / 0.015f);
                if (!band.TryGetValue(key, out var lst)) band[key] = lst = new List<int>();
                lst.Add(f);
            }
            foreach (var (_, faces) in band)
            {
                if (faces.Count < 8) continue;
                float xLo = float.MaxValue, xHi = float.MinValue;
                foreach (var f in faces)
                    for (var k = 0; k < 3; k++)
                    {
                        var x = mesh.Positions[tris[f * 3 + k] * 3];
                        if (x < xLo) xLo = x; if (x > xHi) xHi = x;
                    }
                if (xHi - xLo < 0.15f) continue;
                foreach (var f in faces) { drop[f] = true; dropped++; }
            }
        }

        if (dropped > 0)
        {
            var kept = new List<int>(tris.Count);
            for (var f = 0; f < faceCount; f++)
            {
                if (drop[f]) continue;
                kept.Add(tris[f * 3]); kept.Add(tris[f * 3 + 1]); kept.Add(tris[f * 3 + 2]);
            }
            mesh.Triangles = kept;
            Console.WriteLine($"  [caps {label}] dropped {dropped} interior blocker face(s) (up-skirt occluder).");
        }
    }

    // Bleed the FABRIC RGB outward into the transparent (skin-cutout) margin, keeping the alpha channel
    // (the skin/fabric mask) untouched. The garment diffuse's anti-aliased alpha edge is a few texels wide;
    // where EA left that band GREEN, a naive lerp(skin, garment.rgb, alpha) would tinge the edge green (or
    // beige, if pre-recoloured). Dilating clean fabric RGB under the low-alpha band makes the lerp a clean
    // skin<->fabric gradient. We only need to cover the AA band + a small margin, so a handful of passes.
    // BGRA byte order in System.Drawing. Alpha is preserved verbatim — it remains the per-texel mask.
    // seedMinAlpha: texels at/above this alpha act as colour SOURCES (their RGB spreads outward).
    // fillMaxAlpha: only texels BELOW this alpha may be overwritten. Clothing uses (250, 250): fabric is
    // near-binary so overwriting the whole AA band with solid fabric colour is safe. HAIR must use
    // (26, 26): strands are mostly MID-alpha and their painted gradients are visible at low clip cutoffs —
    // overwriting them flood-fills the strands with muddy core colours (the "dirty texture" bug).
    internal static byte[] DilateFabricRgb(byte[] png, int passes, int seedMinAlpha = 250, int fillMaxAlpha = 250)
    {
        try
        {
            using var ms = new MemoryStream(png);
            using var src = new Bitmap(ms);
            using var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) g.DrawImage(src, 0, 0, src.Width, src.Height);
            int w = bmp.Width, h = bmp.Height;
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var stride = data.Stride; var n = stride * h;
            var buf = new byte[n];
            Marshal.Copy(data.Scan0, buf, 0, n);

            // Seed = SOLID fabric texels (alpha >= 250) only — their clean RGB bleeds one texel per pass into
            // unfilled neighbours. Seeding from solid fabric (not the whole >=128 half) means the clean colour
            // is carried ACROSS the entire anti-aliased alpha band, so no green/beige AA texel survives in the
            // skin<->fabric transition the composite lerps over.
            var filled = new bool[w * h];
            var fillable = new bool[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var a = buf[y * stride + x * 4 + 3];
                    filled[y * w + x] = a >= seedMinAlpha;
                    fillable[y * w + x] = a < fillMaxAlpha;
                }

            int[] dxs = { 1, -1, 0, 0 }, dys = { 0, 0, 1, -1 };
            var updates = new List<(int idx, byte b, byte g, byte r)>();
            for (var p = 0; p < passes; p++)
            {
                updates.Clear();
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                    {
                        if (filled[y * w + x] || !fillable[y * w + x]) continue;
                        for (var k = 0; k < 4; k++)
                        {
                            int nx = x + dxs[k], ny = y + dys[k];
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h || !filled[ny * w + nx]) continue;
                            var ni = ny * stride + nx * 4;
                            updates.Add((y * stride + x * 4, buf[ni], buf[ni + 1], buf[ni + 2]));
                            break;
                        }
                    }
                if (updates.Count == 0) break;
                foreach (var (i, b, gg, r) in updates)
                {
                    buf[i] = b; buf[i + 1] = gg; buf[i + 2] = r; // RGB only; buf[i+3] (alpha) untouched
                    filled[(i / 4)] = true; // i/4 == y*w + x since stride == w*4 for 32bpp
                }
            }
            Marshal.Copy(buf, 0, data.Scan0, n);
            bmp.UnlockBits(data);
            using var outMs = new MemoryStream();
            bmp.Save(outMs, ImageFormat.Png);
            return outMs.ToArray();
        }
        catch { return png; } // on any failure keep the original bytes
    }

    private static string? WriteTexIfAny(string folder, string internalName, string partTag, CanonicalTexture? tex, string suffix)
    {
        if (tex is null || tex.PngBytes.Length == 0) return null;
        var f = $"{Sanitize(internalName)}_{partTag}{suffix}.png";
        File.WriteAllBytes(Path.Combine(folder, f), tex.PngBytes);
        return $"Clothing/{f}";
    }

    // ---- mesh conversion (copy of the exportchar skin path with a custom remap) -----------------

    private static CharacterMesh BuildMeshRemapped(CanonicalMesh mesh, int sharedBoneCount, Func<int, int> remap)
    {
        var vertexCount = mesh.Positions.Count / 3;
        var (boneIndices, boneWeights) = BuildBlendWeights(mesh, vertexCount, sharedBoneCount, remap);
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

    private static (List<int>, List<float>) BuildBlendWeights(CanonicalMesh mesh, int vertexCount, int sharedBoneCount, Func<int, int> remap)
    {
        var slots = Math.Max(0, vertexCount) * 4;
        var boneIndices = new int[slots];
        var boneWeights = new float[slots];
        var byVertex = new Dictionary<int, List<VertexWeight>>();
        foreach (var w in mesh.SkinWeights)
        {
            if (w.VertexIndex < 0 || w.VertexIndex >= vertexCount) continue;
            if (!byVertex.TryGetValue(w.VertexIndex, out var l)) { l = new List<VertexWeight>(); byVertex[w.VertexIndex] = l; }
            l.Add(w);
        }
        int Clamp(int b) => sharedBoneCount <= 0 ? 0 : Math.Clamp(b, 0, sharedBoneCount - 1);
        for (var v = 0; v < vertexCount; v++)
        {
            var baseSlot = v * 4;
            if (!byVertex.TryGetValue(v, out var inf) || inf.Count == 0)
            {
                boneIndices[baseSlot] = Clamp(remap(-1));
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
            if (sum > 0f) for (var i = 0; i < top.Count; i++) boneWeights[baseSlot + i] /= sum;
            else { boneIndices[baseSlot] = Clamp(remap(top[0].BoneIndex)); boneWeights[baseSlot] = 1f; }
        }
        return (boneIndices.ToList(), boneWeights.ToList());
    }

    // ---- small helpers ------------------------------------------------------------------------

    private static bool TryParseInstance(string fullTgi, out ulong instance)
    {
        instance = 0;
        var parts = fullTgi.Split(':');
        var last = parts.Length > 0 ? parts[^1] : fullTgi;
        return ulong.TryParse(last.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out instance);
    }

    private static string Sanitize(string s) => new string(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

    // "yfTop_TshirtCrew" -> "TshirtCrew" (drop the yf<Slot>_ prefix + a pack code).
    private static string StripPrefix(string internalName)
    {
        var s = Regex.Replace(internalName, @"^y[fu](?:Top|Bottom|Body|Shoes|Hair|Acc|Makeup[A-Za-z]*)_", "", RegexOptions.IgnoreCase); // yu = unisex (brows)
        s = Regex.Replace(s, @"^(?:[A-Za-z]{2}\d{2})", "");
        return string.IsNullOrEmpty(s) ? internalName : s;
    }

    private static string Spaced(string camel) =>
        string.IsNullOrEmpty(camel) ? camel : Regex.Replace(camel, @"(?<=[a-z0-9])(?=[A-Z])", " ");
}
