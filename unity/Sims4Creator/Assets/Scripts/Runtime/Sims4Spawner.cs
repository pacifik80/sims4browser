using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// Spawns a designed character from a saved <see cref="CharacterTemplate"/>. It clones an existing built
    /// <see cref="Sims4Character"/> (the "prefab" — all its meshes + catalog) and applies the template's
    /// selections. This is the entry point a future game calls to place saved Sims into a scene.
    ///
    /// v1 gives each spawn its own SKIN material instance so its skin/tone/detail is independent of the
    /// source and of other spawns. NOTE: hair + clothing still share their per-style materials, so multiple
    /// spawns wearing the SAME garment/hair in DIFFERENT colours would fight over that shared texture — full
    /// multi-distinct-spawn independence needs per-instance cloning of every style material, which we'll add
    /// when spawning is actually wired into gameplay (the user deferred that). Single spawns, or spawns that
    /// share hair/clothing colours, are correct today.
    /// </summary>
    public static class Sims4Spawner
    {
        /// <summary>Clone <paramref name="source"/> and apply <paramref name="template"/>. Returns the new Sims4Character.</summary>
        public static Sims4Character Spawn(Sims4Character source, CharacterTemplate template,
                                           Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (source == null) { Debug.LogError("[Sims4Spawner] source character is null."); return null; }

            var clone = Object.Instantiate(source.gameObject, position, rotation, parent);
            clone.name = (template != null && !string.IsNullOrEmpty(template.name) ? template.name : source.name) + " (spawn)";
            var ch = clone.GetComponent<Sims4Character>();
            if (ch == null) return null;

            // Give this spawn its OWN skin material so its skin is independent of the source + other spawns.
            if (ch.skinMaterial != null)
            {
                var orig = ch.skinMaterial;
                var inst = new Material(orig) { name = orig.name + " (spawn)" };
                foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    var changed = false;
                    for (var i = 0; i < mats.Length; i++)
                        if (mats[i] == orig) { mats[i] = inst; changed = true; }
                    if (changed) r.sharedMaterials = mats;
                }
                ch.skinMaterial = inst;
            }

            if (template != null) ch.ApplyTemplate(template);
            return ch;
        }

        /// <summary>Load a template JSON by path and spawn it.</summary>
        public static Sims4Character SpawnFromFile(Sims4Character source, string templatePath,
                                                   Vector3 position, Quaternion rotation, Transform parent = null)
            => Spawn(source, CharacterTemplateStore.Load(templatePath), position, rotation, parent);

        /// <summary>Load a template by its saved NAME (as shown in the CAS templates list) and spawn it.</summary>
        public static Sims4Character SpawnByName(Sims4Character source, string templateName,
                                                 Vector3 position, Quaternion rotation, Transform parent = null)
        {
            foreach (var p in CharacterTemplateStore.List())
                if (CharacterTemplateStore.NameOf(p) == templateName)
                    return SpawnFromFile(source, p, position, rotation, parent);
            Debug.LogWarning($"[Sims4Spawner] no template named '{templateName}'.");
            return null;
        }
    }
}
