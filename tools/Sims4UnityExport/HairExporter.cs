// HairExporter — the `exporthair` pipeline. Adds HAIR to an already-exported character.json.
//
// A hairstyle in EA CAS = a family of CASParts (body_type 2) named yfHair_<Style>_<Colour>. Every
// colour is a SEPARATE CASP that shares the same hair GEOM but references a different baked diffuse
// (RGBA, alpha = strand silhouette). So we model a style as ONE hair mesh + N colour diffuses, and
// the runtime picks a colour by swapping the material's base-colour map — cheap, no re-skin.
//
// Steps:
//   1. Enumerate the style's colour variants from the resource index (internal_name LIKE 'yfHair_<Style>_%').
//   2. Resolve a REPRESENTATIVE colour through the same CAS asset-graph → scene path as `exportcas`,
//      take the largest mesh as the hair, and re-skin its bone indices onto the TARGET character's
//      shared skeleton by BONE NAME (hair-physics bones we lack fall back to b__Head__, so the hair
//      simply rides the head — correct enough for a CAS preview).
//   3. For every colour (capped at maxColors), pull the BaseColor texture and write it as a PNG.
//   4. Append/replace this style in character.json's hairCatalog via JsonNode (everything else is
//      left byte-for-byte untouched).

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Sims4ResourceExplorer.Core;

namespace Sims4UnityExport;

internal static class HairExporter
{
    // Same standard cache DB ProbeAsset + the exporter's SqliteIndexStore use.
    private const string IndexDb =
        @"C:\Users\stani\AppData\Local\Sims4ResourceExplorer\Cache\index.sqlite";

    private static readonly JsonSerializerOptions NodeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    public static async Task RunAsync(
        string unityAssetsDir, string charSlug, string styleName, int maxColors,
        IIndexStore indexStore, IAssetGraphBuilder graphBuilder, ISceneBuildService sceneBuilder,
        CancellationToken ct)
    {
        Console.WriteLine($"Initializing index...");
        await indexStore.InitializeAsync(ct);

        var charDir = Path.Combine(unityAssetsDir, charSlug);
        var manifestPath = Path.Combine(charDir, "character.json");
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"character.json not found at '{manifestPath}'. Export the character first (exportchar/exportsim).");
            return;
        }

        // 1. Enumerate colour variants of the style from the index.
        var colors = EnumerateStyleColors(styleName);
        if (colors.Count == 0)
        {
            Console.Error.WriteLine($"No CAS parts found for style '{styleName}' (body_type 2). Check the internal name.");
            return;
        }
        Console.WriteLine($"Style '{styleName}': {colors.Count} colour variant(s) in the index.");

        // 2. Shared skeleton (bone name -> index) from the existing character.json.
        var root = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var skel = root["skeleton"]?.AsArray();
        if (skel is null || skel.Count == 0)
        {
            Console.Error.WriteLine("character.json has no skeleton; cannot skin hair to it.");
            return;
        }
        var boneIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < skel.Count; i++)
        {
            var bn = skel[i]?["name"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(bn) && !boneIndexByName.ContainsKey(bn)) boneIndexByName[bn] = i;
        }
        var headIdx = boneIndexByName.TryGetValue("b__Head__", out var hi) ? hi : 0;
        Console.WriteLine($"Target skeleton: {skel.Count} bones; hair fallback bone = b__Head__ (index {headIdx}).");

        // Representative colour for the MESH: prefer Black, else the first.
        var rep = colors.FirstOrDefault(c => c.Color.Equals("Black", StringComparison.OrdinalIgnoreCase)) ?? colors[0];

        // 3. Resolve the representative -> scene, take the hair mesh, re-skin to the shared skeleton.
        Console.WriteLine($"Resolving representative colour '{rep.Color}' ({rep.FullTgi}) for the hair MESH...");
        var repScene = await ResolveSceneAsync(rep, indexStore, graphBuilder, sceneBuilder, ct);
        if (repScene is null)
        {
            Console.Error.WriteLine("Representative colour did not build a scene; aborting.");
            return;
        }
        if (repScene.Meshes.Count == 0)
        {
            Console.Error.WriteLine("Hair scene has no meshes; aborting.");
            return;
        }
        Console.WriteLine($"Hair scene: {repScene.Meshes.Count} mesh(es), {repScene.Bones.Count} bone(s), {repScene.Materials.Count} material(s).");
        foreach (var m in repScene.Meshes)
            Console.WriteLine($"  mesh '{m.Name}' verts={m.Positions.Count / 3} tris={m.Indices.Count / 3} mat={m.MaterialIndex}");

        // Largest mesh = the main hair (LODs are already collapsed to highest detail upstream).
        var hairMesh = repScene.Meshes.OrderByDescending(m => m.Positions.Count).First();
        var hairMat = hairMesh.MaterialIndex >= 0 && hairMesh.MaterialIndex < repScene.Materials.Count
            ? repScene.Materials[hairMesh.MaterialIndex] : null;

        // Re-skin: hairScene bone order -> shared skeleton by NAME; unknown/hair-physics bones -> head.
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        var fellBack = 0;
        int Remap(int oldIdx)
        {
            if (oldIdx < 0 || oldIdx >= repScene.Bones.Count) { fellBack++; return headIdx; }
            var nm = repScene.Bones[oldIdx].Name ?? "";
            used[nm] = used.TryGetValue(nm, out var c) ? c + 1 : 1;
            if (boneIndexByName.TryGetValue(nm, out var ni)) return ni;
            fellBack++;
            return headIdx;
        }
        var cmesh = BuildMeshRemapped(hairMesh, skel.Count, Remap);
        var missing = used.Keys.Where(k => !boneIndexByName.ContainsKey(k)).ToList();
        Console.WriteLine($"Hair skin: {used.Count} distinct source bone(s); " +
                          $"{missing.Count} not on the shared skeleton -> b__Head__ [{string.Join(", ", missing.Take(12))}{(missing.Count > 12 ? ", ..." : "")}].");

        // 4. Textures. Write the diffuse for every colour; grab normal/specular once (shared).
        var hairFolder = Path.Combine(charDir, "Hair");
        Directory.CreateDirectory(hairFolder);

        string? WriteTex(CanonicalScene scene, string colorId, CanonicalTextureSemantic sem, string suffix)
        {
            var mat = PickHairMaterial(scene);
            var tex = PickTexture(mat, sem);
            if (tex is null || tex.PngBytes.Length == 0) return null;
            var file = $"{Sanitize(styleName)}_{Sanitize(colorId)}{suffix}.png";
            File.WriteAllBytes(Path.Combine(hairFolder, file), ClothingExporter.DilateFabricRgb(tex.PngBytes, 12, seedMinAlpha: 26, fillMaxAlpha: 26)); // RGB bled under alpha edge (EA green cutout fix)
            return $"Hair/{file}";
        }

        var normalRel = WriteTex(repScene, "shared", CanonicalTextureSemantic.Normal, "_normal");
        var specRel = WriteTex(repScene, "shared", CanonicalTextureSemantic.Specular, "_spec");

        var outColors = new List<CharacterHairColor>();
        var n = 0;
        foreach (var c in colors.OrderBy(c => c.Color, StringComparer.OrdinalIgnoreCase))
        {
            if (n >= maxColors) { Console.WriteLine($"maxColors={maxColors} reached; stopping at {n} colour(s)."); break; }
            CanonicalScene? scene = ReferenceEquals(c, rep)
                ? repScene
                : await ResolveSceneAsync(c, indexStore, graphBuilder, sceneBuilder, ct);
            if (scene is null) { Console.Error.WriteLine($"  colour '{c.Color}': no scene; skipped."); continue; }
            var diffMat = PickHairMaterial(scene);
            var diff = PickTexture(diffMat, CanonicalTextureSemantic.BaseColor);
            if (diff is null || diff.PngBytes.Length == 0) { Console.Error.WriteLine($"  colour '{c.Color}': no diffuse; skipped."); continue; }
            var file = $"{Sanitize(styleName)}_{Sanitize(c.Color)}.png";
            File.WriteAllBytes(Path.Combine(hairFolder, file), ClothingExporter.DilateFabricRgb(diff.PngBytes, 12, seedMinAlpha: 26, fillMaxAlpha: 26)); // RGB bled under alpha edge (EA green cutout fix)
            var srcKey = diff.SourceKey is { } k ? k.FullTgi : "(none)";
            Console.WriteLine($"  colour '{c.Color,-18}' diffuse {diff.PngBytes.Length,7} B  src={srcKey}");
            outColors.Add(new CharacterHairColor { Id = c.Color, Label = Spaced(c.Color), Diffuse = $"Hair/{file}" });
            n++;
        }
        if (outColors.Count == 0) { Console.Error.WriteLine("No colour diffuses written; aborting (no hairCatalog change)."); return; }

        // 5. Assemble the style + append to character.json's hairCatalog (JsonNode; nothing else touched).
        var part = new CharacterPart
        {
            Name = styleName,
            Region = "Hair",
            Mesh = cmesh,
            Material = new CharacterMaterial
            {
                Diffuse = outColors.FirstOrDefault(x => x.Id.Equals(rep.Color, StringComparison.OrdinalIgnoreCase))?.Diffuse
                          ?? outColors[0].Diffuse,
                Normal = normalRel,
                Specular = specRel,
            },
        };
        var style = new CharacterHairStyle
        {
            Id = styleName,
            Label = Spaced(StripPack(styleName)),
            Part = part,
            Colors = outColors,
            DefaultColor = outColors.Any(x => x.Id.Equals(rep.Color, StringComparison.OrdinalIgnoreCase))
                ? rep.Color : outColors[0].Id,
        };

        // Merge with any existing hairCatalog (accumulate styles across runs).
        CharacterHairCatalog catalog;
        if (root["hairCatalog"] is JsonObject existing)
            catalog = existing.Deserialize<CharacterHairCatalog>(NodeOptions) ?? new CharacterHairCatalog();
        else
            catalog = new CharacterHairCatalog();
        catalog.Options.RemoveAll(o => string.Equals(o.Id, styleName, StringComparison.OrdinalIgnoreCase));
        catalog.Options.Add(style);
        catalog.Default = styleName; // start on the just-exported style so it's visible immediately

        root["hairCatalog"] = JsonSerializer.SerializeToNode(catalog, NodeOptions);
        File.WriteAllText(manifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine($"DONE. hairCatalog now has {catalog.Options.Count} style(s); '{styleName}' = '{style.Label}' " +
                          $"with {outColors.Count} colour(s), default '{style.DefaultColor}'. Wrote {manifestPath}.");
        Console.WriteLine("Rebuild the character in Unity (Sims4 Creator > Character > Build CAS Editor Scene) to pick up hair.");
    }

    // ---- resource enumeration -----------------------------------------------------------------

    private sealed record HairVariant(string InternalName, string Color, string FullTgi, ulong Instance, string PackagePath);

    private static List<HairVariant> EnumerateStyleColors(string styleName)
    {
        var list = new List<HairVariant>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // The rebuilt catalog is SHARDED (index.sqlite + index.shardNN.sqlite) — a package's rows live in
        // exactly one shard, so enumeration must scan ALL of them (the store API does; raw SQL must too).
        foreach (var dbPath in IndexShardPaths())
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT internal_name, root_tgi, package_path
                FROM cas_part_facts
                WHERE body_type = 2 AND internal_name LIKE $like ESCAPE '!'
                ORDER BY internal_name
                """;
            cmd.Parameters.AddWithValue("$like", styleName.Replace("_", "!_") + "!_%");
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.IsDBNull(0) ? "" : reader.GetString(0);
                var tgi = reader.GetString(1);
                var pkg = reader.IsDBNull(2) ? "" : reader.GetString(2);
                if (name.Length <= styleName.Length + 1) continue;
                var color = name.Substring(styleName.Length + 1);
                if (!seen.Add(color)) continue; // collapse duplicate index rows (multi-package / age / shard share)
                if (!TryParseInstance(tgi, out var inst)) continue;
                list.Add(new HairVariant(name, color, tgi, inst, pkg));
            }
        }
        return list;
    }

    // All serving catalog databases (main + shards).
    internal static IEnumerable<string> IndexShardPaths()
    {
        var dir = Path.GetDirectoryName(IndexDb)!;
        yield return IndexDb;
        foreach (var f in Directory.EnumerateFiles(dir, "index.shard*.sqlite")) yield return f;
    }

    // ---- scene resolution (mirrors RunExportCasAsync core) ------------------------------------

    // Resolve a colour variant to a scene WITHOUT the slow paged AssetSummary scan: fetch the CASP's
    // resources by instance (index-keyed = fast), synthesise a minimal CAS AssetSummary (BuildCasGraphAsync
    // only reads RootKey.FullTgi + AssetKind), then build the graph + scene exactly as exportcas does.
    private static async Task<CanonicalScene?> ResolveSceneAsync(
        HairVariant v, IIndexStore indexStore, IAssetGraphBuilder graphBuilder, ISceneBuildService sceneBuilder,
        CancellationToken ct)
    {
        var resources = await indexStore.GetResourcesByFullInstanceAsync(v.Instance, ct);
        if (resources.Count == 0) { Console.Error.WriteLine($"  '{v.Color}': no resources for instance 0x{v.Instance:X16}."); return null; }
        var root = resources.FirstOrDefault(r => string.Equals(r.Key.FullTgi, v.FullTgi, StringComparison.OrdinalIgnoreCase))
                   ?? resources.FirstOrDefault(r => string.Equals(r.Key.TypeName, "CASPart", StringComparison.OrdinalIgnoreCase));
        if (root is null) { Console.Error.WriteLine($"  '{v.Color}': no CASPart root among {resources.Count} resource(s)."); return null; }

        var summary = new AssetSummary(
            Id: Guid.NewGuid(),
            DataSourceId: Guid.Empty,
            SourceKind: root.SourceKind,
            AssetKind: AssetKind.Cas,
            DisplayName: v.InternalName,
            Category: "CAS Part",
            PackagePath: root.PackagePath,
            RootKey: root.Key,
            ThumbnailTgi: null,
            VariantCount: 1,
            LinkedResourceCount: resources.Count,
            Diagnostics: string.Empty,
            PackageName: Path.GetFileName(root.PackagePath),
            RootTypeName: root.Key.TypeName,
            IdentityType: root.Key.TypeName);

        var graph = await graphBuilder.BuildAssetGraphAsync(summary, resources, ct);
        if (graph.CasGraph is not { } casGraph)
        {
            Console.Error.WriteLine($"  '{v.Color}': no CasGraph ({string.Join("; ", graph.Diagnostics.Take(2))}).");
            return null;
        }
        var sceneResult = await sceneBuilder.BuildSceneAsync(casGraph, ct);
        if (!sceneResult.Success || sceneResult.Scene is null)
        {
            Console.Error.WriteLine($"  '{v.Color}': scene build failed ({sceneResult.Status}).");
            return null;
        }
        return sceneResult.Scene;
    }

    private static CanonicalMaterial? PickHairMaterial(CanonicalScene scene)
    {
        if (scene.Materials.Count == 0) return null;
        // The hair is the largest mesh; use its material.
        var hairMesh = scene.Meshes.OrderByDescending(m => m.Positions.Count).FirstOrDefault();
        if (hairMesh != null && hairMesh.MaterialIndex >= 0 && hairMesh.MaterialIndex < scene.Materials.Count)
            return scene.Materials[hairMesh.MaterialIndex];
        return scene.Materials[0];
    }

    private static CanonicalTexture? PickTexture(CanonicalMaterial? mat, CanonicalTextureSemantic sem)
    {
        if (mat is null || mat.Textures.Count == 0) return null;
        var bySem = mat.Textures.FirstOrDefault(t => t.Semantic == sem);
        if (bySem != null) return bySem;
        // Fallback by slot name for the common semantics.
        var key = sem switch
        {
            CanonicalTextureSemantic.BaseColor => new[] { "diff", "albedo", "base" },
            CanonicalTextureSemantic.Normal => new[] { "norm", "bump" },
            CanonicalTextureSemantic.Specular => new[] { "spec", "gloss" },
            _ => Array.Empty<string>(),
        };
        var bySlot = mat.Textures.FirstOrDefault(t => key.Any(k => t.Slot.Contains(k, StringComparison.OrdinalIgnoreCase)));
        if (bySlot != null) return bySlot;
        // Base color: last resort = the first texture (a hair CASP's primary map).
        return sem == CanonicalTextureSemantic.BaseColor ? mat.Textures[0] : null;
    }

    // ---- mesh conversion (self-contained copy of the exportchar skin path, with a custom remap) -

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

    private static string Sanitize(string s)
    {
        var chars = s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }

    // "yfHair_GP12LongNatural" -> "LongNatural" (drop the [yf/ym/yu]Hair_ + a pack code like GP12/EP16/SP49).
    private static string StripPack(string internalName)
    {
        var s = internalName;
        foreach (var p in new[] { "yfHair_", "ymHair_", "yuHair_" })
            if (s.StartsWith(p, StringComparison.OrdinalIgnoreCase)) { s = s.Substring(p.Length); break; }
        s = Regex.Replace(s, @"^(?:[A-Za-z]{2}\d{2}|Ep\d+|Sp\d+|Gp\d+)", "");
        return string.IsNullOrEmpty(s) ? internalName : s;
    }

    // "LongNatural" -> "Long Natural"; "BlackSaltAndPepper" -> "Black Salt And Pepper".
    private static string Spaced(string camel)
    {
        if (string.IsNullOrEmpty(camel)) return camel;
        var spaced = Regex.Replace(camel, @"(?<=[a-z0-9])(?=[A-Z])", " ");
        return spaced;
    }
}
