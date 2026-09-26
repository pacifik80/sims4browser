using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition; // HDMaterial (HDRP Runtime asm — the Editor asm isn't referenced)
using Sims4Creator.Game;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Bakes the REAL Buy catalogue into the game scene: reads home_catalog.json + the exported per-item
    /// folders (Assets/Sims4/home/&lt;folder&gt;), builds one inactive furniture template per item via the
    /// home-editor's proven <see cref="HomeEditorSceneBuilder.BuildTemplate"/> (real mesh,
    /// real HDRP materials, BoxCollider), loads the game's own BuyBuildThumbnail + baked swatch diffuses,
    /// and serializes everything onto a <see cref="GameCatalog"/> component. Editor-only construction,
    /// fully runtime-safe result — runtime code only ever clones templates.
    ///
    /// Wall items (doors/windows/portals) are skipped here — they are build-STRUCTURE tools (TASK-011).
    /// </summary>
    internal static class GameCatalogBaker
    {
        private const string HomeRoot = "Assets/Sims4/home";
        private const string GameFolder = "Assets/Game";

        [System.Serializable]
        private sealed class SwatchesFile { public int defaultIndex; public List<SwatchJson> swatches = new(); }

        [System.Serializable]
        private sealed class SwatchJson { public int index; public string label; public bool isDefault; public string diffuse; }

        /// <summary>Build the catalog GO (+ inactive templates root) in the open scene and return it.</summary>
        internal static GameCatalog Bake()
        {
            var catalogGo = new GameObject("Game Catalog");
            var catalog = catalogGo.AddComponent<GameCatalog>();

            var templatesRoot = new GameObject("Catalog Templates");
            templatesRoot.transform.SetParent(catalogGo.transform, false);

            var jsonPath = $"{HomeRoot}/home_catalog.json";
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[Sims4 Game] {jsonPath} not found — the Buy catalogue will be empty. Run the home export first.");
            }
            else
            {
                var file = JsonUtility.FromJson<HomeEditorSceneBuilder.CatalogFile>(File.ReadAllText(jsonPath));
                int built = 0, skipped = 0;
                foreach (var entry in file?.items ?? new List<HomeEditorSceneBuilder.CatalogEntry>())
                {
                    if (!string.IsNullOrEmpty(entry.wallItem)) continue; // doors/windows/portals → TASK-011

                    var template = HomeEditorSceneBuilder.BuildTemplate(
                        entry, templatesRoot.transform, out _, out _, out var bounds);
                    if (template == null)
                    {
                        Debug.LogWarning($"[Sims4 Game] catalog item '{entry.id}': no importable model — skipped.");
                        skipped++;
                        continue;
                    }

                    // The import postprocessor attaches an [ExecuteAlways] Sims4Swatches to multi-swatch
                    // models; on every clone its OnEnable force-assigns ONE material to every renderer
                    // slot, clobbering the per-slot builder materials AND breaking texture-match recolour.
                    // The game recolours via SmartObject.Recolor instead — strip it (as SetupLamp does).
                    foreach (var sw in template.GetComponentsInChildren<Sims4Creator.Sims4Swatches>(true))
                        Object.DestroyImmediate(sw);

                    var itemFolder = $"{HomeRoot}/{entry.folder}";
                    var def = new BuildableDef
                    {
                        id = entry.id,
                        name = string.IsNullOrEmpty(entry.label) ? entry.id : entry.label,
                        category = FunctionOf(entry.id),
                        room = string.IsNullOrEmpty(entry.category) ? "Living" : entry.category,
                        price = PriceOf(entry.id),
                        size = bounds.size,
                        template = template,
                        thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>($"{itemFolder}/thumb.png")
                                    ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{itemFolder}/thumb.jpg"),
                    };

                    // Footprint in MAIN grid tiles (1 m — decision D-106, same rule as the home editor),
                    // so game furniture and future walls (TASK-011) agree on one grid.
                    def.footW = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x - 0.08f));
                    def.footD = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z - 0.08f));

                    WireFunction(def);
                    WirePrimaryTexture(def, template);
                    WireSwatches(def, itemFolder);
                    catalog.items.Add(def);
                    built++;
                }
                Debug.Log($"[Sims4 Game] Buy catalogue: {built} real item(s) baked, {skipped} skipped.");
            }

            catalog.cellFreeMaterial = CellMaterial("GameCellFree", new Color(0.2f, 0.95f, 0.35f, 0.35f));
            catalog.cellBlockedMaterial = CellMaterial("GameCellBlocked", new Color(1f, 0.25f, 0.2f, 0.45f));

            templatesRoot.SetActive(false); // templates never render; the factory clones them
            return catalog;
        }

        // ---- per-item wiring ---------------------------------------------------------------------

        /// <summary>First _BaseColorMap texture in the template = the item's primary diffuse. Swatches
        /// replace exactly this texture on placed clones (the home editor's ApplySwatch contract).</summary>
        private static void WirePrimaryTexture(BuildableDef def, GameObject template)
        {
            foreach (var renderer in template.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    var tex = mat != null && mat.HasProperty("_BaseColorMap") ? mat.GetTexture("_BaseColorMap") as Texture2D : null;
                    if (tex != null) { def.primaryTexture = tex; return; }
                }
            }
        }

        private static void WireSwatches(BuildableDef def, string itemFolder)
        {
            var path = $"{itemFolder}/swatches.json";
            if (!File.Exists(path)) return;
            var file = JsonUtility.FromJson<SwatchesFile>(File.ReadAllText(path));
            if (file?.swatches == null) return;
            foreach (var sw in file.swatches)
            {
                if (string.IsNullOrEmpty(sw.diffuse)) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{itemFolder}/{sw.diffuse.Replace('\\', '/')}");
                if (tex == null) continue;
                def.swatches.Add(new Swatch
                {
                    name = string.IsNullOrEmpty(sw.label) ? $"Swatch {sw.index}" : sw.label,
                    diffuse = tex,
                });
            }
        }

        /// <summary>The sim-side function: what the item advertises to the utility AI, and whether it is
        /// exclusive. Keyed by catalog id; unknown items are pure décor (no ads).</summary>
        private static void WireFunction(BuildableDef def)
        {
            InteractionAdvertisement Ad(string label, string need, float perHour, float hours) =>
                new InteractionAdvertisement { label = label, needId = need, satisfyPerHour = perHour, durationHours = hours };

            switch (def.id)
            {
                case "bed_double": def.exclusive = true; def.ads.Add(Ad("Sleep", "energy", 55f, 2f)); break;
                case "bed_single": def.exclusive = true; def.ads.Add(Ad("Sleep", "energy", 50f, 2f)); break;
                case "fridge": def.exclusive = true; def.ads.Add(Ad("Eat", "hunger", 80f, 0.8f)); break;
                case "toilet": def.exclusive = true; def.ads.Add(Ad("Use toilet", "bladder", 200f, 0.4f)); break;
                case "shower": def.exclusive = true; def.ads.Add(Ad("Shower", "hygiene", 110f, 0.5f)); break;
                case "bathtub": def.exclusive = true; def.ads.Add(Ad("Bathe", "hygiene", 90f, 0.8f)); break;
                case "bath_sink":
                case "kitchen_sink": def.exclusive = false; def.ads.Add(Ad("Wash up", "hygiene", 60f, 0.3f)); break;
                case "tv": def.exclusive = false; def.ads.Add(Ad("Watch TV", "fun", 45f, 1.2f)); break;
                case "sofa":
                    def.exclusive = false;
                    def.ads.Add(Ad("Relax", "social", 50f, 1f));
                    def.ads.Add(Ad("Lounge", "fun", 25f, 1.5f));
                    break;
                case "bench":
                case "ottoman": def.exclusive = false; def.ads.Add(Ad("Relax", "social", 40f, 1f)); break;
                case "bookcase": def.exclusive = false; def.ads.Add(Ad("Read", "fun", 35f, 1f)); break;
                default: def.exclusive = false; break; // décor: placeable, no sim function yet
            }
        }

        private static string FunctionOf(string id)
        {
            switch (id)
            {
                case "sofa": case "bench": case "chair_dining": case "bar_stool": case "ottoman":
                    return "Seating";
                case "table_dining": case "table_coffee": case "kitchen_table_sm": case "makeup_table":
                    return "Surfaces";
                case "bed_double": case "bed_single":
                    return "Beds";
                case "fridge":
                    return "Appliances";
                case "toilet": case "shower": case "bathtub": case "bath_sink": case "kitchen_sink":
                    return "Plumbing";
                case "lamp_floor": case "table_lamp":
                    return "Lighting";
                case "tv":
                    return "Electronics";
                case "bookcase": case "wardrobe": case "clothes_bin":
                    return "Storage";
                default:
                    return "Decor";
            }
        }

        /// <summary>Hand-authored prices — real §prices aren't in the export yet (see TASK-004 notes).</summary>
        private static int PriceOf(string id)
        {
            switch (id)
            {
                case "sofa": return 450;
                case "bench": return 180;
                case "chair_dining": return 90;
                case "bar_stool": return 70;
                case "ottoman": return 90;
                case "table_dining": return 260;
                case "table_coffee": return 150;
                case "kitchen_table_sm": return 160;
                case "makeup_table": return 280;
                case "bed_double": return 600;
                case "bed_single": return 350;
                case "fridge": return 800;
                case "toilet": return 200;
                case "shower": return 650;
                case "bathtub": return 700;
                case "bath_sink": return 130;
                case "kitchen_sink": return 150;
                case "lamp_floor": return 120;
                case "table_lamp": return 60;
                case "tv": return 500;
                case "bookcase": return 240;
                case "wardrobe": return 320;
                case "clothes_bin": return 50;
                default: return 100;
            }
        }

        // ---- cell/overlay materials (HDRP/Unlit transparent — same recipe as the home editor).
        // Internal: GameSceneBuilder also bakes the grid-debug palette with it. ----

        internal static Material CellMaterial(string name, Color color)
        {
            var m = new Material(Shader.Find("HDRP/Unlit")) { name = name };
            m.SetFloat("_SurfaceType", 1f); // transparent
            m.SetColor("_UnlitColor", color);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_TransparentZWrite", 0f);
            HDMaterial.ValidateMaterial(m);
            if (!AssetDatabase.IsValidFolder(GameFolder)) AssetDatabase.CreateFolder("Assets", "Game");
            return HomeEditorSceneBuilder.Persist(m, $"{GameFolder}/{name}.mat"); // GUID-stable overwrite
        }
    }
}
