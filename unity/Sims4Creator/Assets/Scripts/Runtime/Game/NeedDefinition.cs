using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Designer-authored definition of a need. The live value lives on the Sim (<see cref="NeedValue"/>);
    /// this asset defines the need's identity, how fast it decays, its starting value, and how it reads
    /// in the debug UI. Authored as a ScriptableObject so the need set is data, not code.
    /// </summary>
    [CreateAssetMenu(fileName = "Need", menuName = "Sims4 Game/Need Definition")]
    public sealed class NeedDefinition : ScriptableObject
    {
        public string id = "hunger";
        public string displayName = "Hunger";

        [Tooltip("Points (of 100) lost per GAME hour.")]
        public float decayPerHour = 8f;

        [Range(0f, 100f)] public float startValue = 80f;

        public Color barColor = new Color(0.85f, 0.55f, 0.25f);
    }
}
