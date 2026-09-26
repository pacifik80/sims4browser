using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>One colour variant of a buildable: the baked per-swatch diffuse texture from the game's
    /// own export (Swatches/N/diffuse.png). Recolour = swap this texture on the placed object's primary
    /// materials — no colour math, exactly the game's model.</summary>
    [System.Serializable]
    public sealed class Swatch
    {
        public string name;
        public Texture2D diffuse;   // baked swatch diffuse (also used as the swatch chip image)
        public Color color = Color.gray; // fallback tint for template-less placeholder items
    }

    /// <summary>
    /// A REAL buildable catalogue item, baked at scene-build time from the exported game assets
    /// (Assets/Sims4/home/&lt;folder&gt;): real mesh template, real BuyBuildThumbnail, real baked swatches,
    /// real bounds — plus the sim-side function (advertised interactions). Serialized on
    /// <see cref="GameCatalog"/>, so everything ships in the scene with zero runtime loading.
    /// </summary>
    [System.Serializable]
    public sealed class BuildableDef
    {
        public string id;                    // catalog id (e.g. "bed_double")
        public string name;                  // display label (e.g. "Double Bed")
        public string category;              // FUNCTION category: Seating, Surfaces, Beds, …
        public string room;                  // room tag (Living/Kitchen/Bathroom/Bedroom) — the filter axis
        public int price;
        public Vector3 size;                 // authored bounds size (metres)
        public int footW = 1, footD = 1;     // footprint in lot cells (rotates with the item)
        public bool exclusive = true;        // one user at a time (bed/toilet); false = shared (TV/sofa)
        public List<InteractionAdvertisement> ads = new List<InteractionAdvertisement>();

        public GameObject template;          // inactive baked template in the scene — Instantiate to place
        public Texture2D thumbnail;          // the game's own BuyBuildThumbnail (may be null — chip fallback)
        public Texture2D primaryTexture;     // template's primary diffuse — swatches replace THIS texture
        public List<Swatch> swatches = new List<Swatch>();

        public Texture2D ThumbOrFallback => thumbnail != null ? thumbnail : primaryTexture;
    }

    /// <summary>
    /// The Buy catalogue, wired by the scene builder (GameCatalogBaker) from home_catalog.json + the
    /// exported per-item folders. Runtime code reads <see cref="items"/> and clones templates — it never
    /// touches the filesystem or AssetDatabase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameCatalog : MonoBehaviour
    {
        public static GameCatalog Instance { get; private set; }

        [Header("Wired by the scene builder")]
        public List<BuildableDef> items = new List<BuildableDef>();
        public Material cellFreeMaterial;    // ghost footprint quad — placement valid
        public Material cellBlockedMaterial; // ghost footprint quad — placement blocked

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Preferred function-category order; only categories that actually have items appear.</summary>
        public static readonly string[] CategoryOrder =
            { "Seating", "Surfaces", "Beds", "Appliances", "Plumbing", "Lighting", "Electronics", "Storage", "Decor", "Activities" };

        public List<string> Categories()
        {
            var present = new HashSet<string>();
            foreach (var d in items) if (!string.IsNullOrEmpty(d.category)) present.Add(d.category);
            var list = new List<string>();
            foreach (var c in CategoryOrder) if (present.Remove(c)) list.Add(c);
            list.AddRange(present); // anything unmapped still shows
            return list;
        }

        public List<string> Rooms()
        {
            var present = new HashSet<string>();
            var list = new List<string>();
            foreach (var d in items)
                if (!string.IsNullOrEmpty(d.room) && present.Add(d.room)) list.Add(d.room);
            return list;
        }

        public BuildableDef ById(string id) =>
            items.Find(d => string.Equals(d.id, id, System.StringComparison.OrdinalIgnoreCase));
    }
}
