using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Builds a full Sims character from a <c>character.json</c> the exporter writes under
    /// <c>Assets/Sims4/&lt;slug&gt;/</c>: ONE shared skeleton + a separate <see cref="SkinnedMeshRenderer"/>
    /// per body part (body/head/eyes/top/bottom/shoes…), all bound to that skeleton, plus a
    /// <see cref="Sims4Body"/> component for region hide/show (clothing layering).
    ///
    /// Reuses the rig math proven in Sims4RigBuilder: TS4 bind matrices are row-vector (translation in
    /// the last row) so they must be TRANSPOSED into Unity's column-vector Matrix4x4; the skeleton is
    /// built from parent-relative local transforms (no scale compounding); bindposes are derived from
    /// the built bone transforms so the bind pose is exact.
    ///
    /// Run: <b>Sims4 Creator ▸ Build Character(s) from character.json</b>.
    /// </summary>
    public static class Sims4CharacterBuilder
    {
        private const string Root = "Assets/Sims4";

        // ---- character.json DTOs (JsonUtility-compatible) -------------------------------------
        [System.Serializable] private sealed class CharManifest
        {
            public string asset;
            public string kind;
            public string age;
            public string gender;
            public CharBone[] skeleton;
            public CharPart[] parts;
            public SkinCatalog skinCatalog; // optional — present once the exporter writes it
            public MeshCatalog meshCatalog; // optional — body-mesh variants on the shared skeleton
            public HairCatalog hairCatalog; // optional — hairstyles + colour swatches on the shared skeleton
            public ClothingCatalog clothingCatalog; // optional — wardrobe (top/bottom/full/shoes) on the shared skeleton
            public SkinBaseCatalog skinBaseCatalog; // optional — full color base-skin albedos
            public SkinNormalCatalog skinNormalCatalog; // optional — real skin-detail normal maps
            public CharBoneMorph[] boneMorphs; // optional — BONE components of the sliders (SMOD→BOND)
        }

        [System.Serializable] private sealed class CharBoneMorph
        {
            public string name;                  // == blend-shape / morph id
            public CharBoneMorphEntry[] entries;
        }

        [System.Serializable] private sealed class CharBoneMorphEntry
        {
            public string bone;
            public float ox, oy, oz;             // offset (parent-frame add, metres)
            public float sx, sy, sz;             // scale DELTA (0 = none; effective = 1 + delta·weight)
            public float qx, qy, qz, qw = 1f;    // rotation delta
        }

        [System.Serializable] private sealed class MeshCatalog
        {
            public MeshRegion top;    // upper torso (+ arms)
            public MeshRegion bottom; // legs
            public MeshRegion feet;   // feet
        }

        [System.Serializable] private sealed class MeshRegion
        {
            public string @default;            // id of the option that starts active in this slot
            public MeshRegionOption[] options; // independently-selectable meshes for this slot
        }

        [System.Serializable] private sealed class MeshRegionOption
        {
            public string id;
            public string label;
            public CharPart part; // the single region mesh
        }

        [System.Serializable] private sealed class HairCatalog
        {
            public string @default;      // id of the style that starts active ("" = no hair)
            public HairStyle[] options;
        }

        [System.Serializable] private sealed class HairStyle
        {
            public string id;
            public string label;
            public CharPart part;        // the hair mesh (skinned to the shared skeleton)
            public HairColor[] colors;   // colour swatches — a diffuse texture swap
            public string defaultColor;  // id of the colour to start on
        }

        [System.Serializable] private sealed class HairColor
        {
            public string id;
            public string label;
            public string diffuse;       // relpath to the baked RGBA diffuse (alpha = strand silhouette)
        }

        [System.Serializable] private sealed class ClothingCatalog
        {
            public ClothingSlot top;
            public ClothingSlot bottom;
            public ClothingSlot full;
            public ClothingSlot shoes;
            public ClothingSlot socks;
            public ClothingSlot bra;     // underwear LAYER (composites under top/full)
            public ClothingSlot panties; // underwear LAYER (composites under bottom/full)
            public ClothingSlot tights;  // SKIN texture layer (legs; no mesh)
            public ClothingSlot glasses;
            public ClothingSlot earrings;
            public ClothingSlot necklace;
            public ClothingSlot gloves;
            public ClothingSlot wristl;
            public ClothingSlot wristr;
            public ClothingSlot lipstick;  // SKIN layer (face)
            public ClothingSlot eyeshadow; // SKIN layer (face)
            public ClothingSlot eyeliner;  // SKIN layer (face)
            public ClothingSlot blush;     // SKIN layer (face)
            public ClothingSlot brows;     // SKIN layer (face; unisex "yu" parts, colours = hair colours)
            public ClothingSlot eyelashes; // SKIN layer (face)
        }

        [System.Serializable] private sealed class ClothingSlot
        {
            public string @default;         // id of the garment that starts worn ("" = none)
            public ClothingOption[] options;
        }

        [System.Serializable] private sealed class ClothingOption
        {
            public string id;
            public string label;
            public string category;         // top | bottom | full | shoes
            public string[] covers;         // nude regions hidden while worn: top | bottom | feet
            public CharPart[] parts;        // garment mesh(es); parts[0] is the colour-swap primary
            public HairColor[] colors;      // {id,label,diffuse} — composited (shirt over skin) diffuses
            public string defaultColor;
        }

        [System.Serializable] private sealed class SkinBaseCatalog
        {
            public string @default;        // id of the base skin that starts active
            public SkinBaseEntry[] options;
        }

        [System.Serializable] private sealed class SkinNormalCatalog
        {
            public string @default;        // id of the skin-detail normal that starts active
            public SkinNormalEntry[] options;
        }

        [System.Serializable] private sealed class SkinNormalEntry
        {
            public string id;
            public string label;
            public string normal;          // relative path to the normal PNG; "" = Flat (no detail)
        }

        [System.Serializable] private sealed class SkinBaseEntry
        {
            public string id;
            public string label;
            public string albedo; // relpath to the full-color albedo PNG
        }

        // ---- skinCatalog DTOs (JsonUtility-compatible) ---------------------------------------
        [System.Serializable] private sealed class SkinCatalog
        {
            public SkinCatalogEntry[] baseTones;
            public SkinCatalogEntry[] detailLayers; // 'blend': "overlay" (grayscale relief) | "over" (colored wash)
            public SkinCatalogEntry[] eyeColors;
            public SkinCatalogDefault @default;
        }

        [System.Serializable] private sealed class SkinCatalogEntry
        {
            public string id;
            public string label;
            public string texture; // relpath from the asset folder, forward slashes
            public string blend;   // detailLayers only: "overlay"
        }

        [System.Serializable] private sealed class SkinCatalogDefault
        {
            public string baseTone;
            public string[] detailLayers; // ids that start active
            public string eyeColor;
        }

        [System.Serializable] private sealed class CharBone
        {
            public string name;
            public string parentName;
            public float[] bindPose;        // 16, row-major, world (row-vector convention)
            public float[] inverseBindPose; // unused (bindposes derived from transforms)
        }

        [System.Serializable] private sealed class CharPart
        {
            public string name;
            public string region;
            public CharMesh mesh;
            public CharMat material;
        }

        [System.Serializable] private sealed class CharMesh
        {
            public int vertexCount;
            public float[] positions;   // 3*N
            public float[] normals;     // 3*N (EA authored — continuous across seams; optional)
            public float[] uv0;         // 2*N
            public float[] uv1;         // 2*N (optional)
            public int[] triangles;
            public int[] boneIndices;   // 4*N → index into skeleton[]
            public float[] boneWeights; // 4*N normalized
            public CharBlendShape[] blendShapes; // morph sliders (sparse deltas); optional
        }

        [System.Serializable] private sealed class CharBlendShape
        {
            public string name;
            public int[] indices;          // moved vertex indices
            public float[] deltaPositions; // 3 floats per index
        }

        [System.Serializable] private sealed class CharMat
        {
            public string diffuse;
            public string normal;
            public string specular;
            public string shadow;
        }

        [MenuItem("Sims4 Creator/Character/Create Female Character (Customizer)", priority = 1)]
        public static void BuildCustomizer() => BuildCustomizerSlug("af_char", "Female");

        [MenuItem("Sims4 Creator/Character/Create Male Character (Customizer)", priority = 2)]
        public static void BuildCustomizerMale() => BuildCustomizerSlug("am_char", "Male");

        /// <summary>Build the customizer character for a given exported slug (af_char / am_char / ...).</summary>
        public static Sims4Character BuildCustomizerSlug(string slug, string label)
        {
            string json = $"{Root}/{slug}/character.json";
            if (!File.Exists(json))
            {
                Debug.LogWarning($"[Sims4Creator] {json} not found — export it first (exportsim Adult {label} default {slug}).");
                return null;
            }

            // Remove previously-built roots for THIS slug so repeated clicks don't pile up duplicates.
            int removed = 0;
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go != null && go.transform.parent == null &&
                    go.name.StartsWith(slug, System.StringComparison.Ordinal))
                { UnityEngine.Object.DestroyImmediate(go); removed++; }

            try { BuildOne(json.Replace('\\', '/')); }
            catch (System.Exception ex) { Debug.LogError($"[Sims4Creator] {label} customizer build FAILED: {ex}"); return null; }

            var built = GameObject.Find(slug);
            if (built != null)
            {
                built.name = slug + " (Customizer)";
                Selection.activeGameObject = built;
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Sims4Creator] {label} character customizer ready (removed {removed} previous).");
            return built != null ? built.GetComponent<Sims4Character>() : null;
        }

        [MenuItem("Sims4 Creator/Character/Build All Characters (debug)", priority = 20)]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                Debug.LogWarning($"[Sims4Creator] {Root} not found.");
                return;
            }

            var built = 0;
            foreach (var path in Directory.GetFiles(Root, "character.json", SearchOption.AllDirectories))
            {
                try
                {
                    BuildOne(path.Replace('\\', '/'));
                    built++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Sims4Creator] Character build FAILED for '{path}': {ex}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Sims4Creator] Built {built} character(s) from character.json.");
        }

        private static void BuildOne(string jsonPath)
        {
            var dir = Path.GetDirectoryName(jsonPath).Replace('\\', '/');
            var manifest = JsonUtility.FromJson<CharManifest>(File.ReadAllText(jsonPath));
            if (manifest?.skeleton == null || manifest.skeleton.Length == 0 || manifest.parts == null)
            {
                Debug.LogWarning($"[Sims4Creator] {jsonPath}: invalid character.json — skipped.");
                return;
            }

            var assetName = string.IsNullOrEmpty(manifest.asset) ? Path.GetFileName(dir) : manifest.asset;

            // ---- Shared skeleton (parent-relative locals; transpose; placeholder for missing parents) ----
            var root = new GameObject(assetName);
            var bones = manifest.skeleton;
            var boneTransforms = new Transform[bones.Length];
            var byName = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
            var nameToIndex = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var worldMats = new Matrix4x4[bones.Length];

            for (var i = 0; i < bones.Length; i++)
            {
                worldMats[i] = RowMajorToMatrix(bones[i].bindPose);
                var go = new GameObject(bones[i].name);
                boneTransforms[i] = go.transform;
                if (!byName.ContainsKey(bones[i].name))
                {
                    byName[bones[i].name] = go.transform;
                    nameToIndex[bones[i].name] = i;
                }
            }

            Transform rootBone = null;
            for (var i = 0; i < bones.Length; i++)
            {
                var parentName = bones[i].parentName;
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
                        parentWorld = worldMats[pIdx];
                    }
                }
                else
                {
                    var placeholder = new GameObject(parentName);
                    placeholder.transform.SetParent(root.transform, worldPositionStays: false);
                    byName[parentName] = placeholder.transform;
                    parentT = placeholder.transform;
                    rootBone ??= placeholder.transform;
                }

                var local = parentWorld.inverse * worldMats[i];
                var t = boneTransforms[i];
                t.localPosition = local.GetColumn(3);
                t.localRotation = local.rotation;
                t.localScale = local.lossyScale;
                t.SetParent(parentT, worldPositionStays: false);
            }

            rootBone ??= boneTransforms[0];

            // Bindposes derived from the built shared skeleton (mesh GameObjects sit at root identity,
            // so localToWorld is identity → bindpose[i] = bone.worldToLocalMatrix). Same for every part.
            var bindposes = new Matrix4x4[bones.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                bindposes[i] = boneTransforms[i].worldToLocalMatrix;
            }

            // ---- Parts ----
            var body = root.AddComponent<Sims4Body>();
            var hasCatalog = manifest.skinCatalog?.baseTones is { Length: > 0 };
            // Skin parts (body/head/eyes share the EA body atlas) get the ONE shared, recomposed skin
            // material when a skinCatalog is present. Collected across shared + variant parts.
            var skinRenderers = new List<SkinnedMeshRenderer>();
            var skinNormalRel = new List<string>(); // first skin part's normal relpath (one shared)

            // SHARED parts (head/eyes/brows) — directly under root, always visible.
            foreach (var part in manifest.parts)
                BuildPart(part, dir, assetName, root.transform, boneTransforms, rootBone, bindposes,
                    bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);

            // BODY-MESH region slots (top/bottom/feet) — one GameObject per option per slot, all on
            // the shared skeleton. Each slot is an INDEPENDENT dropdown.
            var meshCat = manifest.meshCatalog;
            Debug.Log($"[Sims4Creator] meshCatalog parse: top={meshCat?.top?.options?.Length ?? 0} " +
                      $"bottom={meshCat?.bottom?.options?.Length ?? 0} feet={meshCat?.feet?.options?.Length ?? 0}.");
            var topV = BuildRegion(meshCat?.top, "top", dir, assetName, root, boneTransforms, rootBone,
                bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
            var botV = BuildRegion(meshCat?.bottom, "bottom", dir, assetName, root, boneTransforms, rootBone,
                bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
            var feetV = BuildRegion(meshCat?.feet, "feet", dir, assetName, root, boneTransforms, rootBone,
                bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);

            // ---- Skin catalog: one shared recomposed material + a Sims4Character on the root ----
            if (hasCatalog && skinRenderers.Count > 0)
            {
                WireSkinCatalog(dir, assetName, root, manifest.skinCatalog, skinRenderers,
                    skinNormalRel.Count > 0 ? skinNormalRel[0] : null);
            }

            // ---- Region slot selectors: activate each slot's default, populate the dropdowns. ----
            var regionCount = topV.list.Count + botV.list.Count + feetV.list.Count;
            if (regionCount > 0)
            {
                var character = root.GetComponent<Sims4Creator.Sims4Character>();
                if (character == null) character = root.AddComponent<Sims4Creator.Sims4Character>();
                character.topVariants = topV.list;
                character.bottomVariants = botV.list;
                character.feetVariants = feetV.list;
                character.SetTopMesh(topV.defaultIdx);
                character.SetBottomMesh(botV.defaultIdx);
                character.SetFeetMesh(feetV.defaultIdx);
                EditorUtility.SetDirty(character);
                Debug.Log($"[Sims4Creator] body region slots populated: top={topV.list.Count} " +
                          $"bottom={botV.list.Count} feet={feetV.list.Count}. The Top/Bottom/Feet dropdowns should now appear.");
            }
            else if (meshCat != null)
            {
                Debug.LogWarning("[Sims4Creator] meshCatalog present but 0 region options built — " +
                                 "body parts missing? No body-mesh dropdowns.");
            }

            // ---- Base SKIN sets: load the color albedos + populate the Base Skin dropdown. ----
            var sbc = manifest.skinBaseCatalog;
            if (sbc?.options is { Length: > 0 })
            {
                var character = root.GetComponent<Sims4Creator.Sims4Character>();
                if (character == null) character = root.AddComponent<Sims4Creator.Sims4Character>();
                character.baseSkins = new List<Sims4Creator.Sims4Character.BaseSkin>();
                var defaultIdx = 0;
                foreach (var o in sbc.options)
                {
                    if (o == null || string.IsNullOrEmpty(o.albedo)) continue;
                    var tex = LoadTex(dir, o.albedo);
                    character.baseSkins.Add(new Sims4Creator.Sims4Character.BaseSkin
                    {
                        id = o.id, label = string.IsNullOrEmpty(o.label) ? o.id : o.label, albedo = tex,
                    });
                    if (!string.IsNullOrEmpty(sbc.@default) &&
                        string.Equals(o.id, sbc.@default, System.StringComparison.OrdinalIgnoreCase))
                    {
                        defaultIdx = character.baseSkins.Count - 1;
                    }
                }
                character.SetBaseSkin(defaultIdx);
                EditorUtility.SetDirty(character);
                Debug.Log($"[Sims4Creator] base skins populated: {character.baseSkins.Count} " +
                          $"(default '{sbc.@default}'). The Base Skin dropdown should now appear.");
            }

            // ---- Skin-detail NORMAL maps: load + populate the Skin Detail (Normal) dropdown. ----
            var snc = manifest.skinNormalCatalog;
            if (snc?.options is { Length: > 0 })
            {
                var character = root.GetComponent<Sims4Creator.Sims4Character>();
                if (character == null) character = root.AddComponent<Sims4Creator.Sims4Character>();
                character.skinNormals = new List<Sims4Creator.Sims4Character.SkinNormal>();
                var defaultIdx = 0;
                foreach (var o in snc.options)
                {
                    if (o == null) continue;
                    var tex = string.IsNullOrEmpty(o.normal) ? null : LoadTex(dir, o.normal); // null = Flat
                    character.skinNormals.Add(new Sims4Creator.Sims4Character.SkinNormal
                    {
                        id = o.id, label = string.IsNullOrEmpty(o.label) ? o.id : o.label, normal = tex,
                    });
                    if (!string.IsNullOrEmpty(snc.@default) &&
                        string.Equals(o.id, snc.@default, System.StringComparison.OrdinalIgnoreCase))
                    {
                        defaultIdx = character.skinNormals.Count - 1;
                    }
                }
                character.SetSkinNormal(defaultIdx);
                EditorUtility.SetDirty(character);
                Debug.Log($"[Sims4Creator] skin normals populated: {character.skinNormals.Count} " +
                          $"(default '{snc.@default}'). The Skin Detail (Normal) dropdown should now appear.");
            }

            // ---- Morph sliders: register blend-shape names + BONE morph components. ----
            {
                var morphChar = root.GetComponent<Sims4Creator.Sims4Character>();
                if (morphChar != null)
                {
                    var morphNames = new List<string>();
                    foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (smr.sharedMesh == null) continue;
                        for (var b = 0; b < smr.sharedMesh.blendShapeCount; b++)
                        {
                            var nm = smr.sharedMesh.GetBlendShapeName(b);
                            if (!morphNames.Contains(nm)) morphNames.Add(nm);
                        }
                    }

                    // BONE components (character.json boneMorphs): scale/offset/rotation adjustments per
                    // slider. Names are merged into the catalog so BONE-ONLY sliders (Chest/Head size,
                    // Chin Forward) get sliders too — they have no blend shape at all.
                    var boneMorphList = new List<Sims4Creator.Sims4Character.BoneMorph>();
                    if (manifest.boneMorphs != null)
                    {
                        foreach (var bmSrc in manifest.boneMorphs)
                        {
                            if (bmSrc == null || string.IsNullOrEmpty(bmSrc.name) || bmSrc.entries == null) continue;
                            var bm = new Sims4Creator.Sims4Character.BoneMorph { name = bmSrc.name };
                            foreach (var e in bmSrc.entries)
                            {
                                bm.entries.Add(new Sims4Creator.Sims4Character.BoneMorphEntry
                                {
                                    bone = e.bone,
                                    offset = new Vector3(e.ox, e.oy, e.oz),
                                    scaleDelta = new Vector3(e.sx, e.sy, e.sz),
                                    rotation = new Quaternion(e.qx, e.qy, e.qz, e.qw).normalized,
                                });
                            }
                            if (bm.entries.Count == 0) continue;
                            boneMorphList.Add(bm);
                            if (!morphNames.Contains(bm.name)) morphNames.Add(bm.name);
                        }
                    }
                    morphChar.boneMorphs = boneMorphList;

                    if (morphNames.Count > 0)
                    {
                        morphNames.Sort(System.StringComparer.OrdinalIgnoreCase);
                        morphChar.SetMorphCatalog(morphNames);
                        EditorUtility.SetDirty(morphChar);
                        Debug.Log($"[Sims4Creator] morph sliders: {morphNames.Count} ({boneMorphList.Count} with bone components).");
                    }
                }
            }

            // ---- HAIR: styles on the shared skeleton; colour = a diffuse swap on the style's material. ----
            var hairCat = manifest.hairCatalog;
            if (hairCat?.options is { Length: > 0 })
            {
                var character = root.GetComponent<Sims4Creator.Sims4Character>();
                if (character == null) character = root.AddComponent<Sims4Creator.Sims4Character>();
                PrepHairTextures(dir); // full-res import + alphaIsTransparency (CC hair alpha fidelity)
                var styles = new List<Sims4Creator.Sims4Character.HairStyle>
                {
                    // Option 0 is always "None" (bald) — no GameObject, no material.
                    new Sims4Creator.Sims4Character.HairStyle { id = "", label = "None" },
                };
                var defaultIdx = 0;
                foreach (var s in hairCat.options)
                {
                    if (s?.part?.mesh == null) continue;
                    var styleGo = new GameObject($"hair_{MakeSafe(s.id)}");
                    styleGo.transform.SetParent(root.transform, worldPositionStays: false);

                    // Colour swatches: load each diffuse; pick the style's default colour to start on.
                    var colorList = new List<Sims4Creator.Sims4Character.HairColor>();
                    Texture2D startDiffuse = null;
                    var colorDefault = 0;
                    if (s.colors != null)
                    {
                        foreach (var c in s.colors)
                        {
                            if (c == null) continue;
                            var tex = LoadTex(dir, c.diffuse);
                            colorList.Add(new Sims4Creator.Sims4Character.HairColor
                            {
                                id = c.id, label = string.IsNullOrEmpty(c.label) ? c.id : c.label, diffuse = tex,
                            });
                            if (!string.IsNullOrEmpty(s.defaultColor) &&
                                string.Equals(c.id, s.defaultColor, System.StringComparison.OrdinalIgnoreCase))
                            {
                                startDiffuse = tex; colorDefault = colorList.Count - 1;
                            }
                        }
                    }
                    if (startDiffuse == null && colorList.Count > 0) startDiffuse = colorList[0].diffuse;

                    var mat = BuildHairMaterial(dir, $"{assetName}_hair_{MakeSafe(s.id)}", startDiffuse);
                    BuildPart(s.part, dir, $"{assetName}_hair_{MakeSafe(s.id)}", styleGo.transform,
                        boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel,
                        body, addToBody: false, overrideMaterial: mat, radialLift: 0.0015f); // 1.5mm off the skin

                    styles.Add(new Sims4Creator.Sims4Character.HairStyle
                    {
                        id = s.id,
                        label = string.IsNullOrEmpty(s.label) ? s.id : s.label,
                        root = styleGo,
                        material = mat,
                        colors = colorList,
                        colorIndex = colorDefault,
                    });
                    if (!string.IsNullOrEmpty(hairCat.@default) &&
                        string.Equals(s.id, hairCat.@default, System.StringComparison.OrdinalIgnoreCase))
                    {
                        defaultIdx = styles.Count - 1;
                    }
                }
                character.hairStyles = styles;
                character.SetHairStyle(defaultIdx);
                EditorUtility.SetDirty(character);
                Debug.Log($"[Sims4Creator] hair populated: {styles.Count - 1} style(s) + None " +
                          $"(default '{hairCat.@default}'). The Hair tab should now list styles + colours.");
            }

            // ---- CLOTHING: 18 slots (wardrobe incl. Bra/Panties under-layers, accessories, makeup) on the shared skeleton; each garment has its OWN opaque
            // material (skin composited under the fabric at export). Selecting a garment hides the nude
            // region(s) it covers. ----
            var clothCat = manifest.clothingCatalog;
            if (clothCat != null)
            {
                var character = root.GetComponent<Sims4Creator.Sims4Character>();
                if (character == null) character = root.AddComponent<Sims4Creator.Sims4Character>();
                // Reimport every clothing texture with alphaIsTransparency=true so its alpha survives to
                // the shader (compressed RGBA can drop it → alpha-clip has nothing to clip → the cutout
                // fill renders). Batched. Also makes them readable for the occlusion UV sampling.
                PrepClothingTextures(dir);
                var (topL, topD) = BuildClothingSlot(clothCat.top, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (botL, botD) = BuildClothingSlot(clothCat.bottom, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (fullL, fullD) = BuildClothingSlot(clothCat.full, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (shoesL, shoesD) = BuildClothingSlot(clothCat.shoes, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (socksL, socksD) = BuildClothingSlot(clothCat.socks, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (braL, braD) = BuildClothingSlot(clothCat.bra, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (pantL, pantD) = BuildClothingSlot(clothCat.panties, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (tightsL, tightsD) = BuildClothingSlot(clothCat.tights, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (glassesL, glassesD) = BuildClothingSlot(clothCat.glasses, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (earringsL, earringsD) = BuildClothingSlot(clothCat.earrings, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (necklaceL, necklaceD) = BuildClothingSlot(clothCat.necklace, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (glovesL, glovesD) = BuildClothingSlot(clothCat.gloves, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (wristlL, wristlD) = BuildClothingSlot(clothCat.wristl, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (wristrL, wristrD) = BuildClothingSlot(clothCat.wristr, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (lipL, lipD) = BuildClothingSlot(clothCat.lipstick, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (shadowL, shadowD) = BuildClothingSlot(clothCat.eyeshadow, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (linerL, linerD) = BuildClothingSlot(clothCat.eyeliner, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (blushL, blushD) = BuildClothingSlot(clothCat.blush, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (browsL, browsD) = BuildClothingSlot(clothCat.brows, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                var (lashL, lashD) = BuildClothingSlot(clothCat.eyelashes, dir, assetName, root, boneTransforms, rootBone, bindposes, bones.Length, hasCatalog, skinRenderers, skinNormalRel, body);
                // (The custom physics cloth skirt is PARKED — code preserved in Parked/cloth-skirt/.)
                character.clothingTop = topL;
                character.clothingBottom = botL;
                character.clothingFull = fullL;
                character.clothingShoes = shoesL;
                character.clothingSocks = socksL;
                character.clothingBra = braL;
                character.clothingPanties = pantL;
                character.clothingTights = tightsL;
                character.clothingGlasses = glassesL;
                character.clothingEarrings = earringsL;
                character.clothingNecklace = necklaceL;
                character.clothingGloves = glovesL;
                character.clothingWristL = wristlL;
                character.clothingWristR = wristrL;
                character.clothingLipstick = lipL;
                character.clothingEyeshadow = shadowL;
                character.clothingEyeliner = linerL;
                character.clothingBlush = blushL;
                character.clothingBrows = browsL;
                character.clothingEyelashes = lashL;
                // Apply defaults last (SetClothing enforces full-vs-top/bottom exclusivity + hides nude regions).
                character.SetClothing(0, topD);
                character.SetClothing(1, botD);
                character.SetClothing(2, fullD);
                character.SetClothing(3, shoesD);
                character.SetClothing(4, socksD);
                character.SetClothing(5, braD);
                character.SetClothing(6, pantD);
                character.SetClothing(7, tightsD);
                character.SetClothing(8, glassesD);
                character.SetClothing(9, earringsD);
                character.SetClothing(10, necklaceD);
                character.SetClothing(11, glovesD);
                character.SetClothing(12, wristlD);
                character.SetClothing(13, wristrD);
                character.SetClothing(14, lipD);
                character.SetClothing(15, shadowD);
                character.SetClothing(16, linerD);
                character.SetClothing(17, blushD);
                character.SetClothing(18, browsD);
                character.SetClothing(19, lashD);
                EditorUtility.SetDirty(character);
                var totalGarments = 0;
                for (var s = 0; s < Sims4Creator.Sims4Character.ClothingSlotCount; s++)
                    totalGarments += Mathf.Max(0, character.GetClothingCount(s) - 1);
                Debug.Log($"[Sims4Creator] clothing populated: {totalGarments} item(s) across all 18 slots (wardrobe/accessories/makeup). The Clothing tab should now appear.");
            }

            // Idle animation. Prefer the REAL decoded game idle (animation.json → looping AnimationClip);
            // if there is no animation.json, fall back to the procedural breathing+sway. Both only run in
            // play mode, so edit-mode posing/inspecting is untouched.
            var attachedRealIdle = Sims4Creator.EditorTools.Sims4AnimationBuilder.TryBuildAndAttachIdle(root, dir, assetName, byName);
            if (!attachedRealIdle && root.GetComponent<Sims4Creator.Sims4IdleAnimator>() == null)
            {
                root.AddComponent<Sims4Creator.Sims4IdleAnimator>();
            }

            Selection.activeGameObject = root;
            Debug.Log($"[Sims4Creator] Built character '{assetName}': {body.parts.Count} part(s), " +
                      $"{bones.Length} bones, root='{rootBone.name}'" +
                      (regionCount > 0 ? $", {regionCount} body-mesh option(s) across 3 slots" : "") +
                      (hasCatalog ? $", skinCatalog: {skinRenderers.Count} skin part(s)." : "."));
        }

        // Build every option GameObject for one region slot (top/bottom/feet) under the root, return
        // the MeshVariant list + the index of the slot's default option.
        private static (List<Sims4Creator.Sims4Character.MeshVariant> list, int defaultIdx) BuildRegion(
            MeshRegion region, string regionKey, string dir, string assetName, GameObject root,
            Transform[] boneTransforms, Transform rootBone, Matrix4x4[] bindposes, int boneCount,
            bool hasCatalog, List<SkinnedMeshRenderer> skinRenderers, List<string> skinNormalRel, Sims4Body body)
        {
            var list = new List<Sims4Creator.Sims4Character.MeshVariant>();
            var defaultIdx = 0;
            if (region?.options == null || region.options.Length == 0)
            {
                return (list, defaultIdx);
            }

            foreach (var opt in region.options)
            {
                if (opt?.part == null) continue;
                var go = new GameObject($"{regionKey}_{MakeSafe(opt.id)}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                BuildPart(opt.part, dir, $"{assetName}_{regionKey}_{MakeSafe(opt.id)}", go.transform,
                    boneTransforms, rootBone, bindposes, boneCount, hasCatalog, skinRenderers, skinNormalRel, body,
                    addToBody: false);
                list.Add(new Sims4Creator.Sims4Character.MeshVariant
                {
                    id = opt.id,
                    label = string.IsNullOrEmpty(opt.label) ? opt.id : opt.label,
                    root = go,
                });
                if (!string.IsNullOrEmpty(region.@default) &&
                    string.Equals(opt.id, region.@default, System.StringComparison.OrdinalIgnoreCase))
                {
                    defaultIdx = list.Count - 1;
                }
            }

            return (list, defaultIdx);
        }

        // Builds one part as a SkinnedMeshRenderer parented to `parent`, on the shared skeleton.
        // (Factored from the original inline loop so body-mesh variants reuse it verbatim.)
        private static SkinnedMeshRenderer BuildPart(
            CharPart part, string dir, string assetPrefix, Transform parent,
            Transform[] boneTransforms, Transform rootBone, Matrix4x4[] bindposes, int boneCount,
            bool hasCatalog, List<SkinnedMeshRenderer> skinRenderers, List<string> skinNormalRel, Sims4Body body,
            bool addToBody = true, Material overrideMaterial = null, float radialLift = 0f)
        {
            if (part?.mesh == null) return null;

            var pm = part.mesh;
            var mesh = new Mesh
            {
                name = part.name,
                indexFormat = pm.vertexCount > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.vertices = ToVec3(pm.positions);
            // HAIR z-FIGHT FIX: EA's hairline/sideburn/nape cards lie coplanar with the face skin (<0.5mm),
            // so they z-fight and flash black as the camera orbits. Lift the whole hair a hair's breadth
            // OUTWARD from the scalp centre so it always wins the depth test — POSITIONS only (no normals),
            // uniform (keeps the silhouette), imperceptible visually but decisive for depth.
            if (radialLift > 0f)
            {
                var rv = mesh.vertices;
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                for (var i = 0; i < rv.Length; i++) { if (rv[i].y < minY) minY = rv[i].y; if (rv[i].y > maxY) maxY = rv[i].y; }
                var cut = Mathf.Lerp(minY, maxY, 0.55f); // scalp cap centroid, so a ponytail/long hair can't drag it down
                Vector3 c = Vector3.zero; var cn = 0;
                for (var i = 0; i < rv.Length; i++) if (rv[i].y >= cut) { c += rv[i]; cn++; }
                if (cn > 0)
                {
                    c /= cn;
                    for (var i = 0; i < rv.Length; i++)
                    {
                        var outward = rv[i] - c;
                        if (outward.sqrMagnitude < 1e-8f) continue;
                        rv[i] += outward.normalized * radialLift;
                    }
                    mesh.vertices = rv;
                }
            }
            if (pm.uv0 is { Length: > 0 }) mesh.uv = ToVec2(pm.uv0);
            if (pm.uv1 is { Length: > 0 }) mesh.uv2 = ToVec2(pm.uv1);
            mesh.triangles = pm.triangles;
            mesh.boneWeights = BuildBoneWeights(pm.boneIndices, pm.boneWeights, pm.vertexCount, boneCount);
            mesh.bindposes = bindposes;

            // EA's AUTHORED normals (continuous across UV seams AND part boundaries) when present.
            if (pm.normals is { Length: > 0 } && pm.normals.Length == pm.positions.Length)
                mesh.normals = ToVec3(pm.normals);
            else
                mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // Morph blend shapes: scatter sparse deltas into full-length frames.
            if (pm.blendShapes != null)
            {
                foreach (var bs in pm.blendShapes)
                {
                    if (bs?.indices == null || bs.deltaPositions == null || string.IsNullOrEmpty(bs.name)) continue;
                    var delta = new Vector3[pm.vertexCount];
                    for (var k = 0; k < bs.indices.Length; k++)
                    {
                        var bvi = bs.indices[k];
                        if (bvi < 0 || bvi >= delta.Length || (k * 3) + 2 >= bs.deltaPositions.Length) continue;
                        delta[bvi] = new Vector3(bs.deltaPositions[k * 3], bs.deltaPositions[(k * 3) + 1], bs.deltaPositions[(k * 3) + 2]);
                    }
                    mesh.AddBlendShapeFrame(bs.name, 100f, delta, null, null);
                }
            }

            var safeName = MakeSafe(part.name);
            var meshPath = $"{dir}/{assetPrefix}_{safeName}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var partGo = new GameObject(part.name);
            partGo.transform.SetParent(parent, worldPositionStays: false);
            var smr = partGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = boneTransforms;
            smr.rootBone = rootBone;
            smr.localBounds = mesh.bounds;
            // Recompute real bounds every frame: the static bind-pose box is wrong for lying/sitting
            // animation clips — Unity frustum-culls the renderer mid-pose (legs vanished when lying).
            // One character on screen → the cost is negligible; correctness matters more here.
            smr.updateWhenOffscreen = true;

            if (overrideMaterial != null)
            {
                // Hair/clothing part with a caller-built material (e.g. the alpha hair material). Skip
                // the shared-skin collection and the opaque BuildMaterial.
                smr.sharedMaterial = overrideMaterial;
            }
            else if (hasCatalog && IsSkinPart(part))
            {
                skinRenderers.Add(smr); // shared skin material assigned after all parts are built
                if (skinNormalRel.Count == 0 && !string.IsNullOrEmpty(part.material?.normal))
                    skinNormalRel.Add(part.material.normal);
            }
            else
            {
                smr.sharedMaterial = BuildMaterial(dir, $"{assetPrefix}_{safeName}", part.material);
            }

            // Region-slot OPTION meshes are NOT registered with Sims4Body: Sims4Character owns their
            // visibility (exactly one per slot active). Registering them would let Sims4Body's region
            // hide/show set renderer.enabled=false on the active option and silently blank a slot.
            if (addToBody)
            {
                body.parts.Add(new Sims4Body.Part
                {
                    name = part.name,
                    region = string.IsNullOrEmpty(part.region) ? "Unknown" : part.region,
                    renderer = smr,
                    visible = true,
                });
            }
            return smr;
        }

        // A part is a "skin" part (driven by the shared body atlas) when its diffuse points at the
        // shared skin atlas/base. Covers the current export (Textures/skin_atlas.png) and the new
        // catalog export (Skin/base_*.png). Clothing/hair parts keep their own per-part material.
        private static bool IsSkinPart(CharPart part)
        {
            var diffuse = part?.material?.diffuse;
            if (string.IsNullOrEmpty(diffuse))
            {
                return false;
            }

            var d = diffuse.Replace('\\', '/').ToLowerInvariant();
            return d.Contains("skin_atlas") || d.Contains("skin/base") || d.Contains("/skin/") || d.StartsWith("skin/");
        }

        // Build the shared skin material, attach + populate a Sims4Character, point every skin
        // renderer at the shared material, and composite the default selection into _BaseColorMap.
        private static void WireSkinCatalog(string dir, string assetName, GameObject root,
            SkinCatalog catalog, List<SkinnedMeshRenderer> skinRenderers, string normalRel)
        {
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Sims4Creator] HDRP/Lit not found — skin catalog skipped.");
                return;
            }

            var matPath = $"{dir}/{assetName}_skin.mat";
            var skinMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (skinMat == null)
            {
                skinMat = new Material(shader);
                AssetDatabase.CreateAsset(skinMat, matPath);
            }

            skinMat.shader = shader;
            skinMat.SetColor("_BaseColor", Color.white);
            // 0.34 (was 0.4): with three directional lights + the skin-detail normal, 0.4 produced tiny
            // hard "plastic" glints. MUST stay in sync with BuildGarmentParityMasks.SkinSmoothness (else
            // the head↔garment neck seam returns).
            skinMat.SetFloat("_Smoothness", 0.28f);
            // Geometric specular AA: damps the micro-highlight sparkle the skin-detail normal map creates.
            skinMat.SetFloat("_EnableGeometricSpecularAA", 1f);
            skinMat.SetFloat("_SpecularAAScreenSpaceVariance", 0.25f);
            skinMat.SetFloat("_SpecularAAThreshold", 0.25f);

            // SUBSURFACE SCATTERING: skin as Lit/SSS with the project's SkinSSS diffusion profile (the
            // profile already existed + is registered in the default volume's Diffusion Profile List, but
            // was never wired to the material — which left the skin flat/waxy). SetMaterialType sets
            // _MaterialID + re-validates keywords; SetDiffusionProfile writes the profile hash + guid ref.
            var sssProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.HighDefinition.DiffusionProfileSettings>("Assets/SkinSSS.asset");
            if (sssProfile != null)
            {
                // Transmission OFF (SSS blur stays): it evaluates thickness/shadows differently on the
                // closed head vs a garment's open thin shell — a structural asymmetry that renders as a
                // tone + shadow seam at the neck ring. Must be OFF on skin AND garments alike.
                skinMat.SetFloat("_TransmissionEnable", 0f);
                UnityEngine.Rendering.HighDefinition.HDMaterial.SetMaterialType(skinMat, UnityEngine.Rendering.HighDefinition.MaterialId.LitSSS);
                UnityEngine.Rendering.HighDefinition.HDMaterial.SetDiffusionProfile(skinMat, sssProfile);
            }
            else
            {
                // The material asset persists across builds — actively reset so a stale SSS conversion
                // (with a now-dangling profile hash) can't survive the profile's removal.
                UnityEngine.Rendering.HighDefinition.HDMaterial.SetMaterialType(skinMat, UnityEngine.Rendering.HighDefinition.MaterialId.LitStandard);
                Debug.LogWarning("[Sims4Creator] Assets/SkinSSS.asset not found — skin reset to standard Lit (no SSS).");
            }

            // NOTE: the OLD albedo/relief-derived normal (skin_detail_normal.png, v2a) baked WildGuy's
            // face features (brows) + UV seams into the surface → artifacts. GATED OFF. The real EA
            // skin-detail normals now drive _NormalMap via the Skin Detail (Normal) dropdown
            // (Sims4Character.SetSkinNormal, populated from skinNormalCatalog) — height→normal from a real
            // EA Sculpt bumpmap, no albedo features. The normalfromrelief tool stays for that bake.
            _ = normalRel; // (legacy param retained; normal now owned by the dropdown)

            EditorUtility.SetDirty(skinMat);

            // Assign the shared material to every skin renderer (all slots).
            foreach (var smr in skinRenderers)
            {
                var slots = Mathf.Max(1, smr.sharedMaterials.Length);
                var arr = new Material[slots];
                for (var i = 0; i < slots; i++)
                {
                    arr[i] = skinMat;
                }

                smr.sharedMaterials = arr;
            }

            // Populate the Sims4Character from the catalog (default selection).
            var character = root.GetComponent<Sims4Creator.Sims4Character>();
            if (character == null)
            {
                character = root.AddComponent<Sims4Creator.Sims4Character>();
            }

            character.skinMaterial = skinMat;
            character.baseTones = BuildBaseTones(dir, catalog, out var defaultBaseIdx);
            character.eyeColors = BuildEyeColors(dir, catalog, out var defaultEyeIdx);
            character.detailLayers = BuildDetailLayers(dir, catalog);

            // Apply the default selection (SetBaseTone/SetEyeColor recompose; the last call paints it).
            character.SetBaseTone(defaultBaseIdx);
            character.SetEyeColor(defaultEyeIdx);
            // Recompose once more after all selections are set so detail defaults are folded in.
            character.Recompose();

            EditorUtility.SetDirty(character);
        }

        private static List<Sims4Creator.Sims4Character.BaseTone> BuildBaseTones(
            string dir, SkinCatalog catalog, out int defaultIndex)
        {
            var list = new List<Sims4Creator.Sims4Character.BaseTone>();
            defaultIndex = 0;
            var defId = catalog.@default?.baseTone;
            var src = catalog.baseTones ?? System.Array.Empty<SkinCatalogEntry>();
            for (var i = 0; i < src.Length; i++)
            {
                var e = src[i];
                list.Add(new Sims4Creator.Sims4Character.BaseTone
                {
                    id = e.id,
                    label = e.label,
                    texture = LoadTex(dir, e.texture),
                });
                if (!string.IsNullOrEmpty(defId) &&
                    string.Equals(e.id, defId, System.StringComparison.OrdinalIgnoreCase))
                {
                    defaultIndex = i;
                }
            }

            return list;
        }

        private static List<Sims4Creator.Sims4Character.EyeColor> BuildEyeColors(
            string dir, SkinCatalog catalog, out int defaultIndex)
        {
            var list = new List<Sims4Creator.Sims4Character.EyeColor>();
            defaultIndex = 0;
            var defId = catalog.@default?.eyeColor;
            var src = catalog.eyeColors ?? System.Array.Empty<SkinCatalogEntry>();
            for (var i = 0; i < src.Length; i++)
            {
                var e = src[i];
                list.Add(new Sims4Creator.Sims4Character.EyeColor
                {
                    id = e.id,
                    label = e.label,
                    texture = LoadTex(dir, e.texture),
                });
                if (!string.IsNullOrEmpty(defId) &&
                    string.Equals(e.id, defId, System.StringComparison.OrdinalIgnoreCase))
                {
                    defaultIndex = i;
                }
            }

            return list;
        }

        private static List<Sims4Creator.Sims4Character.DetailLayer> BuildDetailLayers(
            string dir, SkinCatalog catalog)
        {
            var list = new List<Sims4Creator.Sims4Character.DetailLayer>();
            var src = catalog.detailLayers ?? System.Array.Empty<SkinCatalogEntry>();
            var defaults = catalog.@default?.detailLayers ?? System.Array.Empty<string>();
            foreach (var e in src)
            {
                var active = false;
                foreach (var d in defaults)
                {
                    if (string.Equals(d, e.id, System.StringComparison.OrdinalIgnoreCase))
                    {
                        active = true;
                        break;
                    }
                }

                list.Add(new Sims4Creator.Sims4Character.DetailLayer
                {
                    id = e.id,
                    label = e.label,
                    texture = LoadTex(dir, e.texture),
                    blend = string.IsNullOrEmpty(e.blend) ? "overlay" : e.blend, // "over" = colored source-over (PsBoss)
                    active = active,
                });
            }

            return list;
        }

        // TS4 bind matrices are row-vector (translation in the LAST ROW) → TRANSPOSE for Unity.
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
                weights[v] = new BoneWeight
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

        // UVs: flip V. The Sims atlas/UVs are authored v=0 at TOP, but Unity samples v=0 at BOTTOM.
        // The OBJ importer auto-flips V for us; this direct-Mesh path must do it explicitly, or the
        // texture maps vertically mirrored (body samples the face/eye region → "eye on the thigh").
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

        private static Material BuildMaterial(string dir, string baseName, CharMat mat)
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
                // The normal map is DERIVED from the diffuse luminance, so at 1.0 it double-counts
                // the baked detail (crepey/harsh) and at 0.25 it's nearly flat. 0.5 = subtle relief.
                // Real fix is to derive a SOFTER normal (from the detail layer, blurred) — exporter side.
                material.SetFloat("_NormalScale", 0.5f);
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetFloat("_Smoothness", 0.4f);

            var matPath = $"{dir}/{baseName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
            {
                AssetDatabase.DeleteAsset(matPath);
            }

            AssetDatabase.CreateAsset(material, matPath);
            return material;
        }

        // Hair material: HDRP/Lit with ALPHA CLIP (opaque queue → no transparency-sort artefacts) and
        // DOUBLE-SIDED (hair cards are one-sided planes; both faces must render). The diffuse's alpha
        // channel is the strand silhouette. Colour is a runtime swap of _BaseColorMap (see Sims4Character).
        // Normal/spec are intentionally omitted for now (EA hair packs them in swizzled channels that need
        // dedicated decoding, like the skin-detail normals) — diffuse-only reads clean.
        private static Material BuildHairMaterial(string dir, string baseName, Texture2D diffuse)
        {
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null) return null;

            var m = new Material(shader);
            m.SetColor("_BaseColor", Color.white);
            if (diffuse != null) m.SetTexture("_BaseColorMap", diffuse);

            // Alpha clipping. HDMaterial.ValidateMaterial(m) at the end actually engages the HDRP pass.
            m.SetFloat("_AlphaCutoffEnable", 1f);
            // 0.28 (was 0.4): CC "alpha hairs" (Stealthic etc.) carry soft root/crown blends in the
            // 0.2-0.4 alpha band — a 0.4 cutoff deletes them wholesale (bald crown hole on Vapor).
            m.SetFloat("_AlphaCutoff", 0.28f);
            m.SetFloat("_AlphaSrcBlend", 1f);
            m.SetFloat("_AlphaDstBlend", 0f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = 2450; // HDRP AlphaTest (opaque) queue

            // Double-sided.
            m.SetFloat("_DoubleSidedEnable", 1f);
            m.SetFloat("_DoubleSidedNormalMode", 1f); // Mirror
            m.SetFloat("_CullMode", 0f);              // Off
            m.SetFloat("_CullModeForward", 0f);       // Off
            m.EnableKeyword("_DOUBLESIDED_ON");
            m.doubleSidedGI = true;

            m.SetFloat("_Smoothness", 0.35f);

            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m); // engage the HDRP alpha-clip pass
            var matPath = $"{dir}/{baseName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(m, matPath);
            return m;
        }

        // Build one wardrobe slot: index 0 is always "None", then one ClothingItem per garment.
        private static (List<Sims4Creator.Sims4Character.ClothingItem> list, int defaultIdx) BuildClothingSlot(
            ClothingSlot slot, string dir, string assetName, GameObject root,
            Transform[] boneTransforms, Transform rootBone, Matrix4x4[] bindposes, int boneCount,
            bool hasCatalog, List<SkinnedMeshRenderer> skinRenderers, List<string> skinNormalRel, Sims4Body body)
        {
            var list = new List<Sims4Creator.Sims4Character.ClothingItem>
            {
                new Sims4Creator.Sims4Character.ClothingItem { id = "", label = "None" }, // index 0 = None
            };
            var defaultIdx = 0;
            if (slot?.options != null)
            {
                foreach (var o in slot.options)
                {
                    var it = BuildClothingItem(o, dir, assetName, root, boneTransforms, rootBone, bindposes, boneCount, hasCatalog, skinRenderers, skinNormalRel, body);
                    if (it == null) continue;
                    list.Add(it);
                    if (!string.IsNullOrEmpty(slot.@default) && string.Equals(o.id, slot.@default, System.StringComparison.OrdinalIgnoreCase))
                        defaultIdx = list.Count - 1;
                }
            }
            return (list, defaultIdx);
        }

        // Build one garment: a GameObject holding its mesh part(s) on the shared skeleton, each with its
        // own opaque clothing material. parts[0] is the colour-swap primary (its material takes the swatches).
        private static Sims4Creator.Sims4Character.ClothingItem BuildClothingItem(
            ClothingOption o, string dir, string assetName, GameObject root,
            Transform[] boneTransforms, Transform rootBone, Matrix4x4[] bindposes, int boneCount,
            bool hasCatalog, List<SkinnedMeshRenderer> skinRenderers, List<string> skinNormalRel, Sims4Body body)
        {
            if (o == null) return null;
            // TEXTURE-LAYER item (tights/socks): no mesh — colours only; the runtime composites the
            // selected diffuse into the live skin atlas. root/material stay null.
            if (o.parts == null || o.parts.Length == 0)
            {
                if (o.colors == null || o.colors.Length == 0) return null;
                var layerColors = new List<Sims4Creator.Sims4Character.HairColor>();
                var layerDefault = 0;
                foreach (var c in o.colors)
                {
                    if (c == null) continue;
                    var tex = LoadTex(dir, c.diffuse);
                    layerColors.Add(new Sims4Creator.Sims4Character.HairColor
                    {
                        id = c.id, label = string.IsNullOrEmpty(c.label) ? c.id : c.label, diffuse = tex,
                    });
                    if (!string.IsNullOrEmpty(o.defaultColor) && string.Equals(c.id, o.defaultColor, System.StringComparison.OrdinalIgnoreCase))
                        layerDefault = layerColors.Count - 1;
                }
                if (layerColors.Count == 0) return null;
                return new Sims4Creator.Sims4Character.ClothingItem
                {
                    id = o.id,
                    label = string.IsNullOrEmpty(o.label) ? o.id : o.label,
                    category = o.category,
                    covers = o.covers ?? System.Array.Empty<string>(),
                    root = null,
                    material = null,
                    fabricMask = null,
                    colors = layerColors,
                    colorIndex = layerDefault,
                };
            }
            var go = new GameObject($"cloth_{MakeSafe(o.id)}");
            go.transform.SetParent(root.transform, worldPositionStays: false);

            var colorList = new List<Sims4Creator.Sims4Character.HairColor>();
            Texture2D startDiffuse = null;
            var colorDefault = 0;
            if (o.colors != null)
            {
                foreach (var c in o.colors)
                {
                    if (c == null) continue;
                    var tex = LoadTex(dir, c.diffuse);
                    colorList.Add(new Sims4Creator.Sims4Character.HairColor
                    {
                        id = c.id, label = string.IsNullOrEmpty(c.label) ? c.id : c.label, diffuse = tex,
                    });
                    if (!string.IsNullOrEmpty(o.defaultColor) && string.Equals(c.id, o.defaultColor, System.StringComparison.OrdinalIgnoreCase))
                    { startDiffuse = tex; colorDefault = colorList.Count - 1; }
                }
            }
            if (startDiffuse == null && colorList.Count > 0) startDiffuse = colorList[0].diffuse;

            // Fabric-shape mask for runtime UV-alpha body occlusion: any colour's diffuse alpha works
            // (the fabric silhouette is identical across colours). Make it CPU-readable so the runtime
            // can GetPixelBilinear it.
            var fabricMask = colorList.Count > 0 ? colorList[0].diffuse : startDiffuse;
            EnsureReadable(fabricMask);

            Material primaryMat = null;
            for (var pi = 0; pi < o.parts.Length; pi++)
            {
                var part = o.parts[pi];
                if (part?.mesh == null) continue;
                Material mat = pi == 0
                    ? (primaryMat = BuildClothingMaterial(dir, $"{assetName}_cloth_{MakeSafe(o.id)}", startDiffuse))
                    : BuildClothingMaterial(dir, $"{assetName}_cloth_{MakeSafe(o.id)}_p{pi}", LoadTex(dir, part.material?.diffuse));
                BuildPart(part, dir, $"{assetName}_cloth_{MakeSafe(o.id)}_p{pi}", go.transform,
                    boneTransforms, rootBone, bindposes, boneCount, hasCatalog, skinRenderers, skinNormalRel,
                    body, addToBody: false, overrideMaterial: mat);
            }

            return new Sims4Creator.Sims4Character.ClothingItem
            {
                id = o.id,
                label = string.IsNullOrEmpty(o.label) ? o.id : o.label,
                category = o.category,
                covers = o.covers ?? System.Array.Empty<string>(),
                root = go,
                material = primaryMat,
                fabricMask = fabricMask,
                colors = colorList,
                colorIndex = colorDefault,
            };
        }

        // Batch-set alphaIsTransparency + isReadable on every texture under <char>/Clothing so their alpha
        // reaches the shader (alpha-clip) and the CPU (occlusion). One reimport pass via Start/StopAssetEditing.
        private static void PrepHairTextures(string dir)
        {
            var folder = $"{dir}/Hair";
            if (!AssetDatabase.IsValidFolder(folder)) return;
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;
                    var dirty = false;
                    if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
                    if (imp.maxTextureSize < 4096) { imp.maxTextureSize = 4096; dirty = true; } // CC hair ships 2048x4096
                    if (dirty) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }

        private static void PrepClothingTextures(string dir)
        {
            var folder = $"{dir}/Clothing";
            if (!AssetDatabase.IsValidFolder(folder)) return;
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;
                    var dirty = false;
                    if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
                    if (!imp.isReadable) { imp.isReadable = true; dirty = true; }
                    if (dirty) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }

        // Ensure a just-imported texture is CPU-readable (for runtime GetPixelBilinear in the occlusion).
        private static void EnsureReadable(Texture2D tex)
        {
            if (tex == null) return;
            var path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;
            if (AssetImporter.GetAtPath(path) is TextureImporter imp && !imp.isReadable)
            {
                imp.isReadable = true;
                imp.SaveAndReimport();
            }
        }

        // Clothing material: OPAQUE HDRP/Lit. The garment is a single lit mesh whose base map is a runtime
        // composite of fabric OVER the live skin (fabric alpha is a skin/fabric mask consumed by the
        // composite blit, never output as opacity). No alpha-clip: nothing to clip, and clipping would
        // re-introduce the polygonal edge.
        //
        // SSS PARITY: the garment mesh CONTAINS baked exposed-skin faces (neck/chest/arms). The head/nude
        // regions render with subsurface scattering, so if the garment shades as standard Lit its skin areas
        // read flat next to the face (a visible seam at the neck). Fix: the garment is ALSO Lit/SSS with the
        // same SkinSSS profile, but masked per-texel — _SubsurfaceMaskMap = INVERTED fabric alpha, so skin
        // texels scatter exactly like the head while fabric texels stay effectively standard.
        private static Material BuildClothingMaterial(string dir, string baseName, Texture2D diffuse)
        {
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null) return null;
            var m = new Material(shader);
            m.SetColor("_BaseColor", Color.white);
            if (diffuse != null) m.SetTexture("_BaseColorMap", diffuse);

            // Double-sided: some EA garment shells are single-surface (open collars/hems) — render both faces.
            m.SetFloat("_DoubleSidedEnable", 1f);
            m.SetFloat("_DoubleSidedNormalMode", 1f);
            m.SetFloat("_CullMode", 0f);
            m.SetFloat("_CullModeForward", 0f);
            m.EnableKeyword("_DOUBLESIDED_ON");
            m.doubleSidedGI = true;

            m.SetFloat("_Smoothness", 0.3f);
            // Specular-AA parity with the skin material (both sides of the neck ring must damp equally).
            m.SetFloat("_EnableGeometricSpecularAA", 1f);
            m.SetFloat("_SpecularAAScreenSpaceVariance", 0.25f);
            m.SetFloat("_SpecularAAThreshold", 0.25f);

            // Persist FIRST: SetDiffusionProfile registers external refs via AddObjectToAsset, which
            // requires the material to already be a saved asset (throws on an in-memory material).
            var matPath = $"{dir}/{baseName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(m, matPath);

            var sssProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.HighDefinition.DiffusionProfileSettings>("Assets/SkinSSS.asset");
            Texture2D sssMask = null, maskMap = null;
            if (diffuse != null && sssProfile != null) (sssMask, maskMap) = BuildGarmentParityMasks(dir, baseName, diffuse);
            if (sssMask != null)
            {
                m.SetFloat("_TransmissionEnable", 0f); // parity with skin: transmission off (see WireSkinCatalog)
                UnityEngine.Rendering.HighDefinition.HDMaterial.SetMaterialType(m, UnityEngine.Rendering.HighDefinition.MaterialId.LitSSS);
                UnityEngine.Rendering.HighDefinition.HDMaterial.SetDiffusionProfile(m, sssProfile);
                m.SetTexture("_SubsurfaceMaskMap", sssMask); // R = 1-alpha: skin scatters, fabric doesn't
                // MaskMap A is a SELECTOR (0 = baked skin, 1 = fabric); the remap bounds are the two
                // gloss endpoints: smoothness = lerp(RemapMin=skin, RemapMax=fabric, A). The runtime
                // Skin Gloss slider retargets ONLY RemapMin, so body skin and the garment's baked-skin
                // texels move together while the fabric keeps its own gloss.
                m.SetTexture("_MaskMap", maskMap);
                m.SetFloat("_SmoothnessRemapMin", GarmentSkinSmoothness);
                m.SetFloat("_SmoothnessRemapMax", GarmentFabricSmoothness);
            }

            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // The two gloss endpoints garments are built with. RemapMin (skin) MUST match the skin
        // material's default _Smoothness; the runtime Skin Gloss slider retargets RemapMin live.
        private const float GarmentSkinSmoothness = 0.28f;
        private const float GarmentFabricSmoothness = 0.28f;

        // Per-texel parity masks for a garment, derived from the fabric alpha (1 = fabric, 0 = baked skin):
        // - SSS mask (R = 1-alpha): skin texels scatter like the head; fabric doesn't.
        // - Lit MaskMap (R=metallic 0, G=AO 1, B=0, A = the RAW fabric alpha — a skin-vs-fabric
        //   SELECTOR, not a final smoothness). Effective smoothness = lerp(RemapMin, RemapMax, A),
        //   so the material's remap bounds carry the skin/fabric gloss values and the Skin Gloss
        //   slider can move the skin end alone (fabric gloss unaffected).
        // Saved as texture assets next to the material. Returns nulls if unreadable or all-fabric.
        private static (Texture2D sss, Texture2D mask) BuildGarmentParityMasks(string dir, string baseName, Texture2D diffuse)
        {
            Color32[] src;
            try { src = diffuse.GetPixels32(); }
            catch { return (null, null); } // not readable → skip SSS (garment stays standard Lit)
            var sssPx = new Color32[src.Length];
            var maskPx = new Color32[src.Length];
            var anySkin = false;
            for (var i = 0; i < src.Length; i++)
            {
                var inv = (byte)(255 - src[i].a);
                sssPx[i] = new Color32(inv, inv, inv, 255);
                maskPx[i] = new Color32(0, 255, 0, src[i].a); // A: 0 = baked skin, 255 = fabric
                if (inv > 32) anySkin = true;
            }
            if (!anySkin) return (null, null); // fully-opaque garment (e.g. most shoes): keep standard Lit

            Texture2D Bake(Color32[] px, string suffix)
            {
                var t = new Texture2D(diffuse.width, diffuse.height, TextureFormat.RGBA32, true, linear: true);
                t.SetPixels32(px);
                t.Apply(updateMipmaps: true);
                var path = $"{dir}/{baseName}{suffix}.asset";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(t, path);
                return t;
            }
            return (Bake(sssPx, "_sssmask"), Bake(maskPx, "_maskmap"));
        }

        private static Texture2D LoadTex(string dir, string rel)
        {
            if (string.IsNullOrEmpty(rel))
            {
                return null;
            }

            var path = $"{dir}/{rel}";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null && File.Exists(path))
            {
                // Texture not in the AssetDatabase yet (just-written export) — force a sync import.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            if (tex == null)
            {
                Debug.LogWarning($"[Sims4Creator] Skin texture not found: '{path}'.");
            }

            return tex;
        }

        private static string MakeSafe(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "part";
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name;
        }
    }
}
