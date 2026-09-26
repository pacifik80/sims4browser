using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// M2.5 "Home Editor". Tools: Objects (footprint placement + per-item SWATCH recolours),
    /// Walls (Sims-style RUBBER-BAND drawing: LMB-press anchors a node, drag stretches a straight
    /// or 45°-diagonal run with live preview, release commits, RMB cancels; Erase removes the same
    /// way), Wall Paint and Floor Paint (Floor Paint drags a RECTANGLE of tiles, release fills).
    /// Wall geometry is procedural (per-edge 2.8m segments; diagonals are √2 long). Layout v3 JSON
    /// persists objects+swatch, walls (o 0..3) and floor tiles. RMB orbits, MMB pans, wheel zooms.
    /// </summary>
    public sealed class HomeEditor : MonoBehaviour
    {
        [Serializable]
        public sealed class SwatchDef
        {
            public string label;
            public Texture2D diffuse;
        }

        [Serializable]
        public sealed class ItemDef
        {
            public string id;
            public string label;
            public string category;
            public string wallItem = "";     // "" = floor object; "door"/"window" = snaps to walls, cuts an opening
            public GameObject template;
            public Texture2D thumb;          // the game's own BuyBuildThumbnail
            public Texture2D primaryTexture; // the template's primary diffuse — swatches replace THIS
            public List<SwatchDef> swatches = new();
            public int footW = 1;
            public int footD = 1;
            // Wall items: the opening cut into the wall, derived from the item's AUTHORED bounds by
            // the builder (real doors are ~0.9m wide, not a full tile; short windows get sill-lifted).
            public float openWidth = 1f;
            public float holeY0;
            public float holeY1 = 2.1f;
            public float liftY;              // vertical placement of the item itself (window sill lift)
            // The game's OWN cutout silhouette (ModelCutout polygon, item-local metres, y from the
            // authored floor). When present it replaces the rect hole — arches cut real arches.
            public List<Vector2> cutout = new();
        }

        [Serializable]
        public sealed class CoveringDef
        {
            public string id;
            public string label;
            public Material material;
            public Texture2D thumb;
        }

        [Header("Wired by the scene builder")]
        public List<ItemDef> items = new();
        public List<CoveringDef> wallCoverings = new();
        public List<CoveringDef> floorCoverings = new();
        public Material defaultWallMaterial;
        public Camera editorCamera;
        public Transform placedRoot;
        public Material cellFreeMaterial;
        public Material cellBlockedMaterial;

        public const float DefaultWallHeight = 2.8f; // builder gates out wall items taller than this

        [Header("Grid")]
        public float tileSize = 1f;
        public int gridExtent = 12;
        public float wallHeight = DefaultWallHeight;
        public float wallThickness = 0.12f;

        // ---- serialized layout (v4: + lvl; missing field in old files defaults to level 0) ----
        [Serializable]
        private sealed class LayoutEntry { public string id; public int x; public int z; public int rot; public int swatch; public int lvl; }

        [Serializable]
        private sealed class WallEntry { public int x; public int z; public int o; public string cov; public int lvl; }

        [Serializable]
        private sealed class FloorEntry { public int x; public int z; public string cov; public int lvl; }

        [Serializable]
        private sealed class WallItemEntry { public string id; public int x; public int z; public int o; public int flip; public int lvl; }

        [Serializable]
        private sealed class LayoutFile
        {
            public List<LayoutEntry> entries = new();
            public List<WallEntry> walls = new();
            public List<FloorEntry> floors = new();
            public List<WallItemEntry> wallItems = new();
        }

        private sealed class Placed
        {
            public string id;
            public GameObject go;
            public int x;
            public int z;
            public int rot;
            public int w;
            public int d;
            public int swatch;                 // 0 = default
            public Texture2D swatchAppliedTex; // the texture the last swatch applied (for re-matching)
        }

        private sealed class WallSeg
        {
            public GameObject go;
            public string covId;
            public int opening;      // 0 = solid, non-zero = carrying a door/window opening
            public Mesh customMesh;  // the FULL generated mesh (solid or opening); destroyed on rebuild
            public Mesh stubMesh;    // cutaway: customMesh clamped to StubHeight (lazy; rebuilt with customMesh)
            public bool lowered;     // cutaway: currently showing the short stub instead of customMesh
            public GameObject shadowGhost; // cutaway: full-height invisible ShadowsOnly caster (child of go)
        }

        private sealed class PlacedWallItem
        {
            public string id;
            public GameObject go;
            public readonly List<Vector3Int> segs = new(); // wall segments this item's opening covers
            public int flip;                                // 0 or 1: which side the item faces
        }

        private sealed class FloorTile
        {
            public GameObject go;
            public string covId;
        }

        // ---- state ----
        // MULTI-LEVEL: everything buildable lives on a level (world y = level·wallHeight). All
        // tools operate on the ACTIVE level through the aliases below, so the tool code stays
        // level-agnostic; levels above the active one are hidden (Sims-style); floor tiles on
        // level N are the ceiling of level N−1 (two-faced quads). Positions are WORLD-space —
        // the per-level roots exist for visibility toggling and hierarchy tidiness only.
        public const int MaxLevels = 4;

        private sealed class HomeLevel
        {
            public GameObject root;
            public Transform wallsRoot;
            public Transform floorsRoot;
            public Transform itemsRoot;
            public readonly List<Placed> placed = new();
            public readonly Dictionary<Vector2Int, Placed> occupied = new();
            // Wall key: (x, z, o). o=0: node(x,z)→(x+1,z). o=1: →(x,z+1). o=2: →(x+1,z+1). o=3: node(x+1,z)→(x,z+1).
            public readonly Dictionary<Vector3Int, WallSeg> walls = new();
            public readonly Dictionary<Collider, Vector3Int> wallByCollider = new();
            public readonly Dictionary<Vector2Int, FloorTile> floorTiles = new();
            public readonly Dictionary<Vector2Int, GameObject> nodeCaps = new();
            public readonly List<PlacedWallItem> wallItems = new();
            public GameObject ceiling; // ShadowsOnly ceiling over enclosed rooms (invisible; shadows interior)
        }

        private readonly List<HomeLevel> _levels = new();
        private int _activeLevel;
        private HomeLevel Lv => _levels[_activeLevel];
        private float LevelY => _activeLevel * wallHeight;
        private List<Placed> _placed => Lv.placed;
        private Dictionary<Vector2Int, Placed> _occupied => Lv.occupied;
        private Dictionary<Vector3Int, WallSeg> _walls => Lv.walls;
        private Dictionary<Collider, Vector3Int> _wallByCollider => Lv.wallByCollider;
        private Dictionary<Vector2Int, FloorTile> _floorTiles => Lv.floorTiles;

        private int _mode;
        private static readonly string[] ModeNames = { "Objects", "Walls", "Wall Paint", "Floor Paint" };
        private bool _erase;
        private int _wallCovSel;
        private int _floorCovSel;

        private ItemDef _placing;
        private GameObject _ghost;
        private Placed _movingExisting;
        private Placed _selected;
        private int _ghostRot;
        private bool _ghostValid;
        private Vector2 _scroll;
        private string _status = "Pick a tool. Objects places furniture; Walls stretches wall runs.";

        // wall rubber-band drag
        private bool _wallDrag;
        private Vector2Int _wallAnchor;                    // lattice node
        private readonly List<Vector3Int> _bandSegments = new();
        private readonly List<GameObject> _bandGhosts = new();

        // floor rectangle drag
        private bool _floorDrag;
        private Vector2Int _floorAnchor;                   // cell
        private Vector2Int _floorCursor;

        private readonly List<GameObject> _cellPool = new();
        // Walls are a GRAPH: every segment mesh is generated per-segment with MITERED ends — at
        // each node the incident walls' side faces are extended/trimmed to their pairwise
        // intersection (t = h/tan(Δ/2)), so joints are true seams: collinear runs butt flush
        // (t=0), L-corners share a 45° miter plane, and junctions of 3+ walls close around a
        // small cap polygon at the node. No overlaps, no filler posts.
        private readonly Mesh[] _ghostMeshes = new Mesh[2]; // plain full boxes for previews only
        private Mesh _tileMesh; // two-faced floor quad (underside doubles as the ceiling below)
        // Cutaway ("walls down when facing the camera"): near/foreground walls drop to a short stub
        // so they don't hide the room; far walls stay full as a backdrop. Recomputed as the camera
        // orbits. The stub is the wall's OWN full mesh clamped to StubHeight, so it keeps its miter
        // joints to neighbours and its door/window/portal cutouts. Items keep their full-height GO.
        private bool _cutaway = true;
        private float _lastCutYaw = 9999f;
        private bool _bulkLoading; // suppresses per-wall cutaway recompute during LoadLayout
        private const float StubHeight = 0.4f;
        private Transform _wallsRoot => Lv.wallsRoot;
        private Transform _floorsRoot => Lv.floorsRoot;
        private Dictionary<Vector2Int, GameObject> _nodeCaps => Lv.nodeCaps; // junction cap prisms (3+ walls)
        private readonly List<(Vector3Int key, Vector2Int dir)> _incScratch = new();
        private readonly List<FanEntry> _fanScratch = new();
        private readonly List<Vector2> _capScratch = new();
        private List<PlacedWallItem> _wallItems => Lv.wallItems;
        private PlacedWallItem _selectedWallItem;
        private int _wallItemFlip;
        private readonly List<Vector3Int> _wallItemSpan = new(); // segments the current ghost covers

        private Vector3 _focus = Vector3.zero;
        private float _yaw = -45f, _pitch = 42f, _dist = 22f;
        private bool _orbiting;

        private GUIStyle _panel, _head, _btn, _btnActive, _dim, _thumbBtn, _thumbBtnActive;
        private Texture2D _bg;
        private const float PanelWidth = 300f;

        private string LayoutPath => Path.Combine(Application.dataPath, "Sims4", "home", "layout.json");

        private void Start()
        {
            EnsureLevel(0);
            ApplyCamera();
        }

        private void EnsureLevel(int index)
        {
            while (_levels.Count <= index)
            {
                var lv = new HomeLevel { root = new GameObject($"Level {_levels.Count + 1}") };
                if (placedRoot != null)
                {
                    lv.root.transform.SetParent(placedRoot, false);
                }
                lv.wallsRoot = new GameObject("Walls").transform;
                lv.wallsRoot.SetParent(lv.root.transform, false);
                lv.floorsRoot = new GameObject("Floors").transform;
                lv.floorsRoot.SetParent(lv.root.transform, false);
                lv.itemsRoot = new GameObject("Items").transform;
                lv.itemsRoot.SetParent(lv.root.transform, false);
                _levels.Add(lv);
            }
        }

        private void SetActiveLevel(int level)
        {
            level = Mathf.Clamp(level, 0, MaxLevels - 1);
            if (level == _activeLevel)
            {
                return;
            }
            CancelPlacement();
            CancelWallDrag();
            _floorDrag = false;
            _selected = null;
            _selectedWallItem = null;
            HideCellHighlights();
            EnsureLevel(level);
            _activeLevel = level;
            for (var i = 0; i < _levels.Count; i++)
            {
                _levels[i].root.SetActive(i <= level); // Sims-style: everything above is hidden
            }
            _focus.y = LevelY;
            RefreshCutaway(force: true); // re-evaluate walls now that a different level's set is showing
            _status = level == 0
                ? "Level 1 active."
                : $"Level {level + 1} active — floor tiles painted here form the ceiling below.";
        }

        // Plain full node-to-node box, previews only (real segments get per-segment mitered meshes).
        private Mesh FullMeshFor(int orientation)
        {
            var idx = orientation >= 2 ? 1 : 0;
            if (_ghostMeshes[idx] == null)
            {
                var full = idx == 1 ? tileSize * 1.41421356f : tileSize;
                _ghostMeshes[idx] = BuildWallBoxesMesh(full, wallHeight, wallThickness,
                    new[] { (-full / 2f, full / 2f, 0f, wallHeight) });
            }
            return _ghostMeshes[idx];
        }

        // The cutaway stub for a wall: the wall re-generated CLIPPED at StubHeight (not clamped). A
        // door/window LINTEL sits entirely above the clip and drops out, so the opening stays a real
        // gap in the low curb instead of collapsing into a horizontal band across it. Miters cap at the
        // curb height, a window sill below the curb keeps the curb solid, and the wallpaper shows its
        // undistorted bottom slice (UV still normalised by the full height). Cached until the full mesh
        // is rebuilt.
        private Mesh StubFor(WallSeg seg, Vector3Int key)
        {
            if (seg.stubMesh != null)
            {
                return seg.stubMesh;
            }
            var stub = BuildSegMesh(seg, key, StubHeight);
            stub.name = "wall_stub";
            seg.stubMesh = stub;
            return stub;
        }

        // Build a segment's mesh (opening vs solid dispatch), clipped at clipTop. clipTop = +inf builds
        // the full wall; StubHeight builds the cutaway curb.
        private Mesh BuildSegMesh(WallSeg seg, Vector3Int key, float clipTop)
        {
            if (seg.opening != 0)
            {
                var owner = _wallItems.Find(wi => wi.segs.Contains(key));
                var def = owner != null ? FindDef(owner.id) : null;
                if (def != null)
                {
                    return BuildOpeningSegMesh(def, owner, owner.segs.IndexOf(key), clipTop);
                }
            }
            return BuildSolidSegMesh(key, clipTop);
        }

        // Which lattice node sits at the mesh's local −X / +X end: o=1 (yaw 90) and o=3 (yaw 45)
        // map local +X AGAINST their key order, so their endpoints swap.
        private (Vector2Int neg, Vector2Int pos) SegmentLocalEnds(Vector3Int key)
        {
            var (a, b) = SegmentEndpoints(key);
            return key.z == 1 || key.z == 3 ? (b, a) : (a, b);
        }

        private void Update()
        {
            var overPanel = Input.mousePosition.x > Screen.width - PanelWidth;
            HandleCamera(overPanel);
            if (overPanel)
            {
                HideCellHighlights();
                // Releasing LMB over the panel CANCELS an armed drag — otherwise the rubber band
                // stays armed with the button up and the next click commits from a stale anchor.
                if (Input.GetMouseButtonUp(0) && (_wallDrag || _floorDrag))
                {
                    CancelWallDrag();
                    _floorDrag = false;
                }
                return;
            }

            switch (_mode)
            {
                case 0: UpdateObjectsMode(); break;
                case 1: UpdateWallsMode(); break;
                case 2: UpdateWallPaintMode(); break;
                case 3: UpdateFloorPaintMode(); break;
            }

            if (_mode == 0 && (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)))
            {
                if (_selected != null)
                {
                    DeleteSelected();
                }
                else if (_selectedWallItem != null)
                {
                    RemoveWallItem(_selectedWallItem);
                    _status = "Removed wall item (opening restored).";
                }
            }
        }

        private void SetMode(int mode)
        {
            if (mode == _mode)
            {
                return;
            }
            CancelPlacement();
            CancelWallDrag();
            _floorDrag = false;
            _selected = null;
            _selectedWallItem = null;
            HideCellHighlights();
            _mode = mode;
            _status = mode switch
            {
                0 => "Objects: pick a catalog item to place it.",
                1 => "Walls: press LMB and STRETCH a run (straight or diagonal). Release places, RMB cancels. Erase removes.",
                2 => "Wall Paint: pick a covering, then click/drag over walls.",
                3 => "Floor Paint: pick a covering, press LMB and stretch a rectangle. Release fills. Erase removes.",
                _ => _status,
            };
        }

        // ================================ OBJECTS ==============================================

        private void UpdateObjectsMode()
        {
            if (_placing != null)
            {
                if (string.IsNullOrEmpty(_placing.wallItem))
                {
                    UpdateGhost();
                }
                else
                {
                    UpdateWallItemGhost();
                }
            }
            else if (Input.GetMouseButtonDown(0))
            {
                TrySelect();
            }
        }

        // ---- doors & windows: snap to wall runs, cut openings -----------------------------------

        private void UpdateWallItemGhost()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                _wallItemFlip ^= 1; // face the other side of the wall
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelPlacement();
                return;
            }

            // Aim at a wall (they are 2.8m tall — easy raycast targets).
            _wallItemSpan.Clear();
            var valid = false;
            if (RaycastActiveWall(out var hovered))
            {
                if (hovered.z > 1)
                {
                    _status = "Doors/windows need a STRAIGHT wall (not a diagonal).";
                }
                else
                {
                    var span = Mathf.Max(1, _placing.footW);
                    var dir = hovered.z == 0 ? new Vector2Int(1, 0) : new Vector2Int(0, 1);
                    var start = new Vector2Int(hovered.x, hovered.y) - (dir * ((span - 1) / 2)); // center under cursor
                    valid = true;
                    for (var i = 0; i < span; i++)
                    {
                        var key = new Vector3Int(start.x + (dir.x * i), start.y + (dir.y * i), hovered.z);
                        _wallItemSpan.Add(key);
                        if (!_walls.TryGetValue(key, out var seg) || seg.opening != 0)
                        {
                            valid = false; // missing wall or already carrying an opening
                        }
                    }
                    for (var i = 1; i < span && valid; i++)
                    {
                        CollectIncidentWalls(new Vector2Int(start.x + (dir.x * i), start.y + (dir.y * i)), _incScratch);
                        if (_incScratch.Count > 2)
                        {
                            valid = false; // a junction sits inside the span — can't cut through it
                        }
                    }
                }
            }

            // ghost item + span highlight
            if (_wallItemSpan.Count > 0)
            {
                var first = _wallItemSpan[0];
                var last = _wallItemSpan[_wallItemSpan.Count - 1];
                var (p0, _, _) = SegmentTransform(first);
                var (p1, _, _) = SegmentTransform(last);
                var center = (p0 + p1) * 0.5f;
                center.y = LevelY + _placing.liftY; // preview at the same sill height placement will use
                var baseYaw = first.z == 0 ? 0f : 90f;
                var rot = Quaternion.Euler(0f, baseYaw + (_wallItemFlip * 180f), 0f);
                _ghost.SetActive(true);
                _ghost.transform.SetPositionAndRotation(center, rot);
                UpdateBandGhostsForSpan(valid);
            }
            else
            {
                _ghost.SetActive(false);
                UpdateBandGhostsForSpan(false);
            }

            if (Input.GetMouseButtonDown(0) && _wallItemSpan.Count > 0)
            {
                if (!valid)
                {
                    _status = "Needs a solid straight wall run of the right length.";
                    return;
                }
                PlaceWallItem(_placing, new List<Vector3Int>(_wallItemSpan), _wallItemFlip);
                _status = $"Placed {_placing.label} in the wall.";
            }
        }

        private void UpdateBandGhostsForSpan(bool valid)
        {
            while (_bandGhosts.Count < _wallItemSpan.Count)
            {
                var ghost = new GameObject("__bandGhost");
                ghost.transform.SetParent(transform, false);
                ghost.AddComponent<MeshFilter>(); // mesh assigned per-frame below
                var mr = ghost.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _bandGhosts.Add(ghost);
            }
            for (var i = 0; i < _bandGhosts.Count; i++)
            {
                var ghost = _bandGhosts[i];
                if (i >= _wallItemSpan.Count)
                {
                    ghost.SetActive(false);
                    continue;
                }
                var key = _wallItemSpan[i];
                var (pos, rot, _) = SegmentTransform(key);
                ghost.SetActive(true);
                ghost.GetComponent<MeshFilter>().sharedMesh = FullMeshFor(key.z);
                ghost.transform.SetPositionAndRotation(pos, rot);
                ghost.transform.localScale = new Vector3(1.02f, 1.02f, 1.6f); // slightly fatter so it reads over the wall
                ghost.GetComponent<MeshRenderer>().sharedMaterial = valid ? cellFreeMaterial : cellBlockedMaterial;
            }
        }

        private void PlaceWallItem(ItemDef def, List<Vector3Int> segs, int flip)
        {
            // Portals are a passable opening with NO object (template null) — just the cut wall.
            GameObject go = null;
            if (def.template != null)
            {
                go = Instantiate(def.template, Lv.itemsRoot);
                go.name = $"wallitem_{def.id}_{_wallItems.Count}";
                go.SetActive(true);
                var (p0, _, _) = SegmentTransform(segs[0]);
                var (p1, _, _) = SegmentTransform(segs[segs.Count - 1]);
                var baseYaw = segs[0].z == 0 ? 0f : 90f;
                var center = (p0 + p1) * 0.5f;
                center.y = LevelY + def.liftY; // short windows sit at sill height — the game lifts them the same way
                go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, baseYaw + (flip * 180f), 0f));
            }

            var item = new PlacedWallItem { id = def.id, go = go, flip = flip };
            item.segs.AddRange(segs);
            _wallItems.Add(item); // before the mesh pass — RefreshSegmentMesh resolves the owner

            var opening = def.wallItem == "window" ? 2 : 1;
            foreach (var key in segs)
            {
                if (_walls.TryGetValue(key, out var seg))
                {
                    seg.opening = opening;
                    RefreshSegmentMesh(key);
                }
            }
        }

        // The wall-with-opening mesh for span segment i, or null when the hole doesn't reach it.
        // With ModelCutout data the hole is the game's EXACT silhouette (arches cut arches);
        // otherwise a rect derived from the item's authored bounds, centered on the span. Boxes
        // are cut from the segment's CURRENT extents (flush to the node, or set back at a post
        // joint) and UVs stay tile-locked, so pieces from adjacent segments meet at node planes.
        private Mesh BuildOpeningSegMesh(ItemDef def, PlacedWallItem item, int i, float clipTop = float.PositiveInfinity)
        {
            if (def.cutout != null && def.cutout.Count >= 3)
            {
                return BuildCutoutSegMesh(def, item, i, clipTop);
            }
            var segs = item.segs;
            var span = segs.Count;
            var holeW = Mathf.Min(def.openWidth, (span * tileSize) - 0.05f);
            var holeStart = ((span * tileSize) - holeW) * 0.5f; // measured along the run from its start node
            var holeEnd = holeStart + holeW;
            var y0 = Mathf.Min(Mathf.Max(0f, def.holeY0), wallHeight - 0.1f); // keep the sill below the wall top
            var y1 = Mathf.Clamp(def.holeY1, y0 + 0.1f, wallHeight); // full-height hole ⇒ no lintel emitted

            var a0 = Mathf.Max(0f, holeStart - (i * tileSize));
            var a1 = Mathf.Min(tileSize, holeEnd - (i * tileSize));
            if (a1 - a0 < 0.02f)
            {
                return null; // covered by the item but the hole doesn't reach this segment
            }

            var (negX, posX, wedges) = SegmentEnds(segs[i]); // hole clamps to the mitered core

            // Segment-local X: o=0 has local +X along the run; o=1's 90° yaw maps local +X to
            // world −Z, i.e. AGAINST the run — the interval flips.
            float hx0, hx1;
            if (segs[i].z == 1)
            {
                hx0 = (tileSize * 0.5f) - a1;
                hx1 = (tileSize * 0.5f) - a0;
            }
            else
            {
                hx0 = a0 - (tileSize * 0.5f);
                hx1 = a1 - (tileSize * 0.5f);
            }
            hx0 = Mathf.Max(hx0, negX);
            hx1 = Mathf.Min(hx1, posX);
            if (hx1 - hx0 < 0.02f)
            {
                return null; // clamping to the mitered core degenerated the hole — stay solid
            }

            var boxes = new List<(float, float, float, float)>();
            if (hx0 > negX + 0.005f)
            {
                boxes.Add((negX, hx0, 0f, wallHeight)); // jamb wall
            }
            if (hx1 < posX - 0.005f)
            {
                boxes.Add((hx1, posX, 0f, wallHeight)); // jamb wall
            }
            if (y0 > 0.005f)
            {
                boxes.Add((hx0, hx1, 0f, y0)); // sill wall under a window
            }
            if (y1 < wallHeight - 0.005f)
            {
                boxes.Add((hx0, hx1, y1, wallHeight)); // lintel above
            }
            return BuildWallBoxesMesh(tileSize, wallHeight, wallThickness, boxes.ToArray(), 0f, null, wedges, clipTop);
        }

        // Cut the game's exact silhouette: full-height jambs beside the hole, then vertical
        // STRIPS between polygon-vertex x's — each strip a sill trapezoid (floor → lower
        // boundary) and a lintel trapezoid (upper boundary → wall top), so slanted/arched
        // edges are reproduced exactly (a rect polygon degenerates to the plain jamb/sill/
        // lintel boxes). Strips emit no end caps: the jamb boxes' caps line the hole sides.
        private Mesh BuildCutoutSegMesh(ItemDef def, PlacedWallItem item, int i, float clipTop = float.PositiveInfinity)
        {
            var segs = item.segs;
            var spanCenter = segs.Count * tileSize * 0.5f;
            var o = segs[i].z;
            var (negX, posX, wedges) = SegmentEnds(segs[i]); // hole clips to the mitered core

            // Item-local polygon → segment-local X. The item and the wall mesh share the same
            // base yaw (0 or 90), so their mirrors CANCEL: the polygon carries over with only
            // the segment-offset shift (o==1 shifts from the other end), and the item's flip
            // (its extra 180°) is the one true mirror. Y gets the item's sill lift.
            var baseOff = o == 1
                ? ((i + 0.5f) * tileSize) - spanCenter
                : spanCenter - ((i + 0.5f) * tileSize);
            var sign = item.flip == 1 ? -1f : 1f;
            var poly = new List<Vector2>(def.cutout.Count);
            foreach (var p in def.cutout)
            {
                poly.Add(new Vector2(baseOff + (sign * p.x), Mathf.Clamp(p.y + def.liftY, 0f, wallHeight)));
            }
            poly = ClipPolyX(poly, negX, posX); // multi-tile spans split the hole at node planes
            if (poly.Count < 3)
            {
                return null; // the hole lies entirely on other segments
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var p in poly)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
            }

            var boxes = new List<(float, float, float, float)>();
            if (minX > negX + 0.005f)
            {
                boxes.Add((negX, minX, 0f, wallHeight)); // jamb wall (its cap lines the hole side)
            }
            if (maxX < posX - 0.005f)
            {
                boxes.Add((maxX, posX, 0f, wallHeight)); // jamb wall
            }

            // Strip breakpoints at every vertex x. Within a strip each boundary is one straight
            // polygon edge, so sampling its ends reproduces the silhouette exactly.
            var xs = new List<float>(poly.Count);
            foreach (var p in poly)
            {
                xs.Add(p.x);
            }
            xs.Sort();
            var traps = new List<(float xa, float xb, float y0a, float y0b, float y1a, float y1b)>();
            for (var s = 0; s + 1 < xs.Count; s++)
            {
                float xa = xs[s], xb = xs[s + 1];
                if (xb - xa < 0.0005f)
                {
                    continue; // duplicate breakpoint
                }
                var mid = (xa + xb) * 0.5f;
                float Boundary(float x, bool upper)
                {
                    var best = upper ? float.MinValue : float.MaxValue;
                    for (var e = 0; e < poly.Count; e++)
                    {
                        var a = poly[e];
                        var b = poly[(e + 1) % poly.Count];
                        if (Mathf.Abs(b.x - a.x) < 1e-5f || mid < Mathf.Min(a.x, b.x) || mid > Mathf.Max(a.x, b.x))
                        {
                            continue; // vertical edge, or edge not spanning this strip
                        }
                        var y = a.y + ((b.y - a.y) * Mathf.Clamp01((x - a.x) / (b.x - a.x)));
                        best = upper ? Mathf.Max(best, y) : Mathf.Min(best, y);
                    }
                    return Mathf.Clamp(best, 0f, wallHeight);
                }
                float lowA = Boundary(xa, false), lowB = Boundary(xb, false);
                float highA = Boundary(xa, true), highB = Boundary(xb, true);
                if (highA - lowA < 0.001f && highB - lowB < 0.001f)
                {
                    continue; // hole has no height in this strip
                }
                if (lowA > 0.005f || lowB > 0.005f)
                {
                    traps.Add((xa, xb, 0f, 0f, lowA, lowB)); // sill: floor → lower boundary
                }
                if (highA < wallHeight - 0.005f || highB < wallHeight - 0.005f)
                {
                    traps.Add((xa, xb, highA, highB, wallHeight, wallHeight)); // lintel: upper boundary → top
                }
            }
            return BuildWallBoxesMesh(tileSize, wallHeight, wallThickness, boxes.ToArray(), 0f, traps, wedges, clipTop);
        }

        // Sutherland–Hodgman clip of a polygon against x >= lo and x <= hi.
        private static List<Vector2> ClipPolyX(List<Vector2> poly, float lo, float hi)
        {
            List<Vector2> ClipHalf(List<Vector2> pts, float bound, bool keepGreater)
            {
                var res = new List<Vector2>(pts.Count + 2);
                for (var i = 0; i < pts.Count; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % pts.Count];
                    var aIn = keepGreater ? a.x >= bound : a.x <= bound;
                    var bIn = keepGreater ? b.x >= bound : b.x <= bound;
                    if (aIn)
                    {
                        res.Add(a);
                    }
                    if (aIn != bIn && Mathf.Abs(b.x - a.x) > 1e-6f)
                    {
                        var t = (bound - a.x) / (b.x - a.x);
                        res.Add(new Vector2(bound, a.y + ((b.y - a.y) * t)));
                    }
                }
                return res;
            }
            var clipped = ClipHalf(poly, lo, keepGreater: true);
            return clipped.Count == 0 ? clipped : ClipHalf(clipped, hi, keepGreater: false);
        }

        // Re-derive the mesh a segment should wear right now (solid or opening, miter-aware).
        // Every segment owns its generated mesh (customMesh), destroyed on each rebuild.
        private void RefreshSegmentMesh(Vector3Int key)
        {
            if (!_walls.TryGetValue(key, out var seg))
            {
                return;
            }
            Mesh mesh = null;
            if (seg.opening != 0)
            {
                var owner = _wallItems.Find(wi => wi.segs.Contains(key));
                var def = owner != null ? FindDef(owner.id) : null;
                if (def != null)
                {
                    mesh = BuildOpeningSegMesh(def, owner, owner.segs.IndexOf(key));
                }
                else
                {
                    seg.opening = 0; // orphaned opening (no owner item) — heal fully, don't block future items
                }
            }
            if (mesh == null)
            {
                mesh = BuildSolidSegMesh(key);
            }
            if (seg.customMesh != null)
            {
                Destroy(seg.customMesh);
            }
            if (seg.stubMesh != null)
            {
                Destroy(seg.stubMesh); // stale — regenerated from the new full mesh on demand
                seg.stubMesh = null;
            }
            seg.customMesh = mesh;
            // A wall dropped for cutaway keeps a stub clamped from this same full mesh; the full mesh
            // is cached in customMesh and restored when the wall comes back up. Route through
            // SetWallLowered so the full-height shadow ghost tracks the rebuilt mesh.
            SetWallLowered(seg, key, seg.lowered);
        }

        // ---- cutaway: drop near/foreground walls so they don't hide the room -------------------

        // Show/hide a wall as a lowered cutaway stub. Engine requirement R3: lowering is a RENDER-only
        // decision — the room must stay lit as if the wall were full height. So when lowered we render
        // the short stub with shadows OFF and keep a full-height INVISIBLE (ShadowsOnly) ghost that
        // casts the whole wall's shadow (with any door/window opening). The collider is already
        // full-height on `go`, so Sim is unchanged too. See docs/home-editor-engine.md.
        private void SetWallLowered(WallSeg seg, Vector3Int key, bool lowered)
        {
            if (seg.go == null)
            {
                return;
            }
            seg.lowered = lowered;
            var mf = seg.go.GetComponent<MeshFilter>();
            var mr = seg.go.GetComponent<MeshRenderer>();
            var full = seg.customMesh != null ? seg.customMesh : FullMeshFor(key.z);
            if (lowered)
            {
                mf.sharedMesh = StubFor(seg, key);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // the stub casts no shadow
                if (seg.shadowGhost == null)
                {
                    var g = new GameObject("__wallShadow");
                    g.transform.SetParent(seg.go.transform, false); // identity local → matches the wall's transform
                    g.AddComponent<MeshFilter>();
                    var gmr = g.AddComponent<MeshRenderer>();
                    gmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    gmr.sharedMaterial = mr.sharedMaterial; // ShadowsOnly still needs a material for the shadow pass
                    seg.shadowGhost = g;
                }
                seg.shadowGhost.GetComponent<MeshFilter>().sharedMesh = full; // full-height, keeps openings
                seg.shadowGhost.SetActive(true);
            }
            else
            {
                mf.sharedMesh = full;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (seg.shadowGhost != null)
                {
                    seg.shadowGhost.SetActive(false);
                }
            }
        }

        // Recompute which walls drop to the stub. `force` recomputes even when the camera hasn't
        // turned (after a wall add/remove, a level change, or a mode toggle).
        private void RefreshCutaway(bool force)
        {
            if (!force && Mathf.Abs(Mathf.DeltaAngle(_yaw, _lastCutYaw)) < 1.5f)
            {
                return;
            }
            _lastCutYaw = _yaw;

            var fwd3 = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward; // camera → scene, on the ground
            var f = new Vector2(fwd3.x, fwd3.z).normalized;
            var focus = new Vector2(_focus.x, _focus.z);

            foreach (var lv in _levels)
            {
                foreach (var kv in lv.walls)
                {
                    var seg = kv.Value;
                    var lower = _cutaway && ShouldLower(kv.Key, f, focus);
                    if ((lower != seg.lowered || force) && seg.go != null)
                    {
                        SetWallLowered(seg, kv.Key, lower);
                    }
                }
                // A junction cap would float above a dropped wall: hide it only when EVERY incident
                // wall is lowered (a still-full wall at that node still needs its cap).
                foreach (var capKv in lv.nodeCaps)
                {
                    if (capKv.Value == null)
                    {
                        continue;
                    }
                    bool any = false, allLowered = true;
                    foreach (var (koff, _) in NodeIncidence)
                    {
                        var wk = new Vector3Int(capKv.Key.x + koff.x, capKv.Key.y + koff.y, koff.z);
                        if (lv.walls.TryGetValue(wk, out var ws))
                        {
                            any = true;
                            if (!ws.lowered)
                            {
                                allLowered = false;
                            }
                        }
                    }
                    capKv.Value.SetActive(!(_cutaway && any && allLowered));
                }
            }
        }

        // ---- rooms: a closed wall loop encloses cells → a ShadowsOnly ceiling (engine req R2) --------

        // Flood-fill the cell grid from outside the walls; cells the flood can't reach are enclosed.
        // Enclosed cells get a ceiling that shadows the interior (from the sky/sun) and contains lamp
        // light, but is never drawn — so a cut-open, ceiling-less-looking room stays lit like a real
        // room. Axis-aligned walls only (diagonals are ignored for enclosure in v1). See
        // docs/home-editor-engine.md.
        private void RefreshRooms(int levelIndex)
        {
            if (levelIndex < 0 || levelIndex >= _levels.Count)
            {
                return;
            }
            var lv = _levels[levelIndex];
            if (lv.walls.Count == 0)
            {
                BuildCeiling(lv, null, 0f);
                return;
            }
            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            foreach (var key in lv.walls.Keys)
            {
                if (key.x < minX) { minX = key.x; }
                if (key.x > maxX) { maxX = key.x; }
                if (key.y < minZ) { minZ = key.y; }
                if (key.y > maxZ) { maxZ = key.y; }
            }
            minX -= 2; maxX += 2; minZ -= 2; maxZ += 2; // margin so the flood's outer ring is wall-free

            // Is the edge between cell a and its neighbour (a + dir) walled?
            bool Blocked(Vector2Int a, int dx, int dz) =>
                dx == 1 ? lv.walls.ContainsKey(new Vector3Int(a.x + 1, a.y, 1))
              : dx == -1 ? lv.walls.ContainsKey(new Vector3Int(a.x, a.y, 1))
              : dz == 1 ? lv.walls.ContainsKey(new Vector3Int(a.x, a.y + 1, 0))
              : lv.walls.ContainsKey(new Vector3Int(a.x, a.y, 0)); // dz == -1

            var exterior = new HashSet<Vector2Int>();
            var stack = new Stack<Vector2Int>();
            void Seed(Vector2Int c) { if (exterior.Add(c)) { stack.Push(c); } }
            for (var x = minX; x <= maxX; x++) { Seed(new Vector2Int(x, minZ)); Seed(new Vector2Int(x, maxZ)); }
            for (var z = minZ; z <= maxZ; z++) { Seed(new Vector2Int(minX, z)); Seed(new Vector2Int(maxX, z)); }
            var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                foreach (var (dx, dz) in dirs)
                {
                    var n = new Vector2Int(c.x + dx, c.y + dz);
                    if (n.x < minX || n.x > maxX || n.y < minZ || n.y > maxZ || exterior.Contains(n) || Blocked(c, dx, dz))
                    {
                        continue;
                    }
                    exterior.Add(n);
                    stack.Push(n);
                }
            }

            var enclosed = new List<Vector2Int>();
            for (var x = minX + 1; x < maxX; x++)
            {
                for (var z = minZ + 1; z < maxZ; z++)
                {
                    var c = new Vector2Int(x, z);
                    if (!exterior.Contains(c))
                    {
                        enclosed.Add(c);
                    }
                }
            }
            BuildCeiling(lv, enclosed, levelIndex * wallHeight + wallHeight);
        }

        // (Re)build the level's ceiling mesh over the enclosed cells. DOUBLE-SIDED so it shadows the
        // interior from the sun above AND contains lamp light from below. ShadowsOnly = invisible.
        private void BuildCeiling(HomeLevel lv, List<Vector2Int> cells, float y)
        {
            if (lv.ceiling != null)
            {
                var oldMesh = lv.ceiling.GetComponent<MeshFilter>()?.sharedMesh;
                if (oldMesh != null) { Destroy(oldMesh); }
                Destroy(lv.ceiling);
                lv.ceiling = null;
            }
            if (cells == null || cells.Count == 0)
            {
                return;
            }
            var verts = new List<Vector3>(cells.Count * 4);
            var tris = new List<int>(cells.Count * 12);
            foreach (var c in cells)
            {
                var b = verts.Count;
                verts.Add(new Vector3(c.x * tileSize, y, c.y * tileSize));
                verts.Add(new Vector3((c.x + 1) * tileSize, y, c.y * tileSize));
                verts.Add(new Vector3((c.x + 1) * tileSize, y, (c.y + 1) * tileSize));
                verts.Add(new Vector3(c.x * tileSize, y, (c.y + 1) * tileSize));
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
            var mesh = new Mesh { name = "ceiling" };
            if (verts.Count > 65000) { mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; }
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Ceiling");
            go.transform.SetParent(lv.root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly; // invisible, shadows only
            mr.sharedMaterial = ResolveWallMaterial(null); // opaque material for the shadow pass
            lv.ceiling = go;
        }

        // A wall drops when its OUTWARD face (the side facing away from the room-center focus)
        // points back toward the camera — i.e. it is a near/foreground wall that would hide the
        // interior. Far walls (outward face pointing away from the camera) stay up as a backdrop.
        private bool ShouldLower(Vector3Int key, Vector2 f, Vector2 focus)
        {
            var (a, b) = SegmentEndpoints(key);
            var wa = new Vector2(a.x, a.y) * tileSize;
            var wb = new Vector2(b.x, b.y) * tileSize;
            var mid = (wa + wb) * 0.5f;
            var d = (wb - wa).normalized;
            var n = new Vector2(-d.y, d.x);                 // horizontal wall-plane normal
            if (Vector2.Dot(n, mid - focus) < 0f)
            {
                n = -n;                                     // the face pointing AWAY from the focus
            }
            return Vector2.Dot(n, f) < -0.15f;              // that outward face looks toward the camera
        }

        private void RemoveWallItem(PlacedWallItem item)
        {
            foreach (var key in item.segs)
            {
                if (_walls.TryGetValue(key, out var seg))
                {
                    seg.opening = 0;
                    RefreshSegmentMesh(key); // heals to the solid mitered mesh
                }
            }
            if (item.go != null)
            {
                Destroy(item.go);
            }
            _wallItems.Remove(item);
            if (_selectedWallItem == item)
            {
                _selectedWallItem = null;
            }
        }

        private static (int w, int d) RotatedDims(ItemDef def, int rot) =>
            (rot & 1) == 1 ? (def.footD, def.footW) : (def.footW, def.footD);

        private bool CellsFree(Vector2Int origin, int w, int d)
        {
            for (var dx = 0; dx < w; dx++)
            {
                for (var dz = 0; dz < d; dz++)
                {
                    if (_occupied.ContainsKey(new Vector2Int(origin.x + dx, origin.y + dz)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void RegisterCells(Placed p)
        {
            for (var dx = 0; dx < p.w; dx++)
            {
                for (var dz = 0; dz < p.d; dz++)
                {
                    _occupied[new Vector2Int(p.x + dx, p.z + dz)] = p;
                }
            }
        }

        private void FreeCells(Placed p)
        {
            for (var dx = 0; dx < p.w; dx++)
            {
                for (var dz = 0; dz < p.d; dz++)
                {
                    var key = new Vector2Int(p.x + dx, p.z + dz);
                    if (_occupied.TryGetValue(key, out var owner) && owner == p)
                    {
                        _occupied.Remove(key);
                    }
                }
            }
        }

        private Vector3 RectCenter(Vector2Int origin, int w, int d) =>
            new((origin.x + (w * 0.5f)) * tileSize, LevelY, (origin.y + (d * 0.5f)) * tileSize);

        private void BeginPlacement(ItemDef def, Placed moving = null)
        {
            CancelPlacement();
            _placing = def;
            _movingExisting = moving;
            _ghostRot = moving?.rot ?? 0;
            // A portal (template null) has no object to preview — an empty ghost keeps every
            // _ghost.transform call safe; its wall span is shown by the band highlight instead.
            _ghost = def.template != null ? Instantiate(def.template, placedRoot) : new GameObject($"ghost_{def.id}");
            _ghost.name = $"ghost_{def.id}";
            _ghost.SetActive(true);
            SetCollidersEnabled(_ghost, false);
            if (moving != null)
            {
                moving.go.SetActive(false);
                FreeCells(moving);
            }
            _selected = null;
            _selectedWallItem = null; // stale wall-item selection + Delete key mid-placement would silently destroy it
            var (w, d) = RotatedDims(def, _ghostRot);
            _status = $"Placing {def.label} ({w}x{d}): click places, R rotates, right-click cancels.";
        }

        private void UpdateGhost()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                _ghostRot = (_ghostRot + 1) & 3;
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelPlacement();
                return;
            }

            if (!RaycastGround(out var hit))
            {
                return;
            }

            var (w, d) = RotatedDims(_placing, _ghostRot);
            var origin = new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(hit.x / tileSize) - ((w - 1) / 2), -gridExtent, gridExtent - w),
                Mathf.Clamp(Mathf.FloorToInt(hit.z / tileSize) - ((d - 1) / 2), -gridExtent, gridExtent - d));
            _ghostValid = CellsFree(origin, w, d);
            _ghost.transform.SetPositionAndRotation(RectCenter(origin, w, d), Quaternion.Euler(0f, _ghostRot * 90f, 0f));
            UpdateCellHighlightRect(origin, w, d, perCellOccupancy: true);

            if (Input.GetMouseButtonDown(0))
            {
                if (!_ghostValid)
                {
                    _status = "Blocked — those cells are occupied.";
                    return;
                }
                if (_movingExisting != null)
                {
                    _movingExisting.x = origin.x;
                    _movingExisting.z = origin.y;
                    _movingExisting.rot = _ghostRot;
                    (_movingExisting.w, _movingExisting.d) = (w, d);
                    _movingExisting.go.transform.SetPositionAndRotation(_ghost.transform.position, _ghost.transform.rotation);
                    _movingExisting.go.SetActive(true);
                    RegisterCells(_movingExisting);
                    var moved = _movingExisting;
                    _movingExisting = null;
                    _status = $"Moved {_placing.label}.";
                    CancelPlacement();
                    _selected = moved;
                }
                else
                {
                    var def = _placing;
                    var go = Instantiate(def.template, Lv.itemsRoot);
                    go.name = $"placed_{def.id}_{_placed.Count}";
                    go.SetActive(true);
                    go.transform.SetPositionAndRotation(_ghost.transform.position, _ghost.transform.rotation);
                    var p = new Placed { id = def.id, go = go, x = origin.x, z = origin.y, rot = _ghostRot, w = w, d = d };
                    _placed.Add(p);
                    RegisterCells(p);
                    _status = $"Placed {def.label} ({_placed.Count} object(s)).";
                }
            }
        }

        private void CancelPlacement()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
            }
            if (_movingExisting != null && _movingExisting.go != null)
            {
                _movingExisting.go.SetActive(true);
                RegisterCells(_movingExisting);
            }
            _ghost = null;
            _placing = null;
            _movingExisting = null;
            _wallItemFlip = 0;
            _wallItemSpan.Clear();
            HideCellHighlights();
            foreach (var ghost in _bandGhosts) // span/band previews share this pool
            {
                if (ghost != null)
                {
                    ghost.SetActive(false);
                }
            }
        }

        private static void SetCollidersEnabled(GameObject go, bool enabled)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = enabled;
            }
        }

        private void TrySelect()
        {
            var ray = editorCamera.ScreenPointToRay(Input.mousePosition);
            _selected = null;
            _selectedWallItem = null;
            // RaycastAll nearest-first (like RaycastActiveWall): a still-visible LOWER-level object
            // in front of the cursor must not swallow the click — skip past it to the active-level
            // object behind. Lower-level objects stay non-selectable, matching the level model.
            var hits = Physics.RaycastAll(ray, 200f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                foreach (var wi in _wallItems) // doors/windows first — they sit on walls
                {
                    if (wi.go != null && hit.collider.transform.IsChildOf(wi.go.transform))
                    {
                        _selectedWallItem = wi;
                        _status = $"Selected {wi.id} (wall item).";
                        return;
                    }
                }
                foreach (var p in _placed)
                {
                    if (p.go != null && hit.collider.transform.IsChildOf(p.go.transform))
                    {
                        _selected = p;
                        _status = $"Selected {p.id} at ({p.x},{p.z}) {p.w}x{p.d}.";
                        return;
                    }
                }
                // A PORTAL has no object to click — clicking the wall that carries it selects it,
                // so Delete removes the opening and heals the wall. (Doors/windows are selected by
                // their own GO above; a wall hit only falls through to an opening-less-GO owner.)
                if (_wallByCollider.TryGetValue(hit.collider, out var wallKey))
                {
                    var owner = _wallItems.Find(wi => wi.go == null && wi.segs.Contains(wallKey));
                    if (owner != null)
                    {
                        _selectedWallItem = owner;
                        _status = $"Selected {owner.id} (portal) — Delete removes it.";
                        return;
                    }
                }
            }
        }

        private void DeleteSelected()
        {
            if (_selected == null)
            {
                return;
            }
            FreeCells(_selected);
            DestroyPlacedObject(_selected);
            _placed.Remove(_selected);
            _status = "Deleted.";
            _selected = null;
        }

        // Destroy(go) does NOT free instance materials — recoloured objects would leak their
        // material clones until the Play session ends.
        private static void DestroyPlacedObject(Placed p)
        {
            if (p.go == null)
            {
                return;
            }
            if (p.swatchAppliedTex != null)
            {
                foreach (var renderer in p.go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    foreach (var m in renderer.materials)
                    {
                        if (m != null)
                        {
                            Destroy(m);
                        }
                    }
                }
            }
            Destroy(p.go);
        }

        private void RotateSelected()
        {
            var def = FindDef(_selected.id);
            if (def == null)
            {
                return;
            }
            var newRot = (_selected.rot + 1) & 3;
            var (w, d) = RotatedDims(def, newRot);
            var origin = new Vector2Int(
                Mathf.Clamp(_selected.x, -gridExtent, gridExtent - w),
                Mathf.Clamp(_selected.z, -gridExtent, gridExtent - d));
            FreeCells(_selected);
            if (!CellsFree(origin, w, d))
            {
                RegisterCells(_selected);
                _status = "No room to rotate here.";
                return;
            }
            _selected.rot = newRot;
            _selected.x = origin.x;
            _selected.z = origin.y;
            (_selected.w, _selected.d) = (w, d);
            _selected.go.transform.SetPositionAndRotation(RectCenter(origin, w, d), Quaternion.Euler(0f, newRot * 90f, 0f));
            RegisterCells(_selected);
        }

        private ItemDef FindDef(string id) =>
            items.Find(i => string.Equals(i.id, id, StringComparison.OrdinalIgnoreCase));

        // ---- swatches ---------------------------------------------------------------------------

        private void ApplySwatch(Placed p, int swatchIndex)
        {
            var def = FindDef(p.id);
            if (def == null || swatchIndex < 0 || swatchIndex >= def.swatches.Count)
            {
                return;
            }
            p.swatch = swatchIndex;
            var tex = def.swatches[swatchIndex].diffuse;
            if (tex == null)
            {
                return;
            }
            // Replace only the materials that carry the item's PRIMARY diffuse (glass/frame/extra
            // materials keep theirs). Pre-scan sharedMaterials so renderers with no match never get
            // instance clones (renderer.materials CLONES eagerly and the clones leak until session
            // end). Null-guard: a null primaryTexture must not match texture-less tinted materials.
            bool Matches(Material m)
            {
                if (m == null || !m.HasProperty("_BaseColorMap"))
                {
                    return false;
                }
                var current = m.GetTexture("_BaseColorMap");
                return current != null && (current == def.primaryTexture || current == p.swatchAppliedTex);
            }
            foreach (var renderer in p.go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var anyMatch = false;
                foreach (var shared in renderer.sharedMaterials)
                {
                    if (Matches(shared))
                    {
                        anyMatch = true;
                        break;
                    }
                }
                if (!anyMatch)
                {
                    continue;
                }
                var mats = renderer.materials; // instance clones — only on renderers that need them
                for (var i = 0; i < mats.Length; i++)
                {
                    if (Matches(mats[i]))
                    {
                        mats[i].SetTexture("_BaseColorMap", tex);
                    }
                }
                renderer.materials = mats;
            }
            p.swatchAppliedTex = tex;
        }

        // ================================ WALLS (rubber band) ===================================

        // First wall hit that belongs to the ACTIVE level (lower levels stay visible and their
        // colliders stay live — RaycastAll skips past them instead of letting them eat the click).
        private bool RaycastActiveWall(out Vector3Int key)
        {
            var ray = editorCamera.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 200f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (_wallByCollider.TryGetValue(h.collider, out key))
                {
                    return true;
                }
            }
            key = default;
            return false;
        }

        private bool RaycastGround(out Vector3 point)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, LevelY, 0f)); // the active level's floor plane
            var ray = editorCamera.ScreenPointToRay(Input.mousePosition);
            if (plane.Raycast(ray, out var t))
            {
                point = ray.GetPoint(t);
                return true;
            }
            point = default;
            return false;
        }

        private Vector2Int NearestNode(Vector3 p) => new(
            Mathf.Clamp(Mathf.RoundToInt(p.x / tileSize), -gridExtent, gridExtent),
            Mathf.Clamp(Mathf.RoundToInt(p.z / tileSize), -gridExtent, gridExtent));

        private void UpdateWallsMode()
        {
            if (_wallDrag && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
            {
                CancelWallDrag();
                return;
            }

            if (!RaycastGround(out var p))
            {
                return;
            }

            if (!_wallDrag)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    _wallDrag = true;
                    _wallAnchor = NearestNode(p);
                }
                return;
            }

            // Stretch: snap the run to the best of 8 directions from the anchor, integer length.
            var target = NearestNode(p);
            ComputeBand(_wallAnchor, target);
            UpdateBandGhosts();

            if (Input.GetMouseButtonUp(0))
            {
                foreach (var key in _bandSegments)
                {
                    if (_erase)
                    {
                        RemoveWall(key);
                    }
                    else
                    {
                        AddWall(key, null);
                    }
                }
                _status = _erase ? $"Removed {_bandSegments.Count} segment(s)." : $"Placed {_bandSegments.Count} wall segment(s). {_walls.Count} total.";
                CancelWallDrag();
            }
        }

        // Straight or 45° run from anchor toward target — the game's wall-stretch behavior.
        private void ComputeBand(Vector2Int a, Vector2Int b)
        {
            _bandSegments.Clear();
            var dx = b.x - a.x;
            var dz = b.y - a.y;
            if (dx == 0 && dz == 0)
            {
                return;
            }

            int stepX, stepZ, length;
            if (Mathf.Abs(dx) >= 2 * Mathf.Abs(dz)) // horizontal
            {
                stepX = Math.Sign(dx);
                stepZ = 0;
                length = Mathf.Abs(dx);
            }
            else if (Mathf.Abs(dz) >= 2 * Mathf.Abs(dx)) // vertical
            {
                stepX = 0;
                stepZ = Math.Sign(dz);
                length = Mathf.Abs(dz);
            }
            else // diagonal 45°
            {
                stepX = Math.Sign(dx);
                stepZ = Math.Sign(dz);
                length = Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dz));
            }

            var node = a;
            for (var i = 0; i < length; i++)
            {
                var next = new Vector2Int(node.x + stepX, node.y + stepZ);
                if (next.x < -gridExtent || next.x > gridExtent || next.y < -gridExtent || next.y > gridExtent)
                {
                    break;
                }
                var key = SegmentKey(node, next);
                _bandSegments.Add(key);
                node = next;
            }
        }

        // Canonical key for the segment between two adjacent (incl. diagonal) nodes.
        private static Vector3Int SegmentKey(Vector2Int a, Vector2Int b)
        {
            var dx = b.x - a.x;
            var dz = b.y - a.y;
            if (dx == 1 && dz == 0) return new Vector3Int(a.x, a.y, 0);
            if (dx == -1 && dz == 0) return new Vector3Int(b.x, b.y, 0);
            if (dx == 0 && dz == 1) return new Vector3Int(a.x, a.y, 1);
            if (dx == 0 && dz == -1) return new Vector3Int(b.x, b.y, 1);
            if (dx == 1 && dz == 1) return new Vector3Int(a.x, a.y, 2);       // NE diag from (x,z)
            if (dx == -1 && dz == -1) return new Vector3Int(b.x, b.y, 2);
            if (dx == -1 && dz == 1) return new Vector3Int(a.x - 1, a.y, 3);  // NW diag keyed at (x,z): (x+1,z)→(x,z+1)
            return new Vector3Int(b.x - 1, b.y, 3);                            // dx==1, dz==-1
        }

        private (Vector3 pos, Quaternion rot, float len) SegmentTransform(Vector3Int key) => key.z switch
        {
            0 => (new Vector3((key.x + 0.5f) * tileSize, LevelY, key.y * tileSize), Quaternion.identity, tileSize),
            1 => (new Vector3(key.x * tileSize, LevelY, (key.y + 0.5f) * tileSize), Quaternion.Euler(0f, 90f, 0f), tileSize),
            2 => (new Vector3((key.x + 0.5f) * tileSize, LevelY, (key.y + 0.5f) * tileSize), Quaternion.Euler(0f, -45f, 0f), tileSize * 1.41421356f),
            _ => (new Vector3((key.x + 0.5f) * tileSize, LevelY, (key.y + 0.5f) * tileSize), Quaternion.Euler(0f, 45f, 0f), tileSize * 1.41421356f),
        };

        // The two lattice nodes a segment connects (for corner-post refcounting).
        private static (Vector2Int a, Vector2Int b) SegmentEndpoints(Vector3Int key) => key.z switch
        {
            0 => (new Vector2Int(key.x, key.y), new Vector2Int(key.x + 1, key.y)),
            1 => (new Vector2Int(key.x, key.y), new Vector2Int(key.x, key.y + 1)),
            2 => (new Vector2Int(key.x, key.y), new Vector2Int(key.x + 1, key.y + 1)),
            _ => (new Vector2Int(key.x + 1, key.y), new Vector2Int(key.x, key.y + 1)),
        };

        // The 8 segment keys that can touch a node, with their integer direction AWAY from it.
        private static readonly (Vector3Int keyOffset, Vector2Int dir)[] NodeIncidence =
        {
            (new Vector3Int(0, 0, 0), new Vector2Int(1, 0)),
            (new Vector3Int(-1, 0, 0), new Vector2Int(-1, 0)),
            (new Vector3Int(0, 0, 1), new Vector2Int(0, 1)),
            (new Vector3Int(0, -1, 1), new Vector2Int(0, -1)),
            (new Vector3Int(0, 0, 2), new Vector2Int(1, 1)),
            (new Vector3Int(-1, -1, 2), new Vector2Int(-1, -1)),
            (new Vector3Int(-1, 0, 3), new Vector2Int(-1, 1)),
            (new Vector3Int(0, -1, 3), new Vector2Int(1, -1)),
        };

        private void CollectIncidentWalls(Vector2Int node, List<(Vector3Int key, Vector2Int dir)> into)
        {
            into.Clear();
            foreach (var (off, dir) in NodeIncidence)
            {
                var key = new Vector3Int(node.x + off.x, node.y + off.y, off.z);
                if (_walls.ContainsKey(key))
                {
                    into.Add((key, dir));
                }
            }
        }

        private struct FanEntry
        {
            public Vector3Int key;
            public Vector2 dir;    // unit, AWAY from the node
            public float tPlus;    // miter distance along the wall on its CCW (+perp) side
            public float tMinus;   // ... on its CW (−perp) side
        }

        // The junction fan at a node: incident walls sorted CCW; each angularly-adjacent pair
        // meets at a shared miter vertex t = h/tan(Δ/2) along BOTH walls (Δ = the CCW angle
        // between them). Δ=π (collinear) ⇒ t=0, a flush butt; Δ=π/2 (L) ⇒ t=h, the classic 45°
        // miter seam; reflex pairs ⇒ negative t, the outer faces meet PAST the node. The miter
        // vertices in order form the junction cap polygon (degenerate for k≤2). k=1: flat cap.
        private void ComputeNodeFan(Vector2Int node, List<FanEntry> fan, List<Vector2> capPoly)
        {
            fan.Clear();
            capPoly.Clear();
            foreach (var (off, idir) in NodeIncidence)
            {
                var key = new Vector3Int(node.x + off.x, node.y + off.y, off.z);
                if (_walls.ContainsKey(key))
                {
                    fan.Add(new FanEntry { key = key, dir = new Vector2(idir.x, idir.y).normalized });
                }
            }
            if (fan.Count < 2)
            {
                return; // free end: tPlus/tMinus stay 0 (flat cap at the node plane)
            }
            fan.Sort((a, b) => Mathf.Atan2(a.dir.y, a.dir.x).CompareTo(Mathf.Atan2(b.dir.y, b.dir.x)));
            var h = wallThickness * 0.5f;
            for (var i = 0; i < fan.Count; i++)
            {
                var j = (i + 1) % fan.Count;
                var di = fan[i].dir;
                var delta = Mathf.Atan2(fan[j].dir.y, fan[j].dir.x) - Mathf.Atan2(di.y, di.x);
                if (delta <= 0f)
                {
                    delta += 2f * Mathf.PI;
                }
                // Grid directions are ≥45° apart ⇒ Δ/2 ∈ [22.5°, 157.5°] ⇒ |tan| ≥ 0.414; the
                // clamp only guards float noise at Δ≈π where tan → ±huge and t → ±0.
                var t = Mathf.Clamp(h / Mathf.Tan(delta * 0.5f), -3f * h, 3f * h);
                capPoly.Add((di * t) + (new Vector2(-di.y, di.x) * h));
                var ei = fan[i];
                ei.tPlus = t;
                fan[i] = ei;
                var ej = fan[j];
                ej.tMinus = t;
                fan[j] = ej;
            }
        }

        // A segment end's miter edge in MESH-LOCAL coords: the x of the end vertex on the local
        // +Z and −Z sides. At the local−X end the away-dir IS local +X and its CCW-perp IS local
        // +Z; at the local+X end both flip (n(−d) = −n(d)).
        private (float xPlusZ, float xMinusZ) EndEdgeLocal(Vector3Int key, bool negEnd)
        {
            var (negNode, posNode) = SegmentLocalEnds(key);
            ComputeNodeFan(negEnd ? negNode : posNode, _fanScratch, _capScratch);
            float tPlus = 0f, tMinus = 0f;
            foreach (var e in _fanScratch)
            {
                if (e.key == key)
                {
                    tPlus = e.tPlus;
                    tMinus = e.tMinus;
                    break;
                }
            }
            var half = (key.z >= 2 ? tileSize * 1.41421356f : tileSize) * 0.5f;
            return negEnd ? (-half + tPlus, -half + tMinus) : (half - tMinus, half - tPlus);
        }

        // Core = the rectangular, hole-cuttable middle; wedges = the mitered end pieces.
        private (float coreLo, float coreHi, List<(float xP, float xM, float xCore, bool atNeg)> wedges) SegmentEnds(Vector3Int key)
        {
            var (nP, nM) = EndEdgeLocal(key, negEnd: true);
            var (pP, pM) = EndEdgeLocal(key, negEnd: false);
            var coreLo = Mathf.Max(nP, nM);
            var coreHi = Mathf.Min(pP, pM);
            var wedges = new List<(float, float, float, bool)>(2);
            if (Mathf.Abs(nP - nM) > 0.0005f)
            {
                wedges.Add((nP, nM, coreLo, true));
            }
            if (Mathf.Abs(pP - pM) > 0.0005f)
            {
                wedges.Add((pP, pM, coreHi, false));
            }
            return (coreLo, coreHi, wedges);
        }

        private Mesh BuildSolidSegMesh(Vector3Int key, float clipTop = float.PositiveInfinity)
        {
            var (coreLo, coreHi, wedges) = SegmentEnds(key);
            var full = key.z >= 2 ? tileSize * 1.41421356f : tileSize;
            return BuildWallBoxesMesh(full, wallHeight, wallThickness,
                new[] { (coreLo, coreHi, 0f, wallHeight) }, 0f, null, wedges, clipTop);
        }

        // Junction cap: the miter-vertex polygon extruded as a top face at wall height (its side
        // planes are exactly the incident walls' slanted end faces, so only the top is exposed).
        private Mesh BuildNodeCapMesh(List<Vector2> poly)
        {
            var pts = new List<Vector2>(poly);
            var area = 0f;
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                area += (a.x * b.y) - (b.x * a.y);
            }
            if (area > 0f)
            {
                pts.Reverse(); // top faces render CW-in-plan viewed from above (box-top convention)
            }
            var verts = new List<Vector3>(pts.Count);
            var uvs = new List<Vector2>(pts.Count);
            foreach (var p in pts)
            {
                verts.Add(new Vector3(p.x, wallHeight, p.y));
                uvs.Add(new Vector2(p.x + 0.5f, 1f));
            }
            var tris = new List<int>();
            for (var i = 1; i + 1 < pts.Count; i++)
            {
                tris.Add(0);
                tris.Add(i);
                tris.Add(i + 1);
            }
            var mesh = new Mesh { name = "wall_joint_cap" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Re-evaluate a node after any wall change: rebuild its junction cap (3+ walls) and
        // re-derive every incident segment's mitered mesh.
        private void RefreshNode(Vector2Int node)
        {
            ComputeNodeFan(node, _fanScratch, _capScratch);
            if (_fanScratch.Count >= 3 && _capScratch.Count >= 3)
            {
                if (!_nodeCaps.TryGetValue(node, out var cap) || cap == null)
                {
                    cap = new GameObject($"joint_{node.x}_{node.y}");
                    cap.transform.SetParent(_wallsRoot, false);
                    cap.transform.position = new Vector3(node.x * tileSize, LevelY, node.y * tileSize);
                    cap.AddComponent<MeshFilter>();
                    cap.AddComponent<MeshRenderer>();
                    _nodeCaps[node] = cap;
                }
                var mf = cap.GetComponent<MeshFilter>();
                if (mf.sharedMesh != null)
                {
                    Destroy(mf.sharedMesh);
                }
                mf.sharedMesh = BuildNodeCapMesh(_capScratch);
                RefreshCapMaterial(node);
            }
            else if (_nodeCaps.TryGetValue(node, out var old))
            {
                if (old != null)
                {
                    var m = old.GetComponent<MeshFilter>().sharedMesh;
                    if (m != null)
                    {
                        Destroy(m);
                    }
                    Destroy(old);
                }
                _nodeCaps.Remove(node);
            }
            // Copy keys first: rebuilding a segment re-queries fans through the same scratch list.
            var keys = new List<Vector3Int>(_fanScratch.Count);
            foreach (var e in _fanScratch)
            {
                keys.Add(e.key);
            }
            foreach (var k in keys)
            {
                RefreshSegmentMesh(k);
            }
        }

        // The junction cap wears the majority covering of the walls it joins.
        private void RefreshCapMaterial(Vector2Int node)
        {
            if (!_nodeCaps.TryGetValue(node, out var cap) || cap == null)
            {
                return;
            }
            CollectIncidentWalls(node, _incScratch);
            string best = null;
            var bestCount = 0;
            var counts = new Dictionary<string, int>();
            foreach (var (key, _) in _incScratch)
            {
                var cov = _walls[key].covId;
                if (string.IsNullOrEmpty(cov))
                {
                    continue;
                }
                counts.TryGetValue(cov, out var n);
                counts[cov] = ++n;
                if (n > bestCount)
                {
                    bestCount = n;
                    best = cov;
                }
            }
            cap.GetComponent<MeshRenderer>().sharedMaterial = ResolveWallMaterial(best);
        }

        private void UpdateBandGhosts()
        {
            while (_bandGhosts.Count < _bandSegments.Count)
            {
                var ghost = new GameObject("__bandGhost");
                ghost.transform.SetParent(transform, false);
                ghost.AddComponent<MeshFilter>(); // mesh assigned per-frame below
                var mr = ghost.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _bandGhosts.Add(ghost);
            }
            for (var i = 0; i < _bandGhosts.Count; i++)
            {
                var ghost = _bandGhosts[i];
                if (i >= _bandSegments.Count)
                {
                    ghost.SetActive(false);
                    continue;
                }
                var key = _bandSegments[i];
                var (pos, rot, _) = SegmentTransform(key);
                ghost.SetActive(true);
                ghost.GetComponent<MeshFilter>().sharedMesh = FullMeshFor(key.z);
                ghost.transform.SetPositionAndRotation(pos, rot);
                ghost.transform.localScale = Vector3.one;
                ghost.GetComponent<MeshRenderer>().sharedMaterial = _erase ? cellBlockedMaterial : cellFreeMaterial;
            }
        }

        private void CancelWallDrag()
        {
            _wallDrag = false;
            _bandSegments.Clear();
            foreach (var ghost in _bandGhosts)
            {
                if (ghost != null)
                {
                    ghost.SetActive(false);
                }
            }
        }

        private void AddWall(Vector3Int key, string covId)
        {
            if (_walls.ContainsKey(key))
            {
                return;
            }
            // The two diagonals of one cell cross mid-span with no node between them — the miter
            // graph can't joint that, so refuse the X (draw a proper node-crossing instead).
            if (key.z >= 2 && _walls.ContainsKey(new Vector3Int(key.x, key.y, key.z == 2 ? 3 : 2)))
            {
                return;
            }
            var go = new GameObject($"wall_{key.x}_{key.y}_{key.z}");
            go.transform.SetParent(_wallsRoot, false);
            var (pos, rot, len) = SegmentTransform(key);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>(); // mesh assigned by the node refresh below
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ResolveWallMaterial(covId);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, wallHeight * 0.5f, 0f);
            col.size = new Vector3(len, wallHeight, wallThickness);
            _walls[key] = new WallSeg { go = go, covId = covId };
            _wallByCollider[col] = key;
            var (na, nb) = SegmentEndpoints(key);
            RefreshNode(na); // refreshes this segment's mesh too (it is incident to both nodes)
            RefreshNode(nb);
            if (_cutaway && !_bulkLoading)
            {
                RefreshCutaway(force: true); // set the new wall's near/far state immediately
            }
            if (!_bulkLoading)
            {
                RefreshRooms(_activeLevel); // this wall may have closed/opened a room
            }
        }

        private void RemoveWall(Vector3Int key)
        {
            if (!_walls.TryGetValue(key, out var seg))
            {
                return;
            }
            if (seg.opening != 0) // erasing a wall takes its door/window with it
            {
                var owner = _wallItems.Find(wi => wi.segs.Contains(key));
                if (owner != null)
                {
                    RemoveWallItem(owner);
                }
            }
            foreach (var c in seg.go.GetComponentsInChildren<Collider>())
            {
                _wallByCollider.Remove(c);
            }
            if (seg.customMesh != null)
            {
                Destroy(seg.customMesh);
            }
            if (seg.stubMesh != null)
            {
                Destroy(seg.stubMesh);
            }
            Destroy(seg.go);
            _walls.Remove(key);
            var (na, nb) = SegmentEndpoints(key);
            RefreshNode(na); // joint posts may dissolve; neighbours extend back to flush
            RefreshNode(nb);
            if (_cutaway && !_bulkLoading)
            {
                RefreshCutaway(force: true);
            }
            if (!_bulkLoading)
            {
                RefreshRooms(_activeLevel); // removing this wall may have opened a room
            }
        }

        private Material ResolveWallMaterial(string covId)
        {
            if (!string.IsNullOrEmpty(covId))
            {
                var cov = wallCoverings.Find(c => c.id == covId);
                if (cov?.material != null)
                {
                    return cov.material;
                }
            }
            return defaultWallMaterial;
        }

        // ================================ PAINT =================================================

        private void UpdateWallPaintMode()
        {
            if (!Input.GetMouseButton(0) || wallCoverings.Count == 0)
            {
                return;
            }
            if (RaycastActiveWall(out var key))
            {
                var cov = wallCoverings[Mathf.Clamp(_wallCovSel, 0, wallCoverings.Count - 1)];
                var seg = _walls[key];
                seg.covId = cov.id;
                seg.go.GetComponent<MeshRenderer>().sharedMaterial = cov.material;
                var (na, nb) = SegmentEndpoints(key);
                RefreshCapMaterial(na); // junction caps follow their walls' majority covering
                RefreshCapMaterial(nb);
            }
        }

        private void UpdateFloorPaintMode()
        {
            if (_floorDrag && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
            {
                _floorDrag = false;
                HideCellHighlights();
                return;
            }
            if (!RaycastGround(out var p))
            {
                return;
            }
            var cell = new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(p.x / tileSize), -gridExtent, gridExtent - 1),
                Mathf.Clamp(Mathf.FloorToInt(p.z / tileSize), -gridExtent, gridExtent - 1));

            if (!_floorDrag)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    _floorDrag = true;
                    _floorAnchor = cell;
                    _floorCursor = cell;
                }
                return;
            }

            _floorCursor = cell;
            var min = Vector2Int.Min(_floorAnchor, _floorCursor);
            var max = Vector2Int.Max(_floorAnchor, _floorCursor);
            UpdateCellHighlightRect(min, max.x - min.x + 1, max.y - min.y + 1, perCellOccupancy: false);

            if (Input.GetMouseButtonUp(0))
            {
                var count = 0;
                for (var x = min.x; x <= max.x; x++)
                {
                    for (var z = min.y; z <= max.y; z++)
                    {
                        var c = new Vector2Int(x, z);
                        if (_erase)
                        {
                            RemoveFloorTile(c);
                        }
                        else if (floorCoverings.Count > 0)
                        {
                            PaintFloorTile(c, floorCoverings[Mathf.Clamp(_floorCovSel, 0, floorCoverings.Count - 1)].id);
                        }
                        count++;
                    }
                }
                _status = _erase ? $"Erased {count} tile(s)." : $"Painted {count} tile(s).";
                _floorDrag = false;
                HideCellHighlights();
            }
        }

        private void PaintFloorTile(Vector2Int cell, string covId)
        {
            // Unknown covering ids keep the tile with a fallback material — dropping it here would
            // silently DELETE it from the next save. Mirrors how walls preserve unknown covIds.
            var cov = floorCoverings.Find(c => c.id == covId);
            var material = cov?.material != null ? cov.material : defaultWallMaterial;
            if (material == null)
            {
                return;
            }
            if (!_floorTiles.TryGetValue(cell, out var tile))
            {
                var quad = new GameObject($"floor_{cell.x}_{cell.y}");
                quad.transform.SetParent(_floorsRoot, false);
                quad.transform.position = new Vector3((cell.x + 0.5f) * tileSize, LevelY + 0.005f, (cell.y + 0.5f) * tileSize);
                quad.AddComponent<MeshFilter>().sharedMesh = TileMesh();
                var r = quad.AddComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tile = new FloorTile { go = quad };
                _floorTiles[cell] = tile;
            }
            tile.covId = covId;
            tile.go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // A floor tile with BOTH faces: the top is the walkable floor, the underside reads as the
        // ceiling of the level below. Top winding matches the wall box-top convention (CW in plan
        // viewed from above); the underside is the reverse.
        private Mesh TileMesh()
        {
            if (_tileMesh != null)
            {
                return _tileMesh;
            }
            var s = tileSize * 0.5f;
            var verts = new List<Vector3>
            {
                new(-s, 0f, -s), new(-s, 0f, s), new(s, 0f, s), new(s, 0f, -s), // top face ring
                new(-s, 0f, -s), new(-s, 0f, s), new(s, 0f, s), new(s, 0f, -s), // bottom face ring
            };
            var uvs = new List<Vector2>
            {
                new(0f, 0f), new(0f, 1f), new(1f, 1f), new(1f, 0f),
                new(0f, 0f), new(0f, 1f), new(1f, 1f), new(1f, 0f),
            };
            var tris = new List<int>
            {
                0, 1, 2, 0, 2, 3, // up (CW from above)
                4, 6, 5, 4, 7, 6, // down (reverse)
            };
            _tileMesh = new Mesh { name = "floor_tile" };
            _tileMesh.SetVertices(verts);
            _tileMesh.SetUVs(0, uvs);
            _tileMesh.SetTriangles(tris, 0);
            _tileMesh.RecalculateNormals();
            _tileMesh.RecalculateBounds();
            return _tileMesh;
        }

        private void RemoveFloorTile(Vector2Int cell)
        {
            if (_floorTiles.TryGetValue(cell, out var tile))
            {
                Destroy(tile.go);
                _floorTiles.Remove(cell);
            }
        }

        // ================================ CELL HIGHLIGHTS =======================================

        private void UpdateCellHighlightRect(Vector2Int origin, int w, int d, bool perCellOccupancy)
        {
            var needed = w * d;
            while (_cellPool.Count < needed)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "__cell";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(transform, worldPositionStays: false);
                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localScale = new Vector3(tileSize * 0.96f, tileSize * 0.96f, 1f);
                var r = quad.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                _cellPool.Add(quad);
            }

            var index = 0;
            for (var dx = 0; dx < w; dx++)
            {
                for (var dz = 0; dz < d; dz++)
                {
                    var cell = new Vector2Int(origin.x + dx, origin.y + dz);
                    var quad = _cellPool[index++];
                    quad.SetActive(true);
                    quad.transform.position = new Vector3((cell.x + 0.5f) * tileSize, LevelY + 0.012f, (cell.y + 0.5f) * tileSize);
                    var blocked = perCellOccupancy && _occupied.ContainsKey(cell);
                    quad.GetComponent<MeshRenderer>().sharedMaterial =
                        blocked || (!perCellOccupancy && _erase) ? cellBlockedMaterial : cellFreeMaterial;
                }
            }
            for (; index < _cellPool.Count; index++)
            {
                _cellPool[index].SetActive(false);
            }
        }

        private void HideCellHighlights()
        {
            foreach (var quad in _cellPool)
            {
                if (quad != null)
                {
                    quad.SetActive(false);
                }
            }
        }

        // ================================ SAVE / LOAD ===========================================

        private void SaveLayout()
        {
            var file = new LayoutFile();
            for (var lvl = 0; lvl < _levels.Count; lvl++)
            {
                var lv = _levels[lvl];
                foreach (var p in lv.placed)
                {
                    file.entries.Add(new LayoutEntry { id = p.id, x = p.x, z = p.z, rot = p.rot, swatch = p.swatch, lvl = lvl });
                }
                foreach (var kv in lv.walls)
                {
                    file.walls.Add(new WallEntry { x = kv.Key.x, z = kv.Key.y, o = kv.Key.z, cov = kv.Value.covId ?? string.Empty, lvl = lvl });
                }
                foreach (var kv in lv.floorTiles)
                {
                    file.floors.Add(new FloorEntry { x = kv.Key.x, z = kv.Key.y, cov = kv.Value.covId ?? string.Empty, lvl = lvl });
                }
                foreach (var wi in lv.wallItems)
                {
                    var first = wi.segs[0];
                    file.wallItems.Add(new WallItemEntry { id = wi.id, x = first.x, z = first.y, o = first.z, flip = wi.flip, lvl = lvl });
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath));
            File.WriteAllText(LayoutPath, JsonUtility.ToJson(file, prettyPrint: true));
            _status = $"Saved {file.entries.Count} object(s), {file.walls.Count} wall(s), {file.floors.Count} floor tile(s), {file.wallItems.Count} door/window(s) across {_levels.Count} level(s).";
        }

        private void LoadLayout()
        {
            if (!File.Exists(LayoutPath))
            {
                _status = "No saved layout yet.";
                return;
            }
            CancelPlacement();
            CancelWallDrag();
            _floorDrag = false;
            _selectedWallItem = null;
            _selected = null;
            _bulkLoading = true; // suppress per-wall cutaway recompute; do it once at the end
            // Tear down EVERY level wholesale: generated meshes and swatch material clones need
            // explicit Destroy; the level root then takes all GOs (walls, tiles, items) with it.
            foreach (var lv in _levels)
            {
                foreach (var p in lv.placed)
                {
                    DestroyPlacedObject(p); // frees swatch-instanced materials
                }
                foreach (var seg in lv.walls.Values)
                {
                    if (seg.customMesh != null)
                    {
                        Destroy(seg.customMesh);
                    }
                    if (seg.stubMesh != null)
                    {
                        Destroy(seg.stubMesh);
                    }
                }
                foreach (var cap in lv.nodeCaps.Values)
                {
                    if (cap != null)
                    {
                        var m = cap.GetComponent<MeshFilter>().sharedMesh;
                        if (m != null)
                        {
                            Destroy(m); // caps carry per-node generated meshes
                        }
                    }
                }
                if (lv.ceiling != null)
                {
                    var cm = lv.ceiling.GetComponent<MeshFilter>()?.sharedMesh;
                    if (cm != null) { Destroy(cm); }
                    lv.ceiling = null; // GO goes with lv.root below
                }
                if (lv.root != null)
                {
                    Destroy(lv.root);
                }
            }
            _levels.Clear();
            _activeLevel = 0;
            EnsureLevel(0);

            LayoutFile file;
            try
            {
                file = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(LayoutPath));
            }
            catch (Exception ex)
            {
                _status = $"Layout file unreadable ({ex.GetType().Name}).";
                return;
            }
            if (file == null)
            {
                _status = "Layout file unreadable.";
                return;
            }

            // Every entry loads onto ITS level: the whole pipeline (AddWall, PlaceWallItem,
            // PaintFloorTile, RectCenter...) operates on the active level, so we just switch it.
            // Out-of-range lvl (hand-edited / forward-compat file) is DISCARDED, not clamped —
            // clamping would silently merge it onto the top level and corrupt it on the next save.
            bool UseLevel(int lvl)
            {
                if (lvl < 0 || lvl >= MaxLevels)
                {
                    return false;
                }
                EnsureLevel(lvl);
                _activeLevel = lvl;
                return true;
            }

            // Walls FIRST across all levels (wall items validate against them), then the rest.
            foreach (var wallEntry in file.walls ?? new List<WallEntry>())
            {
                if (!UseLevel(wallEntry.lvl))
                {
                    continue;
                }
                AddWall(new Vector3Int(wallEntry.x, wallEntry.z, Mathf.Clamp(wallEntry.o, 0, 3)),
                    string.IsNullOrEmpty(wallEntry.cov) ? null : wallEntry.cov);
            }
            foreach (var e in file.entries ?? new List<LayoutEntry>())
            {
                var def = FindDef(e.id);
                if (def?.template == null || !UseLevel(e.lvl))
                {
                    continue;
                }
                var rot = e.rot & 3;
                var (w, d) = RotatedDims(def, rot);
                var origin = new Vector2Int(e.x, e.z);
                var go = Instantiate(def.template, Lv.itemsRoot);
                go.name = $"placed_{def.id}_{_placed.Count}";
                go.SetActive(true);
                go.transform.SetPositionAndRotation(RectCenter(origin, w, d), Quaternion.Euler(0f, rot * 90f, 0f));
                var p = new Placed { id = e.id, go = go, x = origin.x, z = origin.y, rot = rot, w = w, d = d };
                _placed.Add(p);
                RegisterCells(p);
                if (e.swatch > 0)
                {
                    ApplySwatch(p, e.swatch);
                }
            }
            foreach (var floorEntry in file.floors ?? new List<FloorEntry>())
            {
                if (!string.IsNullOrEmpty(floorEntry.cov) && UseLevel(floorEntry.lvl))
                {
                    PaintFloorTile(new Vector2Int(floorEntry.x, floorEntry.z), floorEntry.cov);
                }
            }
            foreach (var wiEntry in file.wallItems ?? new List<WallItemEntry>())
            {
                if (!UseLevel(wiEntry.lvl))
                {
                    continue; // out-of-range hand-edited level — discard
                }
                var def = FindDef(wiEntry.id);
                // Portals legitimately have a null template; only reject a MODELLED wall item whose
                // template failed to build. o > 1 = a hand-edited entry pointing at a diagonal wall.
                if (def == null || string.IsNullOrEmpty(def.wallItem) || wiEntry.o > 1
                    || (def.template == null && def.wallItem != "portal"))
                {
                    continue;
                }
                var span = Mathf.Max(1, def.footW);
                var dir = wiEntry.o == 0 ? new Vector2Int(1, 0) : new Vector2Int(0, 1);
                var segs = new List<Vector3Int>();
                var ok = true;
                for (var i = 0; i < span; i++)
                {
                    var key = new Vector3Int(wiEntry.x + (dir.x * i), wiEntry.z + (dir.y * i), wiEntry.o);
                    segs.Add(key);
                    if (!_walls.TryGetValue(key, out var seg) || seg.opening != 0)
                    {
                        ok = false; // its wall is gone from the layout — skip the item, keep the wall solid
                    }
                }
                for (var i = 1; i < span && ok; i++)
                {
                    CollectIncidentWalls(new Vector2Int(wiEntry.x + (dir.x * i), wiEntry.z + (dir.y * i)), _incScratch);
                    if (_incScratch.Count > 2)
                    {
                        ok = false; // a junction sits inside the span now — keep the wall solid
                    }
                }
                if (ok)
                {
                    PlaceWallItem(def, segs, wiEntry.flip & 1);
                }
            }
            _activeLevel = 0; // back to ground level, upper levels hidden
            for (var i = 0; i < _levels.Count; i++)
            {
                _levels[i].root.SetActive(i == 0);
            }
            _focus.y = 0f;
            _bulkLoading = false;
            RefreshCutaway(force: true); // set every loaded wall's near/far state in one pass
            for (var i = 0; i < _levels.Count; i++)
            {
                RefreshRooms(i); // rebuild each level's ceiling now that all its walls are in
            }
            int objs = 0, walls = 0, tiles = 0, wis = 0;
            foreach (var lv in _levels)
            {
                objs += lv.placed.Count;
                walls += lv.walls.Count;
                tiles += lv.floorTiles.Count;
                wis += lv.wallItems.Count;
            }
            _status = $"Loaded {objs} object(s), {walls} wall(s), {tiles} floor tile(s), {wis} door/window(s) on {_levels.Count} level(s).";
        }

        // ================================ WALL MESH =============================================

        // A wall piece assembled from axis-aligned BOXES in the segment's local frame: x across the
        // length (−L/2..L/2), y up. One full box = solid wall; a wall with a door/window hole is the
        // rects AROUND the hole (left jamb / right jamb / sill / lintel). The BIG faces (±Z) carry
        // wallpaper UVs — u = (x+L/2)/L, v = y/height — so the pattern stays CONTINUOUS across
        // boxes and neighboring segments. Every box emits all six faces, which automatically gives
        // the hole its jamb/sill/lintel interior surfaces.
        // `length` is the UV panel length (one texture repeat); boxes may extend beyond ±length/2
        // (U extrapolates) and `uOffset` shifts the whole panel (posts straddle the seam at u=1).
        // `traps` are strips with per-end y values (slanted arch linings); they emit no x-end
        // caps — adjacent strips/jamb caps line the seams.
        private static readonly List<(float, float, float, float, float, float)> EmptyTraps = new();
        private static readonly List<(float, float, float, bool)> EmptyWedges = new();

        private static Mesh BuildWallBoxesMesh(float length, float height, float thickness,
            (float x0, float x1, float y0, float y1)[] boxes, float uOffset = 0f,
            List<(float xa, float xb, float y0a, float y0b, float y1a, float y1b)> traps = null,
            List<(float xP, float xM, float xCore, bool atNeg)> wedges = null,
            float clipTop = float.PositiveInfinity)
        {
            var hz = thickness * 0.5f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                var i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
                // Reversed winding (i, i+2, i+1): the vertex order reads naturally as a quad viewed
                // FROM the face's outside, which in Unity's left-handed space is CCW — the straight
                // order wound every face INWARD (walls rendered open-topped/inside-out).
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }

            float U(float x) => ((x + (length * 0.5f)) / length) + uOffset;

            foreach (var box in boxes)
            {
                // clipTop caps the box (self-capping at that height); a box entirely above it — e.g. a
                // door/window LINTEL — drops out, so the cutaway stub keeps the opening as a real gap
                // instead of a horizontal band. UV v still uses the FULL height so wallpaper isn't stretched.
                float x0 = box.x0, x1 = box.x1, y0 = box.y0, y1 = Mathf.Min(box.y1, clipTop);
                if (y0 >= clipTop || x1 - x0 < 0.001f || y1 - y0 < 0.001f)
                {
                    continue;
                }
                float u0 = U(x0), u1 = U(x1), v0 = y0 / height, v1 = y1 / height;
                // ±Z (wallpaper) faces
                Face(new Vector3(x0, y0, hz), new Vector3(x0, y1, hz), new Vector3(x1, y1, hz), new Vector3(x1, y0, hz),
                     new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0));
                Face(new Vector3(x1, y0, -hz), new Vector3(x1, y1, -hz), new Vector3(x0, y1, -hz), new Vector3(x0, y0, -hz),
                     new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), new Vector2(u0, v0));
                // top
                Face(new Vector3(x0, y1, hz), new Vector3(x0, y1, -hz), new Vector3(x1, y1, -hz), new Vector3(x1, y1, hz),
                     new Vector2(u0, v1), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v1));
                // bottom (lintel undersides, lifted sills)
                if (y0 > 0.001f)
                {
                    Face(new Vector3(x0, y0, -hz), new Vector3(x0, y0, hz), new Vector3(x1, y0, hz), new Vector3(x1, y0, -hz),
                         new Vector2(u0, v0), new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v0));
                }
                // end caps (segment ends AND hole jambs)
                Face(new Vector3(x0, y0, -hz), new Vector3(x0, y1, -hz), new Vector3(x0, y1, hz), new Vector3(x0, y0, hz),
                     new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u0 + 0.02f, v1), new Vector2(u0 + 0.02f, v0));
                Face(new Vector3(x1, y0, hz), new Vector3(x1, y1, hz), new Vector3(x1, y1, -hz), new Vector3(x1, y0, -hz),
                     new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u1 - 0.02f, v1), new Vector2(u1 - 0.02f, v0));
            }

            foreach (var trap in traps ?? EmptyTraps)
            {
                // clip arch strips at clipTop too; a strip entirely above it drops out of the stub.
                float xa = trap.xa, xb = trap.xb, y0a = trap.y0a, y0b = trap.y0b;
                float y1a = Mathf.Min(trap.y1a, clipTop), y1b = Mathf.Min(trap.y1b, clipTop);
                if (xb - xa < 0.001f || (y0a >= clipTop && y0b >= clipTop) || (y1a - y0a < 0.001f && y1b - y0b < 0.001f))
                {
                    continue;
                }
                float ua = U(xa), ub = U(xb);
                float v0a = y0a / height, v0b = y0b / height, v1a = y1a / height, v1b = y1b / height;
                // ±Z (wallpaper) faces — quads with independently slanted top/bottom edges
                Face(new Vector3(xa, y0a, hz), new Vector3(xa, y1a, hz), new Vector3(xb, y1b, hz), new Vector3(xb, y0b, hz),
                     new Vector2(ua, v0a), new Vector2(ua, v1a), new Vector2(ub, v1b), new Vector2(ub, v0b));
                Face(new Vector3(xb, y0b, -hz), new Vector3(xb, y1b, -hz), new Vector3(xa, y1a, -hz), new Vector3(xa, y0a, -hz),
                     new Vector2(ub, v0b), new Vector2(ub, v1b), new Vector2(ua, v1a), new Vector2(ua, v0a));
                // top lining (arch underside runs are emitted as the NEXT trap's slanted bottom;
                // this is the strip's own top: wall top for lintels, the sill line for sills)
                Face(new Vector3(xa, y1a, hz), new Vector3(xa, y1a, -hz), new Vector3(xb, y1b, -hz), new Vector3(xb, y1b, hz),
                     new Vector2(ua, v1a), new Vector2(ua, v1a), new Vector2(ub, v1b), new Vector2(ub, v1b));
                // bottom lining (lintel underside = the arch curve) when lifted off the floor
                if (y0a > 0.001f || y0b > 0.001f)
                {
                    Face(new Vector3(xa, y0a, -hz), new Vector3(xa, y0a, hz), new Vector3(xb, y0b, hz), new Vector3(xb, y0b, -hz),
                         new Vector2(ua, v0a), new Vector2(ua, v0a), new Vector2(ub, v0b), new Vector2(ub, v0b));
                }
            }

            // Mitered end wedges: the piece between the slanted end edge (xP on +Z, xM on −Z) and
            // the rectangular core at xCore. Emits ±Z partial sides, the plan-quad top, and the
            // slanted vertical end face; NOTHING at the xCore plane (interior, the core abuts it).
            var wh = Mathf.Min(height, clipTop); // wedges cap at clipTop too (so a stub keeps short miters)
            var vT = wh / height;
            foreach (var (xP, xM, xCore, atNeg) in wedges ?? EmptyWedges)
            {
                float uP = U(xP), uM = U(xM), uC = U(xCore);
                // ±Z side rects between the slant vertex and the core plane
                var pLo = atNeg ? xP : xCore;
                var pHi = atNeg ? xCore : xP;
                if (pHi - pLo > 0.0005f)
                {
                    float u0 = U(pLo), u1 = U(pHi);
                    Face(new Vector3(pLo, 0f, hz), new Vector3(pLo, wh, hz), new Vector3(pHi, wh, hz), new Vector3(pHi, 0f, hz),
                         new Vector2(u0, 0f), new Vector2(u0, vT), new Vector2(u1, vT), new Vector2(u1, 0f));
                }
                var mLo = atNeg ? xM : xCore;
                var mHi = atNeg ? xCore : xM;
                if (mHi - mLo > 0.0005f)
                {
                    float u0 = U(mLo), u1 = U(mHi);
                    Face(new Vector3(mHi, 0f, -hz), new Vector3(mHi, wh, -hz), new Vector3(mLo, wh, -hz), new Vector3(mLo, 0f, -hz),
                         new Vector2(u1, 0f), new Vector2(u1, vT), new Vector2(u0, vT), new Vector2(u0, 0f));
                }
                // top plan quad (box-top rotational order: +z@start, −z@start, −z@end, +z@end)
                if (atNeg)
                {
                    Face(new Vector3(xP, wh, hz), new Vector3(xM, wh, -hz), new Vector3(xCore, wh, -hz), new Vector3(xCore, wh, hz),
                         new Vector2(uP, vT), new Vector2(uM, vT), new Vector2(uC, vT), new Vector2(uC, vT));
                }
                else
                {
                    Face(new Vector3(xCore, wh, hz), new Vector3(xCore, wh, -hz), new Vector3(xM, wh, -hz), new Vector3(xP, wh, hz),
                         new Vector2(uC, vT), new Vector2(uC, vT), new Vector2(uM, vT), new Vector2(uP, vT));
                }
                // slanted vertical end face (the miter seam plane)
                if (atNeg)
                {
                    Face(new Vector3(xM, 0f, -hz), new Vector3(xM, wh, -hz), new Vector3(xP, wh, hz), new Vector3(xP, 0f, hz),
                         new Vector2(uM, 0f), new Vector2(uM, vT), new Vector2(uP, vT), new Vector2(uP, 0f));
                }
                else
                {
                    Face(new Vector3(xP, 0f, hz), new Vector3(xP, wh, hz), new Vector3(xM, wh, -hz), new Vector3(xM, 0f, -hz),
                         new Vector2(uP, 0f), new Vector2(uP, vT), new Vector2(uM, vT), new Vector2(uM, 0f));
                }
                // Core-plane seal: visible when a hole clamps flush to the core (no jamb box abuts
                // there and the wedge would read hollow from inside the doorway). In the solid
                // case it's coincident-opposing with the core's own end cap — culled + buried.
                if (atNeg) // faces +X, into the core/hole (box x1-cap pattern)
                {
                    Face(new Vector3(xCore, 0f, hz), new Vector3(xCore, wh, hz), new Vector3(xCore, wh, -hz), new Vector3(xCore, 0f, -hz),
                         new Vector2(uC, 0f), new Vector2(uC, vT), new Vector2(uC + 0.02f, vT), new Vector2(uC + 0.02f, 0f));
                }
                else // faces −X (box x0-cap pattern)
                {
                    Face(new Vector3(xCore, 0f, -hz), new Vector3(xCore, wh, -hz), new Vector3(xCore, wh, hz), new Vector3(xCore, 0f, hz),
                         new Vector2(uC, 0f), new Vector2(uC, vT), new Vector2(uC - 0.02f, vT), new Vector2(uC - 0.02f, 0f));
                }
            }

            var mesh = new Mesh { name = "wall_piece" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ================================ CAMERA ================================================

        private void HandleCamera(bool overPanel)
        {
            if (Input.GetMouseButtonDown(1) && !overPanel && _placing == null && !_wallDrag && !_floorDrag)
            {
                _orbiting = true;
            }
            if (!Input.GetMouseButton(1))
            {
                _orbiting = false;
            }
            if (!overPanel)
            {
                if (_orbiting)
                {
                    _yaw += Input.GetAxis("Mouse X") * 4f;
                    _pitch = Mathf.Clamp(_pitch - (Input.GetAxis("Mouse Y") * 3f), 15f, 80f);
                }
                if (Input.GetMouseButton(2))
                {
                    var right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
                    var fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
                    _focus -= right * (Input.GetAxis("Mouse X") * 0.15f * (_dist / 10f));
                    _focus -= fwd * (Input.GetAxis("Mouse Y") * 0.15f * (_dist / 10f));
                }
                var wheel = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(wheel) > 0.0001f)
                {
                    _dist = Mathf.Clamp(_dist - (wheel * 8f), 4f, 45f);
                }
            }
            ApplyCamera();
            if (_cutaway)
            {
                RefreshCutaway(force: false); // re-evaluate near/far walls as the view turns
            }
        }

        private void ApplyCamera()
        {
            if (editorCamera == null)
            {
                return;
            }
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            editorCamera.transform.SetPositionAndRotation(_focus - (rot * Vector3.forward * _dist), rot);
        }

        // ================================ UI ====================================================

        private void EnsureStyles()
        {
            if (_panel != null)
            {
                return;
            }
            _bg = new Texture2D(1, 1);
            _bg.SetPixel(0, 0, new Color(0.09f, 0.10f, 0.12f, 0.96f));
            _bg.Apply();
            _panel = new GUIStyle { normal = { background = _bg } };
            _head = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.55f, 0.75f, 0.95f) } };
            _btn = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 6, 4, 4), imagePosition = ImagePosition.ImageLeft, fixedHeight = 40 };
            _btnActive = new GUIStyle(_btn);
            _btnActive.normal.textColor = Color.white;
            _dim = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(0.65f, 0.67f, 0.7f) } };
            _thumbBtn = new GUIStyle(GUI.skin.button) { padding = new RectOffset(2, 2, 2, 2), fixedWidth = 62, fixedHeight = 62, imagePosition = ImagePosition.ImageOnly };
            var selTex = new Texture2D(1, 1);
            selTex.SetPixel(0, 0, new Color(0.25f, 0.5f, 0.85f, 1f)); // visible selection frame for image-only buttons
            selTex.Apply();
            _thumbBtnActive = new GUIStyle(_thumbBtn) { padding = new RectOffset(6, 6, 6, 6) };
            _thumbBtnActive.normal.background = selTex;
            _thumbBtnActive.hover.background = selTex;
            _thumbBtnActive.active.background = selTex;
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(Screen.width - PanelWidth, 0, PanelWidth, Screen.height), _panel);
            GUILayout.Space(8);
            GUILayout.Label("  HOME EDITOR", _head);
            var newMode = GUILayout.Toolbar(_mode, ModeNames);
            if (newMode != _mode)
            {
                SetMode(newMode);
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("  Level", _dim, GUILayout.Width(48));
            for (var i = 0; i < MaxLevels; i++)
            {
                if (GUILayout.Button($"{i + 1}", i == _activeLevel ? _btnActive : _btn, GUILayout.Width(34)))
                {
                    SetActiveLevel(i); // creates the level on first visit; upper levels hide
                }
            }
            GUILayout.FlexibleSpace();
            var newCut = GUILayout.Toggle(_cutaway, "Cutaway", GUILayout.Width(78));
            if (newCut != _cutaway)
            {
                _cutaway = newCut;
                RefreshCutaway(force: true); // drop near walls, or raise everything back up
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("  " + _status, _dim);
            GUILayout.Space(4);

            _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar);
            switch (_mode)
            {
                case 0: DrawObjectsPanel(); break;
                case 1: DrawWallsPanel(); break;
                case 2: DrawCoveringPanel(wallCoverings, ref _wallCovSel, showErase: false); break;
                case 3: DrawCoveringPanel(floorCoverings, ref _floorCovSel, showErase: true); break;
            }

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save layout", _btn))
            {
                SaveLayout();
            }
            if (GUILayout.Button("Load layout", _btn))
            {
                LoadLayout();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("  RMB orbit · MMB pan · wheel zoom", _dim);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawObjectsPanel()
        {
            string currentCat = null;
            foreach (var item in items)
            {
                // Portals legitimately have a null template (no object) — keep them; only skip
                // items whose model genuinely failed to build.
                if (item == null || (item.template == null && item.wallItem != "portal"))
                {
                    continue;
                }
                if (item.category != currentCat)
                {
                    currentCat = item.category;
                    GUILayout.Space(6);
                    GUILayout.Label("  " + currentCat, _head);
                }
                var active = _placing == item;
                var content = item.thumb != null
                    ? new GUIContent($" {item.label}  [{item.footW}x{item.footD}]", item.thumb)
                    : new GUIContent($"{(active ? "▶ " : "  ")}{item.label}  [{item.footW}x{item.footD}]");
                if (GUILayout.Button(content, active ? _btnActive : _btn))
                {
                    if (active)
                    {
                        CancelPlacement();
                    }
                    else
                    {
                        BeginPlacement(item);
                    }
                }
            }

            GUILayout.Space(10);
            if (_selectedWallItem != null)
            {
                GUILayout.Label("  Selected: " + _selectedWallItem.id + " (in wall)", _head);
                if (GUILayout.Button("Delete (restore wall)", _btn))
                {
                    RemoveWallItem(_selectedWallItem);
                    _status = "Removed wall item (opening restored).";
                }
                GUILayout.Space(8);
            }
            if (_selected != null)
            {
                GUILayout.Label("  Selected: " + _selected.id, _head);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Move", _btn))
                {
                    var def = FindDef(_selected.id);
                    if (def != null)
                    {
                        BeginPlacement(def, _selected);
                    }
                }
                if (GUILayout.Button("Rotate", _btn))
                {
                    RotateSelected();
                }
                if (GUILayout.Button("Delete", _btn))
                {
                    DeleteSelected();
                }
                GUILayout.EndHorizontal();

                // swatch recolours (from the game's own per-object swatch set)
                var selDef = FindDef(_selected.id);
                if (selDef != null && selDef.swatches.Count > 1)
                {
                    GUILayout.Label("  Colour:", _dim);
                    var perRow = 6;
                    for (var i = 0; i < selDef.swatches.Count; i += perRow)
                    {
                        GUILayout.BeginHorizontal();
                        for (var j = i; j < Mathf.Min(i + perRow, selDef.swatches.Count); j++)
                        {
                            var sw = selDef.swatches[j];
                            var img = sw.diffuse;
                            var pressed = GUILayout.Button(
                                img != null ? new GUIContent(img, sw.label) : new GUIContent($"{j}"),
                                _selected.swatch == j ? _thumbBtnActive : _thumbBtn,
                                GUILayout.Width(42), GUILayout.Height(42));
                            if (pressed)
                            {
                                ApplySwatch(_selected, j);
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }

        private void DrawWallsPanel()
        {
            _erase = GUILayout.Toggle(_erase, "  Erase mode (stretched run REMOVES walls)");
            GUILayout.Label($"  {_walls.Count} wall segment(s). Press LMB, stretch straight or diagonal, release to place. RMB cancels.", _dim);
        }

        private void DrawCoveringPanel(List<CoveringDef> coverings, ref int selection, bool showErase)
        {
            if (showErase)
            {
                _erase = GUILayout.Toggle(_erase, "  Erase mode (rectangle removes floor tiles)");
            }
            if (coverings.Count == 0)
            {
                GUILayout.Label("  No coverings exported yet (run exportcoverings).", _dim);
                return;
            }
            GUILayout.Label($"  {coverings.Count} covering(s):", _dim);
            var perRow = 4;
            for (var i = 0; i < coverings.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (var j = i; j < Mathf.Min(i + perRow, coverings.Count); j++)
                {
                    var cov = coverings[j];
                    var img = cov.thumb != null ? cov.thumb : (cov.material != null ? cov.material.mainTexture as Texture2D : null);
                    var pressed = GUILayout.Button(
                        img != null ? new GUIContent(img, cov.label) : new GUIContent(cov.label),
                        selection == j ? _thumbBtnActive : _thumbBtn,
                        GUILayout.Width(62), GUILayout.Height(62));
                    if (pressed)
                    {
                        selection = j;
                        _erase = false;
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("  " + coverings[Mathf.Clamp(selection, 0, coverings.Count - 1)].label, _dim);
        }
    }
}
