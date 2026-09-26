using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Makes Sims 4 assets under <c>Assets/Sims4/</c> import correctly with no manual steps.
    ///
    /// Why this is non-trivial: Unity's OBJ auto-material in HDRP mis-binds our textures (it painted
    /// the hat with the green specular map and the sofa with the wrong swatch). And material work
    /// done during model import sees null textures (the sibling PNGs aren't in the AssetDatabase
    /// yet). So we:
    ///   1. force Normals = Calculate (authored Sims normals import wrong);
    ///   2. flag normal/data textures correctly;
    ///   3. DISABLE Unity's auto-material (materialImportMode = None) so nothing fights us;
    ///   4. build an explicit HDRP/Lit material — loading textures SYNCHRONOUSLY so they're never
    ///      null — choosing the base map from the MTL's map_Kd (the correct primary swatch).
    ///
    /// If a scene instance was placed before the fix (stale material), use the menu
    /// <b>Sims4 Creator ▸ Fix Materials on Selection</b> — it rebuilds + assigns the material to the
    /// selected object directly, with textures guaranteed available.
    /// </summary>
    public sealed class Sims4AssetPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/Sims4/";

        private bool InScope => assetPath.Replace('\\', '/').Contains(Root);

        // --- Models ----------------------------------------------------------------------------
        private void OnPreprocessModel()
        {
            if (!InScope)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            // Stop Unity from creating its own (mis-bound) materials — we build our own below.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!InScope)
            {
                return;
            }

            try
            {
                var dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                var modelName = Path.GetFileNameWithoutExtension(assetPath);
                var mat = BuildSims4Material(dir, modelName);
                if (mat != null)
                {
                    AssignToAll(root, mat);
                }

                // Multi-swatch upgrade: if the exporter wrote a swatches.json with >1 swatch next to
                // the model, build one material per swatch and attach a Sims4Swatches switcher. The
                // switcher's ApplyCurrent then overrides the single material above with the default.
                TryAttachSwatchSwitcher(dir, root);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Sims4Creator] Material build FAILED for '{assetPath}': {ex}");
            }
        }

        // --- Textures --------------------------------------------------------------------------
        private void OnPreprocessTexture()
        {
            if (!InScope)
            {
                return;
            }

            var name = Path.GetFileName(assetPath).ToLowerInvariant();
            var importer = (TextureImporter)assetImporter;

            // Match "normal*" and "*_normal*" (e.g. skin_normal.png) so HDRP samples it as a normal map.
            if (name.Contains("normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (name.StartsWith("specular") || name.StartsWith("shadow") || name.StartsWith("mask"))
            {
                importer.sRGBTexture = false;
            }
            else if (name.StartsWith("detail_"))
            {
                // Skin DETAIL layers import LINEAR (sRGB=false) so the shader samples their RAW
                // gamma-authored bytes. The composite shader converts the base to GAMMA space for
                // the detail step (overlay relief AND "over" colored washes), so raw detail bytes
                // are already in the right space — do NOT flip this to sRGB or the detail step
                // double-converts. Preserve the coverage alpha. This rule is keyed on the
                // "detail_" filename prefix: any future detail layer MUST keep that prefix.
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
            }
            else if (name.StartsWith("base_") || name.StartsWith("eye_"))
            {
                // Skin BASE tones and EYE overlays are display COLOR → keep sRGB import (Unity
                // linearizes on sample, the HDRP-standard treatment). The composite shader samples
                // them linear, converts to gamma only for the detail-layer step, and writes an sRGB
                // RenderTexture (see SkinCompositor) so HDRP re-reads it as a normal sRGB
                // _BaseColorMap. Preserve the eye's iris alpha for the source-over step.
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
            }
        }

        // --- Shared material builder ------------------------------------------------------------
        private static Material BuildSims4Material(string dir, string modelName)
        {
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Sims4Creator] HDRP/Lit not found — is HDRP installed?");
                return null;
            }

            var matPath = $"{dir}/{modelName}_Sims4.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.shader = shader;
            mat.SetColor("_BaseColor", Color.white);

            // Base color = MTL map_Kd (the exporter's PRIMARY diffuse / correct swatch); fall back
            // to the first diffuse_* file.
            var texDir = $"{dir}/Textures";
            var diffuse = ResolveDiffuseFromMtl(dir, modelName) ?? LoadFirstTexture(texDir, "diffuse");
            var normal = LoadFirstTexture(texDir, "normal");

            if (diffuse != null)
            {
                mat.SetTexture("_BaseColorMap", diffuse);
            }

            if (normal != null)
            {
                mat.SetTexture("_NormalMap", normal);
                mat.SetFloat("_NormalScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Smoothness", 0.4f);
            EditorUtility.SetDirty(mat);

            Debug.Log($"[Sims4Creator] {modelName}: base={(diffuse != null ? diffuse.name : "NULL")} " +
                      $"normal={(normal != null ? normal.name : "NULL")} → {Path.GetFileName(matPath)}");
            return mat;
        }

        private static void AssignToAll(GameObject root, Material mat)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var count = Mathf.Max(1, renderer.sharedMaterials.Length);
                var replacement = new Material[count];
                for (var i = 0; i < count; i++)
                {
                    replacement[i] = mat;
                }

                renderer.sharedMaterials = replacement;
            }
        }

        private static Texture2D ResolveDiffuseFromMtl(string dir, string modelName)
        {
            var mtlPath = $"{dir}/{modelName}.mtl"; // project-relative; Editor cwd = project root
            if (!File.Exists(mtlPath))
            {
                return null;
            }

            foreach (var raw in File.ReadAllLines(mtlPath))
            {
                var line = raw.Trim();
                if (line.StartsWith("map_Kd", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = line.Substring("map_Kd".Length).Trim().Replace('\\', '/');
                    return LoadTextureSync($"{dir}/{rel}");
                }
            }

            return null;
        }

        private static Texture2D LoadFirstTexture(string texDir, string prefix)
        {
            if (!AssetDatabase.IsValidFolder(texDir))
            {
                return null;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).ToLowerInvariant().StartsWith(prefix))
                {
                    return LoadTextureSync(path);
                }
            }

            return null;
        }

        // Load a texture, forcing a synchronous import if it isn't in the AssetDatabase yet
        // (happens when the model imports before its sibling textures).
        private static Texture2D LoadTextureSync(string assetPath)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null && File.Exists(assetPath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }

            return tex;
        }

        // --- Swatches ---------------------------------------------------------------------------

        // Returns the slug for an asset folder (the folder name, e.g. "big_boss_sofa").
        private static string SlugFromDir(string dir)
        {
            return Path.GetFileName(dir.TrimEnd('/'));
        }

        // Read + parse "<dir>/swatches.json". Returns null if missing/empty/unparseable.
        private static Sims4SwatchesManifest LoadManifest(string dir)
        {
            var jsonPath = $"{dir}/swatches.json";
            if (!File.Exists(jsonPath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(jsonPath);
                var manifest = JsonUtility.FromJson<Sims4SwatchesManifest>(json);
                if (manifest == null || manifest.swatches == null || manifest.swatches.Length == 0)
                {
                    return null;
                }

                return manifest;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Sims4Creator] Failed to parse '{jsonPath}': {ex.Message}");
                return null;
            }
        }

        // Build one HDRP/Lit material for a single swatch and save it as an asset.
        // relDiffuse/relNormal are paths RELATIVE to the asset folder (forward slashes).
        private static Material BuildSwatchMaterial(string dir, string slug, int index,
            string relDiffuse, string relNormal)
        {
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Sims4Creator] HDRP/Lit not found — is HDRP installed?");
                return null;
            }

            var matPath = $"{dir}/{slug}_swatch{index:00}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.shader = shader;
            mat.SetColor("_BaseColor", Color.white);

            var diffuse = string.IsNullOrEmpty(relDiffuse) ? null : LoadTextureSync($"{dir}/{relDiffuse}");
            if (diffuse != null)
            {
                mat.SetTexture("_BaseColorMap", diffuse);
            }

            var normal = string.IsNullOrEmpty(relNormal) ? null : LoadTextureSync($"{dir}/{relNormal}");
            if (normal != null)
            {
                mat.SetTexture("_NormalMap", normal);
                mat.SetFloat("_NormalScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Smoothness", 0.4f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Build every swatch material for a manifest, in swatch-index order (slot i = swatch index i).
        // Returns parallel materials/labels arrays plus the clamped default index, or false if <2 swatches.
        private static bool BuildSwatchSet(string dir, Sims4SwatchesManifest manifest,
            out Material[] materials, out string[] labels, out int defaultIndex)
        {
            materials = null;
            labels = null;
            defaultIndex = 0;

            if (manifest == null || manifest.swatches == null || manifest.swatches.Length < 2)
            {
                return false;
            }

            var slug = SlugFromDir(dir);

            // Size by the largest declared index so slot i lines up with swatch.index = i.
            var maxIndex = manifest.swatches.Length - 1;
            foreach (var s in manifest.swatches)
            {
                if (s.index > maxIndex)
                {
                    maxIndex = s.index;
                }
            }

            materials = new Material[maxIndex + 1];
            labels = new string[maxIndex + 1];

            foreach (var s in manifest.swatches)
            {
                var slot = (s.index >= 0 && s.index <= maxIndex) ? s.index : 0;
                materials[slot] = BuildSwatchMaterial(dir, slug, slot, s.diffuse, s.normal);
                labels[slot] = string.IsNullOrEmpty(s.label) ? $"Swatch {slot}" : s.label;
            }

            defaultIndex = Mathf.Clamp(manifest.defaultIndex, 0, materials.Length - 1);

            // NOTE: do NOT call AssetDatabase.SaveAssets() here — this runs inside OnPostprocessModel
            // (asset import), where SaveAssets is forbidden. Assets created via CreateAsset during
            // import are persisted by the import itself.
            Debug.Log($"[Sims4Creator] {slug}: built {materials.Length} swatch material(s), default={defaultIndex}.");
            return true;
        }

        // Wire a Sims4Swatches switcher onto 'root' from the manifest in 'dir' (import-time path).
        private static void TryAttachSwatchSwitcher(string dir, GameObject root)
        {
            var manifest = LoadManifest(dir);
            if (!BuildSwatchSet(dir, manifest, out var materials, out var labels, out var defaultIndex))
            {
                return; // absent or single-swatch → keep the single-material behavior.
            }

            ConfigureSwitcher(root, materials, labels, defaultIndex);
        }

        // Add (or reuse) a Sims4Swatches component on 'go', populate it, and apply the default swatch.
        private static Sims4Creator.Sims4Swatches ConfigureSwitcher(GameObject go,
            Material[] materials, string[] labels, int defaultIndex)
        {
            var switcher = go.GetComponent<Sims4Creator.Sims4Swatches>();
            if (switcher == null)
            {
                switcher = go.AddComponent<Sims4Creator.Sims4Swatches>();
            }

            switcher.swatchMaterials = materials;
            switcher.swatchLabels = labels;
            switcher.SelectedIndex = defaultIndex; // clamps + ApplyCurrent() → default swatch shows.
            return switcher;
        }

        // --- Menus ------------------------------------------------------------------------------

        // Reliable manual fix for already-placed scene objects (no import timing / stale instances).
        [MenuItem("Sims4 Creator/Assets/Fix Materials on Selection", priority = 61)]
        public static void FixSelection()
        {
            var fixedCount = 0;
            foreach (var go in Selection.gameObjects)
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    var meshPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                    if (string.IsNullOrEmpty(meshPath))
                    {
                        continue;
                    }

                    var dir = Path.GetDirectoryName(meshPath).Replace('\\', '/');
                    var modelName = Path.GetFileNameWithoutExtension(meshPath);
                    var mat = BuildSims4Material(dir, modelName);
                    var renderer = mf.GetComponent<Renderer>();
                    if (mat != null && renderer != null)
                    {
                        var count = Mathf.Max(1, renderer.sharedMaterials.Length);
                        var arr = new Material[count];
                        for (var i = 0; i < count; i++)
                        {
                            arr[i] = mat;
                        }

                        renderer.sharedMaterials = arr;
                        fixedCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Sims4Creator] Fixed materials on {fixedCount} renderer(s) in the selection.");
        }

        [MenuItem("Sims4 Creator/Assets/Reimport Sims4 Assets", priority = 60)]
        public static void ReimportAll()
        {
            AssetDatabase.ImportAsset(
                "Assets/Sims4",
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("[Sims4Creator] Reimported Assets/Sims4 with the Sims4 postprocessor applied.");
        }

        // Dependable fallback for already-placed scene objects: resolve the asset folder from a child
        // MeshFilter's mesh asset path (same trick as Fix Materials on Selection), rebuild the swatch
        // materials, and add/refresh the Sims4Swatches switcher on each selected root.
        [MenuItem("Sims4 Creator/Assets/Add or Refresh Swatch Switcher on Selection", priority = 62)]
        public static void AddOrRefreshSwitcherOnSelection()
        {
            var configured = 0;
            foreach (var go in Selection.gameObjects)
            {
                var dir = ResolveAssetFolder(go);
                if (string.IsNullOrEmpty(dir))
                {
                    Debug.LogWarning($"[Sims4Creator] '{go.name}': no child MeshFilter with a saved mesh — skipped.");
                    continue;
                }

                var manifest = LoadManifest(dir);
                if (!BuildSwatchSet(dir, manifest, out var materials, out var labels, out var defaultIndex))
                {
                    Debug.LogWarning($"[Sims4Creator] '{go.name}': no swatches.json with >1 swatch in '{dir}' — skipped.");
                    continue;
                }

                Undo.RegisterFullObjectHierarchyUndo(go, "Add Swatch Switcher");
                ConfigureSwitcher(go, materials, labels, defaultIndex);
                EditorUtility.SetDirty(go);
                configured++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Sims4Creator] Configured swatch switcher on {configured} selected object(s).");
        }

        // Find the asset folder for a placed object by looking at the first child MeshFilter whose
        // sharedMesh is a saved asset, then taking that asset's directory.
        private static string ResolveAssetFolder(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var meshPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (!string.IsNullOrEmpty(meshPath))
                {
                    return Path.GetDirectoryName(meshPath).Replace('\\', '/');
                }
            }

            return null;
        }
    }

    // --- swatches.json DTOs ---------------------------------------------------------------------
    // JsonUtility handles arrays of [Serializable] classes and leaves missing fields at null/default,
    // which matches the optional colorHex / stateHash / normal / specular / shadow fields.

    [Serializable]
    public sealed class Sims4SwatchesManifest
    {
        public string asset;
        public string kind; // "buildbuy" | "cas"
        public int defaultIndex;
        public Sims4SwatchEntry[] swatches;
    }

    [Serializable]
    public sealed class Sims4SwatchEntry
    {
        public int index;       // 0-based, matches array position
        public string label;    // human label
        public string colorHex; // "#RRGGBB" or null (CAS tint)
        public string stateHash; // "0xXXXXXXXX" or null (Build/Buy MTST)
        public bool isDefault;
        public string diffuse;  // relpath from asset folder, forward slashes
        public string normal;   // relpath or null
        public string specular; // relpath or null
        public string shadow;   // relpath or null
    }
}
