using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// "Forge" — a modern UI Toolkit chat panel that talks to Claude on the user's logged-in
    /// Claude Code account (drives the real <c>claude</c> CLI via <see cref="ClaudeCliDriver"/>).
    /// Compact IDE-style layout: collapsible tool-call blocks, streaming, markdown, history,
    /// spend tracking. NOTE: headless usage is metered against the account's programmatic
    /// ("Agent SDK") credit, separate from interactive use. Menu: "Sims4 Creator/Claude Chat".
    /// </summary>
    public sealed class Sims4ClaudeChatWindow : EditorWindow
    {
        // ---- backend / state ----
        ClaudeCliDriver _driver;
        ClaudeMarkdownRenderer _md;
        string _sessionId;
        string _lastUserPrompt;
        bool _running;
        double _sessionCost;
        double _monthSpent;
        string _monthKey = "";
        bool _followBottom = true;
        bool _scrollPending;

        // ---- settings (EditorPrefs) ----
        string _claudePath = "";
        string _workingDir = "";
        int _permIndex, _modelIndex, _effortIndex;
        bool _streamTokens = true;
        float _monthlyBudget;
        string _extraArgs = "";
        bool _clearApiKey = true;
        bool _showToolResults;
        bool _teachWidgets = true;

        // ---- live turn refs ----
        VisualElement _turnBody;
        Foldout _toolFold;
        int _toolCount;
        VisualElement _thinking;
        Label _streamLabel;
        string _streamText = "";

        // ---- UI refs ----
        ScrollView _transcript;
        Label _statusLabel, _chatCostLabel, _monthCostLabel;
        VisualElement _budgetFill, _settingsPanel;
        TextField _input;
        Button _sendBtn, _retryBtn, _stopBtn;
        Button _jumpPill;
        Font _monoFont;
        VisualElement _attachRow;
        readonly List<string> _attachments = new List<string>();

        static readonly string[] PermModes = { "bypassPermissions", "acceptEdits", "plan", "default" };
        static readonly string[] PermLabels = { "bypassPermissions", "acceptEdits", "plan", "default" };
        static readonly string[] ModelValues = { "", "claude-opus-4-8", "claude-sonnet-4-6", "claude-haiku-4-5", "claude-fable-5" };
        static readonly string[] ModelLabels = { "Auto", "Opus 4.8", "Sonnet 4.6", "Haiku 4.5", "Fable 5" };
        static readonly string[] EffortValues = { "", "low", "medium", "high", "xhigh", "max" };
        static readonly string[] EffortLabels = { "Effort", "low", "medium", "high", "xhigh", "max" };

        const string PrefPrefix = "Sims4Creator.ClaudeChat.";
        const int MaxTranscriptChildren = 600;
        // Taught to Claude via --append-system-prompt so it can intentionally embed previews.
        // Keep it free of double-quotes and cmd-special chars (& | < > ^ %) for safe quoting.
        const string WidgetSpec = "This Unity Editor chat panel renders inline previews. To embed one, write [[image:PATH]] for an image, [[model:PATH]] for a 3D model/prefab/mesh/material, or [[ping:PATH]] for a button that reveals the asset in the Project window. PATH may be project-relative (Assets/...), repo-relative, or absolute. Any asset path you mention (.png .jpg .fbx .prefab .asset .mat) is auto-previewed too. Use these whenever you create, edit, or reference a visual asset so the user sees it inline.";

        [MenuItem("Sims4 Creator/Claude Chat", priority = 200)]
        public static void ShowWindow()
        {
            var w = GetWindow<Sims4ClaudeChatWindow>("Claude Chat");
            w.minSize = new Vector2(380, 460);
            w.Show();
        }

        void OnEnable()
        {
            _running = false; // a domain reload kills any in-flight turn; don't keep a stale "running" state
            LoadPrefs();
            if (string.IsNullOrEmpty(_claudePath)) _claudePath = AutoDetectClaude();
            if (string.IsNullOrEmpty(_workingDir)) _workingDir = FindRepoRoot();
            RollMonthIfNeeded();
            _driver = new ClaudeCliDriver();
            _md = new ClaudeMarkdownRenderer(EditorGUIUtility.isProSkin) { WorkingDir = _workingDir };
            EditorApplication.update += Pump;
            AssemblyReloadEvents.beforeAssemblyReload += StopDriver;
            EditorApplication.quitting += StopDriver;
        }

        void OnDisable()
        {
            EditorApplication.update -= Pump;
            AssemblyReloadEvents.beforeAssemblyReload -= StopDriver;
            EditorApplication.quitting -= StopDriver;
            try { StopDriver(); } catch { /* ignore */ }
            try { SavePrefs(); } catch { /* ignore */ }
        }

        void StopDriver()
        {
            _driver?.Stop();
            _running = false;
        }

        // ---------------------------------------------------------------- build UI

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("chat-root");
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("light");

            var sheet = FindStyleSheet();
            if (sheet != null) root.styleSheets.Add(sheet);

            try { _monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Inter", "Helvetica Neue", "Arial" }, 12); } catch { _monoFont = null; }
            if (_monoFont != null) root.style.unityFontDefinition = FontDefinition.FromFont(_monoFont);

            root.Add(BuildToolbar());
            root.Add(BuildBudgetBar());
            _settingsPanel = BuildSettings();
            _settingsPanel.style.display = DisplayStyle.None;
            root.Add(_settingsPanel);

            var stack = new VisualElement();
            stack.AddToClassList("transcript-stack");
            _transcript = new ScrollView(ScrollViewMode.Vertical);
            _transcript.AddToClassList("transcript");
            _transcript.RegisterCallback<WheelEvent>(_ => ScheduleFollowCheck());
            _transcript.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => { if (_followBottom) ToBottom(); });
            stack.Add(_transcript);

            _jumpPill = new Button(() => SetFollow(true)) { text = "↓ latest" };
            _jumpPill.AddToClassList("jump-pill");
            _jumpPill.style.display = DisplayStyle.None;
            stack.Add(_jumpPill);
            root.Add(stack);

            root.Add(BuildComposer());

            UpdateStatus();
            UpdateBudget();
            if (string.IsNullOrEmpty(_claudePath)) ToggleSettings(true);
        }

        VisualElement BuildToolbar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("toolbar");
            bar.Add(TbButton("New", NewChat));
            bar.Add(TbButton("History", ShowHistoryMenu));
            _stopBtn = TbButton("Stop", StopTurn);
            _stopBtn.AddToClassList("tb-btn--stop");
            _stopBtn.SetEnabled(false);
            bar.Add(_stopBtn);
            bar.Add(TbButton("⚙", () => ToggleSettings(_settingsPanel.style.display == DisplayStyle.None)));

            var spacer = new VisualElement(); spacer.style.flexGrow = 1; bar.Add(spacer);

            var model = new DropdownField(new List<string>(ModelLabels), Mathf.Clamp(_modelIndex, 0, ModelLabels.Length - 1));
            model.AddToClassList("toolbar-dd");
            model.RegisterValueChangedCallback(_ => { _modelIndex = model.index; SavePrefs(); });
            bar.Add(model);

            _statusLabel = new Label("idle");
            _statusLabel.AddToClassList("status");
            bar.Add(_statusLabel);
            return bar;
        }

        VisualElement BuildBudgetBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("budget");
            _chatCostLabel = new Label(); _chatCostLabel.AddToClassList("budget-label"); bar.Add(_chatCostLabel);

            var track = new VisualElement(); track.AddToClassList("budget-track");
            _budgetFill = new VisualElement(); _budgetFill.AddToClassList("budget-fill");
            track.Add(_budgetFill); bar.Add(track);

            _monthCostLabel = new Label(); _monthCostLabel.AddToClassList("budget-label"); bar.Add(_monthCostLabel);

            var usage = new Button(() => Application.OpenURL("https://claude.ai/settings/usage")) { text = "Usage ↗" };
            usage.AddToClassList("usage-link"); bar.Add(usage);
            return bar;
        }

        VisualElement BuildComposer()
        {
            var composer = new VisualElement();
            composer.AddToClassList("composer");

            var tools = new VisualElement(); tools.AddToClassList("attach-tools");
            tools.Add(MiniBtn("📎 Attach", AttachFile));
            tools.Add(MiniBtn("⛰ Scene", () => CaptureAndAttach(true)));
            tools.Add(MiniBtn("▶ Game", () => CaptureAndAttach(false)));
            composer.Add(tools);

            _attachRow = new VisualElement(); _attachRow.AddToClassList("attach-row");
            _attachRow.style.display = DisplayStyle.None;
            composer.Add(_attachRow);

            _input = new TextField { multiline = true };
            _input.AddToClassList("input-field");
            _input.RegisterCallback<KeyDownEvent>(OnInputKey, TrickleDown.TrickleDown);
            composer.Add(_input);

            var row = new VisualElement(); row.AddToClassList("btn-row");
            var hint = new Label("Enter send · Shift+Enter newline"); hint.AddToClassList("input-hint"); row.Add(hint);
            var sp = new VisualElement(); sp.style.flexGrow = 1; row.Add(sp);
            _retryBtn = new Button(() => { if (!string.IsNullOrEmpty(_lastUserPrompt)) SendPrompt(_lastUserPrompt); }) { text = "↻ Retry" };
            _retryBtn.AddToClassList("btn-retry"); row.Add(_retryBtn);
            _sendBtn = new Button(SendFromInput) { text = "Send" };
            _sendBtn.AddToClassList("btn-send"); row.Add(_sendBtn);
            composer.Add(row);

            composer.RegisterCallback<DragUpdatedEvent>(_ => DragAndDrop.visualMode = DragAndDropVisualMode.Copy);
            composer.RegisterCallback<DragPerformEvent>(OnDragPerform);
            return composer;
        }

        Button MiniBtn(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("attach-btn");
            return b;
        }

        VisualElement BuildSettings()
        {
            var p = new VisualElement(); p.AddToClassList("settings");

            p.Add(PathRow("claude path", _claudePath, v => { _claudePath = v; SavePrefs(); }, browse: true, file: true, detect: true));
            p.Add(PathRow("working dir", _workingDir, v => { _workingDir = v; if (_md != null) _md.WorkingDir = v; SavePrefs(); }, browse: true, file: false, detect: false));

            var effort = new DropdownField("effort", new List<string>(EffortLabels), Mathf.Clamp(_effortIndex, 0, EffortLabels.Length - 1));
            effort.RegisterValueChangedCallback(_ => { _effortIndex = effort.index; SavePrefs(); });
            p.Add(effort);

            var perm = new DropdownField("permissions", new List<string>(PermLabels), Mathf.Clamp(_permIndex, 0, PermLabels.Length - 1));
            perm.RegisterValueChangedCallback(_ => { _permIndex = perm.index; SavePrefs(); });
            p.Add(perm);

            var budget = new FloatField("monthly credit $") { value = _monthlyBudget };
            budget.RegisterValueChangedCallback(e => { _monthlyBudget = e.newValue; UpdateBudget(); SavePrefs(); });
            p.Add(budget);

            p.Add(ToggleRow("stream tokens", _streamTokens, v => { _streamTokens = v; SavePrefs(); }));
            p.Add(ToggleRow("force subscription", _clearApiKey, v => { _clearApiKey = v; SavePrefs(); }));
            p.Add(ToggleRow("show tool results", _showToolResults, v => { _showToolResults = v; SavePrefs(); }));
            p.Add(ToggleRow("embed previews", _teachWidgets, v => { _teachWidgets = v; SavePrefs(); }));

            var extra = new TextField("extra args") { value = _extraArgs };
            extra.RegisterValueChangedCallback(e => { _extraArgs = e.newValue; SavePrefs(); });
            p.Add(extra);

            var note = new Label("Drives your logged-in `claude` CLI. Headless usage spends your account's programmatic (Agent SDK) credit, separate from interactive Claude Code.");
            note.AddToClassList("help-note");
            p.Add(note);
            return p;
        }

        VisualElement PathRow(string label, string value, Action<string> onChange, bool browse, bool file, bool detect)
        {
            var row = new VisualElement(); row.AddToClassList("setting-row");
            var tf = new TextField(label) { value = value }; tf.style.flexGrow = 1;
            tf.RegisterValueChangedCallback(e => onChange(e.newValue));
            row.Add(tf);
            if (detect)
                row.Add(new Button(() => { var d = AutoDetectClaude(); if (!string.IsNullOrEmpty(d)) { tf.value = d; } }) { text = "Detect" });
            if (browse)
                row.Add(new Button(() =>
                {
                    var pth = file
                        ? EditorUtility.OpenFilePanel("Locate the claude CLI", "", "exe,cmd,bat")
                        : EditorUtility.OpenFolderPanel("Working directory", tf.value, "");
                    if (!string.IsNullOrEmpty(pth)) tf.value = pth;
                }) { text = "…" });
            return row;
        }

        static VisualElement ToggleRow(string label, bool value, Action<bool> onChange)
        {
            var t = new Toggle(label) { value = value };
            t.RegisterValueChangedCallback(e => onChange(e.newValue));
            return t;
        }

        Button TbButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("tb-btn");
            return b;
        }

        // ---------------------------------------------------------------- event pump

        void Pump()
        {
            if (_driver == null || _transcript == null) return;
            while (_driver.Events.TryDequeue(out var ev)) HandleEvent(ev);
        }

        void HandleEvent(ClaudeEvent ev)
        {
            switch (ev.Kind)
            {
                case ClaudeEventKind.Init:
                    if (!string.IsNullOrEmpty(ev.SessionId)) { _sessionId = ev.SessionId; UpdateStatus(); }
                    AddSystem($"session {Short(ev.SessionId)} · {ev.Model} · auth: {DescribeAuth(ev.AuthSource)}");
                    break;
                case ClaudeEventKind.Assistant:
                    FinalizeStreamSegment();
                    HideThinking();
                    EnsureTurnBody().Add(_md.Build(ev.Text));
                    break;
                case ClaudeEventKind.PartialStart:
                    FinalizeStreamSegment();
                    break;
                case ClaudeEventKind.Partial:
                    HideThinking();
                    if (_streamLabel == null)
                    {
                        _streamText = "";
                        _streamLabel = new Label { enableRichText = false };
                        _streamLabel.AddToClassList("md-p");
                        _streamLabel.selection.isSelectable = true;
                        EnsureTurnBody().Add(_streamLabel);
                    }
                    _streamText += ev.Text;
                    _streamLabel.text = _streamText;
                    break;
                case ClaudeEventKind.PartialEnd:
                    FinalizeStreamSegment();
                    break;
                case ClaudeEventKind.Tool:
                    HideThinking();
                    AddToolLine(ev.ToolName, ev.Text);
                    break;
                case ClaudeEventKind.ToolResult:
                    if (_showToolResults) AddToolLine("↳", ev.Text);
                    break;
                case ClaudeEventKind.Info:
                    AddSystem(ev.Text);
                    break;
                case ClaudeEventKind.Error:
                    AddLine(ev.Text, "error-line");
                    break;
                case ClaudeEventKind.Result:
                    FinalizeStreamSegment();
                    HideThinking();
                    AccrueCost(ev.CostUsd);
                    string secs = (ev.DurationMs / 1000.0).ToString("0.0");
                    string turn = ev.CostUsd > 0 ? $"≈${ev.CostUsd:0.000}" : "no cost reported";
                    AddLine($"{(ev.Success ? "✓" : "✗")} {secs}s · {turn}", "result-line");
                    if (!ev.Success && !string.IsNullOrEmpty(ev.Text)) AddLine(ev.Text, "error-line");
                    break;
                case ClaudeEventKind.Exited:
                    _running = false;
                    FinalizeStreamSegment();
                    HideThinking();
                    _turnBody = null; _toolFold = null;
                    UpdateRunningUI();
                    if (!ev.Stopped && !ev.Success && ev.ExitCode != 0)
                        AddLine($"(claude exited with code {ev.ExitCode} — check path / permission mode / login)", "error-line");
                    break;
            }
            Trim();
            if (_followBottom) ToBottom();
        }

        // ---------------------------------------------------------------- transcript builders

        void AddUserMessage(string text)
        {
            var msg = new VisualElement(); msg.AddToClassList("msg-user");
            var head = new VisualElement(); head.AddToClassList("msg-head");
            head.Add(Tag("you", "role")); head.Add(Tag(DateTime.Now.ToString("HH:mm"), "msg-time"));
            msg.Add(head);
            var body = new Label(text); body.AddToClassList("user-text"); body.selection.isSelectable = true;
            msg.Add(body);
            _transcript.Add(msg);
        }

        void BeginAssistantTurn()
        {
            var turn = new VisualElement(); turn.AddToClassList("msg-claude");

            var head = new VisualElement(); head.AddToClassList("msg-head");
            head.Add(Tag("◈ claude", "role role--claude"));
            head.Add(Tag(DateTime.Now.ToString("HH:mm"), "msg-time"));
            var sp = new VisualElement(); sp.style.flexGrow = 1; head.Add(sp);
            turn.Add(head);

            _thinking = new VisualElement(); _thinking.AddToClassList("thinking");
            _thinking.Add(new Label("● ● ●") { name = "dots" });
            _thinking.Add(new Label("Claude is thinking…"));
            turn.Add(_thinking);

            _turnBody = new VisualElement(); _turnBody.AddToClassList("turn-body"); turn.Add(_turnBody);

            _toolFold = new Foldout { text = "⚙ tool calls", value = true };
            _toolFold.AddToClassList("tool-fold");
            _toolFold.style.display = DisplayStyle.None;
            _toolCount = 0;
            turn.Add(_toolFold);

            var actions = new VisualElement(); actions.AddToClassList("msg-actions");
            var copy = new Button(() => EditorGUIUtility.systemCopyBuffer = TurnPlainText(_turnBody)) { text = "⧉ copy" };
            copy.AddToClassList("copy-btn");
            actions.Add(copy);
            turn.Add(actions);

            _streamLabel = null; _streamText = "";
            _transcript.Add(turn);
        }

        VisualElement EnsureTurnBody()
        {
            if (_turnBody == null) BeginAssistantTurn();
            return _turnBody;
        }

        void AddToolLine(string verb, string info)
        {
            // A late tool event after the turn ended shouldn't spawn an orphan empty bubble.
            if (_turnBody == null) { AddSystem("🔧 " + verb + (string.IsNullOrEmpty(info) ? "" : " " + info)); return; }
            if (_toolFold == null) return;
            _toolFold.style.display = DisplayStyle.Flex;
            _toolCount++;
            _toolFold.text = $"⚙ {_toolCount} tool call{(_toolCount == 1 ? "" : "s")}";
            var line = new VisualElement(); line.AddToClassList("tool-line");
            line.Add(Tag("✓", "tool-ok"));
            line.Add(Tag(verb ?? "", "tool-verb"));
            if (!string.IsNullOrEmpty(info)) line.Add(Tag(info, "tool-info"));
            _toolFold.Add(line);
        }

        void FinalizeStreamSegment()
        {
            if (_streamLabel == null) return;
            var parent = _streamLabel.parent;
            int idx = parent != null ? parent.IndexOf(_streamLabel) : -1;
            _streamLabel.RemoveFromHierarchy();
            if (!string.IsNullOrEmpty(_streamText) && parent != null)
            {
                var rendered = _md.Build(_streamText);
                if (idx >= 0 && idx <= parent.childCount) parent.Insert(idx, rendered);
                else parent.Add(rendered);
            }
            _streamLabel = null; _streamText = "";
        }

        void HideThinking()
        {
            if (_thinking != null) { _thinking.style.display = DisplayStyle.None; _thinking = null; }
        }

        void AddSystem(string text) => AddLine(text, "system-line");

        void AddLine(string text, string cls)
        {
            if (string.IsNullOrEmpty(text)) return;
            var l = new Label(text); l.AddToClassList(cls); l.selection.isSelectable = true;
            _transcript.Add(l);
        }

        static Label Tag(string text, string classes)
        {
            var l = new Label(text);
            foreach (var c in classes.Split(' ')) if (c.Length > 0) l.AddToClassList(c);
            return l;
        }

        static string TurnPlainText(VisualElement body)
        {
            if (body == null) return "";
            var sb = new System.Text.StringBuilder();
            CollectText(body, sb);
            return sb.ToString().Trim();
        }

        static void CollectText(VisualElement e, System.Text.StringBuilder sb)
        {
            if (e is Label l) { sb.Append(l.text).Append('\n'); return; }
            foreach (var c in e.Children()) CollectText(c, sb);
        }

        void Trim()
        {
            while (_transcript.childCount > MaxTranscriptChildren)
                _transcript.RemoveAt(0);
        }

        // ---------------------------------------------------------------- input / actions

        void OnInputKey(KeyDownEvent e)
        {
            if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && !e.shiftKey)
            {
                e.StopPropagation();
                e.StopImmediatePropagation();
                SendFromInput();
            }
        }

        void SendFromInput()
        {
            var text = _input?.value?.Trim() ?? "";
            var atts = _attachments.Count > 0 ? new List<string>(_attachments) : null;
            if (text.Length == 0 && atts == null) return;
            _input.value = "";

            string send = text;
            if (atts != null)
            {
                string note = "[The user attached these files — read/view them before answering: " + string.Join("  |  ", atts) + "]";
                send = text.Length > 0 ? text + "\n\n" + note : note;
                _attachments.Clear();
                RefreshAttachments();
            }
            string display = text.Length > 0 ? text : "(sent " + (atts?.Count ?? 0) + " attachment(s))";
            StartTurn(send, display, atts);
        }

        void SendPrompt(string prompt) => StartTurn(prompt, prompt, null);

        void StartTurn(string sendText, string displayText, List<string> previews)
        {
            if (string.IsNullOrEmpty(sendText) || _running) return;
            if (string.IsNullOrEmpty(_claudePath))
            {
                AddLine("Set the claude path in Settings first.", "error-line");
                ToggleSettings(true);
                return;
            }

            _lastUserPrompt = sendText;
            AddUserMessage(displayText);
            if (previews != null)
                foreach (var a in previews)
                {
                    var card = _md.BuildAssetCard(a, IsImage(a) ? "image" : "model");
                    if (card != null) _transcript.Add(card);
                }
            BeginAssistantTurn();
            SetFollow(true);

            var cfg = new ClaudeChatConfig
            {
                ClaudePath = _claudePath,
                WorkingDirectory = _workingDir,
                PermissionMode = PermModes[Mathf.Clamp(_permIndex, 0, PermModes.Length - 1)],
                Model = ModelValues[Mathf.Clamp(_modelIndex, 0, ModelValues.Length - 1)],
                Effort = EffortValues[Mathf.Clamp(_effortIndex, 0, EffortValues.Length - 1)],
                IncludePartial = _streamTokens,
                ExtraArgs = _extraArgs,
                ClearApiKey = _clearApiKey,
                AppendSystemPrompt = _teachWidgets ? WidgetSpec : "",
            };

            if (!_driver.TryStartTurn(sendText, _sessionId, cfg, out var err))
            {
                AddLine(err, "error-line");
                HideThinking();
                return;
            }
            _running = true;
            UpdateRunningUI();
        }

        void StopTurn()
        {
            _driver.Stop();
            _running = false;
            AddSystem("(stopped)");
            HideThinking();
            UpdateRunningUI();
        }

        void NewChat()
        {
            if (_running) { _driver.Stop(); _running = false; }
            _transcript.Clear();
            _sessionId = null; _sessionCost = 0;
            _turnBody = null; _toolFold = null; _streamLabel = null; _thinking = null;
            _attachments.Clear(); RefreshAttachments();
            UpdateStatus(); UpdateBudget(); UpdateRunningUI();
        }

        // ---------------------------------------------------------------- attachments & capture

        static bool IsImage(string p)
        {
            string e = Path.GetExtension(p ?? "").ToLowerInvariant();
            return e == ".png" || e == ".jpg" || e == ".jpeg" || e == ".tga" || e == ".psd" || e == ".gif" || e == ".bmp";
        }

        void AttachFile()
        {
            var p = EditorUtility.OpenFilePanel("Attach a file for Claude", "", "");
            if (!string.IsNullOrEmpty(p)) { AddAttachment(p); RefreshAttachments(); }
        }

        void AddAttachment(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string full;
            try { full = Path.GetFullPath(path); } catch { full = path; }
            full = full.Replace('\\', '/');
            string staged = StageIfOutside(full).Replace('\\', '/');
            if (!_attachments.Contains(staged)) _attachments.Add(staged);
        }

        // Files outside the working dir can't be read by Claude under restrictive permission
        // modes (headless can't approve a read outside cwd). Copy such files into a folder INSIDE
        // the working dir so they're always readable; files already inside cwd are used as-is.
        string StageIfOutside(string full)
        {
            try
            {
                string wd = string.IsNullOrEmpty(_workingDir) ? "" : Path.GetFullPath(_workingDir).Replace('\\', '/').TrimEnd('/');
                if (wd.Length > 0 && full.StartsWith(wd + "/", StringComparison.OrdinalIgnoreCase)) return full;
                if (!File.Exists(full)) return full; // a directory or missing path — leave as-is
                string dest = UniquePath(Path.Combine(CaptureDir(), Path.GetFileName(full)));
                File.Copy(full, dest, false);
                return dest;
            }
            catch { return full; }
        }

        string CaptureDir()
        {
            string baseDir = string.IsNullOrEmpty(_workingDir) ? Path.GetTempPath() : _workingDir;
            string d = Path.Combine(baseDir, ".sims4-claude-chat");
            Directory.CreateDirectory(d);
            return d;
        }

        static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path), name = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
            for (int i = 1; i < 1000; i++)
            {
                var p = Path.Combine(dir ?? "", name + "_" + i + ext);
                if (!File.Exists(p)) return p;
            }
            return path;
        }

        void RefreshAttachments()
        {
            if (_attachRow == null) return;
            _attachRow.Clear();
            foreach (var a in _attachments)
            {
                var path = a;
                var chip = new VisualElement(); chip.AddToClassList("attach-chip");
                chip.Add(new Label((IsImage(path) ? "🖼 " : "📄 ") + Path.GetFileName(path)));
                var x = new Button(() => { _attachments.Remove(path); RefreshAttachments(); }) { text = "✕" };
                x.AddToClassList("attach-x");
                chip.Add(x);
                _attachRow.Add(chip);
            }
            _attachRow.style.display = _attachments.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnDragPerform(DragPerformEvent e)
        {
            DragAndDrop.AcceptDrag();
            foreach (var p in DragAndDrop.paths) AddAttachment(p); // AddAttachment normalizes to absolute + dedups
            foreach (var o in DragAndDrop.objectReferences)
            {
                var ap = AssetDatabase.GetAssetPath(o);
                if (!string.IsNullOrEmpty(ap)) AddAttachment(ap);
            }
            RefreshAttachments();
        }

        void CaptureAndAttach(bool scene)
        {
            string path = scene ? CaptureSceneView(out string err) : CaptureGameCamera(out err);
            if (!string.IsNullOrEmpty(path))
            {
                AddAttachment(path);
                RefreshAttachments();
                AddSystem("captured " + Path.GetFileName(path) + " (approx view res)");
            }
            else AddLine("Capture failed: " + (err ?? "unknown"), "error-line");
        }

        string CaptureSceneView(out string error)
        {
            error = null;
            var sv = SceneView.lastActiveSceneView;
            if (sv == null || sv.camera == null) { error = "No active Scene view — open/focus one."; return null; }
            return CaptureCamera(sv.camera, "scene", out error);
        }

        string CaptureGameCamera(out string error)
        {
            error = null;
            var cam = Camera.main;
            if (cam == null)
                foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (c.isActiveAndEnabled && c.cameraType == CameraType.Game) { cam = c; break; }
            if (cam == null) { error = "No active game camera found (renders best in Play mode)."; return null; }
            return CaptureCamera(cam, "game", out error);
        }

        string CaptureCamera(Camera cam, string label, out string error)
        {
            error = null;
            if (cam == null) { error = "No camera."; return null; }
            int w = Mathf.Clamp(cam.pixelWidth, 1, 8192);
            int h = Mathf.Clamp(cam.pixelHeight, 1, 8192);
            if (w < 2 || h < 2) { error = "Camera has no render size."; return null; }

            RenderTexture rt = null; Texture2D tex = null;
            var prevActive = RenderTexture.active; var prevTarget = cam.targetTexture;
            try
            {
                // HDRP renders linear; read with Linear so the encoded PNG isn't gamma-wrong.
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

                var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
                if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req))
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);    // SRP/HDRP path (built-in cam.Render() comes out black here)
                else if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
                    { cam.targetTexture = rt; cam.Render(); }                              // legacy built-in only
                else { error = "This camera can't be captured off-screen under the active render pipeline (try Game capture in Play mode, or attach a manual screenshot)."; return null; }

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                var png = tex.EncodeToPNG();
                string dest = UniquePath(Path.Combine(CaptureDir(), label + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png"));
                File.WriteAllBytes(dest, png);
                return dest;
            }
            catch (Exception ex) { error = ex.Message; return null; }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            }
        }

        void ShowHistoryMenu()
        {
            var menu = new GenericMenu();
            var sessions = ClaudeSessionStore.ListSessions(_workingDir, 30);
            if (sessions.Count == 0)
                menu.AddDisabledItem(new GUIContent("(no past sessions in this folder)"));
            else
                foreach (var s in sessions)
                {
                    string label = $"{s.Modified:MM-dd HH:mm}   {s.Title}".Replace("/", "∕");
                    var captured = s;
                    menu.AddItem(new GUIContent(label), s.Id == _sessionId, () => ResumeSession(captured));
                }
            menu.ShowAsContext();
        }

        void ResumeSession(ClaudeSessionStore.SessionInfo s)
        {
            if (_running) { _driver.Stop(); _running = false; }
            _transcript.Clear();
            _turnBody = null; _toolFold = null; _streamLabel = null; _thinking = null;
            _sessionId = s.Id; _sessionCost = 0; _lastUserPrompt = null;

            foreach (var it in ClaudeSessionStore.LoadTranscript(s.Path, 200))
            {
                switch (it.Kind)
                {
                    case "user": AddUserMessage(it.Text); _lastUserPrompt = it.Text; break;
                    case "assistant":
                        var turn = new VisualElement(); turn.AddToClassList("msg-claude");
                        var head = new VisualElement(); head.AddToClassList("msg-head");
                        head.Add(Tag("◈ claude", "role role--claude")); turn.Add(head);
                        turn.Add(_md.Build(it.Text));
                        _transcript.Add(turn);
                        break;
                    case "tool": AddSystem("🔧 " + it.ToolName); break;
                }
            }
            AddSystem($"(resumed {Short(s.Id)} — continue typing; Claude has the full context)");
            UpdateStatus(); UpdateBudget();
            SetFollow(true);
        }

        // ---------------------------------------------------------------- scroll / status

        void SetFollow(bool follow)
        {
            _followBottom = follow;
            if (_jumpPill != null) _jumpPill.style.display = follow ? DisplayStyle.None : DisplayStyle.Flex;
            if (follow) ToBottom();
        }

        void ToBottom()
        {
            if (_transcript == null || _scrollPending) return; // coalesce: at most one pending scroll
            _scrollPending = true;
            _transcript.schedule.Execute(() =>
            {
                _scrollPending = false;
                var vs = _transcript.verticalScroller;
                if (vs != null) vs.value = vs.highValue > 0 ? vs.highValue : 0;
            });
        }

        void ScheduleFollowCheck()
        {
            if (_transcript == null) return;
            _transcript.schedule.Execute(() =>
            {
                var vs = _transcript.verticalScroller;
                bool atBottom = vs == null || vs.highValue <= 0 || vs.value >= vs.highValue - 6f;
                SetFollow(atBottom);
            }).StartingIn(20);
        }

        void UpdateRunningUI()
        {
            if (_stopBtn != null) _stopBtn.SetEnabled(_running);
            if (_sendBtn != null) _sendBtn.SetEnabled(!_running);
            UpdateStatus();
        }

        void UpdateStatus()
        {
            if (_statusLabel == null) return;
            _statusLabel.text = _running ? "● working…" : "idle";
        }

        void UpdateBudget()
        {
            if (_chatCostLabel == null) return;
            _chatCostLabel.text = $"chat ≈${_sessionCost:0.000}";
            _monthCostLabel.text = _monthlyBudget > 0 ? $"mo ≈${_monthSpent:0.00}/${_monthlyBudget:0}" : $"mo ≈${_monthSpent:0.00}";
            float frac = _monthlyBudget > 0 ? Mathf.Clamp01((float)(_monthSpent / _monthlyBudget)) : 0f;
            if (_budgetFill != null) _budgetFill.style.width = Length.Percent(frac * 100f);
        }

        // ---------------------------------------------------------------- budget calc

        void RollMonthIfNeeded()
        {
            string key = DateTime.Now.ToString("yyyy-MM");
            if (_monthKey != key) { _monthKey = key; _monthSpent = 0; }
        }

        void AccrueCost(double c)
        {
            RollMonthIfNeeded();
            if (c > 0) { _monthSpent += c; _sessionCost += c; SavePrefs(); }
            UpdateBudget();
        }

        // ---------------------------------------------------------------- helpers

        void ToggleSettings(bool show)
        {
            if (_settingsPanel != null) _settingsPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static StyleSheet FindStyleSheet()
        {
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("Sims4ClaudeChat t:StyleSheet"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (ss != null) return ss;
                }
            }
            catch { /* ignore */ }
            return null;
        }

        static string Short(string id) => string.IsNullOrEmpty(id) ? "?" : (id.Length <= 8 ? id : id.Substring(0, 8));

        static string DescribeAuth(string src) => string.IsNullOrEmpty(src) || src == "none" ? "subscription/login" : src;

        static string AutoDetectClaude()
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c where claude")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    if (!string.IsNullOrWhiteSpace(outp))
                    {
                        var found = new List<string>();
                        foreach (var raw in outp.Split('\n'))
                        {
                            var l = raw.Trim();
                            if (l.Length > 0 && File.Exists(l)) found.Add(l);
                        }
                        foreach (var pref in new[] { ".exe", ".cmd", ".bat" })
                        {
                            var hit = found.Find(f => f.ToLowerInvariant().EndsWith(pref));
                            if (hit != null) return hit;
                        }
                        if (found.Count > 0) return found[0];
                    }
                }
            }
            catch { /* probe instead */ }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            foreach (var c in new[]
            {
                Path.Combine(home, ".local", "bin", "claude.exe"),
                Path.Combine(appdata, "npm", "claude.cmd"),
                Path.Combine(appdata, "npm", "claude.exe"),
            }) if (File.Exists(c)) return c;
            return "";
        }

        static string FindRepoRoot()
        {
            try
            {
                var dir = new DirectoryInfo(Application.dataPath);
                while (dir != null)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, ".git"))) return dir.FullName;
                    dir = dir.Parent;
                }
            }
            catch { /* ignore */ }
            try { return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath; }
            catch { return Application.dataPath; }
        }

        void LoadPrefs()
        {
            _claudePath = EditorPrefs.GetString(PrefPrefix + "ClaudePath", "");
            _workingDir = EditorPrefs.GetString(PrefPrefix + "WorkingDir", "");
            _permIndex = EditorPrefs.GetInt(PrefPrefix + "PermIndex", 0);
            _modelIndex = EditorPrefs.GetInt(PrefPrefix + "ModelIndex", 0);
            _effortIndex = EditorPrefs.GetInt(PrefPrefix + "EffortIndex", 0);
            _streamTokens = EditorPrefs.GetBool(PrefPrefix + "StreamTokens", true);
            _monthlyBudget = EditorPrefs.GetFloat(PrefPrefix + "MonthlyBudget", 0f);
            _extraArgs = EditorPrefs.GetString(PrefPrefix + "ExtraArgs", "");
            _clearApiKey = EditorPrefs.GetBool(PrefPrefix + "ClearApiKey", true);
            _showToolResults = EditorPrefs.GetBool(PrefPrefix + "ShowToolResults", false);
            _teachWidgets = EditorPrefs.GetBool(PrefPrefix + "TeachWidgets", true);
            _monthKey = EditorPrefs.GetString(PrefPrefix + "MonthKey", "");
            _monthSpent = EditorPrefs.GetFloat(PrefPrefix + "MonthSpent", 0f);
        }

        void SavePrefs()
        {
            EditorPrefs.SetString(PrefPrefix + "ClaudePath", _claudePath ?? "");
            EditorPrefs.SetString(PrefPrefix + "WorkingDir", _workingDir ?? "");
            EditorPrefs.SetInt(PrefPrefix + "PermIndex", _permIndex);
            EditorPrefs.SetInt(PrefPrefix + "ModelIndex", _modelIndex);
            EditorPrefs.SetInt(PrefPrefix + "EffortIndex", _effortIndex);
            EditorPrefs.SetBool(PrefPrefix + "StreamTokens", _streamTokens);
            EditorPrefs.SetFloat(PrefPrefix + "MonthlyBudget", _monthlyBudget);
            EditorPrefs.SetString(PrefPrefix + "ExtraArgs", _extraArgs ?? "");
            EditorPrefs.SetBool(PrefPrefix + "ClearApiKey", _clearApiKey);
            EditorPrefs.SetBool(PrefPrefix + "ShowToolResults", _showToolResults);
            EditorPrefs.SetBool(PrefPrefix + "TeachWidgets", _teachWidgets);
            EditorPrefs.SetString(PrefPrefix + "MonthKey", _monthKey ?? "");
            EditorPrefs.SetFloat(PrefPrefix + "MonthSpent", (float)_monthSpent);
        }
    }
}
