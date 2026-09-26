using System.Collections.Generic;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Pure need-decay logic. No UnityEngine dependency, so it is unit-testable in isolation and can
    /// run for off-lot Sims that have no body. M0 uses only decay; M1 will add satisfaction from
    /// completed interactions.
    /// </summary>
    public static class NeedsSystem
    {
        /// <summary>Advance one soul's needs by elapsed GAME hours using per-need decay rates.</summary>
        public static void Decay(SimSoul soul, IReadOnlyDictionary<string, float> decayPerHour, float gameHours)
        {
            if (soul == null || decayPerHour == null || gameHours <= 0f) return;
            for (int i = 0; i < soul.needs.Count; i++)
            {
                var n = soul.needs[i];
                if (decayPerHour.TryGetValue(n.id, out float rate))
                    n.value = Clamp01to100(n.value - rate * gameHours);
            }
        }

        private static float Clamp01to100(float v) => v < 0f ? 0f : (v > 100f ? 100f : v);
    }
}
