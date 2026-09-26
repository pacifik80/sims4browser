using System;
using System.Collections.Generic;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Pure grid pathfinding: 8-connected A* (no corner-cutting through blocked diagonals) plus
    /// string-pull smoothing so paths come out as natural diagonals instead of stair-steps.
    /// No UnityEngine dependency — unit-testable and independent of how the world is presented.
    /// Cells are simply walkable/blocked; <see cref="LotGrid"/> owns world conversion and footprints.
    /// </summary>
    public sealed class NavGrid
    {
        public int Width { get; }
        public int Height { get; }

        private readonly bool[] _blocked;

        // A* scratch, reused between queries to avoid per-path garbage.
        private readonly float[] _g;
        private readonly int[] _came;
        private readonly bool[] _closed;
        private readonly bool[] _inOpen;
        private readonly List<int> _open = new List<int>();
        private readonly List<int> _stack = new List<int>();

        public NavGrid(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            int n = Width * Height;
            _blocked = new bool[n];
            _g = new float[n];
            _came = new int[n];
            _closed = new bool[n];
            _inOpen = new bool[n];
        }

        public void Clear() => Array.Clear(_blocked, 0, _blocked.Length);
        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Height;
        public int Index(int x, int z) => z * Width + x;
        public bool IsWalkable(int x, int z) => InBounds(x, z) && !_blocked[Index(x, z)];
        public void SetBlocked(int x, int z, bool blocked) { if (InBounds(x, z)) _blocked[Index(x, z)] = blocked; }

        /// <summary>Nearest walkable cell to (x,z), searched in expanding rings. Used when a Sim or a
        /// target anchor sits inside an object's inflated footprint.</summary>
        public bool TryNearestWalkable(int x, int z, int maxRadius, out int ox, out int oz)
        {
            if (IsWalkable(x, z)) { ox = x; oz = z; return true; }
            for (int r = 1; r <= maxRadius; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dz) != r) continue; // ring perimeter only
                        int nx = x + dx, nz = z + dz;
                        if (IsWalkable(nx, nz)) { ox = nx; oz = nz; return true; }
                    }
            ox = x; oz = z;
            return false;
        }

        /// <summary>8-connected A*. Fills <paramref name="result"/> with cells start→goal.</summary>
        public bool TryFindPath(int sx, int sz, int gx, int gz, List<(int x, int z)> result)
        {
            result.Clear();
            if (!IsWalkable(sx, sz) || !IsWalkable(gx, gz)) return false;

            int start = Index(sx, sz), goal = Index(gx, gz);
            if (start == goal) { result.Add((sx, sz)); return true; }

            Array.Clear(_closed, 0, _closed.Length);
            Array.Clear(_inOpen, 0, _inOpen.Length);
            for (int i = 0; i < _g.Length; i++) { _g[i] = float.MaxValue; _came[i] = -1; }
            _open.Clear();
            _g[start] = 0f;
            _open.Add(start); _inOpen[start] = true;

            while (_open.Count > 0)
            {
                // Pop lowest f. Linear scan: these grids are small and paths are requested rarely.
                int bi = 0; float bf = float.MaxValue;
                for (int i = 0; i < _open.Count; i++)
                {
                    int c = _open[i];
                    float f = _g[c] + Heuristic(c % Width, c / Width, gx, gz);
                    if (f < bf) { bf = f; bi = i; }
                }
                int cur = _open[bi];
                _open.RemoveAt(bi);
                _inOpen[cur] = false;

                if (cur == goal) { Reconstruct(cur, start, result); return true; }
                _closed[cur] = true;

                int cx = cur % Width, cz = cur / Width;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = cx + dx, nz = cz + dz;
                        if (!IsWalkable(nx, nz)) continue;
                        // Don't squeeze diagonally between two blocked orthogonals.
                        if (dx != 0 && dz != 0 && (!IsWalkable(cx + dx, cz) || !IsWalkable(cx, cz + dz))) continue;

                        int ni = Index(nx, nz);
                        if (_closed[ni]) continue;
                        float ng = _g[cur] + ((dx != 0 && dz != 0) ? 1.41421356f : 1f);
                        if (ng < _g[ni])
                        {
                            _g[ni] = ng;
                            _came[ni] = cur;
                            if (!_inOpen[ni]) { _open.Add(ni); _inOpen[ni] = true; }
                        }
                    }
            }
            return false;
        }

        private void Reconstruct(int cur, int start, List<(int x, int z)> result)
        {
            _stack.Clear();
            while (cur != -1) { _stack.Add(cur); if (cur == start) break; cur = _came[cur]; }
            for (int i = _stack.Count - 1; i >= 0; i--) result.Add((_stack[i] % Width, _stack[i] / Width));
        }

        private static float Heuristic(int x, int z, int gx, int gz)
        {
            int dx = Math.Abs(x - gx), dz = Math.Abs(z - gz);
            int min = Math.Min(dx, dz), max = Math.Max(dx, dz);
            return min * 1.41421356f + (max - min); // octile
        }

        /// <summary>String-pull: keep a waypoint only where the straight line would be blocked.</summary>
        public void Smooth(List<(int x, int z)> path)
        {
            if (path.Count <= 2) return;
            var kept = new List<(int x, int z)> { path[0] };
            int anchor = 0;
            for (int i = 2; i < path.Count; i++)
            {
                if (!HasLineOfSight(path[anchor].x, path[anchor].z, path[i].x, path[i].z))
                {
                    kept.Add(path[i - 1]);
                    anchor = i - 1;
                }
            }
            kept.Add(path[path.Count - 1]);
            path.Clear();
            path.AddRange(kept);
        }

        /// <summary>Bresenham walk — true only if every cell along the line is walkable.</summary>
        public bool HasLineOfSight(int x0, int z0, int x1, int z1)
        {
            int dx = Math.Abs(x1 - x0), dz = Math.Abs(z1 - z0);
            int sx = x1 > x0 ? 1 : -1, sz = z1 > z0 ? 1 : -1;
            int x = x0, z = z0, err = dx - dz;
            while (true)
            {
                if (!IsWalkable(x, z)) return false;
                if (x == x1 && z == z1) return true;
                int e2 = err * 2;
                if (e2 > -dz) { err -= dz; x += sx; }
                if (e2 < dx) { err += dx; z += sz; }
            }
        }
    }
}
