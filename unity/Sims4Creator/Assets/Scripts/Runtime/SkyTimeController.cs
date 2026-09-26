using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Sims4Creator
{
    /// <summary>
    /// Drives a realistic sky: the sun (and an anti-solar moon) travel across the sky per the chosen
    /// city, date and clock time, using real solar-position astronomy. The sun's intensity and colour
    /// track its elevation (bright white at noon → warm at the horizon → off at night, moon takes over),
    /// so lighting corresponds to time and season. North can be re-aimed. [ExecuteAlways] so it also
    /// previews live in the editor.
    /// </summary>
    [ExecuteAlways]
    public sealed class SkyTimeController : MonoBehaviour
    {
        public struct City
        {
            public string name;
            public double lat, lon, utc;
            public City(string n, double la, double lo, double u) { name = n; lat = la; lon = lo; utc = u; }
        }

        // Diverse spread: high-latitude (Reykjavik), equatorial (Nairobi), southern hemisphere (Sydney/Rio).
        public static readonly City[] Cities =
        {
            new City("London", 51.51, -0.13, 0),
            new City("New York", 40.71, -74.01, -5),
            new City("Reykjavik", 64.15, -21.94, 0),
            new City("Moscow", 55.75, 37.62, 3),
            new City("Cairo", 30.04, 31.24, 2),
            new City("Tokyo", 35.68, 139.69, 9),
            new City("Sydney", -33.87, 151.21, 10),
            new City("Rio de Janeiro", -22.91, -43.17, -3),
            new City("Nairobi", -1.29, 36.82, 3),
        };

        [Header("Lights (directional)")]
        public Light sun;
        public Light moon;

        [Tooltip("Fixed-exposure component driven per time of day (day bright, night dark).")]
        public Exposure exposure;

        [Tooltip("EV offset added to the computed fixed exposure — live-tunable from the debug menu's render options.")]
        public float exposureOffset = 0f;

        [Tooltip("Moonlight at full night, in lux. Real full moon is ~0.25 (invisible); this is a gameplay " +
                 "exaggeration — beware: the PBR sky also scatters it, so high values make night look like day.")]
        public float moonMaxLux = 25f;   // defaults preserve the home-editor look; the game scene tunes down

        [Tooltip("Fixed-exposure EV at full night (day is 13). LOWER = brighter night.")]
        public float nightEv = 3f;

        [Header("Location & time")]
        public int cityIndex = 0;
        [Range(1, 12)] public int month = 6;
        [Range(1, 31)] public int day = 21;
        [Range(0f, 24f)] public float timeOfDay = 12f;
        [Tooltip("Degrees to rotate world-north (so the scene's north can be re-aimed).")]
        [Range(-180f, 180f)] public float northOffset = 0f;

        [Header("Clock")]
        public bool autoAdvance = false;
        [Tooltip("In-world minutes advanced per real second while playing.")]
        public float minutesPerSecond = 60f;

        [Header("UI")]
        public bool showPanel = true;
        [Tooltip("Pixels from the screen's RIGHT edge to right-align the Sky/Time panel (so it clears the " +
                 "view / side panels, e.g. the CAS editor). Negative = the default top-left placement.")]
        public float panelRightMargin = -1f;

        // Last computed sun position, for the readout.
        public float lastElevation, lastAzimuth;

        HDAdditionalLightData _sunHd, _moonHd;

        // Optional: the CAS editor's panel. When its STYLE panel is up, keep the Sky/Time panel clear of it
        // (else it hides underneath). Auto-found once so no wiring/rebuild is needed. Null in other scenes.
        CasController _cas;
        bool _casSearched;

        void OnEnable()
        {
            CacheHd();
            Apply();
        }

        void CacheHd()
        {
            if (sun != null && _sunHd == null) { _sunHd = sun.GetComponent<HDAdditionalLightData>(); }
            if (moon != null && _moonHd == null) { _moonHd = moon.GetComponent<HDAdditionalLightData>(); }
        }

        void Update()
        {
            if (autoAdvance && Application.isPlaying)
            {
                timeOfDay += Time.deltaTime * (minutesPerSecond / 60f);
                if (timeOfDay >= 24f) { timeOfDay -= 24f; }
            }
            Apply();
        }

        void OnValidate() => Apply();

        public void Apply()
        {
            CacheHd();
            if (sun == null)
            {
                return;
            }
            var city = Cities[Mathf.Clamp(cityIndex, 0, Cities.Length - 1)];
            int doy = SolarPosition.DayOfYear(month, day);
            SolarPosition.Compute(city.lat, city.lon, city.utc, doy, timeOfDay, out double elev, out double az);
            lastElevation = (float)elev;
            lastAzimuth = (float)az;

            // ---- Sun: aim, then intensity + colour by elevation (atmospheric extinction) ----
            var toSun = SolarPosition.DirectionToSun(elev, az + northOffset);
            sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
            float highness = Mathf.Clamp01((float)elev / 12f);                 // 0 at horizon → 1 by 12°
            float sunLux = Mathf.Clamp01(((float)elev + 1.5f) / 10f) * 105000f; // fades out just below the horizon
            sun.color = Color.Lerp(new Color(1f, 0.45f, 0.22f), new Color(1f, 0.96f, 0.9f), highness);
            sun.enabled = elev > -3.0;
            if (_sunHd != null)
            {
                _sunHd.interactsWithSky = true;
                sun.lightUnit = LightUnit.Lux; // SetIntensity(unit) is obsolete since 2023.3
                sun.intensity = sunLux;
            }

            // ---- Moon: anti-solar (simple full-moon model), fades in as the sun sets ----
            float nightAmt = Mathf.Clamp01((float)(-elev) / 6f); // 0 at horizon → 1 by -6°
            if (moon != null)
            {
                var toMoon = -toSun;
                moon.transform.rotation = Quaternion.LookRotation(-toMoon, Vector3.up);
                moon.color = new Color(0.55f, 0.66f, 0.95f);
                moon.enabled = nightAmt > 0.01f;
                if (_moonHd != null)
                {
                    _moonHd.interactsWithSky = true;
                    // Real moonlight is ~0.25 lux (invisible); exaggerate so night reads as moonlit.
                    moon.lightUnit = LightUnit.Lux;
                    moon.intensity = nightAmt * moonMaxLux;
                }
            }

            // ---- Exposure: fixed EV by time of day (auto-exposure would normalise night to look like
            // day). Bright day → high EV, moonlit night → low EV. Smoothly crosses at twilight. ----
            if (exposure != null)
            {
                float dayness = Mathf.Clamp01(((float)elev + 6f) / 16f); // 0 at -6°, 1 by +10°
                exposure.fixedExposure.value = Mathf.Lerp(nightEv, 13f, dayness) + exposureOffset;
            }
        }

        void OnGUI()
        {
            if (!showPanel)
            {
                return;
            }
            // Top-LEFT: the Home Editor's asset panel owns the entire right edge, so keep clear of it.
            const float w = 250f;
            if (_cas == null && !_casSearched) { _cas = FindFirstObjectByType<CasController>(); _casSearched = true; }
            // Available right edge: full screen, minus the CAS STYLE panel when it is showing.
            float availRight = Screen.width;
            if (_cas != null && _cas.visible) availRight -= _cas.rightPanelWidth;
            float px = panelRightMargin >= 0f ? Mathf.Max(12f, availRight - w - panelRightMargin) : 12f;
            var r = new Rect(px, 12f, w, 232f);
            GUILayout.BeginArea(r, GUI.skin.box);
            GUILayout.Label("<b>Sky / Time</b>", Rich());

            var city = Cities[Mathf.Clamp(cityIndex, 0, Cities.Length - 1)];
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(28f))) { cityIndex = (cityIndex - 1 + Cities.Length) % Cities.Length; }
            GUILayout.Label($"{city.name}", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("▶", GUILayout.Width(28f))) { cityIndex = (cityIndex + 1) % Cities.Length; }
            GUILayout.EndHorizontal();

            int h = Mathf.FloorToInt(timeOfDay);
            int m = Mathf.FloorToInt((timeOfDay - h) * 60f);
            GUILayout.Label($"Time  {h:00}:{m:00}");
            timeOfDay = GUILayout.HorizontalSlider(timeOfDay, 0f, 24f);

            GUILayout.Label($"Date  {month:00}/{day:00}");
            month = Mathf.RoundToInt(GUILayout.HorizontalSlider(month, 1f, 12f));
            day = Mathf.RoundToInt(GUILayout.HorizontalSlider(day, 1f, 28f));

            GUILayout.Label($"North offset  {northOffset:0}°");
            northOffset = GUILayout.HorizontalSlider(northOffset, -180f, 180f);

            autoAdvance = GUILayout.Toggle(autoAdvance, "Auto-advance clock");
            GUILayout.Label($"Sun elev {lastElevation:0.0}°  az {lastAzimuth:0}°", Rich());
            GUILayout.EndArea();
        }

        static GUIStyle _rich;
        static GUIStyle Rich()
        {
            if (_rich == null)
            {
                _rich = new GUIStyle(GUI.skin.label) { richText = true };
            }
            return _rich;
        }
    }
}
