using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// A saved character "template" — the FULL look as designed, stored by catalog IDs (not indices) so it
    /// survives catalog changes and is portable for spawning later. Captured from / applied to a
    /// <see cref="Sims4Character"/> via its CaptureTemplate / ApplyTemplate methods. Serialized as JSON
    /// (Unity JsonUtility-friendly: only [Serializable] classes, lists and primitives — no dictionaries).
    /// </summary>
    [Serializable]
    public sealed class CharacterTemplate
    {
        [Serializable] public sealed class MorphVal { public string name; public float weight; }
        [Serializable] public sealed class SlotVal { public string slot; public string id; public string color; }

        public string name = "Character";
        public string age;     // optional metadata (for later per-age/gender filtering)
        public string gender;

        // Face/body shape
        public List<MorphVal> morphs = new();

        // Skin
        public string baseTone, eyeColor, baseSkin, skinNormal;
        public float skinNormalScale = 0.6f;
        public float skinSmoothness = 0.28f;
        public List<string> detailLayers = new(); // active overlay ids

        // Body meshes (three region slots)
        public string topMesh, bottomMesh, feetMesh;

        // Hair
        public string hairStyle, hairColor;

        // Clothing + makeup — one entry per WORN slot (slot name + item id + colour id)
        public List<SlotVal> clothing = new();
    }

    /// <summary>
    /// Reads/writes <see cref="CharacterTemplate"/> JSON files under
    /// <c>Application.persistentDataPath/CharacterTemplates</c> — writable in the editor AND in a build,
    /// which is where saved Sims live so a future game can enumerate + spawn them.
    /// </summary>
    public static class CharacterTemplateStore
    {
        public static string Dir => Path.Combine(Application.persistentDataPath, "CharacterTemplates");

        public static string Save(CharacterTemplate t)
        {
            Directory.CreateDirectory(Dir);
            var safe = MakeSafe(string.IsNullOrEmpty(t.name) ? "Character" : t.name);
            var path = Path.Combine(Dir, safe + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(t, true));
            return path;
        }

        public static CharacterTemplate Load(string path)
        {
            try { return File.Exists(path) ? JsonUtility.FromJson<CharacterTemplate>(File.ReadAllText(path)) : null; }
            catch (Exception e) { Debug.LogWarning($"[CharacterTemplate] load failed '{path}': {e.Message}"); return null; }
        }

        /// <summary>All saved template file paths (sorted).</summary>
        public static List<string> List()
        {
            if (!Directory.Exists(Dir)) return new List<string>();
            var files = Directory.GetFiles(Dir, "*.json");
            Array.Sort(files);
            return new List<string>(files);
        }

        public static void Delete(string path) { if (File.Exists(path)) File.Delete(path); }

        public static string NameOf(string path) => Path.GetFileNameWithoutExtension(path);

        private static string MakeSafe(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            s = s.Trim();
            return string.IsNullOrEmpty(s) ? "Character" : s;
        }
    }
}
