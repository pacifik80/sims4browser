using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Sims4Creator.Editor
{
    // Diagnostic: builds the home scene, drops a reference sphere, and captures the scene at several
    // times of day so the sun/moon travel + time-of-day lighting can actually be seen. Directional
    // lights + the PBR sky DO render through a manual camera.Render() (unlike realtime probes).
    // Not part of the shipping build. Set RENDER_OUT to a *.png base path.
    public static class HomeRenderCheck
    {
        public static void Render()
        {
            var baseOut = System.Environment.GetEnvironmentVariable("RENDER_OUT");
            if (string.IsNullOrEmpty(baseOut))
            {
                baseOut = "C:/Temp/home_sky.png";
            }

            HomeEditorSceneBuilder.Build();

            var scene = SceneManager.GetActiveScene();
            SkyTimeController sky = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var s = root.GetComponent<SkyTimeController>();
                if (s != null) { sky = s; break; }
            }
            Debug.Log($"[SkyCheck] controller found = {sky != null}");

            // Reference objects: a matte white sphere + a wall to read the sun's direction, colour and
            // shadow length; a coloured cube for a stable exposure anchor.
            var test = new GameObject("SkyTest").transform;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.SetParent(test, false);
            ball.transform.localPosition = new Vector3(0f, 1f, 0f);
            ball.transform.localScale = Vector3.one * 2f;
            var bm = new Material(Shader.Find("HDRP/Lit"));
            bm.SetColor("_BaseColor", new Color(0.82f, 0.82f, 0.82f, 1f));
            bm.SetFloat("_Smoothness", 0.15f);
            ball.GetComponent<MeshRenderer>().sharedMaterial = bm;

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(test, false);
            wall.transform.localPosition = new Vector3(-3.5f, 1.5f, 0f);
            wall.transform.localScale = new Vector3(0.2f, 3f, 4f);
            wall.GetComponent<MeshRenderer>().sharedMaterial = bm;

            var camGo = new GameObject("SkyCam");
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<HDAdditionalCameraData>();
            // Match the Home Camera's downward pose so the render reproduces what the user actually sees
            // (looking across the floor at the horizon), not an up-at-the-zenith view.
            cam.transform.SetPositionAndRotation(new Vector3(12f, 14f, -12f), Quaternion.Euler(42f, -45f, 0f));
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;

            const int W = 1280, H = 720;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.DefaultHDR);
            cam.targetTexture = rt;

            int[] hours = { 7, 12, 17, 22 };
            foreach (var hr in hours)
            {
                if (sky != null)
                {
                    sky.timeOfDay = hr;
                    sky.Apply();
                    Debug.Log($"[SkyCheck] {hr:00}h  sun elev {sky.lastElevation:0.0}  az {sky.lastAzimuth:0}");
                }
                for (var i = 0; i < 16; i++) // converge auto-exposure at this time of day
                {
                    cam.Render();
                }
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                var p = baseOut.Replace(".png", $"_{hr:00}.png");
                File.WriteAllBytes(p, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log($"[SkyCheck] wrote {p}");
            }

            // Lamp close-up at dusk: verify the BULB glows + the shade is translucent (not the whole
            // fixture glowing, not the bulb dark).
            Transform catalog = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "HomeCatalog") { catalog = root.transform; break; }
            }
            var lampSrc = catalog != null ? catalog.Find("template_lamp_floor") : null;
            if (lampSrc != null && sky != null)
            {
                var lamp = Object.Instantiate(lampSrc.gameObject, test);
                lamp.SetActive(true);
                lamp.transform.localPosition = new Vector3(3f, 0f, 3f);
                sky.timeOfDay = 21.5f; // dark so the glow reads
                sky.Apply();
                cam.transform.position = new Vector3(3f, 1.3f, 5.4f);
                cam.transform.LookAt(new Vector3(3f, 1.4f, 3f));
                cam.fieldOfView = 42f;
                for (var i = 0; i < 16; i++) { cam.Render(); }
                var prev2 = RenderTexture.active;
                RenderTexture.active = rt;
                var tex2 = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex2.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex2.Apply();
                RenderTexture.active = prev2;
                var lp = baseOut.Replace(".png", "_lamp.png");
                File.WriteAllBytes(lp, tex2.EncodeToPNG());
                Object.DestroyImmediate(tex2);
                Debug.Log($"[SkyCheck] wrote {lp}");
            }

            // STUB-CLIP TEST: a cutaway stub must keep door/window openings as real gaps, not a lintel
            // band across them. Build a door + window wall FULL vs CLIPPED at 0.4m and compare.
            var bwm = typeof(Sims4Creator.HomeEditor).GetMethod("BuildWallBoxesMesh",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (bwm != null && sky != null)
            {
                Mesh Boxes((float, float, float, float)[] boxes, float clip) =>
                    (Mesh)bwm.Invoke(null, new object[] { 1f, 2.8f, 0.12f, boxes, 0f, null, null, clip });
                var door = new (float, float, float, float)[]
                    { (-0.5f, -0.4f, 0f, 2.8f), (0.4f, 0.5f, 0f, 2.8f), (-0.4f, 0.4f, 2.1f, 2.8f) };
                var window = new (float, float, float, float)[]
                    { (-0.5f, -0.4f, 0f, 2.8f), (0.4f, 0.5f, 0f, 2.8f), (-0.4f, 0.4f, 0f, 0.9f), (-0.4f, 0.4f, 2.1f, 2.8f) };
                var wmat = new Material(Shader.Find("HDRP/Lit"));
                wmat.SetColor("_BaseColor", new Color(0.82f, 0.78f, 0.72f, 1f));
                void Wall(Mesh m, Vector3 pos)
                {
                    var g = new GameObject("wall");
                    g.transform.SetParent(test, false);
                    g.transform.localPosition = pos;
                    g.AddComponent<MeshFilter>().sharedMesh = m;
                    g.AddComponent<MeshRenderer>().sharedMaterial = wmat;
                }
                Wall(Boxes(door, float.PositiveInfinity), new Vector3(-3f, 0f, 20f));   // door full
                Wall(Boxes(door, 0.4f), new Vector3(-1f, 0f, 20f));                     // door stub (gap, no band)
                Wall(Boxes(window, float.PositiveInfinity), new Vector3(1f, 0f, 20f));  // window full
                Wall(Boxes(window, 0.4f), new Vector3(3f, 0f, 20f));                    // window stub (solid, sill below curb)
                sky.timeOfDay = 12f;
                sky.Apply();
                cam.transform.position = new Vector3(0f, 2.4f, 24.5f);
                cam.transform.LookAt(new Vector3(0f, 1.0f, 20f));
                cam.fieldOfView = 55f;
                for (var i = 0; i < 12; i++) { cam.Render(); }
                var prev3 = RenderTexture.active;
                RenderTexture.active = rt;
                var tex3 = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex3.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex3.Apply();
                RenderTexture.active = prev3;
                var sp = baseOut.Replace(".png", "_stub.png");
                File.WriteAllBytes(sp, tex3.EncodeToPNG());
                Object.DestroyImmediate(tex3);
                Debug.Log($"[SkyCheck] wrote {sp} (door full/stub, window full/stub)");
            }
        }
    }
}
