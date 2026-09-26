using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// The grid debug menu (F3) — visual truth for the D-106 grid so "is it really respecting the grid?"
    /// is answerable by looking:
    ///
    ///   • 1 m grid        — the shared grid overlay quad (same one build mode shows).
    ///   • Occupancy       — an orange quad on every tile OWNED by furniture (the authority map itself).
    ///   • Sim cells       — per Sim: CURRENT tile (cyan), GOAL tile (green), TRANSIT tiles the remaining
    ///                       route passes through (violet). NOTE: transit/current tiles are NOT reserved —
    ///                       Sims never block tiles for each other (soft separation only, TASK-014 ph. 2);
    ///                       this view is where that behaviour becomes visible.
    ///   • Sim paths       — the actual routed waypoints (0.5 m nav sub-grid) as a line per Sim.
    ///   • Nav blocked     — the 0.5 m sub-cells routing refuses (footprints + sim-radius inflation).
    ///
    /// Rendering is pooled transparent quads + LineRenderers with scene-baked HDRP/Unlit materials (the
    /// project's proven pattern — runtime keyword setup for HDRP transparency is editor-only, so
    /// materials come from the scene builder). The UI lives in <see cref="GameDebugMenu"/> (UI Toolkit);
    /// this component only owns the WORLD visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridDebugOverlay : MonoBehaviour
    {
        [Header("Wired by the scene builder")]
        public LotGrid grid;
        public SimulationDirector director;
        public GameBuildMode build;
        public GameObject gridOverlay;      // the shared 1 m grid quad
        public Material matOccupied;
        public Material matSimCurrent;
        public Material matSimGoal;
        public Material matTransit;
        public Material matNavBlocked;
        public Material matPathLine;

        [Header("Options (driven by GameDebugMenu)")]
        public bool showGrid;
        public bool showOccupancy;
        public bool showSimCells;
        public bool showPaths;
        public bool showNavBlocked;

        // pooled visuals
        private Transform _root;
        private readonly List<MeshRenderer> _quads = new List<MeshRenderer>();
        private int _quadCursor;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private int _lineCursor;

        private int _occupiedCount;
        private readonly List<Vector3> _tileScratch = new List<Vector3>();
        private static readonly List<(int tx, int tz)> TransitScratch = new List<(int, int)>();

        /// <summary>Owned-tile count from the last occupancy draw (shown in the debug menu).</summary>
        public int OccupiedCount => _occupiedCount;

        private void Start()
        {
            if (grid == null) grid = LotGrid.Instance != null ? LotGrid.Instance : FindFirstObjectByType<LotGrid>();
            if (director == null) director = FindFirstObjectByType<SimulationDirector>();
            if (build == null) build = FindFirstObjectByType<GameBuildMode>();
        }

        private void LateUpdate()
        {
            // The 1 m grid quad is shared with build mode — debug keeps it honest every frame.
            if (gridOverlay != null)
                gridOverlay.SetActive(showGrid || (build != null && build.InBuild));

            _quadCursor = 0;
            _lineCursor = 0;

            if (grid != null)
            {
                if (showOccupancy) DrawOccupancy();
                if (showNavBlocked) DrawNavBlocked();
                if (showSimCells || showPaths) DrawSims();
            }

            TrimPools();
        }

        // ------------------------------------------------------------------ layers

        private void DrawOccupancy()
        {
            _occupiedCount = 0;
            float tile = grid.tileSize;
            for (int tz = 0; tz < grid.tilesZ; tz++)
                for (int tx = 0; tx < grid.tilesX; tx++)
                {
                    if (grid.OwnerAt(tx, tz) == null) continue;
                    _occupiedCount++;
                    Quad(grid.TileCenter(tx, tz), tile * 0.94f, tile * 0.94f, matOccupied, 0.03f);
                }
        }

        private void DrawNavBlocked()
        {
            float sub = grid.NavCellSize;
            for (int z = 0; z < grid.NavZ; z++)
                for (int x = 0; x < grid.NavX; x++)
                    if (grid.IsNavBlockedAt(x, z))
                        Quad(grid.NavCellCenter(x, z), sub * 0.86f, sub * 0.86f, matNavBlocked, 0.022f);
        }

        private void DrawSims()
        {
            if (director == null) return;
            float tile = grid.tileSize;
            var bodies = director.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body == null) continue;
                var agent = body.GetComponent<SimAgent>();
                if (agent == null) continue;

                Vector3 pos = body.transform.position;
                if (showSimCells)
                {
                    // current tile — where the Sim IS (cyan)
                    Quad(grid.TileCenter(grid.TileX(pos.x), grid.TileZ(pos.z)), tile * 0.8f, tile * 0.8f, matSimCurrent, 0.045f);

                    if (agent.DebugMoving)
                    {
                        // goal tile — where it is HEADING (green)
                        var goal = agent.DebugGoal;
                        int gtx = grid.TileX(goal.x), gtz = grid.TileZ(goal.z);
                        Quad(grid.TileCenter(gtx, gtz), tile * 0.8f, tile * 0.8f, matSimGoal, 0.04f);

                        // transit tiles — every tile the REMAINING route passes through (violet).
                        // These are NOT reserved: another Sim may claim/cross them freely (TASK-014 ph.2).
                        TransitScratch.Clear();
                        var path = agent.DebugPath;
                        for (int p = agent.DebugPathIndex; p < path.Count; p++)
                        {
                            var t = (grid.TileX(path[p].x), grid.TileZ(path[p].z));
                            if (!TransitScratch.Contains(t)) TransitScratch.Add(t);
                        }
                        foreach (var (ttx, ttz) in TransitScratch)
                        {
                            if (ttx == gtx && ttz == gtz) continue;
                            Quad(grid.TileCenter(ttx, ttz), tile * 0.62f, tile * 0.62f, matTransit, 0.035f);
                        }
                    }
                }

                if (showPaths && agent.DebugMoving)
                {
                    _tileScratch.Clear();
                    _tileScratch.Add(new Vector3(pos.x, 0.06f, pos.z));
                    var path = agent.DebugPath;
                    for (int p = agent.DebugPathIndex; p < path.Count; p++)
                        _tileScratch.Add(new Vector3(path[p].x, 0.06f, path[p].z));
                    if (_tileScratch.Count >= 2) Line(_tileScratch);
                }
            }
        }

        // ------------------------------------------------------------------ pooled primitives

        private void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("~grid-debug").transform;
        }

        private void Quad(Vector3 at, float sx, float sz, Material m, float y)
        {
            if (m == null) return;
            EnsureRoot();
            MeshRenderer r;
            if (_quadCursor < _quads.Count) r = _quads[_quadCursor];
            else
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "cell";
                Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(_root, false);
                r = q.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _quads.Add(r);
            }
            _quadCursor++;
            r.gameObject.SetActive(true);
            r.transform.SetPositionAndRotation(new Vector3(at.x, y, at.z), Quaternion.Euler(90f, 0f, 0f));
            r.transform.localScale = new Vector3(sx, sz, 1f);
            r.sharedMaterial = m;
        }

        private void Line(List<Vector3> points)
        {
            if (matPathLine == null) return;
            EnsureRoot();
            LineRenderer lr;
            if (_lineCursor < _lines.Count) lr = _lines[_lineCursor];
            else
            {
                var go = new GameObject("path");
                go.transform.SetParent(_root, false);
                lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.widthMultiplier = 0.06f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.sharedMaterial = matPathLine;
                _lines.Add(lr);
            }
            _lineCursor++;
            lr.gameObject.SetActive(true);
            lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) lr.SetPosition(i, points[i]);
        }

        private void TrimPools()
        {
            for (int i = _quadCursor; i < _quads.Count; i++)
                if (_quads[i] != null && _quads[i].gameObject.activeSelf) _quads[i].gameObject.SetActive(false);
            for (int i = _lineCursor; i < _lines.Count; i++)
                if (_lines[i] != null && _lines[i].gameObject.activeSelf) _lines[i].gameObject.SetActive(false);
        }

    }
}
