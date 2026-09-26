using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Live render tunables for the debug menu (TASK-016): the artistic/grade knobs stay HERE until the
    /// user settles values, then we bake them as scene defaults. Edits go to the volume's sharedProfile
    /// (the asset), so in-editor tuning persists — plus <see cref="Snapshot"/> logs the whole state for
    /// the bake handoff.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RenderDebugController : MonoBehaviour
    {
        [Header("Wired by the scene builder")]
        public Volume volume;                      // the Game Volume (sky/exposure/GI/grade overrides)
        public SkyTimeController sky;              // EV offset rides on its computed fixed exposure
        public HDAdditionalLightData sunHd;        // shadow dimmer
        public Light fillLight;                    // shadowless fill, default OFF
        public HDAdditionalLightData fillHd;

        [SerializeField] private float fillLux = 4000f;

        private GlobalIllumination _ssgi;
        private IndirectLightingController _indirect;
        private Tonemapping _tonemap;
        private ShadowsMidtonesHighlights _smh;
        private bool _resolved;

        /// <summary>False when the scene predates the rig (menu shows a rebuild hint instead of knobs).</summary>
        public bool Ready => Resolve();

        private bool Resolve()
        {
            if (_resolved) return true;
            var p = volume != null ? volume.sharedProfile : null;
            if (p == null) return false;
            p.TryGet(out _ssgi);
            p.TryGet(out _indirect);
            p.TryGet(out _tonemap);
            p.TryGet(out _smh);
            _resolved = true;
            return true;
        }

        // ---- knobs (each setter applies immediately) ----

        public bool SsgiEnabled
        {
            get => Resolve() && _ssgi != null && _ssgi.enable.value;
            set { if (Resolve() && _ssgi != null) _ssgi.enable.Override(value); }
        }

        public float ShadowDimmer
        {
            get => sunHd != null ? sunHd.shadowDimmer : 1f;
            set { if (sunHd != null) sunHd.shadowDimmer = value; }
        }

        public float AmbientBoost
        {
            get => Resolve() && _indirect != null ? _indirect.indirectDiffuseLightingMultiplier.value : 1f;
            set { if (Resolve() && _indirect != null) _indirect.indirectDiffuseLightingMultiplier.Override(value); }
        }

        public float EvOffset
        {
            get => sky != null ? sky.exposureOffset : 0f;
            set { if (sky != null) { sky.exposureOffset = value; sky.Apply(); } }
        }

        public float MoonMaxLux
        {
            get => sky != null ? sky.moonMaxLux : 25f;
            set { if (sky != null) { sky.moonMaxLux = value; sky.Apply(); } }
        }

        /// <summary>Fixed EV at full night (day = 13). LOWER = brighter night.</summary>
        public float NightEv
        {
            get => sky != null ? sky.nightEv : 3f;
            set { if (sky != null) { sky.nightEv = value; sky.Apply(); } }
        }

        public bool FillEnabled
        {
            get => fillLight != null && fillLight.gameObject.activeSelf;
            set { if (fillLight != null) fillLight.gameObject.SetActive(value); }
        }

        public float FillLux
        {
            get => fillLux;
            set
            {
                fillLux = value;
                if (fillLight != null) { fillLight.lightUnit = LightUnit.Lux; fillLight.intensity = value; }
            }
        }

        /// <summary>0 = None, 1 = Neutral, 2 = ACES.</summary>
        public int TonemapIndex
        {
            get
            {
                if (!Resolve() || _tonemap == null) return 1;
                switch (_tonemap.mode.value)
                {
                    case TonemappingMode.None: return 0;
                    case TonemappingMode.ACES: return 2;
                    default: return 1;
                }
            }
            set
            {
                if (!Resolve() || _tonemap == null) return;
                var mode = value == 0 ? TonemappingMode.None : value == 2 ? TonemappingMode.ACES : TonemappingMode.Neutral;
                _tonemap.mode.Override(mode);
            }
        }

        /// <summary>Shadows offset of the Shadows/Midtones/Highlights grade — a gentle lift out of black.</summary>
        public float ShadowLift
        {
            get => Resolve() && _smh != null ? _smh.shadows.value.w : 0f;
            set
            {
                if (!Resolve() || _smh == null) return;
                var v = _smh.shadows.value;
                v.w = value;
                _smh.shadows.Override(v);
            }
        }

        /// <summary>One-line state dump — the bake handoff ("these are the values I liked").</summary>
        public string Snapshot()
        {
            string tone = TonemapIndex == 0 ? "None" : TonemapIndex == 2 ? "ACES" : "Neutral";
            return "[RenderTuning] " +
                   $"SSGI={(SsgiEnabled ? "on" : "off")} · shadowDimmer={ShadowDimmer:0.00} · ambient×={AmbientBoost:0.00} · " +
                   $"EVoffset={EvOffset:+0.0;-0.0;0} · fill={(FillEnabled ? $"{FillLux:0} lux" : "off")} · " +
                   $"tonemap={tone} · shadowLift={ShadowLift:0.000} · moonLux={MoonMaxLux:0.0} · nightEV={NightEv:0.0}";
        }
    }
}
