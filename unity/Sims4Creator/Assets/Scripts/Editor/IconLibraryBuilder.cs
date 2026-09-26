using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Sims4Creator.Game;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Builds/refreshes the <see cref="IconLibrary"/> asset from the imported vector icons under
    /// <c>Assets/Game/UI/Icons</c>. Each <see cref="VectorImage"/> is registered by its file name (so
    /// "need-hungry.svg" → "need-hungry"); the one named "placeholder" becomes the fallback.
    ///
    /// Run after importing new SVGs. The scene builder also calls <see cref="Build"/> so a freshly built
    /// scene always has a current library. If the icons haven't imported as VectorImage yet (the Vector
    /// Graphics package must be present AND the SVG import mode set to a UI-Toolkit vector image), the
    /// library ends up empty and every icon falls back to the placeholder — nothing breaks.
    /// </summary>
    public static class IconLibraryBuilder
    {
        private const string IconsFolder = "Assets/Game/UI/Icons";
        private const string LibraryPath = "Assets/Game/UI/IconLibrary.asset";

        [MenuItem("Sims4 Creator/Game/Rebuild Icon Library", priority = 60)]
        public static void RebuildMenu()
        {
            var lib = Build();
            int n = lib != null ? lib.entries.Count : 0;
            Debug.Log($"[Sims4 Game] Icon library rebuilt: {n} icon(s) at {LibraryPath}. " +
                      (n == 0 ? "No VectorImage assets found under " + IconsFolder + " — Unity 6 imports SVG " +
                                "natively (built-in VectorGraphicsModule); if the icons imported as something " +
                                "else, or use currentColor (unsupported), they won't appear. Icons show the placeholder."
                              : "Missing names fall back to the placeholder."));
        }

        /// <summary>Create/refresh and return the icon library asset.</summary>
        public static IconLibrary Build()
        {
            var lib = AssetDatabase.LoadAssetAtPath<IconLibrary>(LibraryPath);
            if (lib == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Game")) AssetDatabase.CreateFolder("Assets", "Game");
                if (!AssetDatabase.IsValidFolder("Assets/Game/UI")) AssetDatabase.CreateFolder("Assets/Game", "UI");
                lib = ScriptableObject.CreateInstance<IconLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            lib.entries.Clear();
            lib.placeholder = null;

            foreach (var guid in AssetDatabase.FindAssets("t:VectorImage", new[] { IconsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var vi = AssetDatabase.LoadAssetAtPath<VectorImage>(path);
                if (vi == null) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, "placeholder", System.StringComparison.OrdinalIgnoreCase))
                    lib.placeholder = vi;
                else
                    lib.entries.Add(new IconLibrary.Entry { name = name, image = vi });
            }

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }
    }
}
