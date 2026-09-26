using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// One-click, FULLY code-generated "Create A Sim" scene. Nothing is inherited from any other scene:
    /// the builder makes a brand-new scene and creates every object itself — a three-point studio light
    /// rig (soft key w/ 2048px shadow + contact shadows, cool shadowless fill, rim), a graded global
    /// Volume (8m shadow range, exposure limits, saturation/contrast, vignette; saved as
    /// CASVolumeProfile.asset so every value is live-tweakable), a TAA camera with the shot-based
    /// <see cref="CasCameraRig"/>, a neutral floor, the customizer character, and the
    /// <see cref="CasController"/> IMGUI panel — then saves it as CAS.unity. Press Play to use it.
    ///
    /// The on-screen controls (shots, turntable, motion, sliders) are IMGUI and therefore appear in PLAY
    /// mode; in edit mode the camera still frames the Sim (the rig is [ExecuteAlways]) so the preview is
    /// correct before you press Play.
    /// </summary>
    public static class Sims4CasSceneBuilder
    {
        private const string CasVolumeProfile = "Assets/Scenes/CASVolumeProfile.asset";

        [MenuItem("Sims4 Creator/Character/Build CAS Editor Scene (Female)", priority = 2)]
        public static void BuildCasScene() => Build("Assets/Sims4/af_char/character.json", "Assets/Scenes/CAS.unity", "af_char", "Female");

        [MenuItem("Sims4 Creator/Character/Build CAS Editor Scene (Male)", priority = 3)]
        public static void BuildCasSceneMale() => Build("Assets/Sims4/am_char/character.json", "Assets/Scenes/CAS_Male.unity", "am_char", "Male");

        private static void Build(string characterJson, string casScene, string charSlug, string label)
        {
            if (!File.Exists(characterJson))
            {
                Debug.LogError($"[Sims4Creator] CAS scene: {characterJson} not found — export it first " +
                               $"(exportsim Adult {label} default {charSlug}), then run this again.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 1. Fresh scene owned entirely by us. DefaultGameObjects gives a Main Camera + Directional
            // Light that Unity sets up for the active pipeline, and HDRP lights them via its default global
            // volume (sky + exposure) — so the scene renders lit without hand-built HDRP data.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2. STUDIO LIGHT RIG — three-point. The default directional becomes the KEY (warm, soft,
            // high-res shadow); a cooler shadowless FILL from the other side lifts the dark half so the
            // light stops reading "hard sun"; a RIM from behind separates the silhouette (esp. hair).
            // Ratios matter more than absolutes (auto-exposure normalizes the total): key 100k / fill 30k /
            // rim 50k lux ≈ 3:1 key:fill — the classic character-showcase ratio.
            var key = Object.FindFirstObjectByType<Light>();
            if (key != null)
            {
                key.name = "CAS Sun"; // doubles as the day/night sun — SkyTimeController drives it by city+time
                key.type = LightType.Directional;
                key.transform.rotation = Quaternion.Euler(42f, 150f, 0f);
                key.shadows = LightShadows.Soft;
                key.intensity = 100000f; // lux — explicit so the 3:1 key:fill ratio never depends on scene defaults
                key.useColorTemperature = false;           // the default sun ships a warm filter + 6570K —
                key.color = new Color(1f, 0.98f, 0.94f);   // together they cast the reddish tone; near-neutral instead
                if (!key.TryGetComponent(out HDAdditionalLightData hdKey)) hdKey = key.gameObject.AddComponent<HDAdditionalLightData>();
                hdKey.angularDiameter = 1.5f;               // a believable sun disk with a soft-ish penumbra; SkyTime drives
                                                            // its rotation/intensity/colour by city+time, so this light IS the sun
                hdKey.interactsWithSky = true;              // render the sun disk in the PBR sky
                hdKey.shadowResolution.useOverride = true;  // default override was 512 → visibly pixelated
                hdKey.shadowResolution.@override = 4096;    // over the 8m volume range ⇒ ~2mm/texel on the Sim
                hdKey.useContactShadow.useOverride = true;  // EXPLICITLY OFF: screen-space contact shadows band
                hdKey.useContactShadow.@override = false;   // visibly (stair-steps) on close-up necks/faces
                hdKey.shadowDimmer = 0.72f;                 // lift the shadow floor: full-strength shadows read
                                                            // harsh/contrasty in a studio (user feedback)
            }

            Light MakeStudioLight(string name, Vector3 euler, float lux, Color color)
            {
                var go = new GameObject(name);
                var l = go.AddComponent<Light>();
                l.type = LightType.Directional; // BEFORE Init — it branches on light.type (HDRP menu code does the same)
                var hd = go.AddComponent<HDAdditionalLightData>();
                HDAdditionalLightData.InitDefaultHDAdditionalLightData(hd); // required for scripted HDRP lights
                l.transform.rotation = Quaternion.Euler(euler);
                l.intensity = lux;
                l.useColorTemperature = false; // Init turns temperature on; off so the tint below is exact
                l.color = color;
                l.shadows = LightShadows.None; // only the key casts shadows (clean single shadow read)
                // DIFFUSE-ONLY: fill/rim shape brightness, they must not add their own highlights.
                // Their specular at grazing angles drew glowing "electric" streaks along the hip/arm
                // silhouettes (rim especially) that no material smoothness could kill — the standard
                // cinematic rig gives specular to the key alone.
                hd.affectSpecular = false;
                return l;
            }
            // Constant fill/rim keep the Sim SHAPED and always visible/editable at any hour (the sun is the
            // day/night key and drops to zero at night); auto-exposure keeps the whole thing readable.
            MakeStudioLight("CAS Fill Light", new Vector3(18f, 230f, 0f), 25000f, new Color(0.82f, 0.88f, 1f));
            MakeStudioLight("CAS Rim Light", new Vector3(35f, 330f, 0f), 18000f, new Color(0.95f, 0.97f, 1f));

            // 2b. SCENE VOLUME — the grade that was missing (the HDRP default volume is fully neutral):
            // shadow distance shrunk 150m→8m (the entire shadow map now spends its texels on the Sim —
            // a ~19× effective density win), gentle saturation/contrast (the "colorful" knob), a light
            // vignette for portrait focus, and exposure limits so orbiting a dark outfit can't drift the
            // auto-exposure. Saved as an asset so every value stays live-tweakable in the inspector.
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(CasVolumeProfile) != null) AssetDatabase.DeleteAsset(CasVolumeProfile);
            AssetDatabase.CreateAsset(profile, CasVolumeProfile);
            T AddOverride<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>();
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var shadowSettings = AddOverride<HDShadowSettings>();
            shadowSettings.maxShadowDistance.Override(20f); // Sim + a few metres of ground for the cast shadow
            shadowSettings.cascadeShadowSplitCount.Override(2);
            // Keep AUTOMATIC exposure (NOT wired to SkyTime) so the Sim stays well-exposed at any hour — the
            // day/night feel comes from the sky colour + sun angle, not from letting the Sim go dark. Wide
            // limits so it can adapt from a bright noon sky to a moonlit night.
            var exposure = AddOverride<Exposure>();
            exposure.mode.Override(ExposureMode.Automatic);
            exposure.limitMin.Override(7f);  // was 10 — a dim (dusk/dawn) sky clamped here left the light-toned
                                             // skin looking very dark; a lower floor lets dim scenes brighten.
            exposure.limitMax.Override(16f); // bright midday still rides the top, so this doesn't over-expose.
            var grade = AddOverride<ColorAdjustments>();
            grade.saturation.Override(12f);
            grade.contrast.Override(8f);
            var vignette = AddOverride<Vignette>();
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.4f);
            // HDRP's default bloom has threshold 0 — every tiny specular glint on the skin blooms into a
            // visible white "blink". Gate bloom to genuinely bright pixels only.
            var bloom = AddOverride<Bloom>();
            bloom.threshold.Override(1.3f); // above the residual key hotspots — bloom is what made the glints GLOW
            bloom.intensity.Override(0.12f);
            // Slightly cool white balance: with the warm key + wood floor the render leaned reddish.
            var wb = AddOverride<WhiteBalance>();
            wb.temperature.Override(-6f);
            // The day/night SKY (what makes this "outdoor"): a PBR sky whose sun disk + horizon colour track
            // the sun. A cool-neutral ground tint keeps the horizon pale (the PBR default is a brown haze).
            var env = AddOverride<VisualEnvironment>();
            env.skyType.Override((int)SkyType.PhysicallyBased);
            var pbs = AddOverride<PhysicallyBasedSky>();
            pbs.groundTint.Override(new Color(0.16f, 0.18f, 0.19f));
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volumeGo = new GameObject("CAS Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f; // above the HDRP default volume
            volume.sharedProfile = profile;

            // 2c. DAY/NIGHT ENVIRONMENT — a moon (anti-solar, fades in at night) + SkyTimeController which
            // aims the sun/moon and drives their intensity/colour from the chosen city, date and clock time
            // (same system as the building scene). Exposure is left on Automatic so the Sim stays visible.
            var moonGo = new GameObject("CAS Moon");
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            var moonHd = moonGo.AddComponent<HDAdditionalLightData>();
            HDAdditionalLightData.InitDefaultHDAdditionalLightData(moonHd);
            moon.color = new Color(0.55f, 0.66f, 0.95f);
            moon.shadows = LightShadows.None;
            moonHd.interactsWithSky = true;

            var skyGo = new GameObject("SkyTime");
            var skyCtl = skyGo.AddComponent<Sims4Creator.SkyTimeController>();
            skyCtl.sun = key;             // the CAS Sun (the key light) IS the day/night sun
            skyCtl.moon = moon;
            skyCtl.exposure = null;       // keep the CAS auto-exposure — the Sim stays lit day AND night
            skyCtl.cityIndex = 0;         // London
            skyCtl.month = 6; skyCtl.day = 21; // summer solstice
            skyCtl.timeOfDay = 12f;       // noon — highest, brightest sun so the default view isn't dim
            skyCtl.panelRightMargin = 12f; // dock top-RIGHT, clear of the character's head + side panels
            if (key != null) skyCtl.Apply();

            // 3. STUDIO ROOM — a warm enclosed set (glossy wood floor, soft walls, ceiling) is more
            // comfortable for character editing than a void. Room parts RECEIVE light/shadows but never
            // CAST them: the key is directional, so a shadow-casting ceiling would black out the whole set.
            const string StudioDir = "Assets/Studio";
            if (!AssetDatabase.IsValidFolder(StudioDir)) AssetDatabase.CreateFolder("Assets", "Studio");
            Material RoomMat(string name, Color tint, Texture2D tex, float smooth, Vector2 tiling, bool doubleSided)
            {
                var m = new Material(Shader.Find("HDRP/Lit"));
                m.SetColor("_BaseColor", tint);
                if (tex != null) { m.SetTexture("_BaseColorMap", tex); m.SetTextureScale("_BaseColorMap", tiling); }
                m.SetFloat("_Smoothness", smooth);
                if (doubleSided) // walls/ceiling are thin cubes seen from INSIDE — render their back faces
                {
                    m.SetFloat("_DoubleSidedEnable", 1f);
                    m.SetFloat("_DoubleSidedNormalMode", 1f);
                    m.SetFloat("_CullMode", 0f);
                    m.SetFloat("_CullModeForward", 0f);
                    m.EnableKeyword("_DOUBLESIDED_ON");
                }
                UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
                var path = $"{StudioDir}/{name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(m, path);
                return m;
            }
            void RoomPart(string name, PrimitiveType prim, Vector3 pos, Vector3 scale, Material mat)
            {
                var go = GameObject.CreatePrimitive(prim);
                go.name = name;
                go.transform.SetPositionAndRotation(pos, Quaternion.identity);
                go.transform.localScale = scale;
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>($"{StudioDir}/wood_planks.png");
            var floorMat = RoomMat("CAS_Floor", Color.white, wood, 0.5f, new Vector2(4f, 4f), doubleSided: false); // gloss = light sheen on the planks
            // OUTDOOR: an open ground plane under the sky — no walls/ceiling (they'd block the sky and sun).
            RoomPart("CAS Ground", PrimitiveType.Plane, Vector3.zero, new Vector3(3f, 1f, 3f), floorMat); // ~30 m plane

            // 3b. STANDING DRESSER MIRROR — one freestanding cheval mirror BESIDE the character, angled
            // so the DEFAULT camera (on +Z looking −Z) sees her BACK in the glass. The facing normal is
            // the bisector of (mirror→camera) and (mirror→a-point-behind-her), i.e. the mirror bounces the
            // camera's view onto her back side. HDRP Planar Reflection Probe = live true reflection; the
            // probe mirrors across its local +Y plane, so the probe's up is aligned to the glass normal.
            var mirrorMat = RoomMat("CAS_Mirror", Color.white, null, 1f, Vector2.one, doubleSided: false);
            mirrorMat.SetFloat("_Metallic", 1f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(mirrorMat);
            var frameMat = RoomMat("CAS_MirrorFrame", new Color(0.16f, 0.12f, 0.09f), null, 0.35f, Vector2.one, doubleSided: false);
            {
                // Placement accounts for the UI: the visible viewport is the band BETWEEN the two panels,
                // so the mirror sits at ~half-way between the left panel edge and the character (NDC −0.5).
                var pos = new Vector3(1.05f, 0f, -1.9f);
                // Law-of-reflection solve (verified): the camera ray to the glass reflects onto her back —
                // the virtual viewpoint lands at (4.4, 1.2, −8.0) ⇒ a near-straight back view (29° off).
                var facing = new Vector3(-0.322f, 0f, 0.947f).normalized;
                var yaw = Quaternion.LookRotation(facing, Vector3.up);
                GameObject Part(string name, Vector3 center, Vector3 size, Material mat)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = name;
                    go.transform.SetPositionAndRotation(center, yaw);
                    go.transform.localScale = size;
                    var r = go.GetComponent<Renderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    return go;
                }
                Part("CAS Mirror Base", pos + new Vector3(0, 0.03f, 0), new Vector3(0.55f, 0.06f, 0.36f), frameMat);
                Part("CAS Mirror Frame", pos + new Vector3(0, 0.955f, 0), new Vector3(0.9f, 1.75f, 0.05f), frameMat);
                Part("CAS Mirror", pos + new Vector3(0, 0.98f, 0) + facing * 0.03f, new Vector3(0.78f, 1.6f, 0.02f), mirrorMat);
                var probeGo = new GameObject("CAS Mirror Probe");
                probeGo.transform.position = pos + new Vector3(0, 0.98f, 0) + facing * 0.045f; // just off the glass
                probeGo.transform.rotation = Quaternion.FromToRotation(Vector3.up, facing);    // probe up = glass normal
                var probe = probeGo.AddComponent<UnityEngine.Rendering.HighDefinition.PlanarReflectionProbe>();
                probe.mode = UnityEngine.Rendering.HighDefinition.ProbeSettings.Mode.Realtime;
                probe.realtimeMode = UnityEngine.Rendering.HighDefinition.ProbeSettings.RealtimeMode.EveryFrame;
                probe.influenceVolume.boxSize = new Vector3(2f, 0.8f, 2f); // generous in-plane (probe twist is arbitrary)
                // Default capture is 256px → visibly pixelated glass. 2048 renders a sharp reflection
                // (fits the 4096 probe atlas; one planar probe in the scene, cost is fine).
                probe.settingsRaw.resolutionScalable.useOverride = true;
                probe.settingsRaw.resolutionScalable.@override = UnityEngine.Rendering.HighDefinition.PlanarReflectionAtlasResolution.Resolution2048;
            }

            // 4. Build the customizer character (at origin) into the new scene.
            Sims4CharacterBuilder.BuildCustomizerSlug(charSlug, label);
            var character = Object.FindFirstObjectByType<Sims4Character>();
            if (character == null)
            {
                Debug.LogError("[Sims4Creator] CAS scene: character build produced no Sims4Character " +
                               "(build failed — see the console errors above).");
                return;
            }
            character.transform.position = Vector3.zero;

            // 5. Shot-based camera rig on the Main Camera (frames the Sim in edit mode too).
            var cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            CasCameraRig camRig = null;
            if (cam != null)
            {
                cam.name = "CAS Camera";
                // ANTI-ALIASING: the camera previously had NO HDAdditionalCameraData → AA = None (and the
                // deferred path has no MSAA) → the "edgy" image. TAA is HDRP's best-quality default here.
                if (!cam.TryGetComponent(out HDAdditionalCameraData hdCam)) hdCam = cam.gameObject.AddComponent<HDAdditionalCameraData>();
                hdCam.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                if (!cam.TryGetComponent(out camRig)) camRig = cam.gameObject.AddComponent<CasCameraRig>();
                camRig.character = character.transform;
                camRig.SetShot(0);
                camRig.Refresh();
                EditorUtility.SetDirty(camRig);
            }
            else
            {
                Debug.LogWarning("[Sims4Creator] CAS scene: no Camera in the new scene — the shot rig was not attached.");
            }

            // 6. CAS UI panel (its own GameObject).
            var uiGo = new GameObject("CAS UI");
            var ctrl = uiGo.AddComponent<CasController>();
            ctrl.character = character;
            ctrl.idle = character.TryGetComponent(out Sims4IdleSwitcher idle) ? idle : Object.FindFirstObjectByType<Sims4IdleSwitcher>();
            ctrl.rig = camRig;
            EditorUtility.SetDirty(ctrl);

            // 7. Save the generated scene.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, casScene);

            Selection.activeGameObject = uiGo;
            Debug.Log($"[Sims4Creator] {label} CAS Editor Scene GENERATED at " + casScene + " (key light, fill, floor, camera rig, " +
                      "character, UI — all created by the builder). Press Play: VIEW row = shots (Full/Cowboy/Chest/Face); " +
                      "left-drag or Turn buttons rotate the Sim; scroll zooms; Motion = Auto/Idle/Still. Tab hides the panel.");
        }
    }
}
