using UnityEditor;
using UnityEngine;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Inspector for <see cref="Sims4Creator.Sims4Character"/>: a base-tone dropdown, an eye-color
    /// dropdown, and one toggle per detail layer. Because the component is [ExecuteAlways], each change
    /// recomposes the skin RenderTexture live in edit mode. All edits record Undo and mark the target
    /// dirty so the selection persists.
    /// </summary>
    [CustomEditor(typeof(Sims4Creator.Sims4Character))]
    public sealed class Sims4CharacterEditor : UnityEditor.Editor
    {
        private bool _showAdvanced;

        public override void OnInspectorGUI()
        {
            var character = (Sims4Creator.Sims4Character)target;

            // ---- Base skin (full color albedo, raw) ----
            if (character.baseSkins != null && character.baseSkins.Count > 0)
            {
                var labels = new string[character.baseSkins.Count];
                for (var i = 0; i < labels.Length; i++)
                {
                    var s = character.baseSkins[i];
                    labels[i] = Pretty(i, s?.label, s?.id);
                }
                var current = Mathf.Clamp(character.BaseSkinIndex, 0, labels.Length - 1);
                EditorGUILayout.LabelField("Base Skin", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var picked = EditorGUILayout.Popup("Skin", current, labels);
                if (EditorGUI.EndChangeCheck() && picked != current)
                {
                    Undo.RecordObject(character, "Change Base Skin");
                    character.SetBaseSkin(picked);
                    MarkDirty(character);
                }
                EditorGUILayout.Space();
            }

            // ---- Skin detail (real EA normal maps) ----
            if (character.skinNormals != null && character.skinNormals.Count > 0)
            {
                var labels = new string[character.skinNormals.Count];
                for (var i = 0; i < labels.Length; i++)
                {
                    var s = character.skinNormals[i];
                    labels[i] = Pretty(i, s?.label, s?.id);
                }
                var current = Mathf.Clamp(character.SkinNormalIndex, 0, labels.Length - 1);
                EditorGUI.BeginChangeCheck();
                var picked = EditorGUILayout.Popup("Skin Detail (Normal)", current, labels);
                if (EditorGUI.EndChangeCheck() && picked != current)
                {
                    Undo.RecordObject(character, "Change Skin Normal");
                    character.SetSkinNormal(picked);
                    MarkDirty(character);
                }
                EditorGUILayout.Space();
            }

            // ---- Body mesh: three INDEPENDENT region slots ----
            var anyRegion = (character.topVariants != null && character.topVariants.Count > 0)
                || (character.bottomVariants != null && character.bottomVariants.Count > 0)
                || (character.feetVariants != null && character.feetVariants.Count > 0);
            if (anyRegion)
            {
                EditorGUILayout.LabelField("Body Mesh", EditorStyles.boldLabel);
                DrawRegionPopup(character, "Top (torso)", character.topVariants, character.TopIndex, character.SetTopMesh);
                DrawRegionPopup(character, "Bottom (legs)", character.bottomVariants, character.BottomIndex, character.SetBottomMesh);
                DrawRegionPopup(character, "Feet", character.feetVariants, character.FeetIndex, character.SetFeetMesh);
                EditorGUILayout.Space();
            }

            // (Base Tone / shade is deferred "toning" — it was the duplicate of Base Skin and is no
            // longer a separate control; the Base Skin dropdown above is the single color base.)

            // ---- Eye color ----
            if (character.eyeColors != null && character.eyeColors.Count > 0)
            {
                var labels = EyeColorLabels(character);
                var current = Mathf.Clamp(character.EyeColorIndex, 0, labels.Length - 1);
                EditorGUI.BeginChangeCheck();
                var picked = EditorGUILayout.Popup("Eye Color", current, labels);
                if (EditorGUI.EndChangeCheck() && picked != current)
                {
                    Undo.RecordObject(character, "Change Eye Color");
                    character.SetEyeColor(picked);
                    MarkDirty(character);
                }
            }

            // ---- Detail layers ----
            if (character.detailLayers != null && character.detailLayers.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Detail Layers", EditorStyles.boldLabel);
                for (var i = 0; i < character.detailLayers.Count; i++)
                {
                    var d = character.detailLayers[i];
                    if (d == null)
                    {
                        continue;
                    }

                    var label = string.IsNullOrEmpty(d.label) ? d.id : d.label;
                    var v = EditorGUILayout.ToggleLeft(string.IsNullOrEmpty(label) ? $"Detail {i}" : label, d.active);
                    if (v != d.active)
                    {
                        Undo.RecordObject(character, "Toggle Detail Layer");
                        character.ToggleDetailAt(i, v);
                        MarkDirty(character);
                    }
                }
            }

            // ---- Morph sliders (grouped, paired, head→toes collapsible tree) ----
            if (character.AxisCount > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Morphs ({character.AxisCount} sliders)", EditorStyles.boldLabel);

                DrawMorphTree(character);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Force Recompose"))
            {
                character.Recompose();
            }

            EditorGUILayout.Space();
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Catalog / Target (advanced)");
            if (_showAdvanced)
            {
                DrawDefaultInspector();
            }
        }


        private static string[] EyeColorLabels(Sims4Creator.Sims4Character c)
        {
            var labels = new string[c.eyeColors.Count];
            for (var i = 0; i < labels.Length; i++)
            {
                var e = c.eyeColors[i];
                labels[i] = Pretty(i, e?.label, e?.id);
            }

            return labels;
        }

        // Draw one region slot's dropdown (no-op if the slot has no options).
        private void DrawRegionPopup(Sims4Creator.Sims4Character character, string label,
            System.Collections.Generic.List<Sims4Creator.Sims4Character.MeshVariant> list,
            int currentIndex, System.Action<int> setter)
        {
            if (list == null || list.Count == 0)
            {
                return;
            }

            var labels = VariantLabels(list);
            var current = Mathf.Clamp(currentIndex, 0, labels.Length - 1);
            EditorGUI.BeginChangeCheck();
            var picked = EditorGUILayout.Popup(label, current, labels);
            if (EditorGUI.EndChangeCheck() && picked != current)
            {
                Undo.RecordObject(character, $"Change {label}");
                setter(picked);
                MarkDirty(character);
            }
        }

        private static string[] VariantLabels(System.Collections.Generic.List<Sims4Creator.Sims4Character.MeshVariant> list)
        {
            var labels = new string[list.Count];
            for (var i = 0; i < labels.Length; i++)
            {
                var v = list[i];
                labels[i] = Pretty(i, v?.label, v?.id);
            }

            return labels;
        }

        private static string Pretty(int i, string label, string id)
        {
            if (!string.IsNullOrEmpty(label))
            {
                return label;
            }

            return string.IsNullOrEmpty(id) ? $"#{i}" : id;
        }

        // Foldout state for the morph tree (per area / per area-category), persisted across repaints.
        private static readonly System.Collections.Generic.Dictionary<string, bool> _areaOpen = new();
        private static readonly System.Collections.Generic.Dictionary<string, bool> _catOpen = new();

        private static bool Fold(System.Collections.Generic.Dictionary<string, bool> d, string key, bool def)
            => d.TryGetValue(key, out var v) ? v : def;

        // Draw the morph axes as a collapsible Area ▸ Category ▸ slider tree (axes are pre-sorted head→toes).
        private void DrawMorphTree(Sims4Creator.Sims4Character character)
        {
            string curArea = null, curCat = null;
            bool areaOpen = false, catOpen = false;
            for (var i = 0; i < character.morphAxes.Count; i++)
            {
                var ax = character.morphAxes[i];
                if (ax == null) continue;

                if (!string.Equals(ax.area, curArea, System.StringComparison.Ordinal))
                {
                    curArea = ax.area; curCat = null;
                    areaOpen = Fold(_areaOpen, curArea, false);
                    var na = EditorGUILayout.Foldout(areaOpen, curArea, true, EditorStyles.foldoutHeader);
                    if (na != areaOpen) { _areaOpen[curArea] = na; areaOpen = na; }
                }
                if (!areaOpen) continue;

                if (!string.Equals(ax.category, curCat, System.StringComparison.Ordinal))
                {
                    curCat = ax.category;
                    EditorGUI.indentLevel++;
                    var key = curArea + "/" + curCat;
                    catOpen = Fold(_catOpen, key, true);
                    var nc = EditorGUILayout.Foldout(catOpen, curCat, true);
                    if (nc != catOpen) { _catOpen[key] = nc; catOpen = nc; }
                    EditorGUI.indentLevel--;
                }
                if (!catOpen) continue;

                EditorGUI.indentLevel += 2;
                var cur = ax.value;
                EditorGUI.BeginChangeCheck();
                var v = ax.Bidirectional
                    ? EditorGUILayout.Slider(ax.label, cur, -1f, 1f)
                    : EditorGUILayout.Slider(ax.label, cur, 0f, 1f);
                if (EditorGUI.EndChangeCheck() && !Mathf.Approximately(v, cur))
                {
                    Undo.RecordObject(character, "Set Morph");
                    character.SetAxis(i, v);
                    MarkDirty(character);
                    SceneView.RepaintAll();
                }
                EditorGUI.indentLevel -= 2;
            }
        }

        private static void MarkDirty(Object o)
        {
            EditorUtility.SetDirty(o);
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
        }
    }
}
