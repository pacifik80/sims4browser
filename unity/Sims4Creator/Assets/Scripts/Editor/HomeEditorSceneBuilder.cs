using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Builds the M1 "Home Editor" scene: a 24×24m ground grid, an HDRP sky/light rig, and a
    /// furniture catalog assembled from the Build/Buy exports under Assets/Sims4/home/&lt;folder&gt;/
    /// (driven by home_catalog.json). Each catalog item becomes an inactive TEMPLATE GameObject with
    /// HDRP materials rebuilt from the exported diffuse textures (the OBJ/FBX importers wire Standard
    /// materials, which render pink in HDRP) and a BoxCollider for click-selection.
    /// Run: <b>Sims4 Creator ▸ Home ▸ Build Home Editor Scene</b>, then Play.
    /// </summary>
    public static class HomeEditorSceneBuilder
    {
        private const string HomeRoot = "Assets/Sims4/home";
        private const string ScenePath = "Assets/Scenes/HomeEditor.unity";

        [System.Serializable]
        internal sealed class CatalogFile { public List<CatalogEntry> items = new(); }

        [System.Serializable]
        internal sealed class CatalogEntry
        {
            public string id;
            public string label;
            public string category;
            public string folder;
            public string wallItem;
            // Model-less PORTAL entries (wallItem == "portal", no folder): explicit opening dims
            // in metres. cutoutShape "arch" synthesizes an arched top; anything else = rectangle.
            public float openWidth;
            public float holeY0;
            public float holeY1;
            public int footW;
            public int footD;
            public string cutoutShape;
        }

        [System.Serializable]
        private sealed class CoveringsFile { public List<CoveringJson> walls = new(); public List<CoveringJson> floors = new(); }

        [System.Serializable]
        private sealed class SwatchesFile { public int defaultIndex; public List<SwatchJson> swatches = new(); }

        [System.Serializable]
        private sealed class SwatchJson { public int index; public string label; public bool isDefault; public string diffuse; }

        [System.Serializable]
        private sealed class CoveringJson { public string id; public string label; public string texture; public string thumb; }

        [System.Serializable]
        private sealed class CutoutJson { public List<Vector2> points = new(); }

        [MenuItem("Sims4 Creator/Home/Build Home Editor Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return; // user cancelled — don't silently discard their open scene
            }
            var catalogPath = $"{HomeRoot}/home_catalog.json";
            if (!File.Exists(catalogPath))
            {
                Debug.LogError($"[HomeEditor] {catalogPath} not found — export the starter set first.");
                return;
            }
            var catalog = JsonUtility.FromJson<CatalogFile>(File.ReadAllText(catalogPath));
            if (catalog?.items == null || catalog.items.Count == 0)
            {
                Debug.LogError("[HomeEditor] home_catalog.json has no items.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- camera ----
            var camGo = new GameObject("Home Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.AddComponent<HDAdditionalCameraData>();
            cam.transform.SetPositionAndRotation(new Vector3(12f, 14f, -12f), Quaternion.Euler(42f, -45f, 0f));

            // ---- SUN + MOON + sky (fresh scene has NO volume: without this everything renders black).
            // SkyTimeController drives sun/moon DIRECTION + intensity from city/date/time each frame; the
            // values set here are just defaults. Both lights interact with the PBR sky (sun/moon disks). ----
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; // BEFORE Init — it branches on light.type
            var sunHd = sunGo.AddComponent<HDAdditionalLightData>();
            HDAdditionalLightData.InitDefaultHDAdditionalLightData(sunHd);
            sunGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            sun.intensity = 100000f;
            sun.shadows = LightShadows.Soft;
            sunHd.angularDiameter = 0.53f;   // realistic solar disk
            sunHd.interactsWithSky = true;
            sunHd.shadowDimmer = 0.8f;
            // Grounded shadows: the default 512px map over a 500m range smears, and the default
            // slope/normal biases push the shadow away from the wall base (the visible gap).
            sunHd.shadowResolution.useOverride = true;
            sunHd.shadowResolution.@override = 2048;
            sunHd.slopeBias = 0.15f;
            sunHd.normalBias = 0.2f;

            var moonGo = new GameObject("Moon");
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            var moonHd = moonGo.AddComponent<HDAdditionalLightData>();
            HDAdditionalLightData.InitDefaultHDAdditionalLightData(moonHd);
            moon.color = new Color(0.55f, 0.66f, 0.95f);
            moon.shadows = LightShadows.Soft;
            moonHd.angularDiameter = 0.5f;
            moonHd.interactsWithSky = true;
            moonHd.surfaceTint = new Color(0.85f, 0.87f, 0.95f);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var profilePath = $"{HomeRoot}/HomeVolumeProfile.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath) != null)
            {
                AssetDatabase.DeleteAsset(profilePath);
            }
            AssetDatabase.CreateAsset(profile, profilePath);
            T AddOverride<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>();
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var env = AddOverride<VisualEnvironment>();
            env.skyType.Override((int)SkyType.PhysicallyBased);
            // The PBR sky's default groundTint is a dark BROWN (0.12,0.10,0.09) — the planet surface
            // colour the horizon blends toward, which makes a down-looking camera see a brown haze
            // instead of sky. A cool neutral ground tint keeps the horizon pale and the ambient neutral.
            var pbs = AddOverride<PhysicallyBasedSky>();
            pbs.groundTint.Override(new Color(0.16f, 0.18f, 0.19f));
            // FIXED exposure, driven per time-of-day by SkyTimeController. Auto-exposure can't span a
            // full day→night cycle (it normalises brightness, so night looks like day and its clamp
            // range can't reach either extreme); a time-driven fixed EV keeps day bright and night dark.
            var exposure = AddOverride<Exposure>();
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(13f);
            // Concentrate the shadow map on the lot (default 500m range wastes nearly all texels);
            // 80m keeps far lot corners shadowed at maximum camera zoom-out (~79m).
            AddOverride<HDShadowSettings>().maxShadowDistance.Override(80f);
            // Screen-space reflections: smooth surfaces (the mirror at smoothness 0.97) reflect the
            // on-screen room. This is what makes a mirror reflect the room rather than render black.
            // The mirror's 0.97 smoothness clears HDRP's default SSR minSmoothness (0.9) threshold.
            AddOverride<ScreenSpaceReflection>().enabled.Override(true);
            EditorUtility.SetDirty(profile);

            // SkyTimeController needs sun + moon (above) AND the exposure override (just declared).
            var skyGo = new GameObject("SkyTime");
            var skyCtl = skyGo.AddComponent<Sims4Creator.SkyTimeController>();
            skyCtl.sun = sun;
            skyCtl.moon = moon;
            skyCtl.exposure = exposure; // time-of-day drives fixed EV
            skyCtl.cityIndex = 0;               // London
            skyCtl.month = 6;
            skyCtl.day = 21;                    // summer solstice
            skyCtl.timeOfDay = 12f;
            skyCtl.Apply();

            var volumeGo = new GameObject("Home Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            // Flush the profile's sub-assets NOW: AddObjectToAsset is memory-only until an asset
            // save, and an editor exit before one leaves an empty profile → black scene next session.
            AssetDatabase.SaveAssets();

            // ---- ground: 24×24m plane with a 1m grid texture (texture repeats once per tile) ----
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2.4f, 1f, 2.4f); // plane primitive is 10×10m
            var groundMat = BuildGridMaterial();
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            // ---- catalog templates ----
            var catalogRoot = new GameObject("HomeCatalog");
            var placedRoot = new GameObject("Placed");
            var defs = new List<Sims4Creator.HomeEditor.ItemDef>();
            foreach (var entry in catalog.items)
            {
                // PORTAL: a passable opening with NO object — build a model-less def straight from
                // the catalog's explicit opening dims (no template, no export folder needed).
                if (entry.wallItem == "portal")
                {
                    defs.Add(BuildPortalDef(entry));
                    continue;
                }
                var template = BuildTemplate(entry, catalogRoot.transform, out var footW, out var footD, out var bounds);
                if (template == null)
                {
                    Debug.LogWarning($"[HomeEditor] item '{entry.id}': no importable model found — skipped.");
                    continue;
                }
                var itemFolder = $"{HomeRoot}/{entry.folder}";
                var def = new Sims4Creator.HomeEditor.ItemDef
                {
                    id = entry.id,
                    label = string.IsNullOrEmpty(entry.label) ? entry.id : entry.label,
                    category = string.IsNullOrEmpty(entry.category) ? "Objects" : entry.category,
                    wallItem = entry.wallItem ?? string.Empty,
                    template = template,
                    thumb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{itemFolder}/thumb.png")
                            ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{itemFolder}/thumb.jpg"),
                    footW = footW,
                    footD = footD,
                };
                WireSwatches(def, itemFolder, template);
                if (!string.IsNullOrEmpty(def.wallItem))
                {
                    // Opening geometry from the item's AUTHORED bounds: real hole width with jambs,
                    // real height; short ground-authored windows get the game's sill lift (0.9m).
                    var isWindow = def.wallItem == "window";
                    var lift = isWindow && bounds.size.y <= 1.7f && bounds.min.y < 0.3f ? 0.9f : 0f;
                    var itemTop = lift + bounds.max.y;
                    if (itemTop > Sims4Creator.HomeEditor.DefaultWallHeight + 0.15f)
                    {
                        Debug.LogWarning($"[HomeEditor] wall item '{def.id}' tops out at {itemTop:0.00}m — taller than the {Sims4Creator.HomeEditor.DefaultWallHeight:0.0}m wall, excluded (needs tall-wall support).");
                        UnityEngine.Object.DestroyImmediate(template);
                        continue;
                    }
                    def.liftY = lift;
                    // The cut sits slightly INSIDE the item's silhouette so the frame overlaps the
                    // hole edges — outward margins guaranteed a see-through ring around every frame.
                    def.openWidth = Mathf.Clamp(bounds.size.x - 0.04f, 0.3f, 4f);
                    def.holeY0 = isWindow ? Mathf.Max(0f, lift + bounds.min.y + 0.02f) : 0f;
                    def.holeY1 = lift + bounds.max.y - 0.02f;
                    // The game's exact silhouette (exportcutouts → ModelCutout polygon) replaces
                    // the rect when present; the rect fields above stay as the fallback.
                    var cutoutPath = $"{itemFolder}/cutout.json";
                    if (File.Exists(cutoutPath))
                    {
                        var cj = JsonUtility.FromJson<CutoutJson>(File.ReadAllText(cutoutPath));
                        if (cj?.points != null && cj.points.Count >= 3)
                        {
                            // Unity's OBJ/FBX import NEGATES X (RH→LH handedness flip) — the
                            // template mesh is mirrored vs the raw game-frame cutout polygon, so
                            // mirror the polygon to match (asymmetric cutouts misalign otherwise).
                            def.cutout = new List<Vector2>(cj.points.Count);
                            foreach (var p in cj.points)
                            {
                                def.cutout.Add(new Vector2(-p.x, p.y));
                            }
                        }
                    }
                    Debug.Log($"[HomeEditor] wall item '{def.id}': hole {def.openWidth:0.00}m wide, y {def.holeY0:0.00}-{def.holeY1:0.00}, lift {lift:0.0}, cutout {(def.cutout.Count >= 3 ? $"{def.cutout.Count} pts" : "rect fallback")}.");
                }
                defs.Add(def);
            }
            catalogRoot.SetActive(false); // templates never render; HomeEditor clones them
            var portalCount = defs.FindAll(d => d.wallItem == "portal").Count;
            Debug.Log($"[HomeEditor] catalog: {defs.Count} item(s), {portalCount} portal(s).");

            // ---- controller ----
            var editorGo = new GameObject("HomeEditor");
            var editor = editorGo.AddComponent<Sims4Creator.HomeEditor>();
            editor.items = defs;
            editor.editorCamera = cam;
            editor.placedRoot = placedRoot.transform;
            editor.cellFreeMaterial = BuildCellMaterial("HomeCellFree", new Color(0.2f, 0.95f, 0.35f, 0.35f));
            editor.cellBlockedMaterial = BuildCellMaterial("HomeCellBlocked", new Color(1f, 0.25f, 0.2f, 0.45f));

            // ---- coverings (M2): materials from the exported wall/floor tiling textures ----
            var coveringsPath = $"{HomeRoot}/coverings/coverings.json";
            if (File.Exists(coveringsPath))
            {
                var coverings = JsonUtility.FromJson<CoveringsFile>(File.ReadAllText(coveringsPath));
                editor.wallCoverings = BuildCoverings(coverings?.walls, "walls", smoothness: 0.08f);
                editor.floorCoverings = BuildCoverings(coverings?.floors, "floors", smoothness: 0.25f);
                Debug.Log($"[HomeEditor] coverings: {editor.wallCoverings.Count} wall, {editor.floorCoverings.Count} floor.");
            }
            else
            {
                Debug.LogWarning("[HomeEditor] coverings/coverings.json missing — run 'exportcoverings' to enable wall/floor painting.");
            }
            editor.defaultWallMaterial = BuildPlainMaterial("HomeWallDefault", new Color(0.93f, 0.92f, 0.90f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[HomeEditor] scene built with {defs.Count}/{catalog.items.Count} catalog item(s) → {ScenePath}. Press Play.");
        }

        // A PORTAL is a passable wall opening with no object: a model-less ItemDef whose opening
        // dims come straight from the catalog entry. cutoutShape "arch" synthesizes an arched top
        // (straight jambs then a half-ellipse); anything else uses the plain rectangle path.
        private static Sims4Creator.HomeEditor.ItemDef BuildPortalDef(CatalogEntry entry)
        {
            var footW = Mathf.Max(1, entry.footW);
            var openWidth = Mathf.Clamp(entry.openWidth > 0f ? entry.openWidth : 0.9f, 0.4f, 4f);
            // Keep the opening strictly INSIDE the spanned tile width (tiles are 1m), the same
            // invariant modelled doors get for free (openWidth = bounds.x − 0.04). A full-tile-wide
            // cut emits no jamb, so at a FREE wall end the arch lintel's strips (no x-end cap) would
            // read see-through; the 0.04 inset guarantees a thin jamb box whose cap seals the end.
            openWidth = Mathf.Min(openWidth, footW - 0.04f);
            var holeY1 = Mathf.Clamp(entry.holeY1 > 0f ? entry.holeY1 : 2.1f, 0.5f, Sims4Creator.HomeEditor.DefaultWallHeight);
            var holeY0 = Mathf.Clamp(entry.holeY0, 0f, holeY1 - 0.3f);
            var def = new Sims4Creator.HomeEditor.ItemDef
            {
                id = entry.id,
                label = string.IsNullOrEmpty(entry.label) ? entry.id : entry.label,
                category = string.IsNullOrEmpty(entry.category) ? "Portals" : entry.category,
                wallItem = "portal",
                template = null,
                footW = footW,
                footD = Mathf.Max(1, entry.footD),
                openWidth = openWidth,
                holeY0 = holeY0,
                holeY1 = holeY1,
                liftY = 0f,
            };
            if (entry.cutoutShape == "arch")
            {
                // Half-ellipse arch: straight jambs up to the spring line (70% of height), then a
                // rounded top sweeping right jamb -> apex -> left jamb. Item-local metres, x
                // centered, y from floor. Wound clockwise (right side first) to match the exporter's
                // ModelCutout convention (the runtime clips + strips this the same as a real cutout).
                var hx = openWidth * 0.5f;
                var spring = holeY0 + ((holeY1 - holeY0) * 0.7f);
                var rise = holeY1 - spring;
                var pts = new List<Vector2> { new(hx, holeY0), new(hx, spring) };
                const int seg = 8;
                for (var i = 1; i < seg; i++)
                {
                    var ang = (i / (float)seg) * Mathf.PI; // 0..pi: right jamb -> apex -> left jamb
                    pts.Add(new Vector2(hx * Mathf.Cos(ang), spring + (rise * Mathf.Sin(ang))));
                }
                pts.Add(new Vector2(-hx, spring));
                pts.Add(new Vector2(-hx, holeY0));
                def.cutout = pts;
            }
            Debug.Log($"[HomeEditor] portal '{def.id}': {def.openWidth:0.00}m wide, y {def.holeY0:0.00}-{def.holeY1:0.00}, {(def.cutout.Count >= 3 ? $"arch {def.cutout.Count}pts" : "rectangular")}, span {def.footW} tile(s).");
            return def;
        }

        // One template per item: model instantiated from the export, HDRP materials rebuilt from the
        // exported diffuses, BoxCollider sized to the renderer bounds (click-selection needs it).
        // Also reports the FOOTPRINT in whole tiles (from the bounds) for grid occupancy.
        internal static GameObject BuildTemplate(CatalogEntry entry, Transform parent, out int footW, out int footD, out Bounds bounds)
        {
            footW = 1;
            footD = 1;
            bounds = default;
            var folder = $"{HomeRoot}/{entry.folder}";
            var slug = entry.folder.Replace('\\', '/').Split('/').Last();
            var model = LoadModelAsset(folder, slug, out var sourceTag);
            if (model == null)
            {
                return null;
            }

            var template = Object.Instantiate(model, parent);
            template.name = $"template_{entry.id}";

            // Guard against silent unit mishaps (FBX cm-vs-m) and empty imports: report what we got.
            var probeRenderers = template.GetComponentsInChildren<MeshRenderer>(true);
            if (probeRenderers.Length == 0)
            {
                Debug.LogWarning($"[HomeEditor] item '{entry.id}' ({sourceTag}): imported without renderers — skipped.");
                Object.DestroyImmediate(template);
                return null;
            }
            var probe = probeRenderers[0].bounds;
            foreach (var r in probeRenderers.Skip(1))
            {
                probe.Encapsulate(r.bounds);
            }
            Debug.Log($"[HomeEditor] item '{entry.id}' from .{sourceTag}: {probeRenderers.Length} renderer(s), bounds {probe.size:F2}.");
            if (probe.size.magnitude < 0.05f)
            {
                Debug.LogWarning($"[HomeEditor] item '{entry.id}': bounds are near-zero — the model likely imported at the wrong unit scale.");
            }
            // Footprint in whole tiles, with a small tolerance so a 1.02m-wide chair stays 1x1.
            footW = Mathf.Max(1, Mathf.CeilToInt(probe.size.x - 0.08f));
            footD = Mathf.Max(1, Mathf.CeilToInt(probe.size.z - 0.08f));
            bounds = probe;

            // Rebuild materials as HDRP/Lit. The RIGHT texture per material comes from the exported
            // .mtl, matched to the model's submeshes BY INDEX (submesh i ↔ mtl entry i). Names are
            // useless here — the importer's materialImportMode=None gives every submesh the default
            // material — so name matching silently sent EVERY material to the first-diffuse fallback
            // (uniform texture, no per-material dissolve → opaque glass). Fallback (first diffuse) is
            // only for extra/degenerate slots with no entry.
            var mtlEntries = ParseMtlTextures(folder, slug);
            var texFolder = $"{folder}/Textures";
            var diffuses = AssetDatabase.IsValidFolder(texFolder) // FindAssets warns loudly on missing folders
                ? AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => Path.GetFileName(p).StartsWith("diffuse", System.StringComparison.OrdinalIgnoreCase))
                    .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                    .Where(t => t != null)
                    .ToArray()
                : System.Array.Empty<Texture2D>();
            var fallback = diffuses.FirstOrDefault();
            var totalSlots = template.GetComponentsInChildren<MeshRenderer>(true).Sum(r => r.sharedMaterials.Length);
            if (totalSlots != mtlEntries.Count)
            {
                Debug.LogWarning($"[HomeEditor] item '{entry.id}': {totalSlots} material slot(s) vs {mtlEntries.Count} .mtl entrie(s) — index match may be off; check textures.");
            }
            var matIndex = 0;
            foreach (var renderer in template.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = renderer.sharedMaterials;
                var anyTransparent = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    var slot = matIndex++;                 // global submesh index across renderers
                    var mtl = slot < mtlEntries.Count ? mtlEntries[slot] : null;
                    var mirror = mtl?.Mirror ?? false;
                    Texture2D tex = null;
                    if (mtl is { Path: not null } && !mirror)
                    {
                        tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{mtl.Path}");
                    }
                    else if (mtl == null) // no entry for this slot — legacy fallback
                    {
                        tex = (mats[i] != null ? mats[i].mainTexture as Texture2D : null) ?? fallback;
                    }
                    // mtl with Path == null stays TEXTURELESS on purpose: it is a COLOR-ONLY material
                    // (tinted glass / painted frame) — the fallback texture on glass was the door bug.
                    // The game's UV transform (DX convention, v=0 at TOP) combined with the exporter's
                    // V-flip (OBJ convention) maps to Unity ST as: tiling = scale, offsetY = 1 - Sv - Ov.
                    var scale = mtl?.Scale ?? Vector2.one;
                    var offset = mtl != null
                        ? new Vector2(mtl.Offset.x, 1f - mtl.Scale.y - mtl.Offset.y)
                        : Vector2.zero;
                    var dissolve = mtl?.Dissolve ?? 1f;
                    anyTransparent |= dissolve < 0.999f;
                    mats[i] = BuildItemMaterial(
                        folder, $"{entry.id}_m{slot}", tex, scale, offset,
                        mtl?.Kd ?? Color.white, dissolve, mirror);
                }
                renderer.sharedMaterials = mats;
                if (anyTransparent)
                {
                    // Glass: don't block the sun — a see-through pane must let light through, not
                    // cast a solid shadow. (Glass panes are single-material renderers.)
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            // Selection collider from combined renderer bounds (template sits at origin, identity).
            var renderers = template.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers.Skip(1))
                {
                    b.Encapsulate(r.bounds);
                }
                var col = template.AddComponent<BoxCollider>();
                // Proper world→local: FBX import can bake rotation/scale onto the model root, where
                // a plain position subtraction would misplace the collider.
                col.center = template.transform.InverseTransformPoint(b.center);
                var scale = template.transform.lossyScale;
                var size = new Vector3(
                    b.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                    b.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                    b.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
                col.size = Vector3.Max(size, new Vector3(0.2f, 0.2f, 0.2f));
            }

            // Lamps: split the mesh so ONLY the shade glows + a warm light emits from the bulb (the
            // pole/base stay normal). Lamps live under the "Living" room category now, so match the
            // "lamp" id too (and any "Lighting" category).
            var isLamp = string.Equals(entry.category, "Lighting", System.StringComparison.OrdinalIgnoreCase)
                || (entry.id != null && entry.id.IndexOf("lamp", System.StringComparison.OrdinalIgnoreCase) >= 0);
            if (isLamp)
            {
                SetupLamp(template, folder, entry.id);
            }

            return template;
        }

        // Lamp lighting done right: split the single lamp mesh into BODY (base + pole, normal material)
        // and SHADE (the flared cone at the top), glow ONLY the shade, and add a real warm point light
        // at the bulb so the lamp lights the ROOM. The pole/base stay normal — a flat emissive lit the
        // whole fixture, which the user rejected. HDRP notes: emissive uses _EmissiveColor directly
        // (UseEmissiveIntensity off, else ValidateMaterial recomputes+clobbers it) at weight 0 =
        // exposure-INDEPENDENT (constant in bright OR dark rooms; ~0.18 is mid-grey there, so a value
        // of a few units is a clear glow, 4000 is nuclear).
        private static void SetupLamp(GameObject template, string folder, string id)
        {
            var renderer = template.GetComponentInChildren<MeshRenderer>(true);
            var mf = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            if (renderer == null || mf == null || mf.sharedMesh == null)
            {
                Debug.LogWarning($"[SetupLamp] {id}: no renderer/mesh — skipped.");
                return;
            }
            var src = mf.sharedMesh;

            // Body material = the lamp's currently displayed diffuse (the swatch material if the item is
            // recolourable, else the builder material). Clear any leftover emissive on it.
            var bodyMat = renderer.sharedMaterial;
            if (bodyMat != null)
            {
                bodyMat.SetColor("_EmissiveColor", Color.black);
                HDMaterial.ValidateMaterial(bodyMat);
                EditorUtility.SetDirty(bodyMat);
            }

            // SHADE = triangles that are both HIGH (top ~40%) and WIDE (flared from the vertical axis) —
            // captures the lampshade cone while excluding the thin pole (small radius) and the low base
            // (low Y). Mesh-local coords, so a Y-up import is assumed.
            var verts = src.vertices;
            var lb = src.bounds;
            float minY = lb.min.y, h = Mathf.Max(lb.size.y, 1e-4f), cx = lb.center.x, cz = lb.center.z;
            float maxR = 1e-4f;
            foreach (var v in verts)
            {
                var r = ((v.x - cx) * (v.x - cx)) + ((v.z - cz) * (v.z - cz));
                if (r > maxR)
                {
                    maxR = r;
                }
            }
            maxR = Mathf.Sqrt(maxR);
            // 3-way split by height + radius from the central axis:
            //   SHADE = high AND wide (the flared cone)  → translucent, so light passes through it
            //   BULB  = high AND narrow (the socket/bulb at the centre) → the emissive light SOURCE
            //   BODY  = everything else (pole + base)    → normal
            // This matches a real lamp: the BULB glows, the shade is lit/backlit by it — not the shade
            // glowing while the bulb stays dark.
            float yThresh = minY + (0.55f * h), rThresh = 0.30f * maxR;
            var tris = src.triangles;
            var body = new List<int>();
            var shade = new List<int>();
            var bulb = new List<int>();
            for (var i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i], b2 = tris[i + 1], c = tris[i + 2];
                float my = (verts[a].y + verts[b2].y + verts[c].y) / 3f;
                float mx = (verts[a].x + verts[b2].x + verts[c].x) / 3f;
                float mz = (verts[a].z + verts[b2].z + verts[c].z) / 3f;
                float mr = Mathf.Sqrt(((mx - cx) * (mx - cx)) + ((mz - cz) * (mz - cz)));
                var bucket = my <= yThresh ? body : (mr > rThresh ? shade : bulb);
                bucket.Add(a);
                bucket.Add(b2);
                bucket.Add(c);
            }

            // Shade = translucent so light passes through it like a real lampshade (a copy of the body
            // keeps its fabric colour/texture). Not emissive itself — it reads as "lit" because the bulb
            // glows behind it and the point light shines through.
            var shadeMat = bodyMat != null ? new Material(bodyMat) : new Material(Shader.Find("HDRP/Lit"));
            shadeMat.name = $"{id}_shade";
            shadeMat.SetFloat("_SurfaceType", 1f);   // Transparent
            shadeMat.SetFloat("_Smoothness", 0.3f);
            var shadeBase = shadeMat.GetColor("_BaseColor");
            shadeMat.SetColor("_BaseColor", new Color(shadeBase.r, shadeBase.g, shadeBase.b, 0.55f));
            HDMaterial.ValidateMaterial(shadeMat);
            shadeMat = SaveMat(shadeMat, $"{folder}/{id}_shade.mat");

            // Bulb = the emissive light SOURCE: a warm, bright, exposure-independent glow (weight 0).
            var bulbMat = new Material(Shader.Find("HDRP/Lit")) { name = $"{id}_bulb" };
            bulbMat.SetColor("_BaseColor", new Color(1f, 0.92f, 0.78f, 1f));
            bulbMat.SetFloat("_UseEmissiveIntensity", 0f);
            bulbMat.SetColor("_EmissiveColor", new Color(1f, 0.72f, 0.42f) * 10f);
            bulbMat.SetFloat("_EmissiveExposureWeight", 0f);
            HDMaterial.ValidateMaterial(bulbMat);
            bulbMat = SaveMat(bulbMat, $"{folder}/{id}_bulb.mat");

            // Rebuild the mesh with three submeshes [body, shade, bulb] and save it as an asset.
            var mesh = Object.Instantiate(src);
            mesh.name = $"{id}_lampsplit";
            mesh.subMeshCount = 3;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(shade, 1);
            mesh.SetTriangles(bulb, 2);
            var meshPath = $"{folder}/{id}_lampsplit.asset";
            mesh = Persist(mesh, meshPath);
            mf.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { bodyMat, shadeMat, bulbMat };

            // The [ExecuteAlways] Sims4Swatches switcher force-assigns ONE material to EVERY slot, which
            // would overwrite the shade/bulb materials — so recolourable lamps drop swatch switching.
            var sw = template.GetComponentInChildren<Sims4Creator.Sims4Swatches>(true);
            if (sw != null)
            {
                Object.DestroyImmediate(sw);
            }

            // Warm point light at the bulb so the lamp lights the ROOM. Visible in shadowed interiors;
            // bright daylight washes it out (physically correct). Shadows off so the shade doesn't
            // self-occlude the glow (light passes through the translucent shade anyway).
            var lightGo = new GameObject("LampLight");
            lightGo.transform.SetParent(template.transform, false);
            lightGo.transform.position = renderer.transform.TransformPoint(new Vector3(cx, minY + (0.80f * h), cz));
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            var hd = lightGo.AddComponent<HDAdditionalLightData>();
            HDAdditionalLightData.InitDefaultHDAdditionalLightData(hd);
            light.color = new Color(1f, 0.86f, 0.62f); // warm ~2700K
            light.range = 8f;
            light.shadows = LightShadows.None;
            light.lightUnit = LightUnit.Lumen; // SetIntensity(unit) is obsolete since 2023.3
            light.intensity = 3000f;

            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupLamp] {id}: body {body.Count / 3} / shade {shade.Count / 3} / bulb {bulb.Count / 3} tris, " +
                      $"maxR {maxR:0.00}, split y {yThresh:0.00}");
        }

        // Persist WITHOUT changing the asset's GUID. The item-folder assets (materials, split meshes)
        // are shared by BOTH built scenes (HomeEditor.unity and the game scene) — delete+create mints a
        // new GUID and silently breaks every reference in whichever scene was built earlier (pink
        // materials, invisible lamp meshes). Overwrite-in-place keeps old references valid. Returns the
        // PERSISTED instance — callers must use it; the temp copy is destroyed.
        internal static T Persist<T>(T temp, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(temp, path);
                return temp;
            }
            EditorUtility.CopySerialized(temp, existing);
            Object.DestroyImmediate(temp);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material SaveMat(Material m, string path) => Persist(m, path);

        // A true mirror: a realtime PLANAR reflection probe aligned to the mirror surface renders the
        // room from the mirror's viewpoint each frame, so the smooth metallic mirror material reflects
        // the actual room (not the murky screen-space reflection SSR gives a face-on mirror). The plane
        // is taken from the mirror submesh's centroid + average normal; HDRP's planar probe reflects
        // across its transform.forward, so forward is aimed along the mirror's outward normal.
        private static void SetupMirror(GameObject template, MeshRenderer renderer, int mirrorSubmesh)
        {
            var mf = renderer.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mirrorSubmesh < 0 || mirrorSubmesh >= mf.sharedMesh.subMeshCount)
            {
                Debug.LogWarning($"[SetupMirror] {template.name}: no mirror submesh — skipped.");
                return;
            }
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var subTris = mesh.GetTriangles(mirrorSubmesh);
            var centroid = Vector3.zero;
            var nrm = Vector3.zero;
            var seen = new HashSet<int>();
            foreach (var idx in subTris)
            {
                if (!seen.Add(idx))
                {
                    continue;
                }
                centroid += verts[idx];
                if (normals != null && idx < normals.Length)
                {
                    nrm += normals[idx];
                }
            }
            if (seen.Count == 0)
            {
                return;
            }
            centroid /= seen.Count;
            nrm = nrm.sqrMagnitude > 1e-6f ? nrm.normalized : Vector3.forward;
            var worldCentroid = renderer.transform.TransformPoint(centroid);
            var worldNormal = renderer.transform.TransformDirection(nrm).normalized;

            var probeGo = new GameObject("VanityMirrorProbe");
            probeGo.transform.SetParent(template.transform, false);
            probeGo.transform.position = worldCentroid;
            probeGo.transform.rotation = Quaternion.LookRotation(worldNormal, Vector3.up); // forward = mirror normal
            var probe = probeGo.AddComponent<PlanarReflectionProbe>();
            probe.mode = ProbeSettings.Mode.Realtime;
            probe.realtimeMode = ProbeSettings.RealtimeMode.EveryFrame;
            probe.influenceVolume.shape = InfluenceShape.Box;
            probe.influenceVolume.boxSize = new Vector3(6f, 6f, 6f);
            Debug.Log($"[SetupMirror] {template.name}: probe at {worldCentroid:F2} normal {worldNormal:F2} " +
                      $"({seen.Count} mirror verts)");
        }

        private sealed class MtlEntry
        {
            public string Path;                    // null = COLOR-ONLY material (tinted glass, frame paint)
            public Vector2 Scale = Vector2.one;    // -s option: the game material's UV scale
            public Vector2 Offset = Vector2.zero;  // -o option: the game material's UV offset (DX convention)
            public Color Kd = new(0.8f, 0.8f, 0.8f, 1f);
            public float Dissolve = 1f;            // 'd' statement: < 1 = transparent (glass)
            public bool Mirror;                    // 'illum 3' = reflective mirror surface
        }

        // The exported .mtl materials IN FILE ORDER (one MtlEntry per newmtl). We match them to the
        // model's submeshes BY INDEX, not by name: the importer runs with materialImportMode=None,
        // so every submesh wears the default material and material NAMES are unusable. The exporter
        // writes newmtl in material order and the OBJ groups faces (submeshes) in that same order,
        // so submesh i ↔ entry i. map_Kd may carry "-s su sv 1 -o ou ov 0" options.
        private static List<MtlEntry> ParseMtlTextures(string folder, string slug)
        {
            var list = new List<MtlEntry>();
            var mtlPath = $"{folder}/{slug}.mtl";
            if (!File.Exists(mtlPath))
            {
                return list;
            }
            MtlEntry current = null;
            foreach (var raw in File.ReadAllLines(mtlPath))
            {
                var line = raw.Trim();
                if (line.StartsWith("newmtl ", System.StringComparison.OrdinalIgnoreCase))
                {
                    current = new MtlEntry(); // EVERY material gets an entry — color-only ones included
                    list.Add(current);
                }
                else if (current == null)
                {
                    continue;
                }
                else if (line.StartsWith("Kd ", System.StringComparison.Ordinal))
                {
                    var t = line.Substring(3).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                    if (t.Length >= 3)
                    {
                        current.Kd = new Color(ParseF(t[0]), ParseF(t[1]), ParseF(t[2]), 1f);
                    }
                }
                else if (line.StartsWith("d ", System.StringComparison.Ordinal))
                {
                    current.Dissolve = Mathf.Clamp01(ParseF(line.Substring(2).Trim()));
                }
                else if (line.StartsWith("illum ", System.StringComparison.Ordinal))
                {
                    current.Mirror = ParseF(line.Substring(6).Trim()) >= 3f; // 3 = reflection on
                }
                else if (line.StartsWith("map_Kd ", System.StringComparison.OrdinalIgnoreCase))
                {
                    var tokens = line.Substring(7).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                    for (var i = 0; i < tokens.Length; i++)
                    {
                        if (tokens[i] == "-s" && i + 3 < tokens.Length)
                        {
                            current.Scale = new Vector2(ParseF(tokens[i + 1]), ParseF(tokens[i + 2]));
                            i += 3;
                        }
                        else if (tokens[i] == "-o" && i + 3 < tokens.Length)
                        {
                            current.Offset = new Vector2(ParseF(tokens[i + 1]), ParseF(tokens[i + 2]));
                            i += 3;
                        }
                        else
                        {
                            current.Path = tokens[i].Replace('\\', '/'); // last plain token = the file path
                        }
                    }
                }
            }
            return list;
        }

        private static float ParseF(string s) =>
            float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;

        // Prefer the OBJ: it is OUR writer (guaranteed metre units, always emitted). The Assimp FBX
        // declares centimetre units (Unity then shrinks everything 100×) and can import with zero
        // meshes while still returning a loadable GameObject — both read as "nothing appears".
        private static GameObject LoadModelAsset(string folder, string slug, out string sourceTag)
        {
            foreach (var (path, tag) in new[] { ($"{folder}/{slug}.obj", "obj"), ($"{folder}/{slug}.fbx", "fbx") })
            {
                if (!File.Exists(path))
                {
                    continue;
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); // exports may have landed while Unity was closed
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    continue;
                }
                var hasMesh = go.GetComponentsInChildren<MeshFilter>(true)
                    .Any(f => f.sharedMesh != null && f.sharedMesh.vertexCount > 0);
                if (!hasMesh)
                {
                    Debug.LogWarning($"[HomeEditor] {path} imported without meshes; trying the next format.");
                    continue;
                }
                sourceTag = tag;
                return go;
            }
            sourceTag = null;
            return null;
        }

        private static Material BuildItemMaterial(string folder, string name, Texture2D diffuse, Vector2 uvScale, Vector2 uvOffset, Color baseColor, float dissolve, bool mirror = false)
        {
            var m = new Material(Shader.Find("HDRP/Lit")) { name = name };
            if (mirror)
            {
                // Reflection dropped (per user: the SSR/planar reflections looked like "dirt and broken
                // image"). Render it as a clean DARK GLASS instead: a dark dielectric with a moderate
                // sheen. Smoothness 0.6 is below HDRP's SSR threshold (0.9) so it gets no noisy SSR —
                // just a subtle sky Fresnel, which reads as an unobtrusive dark mirror surface.
                m.SetColor("_BaseColor", new Color(0.11f, 0.12f, 0.14f, 1f));
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.6f);
                HDMaterial.ValidateMaterial(m);
                return Persist(m, $"{folder}/{name}.mat");
            }
            if (diffuse != null)
            {
                m.SetTexture("_BaseColorMap", diffuse);
                m.SetTextureScale("_BaseColorMap", uvScale);
                m.SetTextureOffset("_BaseColorMap", uvOffset);
                // Alpha carries the dissolve so a TEXTURED transparent material (tinted glass with a
                // pattern) actually fades — HDRP multiplies baseMap.a × _BaseColor.a.
                m.SetColor("_BaseColor", new Color(1f, 1f, 1f, dissolve));
            }
            else
            {
                m.SetColor("_BaseColor", new Color(baseColor.r, baseColor.g, baseColor.b, dissolve));
            }
            if (dissolve < 0.999f) // glass and other transparent surfaces
            {
                m.SetFloat("_SurfaceType", 1f);
                m.SetFloat("_Smoothness", 0.85f);
            }
            else
            {
                m.SetFloat("_Smoothness", 0.25f);
            }
            HDMaterial.ValidateMaterial(m);
            return Persist(m, $"{folder}/{name}.mat");
        }

        // Swatch recolours from the export's swatches.json (Swatches/N/diffuse.png per colour).
        // primaryTexture = the texture actually assigned to the template's first textured material,
        // so the runtime swap can match by REFERENCE (the swatch-0 PNG is a different asset even
        // when its pixels equal the primary diffuse).
        private static void WireSwatches(Sims4Creator.HomeEditor.ItemDef def, string itemFolder, GameObject template)
        {
            foreach (var renderer in template.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    var tex = mat != null && mat.HasProperty("_BaseColorMap") ? mat.GetTexture("_BaseColorMap") as Texture2D : null;
                    if (tex != null)
                    {
                        def.primaryTexture = tex;
                        break;
                    }
                }
                if (def.primaryTexture != null)
                {
                    break;
                }
            }

            var swatchesPath = $"{itemFolder}/swatches.json";
            if (!File.Exists(swatchesPath))
            {
                return;
            }
            var file = JsonUtility.FromJson<SwatchesFile>(File.ReadAllText(swatchesPath));
            if (file?.swatches == null)
            {
                return;
            }
            foreach (var sw in file.swatches)
            {
                if (string.IsNullOrEmpty(sw.diffuse))
                {
                    continue;
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{itemFolder}/{sw.diffuse.Replace('\\', '/')}");
                if (tex == null)
                {
                    continue;
                }
                def.swatches.Add(new Sims4Creator.HomeEditor.SwatchDef
                {
                    label = string.IsNullOrEmpty(sw.label) ? $"Swatch {sw.index}" : sw.label,
                    diffuse = tex,
                });
            }
        }

        private static List<Sims4Creator.HomeEditor.CoveringDef> BuildCoverings(List<CoveringJson> list, string kind, float smoothness)
        {
            var defs = new List<Sims4Creator.HomeEditor.CoveringDef>();
            if (list == null)
            {
                return defs;
            }
            foreach (var c in list)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HomeRoot}/{c.texture}");
                if (tex == null)
                {
                    continue;
                }
                var m = new Material(Shader.Find("HDRP/Lit")) { name = $"cov_{kind}_{c.id}" };
                m.SetTexture("_BaseColorMap", tex);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", smoothness);
                HDMaterial.ValidateMaterial(m);
                var matPath = $"{HomeRoot}/coverings/cov_{kind}_{c.id}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                {
                    AssetDatabase.DeleteAsset(matPath);
                }
                AssetDatabase.CreateAsset(m, matPath);
                defs.Add(new Sims4Creator.HomeEditor.CoveringDef
                {
                    id = c.id,
                    label = string.IsNullOrEmpty(c.label) ? c.id : c.label,
                    material = m,
                    thumb = string.IsNullOrEmpty(c.thumb) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{HomeRoot}/{c.thumb}"),
                });
            }
            return defs;
        }

        private static Material BuildPlainMaterial(string name, Color color)
        {
            var m = new Material(Shader.Find("HDRP/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.1f);
            HDMaterial.ValidateMaterial(m);
            var path = $"{HomeRoot}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // Translucent overlay quads for the placement footprint (green = free, red = blocked).
        private static Material BuildCellMaterial(string name, Color color)
        {
            var m = new Material(Shader.Find("HDRP/Unlit")) { name = name };
            m.SetFloat("_SurfaceType", 1f); // transparent
            m.SetColor("_UnlitColor", color);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_TransparentZWrite", 0f);
            HDMaterial.ValidateMaterial(m);
            var path = $"{HomeRoot}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material BuildGridMaterial()
        {
            // 64px tile texture: dark floor with a light 1px border → repeats once per metre.
            var texPath = $"{HomeRoot}/grid.png";
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var bg = new Color32(74, 76, 80, 255);
            var line = new Color32(110, 114, 120, 255);
            var px = new Color32[64 * 64];
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    px[(y * 64) + x] = (x == 0 || y == 0) ? line : bg;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            Directory.CreateDirectory(HomeRoot);
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(texPath) is TextureImporter imp)
            {
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.mipmapEnabled = true;
                imp.anisoLevel = 8; // 1px grid lines vanish into mips at grazing angles without aniso
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            }

            var m = new Material(Shader.Find("HDRP/Lit")) { name = "HomeGround" };
            m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetTextureScale("_BaseColorMap", new Vector2(24f, 24f)); // one grid cell per metre on the 24m plane
            m.SetFloat("_Smoothness", 0.12f);
            HDMaterial.ValidateMaterial(m);
            var matPath = $"{HomeRoot}/HomeGround.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
            {
                AssetDatabase.DeleteAsset(matPath);
            }
            AssetDatabase.CreateAsset(m, matPath);
            return m;
        }
    }
}
