using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Minimal IMGUI debug panel for M0: the game clock + speed controls, and each Sim's needs as bars.
    /// This is the first of the per-aspect "test tools"; it will grow / be replaced by real UI later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameDebugHud : MonoBehaviour
    {
        public SimulationDirector director;
        public GameClock clock;
        public PlayerController player;
        public GameCasMode cas;
        public GameBuildMode build;

        private Vector2 _scroll;

        private void Start()
        {
            if (director == null) director = FindFirstObjectByType<SimulationDirector>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (cas == null) cas = FindFirstObjectByType<GameCasMode>();
            if (build == null) build = FindFirstObjectByType<GameBuildMode>();
        }

        private void OnGUI()
        {
            const float w = 330f;
            GUILayout.BeginArea(new Rect(12, 12, w, Screen.height - 24), GUI.skin.box);

            GUILayout.Label(clock != null ? clock.Label : "(no clock)", Header());
            if (clock != null)
            {
                GUILayout.BeginHorizontal();
                SpeedButton("Pause", 0);
                SpeedButton("1x", 1);
                SpeedButton("2x", 2);
                SpeedButton("3x", 3);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(6);

            if (build != null && GUILayout.Button("Build Mode  (place / move objects)")) build.EnterBuild();
            GUILayout.Space(8);

            // Possession
            if (player != null)
            {
                if (player.Possessed != null)
                {
                    GUILayout.Label($"POSSESSING: {(player.Possessed.Soul != null ? player.Possessed.Soul.displayName : player.Possessed.name)}", SubHeader());
                    GUILayout.Label("Click an object to direct  ·  Esc to release");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Release")) player.Release();
                    if (cas != null && GUILayout.Button("Edit Look (CAS)")) cas.EnterCas(player.Possessed);
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.Label("Click a Sim to possess", SubHeader());
                }
                if (!string.IsNullOrEmpty(player.LastActionMessage)) GUILayout.Label(player.LastActionMessage);
            }
            GUILayout.Space(10);

            _scroll = GUILayout.BeginScrollView(_scroll);
            if (director != null)
            {
                foreach (var body in director.Bodies)
                {
                    if (body == null || body.Soul == null) continue;
                    var agent = body.GetComponent<SimAgent>();
                    string status = agent != null ? agent.StatusLabel : "";
                    bool possessed = player != null && player.Possessed == body;
                    GUILayout.Label($"{(possessed ? "▶ " : "")}{body.Soul.displayName}    <{status}>", SubHeader());
                    foreach (var n in body.Soul.needs) Bar(n.id, n.value);
                    GUILayout.Space(6);
                }

                // Relationships — only pairs that have actually interacted (keeps the list short).
                var souls = director.Souls;
                bool relHeader = false;
                for (int a = 0; a < souls.Count; a++)
                    for (int b = a + 1; b < souls.Count; b++)
                    {
                        float r = director.Relationships.Get(souls[a].simId, souls[b].simId);
                        if (Mathf.Abs(r) < 1f) continue;
                        if (!relHeader) { GUILayout.Space(4); GUILayout.Label("Relationships", SubHeader()); relHeader = true; }
                        GUILayout.Label($"{souls[a].displayName} ↔ {souls[b].displayName}:  {Mathf.RoundToInt(r)}  ({RelLabel(r)})");
                    }
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private static string RelLabel(float r)
        {
            if (r >= 60f) return "close";
            if (r >= 25f) return "friends";
            if (r >= 5f) return "acquaintances";
            if (r <= -25f) return "disliked";
            return "strangers";
        }

        private void SpeedButton(string label, int idx)
        {
            bool on = clock.speedIndex == idx;
            var prev = GUI.backgroundColor;
            if (on) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button(label)) clock.speedIndex = idx;
            GUI.backgroundColor = prev;
        }

        private static void Bar(string label, float value0to100)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(70));
            var rect = GUILayoutUtility.GetRect(190, 14);
            GUI.Box(rect, GUIContent.none);
            float t = Mathf.Clamp01(value0to100 / 100f);
            var fill = new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * t, rect.height - 2);
            var prev = GUI.color;
            GUI.color = Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.3f, 0.8f, 0.3f), t);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = prev;
            GUILayout.Space(6);
            GUILayout.Label(Mathf.RoundToInt(value0to100).ToString(), GUILayout.Width(28));
            GUILayout.EndHorizontal();
        }

        private static GUIStyle _header, _subHeader;
        private static GUIStyle Header()
        {
            if (_header == null) _header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            return _header;
        }
        private static GUIStyle SubHeader()
        {
            if (_subHeader == null) _subHeader = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            return _subHeader;
        }
    }
}
