using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Builds the CUSTOM physics cloth skirt — a procedurally generated flared tube (no EA asset is
    /// touched or modified) skinned rigidly to the pelvis and simulated with Unity Cloth: waistband
    /// pinned, hem free, colliding with capsules FITTED to the actual leg mesh. Appended to the
    /// Bottom wardrobe slot as a normal ClothingItem with covers=[] (a true 3D shell over the visible
    /// legs — panties, tights and skin stay live underneath).
    /// Geometry and physics are LIVE-tunable at runtime through <see cref="Sims4Creator.SkirtClothTuner"/>;
    /// this builder bakes the body profile the tuner rebuilds from, and generates the initial mesh
    /// through the tuner's own ComputeVertices so the two can never drift apart.
    /// </summary>
    internal static class Sims4ClothSkirtBuilder
    {
        private const float WaistY = 1.105f; // just under the pelvis head (1.123) — waistband line
        private const int Rings = 24;        // dense enough to fold; Unity Cloth bends at edges only
        private const int Segments = 48;

        private static readonly (string id, string label, string file)[] Swatches =
        {
            ("graphite", "Graphite Plaid", "custom_skirt_graphite.png"),
            ("crimson", "Crimson Plaid", "custom_skirt_crimson.png"),
            ("navy", "Navy Plaid", "custom_skirt_navy.png"),
        };

        internal static Sims4Creator.Sims4Character.ClothingItem Build(
            string dir, string assetName, GameObject root,
            Transform[] boneTransforms, Transform rootBone, Matrix4x4[] bindposes,
            Sims4Creator.Sims4Character character)
        {
            var pelvisIdx = System.Array.FindIndex(boneTransforms, t => t != null && t.name == "b__Pelvis__");
            if (pelvisIdx < 0)
            {
                Debug.LogWarning("[Sims4Creator] cloth skirt skipped: no b__Pelvis__ in the shared skeleton.");
                return null;
            }

            // Colour swatches from the pre-generated plaid textures (already prepped by PrepClothingTextures).
            var colors = new List<Sims4Creator.Sims4Character.HairColor>();
            foreach (var (id, label, file) in Swatches)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/Clothing/{file}");
                if (tex != null) colors.Add(new Sims4Creator.Sims4Character.HairColor { id = id, label = label, diffuse = tex });
            }
            if (colors.Count == 0)
            {
                Debug.LogWarning("[Sims4Creator] cloth skirt skipped: no custom_skirt_*.png under Clothing/.");
                return null;
            }

            var bodyVerts = BodyVertices(character);
            var (profileRx, profileRz) = BakeBodyProfile(bodyVerts);

            var hemY = Mathf.Lerp(Sims4Creator.SkirtClothTuner.MiniHemY, Sims4Creator.SkirtClothTuner.AnkleHemY,
                Sims4Creator.SkirtClothTuner.DefaultLength);
            var mesh = BuildSkirtMesh(profileRx, profileRz, hemY, Sims4Creator.SkirtClothTuner.DefaultHemFlare, pelvisIdx, bindposes);
            var meshPath = $"{dir}/{assetName}_cloth_customskirt_mesh.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject("cloth_custom_SkirtCloth");
            go.transform.SetParent(root.transform, worldPositionStays: false);
            var partGo = new GameObject("custom_SkirtCloth");
            partGo.transform.SetParent(go.transform, worldPositionStays: false);

            var mat = BuildFabricMaterial(dir, $"{assetName}_cloth_customskirt", colors[0].diffuse);
            var smr = partGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = boneTransforms;
            smr.rootBone = rootBone;
            smr.localBounds = mesh.bounds;
            smr.updateWhenOffscreen = true;
            smr.sharedMaterial = mat;

            // ---- Unity Cloth. The serialized values here are placeholders — SkirtClothTuner is the
            // source of truth and re-applies its (user-tunable) fields on every enable.
            var cloth = partGo.AddComponent<Cloth>();
            cloth.useGravity = true;
            cloth.useTethers = true;                 // limits stretch → the hem can't sag through colliders
            cloth.enableContinuousCollision = true;  // fast leg swings tunnel far less
            cloth.collisionMassScale = 0.5f;         // stronger push-out response on contact
            cloth.friction = 0.4f;

            var caps = new List<CapsuleCollider>
            {
                LegCapsule(boneTransforms, "b__L_Thigh__", "b__L_Calf__", FitLegRadius(bodyVerts, boneTransforms, "b__L_Thigh__", "b__L_Calf__", +1f, 0.082f, 0.25f)),
                LegCapsule(boneTransforms, "b__R_Thigh__", "b__R_Calf__", FitLegRadius(bodyVerts, boneTransforms, "b__R_Thigh__", "b__R_Calf__", -1f, 0.082f, 0.25f)),
                LegCapsule(boneTransforms, "b__L_Calf__", "b__L_Foot__", FitLegRadius(bodyVerts, boneTransforms, "b__L_Calf__", "b__L_Foot__", +1f, 0.055f, 0.05f)),
                LegCapsule(boneTransforms, "b__R_Calf__", "b__R_Foot__", FitLegRadius(bodyVerts, boneTransforms, "b__R_Calf__", "b__R_Foot__", -1f, 0.055f, 0.05f)),
                HipCapsule(boneTransforms[pelvisIdx]),
            }.Where(c => c != null).ToArray();
            cloth.capsuleColliders = caps;

            var tuner = partGo.AddComponent<Sims4Creator.SkirtClothTuner>();
            tuner.rings = Rings;
            tuner.segments = Segments;
            tuner.waistY = WaistY;
            tuner.profileRx = profileRx;
            tuner.profileRz = profileRz;
            tuner.baseCapsuleRadii = caps.Select(c => c.radius).ToArray(); // pad multiplies these, order matches capsuleColliders

            // ---- X-ray collider overlay: component on the character root + its always-on-top materials.
            if (!root.TryGetComponent<Sims4Creator.ClothColliderXray>(out var xray))
                xray = root.AddComponent<Sims4Creator.ClothColliderXray>();
            xray.capsuleMaterial = BuildXrayMaterial(dir, $"{assetName}_xray_capsule", new Color(0.15f, 1f, 0.35f, 0.30f));
            xray.sphereMaterial = BuildXrayMaterial(dir, $"{assetName}_xray_sphere", new Color(1f, 0.55f, 0.1f, 0.30f));

            Debug.Log($"[Sims4Creator] custom cloth skirt built: {mesh.vertexCount} verts, " +
                      $"{cloth.capsuleColliders.Length} fitted colliders ({string.Join(", ", cloth.capsuleColliders.Select(c => c.radius.ToString("0.000")))}), " +
                      $"{colors.Count} colour(s).");
            return new Sims4Creator.Sims4Character.ClothingItem
            {
                id = "custom_SkirtCloth",
                label = "Cloth Skirt (Physics)",
                category = "bottom",
                covers = System.Array.Empty<string>(), // true 3D shell: hides nothing, legs stay live
                root = go,
                material = mat,
                fabricMask = colors[0].diffuse,
                colors = colors,
                colorIndex = 0,
            };
        }

        // The nude bottom mesh's vertices (bind space) — the ground truth for skirt radii and
        // collider fitting. Null when no body variant is available (fallback constants used).
        private static Vector3[] BodyVertices(Sims4Creator.Sims4Character character)
        {
            var bodyMesh = character != null && character.bottomVariants != null
                ? character.bottomVariants
                    .Where(v => v?.root != null)
                    .Select(v => v.root.GetComponentInChildren<SkinnedMeshRenderer>(true))
                    .Where(s => s != null && s.sharedMesh != null)
                    .Select(s => s.sharedMesh)
                    .FirstOrDefault()
                : null;
            return bodyMesh != null ? bodyMesh.vertices : null;
        }

        // Body extent profile: max |x| and |z| per height band from the waist down to the ankle,
        // interpolated by the tuner whenever it (re)builds the tube at any length/flare.
        private static (float[] rx, float[] rz) BakeBodyProfile(Vector3[] bodyVerts)
        {
            var n = Sims4Creator.SkirtClothTuner.ProfileSamples;
            var bottomY = Sims4Creator.SkirtClothTuner.ProfileBottomY;
            var rx = new float[n];
            var rz = new float[n];
            for (var i = 0; i < n; i++) // analytic fallback: gentle hip-to-ankle taper
            {
                var t = i / (float)(n - 1);
                rx[i] = Mathf.Lerp(0.155f, 0.10f, t);
                rz[i] = Mathf.Lerp(0.125f, 0.09f, t);
            }
            if (bodyVerts == null) return (rx, rz);

            var bandHalf = (WaistY - bottomY) / (n - 1);
            for (var i = 0; i < n; i++)
            {
                var y = Mathf.Lerp(WaistY, bottomY, i / (float)(n - 1));
                float maxX = 0f, maxZ = 0f;
                var found = false;
                foreach (var v in bodyVerts)
                {
                    if (v.y < y - bandHalf || v.y > y + bandHalf) continue;
                    found = true;
                    if (Mathf.Abs(v.x) > maxX) maxX = Mathf.Abs(v.x);
                    if (Mathf.Abs(v.z) > maxZ) maxZ = Mathf.Abs(v.z);
                }
                if (!found) continue;
                rx[i] = maxX;
                rz[i] = maxZ;
            }
            return (rx, rz);
        }

        // Fit a leg capsule radius from the body mesh: max radial distance to the bone→child axis
        // among this leg's vertices (side-filtered by x sign so the other leg can't inflate it).
        // tMin skips the glute region on the thigh segment (the hip capsule owns that zone).
        private static float FitLegRadius(
            Vector3[] bodyVerts, Transform[] bones, string boneName, string childName,
            float side, float fallback, float tMin)
        {
            if (bodyVerts == null) return fallback;
            var ta = bones.FirstOrDefault(t => t != null && t.name == boneName);
            var tb = bones.FirstOrDefault(t => t != null && t.name == childName);
            if (ta == null || tb == null) return fallback;
            var a = ta.position;
            var axis = tb.position - a;
            var len = axis.magnitude;
            if (len < 1e-4f) return fallback;
            axis /= len;
            var max = 0f;
            foreach (var v in bodyVerts)
            {
                if (side > 0f ? v.x < 0.01f : v.x > -0.01f) continue; // this leg's side only
                var rel = v - a;
                var t = Vector3.Dot(rel, axis) / len;
                if (t < tMin || t > 0.92f) continue;
                var r = (rel - (axis * (t * len))).magnitude;
                if (r > 0.14f) continue; // stray verts / the inner face of the other leg
                if (r > max) max = r;
            }
            return max > 0.02f ? max + 0.008f : fallback;
        }

        // Tube topology (UVs, weights, triangles) with positions from the tuner's shared generator.
        // NO duplicated seam column (Unity Cloth tears at duplicated verts): U is MIRRORED around the
        // circumference (0→1→0) — a plaid tiles that invisibly. BOTH UV axes stay in [0,1]: the
        // runtime swaps the base map for a CLAMPED composite RenderTexture.
        private static Mesh BuildSkirtMesh(float[] profileRx, float[] profileRz, float hemY, float hemFlare, int pelvisIdx, Matrix4x4[] bindposes)
        {
            var verts = new Vector3[Rings * Segments];
            Sims4Creator.SkirtClothTuner.ComputeVertices(verts, Rings, Segments, WaistY, hemY, hemFlare, profileRx, profileRz);
            var uvs = new Vector2[verts.Length];
            var weights = new BoneWeight[verts.Length];
            for (var i = 0; i < Rings; i++)
            {
                var t = i / (float)(Rings - 1);
                for (var s = 0; s < Segments; s++)
                {
                    var vi = (i * Segments) + s;
                    var around = s / (float)Segments * 2f;
                    uvs[vi] = new Vector2(around <= 1f ? around : 2f - around, t);
                    weights[vi] = new BoneWeight { boneIndex0 = pelvisIdx, weight0 = 1f };
                }
            }
            var tris = new int[(Rings - 1) * Segments * 6];
            var k = 0;
            for (var i = 0; i < Rings - 1; i++)
                for (var s = 0; s < Segments; s++)
                {
                    int a = (i * Segments) + s, b = (i * Segments) + ((s + 1) % Segments);
                    int c = ((i + 1) * Segments) + s, d = ((i + 1) * Segments) + ((s + 1) % Segments);
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            var mesh = new Mesh { name = "custom_SkirtCloth" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.boneWeights = weights;
            mesh.bindposes = bindposes;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A capsule spanning bone→child bone (bind pose), parented to the bone so it follows animation.
        private static CapsuleCollider LegCapsule(Transform[] bones, string boneName, string childName, float radius)
        {
            var ta = bones.FirstOrDefault(t => t != null && t.name == boneName);
            var tb = bones.FirstOrDefault(t => t != null && t.name == childName);
            if (ta == null || tb == null) return null;
            var go = NewColliderGo($"__clothcap_{boneName}_{childName}", ta);
            var pa = ta.position;
            var pb = tb.position;
            go.transform.position = (pa + pb) * 0.5f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, (pb - pa).normalized);
            var cc = go.AddComponent<CapsuleCollider>();
            cc.direction = 1;
            cc.radius = radius;
            cc.height = Vector3.Distance(pa, pb) + (radius * 1.6f); // caps overlap the joints
            return cc;
        }

        // A wide capsule across the hips/seat so the skirt back drapes over the pelvis, not into it.
        private static CapsuleCollider HipCapsule(Transform pelvis)
        {
            var go = NewColliderGo("__clothcap_hips", pelvis);
            // Pelvis-relative (bind head sits at Y 1.123): robust even if the character root moves.
            go.transform.position = pelvis.position + new Vector3(0f, -0.153f, 0f);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.right); // axis across the hips
            var cc = go.AddComponent<CapsuleCollider>();
            cc.direction = 1;
            cc.radius = 0.105f;
            cc.height = 0.34f;
            return cc;
        }

        private static GameObject NewColliderGo(string name, Transform parent)
        {
            var existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        // Plain opaque double-sided HDRP/Lit fabric — the runtime composite pipeline swaps its base map
        // (with alpha=1 everywhere the composite output is the fabric verbatim, so no special-casing).
        private static Material BuildFabricMaterial(string dir, string baseName, Texture2D diffuse)
        {
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null) return null;
            var m = new Material(shader) { name = baseName };
            m.SetTexture("_BaseColorMap", diffuse);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.22f);
            m.SetFloat("_DoubleSidedEnable", 1f); // the hem interior is visible when the cloth swings
            m.SetFloat("_EnableGeometricSpecularAA", 1f);
            m.SetFloat("_SpecularAAScreenSpaceVariance", 0.25f);
            m.SetFloat("_SpecularAAThreshold", 0.25f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            var matPath = $"{dir}/{baseName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(m, matPath);
            return m;
        }

        // HDRP/Unlit, transparent, depth-test ALWAYS → renders on top of everything (the X-ray look).
        private static Material BuildXrayMaterial(string dir, string baseName, Color color)
        {
            var shader = Shader.Find("HDRP/Unlit");
            if (shader == null) return null;
            var m = new Material(shader) { name = baseName };
            m.SetFloat("_SurfaceType", 1f); // transparent
            m.SetColor("_UnlitColor", color);
            m.SetFloat("_DoubleSidedEnable", 1f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_TransparentZWrite", 0f);
            m.SetFloat("_ZTestTransparent", (float)UnityEngine.Rendering.CompareFunction.Always);
            m.SetFloat("_AlphaCutoffEnable", 0f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            m.renderQueue = 3300; // late in HDRP's transparent range → draws over other transparents too
            var matPath = $"{dir}/{baseName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(m, matPath);
            return m;
        }
    }
}
