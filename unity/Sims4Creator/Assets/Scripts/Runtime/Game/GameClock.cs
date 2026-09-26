using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// The master game clock, and the single authority on how fast the simulation runs.
    ///
    /// SPEED INVARIANCE — the core simulation principle: a speed setting changes how much GAME time
    /// passes per real second, and <b>everything simulated scales with it equally</b>, so the world
    /// evolves identically at any speed (a Sim always takes the same number of game-minutes to cross a
    /// room; at 3× you just watch it happen faster).
    ///
    /// That is enforced with ONE knob: <c>Time.timeScale</c>. Movement, animation playback and this
    /// clock all read <c>Time.deltaTime</c>, so they stay exactly proportional at every speed — and
    /// foot-matching keeps working, because body speed and clip rate scale together.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameClock : MonoBehaviour
    {
        [Tooltip("0 = paused, then the multipliers below.")]
        [Range(0, 4)] public int speedIndex = 1;

        [Tooltip("Simulation rate per speed setting. EVERYTHING (clock, movement, animation) scales by this. " +
                 "Indices: 0 pause, 1 normal, 2 = 2x, 3 = 3x, 4 = 5x (matches the speed icon art).")]
        public float[] speedMultipliers = { 0f, 1f, 2f, 3f, 5f };

        [Tooltip("Game minutes per real second at multiplier 1.")]
        public float baseGameMinutesPerRealSecond = 2f;

        public int day = 1;
        [Range(0f, 24f)] public float timeOfDay = 8f;

        [Tooltip("Optional: sky / day-night visual driven by this clock.")]
        public SkyTimeController sky;

        /// <summary>Set by modes that pause the world (CAS / Build) without disturbing the chosen speed.</summary>
        public bool ExternalPause { get; set; }

        public float Multiplier =>
            (speedIndex >= 0 && speedIndex < speedMultipliers.Length) ? speedMultipliers[speedIndex] : 0f;

        /// <summary>Game-hours elapsed this frame (0 while paused). The simulation ticks on this.</summary>
        public float DeltaGameHours { get; private set; }

        private void Start()
        {
            if (sky != null) sky.autoAdvance = false; // the clock owns time now
        }

        private void OnDisable()
        {
            Time.timeScale = 1f; // never strand the editor/player at 0
        }

        private void Update()
        {
            Time.timeScale = ExternalPause ? 0f : Multiplier;

            // Time.deltaTime is ALREADY scaled by timeScale, so the clock only needs the base rate —
            // and it automatically advances in step with how fast Sims move and animate.
            float dHours = (baseGameMinutesPerRealSecond * Time.deltaTime) / 60f;
            DeltaGameHours = dHours;
            if (dHours <= 0f) return;

            timeOfDay += dHours;
            while (timeOfDay >= 24f) { timeOfDay -= 24f; day++; }

            if (sky != null) { sky.timeOfDay = timeOfDay; sky.Apply(); }
        }

        public string Label
        {
            get
            {
                int h = Mathf.FloorToInt(timeOfDay);
                int m = Mathf.FloorToInt((timeOfDay - h) * 60f);
                return $"Day {day}    {h:00}:{m:00}    ({(Multiplier <= 0f ? "paused" : Multiplier + "×")})";
            }
        }
    }
}
