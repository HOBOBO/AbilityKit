using System;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Navigation
{
    /// <summary>
    /// 基于 <see cref="NavigationGrid"/> 的导航世界实现。
    /// </summary>
    public sealed class NavigationWorld : INavigationWorld
    {
        private readonly NavigationGrid _grid;
        private readonly GridPathfinder _pathfinder;
        private readonly NavigationWorldOptions _options;

        public NavigationWorld(NavigationGrid grid, NavigationWorldOptions options = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _options = options ?? new NavigationWorldOptions();
            _pathfinder = new GridPathfinder();
        }

        public NavigationGrid Grid => _grid;
        public NavigationWorldOptions Options => _options;

        public PathStatus FindPath(in Vec3 start, in Vec3 target, float agentRadius, out NavigationPath path)
        {
            path = _pathfinder.FindPath(_grid, in start, in target, _options);
            return path.Status;
        }

        public bool IsWalkable(in Vec3 position, float radius)
        {
            if (_grid == null) return false;
            _grid.WorldToCellClamped(in position, out var cx, out var cz);
            return _grid.IsInBounds(cx, cz) && !_grid.IsBlocked(cx, cz);
        }

        public bool TryProjectToWalkable(in Vec3 position, float radius, out Vec3 projected)
        {
            projected = position;
            if (_grid == null) return false;

            _grid.WorldToCellClamped(in position, out var cx, out var cz);
            if (_grid.IsInBounds(cx, cz) && !_grid.IsBlocked(cx, cz))
            {
                projected = _grid.CellCenter(cx, cz);
                return true;
            }

            // 螺旋搜索最近可行 cell。
            var limit = Math.Max(_grid.Width, _grid.Height);
            for (var r = 1; r <= limit; r++)
            {
                for (var dz = -r; dz <= r; dz++)
                {
                    for (var dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        var nx = cx + dx;
                        var nz = cz + dz;
                        if (_grid.IsInBounds(nx, nz) && !_grid.IsBlocked(nx, nz))
                        {
                            projected = _grid.CellCenter(nx, nz);
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
