using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// One interaction a <see cref="SmartObject"/> offers: which need it feeds, how fast, and for how
    /// long. This is the "advertisement" the utility-AI scores. Authored per object in the inspector.
    /// </summary>
    [System.Serializable]
    public sealed class InteractionAdvertisement
    {
        public string label = "Use";
        public string needId = "hunger";

        [Tooltip("Need points (of 100) restored per GAME hour while using this object.")]
        public float satisfyPerHour = 80f;

        [Tooltip("How long the interaction lasts, in GAME hours.")]
        public float durationHours = 1f;
    }

    /// <summary>
    /// A world object that advertises interactions (the Sims "smart object" pattern). A Sim's brain
    /// scores these advertisements and, when it picks one, walks to <see cref="useAnchor"/> and performs
    /// it. Both the AI (autonomy) and the player (possession) drive Sims through these same interactions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SmartObject : MonoBehaviour
    {
        public string displayName = "Object";

        [Tooltip("Where the Sim stands to use this object. Defaults to the object's own position.")]
        public Transform useAnchor;

        public List<InteractionAdvertisement> advertises = new List<InteractionAdvertisement>();

        [Tooltip("Only one Sim may claim this at a time (bed, toilet, shower). Uncheck for shared objects.")]
        public bool exclusive = true;

        /// <summary>Catalogue item this was built from + its current colour swatch (for recolour in build mode).</summary>
        public BuildableDef Def;
        public int SwatchIndex;

        /// <summary>Quarter-turn index 0..3 of this object's yaw (the grid only allows 90° rotations).</summary>
        public int RotIndex => Mathf.RoundToInt(Mathf.Repeat(transform.eulerAngles.y, 360f) / 90f) & 3;

        private Texture2D _appliedSwatchTex; // last swatch diffuse applied — reference-matched on the next swap
        private readonly List<Material> _ownedMats = new List<Material>(); // instance clones we must destroy

        /// <summary>
        /// Swap to another colour swatch. Real items: replace the item's PRIMARY diffuse texture on the
        /// materials that carry it (glass/frame/extra materials keep theirs) — the home-editor's proven
        /// ApplySwatch technique. Instance clones are created only on renderers that need them and are
        /// destroyed with the object.
        /// </summary>
        public void Recolor(int swatch)
        {
            if (Def == null || Def.swatches == null || Def.swatches.Count == 0) return;
            SwatchIndex = Mathf.Clamp(swatch, 0, Def.swatches.Count - 1);
            var sw = Def.swatches[SwatchIndex];

            if (sw.diffuse == null) { TintFallback(sw.color); return; } // placeholder (cube) items

            bool Matches(Material m)
            {
                if (m == null || !m.HasProperty("_BaseColorMap")) return false;
                var current = m.GetTexture("_BaseColorMap");
                return current != null && (current == Def.primaryTexture || current == _appliedSwatchTex);
            }

            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                var any = false;
                foreach (var shared in r.sharedMaterials)
                    if (Matches(shared)) { any = true; break; }
                if (!any) continue;

                var mats = r.materials; // instance clones — only on renderers with a matching slot
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!_ownedMats.Contains(mats[i])) _ownedMats.Add(mats[i]);
                    if (Matches(mats[i])) mats[i].SetTexture("_BaseColorMap", sw.diffuse);
                }
                r.materials = mats;
            }
            _appliedSwatchTex = sw.diffuse;
        }

        private void TintFallback(Color c)
        {
            var r = GetComponentInChildren<Renderer>();
            if (r == null) return;
            var mat = r.material;
            if (!_ownedMats.Contains(mat)) _ownedMats.Add(mat);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
        }

        private void OnDestroy()
        {
            foreach (var m in _ownedMats) if (m != null) Destroy(m);
            _ownedMats.Clear();
        }

        public Vector3 AnchorPosition => useAnchor != null ? useAnchor.position : transform.position;

        /// <summary>The Sim currently claiming this object (heading to it or using it).</summary>
        public SimBody ReservedBy { get; private set; }

        /// <summary>Free for <paramref name="who"/> to claim — or already theirs, or not exclusive.</summary>
        public bool IsAvailableTo(SimBody who) => !exclusive || ReservedBy == null || ReservedBy == who;

        /// <summary>Claim the object. False when another Sim already holds it.</summary>
        public bool TryReserve(SimBody who)
        {
            if (!exclusive) return true;
            if (ReservedBy != null && ReservedBy != who) return false;
            ReservedBy = who;
            return true;
        }

        public void Release(SimBody who) { if (ReservedBy == who) ReservedBy = null; }
    }
}
