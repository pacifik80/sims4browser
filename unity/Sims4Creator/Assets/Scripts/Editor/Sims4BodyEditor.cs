using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Inspector for <see cref="Sims4Body"/>: toggle individual parts, and quick-toggle whole regions
    /// (the same hide/show a clothing item will drive when it covers a body region).
    /// </summary>
    [CustomEditor(typeof(Sims4Body))]
    public sealed class Sims4BodyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var body = (Sims4Body)target;

            EditorGUILayout.LabelField("Regions", EditorStyles.boldLabel);
            var regions = new List<string>();
            foreach (var p in body.parts)
            {
                if (!string.IsNullOrEmpty(p.region) && !regions.Contains(p.region))
                {
                    regions.Add(p.region);
                }
            }

            foreach (var region in regions)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(region, GUILayout.Width(120));
                if (GUILayout.Button("Show", GUILayout.Width(60)))
                {
                    Undo.RecordObject(body, "Show region");
                    body.SetRegionVisible(region, true);
                    EditorUtility.SetDirty(body);
                }

                if (GUILayout.Button("Hide", GUILayout.Width(60)))
                {
                    Undo.RecordObject(body, "Hide region");
                    body.SetRegionVisible(region, false);
                    EditorUtility.SetDirty(body);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Parts", EditorStyles.boldLabel);
            foreach (var p in body.parts)
            {
                var v = EditorGUILayout.ToggleLeft($"{p.name}   [{p.region}]", p.visible);
                if (v != p.visible)
                {
                    Undo.RecordObject(body, "Toggle part");
                    p.visible = v;
                    if (p.renderer != null)
                    {
                        p.renderer.enabled = v;
                    }

                    EditorUtility.SetDirty(body);
                }
            }
        }
    }
}
