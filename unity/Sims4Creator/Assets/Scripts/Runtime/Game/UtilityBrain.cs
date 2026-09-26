using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// The autonomy decision-maker. Given a Sim's soul and the smart objects it can reach, it scores
    /// every advertised interaction and returns the best one — the classic Sims "advertisement" model:
    /// a Sim is drawn most strongly to whatever most relieves its biggest need, nearby. The SAME picker
    /// is what the player implicitly uses when possessing (they just choose the interaction manually).
    /// </summary>
    public static class UtilityBrain
    {
        public struct Choice
        {
            public SmartObject obj;
            public InteractionAdvertisement ad;
            public float score;
        }

        /// <summary>
        /// Pick the best interaction for <paramref name="soul"/>. Returns false if nothing is worth
        /// doing (no advertised need is below the <paramref name="minDeficit"/> threshold).
        /// </summary>
        public static bool Pick(SimSoul soul, IReadOnlyList<SmartObject> objects, Vector3 fromPos,
                                float minDeficit, SimBody self, out Choice best)
        {
            best = default;
            float bestScore = float.NegativeInfinity;
            bool found = false;
            if (soul == null || objects == null) return false;

            for (int o = 0; o < objects.Count; o++)
            {
                var obj = objects[o];
                if (obj == null) continue;
                if (!obj.IsAvailableTo(self)) continue; // another Sim has claimed it
                float dist = Vector3.Distance(fromPos, obj.AnchorPosition);
                for (int a = 0; a < obj.advertises.Count; a++)
                {
                    var ad = obj.advertises[a];
                    var need = soul.GetNeed(ad.needId);
                    if (need == null) continue;
                    float deficit = 100f - need.value;
                    if (deficit < minDeficit) continue;

                    // Big deficits dominate; effective relief helps; distance mildly discourages.
                    float score = deficit + ad.satisfyPerHour * 0.1f - dist * 2f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = new Choice { obj = obj, ad = ad, score = score };
                        found = true;
                    }
                }
            }
            return found;
        }
    }
}
