using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sims4Creator.Game
{
    /// <summary>
    /// In-game Build mode — the Buy catalogue (TASK-004), driven by REAL exported furniture via
    /// <see cref="GameCatalog"/>. The dock (BuildDock.uxml) shows function categories, a room filter,
    /// search, a virtualized thumbnail grid (real BuyBuildThumbnails) and the item's real baked swatches.
    ///
    /// Placement is engine-grade, ported from the proven HomeEditor: a live GHOST (template clone,
    /// colliders off) snapped to lot cells, over a green/red footprint quad showing validity; placement
    /// is refused while blocked. The free camera stays LIVE in build mode (GameCamera): right-DRAG
    /// orbits, right-CLICK (≤5 px, via <see cref="GameCamera.RightDragOrbiting"/>) cancels placing.
    /// Clicking an existing object ALWAYS selects it — placing only happens on empty ground.
    ///
    /// Input contract (build mode):
    ///   LMB  — select object under cursor, else place armed item (stays armed), else deselect.
    ///   LMB-drag on selected — move (snapped; reverts if dropped blocked).
    ///   RMB-click — cancel placing · RMB-drag — camera orbit · MMB/WASD — pan · wheel — zoom (not over UI).
    ///   R — rotate 90° (ghost or selection) · Del — delete selection.
    ///   Esc — blur search → cancel placing → deselect → exit.
    ///   All tool keys are ignored while typing in the search field (UiPointer.TextInputFocused).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBuildMode : MonoBehaviour
    {
        public PlayerController player;
        public GameClock clock;
        public Camera cam;
        public UIDocument document;
        public GameCatalog catalog;
        public GameCamera gameCamera;
        [Tooltip("The 1 m grid visual, shown only while build mode is open.")]
        public GameObject gridOverlay;

        public bool InBuild { get; private set; }

        // catalogue browse state
        private string _category;
        private string _room;                 // active room filter, null = all
        private string _search = "";

        // placement state (armed item + ghost)
        private BuildableDef _placeDef;
        private int _placeSwatch;
        private int _rot;                     // 0..3 → yaw 0/90/180/270
        private GameObject _ghost;
        private GameObject _footQuad;
        private Renderer _footQuadRenderer;
        private readonly List<Material> _ghostMats = new List<Material>(); // swatch-preview clones we own
        private bool _ghostValid;
        private Vector3 _snapPos;

        // selection state (placed object under edit)
        private SmartObject _selected;
        private bool _dragging;
        private Vector3 _dragStartPos;
        private Quaternion _dragStartRot;

        // UI refs
        private VisualElement _root, _swatchBar;
        private Label _status;
        private TextField _searchField;
        private CatalogGrid _grid;
        private readonly Dictionary<string, Button> _catButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> _roomButtons = new Dictionary<string, Button>();

        private void Start()
        {
            if (cam == null) cam = Camera.main;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (catalog == null) catalog = GameCatalog.Instance != null ? GameCatalog.Instance : FindFirstObjectByType<GameCatalog>();
            if (gameCamera == null) gameCamera = FindFirstObjectByType<GameCamera>();
            BuildPanel();
            ShowPanel(false);
        }

        private void OnEnable() { if (document != null) UiPointer.Register(document); }
        private void OnDisable() { if (document != null) UiPointer.Unregister(document); }

        // ---------------------------------------------------------------- UI construction

        private void BuildPanel()
        {
            if (document == null) return;
            _root = document.rootVisualElement;
            if (_root == null) return;
            UiPointer.Register(document); // OnEnable may have run before the document was assigned

            _status = _root.Q<Label>("build-status");
            _swatchBar = _root.Q<VisualElement>("swatch-bar");

            var back = _root.Q<Button>("build-back");
            if (back != null) back.clicked += ExitBuild;

            var rotate = _root.Q<Button>("build-rotate");
            if (rotate != null) rotate.clicked += RotatePressed;

            var delete = _root.Q<Button>("build-delete");
            if (delete != null) delete.clicked += DeleteSelected;

            _searchField = _root.Q<TextField>("build-search");
            if (_searchField != null)
            {
                _searchField.label = "";
                _searchField.RegisterValueChangedCallback(ev => { _search = ev.newValue ?? ""; RefreshGrid(); });
            }

            var rail = _root.Q<VisualElement>("category-rail");
            if (rail != null && catalog != null)
            {
                rail.Clear();
                _catButtons.Clear();
                foreach (var cat in catalog.Categories())
                {
                    string c = cat;
                    var b = new Button(() => SelectCategory(c)) { text = c };
                    b.AddToClassList("cat-btn");
                    rail.Add(b);
                    _catButtons[c] = b;
                }
            }

            var roomBar = _root.Q<VisualElement>("room-filter");
            if (roomBar != null && catalog != null)
            {
                roomBar.Clear();
                _roomButtons.Clear();
                foreach (var room in catalog.Rooms())
                {
                    string r = room;
                    var b = new Button(() => ToggleRoom(r)) { text = r };
                    b.AddToClassList("room-chip");
                    roomBar.Add(b);
                    _roomButtons[r] = b;
                }
            }

            var holder = _root.Q<VisualElement>("grid-holder");
            if (holder != null)
            {
                _grid = new CatalogGrid(columns: 5, cellHeight: 96f);
                holder.Add(_grid.View);
                _grid.Selected += OnItemPicked;
            }
        }

        private void ShowPanel(bool show)
        {
            if (_root != null) _root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------------------------------------------------------------- mode enter/exit

        public void EnterBuild()
        {
            if (InBuild) return;
            if (player != null) player.enabled = false;         // stop world-clicks from possessing/moving
            if (clock != null) clock.ExternalPause = true; else Time.timeScale = 0f;
            _placeDef = null; _placeSwatch = 0; _rot = 0;
            _selected = null; _dragging = false;
            _room = null; _search = "";
            foreach (var kv in _roomButtons) kv.Value.EnableInClassList("room-chip--on", false);
            if (_searchField != null) _searchField.SetValueWithoutNotify("");
            var cats = catalog != null ? catalog.Categories() : null;
            if (cats != null && cats.Count > 0) SelectCategory(cats[0]);
            if (gridOverlay != null) gridOverlay.SetActive(true);
            ShowPanel(true);
            UpdateStatus();
            InBuild = true;
        }

        public void ExitBuild()
        {
            if (!InBuild) return;
            LeavePlacing();
            Deselect();
            if (clock != null) clock.ExternalPause = false; else Time.timeScale = 1f;
            if (player != null) player.enabled = true;
            if (gridOverlay != null) gridOverlay.SetActive(false);
            ShowPanel(false);
            RebuildGrid(); // routing matches the lot the player just edited
            InBuild = false;
        }

        // ---------------------------------------------------------------- catalogue browsing

        private void SelectCategory(string cat)
        {
            _category = cat;
            foreach (var kv in _catButtons) kv.Value.EnableInClassList("cat-btn--on", kv.Key == cat);
            RefreshGrid();
        }

        private void ToggleRoom(string room)
        {
            _room = (_room == room) ? null : room; // click the active chip again to clear
            foreach (var kv in _roomButtons) kv.Value.EnableInClassList("room-chip--on", kv.Key == _room);
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            if (_grid == null || catalog == null) return;
            var list = new List<BuildableDef>();
            foreach (var d in catalog.items)
            {
                if (_category != null && d.category != _category) continue;
                if (_room != null && d.room != _room) continue;
                if (!string.IsNullOrEmpty(_search) &&
                    d.name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Add(d);
            }
            _grid.SetItems(list);
        }

        // ---------------------------------------------------------------- arm / swatches

        private void OnItemPicked(BuildableDef def)
        {
            Deselect();
            _placeDef = def;
            _placeSwatch = 0;
            SpawnGhost();
            ShowSwatches(def, 0);
            UpdateStatus();
        }

        private void ShowSwatches(BuildableDef def, int active)
        {
            if (_swatchBar == null) return;
            _swatchBar.Clear();
            if (def == null || def.swatches == null) return;
            for (int i = 0; i < def.swatches.Count; i++)
            {
                int idx = i;
                var sw = def.swatches[i];
                var b = new Button(() => OnSwatch(idx)) { tooltip = sw.name };
                b.AddToClassList("swatch");
                b.EnableInClassList("swatch--on", i == active);
                if (sw.diffuse != null) b.style.backgroundImage = new StyleBackground(sw.diffuse);
                else b.style.backgroundColor = sw.color;
                _swatchBar.Add(b);
            }
        }

        private void OnSwatch(int i)
        {
            if (_selected != null)             // recolour the selected placed object
            {
                _selected.Recolor(i);
                ShowSwatches(_selected.Def, _selected.SwatchIndex);
            }
            else if (_placeDef != null)        // colour for the next placement — preview it on the ghost
            {
                _placeSwatch = i;
                ShowSwatches(_placeDef, _placeSwatch);
                RespawnGhost();
            }
        }

        // ---------------------------------------------------------------- ghost

        private void SpawnGhost()
        {
            KillGhost();
            if (_placeDef == null) return;

            if (_placeDef.template != null)
            {
                _ghost = Object.Instantiate(_placeDef.template);
                _ghost.SetActive(true);
            }
            else
            {
                _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _ghost.transform.localScale = _placeDef.size;
            }
            _ghost.name = "~ghost";
            foreach (var c in _ghost.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            if (_placeSwatch > 0) PreviewSwatchOnGhost();

            _footQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _footQuad.name = "~ghost-footprint";
            Object.Destroy(_footQuad.GetComponent<Collider>());
            _footQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _footQuadRenderer = _footQuad.GetComponent<Renderer>();
            _footQuadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghost.SetActive(false); // hidden until the first valid ground hit
            _footQuad.SetActive(false);
        }

        private void RespawnGhost() { if (_ghost != null) SpawnGhost(); }

        /// <summary>Apply the chosen swatch's diffuse to the ghost so the preview shows the real colour.</summary>
        private void PreviewSwatchOnGhost()
        {
            if (_ghost == null || _placeDef == null) return;
            if (_placeSwatch <= 0 || _placeSwatch >= _placeDef.swatches.Count) return;
            var tex = _placeDef.swatches[_placeSwatch].diffuse;
            if (tex == null || _placeDef.primaryTexture == null) return;
            foreach (var r in _ghost.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                var changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].HasProperty("_BaseColorMap")
                        && mats[i].GetTexture("_BaseColorMap") == _placeDef.primaryTexture)
                    {
                        var clone = new Material(mats[i]);
                        clone.SetTexture("_BaseColorMap", tex);
                        _ghostMats.Add(clone); // ghost clones die with the ghost — see KillGhost
                        mats[i] = clone;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        private void KillGhost()
        {
            if (_ghost != null) Object.Destroy(_ghost);
            if (_footQuad != null) Object.Destroy(_footQuad);
            foreach (var m in _ghostMats) if (m != null) Object.Destroy(m);
            _ghostMats.Clear();
            _ghost = null; _footQuad = null; _footQuadRenderer = null;
            _ghostValid = false;
        }

        private void LeavePlacing()
        {
            _placeDef = null;
            KillGhost();
            _grid?.ClearSelection();
            if (_selected == null && _swatchBar != null) _swatchBar.Clear();
            UpdateStatus();
        }

        // ---------------------------------------------------------------- per-frame

        private void Update()
        {
            if (!InBuild) return;

            bool typing = UiPointer.TextInputFocused();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (typing) { _searchField?.Blur(); return; }               // 1st Esc leaves the search box
                if (_placeDef != null) { LeavePlacing(); return; }
                if (_selected != null) { Deselect(); return; }
                ExitBuild();
                return;
            }
            if (!typing && Input.GetKeyDown(KeyCode.R)) { RotatePressed(); return; }
            if (!typing && Input.GetKeyDown(KeyCode.Delete) && _selected != null) { DeleteSelected(); return; }
            if (cam == null) return;

            bool overUi = UiPointer.OverAnyUi();

            UpdateGhost(overUi);

            // Right-CLICK (not an orbit drag, not on the dock) cancels placing. Checked on button UP so
            // a right-drag camera orbit never eats the armed item.
            if (Input.GetMouseButtonUp(1) && _placeDef != null && !overUi
                && (gameCamera == null || !gameCamera.RightDragOrbiting))
            {
                LeavePlacing();
                return;
            }

            if (Input.GetMouseButtonDown(0) && !overUi)
            {
                // An EXISTING object under the cursor always wins — a click meant to select must never
                // drop furniture. Only empty, valid ground places.
                var hit = RaycastSmartObject();
                if (hit != null)
                {
                    if (_placeDef != null) LeavePlacing();
                    SelectPlaced(hit);
                    _dragging = true;
                    _dragStartPos = hit.transform.position;
                    _dragStartRot = hit.transform.rotation;
                }
                else if (_placeDef != null)
                {
                    if (_ghostValid)
                    {
                        SmartObjectFactory.Create(_placeDef, _snapPos, _placeSwatch, _rot * 90f);
                        RebuildGrid(); // new obstacle → Sims route around it
                    }
                }
                else
                {
                    Deselect();
                }
            }

            if (_dragging && _selected != null && Input.GetMouseButton(0) && !overUi && GroundPoint(out var g))
            {
                var dims = FootprintCells(_selected.Def, _selected.RotIndex);
                _selected.transform.position = Snap(g, dims.w, dims.d);
            }

            if (Input.GetMouseButtonUp(0) && _dragging)
            {
                _dragging = false;
                // A plain select-click never moved the object — no revert check (a Sim standing at the
                // object would otherwise trigger a spurious "blocked"). Only a real move validates.
                if (_selected != null && _selected.transform.position != _dragStartPos)
                {
                    if (Blocked(_selected.Def, _selected.transform.position, _selected.RotIndex, _selected))
                    {
                        _selected.transform.SetPositionAndRotation(_dragStartPos, _dragStartRot);
                        UpdateStatus("Blocked — moved back.");
                    }
                    RebuildGrid();
                }
            }
        }

        private void UpdateGhost(bool overUi)
        {
            if (_placeDef == null || _ghost == null) return;
            if (overUi || !GroundPoint(out var g))
            {
                _ghost.SetActive(false);
                if (_footQuad != null) _footQuad.SetActive(false);
                _ghostValid = false;
                return;
            }

            var dims = FootprintCells(_placeDef, _rot);
            _snapPos = Snap(g, dims.w, dims.d);
            _ghostValid = !Blocked(_placeDef, _snapPos, _rot, null);

            _ghost.SetActive(true);
            if (_placeDef.template != null)
                _ghost.transform.SetPositionAndRotation(_snapPos, Quaternion.Euler(0f, _rot * 90f, 0f));
            else
                _ghost.transform.SetPositionAndRotation(_snapPos + Vector3.up * (_placeDef.size.y * 0.5f),
                                                        Quaternion.Euler(0f, _rot * 90f, 0f));

            if (_footQuad != null)
            {
                float tile = TileSize;
                _footQuad.SetActive(true);
                _footQuad.transform.position = new Vector3(_snapPos.x, 0.015f, _snapPos.z);
                _footQuad.transform.localScale = new Vector3(dims.w * tile * 0.98f, dims.d * tile * 0.98f, 1f);
                if (_footQuadRenderer != null && catalog != null)
                    _footQuadRenderer.sharedMaterial = _ghostValid ? catalog.cellFreeMaterial : catalog.cellBlockedMaterial;
            }
        }

        // ---------------------------------------------------------------- placement math (1 m tiles, D-106)

        private static float TileSize => LotGrid.Instance != null ? LotGrid.Instance.tileSize : 1f;

        private static (int w, int d) FootprintCells(BuildableDef def, int rot)
        {
            int w = Mathf.Max(1, def.footW), d = Mathf.Max(1, def.footD);
            return (rot & 1) == 1 ? (d, w) : (w, d);
        }

        /// <summary>Snap a ground point so a w×d TILE footprint sits centred under the cursor.</summary>
        private static Vector3 Snap(Vector3 ground, int w, int d)
        {
            var grid = LotGrid.Instance;
            return grid != null
                ? grid.SnapFootprint(ground, w, d)
                : LotGrid.SnapFootprint(Vector3.zero, 1f, ground, w, d);
        }

        /// <summary>
        /// Placement validity, grid-first: the footprint's TILES must be free on the occupancy map
        /// (the authority — D-106); physics is consulted only for Sims, whose positions are continuous.
        /// </summary>
        private static bool Blocked(BuildableDef def, Vector3 pos, int rot, SmartObject ignore)
        {
            if (def == null) return false;
            var (w, d) = FootprintCells(def, rot);

            var grid = LotGrid.Instance;
            if (grid != null)
            {
                var (tx, tz) = grid.OriginTileOf(pos, w, d);
                if (!grid.TilesFree(tx, tz, w, d, ignore)) return true; // off-lot or tile owned
            }

            // Sims stand at continuous positions — a footprint may not land on one.
            var half = new Vector3(def.size.x * 0.5f - 0.03f, def.size.y * 0.5f, def.size.z * 0.5f - 0.03f);
            var centre = pos + Vector3.up * (def.size.y * 0.5f + 0.02f);
            foreach (var h in Physics.OverlapBox(centre, half, Quaternion.Euler(0f, rot * 90f, 0f)))
                if (h.GetComponentInParent<SimAgent>() != null) return true;
            return false;
        }

        private SmartObject RaycastSmartObject()
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out var h, 300f) ? h.collider.GetComponentInParent<SmartObject>() : null;
        }

        private bool GroundPoint(out Vector3 point)
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d)) { point = ray.GetPoint(d); return true; }
            point = default;
            return false;
        }

        // ---------------------------------------------------------------- selection ops

        private void SelectPlaced(SmartObject so)
        {
            _selected = so;
            if (so != null && so.Def != null) ShowSwatches(so.Def, so.SwatchIndex);
            else if (_swatchBar != null) _swatchBar.Clear();
            UpdateStatus();
        }

        private void Deselect()
        {
            _selected = null;
            _dragging = false;
            if (_placeDef == null && _swatchBar != null) _swatchBar.Clear();
            UpdateStatus();
        }

        private void RotatePressed()
        {
            if (_placeDef != null)
            {
                _rot = (_rot + 1) & 3;      // ghost re-validates next frame
            }
            else if (_selected != null)
            {
                int r0 = _selected.RotIndex;
                int r = (r0 + 1) & 3;
                _selected.transform.rotation = Quaternion.Euler(0f, r * 90f, 0f);
                // A rotated footprint (w↔d) can land inside a neighbour — validate like a move does.
                if (Blocked(_selected.Def, _selected.transform.position, r, _selected))
                {
                    _selected.transform.rotation = Quaternion.Euler(0f, r0 * 90f, 0f);
                    UpdateStatus("No room to rotate here.");
                    return;
                }
                RebuildGrid();
            }
        }

        private void DeleteSelected()
        {
            if (_selected == null) return;
            // Deactivate before the deferred Destroy so the nav-grid rebuild (scans ACTIVE objects only)
            // doesn't keep the doomed object blocking cells for a frame.
            _selected.gameObject.SetActive(false);
            Destroy(_selected.gameObject);
            Deselect();
            RebuildGrid();
        }

        private static void RebuildGrid()
        {
            if (LotGrid.Instance != null) LotGrid.Instance.Rebuild();
        }

        // ---------------------------------------------------------------- status line

        private void UpdateStatus(string overrideText = null)
        {
            if (_status == null) return;
            if (overrideText != null) { _status.text = overrideText; return; }
            if (_placeDef != null)
                _status.text = $"Placing {_placeDef.name} — click ground to drop (several ok) · R rotate · right-click stop.  Camera: RMB-drag orbit · MMB/WASD pan · wheel zoom.";
            else if (_selected != null)
                _status.text = $"{_selected.displayName} — drag move · R rotate · swatch recolour · Del delete · Esc done.";
            else
                _status.text = "Pick an item to place, or click a placed object to edit it.  Camera: RMB-drag orbit · MMB/WASD pan · wheel zoom.";
        }
    }
}
