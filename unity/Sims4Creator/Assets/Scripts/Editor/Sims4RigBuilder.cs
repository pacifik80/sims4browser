using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Builds a rigged <see cref="SkinnedMeshRenderer"/> from a <c>rig.json</c> the exporter writes
    /// next to a CAS/Sim asset under <c>Assets/Sims4/</c>. OBJ/FBX can't reliably carry Sims skinning
    /// into Unity, so we construct the Mesh + skeleton + SkinnedMeshRenderer natively.
    ///
    /// Critical correctness (see the rig investigation):
    ///  - bone bind matrices are WORLD-space, ROW-MAJOR float[16] → transpose into Unity's
    ///    column-major <see cref="Matrix4x4"/> (the [row,col] indexer does that for us);
    ///  - the skeleton is built FIRST, then <c>mesh.bindposes[i]</c> is derived from the actual bone
    ///    transforms (<c>bone.worldToLocalMatrix * renderer.localToWorldMatrix</c>). This GUARANTEES
    ///    that at the bind pose skinning resolves to identity (so it renders exactly like the static
    ///    mesh) regardless of any decomposition error in the exported bind matrices, and that bone
    ///    rotation deforms cleanly — using the exported inverseBindPose directly did NOT match the
    ///    decomposed transforms and exploded the mesh on rotation;
    ///  - vertices are authored in BIND pose; up to 4 influences/vertex (normalized by the exporter);
    ///  - normals/tangents are recalculated (matches the proven static path).
    ///
    /// Run: <b>Sims4 Creator ▸ Build Rigged Mesh(es) from rig.json</b>.
    /// </summary>
    public static class Sims4RigBuilder
    {
        private const string Root = "Assets/Sims4";

        // ---- rig.json DTOs (JsonUtility-compatible) -------------------------------------------
        [System.Serializable]
        private sealed class RigManifest
        {
            public string asset;
            public RigMesh mesh;
            public RigBone[] bones;
            public RigMaterial material;
        }

        [System.Serializable]
        private sealed class RigMesh
        {
            public string name;
            public int vertexCount;
            public float[] positions;   // 3 * N
            public float[] uv0;         // 2 * N (optional)
            public float[] uv1;         // 2 * N (optional)
            public int[] triangles;     // flat
            public int[] boneIndices;   // 4 * N
            public float[] boneWeights; // 4 * N (normalized)
        }

        [System.Serializable]
        private sealed class RigBone
        {
            public string name;
            public string parentName;       // null/empty for the root
            public float[] bindPose;        // 16, row-major, world-space
            public float[] inverseBindPose; // 16, row-major, world-space (unused — see class doc)
        }

        [System.Serializable]
        private sealed class RigMaterial
        {
            public string diffuse;
            public string normal;
            public string specular;
            public string shadow;
        }

        [MenuItem("Sims4 Creator/Meshes/Build Rigged Mesh(es) from rig.json", priority = 40)]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                Debug.LogWarning($"[Sims4Creator] {Root} not found.");
                return;
            }

            var built = 0;
            foreach (var path in Directory.GetFiles(Root, "rig.json", SearchOption.AllDirectories))
            {
                try
                {
                    BuildOne(path.Replace('\\', '/'));
                    built++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Sims4Creator] Rig build FAILED for '{path}': {ex}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Sims4Creator] Built {built} rigged mesh(es) from rig.json.");
        }

        private static void BuildOne(string rigJsonPath)
        {
            var dir = Path.GetDirectoryName(rigJsonPath).Replace('\\', '/');
            var manifest = JsonUtility.FromJson<RigManifest>(File.ReadAllText(rigJsonPath));
            if (manifest?.mesh == null || manifest.bones == null || manifest.bones.Length == 0)
            {
                Debug.LogWarning($"[Sims4Creator] {rigJsonPath}: empty/invalid rig.json — skipped.");
                return;
            }

            var m = manifest.mesh;
            var assetName = string.IsNullOrEmpty(manifest.asset) ? Path.GetFileName(dir) : manifest.asset;

            // ---- Skeleton FIRST (bindposes are derived from these transforms) ----
            // Build LOCAL (parent-relative) transforms from the world bind matrices so the hierarchy
            // composes correctly WITHOUT error/scale compounding down long chains (arms, hands, head).
            // Setting world-space localScale and then parenting multiplies scale down every level and
            // explodes deep bones — so we use local = parentWorld^-1 * boneWorld, decomposed per bone.
            var root = new GameObject(assetName);
            var boneTransforms = new Transform[manifest.bones.Length];
            var byName = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
            var nameToIndex = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var worldMats = new Matrix4x4[manifest.bones.Length];

            for (var i = 0; i < manifest.bones.Length; i++)
            {
                worldMats[i] = RowMajorToMatrix(manifest.bones[i].bindPose);
                var go = new GameObject(manifest.bones[i].name);
                boneTransforms[i] = go.transform;
                if (!byName.ContainsKey(manifest.bones[i].name))
                {
                    byName[manifest.bones[i].name] = go.transform;
                    nameToIndex[manifest.bones[i].name] = i;
                }
            }

            Transform rootBone = null;
            for (var i = 0; i < manifest.bones.Length; i++)
            {
                var parentName = manifest.bones[i].parentName;
                Transform parentT;
                var parentWorld = Matrix4x4.identity;

                if (string.IsNullOrEmpty(parentName))
                {
                    parentT = root.transform;
                    rootBone ??= boneTransforms[i];
                }
                else if (byName.TryGetValue(parentName, out parentT))
                {
                    if (nameToIndex.TryGetValue(parentName, out var pIdx))
                    {
                        parentWorld = worldMats[pIdx]; // parent is a real bone
                    }
                    // else: parent is a placeholder at identity → parentWorld stays identity
                }
                else
                {
                    // Referenced parent isn't in the bone list (e.g. pruned b__ROOT_bind__) — placeholder.
                    var placeholder = new GameObject(parentName);
                    placeholder.transform.SetParent(root.transform, worldPositionStays: false);
                    byName[parentName] = placeholder.transform;
                    parentT = placeholder.transform;
                    rootBone ??= placeholder.transform;
                }

                // local = parentWorld^-1 * boneWorld → decompose to parent-relative TRS (no compounding).
                var local = parentWorld.inverse * worldMats[i];
                var t = boneTransforms[i];
                t.localPosition = local.GetColumn(3);
                t.localRotation = local.rotation;
                t.localScale = local.lossyScale;
                t.SetParent(parentT, worldPositionStays: false);
            }

            rootBone ??= boneTransforms[0];

            // ---- Mesh ----
            var meshGo = new GameObject(assetName + "_mesh");
            meshGo.transform.SetParent(root.transform, worldPositionStays: false);

            var mesh = new Mesh
            {
                name = assetName,
                indexFormat = m.vertexCount > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.vertices = ToVec3(m.positions);
            if (m.uv0 is { Length: > 0 })
            {
                mesh.uv = ToVec2(m.uv0);
            }

            if (m.uv1 is { Length: > 0 })
            {
                mesh.uv2 = ToVec2(m.uv1);
            }

            mesh.triangles = m.triangles;
            mesh.boneWeights = BuildBoneWeights(m.boneIndices, m.boneWeights, m.vertexCount, manifest.bones.Length);

            // Bindposes from the ACTUAL built bone transforms — guarantees identity skinning at bind
            // and clean deformation under rotation. bindpose[i] = bone.worldToLocal * mesh.localToWorld.
            var meshLocalToWorld = meshGo.transform.localToWorldMatrix;
            var bindposes = new Matrix4x4[manifest.bones.Length];
            for (var i = 0; i < manifest.bones.Length; i++)
            {
                bindposes[i] = boneTransforms[i].worldToLocalMatrix * meshLocalToWorld;
            }

            mesh.bindposes = bindposes;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var meshPath = $"{dir}/{assetName}_rigged.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
            {
                AssetDatabase.DeleteAsset(meshPath);
            }

            AssetDatabase.CreateAsset(mesh, meshPath);

            // ---- SkinnedMeshRenderer ----
            var smr = meshGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = boneTransforms;
            smr.rootBone = rootBone;
            smr.localBounds = mesh.bounds;
            smr.sharedMaterial = BuildMaterial(dir, manifest.material);

            Selection.activeGameObject = root;
            Debug.Log($"[Sims4Creator] Built rigged '{assetName}': {m.vertexCount} verts, " +
                      $"{manifest.bones.Length} bones, root='{rootBone.name}', bounds={mesh.bounds.size}.");
        }

        // The Sims bind matrices are ROW-VECTOR convention: translation lives in the LAST ROW
        // (m[12..14]) and the matrix multiplies as v*M. Unity is column-vector (M*v, translation in
        // the last column, bottom row 0,0,0,1), so we must TRANSPOSE: u[r,c] = m[c*4+r]. Copying
        // straight (u[r,c]=m[r*4+c]) leaves a non-affine bottom row and makes GetColumn(3)/.rotation/
        // .lossyScale return garbage that explodes deep bone chains.
        private static Matrix4x4 RowMajorToMatrix(float[] m)
        {
            var u = new Matrix4x4();
            if (m is not { Length: 16 })
            {
                return Matrix4x4.identity;
            }

            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++)
                {
                    u[r, c] = m[(c * 4) + r];
                }
            }

            return u;
        }

        private static BoneWeight[] BuildBoneWeights(int[] idx, float[] w, int vertexCount, int boneCount)
        {
            var weights = new BoneWeight[vertexCount];
            for (var v = 0; v < vertexCount; v++)
            {
                var b = new BoneWeight
                {
                    boneIndex0 = Clamp(idx, (v * 4) + 0, boneCount),
                    boneIndex1 = Clamp(idx, (v * 4) + 1, boneCount),
                    boneIndex2 = Clamp(idx, (v * 4) + 2, boneCount),
                    boneIndex3 = Clamp(idx, (v * 4) + 3, boneCount),
                    weight0 = At(w, (v * 4) + 0),
                    weight1 = At(w, (v * 4) + 1),
                    weight2 = At(w, (v * 4) + 2),
                    weight3 = At(w, (v * 4) + 3),
                };
                weights[v] = b;
            }

            return weights;
        }

        private static int Clamp(int[] a, int i, int boneCount)
        {
            if (a == null || i >= a.Length)
            {
                return 0;
            }

            var v = a[i];
            return v >= 0 && v < boneCount ? v : 0;
        }

        private static float At(float[] a, int i) => a != null && i < a.Length ? a[i] : 0f;

        private static Vector3[] ToVec3(float[] flat)
        {
            var n = (flat?.Length ?? 0) / 3;
            var v = new Vector3[n];
            for (var i = 0; i < n; i++)
            {
                v[i] = new Vector3(flat[(i * 3) + 0], flat[(i * 3) + 1], flat[(i * 3) + 2]);
            }

            return v;
        }

        // UVs: flip V (Sims atlas is v=0 at top; Unity samples v=0 at bottom). The OBJ importer
        // auto-flips; this direct-Mesh path must do it explicitly or the texture maps mirrored.
        private static Vector2[] ToVec2(float[] flat)
        {
            var n = (flat?.Length ?? 0) / 2;
            var v = new Vector2[n];
            for (var i = 0; i < n; i++)
            {
                v[i] = new Vector2(flat[(i * 2) + 0], 1f - flat[(i * 2) + 1]);
            }

            return v;
        }

        // Build an HDRP/Lit material from the rig.json texture paths (relative to the asset folder).
        private static Material BuildMaterial(string dir, RigMaterial mat)
        {
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            material.SetColor("_BaseColor", Color.white);

            var diffuse = LoadTex(dir, mat?.diffuse);
            if (diffuse != null)
            {
                material.SetTexture("_BaseColorMap", diffuse);
            }

            var normal = LoadTex(dir, mat?.normal);
            if (normal != null)
            {
                material.SetTexture("_NormalMap", normal);
                material.SetFloat("_NormalScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetFloat("_Smoothness", 0.4f);

            var matPath = $"{dir}/{Path.GetFileName(dir)}_rigged.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
            {
                AssetDatabase.DeleteAsset(matPath);
            }

            AssetDatabase.CreateAsset(material, matPath);
            return material;
        }

        private static Texture2D LoadTex(string dir, string rel)
        {
            if (string.IsNullOrEmpty(rel))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{rel}");
        }
    }
}
