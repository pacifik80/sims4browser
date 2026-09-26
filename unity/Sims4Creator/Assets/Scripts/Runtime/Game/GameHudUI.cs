using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sims4Creator.Game
{
    /// <summary>
    /// The game's UI shell (UI Toolkit, decision D-105), styled after inZOI: floating glass clusters
    /// pinned to the screen edges with a clear centre. All zones are present; ones without a system yet
    /// (camera modes, money, extra modes) render as placeholder slots so the composition reads whole.
    /// Everything is styled from <c>GameTheme.uss</c>; icons come from <see cref="IconLibrary"/> (which
    /// returns a placeholder for any missing name).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHudUI : MonoBehaviour
    {
        public UIDocument document;
        public SimulationDirector director;
        public GameClock clock;
        public PlayerController player;
        public GameCasMode cas;
        public GameBuildMode build;
        public GameDebugMenu debugMenu;
        public IconLibrary icons;

        private static readonly string[] SpeedIcons =
            { "speed-pause", "speed-normal", "speed-x2", "speed-x3", "speed-x5-max" };

        private VisualElement _root;
        private Label _clock, _status, _focusName, _focusInitial, _focusAction, _moodText;
        private VisualElement _focusCard, _focusPortrait, _moodIcon, _needStrip, _portraitList;
        private Button _btnBuild, _btnCas, _btnMenu, _btnDebug;
        private readonly Button[] _speed = new Button[5];

        // right-edge portraits, one per Sim
        private sealed class Portrait { public SimBody body; public VisualElement chip; public Label initial; }
        private readonly List<Portrait> _portraits = new List<Portrait>();

        // focus need strip, rebuilt when the possessed Sim changes
        private SimBody _focusBody;
        private readonly List<(string id, VisualElement icon)> _needChips = new List<(string, VisualElement)>();

        private static string NeedIcon(string id)
        {
            switch (id)
            {
                case "hunger":  return "need-hungry";
                case "energy":  return "need-sleep";
                case "fun":     return "need-bored-fun";
                case "bladder": return "need-toilet";
                case "hygiene": return "need-hygiene"; // → placeholder until provided (TASK-005)
                case "social":  return "need-social";  // → placeholder until provided
                default:        return null;
            }
        }

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (director == null) director = FindFirstObjectByType<SimulationDirector>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (cas == null) cas = FindFirstObjectByType<GameCasMode>();
            if (build == null) build = FindFirstObjectByType<GameBuildMode>();
            if (debugMenu == null) debugMenu = FindFirstObjectByType<GameDebugMenu>();
        }

        private void OnDisable()
        {
            UiPointer.Unregister(document);
        }

        private void OnEnable()
        {
            if (document == null) return;
            UiPointer.Register(document);
            _root = document.rootVisualElement;
            if (_root == null) return;

            _clock = _root.Q<Label>("clock");
            _status = _root.Q<Label>("status");
            _focusCard = _root.Q<VisualElement>("zone-focus");
            _focusPortrait = _root.Q<VisualElement>("focus-portrait");
            _focusInitial = _root.Q<Label>("focus-initial");
            _focusName = _root.Q<Label>("focus-name");
            _focusAction = _root.Q<Label>("focus-action");
            _moodIcon = _root.Q<VisualElement>("mood-icon");
            _moodText = _root.Q<Label>("mood-text");
            _needStrip = _root.Q<VisualElement>("need-strip");
            _portraitList = _root.Q<VisualElement>("portrait-list");

            // static icons
            SetIcon("loc-icon", "location");        // no pin in the pack → placeholder
            SetIcon("daynight-icon", "settings");   // stand-in until a weather/day-night icon exists
            SetIcon("menu-icon", "settings");
            SetIcon("build-icon", "build-mode");
            SetIcon("cas-icon", "character-edit");
            SetIcon("money-icon", "coin");          // → placeholder
            for (int i = 0; i < 5; i++) SetIcon("speed-" + i + "-img", SpeedIcons[i]);
            // placeholder slots show the placeholder clearly
            _root.Query<Button>(className: "view-slot").ForEach(b => SetIconElem(b.Q<VisualElement>(className: "icon-img"), null));
            _root.Query<Button>(className: "mode-slot").ForEach(b => SetIconElem(b.Q<VisualElement>(className: "icon-img"), null));

            // speed transport
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                _speed[i] = _root.Q<Button>("speed-" + i);
                if (_speed[i] != null) _speed[i].clicked += () => { if (clock != null) clock.speedIndex = idx; };
            }

            _btnBuild = _root.Q<Button>("btn-build");
            if (_btnBuild != null) _btnBuild.clicked += () => { if (build != null) build.EnterBuild(); };

            _btnCas = _root.Q<Button>("btn-cas");
            if (_btnCas != null) _btnCas.clicked += () =>
            {
                if (cas != null && player != null && player.Possessed != null) cas.EnterCas(player.Possessed);
            };

            _btnMenu = _root.Q<Button>("btn-menu"); // no menu screen yet — placeholder

            _btnDebug = _root.Q<Button>("btn-debug"); // the debug menu (same as F3)
            if (_btnDebug != null) _btnDebug.clicked += () =>
            {
                EnsureDebugMenu();
                debugMenu.Toggle();
            };

            _portraits.Clear();
        }

        /// <summary>Delegates to <see cref="UiPointer"/> — the one verified over-UI test (flip Y →
        /// ScreenToPanel → panel.Pick across every registered document, HUD and build dock alike).</summary>
        public bool IsPointerOverUI() => UiPointer.OverAnyUi();

        /// <summary>
        /// The debug menu must never be unreachable: if the scene predates it, bootstrap one at runtime
        /// (it self-resolves its document/overlay and shows "re-run Build M0 Scene" hints for the parts
        /// that need scene-baked assets).
        /// </summary>
        private void EnsureDebugMenu()
        {
            if (debugMenu != null) return;
            debugMenu = FindFirstObjectByType<GameDebugMenu>();
            if (debugMenu == null)
                debugMenu = new GameObject("Debug Menu (bootstrap)").AddComponent<GameDebugMenu>();
        }

        private void Update()
        {
            if (_root == null) return;

            // F3 fallback for scenes without a debug-menu object — once the menu exists, ITS Update owns
            // the key (guarded on null here so the toggle never fires twice).
            if (debugMenu == null && Input.GetKeyDown(KeyCode.F3) && !UiPointer.TextInputFocused())
            {
                EnsureDebugMenu();
            }

            bool modeOpen = (cas != null && cas.InCas) || (build != null && build.InBuild);
            _root.style.display = modeOpen ? DisplayStyle.None : DisplayStyle.Flex;
            if (modeOpen) return;

            if (_btnDebug != null)
                _btnDebug.EnableInClassList("icon-btn--on", debugMenu != null && debugMenu.Visible);

            if (_clock != null && clock != null) _clock.text = clock.Label;

            for (int i = 0; i < _speed.Length; i++)
                if (_speed[i] != null && clock != null) _speed[i].EnableInClassList("icon-btn--on", clock.speedIndex == i);

            bool possessing = player != null && player.Possessed != null;
            if (_btnCas != null) _btnCas.SetEnabled(possessing);

            SyncPortraits();
            RefreshPortraits();
            RefreshFocus(possessing);

            if (_status != null)
                _status.text = possessing
                    ? "Double-click = hurry  ·  Esc = release  ·  WASD / right-drag / scroll = camera"
                    : "Click a Sim to possess  ·  WASD / right-drag / scroll = camera  ·  F = focus";
        }

        // ----- right-edge portraits -----

        private void SyncPortraits()
        {
            if (director == null || _portraitList == null) return;
            var bodies = director.Bodies;
            if (_portraits.Count == bodies.Count) return;

            _portraitList.Clear();
            _portraits.Clear();
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body == null || body.Soul == null) continue;

                var chip = new VisualElement();
                chip.AddToClassList("portrait");
                chip.style.backgroundColor = PortraitColor(body.Soul.displayName);
                var initial = new Label(Initial(body.Soul.displayName));
                initial.AddToClassList("portrait-initial");
                chip.Add(initial);
                chip.RegisterCallback<ClickEvent>(_ => { if (player != null) player.Possess(body); });

                _portraitList.Add(chip);
                _portraits.Add(new Portrait { body = body, chip = chip, initial = initial });
            }
        }

        private void RefreshPortraits()
        {
            for (int i = 0; i < _portraits.Count; i++)
                _portraits[i].chip.EnableInClassList("portrait--sel",
                    player != null && player.Possessed == _portraits[i].body);
        }

        // ----- bottom-centre focus card -----

        private void RefreshFocus(bool possessing)
        {
            if (_focusCard == null) return;
            _focusCard.style.display = possessing ? DisplayStyle.Flex : DisplayStyle.None;
            if (!possessing) { _focusBody = null; return; }

            var body = player.Possessed;
            var soul = body.Soul;
            if (soul == null) return;

            if (_focusPortrait != null) _focusPortrait.style.backgroundColor = PortraitColor(soul.displayName);
            if (_focusInitial != null) _focusInitial.text = Initial(soul.displayName);
            if (_focusName != null) _focusName.text = soul.displayName;

            var agent = body.GetComponent<SimAgent>();
            if (_focusAction != null) _focusAction.text = agent != null ? agent.StatusLabel : "";

            var (moodIcon, moodText) = MoodFor(soul);
            if (_moodText != null) _moodText.text = moodText;
            SetIconElem(_moodIcon, moodIcon);

            if (body != _focusBody) BuildNeedStrip(soul);
            _focusBody = body;

            for (int i = 0; i < _needChips.Count; i++)
            {
                var need = soul.GetNeed(_needChips[i].id);
                if (need == null || _needChips[i].icon == null) continue;
                _needChips[i].icon.style.unityBackgroundImageTintColor =
                    Color.Lerp(new Color(0.9f, 0.32f, 0.34f), new Color(0.4f, 0.82f, 0.48f), Mathf.Clamp01(need.value / 100f));
            }
        }

        private void BuildNeedStrip(SimSoul soul)
        {
            if (_needStrip == null) return;
            _needStrip.Clear();
            _needChips.Clear();
            foreach (var need in soul.needs)
            {
                var icon = new VisualElement();
                icon.AddToClassList("icon-img");
                icon.AddToClassList("icon-img--sm");
                icon.style.marginRight = 7;
                SetIconElem(icon, NeedIcon(need.id));
                _needStrip.Add(icon);
                _needChips.Add((need.id, icon));
            }
        }

        // ----- helpers -----

        private void SetIcon(string elementName, string iconName)
        {
            if (_root == null) return;
            SetIconElem(_root.Q<VisualElement>(elementName), iconName);
        }

        private void SetIconElem(VisualElement el, string iconName)
        {
            if (el == null || icons == null) return;
            var vi = icons.Get(iconName); // null iconName → placeholder
            if (vi != null) el.style.backgroundImage = new StyleBackground(vi);
        }

        private static string Initial(string name) =>
            string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();

        private static Color PortraitColor(string name)
        {
            int h = 0;
            if (!string.IsNullOrEmpty(name)) foreach (char c in name) h = h * 31 + c;
            float hue = Mathf.Abs(h % 360) / 360f;
            return Color.HSVToRGB(hue, 0.42f, 0.58f);
        }

        // A rough live mood from the worst need (the real mood system is later).
        private static (string icon, string text) MoodFor(SimSoul soul)
        {
            float min = 100f;
            foreach (var n in soul.needs) if (n.value < min) min = n.value;
            if (min < 15f) return ("emote-sad", "Miserable");
            if (min < 35f) return ("emote-indifferent", "Uneasy");
            if (min > 75f) return ("emote-happy", "Content");
            return ("emote-smile", "Fine");
        }
    }
}
