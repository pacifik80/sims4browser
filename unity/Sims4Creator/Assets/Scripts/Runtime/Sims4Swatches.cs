using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// Runtime swatch (color/state) switcher for an imported Sims 4 asset.
    ///
    /// Each entry in <see cref="swatchMaterials"/> is a fully-built HDRP/Lit material for one
    /// swatch (one diffuse/normal set). Selecting a swatch assigns that single material to EVERY
    /// renderer slot under this GameObject, mirroring how the importer force-assigns one material.
    ///
    /// The component runs in <see cref="ExecuteAlways"/> so the Inspector dropdown switches live in
    /// edit mode, and <see cref="SetSwatch"/> / <see cref="NextSwatch"/> drive it at runtime.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Sims4 Creator/Sims4 Swatches")]
    public sealed class Sims4Swatches : MonoBehaviour
    {
        [Tooltip("One material per swatch, indexed by swatch index (0-based).")]
        public Material[] swatchMaterials;

        [Tooltip("Human-readable label per swatch, parallel to swatchMaterials.")]
        public string[] swatchLabels;

        [SerializeField]
        private int selectedIndex;

        /// <summary>Number of available swatches.</summary>
        public int Count => swatchMaterials?.Length ?? 0;

        /// <summary>
        /// Currently selected swatch index. Setting it clamps to [0, Count-1] and re-applies the
        /// material to all child renderers.
        /// </summary>
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                selectedIndex = Clamp(value);
                ApplyCurrent();
            }
        }

        /// <summary>Select a specific swatch (clamped). Use this at runtime.</summary>
        public void SetSwatch(int index)
        {
            SelectedIndex = index;
        }

        /// <summary>Advance to the next swatch, wrapping back to 0. Use this at runtime.</summary>
        public void NextSwatch()
        {
            var count = Count;
            if (count <= 0)
            {
                return;
            }

            SelectedIndex = (selectedIndex + 1) % count;
        }

        private void OnEnable()
        {
            ApplyCurrent();
        }

        private void OnValidate()
        {
            selectedIndex = Clamp(selectedIndex);
            ApplyCurrent();
        }

        /// <summary>
        /// Assign the selected swatch material to every material slot of every Renderer in the
        /// hierarchy (including inactive ones).
        /// </summary>
        public void ApplyCurrent()
        {
            var count = Count;
            if (count <= 0)
            {
                return;
            }

            var index = Clamp(selectedIndex);
            var mat = swatchMaterials[index];
            if (mat == null)
            {
                return;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var slots = Mathf.Max(1, renderer.sharedMaterials.Length);
                var replacement = new Material[slots];
                for (var i = 0; i < slots; i++)
                {
                    replacement[i] = mat;
                }

                renderer.sharedMaterials = replacement;
            }
        }

        private int Clamp(int index)
        {
            var count = Count;
            if (count <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(index, 0, count - 1);
        }
    }
}
