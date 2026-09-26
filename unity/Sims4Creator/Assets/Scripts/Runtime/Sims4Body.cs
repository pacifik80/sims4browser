using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// A built Sims character: body parts (body/head/eyes/top/bottom/shoes…) sharing ONE skeleton,
    /// each its own <see cref="SkinnedMeshRenderer"/>, grouped by region. Lets clothing logic hide or
    /// show body regions the way the game culls covered body meshes (equip a shirt → hide the
    /// upper-body region). Foundation for the character creator's layering.
    /// </summary>
    [ExecuteAlways]
    public sealed class Sims4Body : MonoBehaviour
    {
        [Serializable]
        public sealed class Part
        {
            public string name;
            public string region;
            public Renderer renderer;
            public bool visible = true;
        }

        public List<Part> parts = new();

        /// <summary>Show/hide every part tagged with the given region (case-insensitive).</summary>
        public void SetRegionVisible(string region, bool visible)
        {
            foreach (var p in parts)
            {
                if (string.Equals(p.region, region, StringComparison.OrdinalIgnoreCase))
                {
                    p.visible = visible;
                    if (p.renderer != null)
                    {
                        p.renderer.enabled = visible;
                    }
                }
            }
        }

        /// <summary>Show/hide a single part by name (case-insensitive).</summary>
        public void SetPartVisible(string name, bool visible)
        {
            foreach (var p in parts)
            {
                if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    p.visible = visible;
                    if (p.renderer != null)
                    {
                        p.renderer.enabled = visible;
                    }
                }
            }
        }

        /// <summary>Re-apply the stored visibility flags to the renderers.</summary>
        public void ApplyVisibility()
        {
            foreach (var p in parts)
            {
                if (p.renderer != null)
                {
                    p.renderer.enabled = p.visible;
                }
            }
        }

        private void OnEnable() => ApplyVisibility();

        private void OnValidate() => ApplyVisibility();
    }
}
