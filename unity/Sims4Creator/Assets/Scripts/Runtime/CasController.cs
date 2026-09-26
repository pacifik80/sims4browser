using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// Runtime "Create A Sim" panel — an in-game (Play-mode) editor for every parameter the built
    /// <see cref="Sims4Character"/> exposes: morph sliders (grouped head→toes by area/category, paired
    /// bidirectional), base skin, skin-detail normal + strength, base tone, detail overlays, eye colour,
    /// the three body-mesh slots, and the <see cref="Sims4IdleSwitcher"/> animation set. Hair / clothing /
    /// accessories appear as wired-in "coming soon" tabs so they drop in once their data pipeline exists.
    ///
    /// Rendered with IMGUI (OnGUI) on purpose: it needs no UI package, no fonts, no Canvas/EventSystem,
    /// and draws correctly over HDRP in Play mode. Utilitarian look, complete function — reskin to uGUI
    /// later if a Sims-grade visual is wanted. Everything here is a thin driver over the public
    /// Sims4Character API, so changes stay live and correct.
    /// </summary>
    [AddComponentMenu("Sims4 Creator/CAS UI Controller")]
    [DisallowMultipleComponent]
    public sealed class CasController : MonoBehaviour
    {
        [Tooltip("Auto-found in the scene if left empty.")]
        public Sims4Character character;
        public Sims4IdleSwitcher idle;
        public CasCameraRig rig;

        [Header("Layout — BODY panel left, STYLE panel right")]
        public float panelWidth = 420f;      // left panel (body modification + fine-tune)
        public float rightPanelWidth = 420f; // right panel (skin/hair/clothing/etc)
        public int fontSize = 14;
        public KeyCode toggleKey = KeyCode.Tab;
        public bool visible = true;

        // Motion: 0 Auto (idle only on the full-body shot, still when zoomed — like CAS), 1 Idle, 2 Still.
        [Range(0, 2)] public int animMode;
        private static readonly string[] MotionNames = { "Auto", "Idle", "Still" };
        private Animation _anim;
        private readonly List<string> _idleClips = new();
        private int _idleIdx;
        [Header("Idle variety + still pose")]
        [Tooltip("In Idle/Full, cross-fade to a different idle every few seconds (like a Sim's fidgets).")]
        public bool shuffleIdles = true;
        public float shuffleSeconds = 12f;
        [Tooltip("How far to bring the arms DOWN for the Still pose (deg) — the bind A-pose has them spread.")]
        [Range(0f, 70f)] public float stillArmsDown = 40f;
        [Tooltip("On mid shots (Cowboy/Chest) keep a calm face alive (mostly straight + blink). The Face " +
                 "close-up always freezes the face for detail editing.")]
        public bool faceLifeInStill = true;
        private float _shuffleTimer;
        private string _faceCalm;   // calm face idle mixed onto the head while the body is still
        private Transform _headBone, _lUpperArm, _rUpperArm;
        private string _mixedClip;  // face clip currently head-mixed (null = none)
        private bool _bodyStillApplied;
        private int _faceMode = -1; // -1 uninit | 0 full-idle (face via body clip) | 1 alive-mixed | 2 frozen
        private string _curClip;    // full idle clip currently playing (body-idle mode)

        // Captured BIND pose (symmetric, centred rest) — "Still" restores this so subtle/face editing is
        // done on the neutral reference pose, not a leg-weighted idle frame.
        private Transform[] _bindT;
        private Vector3[] _bindP;
        private Quaternion[] _bindR;

        // Split UI: LEFT = body modification + fine-tune; RIGHT = styling (skin/hair/clothes/etc).
        private static readonly string[] TabsL = { "Face", "Body", "Meshes", "Anim" };
        private static readonly string[] TabsR = { "Skin", "Hair", "Clothing", "More" };
        private int _tabL, _tabR;
        private Vector2 _scrollL, _scrollR;
        private float _activePanelWidth = 420f; // width of the panel currently being drawn (for Grid columns)
        private string _filter = string.Empty;

        // Character templates (save/load the full designed look — see Sims4Character.CaptureTemplate/ApplyTemplate).
        private string _templateName = string.Empty;
        private List<string> _templatePaths;
        private string _templateMsg = string.Empty;
        private readonly Dictionary<string, bool> _open = new();

        // ANIMATIONS dropdown (full-height view): collapsible category tree of every clip on the character.
        private sealed class AnimCat { public string name; public readonly List<string> clips = new(); }
        private readonly List<AnimCat> _animTree = new();
        private bool _animTreeOpen;
        private Vector2 _animTreeScroll;
        private Rect _animTreeRect;
        private string _customClip; // non-null: the tree picked a specific clip (overrides idle shuffle)
        // Diagnostics: per-layer visibility overrides + occlusion toggle (see the "More" tab).
        private bool _diagBody = true, _diagClothes = true, _diagHair = true, _diagOcclusion = true;

        // ---- morph grouping cache (rebuilt when the axis count changes) ----
        private sealed class CatGroup { public string cat; public int order; public readonly List<int> axes = new(); }
        private sealed class AreaGroup { public string area; public int order; public readonly List<CatGroup> cats = new(); }
        private readonly List<AreaGroup> _groups = new();
        private int _cachedAxisCount = -1;
        private static readonly string[] AreaOrder = { "Head", "Torso", "Arms", "Legs", "Other" };

        // ---- styles / textures (lazy) ----
        private bool _stylesReady;
        private GUIStyle _panelLabel, _title, _sectionHead, _catHead, _btn, _btnActive, _slLabel, _tabStyle, _dim, _gridBtn;
        private Texture2D _bg, _rowA, _rowB, _accentTex;

        private void Awake()
        {
            if (character == null) character = FindFirstObjectByType<Sims4Character>();
            if (idle == null) idle = FindFirstObjectByType<Sims4IdleSwitcher>();
            if (rig == null) rig = FindFirstObjectByType<CasCameraRig>();

            // CAS drives motion itself (idle-only, still-when-zoomed), so silence BOTH built-in drivers:
            // the auto-cycling clip switcher (which also carries walk/run/dance) AND the procedural idle
            // (used when a character has no real clips) — otherwise "Still" wouldn't actually freeze.
            if (idle != null) { idle.autoCycle = false; idle.enabled = false; }
            if (character != null)
            {
                var proc = character.GetComponent<Sims4IdleAnimator>();
                if (proc != null) proc.enabled = false;
                _anim = character.GetComponentInChildren<Animation>();
                CaptureBindPose();          // bones are at bind now (before any clip has sampled)
                if (_anim != null) { _anim.Stop(); _anim.playAutomatically = false; } // no rogue autoplay
            }
            BuildIdleClips();
        }

        // Snapshot every bone's local transform (the character root itself is excluded so the turntable
        // rotation is never captured/clobbered). Called in Awake, before any animation sampling.
        private void CaptureBindPose()
        {
            var all = character.GetComponentsInChildren<Transform>(true);
            var list = new List<Transform>(all.Length);
            foreach (var t in all) if (t != character.transform) list.Add(t);
            _bindT = list.ToArray();
            _bindP = new Vector3[_bindT.Length];
            _bindR = new Quaternion[_bindT.Length];
            for (var i = 0; i < _bindT.Length; i++)
            {
                _bindP[i] = _bindT[i].localPosition;
                _bindR[i] = _bindT[i].localRotation;
                switch (_bindT[i].name)
                {
                    case "b__Head__": _headBone = _bindT[i]; break;
                    case "b__L_UpperArm__": _lUpperArm = _bindT[i]; break;
                    case "b__R_UpperArm__": _rUpperArm = _bindT[i]; break;
                }
            }
        }

        private void RestoreBindPose()
        {
            if (_bindT == null) return;
            for (var i = 0; i < _bindT.Length; i++)
            {
                if (_bindT[i] == null) continue;
                // Rotation + position only — our clips animate those, never scale. localScale carries the
                // BONE-morph sizing (breast/head/eye); leave it so "Still" doesn't wipe those sliders.
                _bindT[i].localPosition = _bindP[i];
                _bindT[i].localRotation = _bindR[i];
            }
        }

        private void BuildIdleClips()
        {
            _idleClips.Clear();
            var names = idle != null && idle.clipNames != null ? idle.clipNames : System.Array.Empty<string>();
            foreach (var n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                var low = n.ToLowerInvariant();
                // STANDING idles ONLY. The ambient edit-time shuffle must NEVER pick an activity clip:
                // a2o_* object-interaction clips and anything sit/sleep-flavoured contain "idle" in their
                // names too (a2o_bed_sleep_idle_breathe_x put her to sleep mid-edit). Everything beyond
                // plain standing idles is reachable ONLY via the explicit ANIMATIONS tree.
                var isStandingIdle = (low.StartsWith("a_idle") || low.Contains("waiting")) &&
                                     !low.StartsWith("a2o_") &&
                                     !low.Contains("sit") && !low.Contains("seated") &&
                                     !low.Contains("sleep") && !low.Contains("nap") && !low.Contains("dance");
                if (isStandingIdle) _idleClips.Add(n);
            }
            // Face-life clip: a CALM idle (mostly-straight head + natural blink), NOT a look-around (too busy
            // when looped). Prefer "waiting"; else any non-look idle; else the first.
            _faceCalm = _idleClips.Find(c => c.ToLowerInvariant().Contains("waiting"));
            if (string.IsNullOrEmpty(_faceCalm)) _faceCalm = _idleClips.Find(c => !c.ToLowerInvariant().Contains("look"));
            if (string.IsNullOrEmpty(_faceCalm) && _idleClips.Count > 0) _faceCalm = _idleClips[0];

            BuildAnimTree(names);
        }

        // Categorize EVERY clip on the character (not just idles) for the ANIMATIONS dropdown tree.
        private void BuildAnimTree(IReadOnlyList<string> names)
        {
            _animTree.Clear();
            AnimCat Cat(string name)
            {
                var c = _animTree.Find(x => x.name == name);
                if (c == null) { c = new AnimCat { name = name }; _animTree.Add(c); }
                return c;
            }
            foreach (var n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                var low = n.ToLowerInvariant();
                string cat;
                // Order matters: "situp" must beat "sit"; "seated" idles are Sitting, not Idle.
                if (low.Contains("situp") || low.Contains("yoga") || low.Contains("stretch") || low.Contains("workout") ||
                    low.Contains("exercise") || low.Contains("pushup") || low.Contains("punchingbag")) cat = "Exercise";
                else if (low.Contains("sit") || low.Contains("seated") || low.Contains("chair")) cat = "Sitting";
                else if (low.Contains("sleep") || low.Contains("nap") || low.Contains("lie") || low.Contains("bed")) cat = "Sleeping";
                else if (low.Contains("dance")) cat = "Dance";
                else if (low.Contains("phone") || low.Contains("drink") || low.Contains("book") || low.Contains("read") ||
                         low.Contains("eat") || low.Contains("watch")) cat = "Activity";
                else if (low.Contains("react") || low.Contains("cheer") || low.Contains("laugh") || low.Contains("cry") ||
                         low.Contains("wave") || low.Contains("angry") || low.Contains("flirt")) cat = "Expressive";
                else if (low.Contains("walk") || low.Contains("run") || low.Contains("jog") || low.Contains("loco")) cat = "Locomotion";
                else if (low.Contains("idle") || low.Contains("waiting") || low.Contains("stand")) cat = "Idle";
                else cat = "Other";
                Cat(cat).clips.Add(n);
            }
            // Stable, sensible order.
            var order = new[] { "Idle", "Sitting", "Sleeping", "Dance", "Exercise", "Activity", "Expressive", "Locomotion", "Other" };
            _animTree.Sort((a, b) => System.Array.IndexOf(order, a.name).CompareTo(System.Array.IndexOf(order, b.name)));
            foreach (var c in _animTree) c.clips.Sort(string.CompareOrdinal);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;

            // Experiment clips (ANIMATIONS tree) are Full-shot only: zooming in to EDIT stops them —
            // they must never mix into the editing shots' still/calm behaviour.
            if (_customClip != null && rig != null && rig.shot != 0) _customClip = null;

            // Idle variety — periodically pick a different idle (only matters while actually idling).
            if (shuffleIdles && _idleClips.Count > 1)
            {
                _shuffleTimer += Time.deltaTime;
                if (_shuffleTimer >= Mathf.Max(3f, shuffleSeconds))
                {
                    _shuffleTimer = 0f;
                    var next = _idleIdx;
                    for (var g = 0; g < 8 && next == _idleIdx; g++) next = UnityEngine.Random.Range(0, _idleClips.Count);
                    _idleIdx = next;
                }
            }
            ApplyMotion();
        }

        // Per-frame motion state machine. Three regimes:
        //   • BODY-IDLE  (Idle mode, or Auto on the Full shot): one full idle clip animates body + face.
        //   • BODY-STILL + FACE-ALIVE (Auto on Cowboy/Chest): body frozen in the symmetric arms-down rest
        //     pose; a CALM face idle mixed onto the head only keeps a mostly-straight look + natural blink.
        //   • FROZEN (Still mode, or the Face close-up on any mode): body AND face fully still for editing.
        // Change-detection (_bodyStillApplied / _faceMode / _curClip) means the heavy work runs once per
        // state change, not every frame.
        private void ApplyMotion()
        {
            // A tree-picked clip must play even when NO clip matched the idle-name filter.
            if (_anim == null || (_customClip == null && _idleClips.Count == 0)) return;
            // An explicit tree pick plays regardless of Motion mode (it can only exist on the Full shot —
            // Update clears it the moment the view changes, so editing shots are never affected).
            var bodyIdle = _customClip != null || animMode == 1 || (animMode == 0 && (rig == null || rig.shot == 0));

            if (bodyIdle)
            {
                _bodyStillApplied = false;
                if (_faceMode != 0) { UnmixFace(); _faceMode = 0; }
                // A clip picked from the ANIMATIONS tree overrides the idle shuffle until cleared.
                if (_customClip != null && _anim[_customClip] == null) _customClip = null; // stale pick: drop it
                var clip = _customClip ?? (_idleClips.Count > 0 ? _idleClips[Mathf.Clamp(_idleIdx, 0, _idleClips.Count - 1)] : null);
                if (clip != null && clip != _curClip && _anim[clip] != null)
                {
                    _anim.enabled = true;
                    _anim[clip].speed = 1f;
                    _anim[clip].wrapMode = WrapMode.Loop; // sit/sleep/activity clips must hold, not pop back
                    _anim.CrossFade(clip, 0.25f);
                    _curClip = clip;
                }
                return;
            }

            // BODY STILL — freeze the body once in the symmetric, centred, arms-down rest pose.
            if (!_bodyStillApplied)
            {
                _anim.Stop(); _anim.enabled = false;
                RestoreBindPose();
                if (rig != null) rig.ResetRotation();
                ApplyArmsDown();
                _bodyStillApplied = true; _curClip = null; _faceMode = -1;
            }

            // FACE — frozen on the close-up / in Still mode; otherwise a calm mixed idle keeps it alive.
            var faceFrozen = animMode == 2 || (rig != null && rig.shot == 3) || !faceLifeInStill;
            if (faceFrozen)
            {
                if (_faceMode != 2) { UnmixFace(); RestoreHeadBind(); _anim.enabled = false; _faceMode = 2; }
            }
            else if (_faceMode != 1)
            {
                MixFace(_faceCalm);
                _faceMode = 1;
            }
        }

        // Bring the arms down symmetrically from the A-pose bind. Rotating each upper-arm about the Sim's
        // OWN forward axis adducts it in the frontal plane (correct even after the Sim is turned); opposite
        // signs L/R = symmetric by construction. Children (forearm/hand) follow.
        private void ApplyArmsDown()
        {
            if (stillArmsDown <= 0.01f || character == null) return;
            var axis = character.transform.forward; // Unity is left-handed: −L / +R brings both arms DOWN.
            if (_lUpperArm != null) _lUpperArm.Rotate(axis, -stillArmsDown, Space.World);
            if (_rUpperArm != null) _rUpperArm.Rotate(axis, stillArmsDown, Space.World);
        }

        // Play one face clip restricted to the head+face bones (leaves the still body untouched). Tracks
        // the mixed clip so RemoveMixingTransform is only ever called on a transform we added.
        private void MixFace(string clip)
        {
            if (string.IsNullOrEmpty(clip) || _headBone == null || _anim == null || _anim[clip] == null) return;
            if (_mixedClip == clip) return;
            var st = _anim[clip];
            st.AddMixingTransform(_headBone, true);
            st.wrapMode = WrapMode.Loop; st.speed = 1f;
            _anim.enabled = true;
            _anim.Play(clip);                                 // hard switch — stops any prior state cleanly
            if (!string.IsNullOrEmpty(_mixedClip) && _mixedClip != clip && _anim[_mixedClip] != null)
                _anim[_mixedClip].RemoveMixingTransform(_headBone); // prior clip now stopped — safe to unmix
            _mixedClip = clip;
        }

        private void UnmixFace()
        {
            if (string.IsNullOrEmpty(_mixedClip)) return; // only remove what we added
            if (_headBone != null && _anim != null && _anim[_mixedClip] != null)
                _anim[_mixedClip].RemoveMixingTransform(_headBone);
            _mixedClip = null;
        }

        // Restore just the head + face bones to bind (used when the face freezes after a mixed idle moved it).
        private void RestoreHeadBind()
        {
            if (_bindT == null || _headBone == null) return;
            for (var i = 0; i < _bindT.Length; i++)
            {
                if (_bindT[i] == null || !_bindT[i].IsChildOf(_headBone)) continue; // IsChildOf includes self
                _bindT[i].localPosition = _bindP[i];
                _bindT[i].localRotation = _bindR[i];
            }
        }

        /// <summary>True when the cursor is over EITHER panel or the animations dropdown — the orbit
        /// camera checks this to ignore input.</summary>
        public bool PointerOverPanel
        {
            get
            {
                if (!visible) return false;
                var x = Input.mousePosition.x;
                var y = Input.mousePosition.y;
                if (y < 0 || y > Screen.height) return false;
                var wL = Mathf.Min(panelWidth, Screen.width * 0.45f);      // same clamping as OnGUI draws
                var wR = Mathf.Min(rightPanelWidth, Screen.width * 0.45f);
                if (x >= 0 && x < wL) return true;                         // left (BODY) panel
                if (x >= Screen.width - wR && x <= Screen.width) return true; // right (STYLE) panel
                if (_animTreeRect.width > 0)
                {
                    // _animTreeRect is in GUI space (y down); mousePosition is y-up.
                    var guiY = Screen.height - y;
                    if (_animTreeRect.Contains(new Vector2(x, guiY))) return true;
                }
                return false;
            }
        }

        // -------------------------------------------------------------------------------------------
        private void OnGUI()
        {
            EnsureStyles();

            if (!visible)
            {
                if (GUI.Button(new Rect(8, 8, 150, 30), "Show CAS  (Tab)", _btn)) visible = true;
                return;
            }

            var wL = Mathf.Min(panelWidth, Screen.width * 0.45f);
            var wR = Mathf.Min(rightPanelWidth, Screen.width * 0.45f);

            // ---- LEFT: BODY — modification + fine-tune (morphs, body meshes, posing) ----
            GUI.DrawTexture(new Rect(0, 0, wL, Screen.height), _bg, ScaleMode.StretchToFill);
            GUILayout.BeginArea(new Rect(12, 10, wL - 24, Screen.height - 20));
            _activePanelWidth = wL;

            GUILayout.BeginHorizontal();
            GUILayout.Label("BODY", _title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Hide", _btn, GUILayout.Width(52))) visible = false;
            GUILayout.EndHorizontal();

            if (character == null)
            {
                GUILayout.Space(8);
                GUILayout.Label("No Sims4Character found in the scene.\nBuild one via\nSims4 Creator ▸ Character ▸ Create Female Character.", _dim);
                GUILayout.EndArea();
                return;
            }

            // VIEW — shots + turntable (global, drives the camera rig)
            if (rig != null)
            {
                GUILayout.Label("VIEW", _sectionHead);
                var s = GUILayout.Toolbar(rig.shot, CasCameraRig.ShotNames, _tabStyle);
                if (s != rig.shot) rig.SetShot(s);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("< Turn", _btn)) rig.RotateCharacter(-30f);
                if (GUILayout.Button("Turn >", _btn)) rig.RotateCharacter(30f);
                if (GUILayout.Button("Front", _btn)) rig.ResetRotation();
                GUILayout.EndHorizontal();
            }

            // Motion — Auto / Idle / Still
            GUILayout.BeginHorizontal();
            GUILayout.Label("Motion", _slLabel, GUILayout.Width(52));
            animMode = GUILayout.Toolbar(Mathf.Clamp(animMode, 0, 2), MotionNames, _tabStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            // filter (morph sliders)
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter", _slLabel, GUILayout.Width(40));
            _filter = GUILayout.TextField(_filter ?? string.Empty, _btn);
            if (GUILayout.Button("x", _btn, GUILayout.Width(26))) _filter = string.Empty;
            GUILayout.EndHorizontal();

            _tabL = GUILayout.Toolbar(_tabL, TabsL, _tabStyle);
            GUILayout.Space(4);

            _scrollL = GUILayout.BeginScrollView(_scrollL, false, true, GUIStyle.none, GUI.skin.verticalScrollbar); // vertical only — never scroll a panel sideways
            switch (_tabL)
            {
                case 0: DrawFacial(); DrawMorphs("Head"); break;
                case 1: DrawMorphs("Torso", "Arms", "Legs", "Other"); break;
                case 2: DrawMeshes(); break;
                default: DrawAnim(); break;
            }
            GUILayout.Space(24);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // ---- RIGHT: STYLE — skin/makeup, hair, clothing, extras ----
            GUI.DrawTexture(new Rect(Screen.width - wR, 0, wR, Screen.height), _bg, ScaleMode.StretchToFill);
            GUILayout.BeginArea(new Rect(Screen.width - wR + 12, 10, wR - 24, Screen.height - 20));
            _activePanelWidth = wR;

            GUILayout.Label("STYLE", _title);
            _tabR = GUILayout.Toolbar(_tabR, TabsR, _tabStyle);
            GUILayout.Space(4);

            _scrollR = GUILayout.BeginScrollView(_scrollR, false, true, GUIStyle.none, GUI.skin.verticalScrollbar); // vertical only — never scroll a panel sideways
            switch (_tabR)
            {
                case 0: DrawSkin(); break;
                case 1: DrawHair(); break;
                case 2: DrawClothing(); break;
                default: DrawMore(); break;
            }
            GUILayout.Space(24);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // ---- CENTER: ANIMATIONS dropdown (full-height shot only) ----
            DrawAnimTree(wL, wR);
        }

        // Collapsible category tree of every clip on the character — visible on the FULL shot so outfits
        // and the body can be tested in varied poses (sit/sleep/dance/...). Picking a clip overrides the
        // idle shuffle until "Default idles" is pressed.
        private void DrawAnimTree(float wL, float wR)
        {
            _animTreeRect = default;
            if (character == null || _anim == null || _animTree.Count == 0) return;
            if (rig != null && rig.shot != 0) return; // full-height view only

            var x = wL + 16f;
            var width = Mathf.Min(300f, Screen.width - wL - wR - 32f);
            if (width < 160f) return; // no room between the panels

            if (!_animTreeOpen)
            {
                _animTreeRect = new Rect(x, 10, width, 34);
                if (GUI.Button(_animTreeRect, "Animations  ▾", _btn)) _animTreeOpen = true;
                return;
            }

            var height = Mathf.Min(Screen.height * 0.7f, 560f);
            _animTreeRect = new Rect(x, 10, width, height);
            GUI.DrawTexture(_animTreeRect, _bg, ScaleMode.StretchToFill);
            GUILayout.BeginArea(new Rect(_animTreeRect.x + 8, _animTreeRect.y + 6, _animTreeRect.width - 16, _animTreeRect.height - 12));

            GUILayout.BeginHorizontal();
            GUILayout.Label("ANIMATIONS", _sectionHead);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("▴", _btn, GUILayout.Width(30))) _animTreeOpen = false;
            GUILayout.EndHorizontal();

            // Explicit play state. Experiment clips play ONLY on this Full shot and never change the
            // Motion mode — zooming in to edit stops them automatically (see Update).
            if (_customClip != null)
            {
                GUILayout.Label("▶ " + Pretty(_customClip), _slLabel);
                if (GUILayout.Button("■ Stop — back to default idles", _btn)) _customClip = null;
            }
            else
            {
                GUILayout.Label("Pick a clip to play it (Full view only; stops when you zoom in to edit).", _dim);
            }

            _animTreeScroll = GUILayout.BeginScrollView(_animTreeScroll, false, true);
            foreach (var cat in _animTree)
            {
                if (!Foldout("animtree/" + cat.name, $"{cat.name} ({cat.clips.Count})", false)) continue;
                foreach (var clip in cat.clips)
                {
                    var active = clip == _customClip;
                    if (GUILayout.Button((active ? "> " : "   ") + Pretty(clip), active ? _btnActive : _btn))
                        _customClip = active ? null : clip; // click again to stop
                }
            }
            GUILayout.Space(8);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---- MORPHS ------------------------------------------------------------------------------
        private void RebuildGroupsIfNeeded()
        {
            var n = character.AxisCount;
            if (n == _cachedAxisCount && (_groups.Count > 0 || n == 0)) return;
            _cachedAxisCount = n;
            _groups.Clear();
            var byArea = new Dictionary<string, AreaGroup>();
            for (var i = 0; i < n; i++)
            {
                var a = character.morphAxes[i];
                if (a == null) continue;
                var areaName = string.IsNullOrEmpty(a.area) ? "Other" : a.area;
                if (!byArea.TryGetValue(areaName, out var ag))
                {
                    ag = new AreaGroup { area = areaName, order = Array.IndexOf(AreaOrder, areaName) };
                    if (ag.order < 0) ag.order = 99;
                    byArea[areaName] = ag;
                    _groups.Add(ag);
                }
                var catName = string.IsNullOrEmpty(a.category) ? areaName : a.category;
                var cg = ag.cats.Find(c => c.cat == catName);
                if (cg == null) { cg = new CatGroup { cat = catName, order = a.order }; ag.cats.Add(cg); }
                cg.order = Mathf.Min(cg.order, a.order);
                cg.axes.Add(i);
            }
            _groups.Sort((x, y) => x.order.CompareTo(y.order));
            foreach (var ag in _groups)
            {
                ag.cats.Sort((x, y) => x.order != y.order ? x.order.CompareTo(y.order) : string.CompareOrdinal(x.cat, y.cat));
                foreach (var cg in ag.cats)
                    cg.axes.Sort((x, y) =>
                    {
                        var ax = character.morphAxes[x]; var ay = character.morphAxes[y];
                        return ax.order != ay.order ? ax.order.CompareTo(ay.order) : string.CompareOrdinal(ax.label, ay.label);
                    });
            }
        }

        // Facial EXPRESSION banks — Emotion + Viseme toggles, each revealing blendable 0..1 sliders that
        // pose the facial bones additively over the idle (Sims4Character.Set*Weight). Manual; no audio.
        private void DrawFacial()
        {
            if (character == null) return;

            GUILayout.Label("EXPRESSION", _sectionHead);

            var emoOn = GUILayout.Toggle(character.emotionEnabled, "  Emotion control", _slLabel);
            if (emoOn != character.emotionEnabled) character.SetEmotionEnabled(emoOn);
            if (character.emotionEnabled)
            {
                for (var i = 0; i < character.EmotionCount; i++)
                    DrawWeightRow(character.GetEmotionName(i), character.GetEmotionWeight(i), i, true);
                if (GUILayout.Button("Reset emotions", _btn)) character.ResetEmotions();
                GUILayout.Space(4);
            }

            var visOn = GUILayout.Toggle(character.visemeEnabled, "  Viseme control (mouth shapes)", _slLabel);
            if (visOn != character.visemeEnabled) character.SetVisemeEnabled(visOn);
            if (character.visemeEnabled)
            {
                for (var i = 0; i < character.VisemeCount; i++)
                    DrawWeightRow(character.GetVisemeName(i), character.GetVisemeWeight(i), i, false);
                if (GUILayout.Button("Reset visemes", _btn)) character.ResetVisemes();
                GUILayout.Space(4);
            }

            if (character.emotionEnabled || character.visemeEnabled)
                GUILayout.Label("Tip: Motion ▸ Still (or the Face close-up) freezes the idle while you pose.", _dim);
            GUILayout.Space(6);
        }

        // One blendable expression slider; isEmotion picks which bank to write.
        private void DrawWeightRow(string label, float cur, int index, bool isEmotion)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _slLabel, GUILayout.Width(panelWidth * 0.42f));
            var v = GUILayout.HorizontalSlider(cur, 0f, 1f, GUILayout.ExpandWidth(true));
            GUILayout.Label(cur.ToString("0.00"), _slLabel, GUILayout.Width(40));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(v - cur) > 0.0005f)
            {
                if (isEmotion) character.SetEmotionWeight(index, v);
                else character.SetVisemeWeight(index, v);
            }
        }

        private void DrawMorphs(params string[] areas)
        {
            RebuildGroupsIfNeeded();
            var filtering = !string.IsNullOrEmpty(_filter);
            var f = filtering ? _filter.Trim().ToLowerInvariant() : null;

            GUILayout.BeginHorizontal();
            GUILayout.Label(filtering ? "Filtered morphs" : "Morphs", _sectionHead);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset shown", _btn, GUILayout.Width(96))) ResetShown(areas, f);
            GUILayout.EndHorizontal();

            var any = false;
            foreach (var areaName in areas)
            {
                var ag = _groups.Find(g => g.area == areaName);
                if (ag == null) continue;
                foreach (var cg in ag.cats)
                {
                    // filter this category's axes first
                    List<int> shown;
                    if (!filtering) shown = cg.axes;
                    else
                    {
                        shown = new List<int>();
                        foreach (var i in cg.axes)
                            if (character.morphAxes[i].label.ToLowerInvariant().Contains(f)) shown.Add(i);
                        if (shown.Count == 0) continue;
                    }
                    any = true;
                    var key = areaName + "/" + cg.cat;
                    var label = areas.Length > 1 ? $"{areaName} · {cg.cat}" : cg.cat;
                    var open = Foldout(key, $"{label}  ({shown.Count})", filtering);
                    if (!open) continue;
                    foreach (var i in shown) AxisSlider(i);
                    GUILayout.Space(2);
                }
            }
            if (!any) GUILayout.Label(filtering ? "No morphs match the filter." : "No morphs in this section.", _dim);
        }

        private void AxisSlider(int i)
        {
            var a = character.morphAxes[i];
            var cur = character.GetAxisValue(i);
            var bidir = a.Bidirectional;

            GUILayout.BeginHorizontal();
            GUILayout.Label(a.label, _slLabel, GUILayout.Width(panelWidth * 0.42f));
            var v = GUILayout.HorizontalSlider(cur, bidir ? -1f : 0f, 1f, GUILayout.ExpandWidth(true));
            GUILayout.Label(cur.ToString("+0.00;-0.00; 0.00"), _slLabel, GUILayout.Width(46));
            if (GUILayout.Button("0", _btn, GUILayout.Width(24))) v = 0f;
            GUILayout.EndHorizontal();

            if (Mathf.Abs(v - cur) > 0.0005f) character.SetAxis(i, v);
        }

        private void ResetShown(string[] areas, string f)
        {
            foreach (var areaName in areas)
            {
                var ag = _groups.Find(g => g.area == areaName);
                if (ag == null) continue;
                foreach (var cg in ag.cats)
                    foreach (var i in cg.axes)
                        if (f == null || character.morphAxes[i].label.ToLowerInvariant().Contains(f))
                            character.SetAxis(i, 0f);
            }
        }

        // ---- SKIN --------------------------------------------------------------------------------
        // MAKEUP — skin-layer slots (14..17) rendered inside the Skin tab via the generic clothing API.
        private void DrawMakeup()
        {
            var any = false;
            for (var slot = Sims4Creator.Sims4Character.FirstMakeupSlot; slot < Sims4Creator.Sims4Character.ClothingSlotCount; slot++)
                if (character.GetClothingCount(slot) > 1) { any = true; break; }
            if (!any) return;
            GUILayout.Space(10);
            GUILayout.Label("Makeup", _sectionHead);
            for (var slot = Sims4Creator.Sims4Character.FirstMakeupSlot; slot < Sims4Creator.Sims4Character.ClothingSlotCount; slot++)
            {
                var list = character.GetClothing(slot);
                if (list == null || list.Count <= 1) continue;
                GUILayout.Space(4);
                GUILayout.Label(Sims4Creator.Sims4Character.ClothingSlotNames[slot], _catHead);
                var labels = Labels(list, c => c.label ?? c.id);
                var sel = Grid(character.GetClothingIndex(slot), labels);
                if (sel != character.GetClothingIndex(slot)) character.SetClothing(slot, sel);
                if (character.GetClothingColorCount(slot) > 0)
                {
                    var item = list[character.GetClothingIndex(slot)];
                    var colorLabels = Labels(item.colors, c => c.label ?? c.id);
                    var csel = Grid(character.GetClothingColorIndex(slot), colorLabels);
                    if (csel != character.GetClothingColorIndex(slot)) character.SetClothingColor(slot, csel);
                }
            }
        }

        private void DrawSkin()
        {
            if (character.baseSkins != null && character.baseSkins.Count > 0)
            {
                GUILayout.Label("Base Skin", _sectionHead);
                var labels = Labels(character.baseSkins, s => s.label ?? s.id);
                var sel = Grid(character.BaseSkinIndex, labels);
                if (sel != character.BaseSkinIndex) character.SetBaseSkin(sel);
            }

            if (character.skinNormals != null && character.skinNormals.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("Skin Detail (Normal)", _sectionHead);
                var labels = Labels(character.skinNormals, s => s.label ?? s.id);
                var sel = Grid(character.SkinNormalIndex, labels);
                if (sel != character.SkinNormalIndex) character.SetSkinNormal(sel);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Strength", _slLabel, GUILayout.Width(70));
                var ns = GUILayout.HorizontalSlider(character.skinNormalScale, 0f, 2f);
                GUILayout.Label(character.skinNormalScale.ToString("0.00"), _slLabel, GUILayout.Width(40));
                GUILayout.EndHorizontal();
                if (Mathf.Abs(ns - character.skinNormalScale) > 0.001f) { character.skinNormalScale = ns; character.ApplySkinNormal(); }
            }

            GUILayout.Space(8);
            GUILayout.Label("Skin Gloss", _sectionHead);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Smoothness", _slLabel, GUILayout.Width(80));
            var gloss = GUILayout.HorizontalSlider(character.skinSmoothness, 0.05f, 0.6f);
            GUILayout.Label(character.skinSmoothness.ToString("0.00"), _slLabel, GUILayout.Width(40));
            GUILayout.EndHorizontal();
            GUILayout.Label("Lower = matte (tames travelling wet glints). Skin only — exposed skin on garments follows; fabric keeps its own gloss.", _dim);
            if (Mathf.Abs(gloss - character.skinSmoothness) > 0.001f) character.SetSkinSmoothness(gloss);

            if (character.baseTones != null && character.baseTones.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("Skin Tone", _sectionHead);
                var tonesApply = character.TonesApply;
                if (!tonesApply) GUILayout.Label("(applies with the EA Default skin — a CC base skin carries its own tone)", _dim);
                GUI.enabled = tonesApply;
                var labels = Labels(character.baseTones, s => s.label ?? s.id);
                var sel = Grid(character.BaseToneIndex, labels);
                if (sel != character.BaseToneIndex) character.SetBaseTone(sel);
                GUI.enabled = true;
            }

            if (character.eyeColors != null && character.eyeColors.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("Eye Colour", _sectionHead);
                var labels = Labels(character.eyeColors, s => s.label ?? s.id);
                var sel = Grid(character.EyeColorIndex, labels);
                if (sel != character.EyeColorIndex) character.SetEyeColor(sel);
            }

            if (character.detailLayers != null && character.detailLayers.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("Detail Overlays", _sectionHead);
                for (var i = 0; i < character.detailLayers.Count; i++)
                {
                    var d = character.detailLayers[i];
                    var on = GUILayout.Toggle(d.active, "  " + (d.label ?? d.id), _slLabel);
                    if (on != d.active) character.ToggleDetailAt(i, on);
                }
            }

            DrawMakeup(); // makeup = skin-layer slots (lipstick/eyeshadow/eyeliner/blush)
        }

        // ---- MESHES ------------------------------------------------------------------------------
        private void DrawMeshes()
        {
            MeshSlot("Top (torso + arms)", character.topVariants, character.TopIndex, character.SetTopMesh);
            GUILayout.Space(8);
            MeshSlot("Bottom (legs)", character.bottomVariants, character.BottomIndex, character.SetBottomMesh);
            GUILayout.Space(8);
            MeshSlot("Feet", character.feetVariants, character.FeetIndex, character.SetFeetMesh);
        }

        private void MeshSlot(string title, List<Sims4Character.MeshVariant> list, int current, Action<int> setter)
        {
            GUILayout.Label(title, _sectionHead);
            if (list == null || list.Count == 0) { GUILayout.Label("(no variants)", _dim); return; }
            var labels = Labels(list, m => m.label ?? m.id);
            var sel = Grid(current, labels);
            if (sel != current) setter(sel);
        }

        // ---- HAIR (styles on the shared skeleton; colour = a diffuse swap) -----------------------
        private void DrawHair()
        {
            if (character.hairStyles == null || character.hairStyles.Count == 0)
            {
                GUILayout.Label("No hair in this character yet.", _dim);
                GUILayout.Space(4);
                GUILayout.Label("Export a style, then rebuild the scene:", _dim);
                GUILayout.Label("exporthair af_char <styleInternalName>", _slLabel);
                return;
            }

            GUILayout.Label("Style", _sectionHead);
            var styleLabels = Labels(character.hairStyles, h => h.label ?? h.id);
            var sSel = Grid(character.HairStyleIndex, styleLabels);
            if (sSel != character.HairStyleIndex) character.SetHairStyle(sSel);

            if (character.HairColorCount > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("Colour", _sectionHead);
                var style = character.hairStyles[character.HairStyleIndex];
                var colorLabels = Labels(style.colors, c => c.label ?? c.id);
                var cSel = Grid(character.HairColorIndex, colorLabels);
                if (cSel != character.HairColorIndex) character.SetHairColor(cSel);
            }
            else if (character.HairStyleIndex > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("(no colour swatches for this style)", _dim);
            }
        }

        // ---- CLOTHING (wardrobe + accessory slots; makeup slots live in the SKIN tab) ----
        private void DrawClothing()
        {
            var any = false; // per-slot >1 check: stale scenes deserialize new slots as EMPTY lists, not [None]
            for (var s = 0; s < Sims4Creator.Sims4Character.FirstMakeupSlot; s++)
                if (character.GetClothingCount(s) > 1) { any = true; break; }
            if (!any)
            {
                GUILayout.Label("No clothing in this character yet.", _dim);
                GUILayout.Space(4);
                GUILayout.Label("Export a garment, then rebuild the scene:", _dim);
                GUILayout.Label("exportcloth af_char <yfTop_/yfBottom_/yfBody_/yfShoes_/yfAcc_Socks...>", _slLabel);
                return;
            }

            for (var slot = 0; slot < Sims4Creator.Sims4Character.FirstMakeupSlot; slot++) // makeup slots draw in the SKIN tab
            {
                var list = character.GetClothing(slot);
                if (list == null || list.Count <= 1) continue; // slot has only "None" — skip
                if (slot > 0) GUILayout.Space(8);
                var header = Sims4Creator.Sims4Character.ClothingSlotNames[slot];
                // Underwear slots layer: when covered by an outer garment their fabric composites into it.
                if ((slot == 5 || slot == 6) && character.GetClothingIndex(slot) > 0 && !character.IsClothingVisible(slot))
                    header += "   (layered under)";
                GUILayout.Label(header, _sectionHead);

                var labels = Labels(list, c => c.label ?? c.id);
                var sel = Grid(character.GetClothingIndex(slot), labels);
                if (sel != character.GetClothingIndex(slot)) character.SetClothing(slot, sel);

                if (character.GetClothingColorCount(slot) > 0)
                {
                    var item = list[character.GetClothingIndex(slot)];
                    var colorLabels = Labels(item.colors, c => c.label ?? c.id);
                    var csel = Grid(character.GetClothingColorIndex(slot), colorLabels);
                    if (csel != character.GetClothingColorIndex(slot)) character.SetClothingColor(slot, csel);
                }
            }
        }

        // ---- ANIMATION (idle only — walk/run/dance are locomotion, excluded from CAS) ------------
        private void DrawAnim()
        {
            GUILayout.Label("Motion", _sectionHead);
            animMode = GUILayout.Toolbar(Mathf.Clamp(animMode, 0, 2), MotionNames, _tabStyle);
            GUILayout.Label(animMode == 0 ? "Auto (like CAS): idles on Full; on Cowboy/Chest the body holds an arms-down rest with a calm face (blinks); the Face close-up is fully still."
                          : animMode == 1 ? "Idle: the Sim always plays a gentle idle (blinks + looks around)."
                          : "Still: body + face fully frozen in the symmetric arms-down rest pose — for detailed editing on any shot.", _dim);

            GUILayout.Space(8);
            GUILayout.Label("Still pose", _sectionHead);
            faceLifeInStill = GUILayout.Toggle(faceLifeInStill, "  Calm face on mid shots (Face close-up always frozen)", _slLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Arms down", _slLabel, GUILayout.Width(80));
            var ad = GUILayout.HorizontalSlider(stillArmsDown, 0f, 70f);
            GUILayout.Label(stillArmsDown.ToString("0") + "°", _slLabel, GUILayout.Width(34));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(ad - stillArmsDown) > 0.5f)
            {
                stillArmsDown = ad;
                if (_bodyStillApplied) { RestoreBindPose(); ApplyArmsDown(); } // re-pose live while the body is still
            }

            GUILayout.Space(8);
            if (_idleClips.Count == 0)
            {
                GUILayout.Label("No idle clips built into this character.", _dim);
                return;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Idle variety", _sectionHead);
            GUILayout.FlexibleSpace();
            shuffleIdles = GUILayout.Toggle(shuffleIdles, "  Shuffle", _slLabel);
            GUILayout.EndHorizontal();
            for (var i = 0; i < _idleClips.Count; i++)
            {
                var active = i == _idleIdx;
                if (GUILayout.Button((active ? "> " : "   ") + Pretty(_idleClips[i]), active ? _btnActive : _btn))
                    { _idleIdx = i; _shuffleTimer = 0f; _customClip = null; } // manual idle pick overrides the tree selection
            }
            GUILayout.Space(6);
            GUILayout.Label("Walk / run / dance are locomotion — excluded from CAS.", _dim);
        }

        // ---- MORE (placeholders wired to the same architecture) ---------------------------------
        private void DrawMore()
        {
            // ---- CHARACTER TEMPLATES: save the FULL designed look (body/face/skin/hair/clothing) as a
            // portable JSON, and load any saved look back with one click. Files live under
            // Application.persistentDataPath/CharacterTemplates so a future game can enumerate + spawn them. ----
            GUILayout.Label("Character Templates", _sectionHead);
            if (character != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name", _slLabel, GUILayout.Width(46));
                _templateName = GUILayout.TextField(_templateName ?? string.Empty, _btn);
                if (GUILayout.Button("Save", _btn, GUILayout.Width(64)))
                {
                    var nm = string.IsNullOrWhiteSpace(_templateName) ? "Character" : _templateName.Trim();
                    var path = character.SaveTemplate(nm);
                    _templateMsg = "Saved '" + CharacterTemplateStore.NameOf(path) + "'";
                    _templatePaths = null; // refresh the list next draw
                }
                GUILayout.EndHorizontal();

                _templatePaths ??= CharacterTemplateStore.List();
                if (_templatePaths.Count == 0)
                {
                    GUILayout.Label("No saved templates yet — design a look and press Save.", _dim);
                }
                else
                {
                    GUILayout.Label("Saved (click to load):", _slLabel);
                    foreach (var p in _templatePaths)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(CharacterTemplateStore.NameOf(p), _btn))
                        {
                            if (character.ApplyTemplateFile(p))
                            {
                                _templateName = CharacterTemplateStore.NameOf(p);
                                _templateMsg = "Loaded '" + _templateName + "'";
                            }
                            else _templateMsg = "Failed to load.";
                        }
                        if (GUILayout.Button("x", _btn, GUILayout.Width(26)))
                        {
                            CharacterTemplateStore.Delete(p);
                            _templatePaths = null;
                            _templateMsg = "Deleted.";
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                if (GUILayout.Button("Refresh list", _btn)) _templatePaths = null;
                if (!string.IsNullOrEmpty(_templateMsg)) GUILayout.Label(_templateMsg, _dim);
                GUILayout.Label("Folder: " + CharacterTemplateStore.Dir, _dim);
            }
            GUILayout.Space(10);

            // ---- DIAGNOSTICS: the body, clothing and hair are SEPARATE meshes; toggle each to inspect
            // how a layer is constructed (e.g. is a strap cut from the fabric, or clipping the body?). ----
            GUILayout.Label("Diagnostics", _sectionHead);
            GUILayout.Label("Body, clothing and hair are separate meshes — hide any layer to inspect the others.", _dim);
            var b = GUILayout.Toggle(_diagBody, "  Body (nude skin)", _slLabel);
            if (b != _diagBody) { _diagBody = b; character.SetBodyVisible(b); }
            var c = GUILayout.Toggle(_diagClothes, "  Clothing", _slLabel);
            if (c != _diagClothes) { _diagClothes = c; character.SetClothingVisible(c); }
            var h = GUILayout.Toggle(_diagHair, "  Hair", _slLabel);
            if (h != _diagHair) { _diagHair = h; character.SetHairVisible(h); }
            var o = GUILayout.Toggle(_diagOcclusion, "  Occlusion cull (hide body skin under fabric)", _slLabel);
            if (o != _diagOcclusion) { _diagOcclusion = o; character.SetOcclusionEnabled(o); }
            GUILayout.Label("Tip: hide the Body to see a garment's fabric mesh alone — its silhouette is where the skin-cutout faces were stripped.", _dim);
            GUILayout.Space(12);

            GUILayout.Label("Coming next", _sectionHead);
            GUILayout.Label("These plug into the same catalog+builder pipeline once their assets are curated:", _dim);
            GUILayout.Space(6);
            foreach (var s in new[] { "Hats (need hat-chop hair variants)", "Rings / scarves", "Preset save / load" })
            {
                GUILayout.BeginHorizontal();
                GUI.enabled = false;
                GUILayout.Button("  " + s, _btn);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
            GUILayout.Label("Tab hides/shows this panel. Left-drag turns the Sim, scroll zooms; use the VIEW shots + Turn/Front buttons for precise framing.", _dim);
        }

        // ---- small IMGUI helpers -----------------------------------------------------------------
        private bool Foldout(string key, string label, bool forceOpen)
        {
            // Persist ONLY the user's manual open state; a filter's forced-open must never be written back
            // (else categories stay stuck open after the filter is cleared).
            if (!_open.TryGetValue(key, out var stored)) { stored = false; _open[key] = false; }
            var shown = forceOpen || stored;
            if (GUILayout.Button((shown ? "[-]  " : "[+]  ") + label, _catHead) && !forceOpen)
            {
                stored = !stored;
                _open[key] = stored;
            }
            return shown;
        }

        private static string[] Labels<T>(List<T> list, Func<T, string> pick)
        {
            var r = new string[list.Count];
            for (var i = 0; i < list.Count; i++) r[i] = pick(list[i]);
            return r;
        }

        private int Grid(int selected, string[] labels)
        {
            if (labels.Length == 0) return selected;
            var cols = Mathf.Max(1, Mathf.FloorToInt((_activePanelWidth - 40f) / 130f));
            // Explicit total width: SelectionGrid otherwise sizes cells to the LONGEST label and
            // overflows the panel sideways (cropped content + horizontal scrollbar).
            return GUILayout.SelectionGrid(Mathf.Clamp(selected, 0, labels.Length - 1), labels,
                Mathf.Min(cols, labels.Length), _gridBtn, GUILayout.Width(_activePanelWidth - 46f));
        }

        private static string Pretty(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            var s = raw;
            if (s.StartsWith("a_")) s = s.Substring(2);
            if (s.StartsWith("ad_") || s.StartsWith("af_")) s = s.Substring(3);
            s = s.Replace("_x", "").Replace("_", " ");
            return s.Length > 34 ? s.Substring(0, 34) : s;
        }

        // ---- styling -----------------------------------------------------------------------------
        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _bg = Solid(new Color(0.11f, 0.115f, 0.13f, 0.94f));
            _rowA = Solid(new Color(1, 1, 1, 0.03f));
            _rowB = Solid(new Color(0, 0, 0, 0.10f));
            _accentTex = Solid(new Color(0.22f, 0.42f, 0.62f, 1f));

            _title = new GUIStyle(GUI.skin.label) { fontSize = fontSize + 4, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.9f, 0.92f, 0.95f) } };
            _sectionHead = new GUIStyle(GUI.skin.label) { fontSize = fontSize + 1, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.55f, 0.75f, 0.95f) } };
            _panelLabel = new GUIStyle(GUI.skin.label) { fontSize = fontSize, normal = { textColor = new Color(0.85f, 0.86f, 0.88f) } };
            _slLabel = new GUIStyle(GUI.skin.label) { fontSize = fontSize, normal = { textColor = new Color(0.82f, 0.84f, 0.87f) }, wordWrap = false, clipping = TextClipping.Clip };
            _dim = new GUIStyle(GUI.skin.label) { fontSize = fontSize, wordWrap = true, normal = { textColor = new Color(0.6f, 0.62f, 0.66f) } };

            _btn = new GUIStyle(GUI.skin.button) { fontSize = fontSize, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 4, 4) };
            // Grid cells wrap long labels ("EA tone 5545 (warm peach, default AF)") instead of forcing
            // the row wider than the panel — the old overflow cropped the panel behind a horizontal bar.
            _gridBtn = new GUIStyle(_btn) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
            _btnActive = new GUIStyle(_btn);
            _btnActive.normal.textColor = Color.white; _btnActive.normal.background = _accentTex;
            _btnActive.hover.background = _accentTex; _btnActive.hover.textColor = Color.white;

            _catHead = new GUIStyle(GUI.skin.button) { fontSize = fontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 6, 5, 5) };
            _catHead.normal.textColor = new Color(0.88f, 0.9f, 0.93f);

            _tabStyle = new GUIStyle(GUI.skin.button) { fontSize = fontSize, padding = new RectOffset(4, 4, 5, 5) };
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c); t.Apply();
            return t;
        }
    }
}
