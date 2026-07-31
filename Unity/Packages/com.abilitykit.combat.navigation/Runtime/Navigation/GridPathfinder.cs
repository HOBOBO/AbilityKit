using System;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Navigation
{
    /// <summary>
    /// 确定性整数格 A* 寻路器。
    ///
    /// 确定性保证（无需定点数学）：
    /// - 全程在整数 cell 空间运算，步进代价正交=10 / 对角=14，启发式为整数 octile 距离；
    /// - 决策路径不含 Sqrt / sin 等 IEEE 跨平台不确定的超越函数；
    /// - 固定邻居展开顺序 + open 堆按 (f, 插入序) tie-break；
    /// - closed 集与 g-cost 用 searchId 计数器标记，每次查询不清零数组；
    /// - 无 System.Random，无基于哈希迭代的定序。
    /// 浮点仅出现在 world↔cell 边界（除以常数格距）与输出（cell 中心→Vec3）。
    /// </summary>
    public sealed class GridPathfinder
    {
        private const int OrthogonalCost = 10;
        private const int DiagonalCost = 14;

        // 固定邻居顺序：先四邻接（代价 10），再对角（代价 14）。
        private static readonly int[] NeighborDx = { 1, 0, -1, 0, 1, -1, -1, 1 };
        private static readonly int[] NeighborDz = { 0, 1, 0, -1, 1, 1, -1, -1 };
        private static readonly int[] NeighborCost = { OrthogonalCost, OrthogonalCost, OrthogonalCost, OrthogonalCost, DiagonalCost, DiagonalCost, DiagonalCost, DiagonalCost };

        private int _gridCells;
        private int[] _gCost;
        private int[] _gStamp;
        private int[] _closedStamp;
        private int[] _parent;

        private int _search;
        private int _insertCounter;

        // open 堆（并行数组）：按 (f asc, insert asc) 取最小。
        private int _heapCount;
        private int[] _heapF;
        private int[] _heapOrder;
        private int[] _heapCell;
        private int[] _heapG;

        /// <summary>
        /// 规划路径。start 可落在 blocked cell（允许走出重叠区）；goal 若 blocked 则投影到最近可行 cell（状态 Partial）。
        /// </summary>
        public NavigationPath FindPath(NavigationGrid grid, in Vec3 startWorld, in Vec3 targetWorld, NavigationWorldOptions options)
        {
            if (grid == null) return NavigationPath.Failed;
            if (options == null) options = new NavigationWorldOptions();

            EnsureCapacity(grid.CellCount);
            _search++;
            if (_search <= 0) _search = 1; // 防溢出回绕
            _heapCount = 0;
            _insertCounter = 0;

            grid.WorldToCellClamped(in startWorld, out var sx, out var sz);
            grid.WorldToCellClamped(in targetWorld, out var gx, out var gz);

            var status = PathStatus.Found;
            var goalIndex = grid.Index(gx, gz);

            // 目标 blocked：投影到最近可行 cell。
            if (grid.IsBlocked(gx, gz))
            {
                if (TryNearestFree(grid, gx, gz, Math.Max(grid.Width, grid.Height), out var fx, out var fz))
                {
                    gx = fx;
                    gz = fz;
                    goalIndex = grid.Index(gx, gz);
                    status = PathStatus.Partial;
                }
                else
                {
                    return NavigationPath.Failed;
                }
            }

            var startIndex = grid.Index(sx, sz);
            SetG(startIndex, 0);
            _parent[startIndex] = -1;
            Push(Heuristic(sx, sz, gx, gz), 0, startIndex);

            var reached = false;
            var iterations = 0;
            var maxIterations = options.MaxIterations <= 0 ? int.MaxValue : options.MaxIterations;

            while (_heapCount > 0 && iterations < maxIterations)
            {
                iterations++;
                var cell = Pop(out var poppedG);
                if (_closedStamp[cell] == _search) continue;
                _closedStamp[cell] = _search;

                if (cell == goalIndex)
                {
                    reached = true;
                    break;
                }

                cell.ToCell(grid, out var cx, out var cz);

                for (int i = 0; i < 8; i++)
                {
                    if (i >= 4 && !options.AllowDiagonal) break;

                    var nx = cx + NeighborDx[i];
                    var nz = cz + NeighborDz[i];
                    if (!grid.IsInBounds(nx, nz)) continue;
                    if (grid.IsBlocked(nx, nz)) continue;

                    // 对角不切角：两个正交邻居必须都可行。
                    if (i >= 4)
                    {
                        if (grid.IsBlocked(cx + NeighborDx[i], cz)) continue;
                        if (grid.IsBlocked(cx, cz + NeighborDz[i])) continue;
                    }

                    var neighborIndex = grid.Index(nx, nz);
                    if (_closedStamp[neighborIndex] == _search) continue;

                    var tentativeG = poppedG + NeighborCost[i];
                    if (!HasG(neighborIndex) || tentativeG < _gCost[neighborIndex])
                    {
                        SetG(neighborIndex, tentativeG);
                        _parent[neighborIndex] = cell;
                        var f = tentativeG + Heuristic(nx, nz, gx, gz);
                        Push(f, tentativeG, neighborIndex);
                    }
                }
            }

            if (!reached) return NavigationPath.Failed;

            var cells = Reconstruct(grid, startIndex, goalIndex);
            var waypoints = CellsToWaypoints(grid, cells);

            if (options.SimplifyPath && waypoints.Length > 2)
            {
                waypoints = Simplify(grid, waypoints, cells);
            }

            // Found 时把末点替换为精确目标，使 actor 正好停在请求点。
            if (status == PathStatus.Found && waypoints.Length > 0)
            {
                waypoints[waypoints.Length - 1] = targetWorld;
            }

            return new NavigationPath(waypoints, status);
        }

        private int[] Reconstruct(NavigationGrid grid, int startIndex, int goalIndex)
        {
            var count = 0;
            var cursor = goalIndex;
            while (cursor >= 0)
            {
                count++;
                if (cursor == startIndex) break;
                cursor = _parent[cursor];
                if (count > grid.CellCount) break; // 保护
            }

            var cells = new int[count];
            cursor = goalIndex;
            for (int i = count - 1; i >= 0; i--)
            {
                cells[i] = cursor;
                if (cursor == startIndex) break;
                cursor = _parent[cursor];
            }
            return cells;
        }

        private static Vec3[] CellsToWaypoints(NavigationGrid grid, int[] cells)
        {
            var waypoints = new Vec3[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].ToCell(grid, out var cx, out var cz);
                waypoints[i] = grid.CellCenter(cx, cz);
            }
            return waypoints;
        }

        /// <summary>
        /// 贪心 LOS 拉直（string-pull）：从起点出发，跳到能直线通行（无 blocked cell 横穿）的最远路径点。
        /// </summary>
        private static Vec3[] Simplify(NavigationGrid grid, Vec3[] waypoints, int[] cells)
        {
            var keep = new bool[waypoints.Length];
            keep[0] = true;
            keep[waypoints.Length - 1] = true;

            var anchor = 0;
            while (anchor < waypoints.Length - 1)
            {
                cells[anchor].ToCell(grid, out var ax, out var az);
                var farthest = anchor;
                for (int j = waypoints.Length - 1; j > anchor; j--)
                {
                    cells[j].ToCell(grid, out var jx, out var jz);
                    if (HasClearLine(grid, ax, az, jx, jz))
                    {
                        farthest = j;
                        break;
                    }
                }

                if (farthest <= anchor) farthest = anchor + 1;
                keep[farthest] = true;
                anchor = farthest;
            }

            var kept = 0;
            for (int i = 0; i < keep.Length; i++) if (keep[i]) kept++;
            var result = new Vec3[kept];
            var k = 0;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (!keep[i]) continue;
                result[k++] = waypoints[i];
            }
            return result;
        }

        /// <summary>
        /// 超覆盖 Bresenham 直线通行性：起终 cell 之间的中间 cell 不得 blocked，且不切对角 blocked 角。
        /// </summary>
        private static bool HasClearLine(NavigationGrid grid, int x0, int z0, int x1, int z1)
        {
            var dx = Math.Abs(x1 - x0);
            var dz = Math.Abs(z1 - z0);
            var sx = x0 < x1 ? 1 : -1;
            var sz = z0 < z1 ? 1 : -1;
            var err = dx - dz;

            var x = x0;
            var z = z0;
            while (true)
            {
                if (x == x1 && z == z1) break;
                var e2 = err * 2;
                var xCross = e2 > -dz;
                var zCross = e2 < dx;
                if (xCross && zCross)
                {
                    if (grid.IsBlocked(x + sx, z)) return false;
                    if (grid.IsBlocked(x, z + sz)) return false;
                }
                if (xCross) { err -= dz; x += sx; }
                if (zCross) { err += dx; z += sz; }
                if (x == x1 && z == z1) break;
                if (grid.IsBlocked(x, z)) return false;
            }
            return true;
        }

        private static bool TryNearestFree(NavigationGrid grid, int cx, int cz, int maxRadius, out int fx, out int fz)
        {
            if (grid.IsInBounds(cx, cz) && !grid.IsBlocked(cx, cz))
            {
                fx = cx;
                fz = cz;
                return true;
            }

            var limit = Math.Min(maxRadius, Math.Max(grid.Width, grid.Height));
            for (var r = 1; r <= limit; r++)
            {
                for (var dz = -r; dz <= r; dz++)
                {
                    for (var dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        var nx = cx + dx;
                        var nz = cz + dz;
                        if (grid.IsInBounds(nx, nz) && !grid.IsBlocked(nx, nz))
                        {
                            fx = nx;
                            fz = nz;
                            return true;
                        }
                    }
                }
            }

            fx = cx;
            fz = cz;
            return false;
        }

        private static int Heuristic(int ax, int az, int bx, int bz)
        {
            var dx = Math.Abs(ax - bx);
            var dz = Math.Abs(az - bz);
            // octile: 14*min + 10*(max-min) == 10*max + 4*min
            return Math.Max(dx, dz) * OrthogonalCost + Math.Min(dx, dz) * (DiagonalCost - OrthogonalCost);
        }

        private void EnsureCapacity(int cellCount)
        {
            if (_gCost != null && _gridCells == cellCount) return;
            _gridCells = cellCount;
            _gCost = new int[cellCount];
            _gStamp = new int[cellCount];
            _closedStamp = new int[cellCount];
            _parent = new int[cellCount];
            _search = 0;
        }

        private bool HasG(int index) => _gStamp[index] == _search;
        private void SetG(int index, int value)
        {
            _gCost[index] = value;
            _gStamp[index] = _search;
        }

        private void Push(int f, int g, int cell)
        {
            if (_heapF == null)
            {
                const int initial = 256;
                _heapF = new int[initial];
                _heapOrder = new int[initial];
                _heapCell = new int[initial];
                _heapG = new int[initial];
            }
            if (_heapCount >= _heapF.Length)
            {
                var cap = _heapF.Length * 2;
                Array.Resize(ref _heapF, cap);
                Array.Resize(ref _heapOrder, cap);
                Array.Resize(ref _heapCell, cap);
                Array.Resize(ref _heapG, cap);
            }

            var i = _heapCount++;
            _heapF[i] = f;
            _heapOrder[i] = ++_insertCounter;
            _heapCell[i] = cell;
            _heapG[i] = g;

            while (i > 0)
            {
                var parent = (i - 1) >> 1;
                if (!IsBefore(i, parent)) break;
                Swap(i, parent);
                i = parent;
            }
        }

        private int Pop(out int g)
        {
            var cell = _heapCell[0];
            g = _heapG[0];
            Swap(0, --_heapCount);

            var i = 0;
            while (true)
            {
                var left = i * 2 + 1;
                if (left >= _heapCount) break;
                var right = left + 1;
                var best = right < _heapCount && IsBefore(right, left) ? right : left;
                if (!IsBefore(best, i)) break;
                Swap(best, i);
                i = best;
            }

            return cell;
        }

        // i 是否应排在 j 之前：f 小优先，f 相同则插入序小优先。
        private bool IsBefore(int i, int j)
        {
            var fi = _heapF[i];
            var fj = _heapF[j];
            if (fi != fj) return fi < fj;
            return _heapOrder[i] < _heapOrder[j];
        }

        private void Swap(int i, int j)
        {
            var f = _heapF[i]; _heapF[i] = _heapF[j]; _heapF[j] = f;
            var o = _heapOrder[i]; _heapOrder[i] = _heapOrder[j]; _heapOrder[j] = o;
            var c = _heapCell[i]; _heapCell[i] = _heapCell[j]; _heapCell[j] = c;
            var g = _heapG[i]; _heapG[i] = _heapG[j]; _heapG[j] = g;
        }
    }

    internal static class GridPathfinderCellExtensions
    {
        public static void ToCell(this int flatIndex, NavigationGrid grid, out int cx, out int cz)
        {
            cz = flatIndex / grid.Width;
            cx = flatIndex - cz * grid.Width;
        }
    }
}
