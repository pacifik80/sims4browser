using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Name → vector icon lookup for the game UI. Populated by the editor (Sims4 Creator → Game → Rebuild
    /// Icon Library) from the imported SVGs. <see cref="Get"/> returns the <see cref="placeholder"/> for
    /// any name that isn't present, so missing icons show an obvious placeholder rather than breaking the
    /// UI (per the user's request — real icons get dropped in later, tracked as TASK-005).
    ///
    /// Icons are UI Toolkit <see cref="VectorImage"/> assets (Vector Graphics package) so they stay crisp
    /// at any screen resolution.
    /// </summary>
    [CreateAssetMenu(fileName = "IconLibrary", menuName = "Sims4 Game/Icon Library")]
    public sealed class IconLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string name;
            public VectorImage image;
        }

        public List<Entry> entries = new List<Entry>();
        public VectorImage placeholder;

        private Dictionary<string, VectorImage> _map;

        private void Build()
        {
            _map = new Dictionary<string, VectorImage>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
                if (e != null && !string.IsNullOrEmpty(e.name) && e.image != null)
                    _map[e.name] = e.image;
        }

        public bool Has(string name)
        {
            if (_map == null) Build();
            return !string.IsNullOrEmpty(name) && _map.ContainsKey(name);
        }

        /// <summary>The icon for <paramref name="name"/>, or the placeholder if it isn't in the library.</summary>
        public VectorImage Get(string name)
        {
            if (_map == null) Build();
            if (!string.IsNullOrEmpty(name) && _map.TryGetValue(name, out var v)) return v;
            return placeholder;
        }
    }
}
