using UnityEngine;
using UnityEngine.UIElements;

namespace Sims4Creator.Game
{
    /// <summary>
    /// THE debug menu — a proper UI Toolkit panel in the game's own glass style (user direction: no more
    /// IMGUI side box). Opened by the HUD's DBG button or F3; draws above the HUD and the build dock
    /// (own UIDocument, sortingOrder 30), so it works in every mode.
    ///
    /// Sections:
    ///   GRID   — the D-106 visual debuggers (grid / occupancy / sim cells / paths / nav) + live per-Sim
    ///            tile info (drawn by <see cref="GridDebugOverlay"/>).
    ///   RENDER — the TASK-016 tunables (SSGI, sun shadow dimmer, ambient boost, EV offset, fill light,
    ///            tonemap, shadow lift) via <see cref="RenderDebugController"/>. Tune live first — bake
    ///            as scene defaults after ("Log tuning values" prints the bake handoff).
    ///
    /// Self-healing: if the scene predates any piece (no overlay / no render rig), the menu still opens,
    /// bootstraps what it can, and shows a "re-run Build M0 Scene" hint for the parts that need baking.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameDebugMenu : MonoBehaviour
    {
        public KeyCode toggleKey = KeyCode.F3;

        [Header("Wired by the scene builder (self-resolves when missing)")]
        public UIDocument document;          // created at runtime if absent
        public StyleSheet theme;             // GameTheme.uss
        public GridDebugOverlay gridDebug;
        public RenderDebugController render;
        public SimulationDirector director;

        public bool Visible { get; private set; }

        /// <summary>Bootstrap UX: a Toggle() that lands before Start (menu created by the first DBG/F3
        /// press on an old scene) queues an immediate open instead of being swallowed.</summary>
        [HideInInspector] public bool openOnStart;

        private VisualElement _panel, _simList;
        private Label _gridInfo, _occLabel;
        private float _nextSimRefresh;
        private bool _built;

        private void Awake()
        {
            if (gridDebug == null) gridDebug = FindFirstObjectByType<GridDebugOverlay>();
            if (gridDebug == null) gridDebug = gameObject.AddComponent<GridDebugOverlay>();
            if (render == null) render = FindFirstObjectByType<RenderDebugController>();
            if (director == null) director = FindFirstObjectByType<SimulationDirector>();
        }

        private void Start()
        {
            EnsureDocument();
            Build();
            SetVisible(openOnStart);
        }

        private void OnDisable() { if (document != null) UiPointer.Unregister(document); }

        /// <summary>The menu needs a UIDocument; borrow the HUD's PanelSettings when self-created.</summary>
        private void EnsureDocument()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null)
            {
                PanelSettings settings = null;
                foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                    if (doc != null && doc.panelSettings != null) { settings = doc.panelSettings; break; }
                document = gameObject.AddComponent<UIDocument>();
                document.panelSettings = settings;
            }
            document.sortingOrder = 30; // above HUD (0) and build dock (10)
        }

        public void Toggle()
        {
            if (!_built) { openOnStart = true; return; } // created this frame — open once Start builds it
            SetVisible(!Visible);
        }

        public void SetVisible(bool show)
        {
            Visible = show;
            if (_panel != null) _panel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show && _built) SyncControls();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey) && !UiPointer.TextInputFocused()) Toggle();
            if (!Visible || !_built) return;

            if (_gridInfo != null && gridDebug != null && gridDebug.grid != null)
            {
                var g = gridDebug.grid;
                _gridInfo.text = $"{g.tilesX}×{g.tilesZ} tiles @ {g.tileSize:0.#} m · nav ÷{g.navSubdivision} ({g.NavCellSize:0.##} m)";
            }
            if (_occLabel != null && gridDebug != null)
                _occLabel.text = gridDebug.showOccupancy ? $"owned tiles: {gridDebug.OccupiedCount}" : "";

            if (Time.unscaledTime >= _nextSimRefresh)
            {
                _nextSimRefresh = Time.unscaledTime + 0.25f;
                RefreshSimList();
            }
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            if (document == null) return;
            var root = document.rootVisualElement;
            if (root == null) return;
            root.style.flexGrow = 1;
            root.pickingMode = PickingMode.Ignore;
            if (theme != null && !root.styleSheets.Contains(theme)) root.styleSheets.Add(theme);
            UiPointer.Register(document);

            _panel = new VisualElement();
            _panel.AddToClassList("glass");
            _panel.AddToClassList("debug-menu");
            root.Add(_panel);

            // header
            var head = new VisualElement();
            head.AddToClassList("row");
            var title = new Label("DEBUG");
            title.AddToClassList("debug-title");
            var spacer = new VisualElement();
            spacer.AddToClassList("spacer");
            var close = new Button(() => SetVisible(false)) { text = "✕" };
            close.AddToClassList("btn");
            head.Add(title); head.Add(spacer); head.Add(close);
            _panel.Add(head);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            _panel.Add(scroll);

            // ---------------- GRID ----------------
            Section(scroll, "GRID");
            _gridInfo = Note(scroll, "");
            if (gridDebug != null)
            {
                AddToggle(scroll, "1 m grid", () => gridDebug.showGrid, v => gridDebug.showGrid = v);
                AddToggle(scroll, "Furniture occupancy", () => gridDebug.showOccupancy, v => gridDebug.showOccupancy = v);
                _occLabel = Note(scroll, "");
                AddToggle(scroll, "Sim cells (current / goal / transit)", () => gridDebug.showSimCells, v => gridDebug.showSimCells = v);
                AddToggle(scroll, "Sim paths (0.5 m route)", () => gridDebug.showPaths, v => gridDebug.showPaths = v);
                AddToggle(scroll, "Nav blocked sub-cells", () => gridDebug.showNavBlocked, v => gridDebug.showNavBlocked = v);
                if (gridDebug.matOccupied == null)
                    Note(scroll, "⚠ cell visuals not baked — re-run Build M0 Scene.");
            }
            _simList = new VisualElement();
            _simList.AddToClassList("debug-simlist");
            scroll.Add(_simList);
            Note(scroll, "Sims do NOT reserve tiles (soft separation only) — TASK-014 ph. 2.");

            // ---------------- RENDER ----------------
            Section(scroll, "RENDER");
            if (render == null || !render.Ready)
            {
                Note(scroll, "⚠ render rig not in this scene — re-run Build M0 Scene.");
            }
            else
            {
                AddToggle(scroll, "SSGI (screen-space bounce)", () => render.SsgiEnabled, v => render.SsgiEnabled = v);
                AddSlider(scroll, "Sun shadow dimmer", 0f, 1f, () => render.ShadowDimmer, v => render.ShadowDimmer = v);
                AddSlider(scroll, "Ambient boost ×", 0f, 4f, () => render.AmbientBoost, v => render.AmbientBoost = v);
                AddSlider(scroll, "Exposure offset (EV)", -3f, 3f, () => render.EvOffset, v => render.EvOffset = v);
                AddToggle(scroll, "Fill light (shadowless)", () => render.FillEnabled, v => render.FillEnabled = v);
                AddSlider(scroll, "Fill intensity (lux)", 0f, 30000f, () => render.FillLux, v => render.FillLux = v);
                AddDropdown(scroll, "Tonemap", new System.Collections.Generic.List<string> { "None", "Neutral", "ACES" },
                            () => render.TonemapIndex, v => render.TonemapIndex = v);
                AddSlider(scroll, "Shadow lift (grade)", 0f, 0.3f, () => render.ShadowLift, v => render.ShadowLift = v);
                AddSlider(scroll, "Moon max lux (night)", 0f, 30f, () => render.MoonMaxLux, v => render.MoonMaxLux = v);
                AddSlider(scroll, "Night exposure EV (lower = brighter)", 1f, 6f, () => render.NightEv, v => render.NightEv = v);

                var log = new Button(() => Debug.Log(render.Snapshot())) { text = "Log tuning values" };
                log.AddToClassList("btn");
                log.AddToClassList("btn--wide");
                scroll.Add(log);
                Note(scroll, "Tune live, then 'Log tuning values' → we bake them as scene defaults (TASK-016).");
            }

            _built = true;
        }

        // ------------------------------------------------------------------ widgets

        private readonly System.Collections.Generic.List<(Toggle t, System.Func<bool> get)> _toggles = new();
        private readonly System.Collections.Generic.List<(Slider s, System.Func<float> get)> _sliders = new();
        private readonly System.Collections.Generic.List<(DropdownField d, System.Func<int> get)> _drops = new();

        private static void Section(VisualElement parent, string title)
        {
            var l = new Label(title);
            l.AddToClassList("panel-title");
            l.AddToClassList("debug-section");
            parent.Add(l);
        }

        private static Label Note(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.AddToClassList("debug-note");
            parent.Add(l);
            return l;
        }

        private void AddToggle(VisualElement parent, string label, System.Func<bool> get, System.Action<bool> set)
        {
            var t = new Toggle(label) { value = get() };
            t.AddToClassList("debug-toggle");
            t.RegisterValueChangedCallback(e => set(e.newValue));
            parent.Add(t);
            _toggles.Add((t, get));
        }

        private void AddSlider(VisualElement parent, string label, float lo, float hi, System.Func<float> get, System.Action<float> set)
        {
            var s = new Slider(label, lo, hi) { value = get(), showInputField = true };
            s.AddToClassList("debug-slider");
            s.RegisterValueChangedCallback(e => set(e.newValue));
            parent.Add(s);
            _sliders.Add((s, get));
        }

        private void AddDropdown(VisualElement parent, string label, System.Collections.Generic.List<string> choices,
                                 System.Func<int> get, System.Action<int> set)
        {
            var d = new DropdownField(label, choices, Mathf.Clamp(get(), 0, choices.Count - 1));
            d.AddToClassList("debug-slider");
            d.RegisterValueChangedCallback(_ => set(d.index));
            parent.Add(d);
            _drops.Add((d, get));
        }

        /// <summary>Re-read live state into the controls when the menu opens (values may change elsewhere).</summary>
        private void SyncControls()
        {
            foreach (var (t, get) in _toggles) t.SetValueWithoutNotify(get());
            foreach (var (s, get) in _sliders) s.SetValueWithoutNotify(get());
            foreach (var (d, get) in _drops) d.index = Mathf.Clamp(get(), 0, d.choices.Count - 1);
        }

        private void RefreshSimList()
        {
            if (_simList == null) return;
            _simList.Clear();
            if (director == null || gridDebug == null || gridDebug.grid == null) return;
            var grid = gridDebug.grid;
            var bodies = director.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                var b = bodies[i];
                if (b == null) continue;
                var a = b.GetComponent<SimAgent>();
                var p = b.transform.position;
                string goal = a != null && a.DebugMoving
                    ? $"  →  ({grid.TileX(a.DebugGoal.x)},{grid.TileZ(a.DebugGoal.z)})" : "";
                var l = new Label($"{b.displayName}: {(a != null ? a.StatusLabel : "?")}   ({grid.TileX(p.x)},{grid.TileZ(p.z)}){goal}");
                l.AddToClassList("debug-sim");
                _simList.Add(l);
            }
        }
    }
}
