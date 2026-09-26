using UnityEditor;
using UnityEngine;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Inspector for <see cref="Sims4Creator.Sims4Swatches"/>. Adds a labelled dropdown bound to
    /// <see cref="Sims4Creator.Sims4Swatches.SelectedIndex"/> so the artist can flip swatches live
    /// in edit mode (the component is [ExecuteAlways], so the change shows immediately in the scene).
    /// The material/label arrays are still drawn below via the default inspector for inspection.
    /// </summary>
    [CustomEditor(typeof(Sims4Creator.Sims4Swatches))]
    public sealed class Sims4SwatchesEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var swatches = (Sims4Creator.Sims4Swatches)target;
            var count = swatches.Count;

            if (count <= 0)
            {
                EditorGUILayout.HelpBox(
                    "No swatch materials assigned. Re-import the asset or use " +
                    "'Sims4 Creator ▸ Add or Refresh Swatch Switcher on Selection'.",
                    MessageType.Info);
            }
            else
            {
                var labels = BuildPopupLabels(swatches, count);
                var current = Mathf.Clamp(swatches.SelectedIndex, 0, count - 1);

                EditorGUI.BeginChangeCheck();
                var picked = EditorGUILayout.Popup("Swatch", current, labels);
                if (EditorGUI.EndChangeCheck() && picked != current)
                {
                    Undo.RecordObject(swatches, "Change Swatch");
                    swatches.SelectedIndex = picked;
                    EditorUtility.SetDirty(swatches);
                }
            }

            EditorGUILayout.Space();

            // Keep the default inspector for the underlying arrays so they remain visible/editable.
            DrawDefaultInspector();
        }

        private static string[] BuildPopupLabels(Sims4Creator.Sims4Swatches swatches, int count)
        {
            var labels = new string[count];
            for (var i = 0; i < count; i++)
            {
                var label = (swatches.swatchLabels != null && i < swatches.swatchLabels.Length)
                    ? swatches.swatchLabels[i]
                    : null;
                labels[i] = string.IsNullOrEmpty(label) ? $"Swatch {i}" : $"{i}: {label}";
            }

            return labels;
        }
    }
}
