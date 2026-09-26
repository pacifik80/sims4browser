using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UIElements;
using Sims4Creator.Game;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Builds the M0 foundation scene: a ground lot, a framing camera, a day/night clock, a couple of
    /// existing characters as Sims, the simulation director, and the debug HUD. Mirrors the structure of
    /// <c>Sims4CasSceneBuilder</c>. Press Play in the generated scene: needs decay on the HUD and the
    /// speed buttons control time. No AI yet — this proves soul/body + time + needs (M0).
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string Scene = "Assets/Scenes/GameM0.unity";
        private const string GameFolder = "Assets/Game";
        private const string NeedsFolder = "Assets/Game/Needs";
        private static readonly Vector3 LotOrigin = new Vector3(-15f, 0f, -15f); // (0,0) tile corner

        [MenuItem("Sims4 Creator/Game/Build M0 Scene", priority = 40)]
        public static void BuildM0()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            TryEnableSsgiSupport(); // SSGI volume override is inert unless the HDRP asset supports it

            var needs = EnsureNeedDefinitions();

            // DefaultGameObjects gives a Main Camera + Directional Light that HDRP lights via its default
            // global volume — the scene renders lit without hand-built HDRP data (same as the CAS scene).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Lot Ground";
            ground.transform.localScale = new Vector3(3f, 1f, 3f);

            // Day/night: hang a SkyTimeController off the default directional light; the GameClock drives it.
            var sun = Object.FindFirstObjectByType<Light>();
            SkyTimeController sky = null;
            HDAdditionalLightData sunHd = null;
            if (sun != null)
            {
                sun.gameObject.name = "Sun";
                sun.shadows = LightShadows.Soft;
                // Crisp shadows: the default sun disk is large (mushy penumbra) and HDRP's default shadow
                // resolution is low. A small angular diameter + a 4096 map fixes the "blurry/distorted" look.
                if (!sun.TryGetComponent(out sunHd)) sunHd = sun.gameObject.AddComponent<HDAdditionalLightData>();
                sunHd.angularDiameter = 0.5f;
                sunHd.shadowResolution.useOverride = true;
                sunHd.shadowResolution.@override = 4096;
                sunHd.useContactShadow.useOverride = true;
                sunHd.useContactShadow.@override = false; // screen-space contact shadows band on close geometry
                sunHd.interactsWithSky = true;            // sun disk + sky colour track the light
                sunHd.shadowDimmer = 0.8f;                // slight sun-shadow lift (home-editor-proven; debug-tunable)

                var skyGo = new GameObject("SkyTime");
                sky = skyGo.AddComponent<SkyTimeController>();
                sky.sun = sun;
                sky.timeOfDay = 12f;
                sky.autoAdvance = false;
                sky.showPanel = false; // the game HUD owns the on-screen controls
            }

            // Moon so nights are moonlit, not void (SkyTimeController drives it anti-solar).
            var moonGo = new GameObject("Moon");
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            var moonHd = moonGo.AddComponent<HDAdditionalLightData>();
            HDAdditionalLightData.InitDefaultHDAdditionalLightData(moonHd);
            moon.color = new Color(0.55f, 0.66f, 0.95f);
            moon.shadows = LightShadows.Soft;
            moonHd.angularDiameter = 0.5f;
            moonHd.interactsWithSky = true;
            moonHd.surfaceTint = new Color(0.85f, 0.87f, 0.95f); // moon disk, not a second sun
            if (sky != null)
            {
                sky.moon = moon;
                // GAME night tuning (home editor keeps its own 25/3 defaults): the 25-lux moon also gets
                // Rayleigh-scattered by the PBR sky into a day-blue night — 6 lux @ EV 3 reads as real
                // night (~9 % of day brightness). Debug-menu tunable before baking (TASK-016).
                sky.moonMaxLux = 6f;
                sky.nightEv = 3f;
            }

            // Shadowless FILL light — an artistic shadow-filler, OFF by default; the debug menu's render
            // options toggle it live (TASK-016: tune before we bake).
            var fillGo = new GameObject("Fill Light");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            var fillHd = fillGo.AddComponent<HDAdditionalLightData>();
            HDAdditionalLightData.InitDefaultHDAdditionalLightData(fillHd);
            fillGo.transform.rotation = Quaternion.Euler(45f, 210f, 0f); // roughly opposite the sun
            fill.shadows = LightShadows.None;
            fillHd.interactsWithSky = false;
            fill.lightUnit = LightUnit.Lux;
            fill.intensity = 4000f;
            fillGo.SetActive(false);

            // The lighting rig (TASK-016) — the game-scene half of what the home editor proved:
            //  • PBR sky = real blue-sky AMBIENT that fills shadows and tracks time of day (the missing
            //    piece behind "shadows are black holes").
            //  • FIXED exposure driven per time-of-day by SkyTimeController (auto-exposure meters for the
            //    sunlit ground and crushes the rest; it also can't span day→night).
            //  • SSGI = screen-space bounce light (debug-menu-toggleable).
            //  • Short shadow distance concentrates the 4096 map on the lot (crisp), split across cascades.
            //  • IndirectLightingController / Tonemapping / SMH exist as debug-menu TUNABLES — values get
            //    baked here once the user settles them (edits go to this asset, so tuning persists).
            const string GameVolumeProfile = "Assets/Scenes/GameVolumeProfile.asset";
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(GameVolumeProfile) != null) AssetDatabase.DeleteAsset(GameVolumeProfile);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GameVolumeProfile);
            T AddOverride<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>();
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var shadowSettings = AddOverride<HDShadowSettings>();
            shadowSettings.maxShadowDistance.Override(45f);       // covers the whole visible lot + cast shadows
            shadowSettings.cascadeShadowSplitCount.Override(4);   // distribute the 4096 map across the range
            var env = AddOverride<VisualEnvironment>();
            env.skyType.Override((int)SkyType.PhysicallyBased);
            // Default PBR groundTint is a dark brown planet surface — a cool neutral keeps ambient neutral.
            var pbs = AddOverride<PhysicallyBasedSky>();
            pbs.groundTint.Override(new Color(0.16f, 0.18f, 0.19f));
            var exposure = AddOverride<Exposure>();
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(13f);                 // SkyTimeController drives it per time of day
            var gi = AddOverride<GlobalIllumination>();
            gi.enable.Override(true);                             // SSGI on by default — judge it, toggle in DBG
            var indirectCtl = AddOverride<IndirectLightingController>();
            indirectCtl.indirectDiffuseLightingMultiplier.Override(1f);
            var tonemap = AddOverride<Tonemapping>();
            tonemap.mode.Override(TonemappingMode.Neutral);
            AddOverride<ShadowsMidtonesHighlights>();             // grade shadow-lift lives here (DBG slider)
            AssetDatabase.SaveAssets();

            if (sky != null) sky.exposure = exposure; // day bright / night dark, deterministic

            var volGo = new GameObject("Game Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
            vol.sharedProfile = profile; // shared: the debug menu's tuning writes THIS asset (bake-ready)

            var clockGo = new GameObject("GameClock");
            var clock = clockGo.AddComponent<GameClock>();
            clock.sky = sky;
            clock.timeOfDay = 8f;
            clock.speedIndex = 1;

            var placed = new List<string>();
            var adam = TryBuildSim("am_char", "Male", new Vector3(-1.2f, 0f, 0.4f), "Adam", placed);
            var eve = TryBuildSim("af_char", "Female", new Vector3(1.2f, 0f, 0.4f), "Eve", placed);

            // Two Sims only (Adam + Eve) while we build out locomotion/navigation — easier to read what
            // each one is doing. CloneSim() stays available for repopulating the lot later.

            // REAL Buy catalogue: baked furniture templates + thumbnails + swatches from the game's own
            // Build/Buy exports (Assets/Sims4/home). Runtime clones templates; nothing loads at runtime.
            var catalog = GameCatalogBaker.Bake();

            BuildLot(catalog); // starter furniture — the SAME real items build mode places (M1)

            // THE lot grid (D-106): 1 m MAIN tiles (placement/occupancy/interaction authority) over a
            // 30×30 m lot, with 0.5 m nav sub-cells for routing (see docs/game/systems/
            // locomotion-navigation.md).
            var gridGo = new GameObject("Lot Grid");
            var lotGrid = gridGo.AddComponent<LotGrid>();
            lotGrid.tileSize = 1f;
            lotGrid.navSubdivision = 2;
            lotGrid.origin = LotOrigin;
            lotGrid.tilesX = 30;
            lotGrid.tilesZ = 30;

            var dirGo = new GameObject("SimulationDirector");
            var dir = dirGo.AddComponent<SimulationDirector>();
            dir.clock = clock;
            dir.needs = needs;

            var playerGo = new GameObject("PlayerController");
            var player = playerGo.AddComponent<PlayerController>();
            player.director = dir; // camera resolved to Camera.main at play start

            // NOTE: the old IMGUI GameDebugHud is intentionally NOT added any more — the UI Toolkit
            // shell below replaces it (decision D-105). GameCasMode's `hud` field stays null (it
            // null-guards it); GameHudUI hides itself while a modal mode is open.

            var casModeGo = new GameObject("Game CAS Mode");
            var casMode = casModeGo.AddComponent<GameCasMode>();
            casMode.player = player;
            casMode.clock = clock; // camera resolved to Camera.main at play start

            var buildModeGo = new GameObject("Game Build Mode");
            var buildMode = buildModeGo.AddComponent<GameBuildMode>();
            buildMode.player = player;
            buildMode.clock = clock;
            buildMode.catalog = catalog;
            buildMode.gridOverlay = BuildGridOverlay();

            // The DEBUG stack: world grid visuals + render tunables + the UI Toolkit debug menu (DBG/F3).
            var debugGo = new GameObject("Debug");
            var gridDebug = debugGo.AddComponent<GridDebugOverlay>();
            gridDebug.grid = lotGrid;
            gridDebug.director = dir;
            gridDebug.build = buildMode;
            gridDebug.gridOverlay = buildMode.gridOverlay;
            gridDebug.matOccupied = GameCatalogBaker.CellMaterial("DebugOccupied", new Color(1f, 0.62f, 0.15f, 0.35f));
            gridDebug.matSimCurrent = GameCatalogBaker.CellMaterial("DebugSimCurrent", new Color(0.2f, 0.85f, 1f, 0.5f));
            gridDebug.matSimGoal = GameCatalogBaker.CellMaterial("DebugSimGoal", new Color(0.25f, 1f, 0.4f, 0.5f));
            gridDebug.matTransit = GameCatalogBaker.CellMaterial("DebugTransit", new Color(0.85f, 0.4f, 1f, 0.32f));
            gridDebug.matNavBlocked = GameCatalogBaker.CellMaterial("DebugNavBlocked", new Color(1f, 0.25f, 0.2f, 0.3f));
            gridDebug.matPathLine = GameCatalogBaker.CellMaterial("DebugPathLine", new Color(0.25f, 0.7f, 1f, 0.9f));

            var renderDebug = debugGo.AddComponent<RenderDebugController>();
            renderDebug.volume = vol;
            renderDebug.sky = sky;
            renderDebug.sunHd = sunHd;
            renderDebug.fillLight = fill;
            renderDebug.fillHd = fillHd;

            var debugMenu = debugGo.AddComponent<GameDebugMenu>();
            debugMenu.gridDebug = gridDebug;
            debugMenu.render = renderDebug;
            debugMenu.director = dir;
            debugMenu.theme = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Game/UI/GameTheme.uss");

            BuildGameUI(dir, clock, player, casMode, buildMode, debugMenu);

            var cam = Camera.main;
            if (cam != null)
            {
                // Free gameplay camera (pan/orbit/zoom). Stays LIVE in build mode; only CAS overrides it.
                var gameCam = cam.gameObject.AddComponent<GameCamera>();
                gameCam.cam = cam;
                gameCam.pivot = Vector3.zero;
                gameCam.yaw = 0f;
                gameCam.pitch = 42f;
                gameCam.distance = 13f;
                buildMode.gameCamera = gameCam;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Scene);
            Selection.activeGameObject = dirGo;
            Debug.Log($"[Sims4 Game] M0 scene built at {Scene}: {placed.Count} Sim(s) [{string.Join(", ", placed)}], " +
                      $"{needs.Count} need(s). Press Play — needs decay on the HUD; use Pause/1x/2x/3x.");
        }

        // The SSGI volume override silently does nothing unless the HDRP asset's pipeline settings
        // support it — flip the flag once (SerializedObject: no public setter exists).
        private static void TryEnableSsgiSupport()
        {
            var hdrp = GraphicsSettings.defaultRenderPipeline as HDRenderPipelineAsset;
            if (hdrp == null) return;
            var so = new SerializedObject(hdrp);
            var p = so.FindProperty("m_RenderPipelineSettings.supportSSGI");
            if (p == null)
            {
                Debug.LogWarning("[Sims4 Game] Couldn't find supportSSGI on the HDRP asset — if the SSGI " +
                                 "toggle has no visible effect, enable 'Screen Space Global Illumination' " +
                                 "in the HDRP asset's Lighting section manually.");
                return;
            }
            if (!p.boolValue)
            {
                p.boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(hdrp);
                Debug.Log("[Sims4 Game] Enabled SSGI support on the HDRP asset.");
            }
        }

        private static GameObject TryBuildSim(string slug, string label, Vector3 pos, string displayName, List<string> placed)
        {
            var json = $"Assets/Sims4/{slug}/character.json";
            if (!System.IO.File.Exists(json))
            {
                Debug.LogWarning($"[Sims4 Game] {json} not found — skipping {displayName}. Export it first (exportchar).");
                return null;
            }
            var character = Sims4CharacterBuilder.BuildCustomizerSlug(slug, label);
            if (character == null) { Debug.LogWarning($"[Sims4 Game] character build failed for {slug}."); return null; }

            var root = character.transform.root.gameObject;
            root.name = displayName;
            root.transform.position = pos;

            var body = root.GetComponent<SimBody>();
            if (body == null) body = root.AddComponent<SimBody>();
            body.displayName = displayName;
            body.appearanceSlug = slug;
            placed.Add(displayName);
            return root;
        }

        // Clone a built character into another independent Sim (shares the look, own body + soul at play
        // start). Instantiate copies the already-assembled meshes/materials, so the clone looks correct.
        private static void CloneSim(GameObject source, string slug, string name, Vector3 pos, List<string> placed)
        {
            if (source == null) return;
            var clone = Object.Instantiate(source);
            clone.name = name;
            clone.transform.position = pos;
            clone.transform.rotation = Quaternion.identity;

            var body = clone.GetComponent<SimBody>();
            if (body == null) body = clone.AddComponent<SimBody>();
            body.displayName = name;
            body.appearanceSlug = slug;
            placed.Add(name);
        }

        // The UI Toolkit shell (D-105): a UIDocument driving GameHud.uxml + GameTheme.uss, plus the
        // EventSystem runtime UI Toolkit needs for input.
        private static void BuildGameUI(SimulationDirector dir, GameClock clock, PlayerController player,
                                        GameCasMode casMode, GameBuildMode buildMode, GameDebugMenu debugMenu)
        {
            const string uiFolder = "Assets/Game/UI";
            const string panelPath = uiFolder + "/GamePanelSettings.asset";

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                // Runtime UI Toolkit needs a theme for its built-in controls (scrollbars etc.).
                var themes = AssetDatabase.FindAssets("t:ThemeStyleSheet");
                if (themes.Length > 0)
                    panelSettings.themeStyleSheet =
                        AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(themes[0]));
                else
                    Debug.LogWarning("[Sims4 Game] No ThemeStyleSheet in the project. Create one via " +
                                     "Assets → Create → UI Toolkit → Panel Settings Asset (it generates the " +
                                     "default runtime theme), then re-run this menu. Built-in controls will " +
                                     "look unstyled until then.");
                AssetDatabase.CreateAsset(panelSettings, panelPath);
                AssetDatabase.SaveAssets();
            }

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uiFolder + "/GameHud.uxml");
            if (uxml == null) Debug.LogWarning($"[Sims4 Game] {uiFolder}/GameHud.uxml not found — the HUD will be empty.");

            var uiGo = new GameObject("Game UI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            doc.visualTreeAsset = uxml;

            var hudUi = uiGo.AddComponent<GameHudUI>();
            hudUi.document = doc;
            hudUi.director = dir;
            hudUi.clock = clock;
            hudUi.player = player;
            hudUi.cas = casMode;
            hudUi.build = buildMode;
            hudUi.debugMenu = debugMenu; // DBG button in the top-right cluster toggles the same menu as F3
            hudUi.icons = IconLibraryBuilder.Build(); // name→vector-icon lookup (placeholder for missing)
            player.ui = hudUi; // so world clicks ignore clicks that land on a panel

            // Build mode's own panel (its own UIDocument, drawn above the HUD, shown only in build mode).
            // BuildDock.uxml = the Buy catalogue dock (category rail + thumbnail grid + swatches, TASK-004).
            var buildUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uiFolder + "/BuildDock.uxml");
            if (buildUxml == null) Debug.LogWarning($"[Sims4 Game] {uiFolder}/BuildDock.uxml not found — build panel will be empty.");
            var buildUiGo = new GameObject("Build UI");
            var buildDoc = buildUiGo.AddComponent<UIDocument>();
            buildDoc.panelSettings = panelSettings;
            buildDoc.visualTreeAsset = buildUxml;
            buildDoc.sortingOrder = 10; // above the HUD
            buildMode.document = buildDoc;

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        // Starter furniture — REAL catalogue items via SmartObjectFactory, the SAME path the in-game
        // build mode uses. Positions are SNAPPED through the one grid rule (D-106): the hint is where
        // we'd like it, the tile lattice decides where it lands — identical to a player placement.
        private static void BuildLot(GameCatalog catalog)
        {
            if (catalog == null) return;
            Place(catalog, "fridge", new Vector3(-3f, 0f, 2f), 90f);
            Place(catalog, "bed_double", new Vector3(3f, 0f, 2f), 180f);
            Place(catalog, "toilet", new Vector3(-3f, 0f, -2f), 270f);
            Place(catalog, "shower", new Vector3(-1.5f, 0f, -3f), 0f);
            Place(catalog, "tv", new Vector3(3f, 0f, -2f), 270f);
            Place(catalog, "sofa", new Vector3(1.5f, 0f, 3f), 180f);
        }

        private static void Place(GameCatalog catalog, string id, Vector3 hint, float yaw = 0f)
        {
            var def = catalog.ById(id);
            if (def == null)
            {
                Debug.LogWarning($"[Sims4 Game] starter lot: catalog item '{id}' missing — skipped.");
                return;
            }
            int rot = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 90f) & 3;
            int w = Mathf.Max(1, def.footW), d = Mathf.Max(1, def.footD);
            if ((rot & 1) == 1) (w, d) = (d, w);
            var pos = LotGrid.SnapFootprint(LotOrigin, 1f, hint, w, d);
            SmartObjectFactory.Create(def, pos, 0, rot * 90f);
        }

        // The 1 m grid visual: a transparent quad over the lot with one line per tile, OFF by default —
        // GameBuildMode shows it while build mode is open (D-106: the player sees the space the engine
        // reasons in).
        private static GameObject BuildGridOverlay()
        {
            const string texPath = GameFolder + "/GridOverlay.png";
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var px = new Color32[64 * 64];
            var line = new Color32(255, 255, 255, 110);
            var clear = new Color32(0, 0, 0, 0);
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 64; x++)
                    px[(y * 64) + x] = (x <= 1 || y <= 1) ? line : clear; // 2px line ≈ 3 cm at 1 m/tile
            tex.SetPixels32(px);
            tex.Apply();
            if (!AssetDatabase.IsValidFolder(GameFolder)) AssetDatabase.CreateFolder("Assets", "Game");
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(texPath) is TextureImporter imp)
            {
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
            var gridTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            var m = new Material(Shader.Find("HDRP/Unlit")) { name = "GameGridOverlay" };
            m.SetFloat("_SurfaceType", 1f); // transparent
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_TransparentZWrite", 0f);
            m.SetColor("_UnlitColor", new Color(1f, 1f, 1f, 0.55f));
            m.SetTexture("_UnlitColorMap", gridTex);
            m.SetTextureScale("_UnlitColorMap", new Vector2(30f, 30f)); // one repeat per tile
            HDMaterial.ValidateMaterial(m);
            var mat = HomeEditorSceneBuilder.Persist(m, GameFolder + "/GameGridOverlay.mat");

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Build Grid Overlay";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetPositionAndRotation(new Vector3(0f, 0.02f, 0f), Quaternion.Euler(90f, 0f, 0f));
            quad.transform.localScale = new Vector3(30f, 30f, 1f);
            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            quad.SetActive(false);
            return quad;
        }

        private static List<NeedDefinition> EnsureNeedDefinitions()
        {
            if (!AssetDatabase.IsValidFolder(GameFolder)) AssetDatabase.CreateFolder("Assets", "Game");
            if (!AssetDatabase.IsValidFolder(NeedsFolder)) AssetDatabase.CreateFolder(GameFolder, "Needs");

            var seed = new (string id, string name, float decay, float start, Color col)[]
            {
                ("hunger",  "Hunger",  8f,  80f, new Color(0.85f, 0.55f, 0.25f)),
                ("energy",  "Energy",  6f,  85f, new Color(0.35f, 0.55f, 0.95f)),
                ("fun",     "Fun",     10f, 70f, new Color(0.85f, 0.35f, 0.85f)),
                ("social",  "Social",  7f,  75f, new Color(0.35f, 0.80f, 0.85f)),
                ("hygiene", "Hygiene", 9f,  85f, new Color(0.40f, 0.85f, 0.55f)),
                ("bladder", "Bladder", 14f, 80f, new Color(0.90f, 0.85f, 0.35f)),
            };

            var list = new List<NeedDefinition>();
            foreach (var d in seed)
            {
                var path = $"{NeedsFolder}/{d.id}.asset";
                var nd = AssetDatabase.LoadAssetAtPath<NeedDefinition>(path);
                if (nd == null)
                {
                    nd = ScriptableObject.CreateInstance<NeedDefinition>();
                    nd.id = d.id; nd.displayName = d.name; nd.decayPerHour = d.decay;
                    nd.startValue = d.start; nd.barColor = d.col;
                    AssetDatabase.CreateAsset(nd, path);
                }
                list.Add(nd);
            }
            AssetDatabase.SaveAssets();
            return list;
        }
    }
}
