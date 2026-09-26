// ExportSim — headless assembly + export of a FULL default human Sim (body + head + eyes)
// into a multi-part character.json the Unity Sims4Creator project consumes directly.
//
// Pipeline (all headless, no WinUI):
//   1. SyntheticSimService.CreateHumanSeed(age, gender, skintone)  → SimConstructorSeed
//   2. SyntheticSimService.BuildHumanAssetGraphAsync(seed)         → AssetGraph (with SimGraph)
//   3. ISimAssetGraphRenderer.BuildSimSceneAsync(graph)            → SimRenderResult.Scene
//
// Step 3 is the SAME code path the SimConstructorWindow uses. It resolves every active body
// layer (Top/Bottom/Shoes/…), the Head shell, builds each CAS part into its own CanonicalScene,
// composes the body layers via CanonicalSceneComposer.Compose (merge bones by name, remap
// weights), then merges the head via SimSceneComposer.ComposeBodyAndHead (rig-basis unification
// + bone-name merge + weight rebasing). The returned Scene is therefore ALREADY a single
// CanonicalScene whose Meshes are the separate parts and whose Bones are the shared skeleton,
// with every mesh's SkinWeights.BoneIndex indexing into that shared Bones list.
//
// character.json schema (per the brief) is emitted with System.Text.Json (WriteIndented). Each
// part carries its own mesh (positions/uv0/uv1/triangles + 4-influence boneIndices/boneWeights
// that index into the shared skeleton) and material (diffuse/normal/specular/shadow as relative
// Textures/<file>.png paths). Texture PNGs are written into Textures/ (shared filenames OK) and
// diffuse PNGs are run through the SAME transparent-pixel blackening as the other export paths.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Preview.SimRender;

namespace Sims4UnityExport;

// ---------------------------------------------------------------------------
// character.json data contract (serialized verbatim per the brief).
// ---------------------------------------------------------------------------
internal sealed class CharacterManifest
{
    [JsonPropertyName("asset")] public string Asset { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; set; } = "sim";
    [JsonPropertyName("age")] public string Age { get; set; } = string.Empty;
    [JsonPropertyName("gender")] public string Gender { get; set; } = string.Empty;
    [JsonPropertyName("skeleton")] public List<CharacterBone> Skeleton { get; set; } = new();
    [JsonPropertyName("parts")] public List<CharacterPart> Parts { get; set; } = new();
}

internal sealed class CharacterBone
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("parentName")] public string? ParentName { get; set; }
    [JsonPropertyName("bindPose")] public float[] BindPose { get; set; } = Array.Empty<float>();
    [JsonPropertyName("inverseBindPose")] public float[] InverseBindPose { get; set; } = Array.Empty<float>();
}

internal sealed class CharacterPart
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("region")] public string Region { get; set; } = "Unknown";
    [JsonPropertyName("mesh")] public CharacterMesh Mesh { get; set; } = new();
    [JsonPropertyName("material")] public CharacterMaterial Material { get; set; } = new();
}

internal sealed class CharacterMesh
{
    [JsonPropertyName("vertexCount")] public int VertexCount { get; set; }
    [JsonPropertyName("positions")] public List<float> Positions { get; set; } = new();
    // EA's AUTHORED per-vertex normals (flat 3*vertexCount, SAME order/space as positions). The
    // Unity character builder reads this verbatim and uses it instead of recomputing normals — the
    // recompute splits normals at every UV seam/part boundary and causes a hard lighting "stitch".
    // Empty when the source CanonicalMesh carried no normals (builder falls back to RecalculateNormals).
    [JsonPropertyName("normals")] public List<float> Normals { get; set; } = new();
    [JsonPropertyName("uv0")] public List<float> Uv0 { get; set; } = new();
    [JsonPropertyName("uv1")] public List<float> Uv1 { get; set; } = new();
    [JsonPropertyName("triangles")] public List<int> Triangles { get; set; } = new();
    [JsonPropertyName("boneIndices")] public List<int> BoneIndices { get; set; } = new();
    [JsonPropertyName("boneWeights")] public List<float> BoneWeights { get; set; } = new();
    // Morph sliders baked as SPARSE blend-shape deltas (only moved vertices). Each = a Unity blend
    // shape; a slider drives SetBlendShapeWeight. Empty for meshes/morphs with no movement.
    [JsonPropertyName("blendShapes")] public List<CharacterBlendShape> BlendShapes { get; set; } = new();
}

internal sealed class CharacterBlendShape
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    // Parallel arrays: indices[k] is a vertex index; deltaPositions[k*3..k*3+2] is its delta (x,y,z).
    [JsonPropertyName("indices")] public List<int> Indices { get; set; } = new();
    [JsonPropertyName("deltaPositions")] public List<float> DeltaPositions { get; set; } = new();
}

internal sealed class CharacterMaterial
{
    [JsonPropertyName("diffuse")] public string? Diffuse { get; set; }
    [JsonPropertyName("normal")] public string? Normal { get; set; }
    [JsonPropertyName("specular")] public string? Specular { get; set; }
    [JsonPropertyName("shadow")] public string? Shadow { get; set; }
}

internal static class CharacterExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // parentName == null for the root bone, and material.* are null when absent — those
        // nulls are part of the schema, so never drop them.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    // 16-float row-major identity, reused for bones missing a bind/inverse-bind pose.
    private static float[] Identity() => new float[]
    {
        1f, 0f, 0f, 0f,
        0f, 1f, 0f, 0f,
        0f, 0f, 1f, 0f,
        0f, 0f, 0f, 1f
    };

    /// <summary>
    /// Assemble a full default human Sim headlessly and write character.json + textures.
    /// Returns true on success. All progress/diagnostics are printed to Console.
    /// </summary>
    public static async Task<bool> RunAsync(
        string unityAssetsDir,
        ISyntheticSimService syntheticSimService,
        ISimAssetGraphRenderer simRenderer,
        string ageLabel,
        string genderLabel,
        ulong skintoneInstance,
        string slug,
        CancellationToken ct)
    {
        Console.WriteLine($"Assembling synthetic human Sim: age='{ageLabel}' gender='{genderLabel}' skintone=0x{skintoneInstance:X16} slug='{slug}'.");

        // 1) Seed.
        var seed = syntheticSimService.CreateHumanSeed(ageLabel, genderLabel, skintoneInstance);
        Console.WriteLine($"Seed: {seed.SummaryText}");
        Console.WriteLine($"Seed: outfit parts={seed.OutfitPartCount}, synthetic instance=0x{seed.SyntheticFullInstance:X16}.");

        // 2) Asset graph (with SimGraph).
        var graph = await syntheticSimService.BuildHumanAssetGraphAsync(seed, ct).ConfigureAwait(false);
        foreach (var d in graph.Diagnostics)
        {
            Console.WriteLine($"[graph] {d}");
        }
        if (graph.SimGraph is null)
        {
            Console.Error.WriteLine("Asset graph produced no SimGraph — cannot assemble a Sim. Aborting.");
            return false;
        }
        var sim = graph.SimGraph;
        Console.WriteLine($"SimGraph ready: body assembly mode={sim.BodyAssembly.Mode}, layers={sim.BodyAssembly.Layers.Count}, candidate buckets={sim.BodyCandidates.Count}.");
        foreach (var bucket in sim.BodyCandidates)
        {
            Console.WriteLine($"  bucket '{bucket.Label}' [{bucket.SourceKind}] count={bucket.Count}");
        }

        // 3) Render → unified CanonicalScene. This is the SimConstructorWindow path: it builds
        //    each CAS part scene, composes body layers, then merges the head (bone-name merge +
        //    weight rebasing). The result is ALREADY one scene with a shared skeleton.
        var renderResult = await simRenderer.BuildSimSceneAsync(graph, ct).ConfigureAwait(false);
        Console.WriteLine();
        Console.WriteLine($"Render diagnostics ({renderResult.Diagnostics.Count}):");
        foreach (var d in renderResult.Diagnostics)
        {
            Console.WriteLine($"[render] {d}");
        }
        if (renderResult.AssemblyPlan is { } plan)
        {
            Console.WriteLine($"[assembly] basis={plan.BasisKind} ({plan.BasisLabel}) includesHeadShell={plan.IncludesHeadShell}");
            Console.WriteLine($"[assembly] notes: {plan.Notes}");
        }

        if (renderResult.Scene is not { } scene)
        {
            Console.Error.WriteLine("Render produced no Scene; nothing to export.");
            return false;
        }

        var b = scene.Bounds;
        Console.WriteLine();
        Console.WriteLine($"Unified scene: meshes={scene.Meshes.Count}, materials={scene.Materials.Count}, bones={scene.Bones.Count}.");
        Console.WriteLine(FormattableString.Invariant(
            $"Scene bounds: X[{b.MinX:0.###}..{b.MaxX:0.###}] Y[{b.MinY:0.###}..{b.MaxY:0.###}] Z[{b.MinZ:0.###}..{b.MaxZ:0.###}]."));

        // --- Write textures + assemble character.json ------------------------------------
        var assetFolder = Path.Combine(unityAssetsDir, slug);
        var texturesFolder = Path.Combine(assetFolder, "Textures");
        Directory.CreateDirectory(texturesFolder);

        // Compose the FULL-FIDELITY skin atlas — the unified color atlas that BOTH the body meshes
        // (torso/hands/legs) and the head shell (face region) UV-sample. This runs the SAME
        // SimSkinAtlasComposer chain the WinUI Sim Constructor uses, ported headlessly to
        // System.Drawing (SkinAtlasComposer): base skin + neutral/overlay/physique detail rows,
        // the in-game albedo equation, the tone face overlay, and the face CAS overlays (eye color
        // + brows + makeup) composited into the face region so the head shows eyes/brows. A
        // tangent-space normal map is derived from the composed atlas. Every skin part binds its
        // diffuse to skin_atlas.png and its normal to skin_normal.png.
        var (skinDiffuseRel, skinNormalRel) = await ResolveAndWriteSkinAtlasAsync(
            syntheticSimService, ageLabel, genderLabel, skintoneInstance, texturesFolder, ct).ConfigureAwait(false);

        var manifest = new CharacterManifest
        {
            Asset = slug,
            Kind = "sim",
            Age = NormalizeAge(ageLabel),
            Gender = NormalizeGender(genderLabel),
            Skeleton = BuildSkeleton(scene),
        };

        // Write each part. Every part (body top/bottom/shoes AND head) is skin sharing the same
        // composed atlas, so each part's diffuse points at the single skin_atlas.png and its normal
        // at skin_normal.png we wrote above. Specular/shadow stay null (the App leaves them null).
        for (var i = 0; i < scene.Meshes.Count; i++)
        {
            var mesh = scene.Meshes[i];
            var material = (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.Materials.Count)
                ? scene.Materials[mesh.MaterialIndex]
                : null;

            var part = new CharacterPart
            {
                Name = string.IsNullOrWhiteSpace(mesh.Name) ? $"part_{i}" : mesh.Name,
                Region = InferRegion(mesh.Name, material),
                Mesh = BuildMesh(mesh, scene.Bones.Count),
                Material = new CharacterMaterial { Diffuse = skinDiffuseRel, Normal = skinNormalRel },
            };
            manifest.Parts.Add(part);
        }

        var characterPath = Path.Combine(assetFolder, "character.json");
        await File.WriteAllTextAsync(characterPath, JsonSerializer.Serialize(manifest, JsonOptions), ct).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"Wrote {characterPath}");
        Console.WriteLine($"character.json: {manifest.Parts.Count} part(s), {manifest.Skeleton.Count} bone(s).");
        foreach (var part in manifest.Parts)
        {
            Console.WriteLine(
                $"  part '{part.Name}' region={part.Region} verts={part.Mesh.VertexCount} " +
                $"tris={part.Mesh.Triangles.Count / 3} diffuse='{part.Material.Diffuse ?? "(none)"}' " +
                $"normal='{part.Material.Normal ?? "(none)"}'.");
        }

        return true;
    }

    // Build the shared skeleton verbatim (scene order = the order every BoneIndex references).
    private static List<CharacterBone> BuildSkeleton(CanonicalScene scene)
    {
        var bones = new List<CharacterBone>(scene.Bones.Count);
        foreach (var bone in scene.Bones)
        {
            bones.Add(new CharacterBone
            {
                Name = bone.Name,
                ParentName = string.IsNullOrEmpty(bone.ParentName) ? null : bone.ParentName,
                BindPose = bone.BindPoseMatrix is { Length: 16 } bind ? bind : Identity(),
                InverseBindPose = bone.InverseBindPoseMatrix is { Length: 16 } inv ? inv : Identity(),
            });
        }
        return bones;
    }

    // Build one part's mesh: geometry + 4-influence skin weights indexing the shared skeleton.
    private static CharacterMesh BuildMesh(CanonicalMesh mesh, int skeletonBoneCount)
    {
        var vertexCount = mesh.Positions.Count / 3;
        var (boneIndices, boneWeights) = BuildBlendWeights(mesh, vertexCount, skeletonBoneCount);

        // uv0 = Uvs (resolved/preferred channel) when present, else Uv0s; uv1 = Uv1s or empty.
        var uv0 = mesh.Uvs is { Count: > 0 } ? mesh.Uvs : (mesh.Uv0s ?? (IReadOnlyList<float>)Array.Empty<float>());
        var uv1 = mesh.Uv1s ?? (IReadOnlyList<float>)Array.Empty<float>();

        // EA's authored normals: emit verbatim (same flat 3*N order/space as positions, no
        // transform/flip/normalize). Empty list when the mesh carried none.
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

    // 4-influence boneIndices/boneWeights per vertex (length 4*vertexCount), weights normalized
    // to sum 1. Indices are clamped into [0, skeletonBoneCount) defensively. A vertex with no
    // influence binds to bone 0 with weight 1. (Mirrors RigExporter.BuildBlendWeights.)
    private static (List<int> BoneIndices, List<float> BoneWeights) BuildBlendWeights(
        CanonicalMesh mesh, int vertexCount, int skeletonBoneCount)
    {
        var slots = Math.Max(0, vertexCount) * 4;
        var boneIndices = new int[slots];
        var boneWeights = new float[slots];

        var byVertex = new Dictionary<int, List<VertexWeight>>();
        foreach (var weight in mesh.SkinWeights)
        {
            if (weight.VertexIndex < 0 || weight.VertexIndex >= vertexCount)
            {
                continue;
            }
            if (!byVertex.TryGetValue(weight.VertexIndex, out var list))
            {
                list = new List<VertexWeight>();
                byVertex[weight.VertexIndex] = list;
            }
            list.Add(weight);
        }

        int Clamp(int boneIndex) =>
            skeletonBoneCount <= 0 ? 0 : Math.Clamp(boneIndex, 0, skeletonBoneCount - 1);

        for (var v = 0; v < vertexCount; v++)
        {
            var baseSlot = v * 4;
            if (!byVertex.TryGetValue(v, out var influences) || influences.Count == 0)
            {
                boneIndices[baseSlot] = 0;
                boneWeights[baseSlot] = 1f;
                continue;
            }

            var top = influences.OrderByDescending(w => w.Weight).Take(4).ToList();
            var sum = 0f;
            for (var i = 0; i < top.Count; i++)
            {
                boneIndices[baseSlot + i] = Clamp(top[i].BoneIndex);
                boneWeights[baseSlot + i] = top[i].Weight;
                sum += top[i].Weight;
            }

            if (sum > 0f)
            {
                for (var i = 0; i < top.Count; i++)
                {
                    boneWeights[baseSlot + i] /= sum;
                }
            }
            else
            {
                boneIndices[baseSlot] = Clamp(top[0].BoneIndex);
                boneWeights[baseSlot] = 1f;
            }
        }

        return (boneIndices.ToList(), boneWeights.ToList());
    }

    // Resolve the skintone render summary, run the FULL SimSkinAtlasComposer chain headlessly
    // (SkinAtlasComposer), and write the composed atlas to Textures/skin_atlas.png plus the
    // derived tangent-space normal map to Textures/skin_normal.png. Returns the two relpaths
    // ("Textures/skin_atlas.png", "Textures/skin_normal.png"); either may be null if its step
    // could not be produced (in which case parts fall back to no diffuse/normal).
    //
    // The composed atlas is opaque skin, so the EA acid-green transparent-block blacken pass would
    // only damage it if applied blindly. We run TextureCleanup ONLY when the PNG actually contains
    // alpha==0 pixels (it normally does not after compositing), preserving the skin RGB.
    private static async Task<(string? Diffuse, string? Normal)> ResolveAndWriteSkinAtlasAsync(
        ISyntheticSimService syntheticSimService,
        string ageLabel,
        string genderLabel,
        ulong skintoneInstance,
        string texturesFolder,
        CancellationToken ct)
    {
        SimSkintoneRenderSummary? skin = null;
        try
        {
            skin = await syntheticSimService
                .ResolveSkintoneRenderAsync(ageLabel, genderLabel, skintoneInstance, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[skin] ResolveSkintoneRenderAsync FAILED ({ex.GetType().Name}: {ex.Message}).");
        }

        if (skin is null)
        {
            Console.Error.WriteLine("[skin] No skintone render summary resolved; parts will have no diffuse/normal.");
            return (null, null);
        }

        Console.WriteLine(
            $"[skin] tone=0x{skintoneInstance:X16} baseTgi={skin.BaseTextureResourceTgi ?? "(none)"} " +
            $"basePkg={skin.BaseTexturePackagePath ?? "(none)"} baseBytes={skin.BaseTexturePngBytes?.Length ?? 0}.");
        Console.WriteLine(
            $"[skin] inputs: detailNeutral={skin.DetailNeutralPngBytes?.Length ?? 0}B " +
            $"detailOverlay={skin.DetailOverlayPngBytes?.Length ?? 0}B " +
            $"faceOverlay={skin.FaceOverlayPngBytes?.Length ?? 0}B " +
            $"faceCasOverlays={skin.FaceCasOverlayPngBytes?.Count ?? 0} " +
            $"hue={skin.SkintoneHue} sat={skin.SkintoneSaturation} overlayOpacity={skin.OverlayOpacity} " +
            $"physiqueRows(detail/overlay)={skin.PhysiqueDetailPngBytes?.Count ?? 0}/{skin.PhysiqueOverlayPngBytes?.Count ?? 0}.");

        if (skin.BaseTexturePngBytes is not { Length: > 0 })
        {
            Console.Error.WriteLine("[skin] Skintone summary carried no BaseTexturePngBytes; parts will have no diffuse/normal.");
            return (null, null);
        }

        // Physique weights drive the per-physique detail rows. Synthesised Sims carry no weights
        // (PhysiqueWeights is empty), so for the full-fidelity export we supply a default human
        // "fit-leaning" physique [heavy, fit, lean, bony] so the muscle/relief detail rows actually
        // contribute (matching how a constructor user would dial the body-type sliders up). The
        // rows are only blended when the summary actually carries them.
        var physiqueWeights = skin.PhysiqueWeights is { Count: 4 } w &&
                              (w[0] > 0f || w[1] > 0f || w[2] > 0f || w[3] > 0f)
            ? w
            : new[] { 0f, 0.6f, 0.4f, 0f };
        Console.WriteLine(FormattableString.Invariant(
            $"[skin] physiqueWeights [heavy,fit,lean,bony] = [{physiqueWeights[0]:0.##},{physiqueWeights[1]:0.##},{physiqueWeights[2]:0.##},{physiqueWeights[3]:0.##}]."));

        // 1) Compose the atlas via the faithful System.Drawing port of SimSkinAtlasComposer.
        byte[]? atlas;
        try
        {
            atlas = SkinAtlasComposer.ComposeAtlas(skin, physiqueWeights);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[skin] ComposeAtlas FAILED ({ex.GetType().Name}: {ex.Message}); parts will have no diffuse/normal.");
            return (null, null);
        }
        if (atlas is not { Length: > 0 })
        {
            Console.Error.WriteLine("[skin] ComposeAtlas produced no atlas; parts will have no diffuse/normal.");
            return (null, null);
        }

        const string diffuseFile = "skin_atlas.png";
        var diffusePath = Path.Combine(texturesFolder, diffuseFile);
        await File.WriteAllBytesAsync(diffusePath, atlas, ct).ConfigureAwait(false);
        Console.WriteLine($"[skin] wrote {diffuseFile} ({atlas.Length} bytes) — composed full-fidelity skin atlas.");

        // The atlas has transparent UV gutters between islands. Blackening them (RGB=0) trades a
        // green seam for a BLACK one because Unity's bilinear/mipmap filtering still pulls the
        // gutter color across UV-island edges. Instead DILATE: flood the nearest opaque (skin)
        // color a few pixels outward into the gutters so edge filtering samples skin — no green
        // AND no black seam. (Only the skin atlas is dilated; the normal map is left alone.)
        try
        {
            if (TextureCleanup.HasTransparentPixels(diffusePath))
            {
                var filled = TextureCleanup.DilateOpaque(diffusePath, 16);
                Console.WriteLine($"[skin] atlas had transparent gutters; dilated {filled} gutter pixel(s) with skin color (no blacken).");
            }
            else
            {
                Console.WriteLine("[skin] atlas is opaque; left untouched (no dilation needed).");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[skin] transparency check/dilation FAILED ({ex.GetType().Name}: {ex.Message}); left as-is.");
        }

        // 2) Derive the tangent-space normal map from the composed atlas (App strength 3.0).
        string? normalRel = null;
        try
        {
            var normal = SkinAtlasComposer.DeriveNormalMap(atlas, 3.0f);
            if (normal is { Length: > 0 })
            {
                const string normalFile = "skin_normal.png";
                var normalPath = Path.Combine(texturesFolder, normalFile);
                await File.WriteAllBytesAsync(normalPath, normal, ct).ConfigureAwait(false);
                Console.WriteLine($"[skin] wrote {normalFile} ({normal.Length} bytes) — derived tangent-space normal.");
                normalRel = $"Textures/{normalFile}";
            }
            else
            {
                Console.Error.WriteLine("[skin] DeriveNormalMap produced no normal map; normal slot will be null.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[skin] DeriveNormalMap FAILED ({ex.GetType().Name}: {ex.Message}); normal slot will be null.");
        }

        return ($"Textures/{diffuseFile}", normalRel);
    }

    // Infer a render region from the mesh name + the material's approximation note / textures.
    // Best-effort; returns "Unknown" when no signal matches.
    private static string InferRegion(string? meshName, CanonicalMaterial? material)
    {
        var name = (meshName ?? string.Empty).ToLowerInvariant();
        var note = (material?.Approximation ?? string.Empty).ToLowerInvariant();
        var hay = name + " " + note;

        // Order matters: more specific tokens first.
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
        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string NormalizeAge(string ageLabel) =>
        string.IsNullOrWhiteSpace(ageLabel) ? "Unknown" : Capitalize(ageLabel.Trim());

    private static string NormalizeGender(string genderLabel) =>
        string.IsNullOrWhiteSpace(genderLabel) ? "Unknown" : Capitalize(genderLabel.Trim());

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);
}
