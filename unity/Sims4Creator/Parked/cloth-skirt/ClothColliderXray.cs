using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// Debug overlay: draws every collider referenced by Cloth components under this character as a
    /// translucent shell whose material depth-tests ALWAYS — so the colliders stay visible even when
    /// buried under the body or clothing ("X-ray"). Materials are authored by the editor builder
    /// (HDRP/Unlit, transparent, ZTest Always) and wired into the fields below.
    /// </summary>
    public sealed class ClothColliderXray : MonoBehaviour
    {
        public Material capsuleMaterial;
        public Material sphereMaterial;

        private readonly List<GameObject> _viz = new();
        public bool Shown { get; private set; }

        public void SetShown(bool on)
        {
            Shown = on;
            Clear();
            if (on) Build();
        }

        private void OnDisable()
        {
            Clear();
            Shown = false;
        }

        private void Build()
        {
            foreach (var cloth in GetComponentsInChildren<Cloth>(true))
            {
                var caps = cloth.capsuleColliders;
                if (caps != null)
                    foreach (var cc in caps)
                        if (cc != null) AddCapsule(cc);
                var spheres = cloth.sphereColliders;
                if (spheres != null)
                    foreach (var pair in spheres)
                    {
                        if (pair.first != null) AddSphere(pair.first);
                        if (pair.second != null) AddSphere(pair.second);
                    }
            }
        }

        private void AddCapsule(CapsuleCollider cc)
        {
            var mesh = BuildCapsuleMesh(cc.radius, Mathf.Max(cc.height, cc.radius * 2f), 16, 8);
            var go = Spawn(mesh, capsuleMaterial, cc.transform, cc.center);
            // The overlay mesh is authored along local Y; rotate onto the collider's direction axis.
            go.transform.localRotation = cc.direction switch
            {
                0 => Quaternion.FromToRotation(Vector3.up, Vector3.right),
                2 => Quaternion.FromToRotation(Vector3.up, Vector3.forward),
                _ => Quaternion.identity,
            };
        }

        private void AddSphere(SphereCollider sc)
        {
            var mesh = BuildCapsuleMesh(sc.radius, sc.radius * 2f, 16, 8); // height == 2r → a sphere
            Spawn(mesh, sphereMaterial != null ? sphereMaterial : capsuleMaterial, sc.transform, sc.center);
        }

        private GameObject Spawn(Mesh mesh, Material mat, Transform parent, Vector3 center)
        {
            var go = new GameObject("__collider_xray");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = center;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _viz.Add(go);
            return go;
        }

        private void Clear()
        {
            foreach (var go in _viz)
            {
                if (go == null) continue;
                var mesh = go.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
                if (Application.isPlaying) { if (mesh != null) Destroy(mesh); Destroy(go); }
                else { if (mesh != null) DestroyImmediate(mesh); DestroyImmediate(go); } // public API: edit-mode callers must not throw
            }
            _viz.Clear();
        }

        // Capsule along local Y; total height includes both caps. height == 2r degenerates to a sphere.
        private static Mesh BuildCapsuleMesh(float radius, float height, int segments, int capRings)
        {
            var half = Mathf.Max(0f, height * 0.5f - radius);
            var lat = new List<(float y, float r)>();
            for (var i = 0; i <= capRings; i++) // bottom cap: pole → equator
            {
                var a = Mathf.PI * 0.5f * (i / (float)capRings);
                lat.Add((-half - Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }
            for (var i = 0; i <= capRings; i++) // top cap: equator → pole
            {
                var a = Mathf.PI * 0.5f * (i / (float)capRings);
                lat.Add((half + Mathf.Sin(a) * radius, Mathf.Cos(a) * radius));
            }
            var verts = new List<Vector3>(lat.Count * segments);
            foreach (var (y, r) in lat)
                for (var s = 0; s < segments; s++)
                {
                    var ang = Mathf.PI * 2f * s / segments;
                    verts.Add(new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r));
                }
            var tris = new List<int>();
            for (var li = 0; li < lat.Count - 1; li++)
                for (var s = 0; s < segments; s++)
                {
                    int a = (li * segments) + s, b = (li * segments) + ((s + 1) % segments);
                    int c = ((li + 1) * segments) + s, d = ((li + 1) * segments) + ((s + 1) % segments);
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            var m = new Mesh { name = "xray_capsule" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
