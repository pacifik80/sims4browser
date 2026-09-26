using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// THE lot grid — the first-class space authority (decision D-106). Two layers, one definition:
    ///
    ///   • **Tiles (1 m)** — the MAIN game grid. Furniture/build placement snaps to it, an OCCUPANCY map
    ///     records which object owns which tile (placement validity = <see cref="TilesFree"/>, not
    ///     physics), and Sims stand/interact at tile centres (use anchors, social stands).
    ///   • **Nav sub-cells (tile / navSubdivision, default 0.5 m)** — routing only. A* runs over the
    ///     finer walkability bitmap derived from the SAME tile footprints (inflated by the Sim radius so
    ///     a Sim's centre clears corners). Sims MOVE continuously along the routed path — the grid
    ///     constrains where things ARE, not how bodies glide between them.
    ///
    /// Rebuilding after a build-mode edit re-registers occupancy and re-marks nav from the registered
    /// footprints (no NavMesh bake); bumping <see cref="Version"/> tells agents and the director to
    /// re-plan/re-scan.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LotGrid : MonoBehaviour
    {
        public static LotGrid Instance { get; private set; }

        [Tooltip("MAIN grid tile in metres — placement, occupancy and interaction authority (D-106).")]
        public float tileSize = 1f;
        [Tooltip("Nav sub-cells per tile edge — routing resolution (2 → 0.5 m sub-cells).")]
        public int navSubdivision = 2;
        [Tooltip("World position of the (0,0) tile corner.")]
        public Vector3 origin = new Vector3(-15f, 0f, -15f);
        public int tilesX = 30;
        public int tilesZ = 30;
        [Tooltip("Nav obstacles are inflated by this so the Sim's CENTRE can follow the path safely.")]
        public float simRadius = 0.24f;
        public bool drawGizmos = true;

        /// <summary>Incremented on every rebuild; agents re-path and the director re-scans when it changes.</summary>
        public int Version { get; private set; }

        private NavGrid _grid;                 // sub-cell walkability (routing)
        private SmartObject[,] _owner;         // tile occupancy (authority)
        private readonly List<(int x, int z)> _cells = new List<(int x, int z)>();

        private float SubSize => tileSize / Mathf.Max(1, navSubdivision);
        private int SubX => tilesX * Mathf.Max(1, navSubdivision);
        private int SubZ => tilesZ * Mathf.Max(1, navSubdivision);

        private void Awake()
        {
            Instance = this;
            _grid = new NavGrid(SubX, SubZ);
            _owner = new SmartObject[tilesX, tilesZ];
            Rebuild();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ------------------------------------------------------------------ tiles (MAIN grid)

        public int TileX(float worldX) => Mathf.FloorToInt((worldX - origin.x) / tileSize);
        public int TileZ(float worldZ) => Mathf.FloorToInt((worldZ - origin.z) / tileSize);
        public Vector3 TileCenter(int tx, int tz)
            => new Vector3(origin.x + (tx + 0.5f) * tileSize, origin.y, origin.z + (tz + 0.5f) * tileSize);

        public bool InBounds(int tx, int tz, int w = 1, int d = 1)
            => tx >= 0 && tz >= 0 && tx + w <= tilesX && tz + d <= tilesZ;

        /// <summary>The object occupying a tile, or null.</summary>
        public SmartObject OwnerAt(int tx, int tz)
            => InBounds(tx, tz) && _owner != null ? _owner[tx, tz] : null;

        /// <summary>Placement validity: every tile of the w×d rect is inside the lot and unowned
        /// (or owned by <paramref name="ignore"/> — the object being moved/rotated).</summary>
        public bool TilesFree(int tx, int tz, int w, int d, SmartObject ignore = null)
        {
            if (!InBounds(tx, tz, w, d) || _owner == null) return false;
            for (int z = tz; z < tz + d; z++)
                for (int x = tx; x < tx + w; x++)
                {
                    var o = _owner[x, z];
                    if (o != null && o != ignore) return false;
                }
            return true;
        }

        /// <summary>Origin tile of a w×d footprint whose SNAPPED centre is at <paramref name="center"/>.</summary>
        public (int tx, int tz) OriginTileOf(Vector3 center, int w, int d)
            => (Mathf.RoundToInt((center.x - origin.x) / tileSize - w * 0.5f),
                Mathf.RoundToInt((center.z - origin.z) / tileSize - d * 0.5f));

        /// <summary>Snap a world point so a w×d TILE footprint sits centred under it, edges on tile lines.</summary>
        public Vector3 SnapFootprint(Vector3 world, int w, int d) => SnapFootprint(origin, tileSize, world, w, d);

        /// <summary>Static form for edit-time use (scene builder), where no instance is awake.</summary>
        public static Vector3 SnapFootprint(Vector3 origin, float tile, Vector3 world, int w, int d)
        {
            int tx = Mathf.FloorToInt((world.x - origin.x) / tile) - (w - 1) / 2;
            int tz = Mathf.FloorToInt((world.z - origin.z) / tile) - (d - 1) / 2;
            return new Vector3(origin.x + (tx + w * 0.5f) * tile, 0f, origin.z + (tz + d * 0.5f) * tile);
        }

        /// <summary>An object's footprint in tiles, rotation applied (w↔d on odd quarter-turns).</summary>
        public (int w, int d) FootprintOf(SmartObject so)
        {
            int w, d;
            if (so.Def != null) { w = Mathf.Max(1, so.Def.footW); d = Mathf.Max(1, so.Def.footD); }
            else
            {
                var r = so.GetComponentInChildren<Renderer>();
                var size = r != null ? r.bounds.size : so.transform.lossyScale;
                w = Mathf.Max(1, Mathf.CeilToInt((size.x - 0.08f) / tileSize));
                d = Mathf.Max(1, Mathf.CeilToInt((size.z - 0.08f) / tileSize));
                return (w, d); // bounds are already world-rotated
            }
            return (so.RotIndex & 1) == 1 ? (d, w) : (w, d);
        }

        // ------------------------------------------------------------------ rebuild (occupancy + nav)

        /// <summary>Re-register every SmartObject's tiles and re-mark nav from those SAME tiles.
        /// Call after any build-mode edit.</summary>
        public void Rebuild()
        {
            if (_grid == null) _grid = new NavGrid(SubX, SubZ);
            if (_owner == null) _owner = new SmartObject[tilesX, tilesZ];
            _grid.Clear();
            System.Array.Clear(_owner, 0, _owner.Length);

            foreach (var so in FindObjectsByType<SmartObject>(FindObjectsSortMode.None))
            {
                if (so == null) continue;
                var (w, d) = FootprintOf(so);
                var (tx, tz) = OriginTileOf(so.transform.position, w, d);

                // Occupancy: the authority. Clamped write so an off-lot object can't corrupt the array.
                for (int z = Mathf.Max(0, tz); z < Mathf.Min(tilesZ, tz + d); z++)
                    for (int x = Mathf.Max(0, tx); x < Mathf.Min(tilesX, tx + w); x++)
                        _owner[x, z] = so;

                // Nav: block the sub-cells under the SAME tile rect, inflated by the Sim radius.
                float minX = origin.x + tx * tileSize - simRadius;
                float maxX = origin.x + (tx + w) * tileSize + simRadius;
                float minZ = origin.z + tz * tileSize - simRadius;
                float maxZ = origin.z + (tz + d) * tileSize + simRadius;
                int sx0 = Mathf.FloorToInt((minX - origin.x) / SubSize), sx1 = Mathf.FloorToInt((maxX - origin.x) / SubSize);
                int sz0 = Mathf.FloorToInt((minZ - origin.z) / SubSize), sz1 = Mathf.FloorToInt((maxZ - origin.z) / SubSize);
                for (int z = sz0; z <= sz1; z++)
                    for (int x = sx0; x <= sx1; x++)
                        _grid.SetBlocked(x, z, true);
            }
            Version++;
        }

        // ------------------------------------------------------------------ routing (nav sub-cells)

        private int SubXOf(float worldX) => Mathf.FloorToInt((worldX - origin.x) / SubSize);
        private int SubZOf(float worldZ) => Mathf.FloorToInt((worldZ - origin.z) / SubSize);
        private Vector3 SubCenter(int x, int z)
            => new Vector3(origin.x + (x + 0.5f) * SubSize, origin.y, origin.z + (z + 0.5f) * SubSize);

        // ---- debug introspection (GridDebugOverlay) ----
        public int NavX => SubX;
        public int NavZ => SubZ;
        public float NavCellSize => SubSize;
        public Vector3 NavCellCenter(int sx, int sz) => SubCenter(sx, sz);
        public bool IsNavBlockedAt(int sx, int sz) => _grid != null && !_grid.IsWalkable(sx, sz);

        public bool IsWalkableWorld(Vector3 world)
            => _grid != null && _grid.IsWalkable(SubXOf(world.x), SubZOf(world.z));

        /// <summary>
        /// Smoothed world path from → to over the nav sub-cells. False means "no route" and the caller
        /// should fall back to a straight line. Both ends snap to the nearest free sub-cell, because a
        /// Sim can stand inside an inflated footprint and a use-anchor sits beside its own object.
        /// </summary>
        public bool TryPath(Vector3 from, Vector3 to, List<Vector3> outPath)
        {
            outPath.Clear();
            if (_grid == null) return false;

            int sx = SubXOf(from.x), sz = SubZOf(from.z);
            int gx = SubXOf(to.x), gz = SubZOf(to.z);
            if (!_grid.TryNearestWalkable(sx, sz, 10, out sx, out sz)) return false;
            if (!_grid.TryNearestWalkable(gx, gz, 10, out gx, out gz)) return false;

            if (!_grid.TryFindPath(sx, sz, gx, gz, _cells)) return false;
            _grid.Smooth(_cells);

            for (int i = 0; i < _cells.Count; i++)
            {
                var p = SubCenter(_cells[i].x, _cells[i].z);
                p.y = from.y;
                outPath.Add(p);
            }

            // Drop a first waypoint we're already standing on (prevents a little step backwards).
            if (outPath.Count > 1)
            {
                float t = SubSize * 0.5f;
                if ((outPath[0] - from).sqrMagnitude < t * t) outPath.RemoveAt(0);
            }
            return outPath.Count > 0;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || _grid == null) return;
            Gizmos.color = new Color(1f, 0.35f, 0.3f, 0.35f);
            var size = new Vector3(SubSize * 0.92f, 0.02f, SubSize * 0.92f);
            for (int z = 0; z < SubZ; z++)
                for (int x = 0; x < SubX; x++)
                    if (!_grid.IsWalkable(x, z))
                        Gizmos.DrawCube(SubCenter(x, z) + Vector3.up * 0.01f, size);
        }
    }
}
