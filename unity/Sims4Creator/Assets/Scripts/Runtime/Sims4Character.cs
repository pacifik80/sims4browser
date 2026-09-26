using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// Live skin recomposition for a built Sims character. Holds the skin catalog the exporter wrote
    /// (base tones, grayscale detail layers, eye colors) plus the current selection, and on any change
    /// recomposes the skin albedo into a <see cref="RenderTexture"/> via <see cref="SkinCompositor"/>
    /// and assigns it to the shared skin material's <c>_BaseColorMap</c>.
    ///
    /// All skin textures share the EA body-atlas UV layout (the meshes' uv0), so a single composited
    /// atlas drives every skin part. The normal map (<c>_NormalMap</c>) is left as the static derived
    /// skin_normal — only the base color is recomposed here.
    ///
    /// [ExecuteAlways] so the recompose runs in edit mode (Inspector dropdowns) and at runtime
    /// (<see cref="SetBaseTone"/>/<see cref="ToggleDetail"/>/<see cref="SetEyeColor"/>).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Sims4 Creator/Sims4 Character")]
    public sealed class Sims4Character : MonoBehaviour
    {
        [Serializable]
        public sealed class BaseTone
        {
            public string id;
            public string label;
            public Texture2D texture; // RGBA color base, UV-aligned
        }

        [Serializable]
        public sealed class DetailLayer
        {
            public string id;
            public string label;
            public Texture2D texture; // "overlay": grayscale relief in .r, coverage in .a; "over": colored wash in .rgb
            public string blend;      // "overlay" (grayscale relief) | "over" (colored source-over, e.g. PsBoss)
            public bool active;       // current on/off
        }

        [Serializable]
        public sealed class EyeColor
        {
            public string id;
            public string label;
            public Texture2D texture; // RGBA overlay, transparent except iris island
        }

        [Serializable]
        public sealed class MeshVariant
        {
            public string id;
            public string label;
            public GameObject root; // the per-variant body group; toggled active/inactive
        }

        [Serializable]
        public sealed class HairColor
        {
            public string id;
            public string label;
            public Texture2D diffuse; // baked RGBA (alpha = strand silhouette)
        }

        [Serializable]
        public sealed class HairStyle
        {
            public string id;
            public string label;
            public GameObject root;      // the hair mesh group; toggled active/inactive (null for "None")
            public Material material;    // this style's hair material (null for "None")
            public List<HairColor> colors = new();
            public int colorIndex;       // selected colour within this style
        }

        [Serializable]
        public sealed class ClothingItem
        {
            public string id;
            public string label;
            public string category;      // top | bottom | full | shoes
            public string[] covers;      // nude regions this garment occludes: top | bottom | feet
            public GameObject root;      // the garment mesh group; toggled active/inactive (null for "None")
            public Material material;    // the PRIMARY part's material (colour-swap target); null for "None"
            public Texture2D fabricMask; // readable fabric-shape texture (alpha = fabric) — legacy (unused by region-hide)
            public List<HairColor> colors = new(); // {id,label,diffuse} — same shape as hair swatches
            public int colorIndex;
            [NonSerialized] public RenderTexture composite; // fabric-over-live-skin composite, bound as the material base map
            [NonSerialized] public RenderTexture normalComposite; // skin-detail normal masked to skin texels (fabric flat)
        }

        [Serializable]
        public sealed class BaseSkin
        {
            public string id;
            public string label;
            public Texture2D albedo; // full COLOR skin albedo, applied RAW (no detail/eye/tone compositing)
        }

        [Serializable]
        public sealed class SkinNormal
        {
            public string id;
            public string label;
            public Texture2D normal; // real skin-detail normal map; null = Flat (no detail)
        }

        [Header("Catalog (populated by the builder from character.json skinCatalog)")]
        public List<BaseTone> baseTones = new();
        public List<DetailLayer> detailLayers = new();
        public List<EyeColor> eyeColors = new();

        [Header("Base skin — full color albedo set (raw; takes over _BaseColorMap)")]
        public List<BaseSkin> baseSkins = new();

        [Header("Skin detail — real EA normal maps (drives _NormalMap)")]
        public List<SkinNormal> skinNormals = new();
        [SerializeField] private int skinNormalIndex;
        [Range(0f, 2f)] public float skinNormalScale = 0.6f;

        [Header("Skin gloss — uniform smoothness; garments' baked-skin texels follow (parity)")]
        [Range(0.05f, 0.6f)] public float skinSmoothness = 0.28f;

        [Header("Morph sliders (baked blend shapes; 0..1 each)")]
        public List<string> morphNames = new();
        [SerializeField] private List<float> morphWeights = new();
        // Grouped, paired, ordered slider axes derived from morphNames (built in SetMorphCatalog).
        public List<MorphAxis> morphAxes = new();

        // ---- BONE components of the sliders (SMOD→BOND) ----------------------------------------
        // Per slider (name == blend-shape / morph name): per-bone offset (parent-frame add, metres),
        // scale DELTA (0 = none; effective localScale factor = 1 + delta·weight — TS4SimRipper
        // RIG.Bone.UpdateLocalData semantics) and rotation delta. ~63 sliders carry these on top of
        // their blend shape; 6 (Chest/Head size, Chin Forward) are bone-ONLY. Scales apply directly
        // (animation clips never write localScale); offsets/rotations apply in LateUpdate AFTER the
        // Animation component so they compose with the playing idle/walk clips.
        [System.Serializable]
        public sealed class BoneMorphEntry
        {
            public string bone;
            public Vector3 offset;
            public Vector3 scaleDelta;
            public Quaternion rotation = Quaternion.identity;
        }

        [System.Serializable]
        public sealed class BoneMorph
        {
            public string name;
            public List<BoneMorphEntry> entries = new();
        }

        [Header("Bone morph components (set by the builder from character.json boneMorphs)")]
        public List<BoneMorph> boneMorphs = new();

        // ---- Facial expression: emotions + visemes ------------------------------------------------
        // Hand-authored, BLENDABLE (0..1 each) additive facial-bone rotations, fed into the SAME bone
        // accumulation as the CAS sculpt sliders (RecomputeBoneMorphs) so they compose over the playing
        // idle exactly like everything else. Each pose is a list of {facial bone -> local euler delta};
        // the slider weight scales it (Slerp(identity, Euler, w)). Manual only — nothing wires audio.
        [System.Serializable]
        public sealed class FacialBoneRot
        {
            public string bone;      // e.g. "b__Jaw__", "b__L_Mouth__"
            public Vector3 euler;    // local rotation delta in DEGREES — used for the JAW hinge only.
            public Vector3 move;     // translation in CHARACTER space, METRES (up=+Y, fwd=+Z, right=+X).
                                     // The surface facial bones (brows/mouth/lips/cheeks) carry real skin
                                     // weight but no clip animates them, so we DEFORM them by translation,
                                     // converted into each bone's parent frame at apply time. Weight-scaled.
        }

        [System.Serializable]
        public sealed class FacialPose
        {
            public string name;      // "Happy", "AA", ...
            public List<FacialBoneRot> bones = new();
        }

        [Header("Facial expression — hand-authored, blendable (composed over the idle); manual only")]
        public bool emotionEnabled;
        public bool visemeEnabled;
        public List<FacialPose> emotions = new();  // seeded by EnsureFacialDefaults if empty
        public List<FacialPose> visemes = new();
        [SerializeField] private List<float> emotionWeights = new();
        [SerializeField] private List<float> visemeWeights = new();

        // Runtime accumulation state (not serialized).
        private sealed class BoneMorphState
        {
            public Transform bone;
            public Vector3 bindPos; public Quaternion bindRot; public Vector3 bindScale; // captured pre-adjust
            public Vector3 offset; public Quaternion rot; public Vector3 scaleMul;       // accumulated
            public Vector3 lastWrittenPos; public Quaternion lastWrittenRot;             // LateUpdate baseline detection
            public Vector3 baselinePos; public Quaternion baselineRot;
            public bool hasBaseline;
        }
        private readonly Dictionary<Transform, BoneMorphState> _boneMorphStates = new();
        private Dictionary<string, Transform> _boneByName;

        [Header("Body mesh — three INDEPENDENT region slots on the shared skeleton")]
        public List<MeshVariant> topVariants = new();    // upper torso (+ arms)
        public List<MeshVariant> bottomVariants = new(); // legs
        public List<MeshVariant> feetVariants = new();   // feet

        [Header("Hair — styles on the shared skeleton; colour = a diffuse swap")]
        public List<HairStyle> hairStyles = new();       // index 0 is "None" (bald)

        [Header("Clothing — 18 slots (wardrobe, accessories, makeup); index 0 in each is None")]
        public List<ClothingItem> clothingTop = new();    // shirts/blouses/jackets (cover nude top)
        public List<ClothingItem> clothingBottom = new(); // pants/skirts/shorts (cover nude bottom)
        public List<ClothingItem> clothingFull = new();   // dresses/jumpsuits/slips (cover nude top+bottom)
        public List<ClothingItem> clothingShoes = new();  // shoes/boots (cover nude feet)
        public List<ClothingItem> clothingSocks = new();  // socks — LAYER under shoes (own slot)
        public List<ClothingItem> clothingBra = new();     // underwear LAYER: composites under top/full; renders alone otherwise
        public List<ClothingItem> clothingPanties = new(); // underwear LAYER: composites under bottom/full; renders alone otherwise
        public List<ClothingItem> clothingTights = new();  // SKIN texture layer (legs) — no mesh; composites into the skin atlas
        public List<ClothingItem> clothingGlasses = new(); // mesh accessories: hide nothing, layer with everything
        public List<ClothingItem> clothingEarrings = new();
        public List<ClothingItem> clothingNecklace = new();
        public List<ClothingItem> clothingGloves = new();
        public List<ClothingItem> clothingWristL = new();
        public List<ClothingItem> clothingWristR = new();
        public List<ClothingItem> clothingLipstick = new();  // makeup — SKIN texture layers (face region)
        public List<ClothingItem> clothingEyeshadow = new();
        public List<ClothingItem> clothingEyeliner = new();
        public List<ClothingItem> clothingBlush = new();
        public List<ClothingItem> clothingBrows = new();     // SKIN layer (face) — EA brows are painted, colour = hair colours
        public List<ClothingItem> clothingEyelashes = new(); // SKIN layer (face) — EA lashes are painted (EP16/base texture parts)

        /// <summary>Wardrobe slot count (indices used by the generic clothing API).</summary>
        public const int ClothingSlotCount = 20;
        /// <summary>Slots 14..19 are makeup/face layers — drawn in the SKIN tab, not the Clothing tab.</summary>
        public const int FirstMakeupSlot = 14;

        [Header("Current selection")]
        [SerializeField] private int baseToneIndex;
        [SerializeField] private int eyeColorIndex;
        [SerializeField] private int baseSkinIndex;
        [SerializeField] private int topIndex;
        [SerializeField] private int bottomIndex;
        [SerializeField] private int feetIndex;
        [SerializeField] private int hairStyleIndex;
        [SerializeField] private int clothingTopIndex;
        [SerializeField] private int clothingBottomIndex;
        [SerializeField] private int clothingFullIndex;
        [SerializeField] private int clothingShoesIndex;
        [SerializeField] private int clothingSocksIndex;
        [SerializeField] private int clothingBraIndex;
        [SerializeField] private int clothingPantiesIndex;
        [SerializeField] private int clothingTightsIndex;
        [SerializeField] private int clothingGlassesIndex;
        [SerializeField] private int clothingEarringsIndex;
        [SerializeField] private int clothingNecklaceIndex;
        [SerializeField] private int clothingGlovesIndex;
        [SerializeField] private int clothingWristLIndex;
        [SerializeField] private int clothingWristRIndex;
        [SerializeField] private int clothingLipstickIndex;
        [SerializeField] private int clothingEyeshadowIndex;
        [SerializeField] private int clothingEyelinerIndex;
        [SerializeField] private int clothingBlushIndex;
        [SerializeField] private int clothingBrowsIndex;
        [SerializeField] private int clothingEyelashesIndex;

        [Header("Target")]
        [Tooltip("The shared skin material whose _BaseColorMap receives the composited RenderTexture.")]
        public Material skinMaterial;

        // The composited albedo. Not serialized (RenderTextures are transient); rebuilt on enable.
        [NonSerialized] private RenderTexture _composited;
        [NonSerialized] private SkinCompositor _compositor;

        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int SmoothnessRemapMinId = Shader.PropertyToID("_SmoothnessRemapMin"); // garment SKIN gloss end (fabric = RemapMax, untouched)
        private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");

        /// <summary>The most recently composited skin albedo (or null before the first recompose).</summary>
        public RenderTexture Composited => _composited;

        public int BaseToneIndex => baseToneIndex;
        /// <summary>Whether the Skin Tone selection participates: true with no base skins, or when
        /// the EA Default base is selected. CC bases are full-color skins that replace tones.</summary>
        public bool TonesApply
        {
            get
            {
                if (baseSkins == null || baseSkins.Count == 0) return true;
                var bs = baseSkins[Clamp(baseSkinIndex, baseSkins.Count)];
                return bs != null && string.Equals(bs.id, "ea_default", StringComparison.OrdinalIgnoreCase);
            }
        }
        public int EyeColorIndex => eyeColorIndex;
        public int BaseSkinIndex => baseSkinIndex;
        public int TopIndex => topIndex;
        public int BottomIndex => bottomIndex;
        public int FeetIndex => feetIndex;
        public int HairStyleIndex => hairStyleIndex;
        public int HairStyleCount => hairStyles?.Count ?? 0;
        public int HairColorIndex => (hairStyles != null && hairStyleIndex >= 0 && hairStyleIndex < hairStyles.Count)
            ? hairStyles[hairStyleIndex].colorIndex : 0;
        public int HairColorCount => (hairStyles != null && hairStyleIndex >= 0 && hairStyleIndex < hairStyles.Count
                                      && hairStyles[hairStyleIndex].colors != null)
            ? hairStyles[hairStyleIndex].colors.Count : 0;

        // ---- Public API -----------------------------------------------------------------------

        /// <summary>Select a base tone by catalog index (clamped) and recompose.</summary>
        public void SetBaseTone(int index)
        {
            baseToneIndex = Clamp(index, baseTones.Count);
            Recompose();
        }

        /// <summary>Turn a detail layer on/off by its catalog id (case-insensitive) and recompose.</summary>
        public void ToggleDetail(string id, bool on)
        {
            for (var i = 0; i < detailLayers.Count; i++)
            {
                if (string.Equals(detailLayers[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    detailLayers[i].active = on;
                    Recompose();
                    return;
                }
            }
        }

        /// <summary>Turn a detail layer on/off by catalog index and recompose.</summary>
        public void ToggleDetailAt(int index, bool on)
        {
            if (index >= 0 && index < detailLayers.Count)
            {
                detailLayers[index].active = on;
                Recompose();
            }
        }

        /// <summary>Select an eye color by catalog index (clamped) and recompose.</summary>
        public void SetEyeColor(int index)
        {
            eyeColorIndex = Clamp(index, eyeColors.Count);
            Recompose();
        }

        /// <summary>Select the base skin (bottom of the stack) by index (clamped) and recompose.</summary>
        public void SetBaseSkin(int index)
        {
            baseSkinIndex = Clamp(index, baseSkins.Count);
            Recompose();
        }

        /// <summary>Set the SKIN gloss (HDRP smoothness). Lower = matte. Applies to the body and to
        /// every garment's baked-SKIN texels (via the smoothness-remap skin end — no neck seam);
        /// fabric texels keep their own gloss (RemapMax, set at build).</summary>
        public void SetSkinSmoothness(float value)
        {
            skinSmoothness = Mathf.Clamp(value, 0.05f, 0.6f);
            ApplySkinSmoothness();
        }

        private void ApplySkinSmoothness()
        {
            if (skinMaterial != null && skinMaterial.HasProperty(SmoothnessId))
                skinMaterial.SetFloat(SmoothnessId, skinSmoothness);
            for (var slot = 0; slot < ClothingSlotCount; slot++)
            {
                var list = ClothingList(slot);
                if (list == null) continue;
                foreach (var it in list)
                {
                    var m = it?.material;
                    if (m == null) continue;
                    // Garment _MaskMap alpha is a skin-vs-fabric SELECTOR (0 = baked skin, 1 = fabric)
                    // and smoothness = lerp(RemapMin, RemapMax, A). Retarget ONLY the skin end so the
                    // garment's baked-skin texels track the body while FABRIC keeps its own gloss.
                    if (m.HasProperty(SmoothnessRemapMinId)) m.SetFloat(SmoothnessRemapMinId, skinSmoothness);
                }
            }
        }

        // ---- Skin-detail normal (real EA normal maps) -----------------------------------------
        public int SkinNormalCount => skinNormals?.Count ?? 0;
        public int SkinNormalIndex => skinNormalIndex;

        /// <summary>Select a skin-detail normal by index (clamped) and apply it to the shared skin material.</summary>
        public void SetSkinNormal(int index)
        {
            skinNormalIndex = Clamp(index, skinNormals.Count);
            ApplySkinNormal();
        }

        /// <summary>Push the selected skin-detail normal to the skin material's _NormalMap (null = Flat).</summary>
        public void ApplySkinNormal()
        {
            if (skinMaterial == null || skinNormals == null || skinNormals.Count == 0) return;
            var i = Clamp(skinNormalIndex, skinNormals.Count);
            var n = skinNormals[i]?.normal;
            if (n != null)
            {
                if (skinMaterial.HasProperty(NormalMapId)) skinMaterial.SetTexture(NormalMapId, n);
                if (skinMaterial.HasProperty(NormalScaleId)) skinMaterial.SetFloat(NormalScaleId, skinNormalScale);
                skinMaterial.EnableKeyword("_NORMALMAP");
            }
            else // Flat (no detail)
            {
                if (skinMaterial.HasProperty(NormalMapId)) skinMaterial.SetTexture(NormalMapId, null);
                skinMaterial.DisableKeyword("_NORMALMAP");
            }

            // Mirror onto every VISIBLE garment's baked-skin texels (masked; fabric stays flat). Without
            // this the garment neck/chest has no normal map while the head runs the skin detail — the
            // response difference reads as a tone/shadow seam exactly at the head↔garment ring.
            for (var slot = 0; slot < ClothingSlotCount; slot++)
            {
                var list = ClothingList(slot); var idx = GetClothingIndex(slot);
                if (list == null || idx <= 0 || idx >= list.Count) continue;
                if (IsLayeredUnder(slot)) continue; // mesh hidden; its fabric rides in the outer composite
                ApplyGarmentSkinNormal(list[idx], n);
            }
        }

        // Bind a masked skin-detail normal to a garment: skin texels get the character's skin normal,
        // fabric texels are flattened (body relief must not emboss through cloth). Only garments that
        // carry baked-skin texels (marked by their _SubsurfaceMaskMap) participate.
        private void ApplyGarmentSkinNormal(ClothingItem it, Texture skinNormal)
        {
            if (it?.material == null) return;
            var hasSkin = it.material.HasProperty(SssMaskMapId) && it.material.GetTexture(SssMaskMapId) != null;
            if (skinNormal == null || !hasSkin)
            {
                if (it.material.HasProperty(NormalMapId)) it.material.SetTexture(NormalMapId, null);
                it.material.DisableKeyword("_NORMALMAP");
                return;
            }

            var fabric = it.colors != null && it.colors.Count > 0 ? it.colors[Clamp(it.colorIndex, it.colors.Count)].diffuse : null;
            if (fabric == null) fabric = it.fabricMask;
            if (fabric == null) return;

            if (_clothNormalMat == null)
            {
                var sh = Shader.Find("Hidden/Sims4/ClothNormalComposite");
                if (sh == null) return;
                _clothNormalMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }

            int w = fabric.width > 0 ? fabric.width : 1024, h = fabric.height > 0 ? fabric.height : 1024;
            if (it.normalComposite == null || it.normalComposite.width != w || it.normalComposite.height != h)
            {
                if (it.normalComposite != null) it.normalComposite.Release();
                var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0)
                { sRGB = false, useMipMap = false, autoGenerateMips = false }; // LINEAR: normal data, not color
                it.normalComposite = new RenderTexture(desc)
                {
                    name = it.id + "_nrm",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                it.normalComposite.Create();
            }

            _clothNormalMat.SetTexture(ClothSkinNormalId, skinNormal);
            // Under-layer fabrics also flatten the skin relief (a bra composited under a low-cut top must
            // not show body relief embossed on its cups). Up to one under-layer per covered region.
            Texture underA = null, underB = null;
            if (it.covers != null)
                foreach (var region in it.covers)
                {
                    var u = UnderLayerFabric(region);
                    if (u == null) continue;
                    if (underA == null) underA = u; else underB = u;
                }
            _clothNormalMat.SetTexture(ClothUnderAId, underA != null ? underA : Texture2D.blackTexture);
            _clothNormalMat.SetTexture(ClothUnderBId, underB != null ? underB : Texture2D.blackTexture);
            _clothNormalMat.SetFloat(ClothHasUnderAId, underA != null ? 1f : 0f);
            _clothNormalMat.SetFloat(ClothHasUnderBId, underB != null ? 1f : 0f);
            Graphics.Blit(fabric, it.normalComposite, _clothNormalMat, 0);
            if (it.material.HasProperty(NormalMapId)) it.material.SetTexture(NormalMapId, it.normalComposite);
            if (it.material.HasProperty(NormalScaleId)) it.material.SetFloat(NormalScaleId, skinNormalScale);
            it.material.EnableKeyword("_NORMALMAP");
        }

        // ---- Morph sliders (blend shapes) -----------------------------------------------------
        public int MorphCount => morphNames?.Count ?? 0;

        public float GetMorphWeight(int i) =>
            (morphWeights != null && i >= 0 && i < morphWeights.Count) ? morphWeights[i] : 0f;

        /// <summary>Builder hook: set the available morph names, build the grouped/paired axes, apply.</summary>
        public void SetMorphCatalog(List<string> names)
        {
            morphNames = names ?? new List<string>();
            morphWeights = new List<float>(new float[morphNames.Count]);
            morphAxes = MorphAxisBuilder.Build(morphNames);
            ApplyMorphs();
        }

        // ---- Axis API (grouped, paired sliders) -----------------------------------------------
        public int AxisCount => morphAxes?.Count ?? 0;

        public float GetAxisValue(int i) =>
            (morphAxes != null && i >= 0 && i < morphAxes.Count) ? morphAxes[i].value : 0f;

        /// <summary>Set one axis (-1..1 if bidirectional else 0..1): +value drives the positive morph,
        /// -value the negative one. Both at 0 = neutral.</summary>
        public void SetAxis(int i, float value)
        {
            if (morphAxes == null || i < 0 || i >= morphAxes.Count) return;
            var a = morphAxes[i];
            a.value = a.Bidirectional ? Mathf.Clamp(value, -1f, 1f) : Mathf.Clamp01(value);
            ApplyAxis(a);
        }

        public void ApplyAxes()
        {
            if (morphAxes == null) return;
            foreach (var a in morphAxes) ApplyAxis(a);
        }

        private void ApplyAxis(MorphAxis a)
        {
            if (a == null) return;
            if (a.Bidirectional)
            {
                ApplyMorphByName(a.positive, Mathf.Max(0f, a.value));
                ApplyMorphByName(a.negative, Mathf.Max(0f, -a.value));
            }
            else
            {
                ApplyMorphByName(!string.IsNullOrEmpty(a.positive) ? a.positive : a.negative, a.value);
            }
        }

        /// <summary>Push a single morph's weight (0..1) to every renderer carrying the blend shape, and
        /// record it in morphWeights (so the scene-view heat-map highlight stays in sync).</summary>
        public void ApplyMorphByName(string morphName, float weight01)
        {
            if (string.IsNullOrEmpty(morphName)) return;
            var w01 = Mathf.Clamp01(weight01);
            var idx = morphNames != null ? morphNames.IndexOf(morphName) : -1;
            if (idx >= 0)
            {
                while (morphWeights.Count < morphNames.Count) morphWeights.Add(0f);
                morphWeights[idx] = w01;
            }
            var w = w01 * 100f;
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                var bi = smr.sharedMesh.GetBlendShapeIndex(morphName);
                if (bi >= 0) smr.SetBlendShapeWeight(bi, w);
            }
            RecomputeBoneMorphs(); // bone component (offset/scale/rotation) of this + all active sliders
        }

        // ---- Bone-morph application -------------------------------------------------------------
        // Accumulate every active slider's BoneMorph entries per bone (offset: Σ off·w, parent-frame;
        // scale: Π (1 + delta·w) per axis; rotation: Π slerp(identity, q, w) — TS4SimRipper
        // RIG.Bone.UpdateLocalData semantics), then apply. Scale is safe to set directly (animation
        // clips write rotation/position only). Position/rotation apply directly in EDIT mode (bones
        // rest at bind); in PLAY mode <see cref="LateUpdate"/> composes them AFTER the Animation.
        private void RecomputeBoneMorphs()
        {
            var hasCas = boneMorphs != null && boneMorphs.Count > 0;
            var hasFacial = (emotions != null && emotions.Count > 0) || (visemes != null && visemes.Count > 0);
            if (!hasCas && !hasFacial) return;
            if (_boneByName == null)
            {
                _boneByName = new Dictionary<string, Transform>();
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (!_boneByName.ContainsKey(t.name)) _boneByName[t.name] = t;
            }

            foreach (var s in _boneMorphStates.Values)
            {
                s.offset = Vector3.zero; s.rot = Quaternion.identity; s.scaleMul = Vector3.one;
            }

            foreach (var bmorph in boneMorphs)
            {
                var idx = morphNames != null ? morphNames.IndexOf(bmorph.name) : -1;
                var w = (idx >= 0 && morphWeights != null && idx < morphWeights.Count) ? morphWeights[idx] : 0f;
                if (w <= 0.0001f || bmorph.entries == null) continue;
                foreach (var e in bmorph.entries)
                {
                    if (string.IsNullOrEmpty(e.bone) || !_boneByName.TryGetValue(e.bone, out var bt) || bt == null) continue;
                    if (!_boneMorphStates.TryGetValue(bt, out var s))
                    {
                        s = new BoneMorphState
                        {
                            bone = bt,
                            bindPos = bt.localPosition, bindRot = bt.localRotation, bindScale = bt.localScale,
                            offset = Vector3.zero, rot = Quaternion.identity, scaleMul = Vector3.one,
                        };
                        _boneMorphStates[bt] = s;
                    }
                    s.offset += e.offset * w;
                    s.scaleMul = Vector3.Scale(s.scaleMul, Vector3.one + (e.scaleDelta * w));
                    s.rot *= Quaternion.Slerp(Quaternion.identity, e.rotation, w);
                }
            }

            // Facial expression banks compose into the SAME accumulation (only when their toggle is on).
            AccumulateFacial(emotionEnabled ? emotions : null, emotionWeights);
            AccumulateFacial(visemeEnabled ? visemes : null, visemeWeights);

            foreach (var s in _boneMorphStates.Values)
            {
                if (s.bone == null) continue;
                s.bone.localScale = Vector3.Scale(s.bindScale, s.scaleMul);
                if (!Application.isPlaying)
                {
                    s.bone.localPosition = s.bindPos + s.offset;
                    s.bone.localRotation = s.bindRot * s.rot;
                }
                // Do NOT invalidate the LateUpdate baseline here. The baseline is the UN-morphed pose and
                // weights changing doesn't change it; LateUpdate's own change-detection re-baselines when
                // the animation (or a pose restore) rewrites the bone. Forcing re-baseline on every slider
                // tick captured the PREVIOUS tick's offset into the baseline for bones no clip animates
                // (the CAS face bones) — offsets accumulated during a drag and the face flew apart.
            }
        }

        // Compose bone-morph offsets/rotations ON TOP of whatever the Animation component wrote this
        // frame. Baseline detection: if the bone's current value differs from what WE wrote last frame,
        // the animation rewrote it → new baseline; if unchanged (bone not in the playing clip), keep
        // the previous baseline — prevents our additive offset from accumulating frame over frame.
        private void LateUpdate()
        {
            if (!Application.isPlaying || _boneMorphStates.Count == 0) return;
            foreach (var s in _boneMorphStates.Values)
            {
                if (s.bone == null) continue;
                var curPos = s.bone.localPosition;
                var curRot = s.bone.localRotation;
                if (!s.hasBaseline || curPos != s.lastWrittenPos) s.baselinePos = curPos;
                if (!s.hasBaseline || Quaternion.Angle(curRot, s.lastWrittenRot) > 0.001f) s.baselineRot = curRot;
                s.hasBaseline = true;

                var np = s.baselinePos + s.offset;
                var nr = s.baselineRot * s.rot;
                s.bone.localPosition = np;
                s.bone.localRotation = nr;
                s.lastWrittenPos = np;
                s.lastWrittenRot = nr;
            }
        }

        /// <summary>Set one raw morph's weight (0..1) by index — kept for compatibility/debug.</summary>
        public void SetMorph(int index, float weight01)
        {
            if (morphNames == null || index < 0 || index >= morphNames.Count) return;
            ApplyMorphByName(morphNames[index], weight01);
        }

        /// <summary>Re-apply all axis values to the renderers (e.g. after enable/rebuild).</summary>
        public void ApplyMorphs() => ApplyAxes();

        // ---- Templates: capture / apply the full designed look (save-load + spawning) ----------
        private static string CatId<T>(List<T> list, int idx, System.Func<T, string> id)
            => (list != null && idx >= 0 && idx < list.Count && list[idx] != null) ? id(list[idx]) : null;

        private static int CatIndex<T>(List<T> list, string id, System.Func<T, string> getId)
        {
            if (list == null || string.IsNullOrEmpty(id)) return 0;
            for (var i = 0; i < list.Count; i++)
                if (list[i] != null && string.Equals(getId(list[i]), id, System.StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        /// <summary>Snapshot the CURRENT selection (by catalog id) into a portable template.</summary>
        public CharacterTemplate CaptureTemplate(string templateName)
        {
            var t = new CharacterTemplate { name = templateName };
            if (morphNames != null)
                for (var i = 0; i < morphNames.Count; i++)
                {
                    var w = GetMorphWeight(i);
                    if (w > 0.0005f) t.morphs.Add(new CharacterTemplate.MorphVal { name = morphNames[i], weight = w });
                }
            t.baseTone = CatId(baseTones, baseToneIndex, x => x.id);
            t.eyeColor = CatId(eyeColors, eyeColorIndex, x => x.id);
            t.baseSkin = CatId(baseSkins, baseSkinIndex, x => x.id);
            t.skinNormal = CatId(skinNormals, skinNormalIndex, x => x.id);
            t.skinNormalScale = skinNormalScale;
            t.skinSmoothness = skinSmoothness;
            t.detailLayers = new List<string>();
            if (detailLayers != null) foreach (var d in detailLayers) if (d != null && d.active) t.detailLayers.Add(d.id);
            t.topMesh = CatId(topVariants, topIndex, x => x.id);
            t.bottomMesh = CatId(bottomVariants, bottomIndex, x => x.id);
            t.feetMesh = CatId(feetVariants, feetIndex, x => x.id);
            if (hairStyles != null && hairStyleIndex >= 0 && hairStyleIndex < hairStyles.Count)
            {
                var hs = hairStyles[hairStyleIndex];
                t.hairStyle = hs.id;
                if (hs.colors != null && hs.colorIndex >= 0 && hs.colorIndex < hs.colors.Count) t.hairColor = hs.colors[hs.colorIndex].id;
            }
            for (var slot = 0; slot < ClothingSlotCount; slot++)
            {
                var idx = GetClothingIndex(slot);
                if (idx <= 0) continue;
                var list = ClothingList(slot);
                if (list == null || idx >= list.Count) continue;
                var item = list[idx];
                var colorId = (item.colors != null && item.colorIndex >= 0 && item.colorIndex < item.colors.Count) ? item.colors[item.colorIndex].id : null;
                t.clothing.Add(new CharacterTemplate.SlotVal { slot = ClothingSlotNames[slot], id = item.id, color = colorId });
            }
            return t;
        }

        /// <summary>Apply a saved template — set every knob to match (missing ids fall back to option 0).</summary>
        public void ApplyTemplate(CharacterTemplate t)
        {
            if (t == null) return;
            // Meshes first (occlusion may instance them, resetting morph weights → morphs re-applied last).
            SetTopMesh(CatIndex(topVariants, t.topMesh, x => x.id));
            SetBottomMesh(CatIndex(bottomVariants, t.bottomMesh, x => x.id));
            SetFeetMesh(CatIndex(feetVariants, t.feetMesh, x => x.id));
            // Skin
            SetBaseSkin(CatIndex(baseSkins, t.baseSkin, x => x.id));
            SetBaseTone(CatIndex(baseTones, t.baseTone, x => x.id));
            SetEyeColor(CatIndex(eyeColors, t.eyeColor, x => x.id));
            SetSkinNormal(CatIndex(skinNormals, t.skinNormal, x => x.id));
            skinNormalScale = t.skinNormalScale; ApplySkinNormal();
            SetSkinSmoothness(t.skinSmoothness);
            // Detail overlays: all off, then the saved ones on.
            if (detailLayers != null) for (var i = 0; i < detailLayers.Count; i++) ToggleDetailAt(i, false);
            if (t.detailLayers != null) foreach (var id in t.detailLayers) ToggleDetail(id, true);
            // Hair
            if (!string.IsNullOrEmpty(t.hairStyle))
            {
                SetHairStyle(CatIndex(hairStyles, t.hairStyle, x => x.id));
                if (!string.IsNullOrEmpty(t.hairColor) && hairStyles != null && hairStyleIndex >= 0 && hairStyleIndex < hairStyles.Count)
                    SetHairColor(CatIndex(hairStyles[hairStyleIndex].colors, t.hairColor, x => x.id));
            }
            else SetHairStyle(0);
            // Clothing: clear all slots, then set the saved items + colours.
            for (var slot = 0; slot < ClothingSlotCount; slot++) SetClothing(slot, 0);
            if (t.clothing != null)
                foreach (var sv in t.clothing)
                {
                    var slot = System.Array.IndexOf(ClothingSlotNames, sv.slot);
                    if (slot < 0) continue;
                    var list = ClothingList(slot);
                    var idx = CatIndex(list, sv.id, x => x.id);
                    SetClothing(slot, idx);
                    if (idx > 0 && list != null && idx < list.Count && !string.IsNullOrEmpty(sv.color))
                        SetClothingColor(slot, CatIndex(list[idx].colors, sv.color, x => x.id));
                }
            // Morphs: reset all, then apply the saved weights.
            if (morphNames != null) foreach (var n in morphNames) ApplyMorphByName(n, 0f);
            if (t.morphs != null) foreach (var mv in t.morphs) ApplyMorphByName(mv.name, mv.weight);
        }

        /// <summary>Convenience: capture + save to the template store; returns the file path.</summary>
        public string SaveTemplate(string templateName) => CharacterTemplateStore.Save(CaptureTemplate(templateName));

        /// <summary>Convenience: load a template file and apply it. Returns false if the file is missing/bad.</summary>
        public bool ApplyTemplateFile(string path)
        {
            var t = CharacterTemplateStore.Load(path);
            if (t == null) return false;
            ApplyTemplate(t);
            return true;
        }

        // ---- Facial expression API (emotions + visemes) ---------------------------------------
        public int EmotionCount => emotions?.Count ?? 0;
        public int VisemeCount => visemes?.Count ?? 0;
        public string GetEmotionName(int i) => (emotions != null && i >= 0 && i < emotions.Count) ? emotions[i].name : "";
        public string GetVisemeName(int i) => (visemes != null && i >= 0 && i < visemes.Count) ? visemes[i].name : "";
        public float GetEmotionWeight(int i) => (emotionWeights != null && i >= 0 && i < emotionWeights.Count) ? emotionWeights[i] : 0f;
        public float GetVisemeWeight(int i) => (visemeWeights != null && i >= 0 && i < visemeWeights.Count) ? visemeWeights[i] : 0f;

        public void SetEmotionEnabled(bool on) { emotionEnabled = on; RecomputeBoneMorphs(); }
        public void SetVisemeEnabled(bool on) { visemeEnabled = on; RecomputeBoneMorphs(); }

        public void SetEmotionWeight(int i, float w)
        {
            EnsureFacialWeights();
            if (i >= 0 && i < emotionWeights.Count) { emotionWeights[i] = Mathf.Clamp01(w); RecomputeBoneMorphs(); }
        }
        public void SetVisemeWeight(int i, float w)
        {
            EnsureFacialWeights();
            if (i >= 0 && i < visemeWeights.Count) { visemeWeights[i] = Mathf.Clamp01(w); RecomputeBoneMorphs(); }
        }
        public void ResetEmotions() { if (emotionWeights != null) for (var i = 0; i < emotionWeights.Count; i++) emotionWeights[i] = 0f; RecomputeBoneMorphs(); }
        public void ResetVisemes() { if (visemeWeights != null) for (var i = 0; i < visemeWeights.Count; i++) visemeWeights[i] = 0f; RecomputeBoneMorphs(); }

        private void EnsureFacialWeights()
        {
            emotionWeights ??= new List<float>();
            visemeWeights ??= new List<float>();
            while (emotionWeights.Count < (emotions?.Count ?? 0)) emotionWeights.Add(0f);
            while (visemeWeights.Count < (visemes?.Count ?? 0)) visemeWeights.Add(0f);
        }

        // Accumulate one bank's weighted rotations into the shared bone-morph state (creating the state
        // from the bone's current bind on first touch), exactly like the CAS sliders. Skips zero weights.
        private void AccumulateFacial(List<FacialPose> poses, List<float> weights)
        {
            if (poses == null || weights == null) return;
            var rootRot = transform.rotation; // character-space -> world for the translation directions
            for (var p = 0; p < poses.Count; p++)
            {
                var w = (p < weights.Count) ? weights[p] : 0f;
                if (w <= 0.0001f) continue;
                var pose = poses[p];
                if (pose?.bones == null) continue;
                foreach (var br in pose.bones)
                {
                    if (string.IsNullOrEmpty(br.bone) || !_boneByName.TryGetValue(br.bone, out var bt) || bt == null) continue;
                    if (!_boneMorphStates.TryGetValue(bt, out var s))
                    {
                        s = new BoneMorphState
                        {
                            bone = bt,
                            bindPos = bt.localPosition, bindRot = bt.localRotation, bindScale = bt.localScale,
                            offset = Vector3.zero, rot = Quaternion.identity, scaleMul = Vector3.one,
                        };
                        _boneMorphStates[bt] = s;
                    }
                    if (br.euler != Vector3.zero)
                        s.rot *= Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(br.euler), w);
                    if (br.move != Vector3.zero && bt.parent != null)
                    {
                        // character-space move -> world -> the bone's parent frame (localPosition is parent-relative)
                        var world = rootRot * br.move;
                        var local = Quaternion.Inverse(bt.parent.rotation) * world;
                        s.offset += local * w;
                    }
                }
            }
        }

        // Seed the default emotion + viseme poses (once, if empty). Hand-authored, SYMMETRIC (L=R) for v1.
        // Jaw rotates about local Z (calibrated from EA's talk clip); other facial-bone axes are best-guess
        // Z-bends and are meant to be tuned. Magnitudes are deliberately visible so testing is unambiguous.
        private void EnsureFacialDefaults()
        {
            emotions ??= new List<FacialPose>();
            visemes ??= new List<FacialPose>();
            if (NeedsReseed(emotions)) { emotions.Clear(); SeedEmotions(); }
            if (NeedsReseed(visemes)) { visemes.Clear(); SeedVisemes(); }
            EnsureFacialWeights();
        }

        // Reseed when empty OR authored in the old rotation-only format (no translation) — so a code change
        // to the default poses takes effect instead of stale serialized data.
        private static bool NeedsReseed(List<FacialPose> poses)
        {
            if (poses == null || poses.Count == 0) return true;
            foreach (var p in poses)
                if (p?.bones != null)
                    foreach (var b in p.bones)
                        if (b.move != Vector3.zero) return false; // has translation -> current format
            return true;
        }

        // Author surface bones by TRANSLATION (character-space metres, up=+Y fwd=+Z right=+X); the Jaw is a
        // hinge so it stays a rotation. MvPair applies the same move to L and R (symmetric for v1).
        private static void Mv(FacialPose p, string bone, float x, float y, float z) => p.bones.Add(new FacialBoneRot { bone = bone, move = new Vector3(x, y, z) });
        private static void MvPair(FacialPose p, string core, float x, float y, float z) { Mv(p, "b__L_" + core + "__", x, y, z); Mv(p, "b__R_" + core + "__", x, y, z); }
        private static void RotZ(FacialPose p, string bone, float z) => p.bones.Add(new FacialBoneRot { bone = bone, euler = new Vector3(0f, 0f, z) });

        private void SeedEmotions()
        {
            const float U = 0.012f; // a clearly-visible facial displacement (~12 mm at full slider)
            FacialPose Happy = new() { name = "Happy" };
            MvPair(Happy, "Mouth", 0f, U, 0.002f); MvPair(Happy, "Cheek", 0f, U * 0.7f, 0.002f); MvPair(Happy, "Squint", 0f, U * 0.4f, 0f);
            FacialPose Sad = new() { name = "Sad" };
            MvPair(Sad, "InBrow", 0f, U * 0.8f, 0.001f); MvPair(Sad, "OutBrow", 0f, -U * 0.5f, 0f); MvPair(Sad, "Mouth", 0f, -U, 0f);
            FacialPose Angry = new() { name = "Angry" };
            MvPair(Angry, "InBrow", 0f, -U * 0.8f, 0f); MvPair(Angry, "MidBrow", 0f, -U * 0.6f, 0f); MvPair(Angry, "Squint", 0f, U * 0.4f, 0f); MvPair(Angry, "Mouth", 0f, -U * 0.4f, 0f);
            FacialPose Afraid = new() { name = "Afraid" };
            MvPair(Afraid, "InBrow", 0f, U, 0f); MvPair(Afraid, "MidBrow", 0f, U, 0f); MvPair(Afraid, "OutBrow", 0f, U * 0.8f, 0f); MvPair(Afraid, "Squint", 0f, -U * 0.5f, 0f); RotZ(Afraid, "b__Jaw__", -8f);
            FacialPose Surprised = new() { name = "Surprised" };
            MvPair(Surprised, "InBrow", 0f, U * 1.3f, 0f); MvPair(Surprised, "MidBrow", 0f, U * 1.3f, 0f); MvPair(Surprised, "OutBrow", 0f, U * 1.2f, 0f); MvPair(Surprised, "Squint", 0f, -U * 0.6f, 0f); RotZ(Surprised, "b__Jaw__", -16f);
            FacialPose Disgusted = new() { name = "Disgusted" };
            MvPair(Disgusted, "UpLip", 0f, U * 0.8f, 0f); Mv(Disgusted, "b__UpLip__", 0f, U * 0.8f, 0f); MvPair(Disgusted, "InBrow", 0f, -U * 0.5f, 0f); MvPair(Disgusted, "Squint", 0f, U * 0.6f, 0f);
            emotions.AddRange(new[] { Happy, Sad, Angry, Afraid, Surprised, Disgusted });
        }

        private void SeedVisemes()
        {
            const float L = 0.011f;
            FacialPose Rest = new() { name = "Rest (sil)" };
            FacialPose MBP = new() { name = "M/B/P (closed)" };
            Mv(MBP, "b__UpLip__", 0f, -L * 0.5f, 0f); Mv(MBP, "b__LoLip__", 0f, L * 0.5f, 0f); MvPair(MBP, "UpLip", 0f, -L * 0.4f, 0f); MvPair(MBP, "LoLip", 0f, L * 0.4f, 0f);
            FacialPose FV = new() { name = "F/V (lip-teeth)" };
            Mv(FV, "b__LoLip__", 0f, L * 0.7f, 0.002f); MvPair(FV, "LoLip", 0f, L * 0.7f, 0.002f); RotZ(FV, "b__Jaw__", -3f);
            FacialPose AA = new() { name = "AA (open)" };
            RotZ(AA, "b__Jaw__", -18f);
            FacialPose E = new() { name = "E (mid)" };
            RotZ(E, "b__Jaw__", -8f);
            FacialPose IH = new() { name = "IH (slight)" };
            RotZ(IH, "b__Jaw__", -5f);
            FacialPose OH = new() { name = "OH (round)" };
            RotZ(OH, "b__Jaw__", -11f); Mv(OH, "b__UpLip__", 0f, 0f, 0.004f); Mv(OH, "b__LoLip__", 0f, 0f, 0.004f); MvPair(OH, "UpLip", 0f, 0f, 0.003f); MvPair(OH, "LoLip", 0f, 0f, 0.003f);
            FacialPose OU = new() { name = "OU/W (tight round)" };
            RotZ(OU, "b__Jaw__", -5f); Mv(OU, "b__UpLip__", 0f, 0f, 0.006f); Mv(OU, "b__LoLip__", 0f, 0f, 0.006f); MvPair(OU, "UpLip", 0f, 0f, 0.005f); MvPair(OU, "LoLip", 0f, 0f, 0.005f);
            FacialPose Lv = new() { name = "L (tongue)" };
            RotZ(Lv, "b__Jaw__", -9f);
            visemes.AddRange(new[] { Rest, MBP, FV, AA, E, IH, OH, OU, Lv });
        }

        /// <summary>Select the upper-torso mesh by option index (clamped) and activate only it.</summary>
        public void SetTopMesh(int index)
        {
            topIndex = Clamp(index, topVariants.Count);
            ApplyRegion(topVariants, topIndex);
            ApplyClothingOcclusion(); // re-occlude the new nude mesh under any worn garment
            ApplyMorphs();            // occlusion may instance the mesh (resets blend weights) → re-apply
        }

        /// <summary>Select the legs mesh by option index (clamped) and activate only it.</summary>
        public void SetBottomMesh(int index)
        {
            bottomIndex = Clamp(index, bottomVariants.Count);
            ApplyRegion(bottomVariants, bottomIndex);
            ApplyClothingOcclusion();
            ApplyMorphs();
        }

        /// <summary>Select the feet mesh by option index (clamped) and activate only it.</summary>
        public void SetFeetMesh(int index)
        {
            feetIndex = Clamp(index, feetVariants.Count);
            ApplyRegion(feetVariants, feetIndex);
            ApplyClothingOcclusion();
            ApplyMorphs();
        }

        /// <summary>Activate the selected option in every region slot; deactivate the rest.</summary>
        public void ApplyMeshVariants()
        {
            topIndex = Clamp(topIndex, topVariants.Count);
            bottomIndex = Clamp(bottomIndex, bottomVariants.Count);
            feetIndex = Clamp(feetIndex, feetVariants.Count);
            ApplyRegion(topVariants, topIndex);
            ApplyRegion(bottomVariants, bottomIndex);
            ApplyRegion(feetVariants, feetIndex);
        }

        /// <summary>Select a hairstyle by index (0 = None); show only its mesh + its current colour.</summary>
        public void SetHairStyle(int index)
        {
            if (hairStyles == null || hairStyles.Count == 0) return;
            hairStyleIndex = Clamp(index, hairStyles.Count);
            for (var i = 0; i < hairStyles.Count; i++)
            {
                var go = hairStyles[i]?.root;
                if (go != null) go.SetActive(i == hairStyleIndex);
            }
            ApplyHairColor();
        }

        /// <summary>Select a colour within the active hairstyle (swaps the style material's base map).</summary>
        public void SetHairColor(int index)
        {
            if (hairStyles == null || hairStyleIndex < 0 || hairStyleIndex >= hairStyles.Count) return;
            var style = hairStyles[hairStyleIndex];
            if (style?.colors == null || style.colors.Count == 0) return;
            style.colorIndex = Clamp(index, style.colors.Count);
            ApplyHairColor();
        }

        // Push the active style's selected colour onto its material's _BaseColorMap.
        private void ApplyHairColor()
        {
            if (hairStyles == null || hairStyleIndex < 0 || hairStyleIndex >= hairStyles.Count) return;
            var style = hairStyles[hairStyleIndex];
            if (style?.material == null || style.colors == null || style.colors.Count == 0) return;
            var ci = Clamp(style.colorIndex, style.colors.Count);
            var tex = style.colors[ci].diffuse;
            if (tex != null) style.material.SetTexture(BaseColorMapId, tex);
        }

        // ---- Clothing: 18 slots (0=Top 1=Bottom 2=Full 3=Shoes 4=Socks 5=Bra 6=Panties 7=Tights
        // 8-13=accessories 14-17=makeup);
        // index 0 in each is None. Bra/Panties are UNDER-LAYERS: worn beneath an outer garment their
        // mesh hides and their fabric composites into the outer garment's base map. ----
        public static readonly string[] ClothingSlotNames =
        {
            "Top", "Bottom", "Full", "Shoes", "Socks", "Bra", "Panties", "Tights",
            "Glasses", "Earrings", "Necklace", "Gloves", "Wrist L", "Wrist R",
            "Lipstick", "Eyeshadow", "Eyeliner", "Blush", "Brows", "Eyelashes",
        };

        private List<ClothingItem> ClothingList(int slot) => slot switch
        {
            0 => clothingTop, 1 => clothingBottom, 2 => clothingFull, 3 => clothingShoes, 4 => clothingSocks,
            5 => clothingBra, 6 => clothingPanties, 7 => clothingTights,
            8 => clothingGlasses, 9 => clothingEarrings, 10 => clothingNecklace, 11 => clothingGloves,
            12 => clothingWristL, 13 => clothingWristR,
            14 => clothingLipstick, 15 => clothingEyeshadow, 16 => clothingEyeliner, 17 => clothingBlush,
            18 => clothingBrows, 19 => clothingEyelashes,
            _ => clothingTop,
        };
        public List<ClothingItem> GetClothing(int slot) => ClothingList(slot);
        public int GetClothingCount(int slot) => ClothingList(slot)?.Count ?? 0;
        public int GetClothingIndex(int slot) => slot switch
        {
            0 => clothingTopIndex, 1 => clothingBottomIndex, 2 => clothingFullIndex, 3 => clothingShoesIndex, 4 => clothingSocksIndex,
            5 => clothingBraIndex, 6 => clothingPantiesIndex, 7 => clothingTightsIndex,
            8 => clothingGlassesIndex, 9 => clothingEarringsIndex, 10 => clothingNecklaceIndex, 11 => clothingGlovesIndex,
            12 => clothingWristLIndex, 13 => clothingWristRIndex,
            14 => clothingLipstickIndex, 15 => clothingEyeshadowIndex, 16 => clothingEyelinerIndex, 17 => clothingBlushIndex,
            18 => clothingBrowsIndex, 19 => clothingEyelashesIndex,
            _ => 0,
        };
        private void SetClothingIndexField(int slot, int v)
        {
            switch (slot)
            {
                case 0: clothingTopIndex = v; break; case 1: clothingBottomIndex = v; break; case 2: clothingFullIndex = v; break;
                case 3: clothingShoesIndex = v; break; case 4: clothingSocksIndex = v; break;
                case 5: clothingBraIndex = v; break; case 6: clothingPantiesIndex = v; break;
                case 7: clothingTightsIndex = v; break;
                case 8: clothingGlassesIndex = v; break; case 9: clothingEarringsIndex = v; break;
                case 10: clothingNecklaceIndex = v; break; case 11: clothingGlovesIndex = v; break;
                case 12: clothingWristLIndex = v; break; case 13: clothingWristRIndex = v; break;
                case 14: clothingLipstickIndex = v; break; case 15: clothingEyeshadowIndex = v; break;
                case 16: clothingEyelinerIndex = v; break; case 17: clothingBlushIndex = v; break;
                case 18: clothingBrowsIndex = v; break; case 19: clothingEyelashesIndex = v; break;
            }
        }

        // An under-layer garment is LAYERED (mesh hidden, fabric composited into the outer garment)
        // when a worn outer garment actually COVERS its region: bra under top/full, panties under
        // bottom/full. Keyed on covers[] (not just "a bottom is worn") because a physics SHELL garment
        // (custom cloth skirt, covers=[]) hides nothing — underwear beneath it must stay a real mesh.
        private bool IsLayeredUnder(int slot) => slot switch
        {
            5 => OuterCoversRegion("top"),
            6 => OuterCoversRegion("bottom"),
            _ => false,
        };
        private bool OuterCoversRegion(string region)
        {
            for (var slot = 0; slot <= 2; slot++) // top/bottom/full are the only outer wardrobe slots
            {
                var list = ClothingList(slot); var idx = GetClothingIndex(slot);
                if (list == null || idx <= 0 || idx >= list.Count) continue;
                var cov = list[idx]?.covers;
                if (cov == null) continue;
                for (var i = 0; i < cov.Length; i++)
                    if (cov[i] == region) return true;
            }
            return false;
        }
        /// <summary>Whether the selected garment in a slot renders its own MESH (worn and not layered under).</summary>
        public bool IsClothingVisible(int slot)
        {
            var list = ClothingList(slot); var idx = GetClothingIndex(slot);
            return list != null && idx > 0 && idx < list.Count && !IsLayeredUnder(slot);
        }
        public int GetClothingColorCount(int slot)
        {
            var list = ClothingList(slot); var i = GetClothingIndex(slot);
            return (list != null && i > 0 && i < list.Count && list[i].colors != null) ? list[i].colors.Count : 0;
        }
        public int GetClothingColorIndex(int slot)
        {
            var list = ClothingList(slot); var i = GetClothingIndex(slot);
            return (list != null && i > 0 && i < list.Count) ? list[i].colorIndex : 0;
        }

        /// <summary>Wear a garment (0 = None) in a slot; a full outfit and separate top/bottom are mutually exclusive.</summary>
        public void SetClothing(int slot, int index)
        {
            var list = ClothingList(slot);
            if (list == null || list.Count == 0) return;
            index = Clamp(index, list.Count);
            SetClothingIndexField(slot, index);
            if (index > 0)
            {
                if (slot == 2) { SetClothingIndexField(0, 0); SetClothingIndexField(1, 0); } // full clears top+bottom
                else if (slot == 0 || slot == 1) SetClothingIndexField(2, 0);                 // top/bottom clears full
            }
            ApplyClothing();
        }

        /// <summary>Select a colour within the active garment of a slot (swaps its material's base map).</summary>
        public void SetClothingColor(int slot, int colorIdx)
        {
            var list = ClothingList(slot); var i = GetClothingIndex(slot);
            if (list == null || i <= 0 || i >= list.Count) return;
            var it = list[i];
            if (it.colors == null || it.colors.Count == 0) return;
            it.colorIndex = Clamp(colorIdx, it.colors.Count);
            if (IsSkinLayerSlot(slot)) { Recompose(); return; } // skin layers live inside the atlas chain
            // An under-layer's colour shows THROUGH the outer garment's composite, so refresh every
            // visible garment (cheap: one blit chain per worn garment).
            RecompositeWornGarments();
        }

        /// <summary>Activate the selected garment in every slot, push its colour, and hide the covered nude regions.</summary>
        public void ApplyClothing()
        {
            // Pre-clamp EVERY slot first: an outer garment's composite reads the bra/panties selection
            // (UnderLayerFabric), so all indices must be valid before any slot composites.
            for (var slot = 0; slot < ClothingSlotCount; slot++)
                SetClothingIndexField(slot, Clamp(GetClothingIndex(slot), ClothingList(slot)?.Count ?? 0));

            for (var slot = 0; slot < ClothingSlotCount; slot++)
            {
                var list = ClothingList(slot);
                var idx = GetClothingIndex(slot);
                if (list == null) continue;
                var meshVisible = idx > 0 && !IsLayeredUnder(slot); // layered underwear: mesh hidden, fabric composited
                for (var k = 0; k < list.Count; k++) { var go = list[k]?.root; if (go != null) go.SetActive(k == idx && meshVisible); }
                if (meshVisible && idx < list.Count) CompositeGarment(list[idx]); // fabric (+ under-layers) over live skin
            }
            ApplyClothingOcclusion();
            ApplyMorphs(); // occlusion may instance nude meshes (resets blend weights) → re-apply morphs
            ApplySkinNormal(); // newly equipped garments need the masked skin-detail normal pushed
            Recompose();       // skin layers (tights/socks) live inside the atlas chain
        }

        // ---- DIAGNOSTICS: independently show/hide the body, clothing, and hair meshes, and toggle the
        // body-occlusion cull. They ARE separate renderers, so this just flips renderer.enabled (visual
        // only; does not change what is "worn"). Lets you inspect how each layer is constructed. ----
        private IEnumerable<GameObject> ClothingRoots()
        {
            for (var slot = 0; slot < ClothingSlotCount; slot++) // generic: new slots can never be missed again
            {
                var list = ClothingList(slot);
                if (list == null) continue;
                foreach (var it in list)
                    if (it?.root != null) yield return it.root;
            }
        }
        private IEnumerable<GameObject> HairRoots()
        {
            if (hairStyles != null)
                foreach (var s in hairStyles)
                    if (s?.root != null) yield return s.root;
        }

        public void SetClothingVisible(bool visible)
        {
            foreach (var go in ClothingRoots())
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }
        public void SetHairVisible(bool visible)
        {
            foreach (var go in HairRoots())
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }
        // Body = every renderer that is NOT under a clothing or hair root (nude regions + head/eyes/brows).
        public void SetBodyVisible(bool visible)
        {
            var exclude = new HashSet<Transform>();
            foreach (var go in ClothingRoots()) exclude.Add(go.transform);
            foreach (var go in HairRoots()) exclude.Add(go.transform);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var under = false;
                for (var t = r.transform; t != null; t = t.parent)
                    if (exclude.Contains(t)) { under = true; break; }
                if (!under) r.enabled = visible;
            }
        }
        // When disabled, SHOW every nude region fully (undo the hem-clip) so you can see the body under the
        // clothes; re-enabling re-clips the regions the worn garments cover.
        public void SetOcclusionEnabled(bool enabled)
        {
            if (enabled) { ApplyClothingOcclusion(); return; }
            RestoreNudeClips();
            SetRegionVisible(topVariants, topIndex, true);
            SetRegionVisible(bottomVariants, bottomIndex, true);
            SetRegionVisible(feetVariants, feetIndex, true);
        }

        // GAME-FAITHFUL clothing (proven by RenderDoc eid 602: albedo = lerp(skin, outfit.rgb, outfit.a)).
        // The garment is ONE opaque mesh that already contains its exposed-skin faces; we composite the
        // fabric OVER the live skin atlas PER-TEXEL by the fabric alpha (a mask, not transparency) into the
        // garment material's base map, and hide the nude regions the garment covers WHOLESALE. No geometry
        // cutting and no second body mesh — so straps stay intact, the fabric/skin edge is smooth, and
        // nothing z-fights. Exposed-skin faces carry body-UVs, so they sample the live skin at the matching
        // UV; a tone/skin change recomposes the skin atlas and we re-composite every worn garment.
        private static readonly int ClothSkinTexId = Shader.PropertyToID("_SkinTex");
        private static readonly int ClothSkinNormalId = Shader.PropertyToID("_SkinNormal");
        private static readonly int ClothUnderAId = Shader.PropertyToID("_UnderA");
        private static readonly int ClothUnderBId = Shader.PropertyToID("_UnderB");
        private static readonly int ClothHasUnderAId = Shader.PropertyToID("_HasUnderA");
        private static readonly int ClothHasUnderBId = Shader.PropertyToID("_HasUnderB");
        private static readonly int SssMaskMapId = Shader.PropertyToID("_SubsurfaceMaskMap");
        [NonSerialized] private Material _clothCompositeMat;
        [NonSerialized] private Material _clothNormalMat;

        // A clipped nude region: our owned instance + its original full triangle list (to restore).
        private sealed class NudeClip { public SkinnedMeshRenderer smr; public Mesh original, instance; public int[] origTris; }
        [NonSerialized] private readonly List<NudeClip> _nudeClips = new();
        private NudeClip FindNudeClip(SkinnedMeshRenderer smr) { foreach (var c in _nudeClips) if (c.smr == smr) return c; return null; }
        private const float ClipOverlap = 0.02f; // keep nude ~2cm INTO the covered zone so the seam tucks under the fabric hem

        // MASK the nude body: for each covered region, keep the nude faces BELOW the covering garment's hem
        // (exposed skin — calves under cropped jeans / a knee dress, midriff under a crop top) and hide the
        // faces ABOVE it (the fabric covers them). Adapts per garment: a garment that bakes its own exposed
        // skin reaches low (hem near the ankle) so nothing nude is kept — no double mesh; one that stops at
        // the hem keeps the nude below it. This is the fine-region masking the game does, by hem height.
        private void ApplyClothingOcclusion()
        {
            bool covTop = false, covBottom = false, covFeet = false;
            float hemTopL = float.PositiveInfinity, hemTopR = float.PositiveInfinity;
            float hemBotL = float.PositiveInfinity, hemBotR = float.PositiveInfinity;
            for (var slot = 0; slot < ClothingSlotCount; slot++)
            {
                var list = ClothingList(slot); var idx = GetClothingIndex(slot);
                if (list == null || idx <= 0 || idx >= list.Count) continue;
                if (IsLayeredUnder(slot)) continue; // layered underwear is texture-only: its MESH defines no coverage
                var it = list[idx]; var cov = it?.covers; if (cov == null || it.root == null) continue;
                var (hemL, hemR) = GarmentHemY(it);
                foreach (var r in cov)
                {
                    if (r == "top") { covTop = true; hemTopL = Mathf.Min(hemTopL, hemL); hemTopR = Mathf.Min(hemTopR, hemR); }
                    else if (r == "bottom") { covBottom = true; hemBotL = Mathf.Min(hemBotL, hemL); hemBotR = Mathf.Min(hemBotR, hemR); }
                    else if (r == "feet") covFeet = true;
                }
            }
            ClipRegionBelow(topVariants, topIndex, covTop, hemTopL, hemTopR);
            ClipRegionBelow(bottomVariants, bottomIndex, covBottom, hemBotL, hemBotR);
            SetRegionVisible(feetVariants, feetIndex, !covFeet); // shoes bake their own exposed foot skin
        }

        // Lowest Y of the garment's CENTRAL column (|x| < 0.25), measured PER SIDE — EA meshes can bake
        // their exposed legs ASYMMETRICALLY (DressKneeBelt: left leg to Y=0.43, right only to Y=0.57), so a
        // single hem would leave the shorter side's knee uncovered. Centre verts (|x|<=0.02) count for both.
        // Excludes the arms/hands (|x|>=0.25), which would drag a top's hem down to the wrists.
        private static (float left, float right) GarmentHemY(ClothingItem it)
        {
            float loL = float.PositiveInfinity, loR = float.PositiveInfinity;
            foreach (var smr in it.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = smr.sharedMesh; if (m == null) continue;
                var vs = m.vertices;
                for (var i = 0; i < vs.Length; i++)
                {
                    var x = vs[i].x;
                    if (Mathf.Abs(x) >= 0.25f) continue;
                    var y = vs[i].y;
                    if (x >= -0.02f && y < loL) loL = y;
                    if (x <= 0.02f && y < loR) loR = y;
                }
            }
            return (loL, loR);
        }

        // Keep the nude faces below their side's hem (+ overlap); hide the rest. covered=false restores.
        private void ClipRegionBelow(List<MeshVariant> list, int idx, bool covered, float hemL, float hemR)
        {
            if (list == null || idx < 0 || idx >= list.Count) return;
            var go = list[idx]?.root; if (go == null) return;
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null) { go.SetActive(!covered); return; }
            var clip = FindNudeClip(smr);

            // Does the garment bake its own exposed skin all the way down (BOTH sides)? Then the whole
            // nude region can hide — no thin nude ring left z-fighting under it.
            var extentMesh = clip != null ? clip.original : smr.sharedMesh;
            var ev = extentMesh.vertices;
            var regionMinY = float.PositiveInfinity;
            for (var i = 0; i < ev.Length; i++) if (ev[i].y < regionMinY) regionMinY = ev[i].y;
            var reachesBottom = hemL <= regionMinY + 0.05f && hemR <= regionMinY + 0.05f;

            // Edit mode: never instance/mutate meshes (unsafe for scene serialization). Whole-hide only
            // when the garment genuinely replaces the region; otherwise keep the nude region VISIBLE so
            // limbs don't vanish in the editor preview (the fine per-side clip runs in Play mode).
            if (!Application.isPlaying)
            {
                go.SetActive(!(covered && reachesBottom));
                if (go.activeSelf) foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                return;
            }

            if (!covered)
            {
                if (clip != null && ReferenceEquals(smr.sharedMesh, clip.instance)) clip.instance.triangles = clip.origTris;
                go.SetActive(true);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                return;
            }

            if (reachesBottom)
            {
                if (clip != null && ReferenceEquals(smr.sharedMesh, clip.instance)) clip.instance.triangles = clip.origTris;
                go.SetActive(false);
                return;
            }

            if (clip == null)
            {
                var orig = smr.sharedMesh;
                var inst = Instantiate(orig); inst.name = orig.name + "_clip";
                smr.sharedMesh = inst; // instancing resets blend weights → callers re-run ApplyMorphs()
                clip = new NudeClip { smr = smr, original = orig, instance = inst, origTris = inst.triangles };
                _nudeClips.Add(clip);
            }
            var mesh = clip.instance; var tris = clip.origTris; var verts = mesh.vertices;
            if (verts == null || verts.Length == 0) return;
            var cutL = hemL + ClipOverlap;
            var cutR = hemR + ClipOverlap;
            var kept = new List<int>(tris.Length);
            for (var t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                if (a >= verts.Length || b >= verts.Length || c >= verts.Length) continue;
                var cy = (verts[a].y + verts[b].y + verts[c].y) * (1f / 3f);
                var cx = (verts[a].x + verts[b].x + verts[c].x) * (1f / 3f);
                var cut = cx >= 0f ? cutL : cutR; // same sign convention as GarmentHemY
                if (cy < cut) { kept.Add(a); kept.Add(b); kept.Add(c); } // below the hem → exposed skin → keep
            }
            mesh.triangles = kept.ToArray();
            var any = kept.Count > 0;
            go.SetActive(any);
            if (any) foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        }

        private void RestoreNudeClips()
        {
            foreach (var c in _nudeClips)
                if (c?.instance != null && c.origTris != null && c.smr != null && ReferenceEquals(c.smr.sharedMesh, c.instance))
                    c.instance.triangles = c.origTris;
        }

        private static void SetRegionVisible(List<MeshVariant> list, int idx, bool visible)
        {
            if (list == null || idx < 0 || idx >= list.Count) return;
            var go = list[idx]?.root; if (go == null) return;
            go.SetActive(visible);
            if (visible)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        }

        // The worn under-layer fabric for a region ("top" → bra, "bottom" → panties), else null.
        private Texture UnderLayerFabric(string region)
        {
            var slot = region == "top" ? 5 : region == "bottom" ? 6 : -1;
            if (slot < 0 || !IsLayeredUnder(slot)) return null;
            var list = ClothingList(slot); var idx = GetClothingIndex(slot);
            if (list == null || idx <= 0 || idx >= list.Count) return null;
            var it = list[idx];
            if (it?.colors == null || it.colors.Count == 0) return null;
            return it.colors[Clamp(it.colorIndex, it.colors.Count)].diffuse;
        }

        // Composite a worn garment's base map: live skin, then any UNDER-LAYER fabrics for the regions
        // this garment covers (bra under a top/dress, panties under a bottom/dress — the game's
        // texture-only layering), then the garment's own fabric on top:
        //   albedo = lerp(lerp(lerp(skin, under0, a0), under1, a1), fabric, a)
        // Bound as the garment material's base map. Called on equip, colour change, and whenever the
        // skin atlas is recomposed (tone/skin/detail change).
        private void CompositeGarment(ClothingItem it)
        {
            if (it?.material == null || it.colors == null || it.colors.Count == 0) return;
            var fabric = it.colors[Clamp(it.colorIndex, it.colors.Count)].diffuse;
            if (fabric == null) return;

            var skin = CurrentSkinAtlas();
            if (skin == null) { it.material.SetTexture(BaseColorMapId, fabric); return; } // no skin yet: show raw fabric

            if (_clothCompositeMat == null)
            {
                var sh = Shader.Find("Hidden/Sims4/ClothComposite");
                if (sh == null) { it.material.SetTexture(BaseColorMapId, fabric); return; }
                _clothCompositeMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }

            int w = fabric.width > 0 ? fabric.width : 1024, h = fabric.height > 0 ? fabric.height : 1024;
            if (it.composite == null || it.composite.width != w || it.composite.height != h)
            {
                if (it.composite != null) it.composite.Release();
                var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0)
                { sRGB = true, useMipMap = false, autoGenerateMips = false };
                it.composite = new RenderTexture(desc)
                {
                    name = it.id + "_cloth",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                it.composite.Create();
            }

            // Chain: skin → under-layer(s) → this garment's fabric. Each blit lerps one fabric over the
            // running base by its alpha; ping-pong through temporaries for the under-layers.
            Texture baseTex = skin;
            RenderTexture tmp = null;
            if (it.covers != null)
            {
                foreach (var region in it.covers)
                {
                    var under = UnderLayerFabric(region);
                    if (under == null) continue;
                    var next = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    _clothCompositeMat.SetTexture(ClothSkinTexId, baseTex);
                    Graphics.Blit(under, next, _clothCompositeMat, 0);
                    if (tmp != null) RenderTexture.ReleaseTemporary(tmp);
                    tmp = next;
                    baseTex = next;
                }
            }
            _clothCompositeMat.SetTexture(ClothSkinTexId, baseTex);
            Graphics.Blit(fabric, it.composite, _clothCompositeMat, 0);
            if (tmp != null) RenderTexture.ReleaseTemporary(tmp);
            it.material.SetTexture(BaseColorMapId, it.composite);
        }

        // Re-composite every garment whose mesh is visible (e.g. after the skin atlas changed).
        private void RecompositeWornGarments()
        {
            for (var slot = 0; slot < ClothingSlotCount; slot++)
            {
                var list = ClothingList(slot); var idx = GetClothingIndex(slot);
                if (list == null || idx <= 0 || idx >= list.Count) continue;
                if (IsLayeredUnder(slot)) continue; // its fabric rides in the outer garment's composite
                CompositeGarment(list[idx]);
            }
        }

        // The current live skin albedo (the recomposed RT, else the shared skin material's base map).
        private Texture CurrentSkinAtlas()
        {
            if (_composited != null) return _composited;
            if (skinMaterial != null && skinMaterial.HasProperty(BaseColorMapId)) return skinMaterial.GetTexture(BaseColorMapId);
            return null;
        }

        // ---- SKIN TEXTURE LAYERS (tights slot 7, socks slot 4): mesh-less parts whose diffuse is
        // composited INTO the live skin atlas (legs region of the shared body UV). Because they blend
        // into the atlas itself, they show on the nude legs AND under every garment's baked skin, and
        // they sit UNDER real clothing exactly like in the game. Chain order: skin -> tights -> socks.
        // Compose order: lipstick → eyeshadow → eyeliner → blush → lashes over the liner → brows LAST
        // on the face (nothing may tint them) → body layers (gloves/tights/socks; disjoint regions).
        private static readonly int[] SkinLayerSlots = { 14, 15, 16, 17, 19, 18, 11, 7, 4 };
        [NonSerialized] private RenderTexture _skinLayerRtA, _skinLayerRtB;

        // 4=Socks 7=Tights 11=Gloves (EA gloves are painted-on-skin, no mesh) + face layers 14..19.
        private bool IsSkinLayerSlot(int slot) => slot == 4 || slot == 7 || slot == 11 || (slot >= FirstMakeupSlot && slot < ClothingSlotCount);

        private Texture SkinLayerFabric(int slot)
        {
            var list = ClothingList(slot); var idx = GetClothingIndex(slot);
            if (list == null || idx <= 0 || idx >= list.Count) return null;
            var it = list[idx];
            if (it?.colors == null || it.colors.Count == 0) return null;
            return it.colors[Clamp(it.colorIndex, it.colors.Count)].diffuse;
        }

        // Blit-chain the worn skin layers over the composed skin. Returns the final atlas texture
        // (the input when no layer is worn). The result RTs persist (materials reference them).
        private RenderTexture ApplySkinLayers(RenderTexture skin)
        {
            var layers = new List<Texture>(2);
            foreach (var slot in SkinLayerSlots)
            {
                var f = SkinLayerFabric(slot);
                if (f != null) layers.Add(f);
            }
            if (layers.Count == 0 || skin == null) return skin;

            if (_clothCompositeMat == null)
            {
                var sh = Shader.Find("Hidden/Sims4/ClothComposite");
                if (sh == null) return skin;
                _clothCompositeMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
            RenderTexture Ensure(ref RenderTexture rt, string name)
            {
                if (rt == null || rt.width != skin.width || rt.height != skin.height)
                {
                    if (rt != null) rt.Release();
                    var desc = new RenderTextureDescriptor(skin.width, skin.height, RenderTextureFormat.ARGB32, 0)
                    { sRGB = true, useMipMap = false, autoGenerateMips = false };
                    rt = new RenderTexture(desc) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                    rt.Create();
                }
                return rt;
            }
            Texture src = skin;
            RenderTexture dst = null;
            for (var i = 0; i < layers.Count; i++)
            {
                dst = (i % 2 == 0) ? Ensure(ref _skinLayerRtA, "Sims4SkinLayerA") : Ensure(ref _skinLayerRtB, "Sims4SkinLayerB");
                _clothCompositeMat.SetTexture(ClothSkinTexId, src);
                Graphics.Blit(layers[i], dst, _clothCompositeMat, 0);
                src = dst;
            }
            return dst;
        }

        /// <summary>Activate only option <paramref name="idx"/> in a region list; deactivate the others.</summary>
        private static void ApplyRegion(List<MeshVariant> list, int idx)
        {
            if (list == null || list.Count == 0) return;
            for (var i = 0; i < list.Count; i++)
            {
                var go = list[i]?.root;
                if (go == null) continue;
                var active = i == idx;
                go.SetActive(active);
                if (active)
                {
                    // Be authoritative over the active option's renderers so a stray
                    // renderer.enabled=false can never leave the selected mesh invisible.
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        r.enabled = true;
                    }
                }
            }
        }

        // ---- Recompose ------------------------------------------------------------------------

        /// <summary>
        /// Rebuild the skin albedo from the current selection and assign it to the skin material's
        /// _BaseColorMap. Safe to call repeatedly; reuses the compositor and its RenderTextures.
        /// </summary>
        public void Recompose()
        {
            eyeColorIndex = Clamp(eyeColorIndex, eyeColors.Count);

            // Bottom of the stack = the selected SKIN BASE (color albedo: EA / Obscurus / PsBoss).
            Texture baseTex = null;
            if (baseSkins.Count > 0)
            {
                baseSkinIndex = Clamp(baseSkinIndex, baseSkins.Count);
                baseTex = baseSkins[baseSkinIndex]?.albedo;
            }
            // The EA Default base is TONE-DRIVEN: the Skin Tone selection supplies the bottom layer
            // (the EA base albedo is just the default tone frozen). CC bases (Obscurus/PsBoss) are
            // full-color skins that REPLACE the tone system — tones don't apply there (UI dims them).
            if (TonesApply && baseTones.Count > 0)
            {
                baseToneIndex = Clamp(baseToneIndex, baseTones.Count);
                var tone = baseTones[baseToneIndex]?.texture;
                if (tone != null) baseTex = tone;
            }
            if (baseTex == null && baseTones.Count > 0)
            {
                baseToneIndex = Clamp(baseToneIndex, baseTones.Count);
                baseTex = baseTones[baseToneIndex]?.texture;
            }
            if (baseTex == null)
            {
                return;
            }

            var activeDetails = new List<Texture>();
            var detailModes = new List<bool>(); // true = colored source-over, false = grayscale overlay
            foreach (var d in detailLayers)
            {
                if (d != null && d.active && d.texture != null)
                {
                    activeDetails.Add(d.texture);
                    detailModes.Add(string.Equals(d.blend, "over", StringComparison.OrdinalIgnoreCase));
                }
            }

            Texture eyeTex = null;
            if (eyeColors.Count > 0)
            {
                eyeTex = eyeColors[eyeColorIndex]?.texture;
            }

            _compositor ??= new SkinCompositor();
            var rt = _compositor.Compose(baseTex, activeDetails, detailModes, eyeTex);
            if (rt == null)
            {
                return;
            }

            _composited = ApplySkinLayers(rt); // tights/socks composite INTO the atlas (legs region)

            if (skinMaterial != null && skinMaterial.HasProperty(BaseColorMapId))
            {
                skinMaterial.SetTexture(BaseColorMapId, _composited);
            }

            RecompositeWornGarments(); // the live skin changed → refresh exposed skin on every worn garment
        }

        // ---- Unity lifecycle ------------------------------------------------------------------

        private void OnEnable()
        {
            EnsureFacialDefaults(); // seed emotion/viseme poses (once) before the first bone-morph recompute
            ApplyMeshVariants();
            ApplyClothing(); // after ApplyMeshVariants: re-hide the nude regions the worn garments cover
            Recompose();
            ApplyMorphs();
            ApplySkinNormal();
            ApplySkinSmoothness();
        }

        private void OnValidate()
        {
            // Clamp indices edited via the default inspector, then recompose. OnValidate can fire
            // before the catalog is populated (e.g. during deserialization) — Recompose no-ops then.
            baseToneIndex = Clamp(baseToneIndex, baseTones.Count);
            eyeColorIndex = Clamp(eyeColorIndex, eyeColors.Count);
            baseSkinIndex = Clamp(baseSkinIndex, baseSkins.Count);
            topIndex = Clamp(topIndex, topVariants.Count);
            bottomIndex = Clamp(bottomIndex, bottomVariants.Count);
            feetIndex = Clamp(feetIndex, feetVariants.Count);

            // Defer one tick: OnValidate may run on a worker context where Graphics.Blit/material
            // mutation is unsafe. In the editor, delayCall guarantees the main thread; at runtime
            // OnEnable already drove the first recompose, so a direct call is fine.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += DeferredRecompose;
#else
            Recompose();
#endif
        }

#if UNITY_EDITOR
        private void DeferredRecompose()
        {
            // The object may have been destroyed between OnValidate and the deferred callback.
            if (this == null)
            {
                return;
            }

            ApplyMeshVariants();
            ApplyClothing();
            Recompose();
            ApplyMorphs();
            ApplySkinNormal();
        }
#endif

        private void OnDisable()
        {
            _compositor?.Dispose();
            _compositor = null;
            _composited = null;

            // Release the per-garment composite RenderTextures (native objects are not GC-collected).
            // Generic slot loop: a by-name array here silently missed newly added slots (review find).
            for (var slot = 0; slot < ClothingSlotCount; slot++)
            {
                var list = ClothingList(slot);
                if (list == null) continue;
                foreach (var it in list)
                {
                    if (it == null) continue;
                    if (it.composite != null)
                    {
                        it.composite.Release();
                        if (Application.isPlaying) Destroy(it.composite); else DestroyImmediate(it.composite);
                        it.composite = null;
                    }
                    if (it.normalComposite != null)
                    {
                        it.normalComposite.Release();
                        if (Application.isPlaying) Destroy(it.normalComposite); else DestroyImmediate(it.normalComposite);
                        it.normalComposite = null;
                    }
                }
            }
            if (_clothCompositeMat != null)
            {
                if (Application.isPlaying) Destroy(_clothCompositeMat); else DestroyImmediate(_clothCompositeMat);
                _clothCompositeMat = null;
            }
            if (_clothNormalMat != null)
            {
                if (Application.isPlaying) Destroy(_clothNormalMat); else DestroyImmediate(_clothNormalMat);
                _clothNormalMat = null;
            }
            if (_skinLayerRtA != null) { _skinLayerRtA.Release(); if (Application.isPlaying) Destroy(_skinLayerRtA); else DestroyImmediate(_skinLayerRtA); _skinLayerRtA = null; }
            if (_skinLayerRtB != null) { _skinLayerRtB.Release(); if (Application.isPlaying) Destroy(_skinLayerRtB); else DestroyImmediate(_skinLayerRtB); _skinLayerRtB = null; }

            // Restore each clipped nude renderer to its original mesh and destroy our owned instances.
            foreach (var c in _nudeClips)
            {
                if (c == null) continue;
                if (c.smr != null && c.original != null) c.smr.sharedMesh = c.original;
                if (c.instance != null)
                {
                    if (Application.isPlaying) Destroy(c.instance); else DestroyImmediate(c.instance);
                }
            }
            _nudeClips.Clear();
        }

        private static int Clamp(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(index, 0, count - 1);
        }
    }
}
