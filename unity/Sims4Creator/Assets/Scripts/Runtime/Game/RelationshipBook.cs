using System.Collections.Generic;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Pairwise relationship scores between Sims, −100..+100. Keyed by an order-independent pair of
    /// sim ids, so Get(a,b) == Get(b,a). Plain C#, no UnityEngine dependency — part of the simulation,
    /// not the presentation, and serializable with the world later.
    /// </summary>
    public sealed class RelationshipBook
    {
        private readonly Dictionary<string, float> _rel = new Dictionary<string, float>();

        private static string Key(string a, string b)
            => string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

        public float Get(string a, string b)
            => _rel.TryGetValue(Key(a, b), out var v) ? v : 0f;

        public void Add(string a, string b, float delta)
        {
            if (a == b) return;
            var k = Key(a, b);
            float v = (_rel.TryGetValue(k, out var cur) ? cur : 0f) + delta;
            _rel[k] = v < -100f ? -100f : (v > 100f ? 100f : v);
        }
    }
}
