using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// Runtime hub for the custom physics cloth skirt. Owns:
    /// 1. the Cloth constraint gradient (waistband pinned, hem free) — applied at runtime because
    ///    Unity Cloth welds/reorders sim vertices on init and may init after a sibling's OnEnable;
    /// 2. live PHYSICS tunables (stiffness/damping/motion/solver/collider padding) — applied to the
    ///    Cloth component and its capsule colliders on demand;
    /// 3. live GEOMETRY tunables (length, hem width) — the mesh is regenerated from the body profile
    ///    the builder baked in (radii sampled off the actual nude bottom mesh), then the Cloth is
    ///    bounced so it re-reads the new rest shape. Rebuilds are debounced while a slider drags.
    /// </summary>
    [RequireComponent(typeof(Cloth))]
    public sealed class SkirtClothTuner : MonoBehaviour
    {
        [Header("Baked by the builder (do not edit)")]
        public int rings = 24;
        public int segments = 48;
        public float waistY = 1.105f;
        public float[] profileRx; // body max |x| sampled at ProfileSamples heights, waist → ankle
        public float[] profileRz; // body max |z| at the same heights
        public float[] baseCapsuleRadii; // fitted collider radii (same order as cloth.capsuleColliders) — pad multiplies these
        public const float ProfileBottomY = 0.15f;
        public const int ProfileSamples = 32;
        public const float MiniHemY = 0.85f;
        public const float AnkleHemY = 0.18f;
        public const float DefaultLength = 0.55f; // ≈ knee — also what the builder bakes the initial mesh at
        public const float DefaultHemFlare = 1f;

        [Header("Geometry (live)")]
        [Range(0f, 1f)] public float length = DefaultLength;      // 0 = mini … 1 = ankle
        [Range(0.7f, 2f)] public float hemFlare = DefaultHemFlare; // scales the hem clearance, never cuts into the body

        [Header("Physics (live)")]
        [Range(0.01f, 1f)] public float bendStiffness = 0.10f;
        [Range(0.3f, 1f)] public float stretchStiffness = 0.95f;
        [Range(0f, 1f)] public float damping = 0.55f;
        [Range(0f, 1f)] public float motionResponse = 0.18f; // how much character motion drags the cloth
        [Range(0f, 1f)] public float sway = 0.12f;           // idle random acceleration
        [Range(0.05f, 0.8f)] public float hemFreedom = 0.45f; // max deviation from the skinned pose at the hem
        [Range(60f, 480f)] public float solverHz = 320f;
        [Range(0.8f, 1.6f)] public float colliderPad = 1f;    // scales every capsule radius
        [Range(0f, 0.5f)] public float pinBand = 0.14f;

        public float HemY => Mathf.Lerp(MiniHemY, AnkleHemY, length);

        private Mesh _runtimeMesh;             // play-mode clone — the baked asset is never mutated
        private bool _coeffApplied;
        private bool _geoDirty;
        private float _lastGeoChange;
        private float _lastAppliedPad = -1f;   // X-ray overlay refresh only when the pad actually changed
        private ClothSkinningCoefficient[] _coeffCache; // reused — sliders drag at frame rate

        private void OnEnable()
        {
            _coeffApplied = false; // Cloth re-inits on every enable → gradient must be re-applied
            ApplyPhysics();
        }

        private void Update()
        {
            if (!_coeffApplied) TryApplyCoefficients(); // Cloth may init after our OnEnable
            if (_geoDirty && Time.unscaledTime - _lastGeoChange > 0.25f) RebuildGeometry();
        }

        /// <summary>UI hook: geometry slider changed — rebuild after the drag settles.</summary>
        public void NotifyGeometryChanged()
        {
            _geoDirty = true;
            _lastGeoChange = Time.unscaledTime;
        }

        /// <summary>UI hook: physics slider changed — cheap, applied immediately.</summary>
        public void NotifyPhysicsChanged() => ApplyPhysics();

        /// <summary>Bounce the Cloth so the drape restarts from the skinned pose.</summary>
        public void Redrape()
        {
            var cloth = GetComponent<Cloth>();
            cloth.enabled = false;
            cloth.enabled = true;
            _coeffApplied = false;
            ApplyPhysics();
        }

        public void ResetDefaults()
        {
            length = DefaultLength; hemFlare = DefaultHemFlare;
            bendStiffness = 0.10f; stretchStiffness = 0.95f; damping = 0.55f;
            motionResponse = 0.18f; sway = 0.12f; hemFreedom = 0.45f;
            solverHz = 320f; colliderPad = 1f; pinBand = 0.14f;
            NotifyGeometryChanged();
            ApplyPhysics();
        }

        public void ApplyPhysics()
        {
            var cloth = GetComponent<Cloth>();
            cloth.bendingStiffness = bendStiffness;
            cloth.stretchingStiffness = stretchStiffness;
            cloth.damping = damping;
            cloth.worldVelocityScale = motionResponse;
            cloth.worldAccelerationScale = Mathf.Min(1f, motionResponse * 2f);
            cloth.randomAcceleration = new Vector3(sway, 0f, sway);
            cloth.clothSolverFrequency = solverHz;
            // Radii come from the builder-baked fitted values (survives mid-play domain reloads,
            // where a lazy capture would re-bake ALREADY-padded radii and compound the pad).
            var caps = cloth.capsuleColliders;
            if (caps != null && baseCapsuleRadii != null && baseCapsuleRadii.Length == caps.Length)
                for (var i = 0; i < caps.Length; i++)
                    if (caps[i] != null) caps[i].radius = baseCapsuleRadii[i] * colliderPad;
            _coeffApplied = false; // pinBand/hemFreedom feed the gradient
            TryApplyCoefficients();
            // The X-ray overlay bakes collider sizes when shown — refresh it, but only on an actual
            // pad change (SetShown rebuilds every overlay mesh; doing that per drag frame hitches).
            if (!Mathf.Approximately(colliderPad, _lastAppliedPad))
            {
                _lastAppliedPad = colliderPad;
                var xray = GetComponentInParent<ClothColliderXray>();
                if (xray != null && xray.Shown) xray.SetShown(true);
            }
        }

        private void TryApplyCoefficients()
        {
            var cloth = GetComponent<Cloth>();
            var vs = cloth.vertices;
            if (vs == null || vs.Length == 0) return; // cloth not initialised yet — retry next frame
            if (_coeffCache == null || _coeffCache.Length != vs.Length) _coeffCache = new ClothSkinningCoefficient[vs.Length];
            var co = _coeffCache;
            var hemY = HemY;
            var span = Mathf.Max(0.0001f, waistY - hemY);
            for (var i = 0; i < vs.Length; i++)
            {
                var t = Mathf.Clamp01((waistY - vs[i].y) / span);
                var free = t <= pinBand ? 0f
                    : Mathf.Pow((t - pinBand) / (1f - pinBand), 1.3f) * hemFreedom;
                co[i] = new ClothSkinningCoefficient { maxDistance = free, collisionSphereDistance = 0.006f };
            }
            cloth.coefficients = co;
            _coeffApplied = true;
        }

        private void RebuildGeometry()
        {
            _geoDirty = false;
            var smr = GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null || profileRx == null || profileRx.Length == 0) return;
            if (_runtimeMesh == null)
            {
                _runtimeMesh = Instantiate(smr.sharedMesh);
                _runtimeMesh.name = smr.sharedMesh.name + "_live";
                smr.sharedMesh = _runtimeMesh;
            }
            var verts = new Vector3[rings * segments];
            ComputeVertices(verts, rings, segments, waistY, HemY, hemFlare, profileRx, profileRz);
            _runtimeMesh.vertices = verts;
            _runtimeMesh.RecalculateNormals();
            _runtimeMesh.RecalculateTangents();
            _runtimeMesh.RecalculateBounds();
            smr.localBounds = _runtimeMesh.bounds;
            // Cloth caches the mesh on enable — bounce it to re-read the new rest shape.
            var cloth = GetComponent<Cloth>();
            var wasEnabled = cloth.enabled;
            cloth.enabled = false;
            if (wasEnabled) cloth.enabled = true;
            _coeffApplied = false;
            TryApplyCoefficients();
        }

        /// <summary>
        /// SINGLE source of truth for skirt vertex positions — the editor builder bakes the initial
        /// mesh through this same function, so runtime rebuilds can never drift from it.
        /// Ring radius = body extent (interpolated from the baked profile) + clearance growing toward
        /// the hem; hemFlare scales only the clearance so the skirt can never cut into the body.
        /// The silhouette is forced monotonic (a skirt never narrows on the way down).
        /// </summary>
        public static void ComputeVertices(
            Vector3[] into, int rings, int segments, float waistY, float hemY, float hemFlare,
            float[] profileRx, float[] profileRz)
        {
            var prevRx = 0f;
            var prevRz = 0f;
            for (var i = 0; i < rings; i++)
            {
                var t = i / (float)(rings - 1);
                var y = Mathf.Lerp(waistY, hemY, t);
                var clearance = Mathf.Lerp(0.018f, 0.10f * hemFlare, Mathf.Pow(t, 1.4f)); // waistband stays snug; flare scales the hem end only
                var rx = SampleProfile(profileRx, waistY, y) + clearance;
                var rz = SampleProfile(profileRz, waistY, y) + clearance;
                if (i > 0) { rx = Mathf.Max(rx, prevRx); rz = Mathf.Max(rz, prevRz); }
                prevRx = rx;
                prevRz = rz;
                for (var s = 0; s < segments; s++)
                {
                    var ang = Mathf.PI * 2f * s / segments;
                    into[(i * segments) + s] = new Vector3(Mathf.Cos(ang) * rx, y, Mathf.Sin(ang) * rz);
                }
            }
        }

        private static float SampleProfile(float[] profile, float waistY, float y)
        {
            if (profile == null || profile.Length == 0) return 0.16f;
            var t = Mathf.Clamp01((waistY - y) / Mathf.Max(0.0001f, waistY - ProfileBottomY)) * (profile.Length - 1);
            var i0 = Mathf.FloorToInt(t);
            var i1 = Mathf.Min(i0 + 1, profile.Length - 1);
            return Mathf.Lerp(profile[i0], profile[i1], t - i0);
        }
    }
}
