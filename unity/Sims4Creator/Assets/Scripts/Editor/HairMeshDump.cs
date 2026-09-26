using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Diagnostic: dumps the ACTIVE hairstyle's mesh + how close its polygons sit to the skin, so we can
    /// see why hair triangles flicker / render black (z-fighting with the face, coplanar cards, degenerate
    /// triangles). Writes a report to C:/tmp/hair_dump.txt. Run it in Play or Edit mode with a Sim built.
    /// </summary>
    public static class HairMeshDump
    {
        [MenuItem("Sims4 Creator/Debug/Dump Hair Mesh")]
        public static void Dump()
        {
            var ch = Object.FindFirstObjectByType<Sims4Character>();
            if (ch == null) { Debug.LogError("[HairDump] No Sims4Character in the scene."); return; }

            // Active hair SMR (the selected hairstyle's mesh).
            SkinnedMeshRenderer hairSmr = null;
            if (ch.hairStyles != null && ch.HairStyleIndex >= 0 && ch.HairStyleIndex < ch.hairStyles.Count)
            {
                var root = ch.hairStyles[ch.HairStyleIndex].root;
                if (root != null) hairSmr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            }
            if (hairSmr == null) { Debug.LogError("[HairDump] No active hair mesh (is a hairstyle other than 'None' selected?)."); return; }

            var sb = new StringBuilder();
            sb.AppendLine($"=== HAIR: {hairSmr.name} ===");

            // Material properties that govern the depth/cull behaviour.
            var mat = hairSmr.sharedMaterial;
            if (mat != null)
            {
                sb.AppendLine($"material shader={mat.shader.name} renderQueue={mat.renderQueue}");
                foreach (var p in new[] { "_AlphaCutoffEnable", "_AlphaCutoff", "_SurfaceType", "_DoubleSidedEnable",
                                          "_DoubleSidedNormalMode", "_CullMode", "_CullModeForward", "_ZWrite",
                                          "_ZTestDepthEqualForOpaque", "_TransparentDepthPrepassEnable" })
                    if (mat.HasProperty(p)) sb.AppendLine($"  {p} = {mat.GetFloat(p)}");
            }

            Vector3[] BakeWorld(SkinnedMeshRenderer s)
            {
                var bm = new Mesh();
                s.BakeMesh(bm, true);
                var v = bm.vertices;
                var l2w = s.transform.localToWorldMatrix;
                for (var i = 0; i < v.Length; i++) v[i] = l2w.MultiplyPoint3x4(v[i]);
                Object.DestroyImmediate(bm);
                return v;
            }

            var hairV = BakeWorld(hairSmr);
            var hm = hairSmr.sharedMesh;
            sb.AppendLine($"verts={hairV.Length} tris={(hm != null ? hm.triangles.Length / 3 : 0)} localBounds={(hm != null ? hm.bounds.size.ToString("0.000") : "?")}");

            // "Skin" = every other visible SMR that is NOT under a hairstyle root.
            var hairRoots = ch.hairStyles?.Where(h => h.root != null).Select(h => h.root.transform).ToList() ?? new List<Transform>();
            var skin = new List<Vector3>();
            foreach (var s in ch.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (s == hairSmr) continue;
                var underHair = false;
                for (var t = s.transform; t != null; t = t.parent) if (hairRoots.Contains(t)) { underHair = true; break; }
                if (underHair) continue;
                skin.AddRange(BakeWorld(s));
            }
            var sv = skin.ToArray();
            sb.AppendLine($"skin verts (all non-hair visible SMRs): {sv.Length}");

            // Distance of each hair vert to the nearest SKIN vert — coplanar/z-fight detection.
            var thr = new[] { 0.0005f, 0.001f, 0.002f, 0.005f, 0.01f };
            var buckets = new int[6];
            var samples = new List<Vector3>();
            for (var i = 0; i < hairV.Length; i++)
            {
                var best = float.MaxValue;
                for (var j = 0; j < sv.Length; j++)
                {
                    var d = (hairV[i] - sv[j]).sqrMagnitude;
                    if (d < best) best = d;
                }
                best = Mathf.Sqrt(best);
                var b = 5;
                for (var k = 0; k < thr.Length; k++) if (best < thr[k]) { b = k; break; }
                buckets[b]++;
                if (best < 0.002f && samples.Count < 25) samples.Add(hairV[i]);
            }
            sb.AppendLine("hair-vert distance to nearest skin-vert:");
            sb.AppendLine($"  <0.5mm={buckets[0]}  <1mm={buckets[1]}  <2mm={buckets[2]}  <5mm={buckets[3]}  <10mm={buckets[4]}  >=10mm={buckets[5]}");
            sb.AppendLine($"  => {buckets[0] + buckets[1] + buckets[2]} hair verts sit within 2mm of skin (z-fight candidates)");
            foreach (var p in samples) sb.AppendLine($"    near-skin hair vert @ ({p.x:0.000},{p.y:0.000},{p.z:0.000})");

            // Degenerate/thin triangles in the hair mesh (another flicker source).
            if (hm != null)
            {
                var lv = hm.vertices; var tris = hm.triangles; var degen = 0; var thin = 0;
                for (var t = 0; t + 2 < tris.Length; t += 3)
                {
                    var a = lv[tris[t]]; var b = lv[tris[t + 1]]; var c = lv[tris[t + 2]];
                    var area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                    if (area < 1e-9f) degen++;
                    else if (area < 1e-7f) thin++;
                }
                sb.AppendLine($"triangles: degenerate(area~0)={degen}  very-thin={thin}");
            }

            const string path = "C:/tmp/hair_dump.txt";
            Directory.CreateDirectory("C:/tmp");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[HairDump] wrote {path}\n{sb}");
        }
    }
}
