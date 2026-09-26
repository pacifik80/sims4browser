using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Spawns a placed buildable: clones the REAL baked furniture template (mesh + materials + collider,
    /// authored ground-level pivot) and attaches the sim contract (<see cref="SmartObject"/> + use
    /// anchor). Runs at runtime (build mode) and edit time (scene builder) — the starter lot and
    /// player-placed objects are identical. A labelled cube stands in only when a def has no template.
    /// </summary>
    public static class SmartObjectFactory
    {
        public static SmartObject Create(BuildableDef def, Vector3 groundPos, int swatchIndex = 0, float yaw = 0f)
        {
            if (def == null) return null;

            GameObject go;
            if (def.template != null)
            {
                go = Object.Instantiate(def.template);
                go.SetActive(true); // templates live under an inactive catalog root
                go.name = def.id;
                go.transform.SetPositionAndRotation(groundPos, Quaternion.Euler(0f, yaw, 0f));
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = def.id;
                go.transform.SetPositionAndRotation(groundPos + Vector3.up * (def.size.y * 0.5f),
                                                    Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = def.size;
            }

            var so = go.AddComponent<SmartObject>();
            so.displayName = def.name;
            so.advertises = new List<InteractionAdvertisement>(def.ads);
            so.exclusive = def.exclusive;
            so.Def = def;
            if (swatchIndex > 0) so.Recolor(swatchIndex); else so.SwatchIndex = 0;

            // APPROACH SLOT (D-106): the Sim stands in the centre of the first TILE in front of the
            // footprint (authored front = local -Z, rotates with the item). With a snapped position this
            // lands exactly on a tile centre — the Sim occupies that full cell while using the object.
            float tile = LotGrid.Instance != null ? LotGrid.Instance.tileSize : 1f;
            var anchor = new GameObject("Anchor").transform;
            anchor.SetParent(go.transform, worldPositionStays: false);
            float front = Mathf.Max(1, def.footD) * 0.5f * tile + tile * 0.5f;
            anchor.position = groundPos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -front);
            so.useAnchor = anchor;

            return so;
        }
    }
}
