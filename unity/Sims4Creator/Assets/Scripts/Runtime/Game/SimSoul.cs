using System.Collections.Generic;

namespace Sims4Creator.Game
{
    /// <summary>
    /// One need's live value on a Sim, 0..100 (0 = desperate, 100 = fully satisfied). Plain data.
    /// </summary>
    [System.Serializable]
    public sealed class NeedValue
    {
        public string id;
        public float value;

        public NeedValue() { }
        public NeedValue(string id, float value) { this.id = id; this.value = value; }
    }

    /// <summary>
    /// The persistent, engine-independent state of a Sim — its "soul". This is the source of truth
    /// for who a Sim is and how it's doing; it is deliberately separate from the presentation body
    /// (<see cref="SimBody"/>) so off-lot Sims can be simulated without a GameObject, so control can
    /// be swapped (autonomy vs possession) on the same entity, and so the world can be saved as data.
    /// No UnityEngine dependency — this stays plain, serializable C#.
    /// </summary>
    [System.Serializable]
    public sealed class SimSoul
    {
        public string simId;
        public string displayName;
        /// <summary>Exported character folder this Sim looks like, e.g. "am_char".</summary>
        public string appearanceSlug;
        public List<NeedValue> needs = new List<NeedValue>();
        public string mood = "Neutral";
        /// <summary>Current action id, or null when idle. Used from M1 (autonomy).</summary>
        public string currentAction;

        public NeedValue GetNeed(string id)
        {
            for (int i = 0; i < needs.Count; i++)
                if (needs[i].id == id) return needs[i];
            return null;
        }
    }
}
