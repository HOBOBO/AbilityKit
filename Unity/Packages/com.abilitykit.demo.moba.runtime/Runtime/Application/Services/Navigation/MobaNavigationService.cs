using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Navigation;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services.Map;

namespace AbilityKit.Demo.Moba.Services.Navigation
{
    /// <summary>
    /// MOBA 导航运行时服务。烘焙 <see cref="BattleMapMO"/> 为导航网格，提供 <see cref="INavigationWorld"/>。
    ///
    /// 依赖 <see cref="ICollisionService"/>（烘焙时查 World 层障碍）与 <see cref="IMobaMapRuntimeService"/>（地图数据）。
    /// 由 <c>MapRuntimeStage</c> 在 <c>maps.Load</c> 之后调用 <see cref="Build"/> 触发烘焙。
    /// 注册范式镜像 <see cref="MobaMapRuntimeService"/>（[WorldService] Scoped + 构造器注入）。
    /// </summary>
    [WorldService(typeof(INavigationService), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaNavigationService), WorldLifetime.Scoped)]
    public sealed class MobaNavigationService : INavigationService
    {
        private readonly ICollisionWorld _collisionWorld;
        private readonly IMobaMapRuntimeService _maps;
        private readonly NavigationWorldOptions _options;
        private readonly NavigationDebugState _debug;
        private bool _buildCompleted;
        private bool _predictionNavigationEnabled;

        public MobaNavigationService(ICollisionService collisionService, IMobaMapRuntimeService maps)
            : this(collisionService, maps, new NavigationWorldOptions(), null)
        {
        }

        public MobaNavigationService(ICollisionService collisionService, IMobaMapRuntimeService maps, NavigationWorldOptions options, NavigationDebugState debug = null)
        {
            _collisionWorld = collisionService?.World ?? throw new ArgumentNullException(nameof(collisionService));
            _maps = maps ?? throw new ArgumentNullException(nameof(maps));
            _options = options ?? new NavigationWorldOptions();
            _debug = debug;
        }

        public INavigationWorld World { get; private set; }

        public bool IsBuilt => World != null;

        internal void EnablePredictionNavigation()
        {
            if (_predictionNavigationEnabled) return;
            if (World is NavigationWorld world)
                World = new FrozenNavigationWorld(world.Grid, world.Options);
            else if (World != null)
                throw new InvalidOperationException("Prediction requires a standard MOBA navigation world.");
            _predictionNavigationEnabled = true;
        }

        internal FrozenNavigationWorld GetPredictionInput(out bool buildCompleted)
        {
            if (!_predictionNavigationEnabled)
                throw new InvalidOperationException("Prediction navigation was not enabled for this world.");
            buildCompleted = _buildCompleted;
            return (FrozenNavigationWorld)World;
        }

        /// <summary>根据当前已加载地图烘焙导航网格。需在 <see cref="IMobaMapRuntimeService.Load"/> 之后调用。</summary>
        public void Build()
        {
            if (_predictionNavigationEnabled && _buildCompleted)
                throw new InvalidOperationException("Cannot rebuild navigation during a MOBA prediction session.");
            var map = _maps.CurrentMap;
            if (map == null)
            {
                World = null;
                _buildCompleted = true;
                return;
            }

            var grid = MobaNavigationBake.Build(map, _collisionWorld, _options);
            World = _predictionNavigationEnabled
                ? (INavigationWorld)new FrozenNavigationWorld(grid, _options)
                : new NavigationWorld(grid, _options);
            _buildCompleted = true;

            if (_debug != null)
            {
                _debug.Grid = grid;
                _debug.Options = _options;
                _debug.ActivePaths.Clear();
            }
        }

        public void Dispose()
        {
            World = null;
            _predictionNavigationEnabled = false;
        }

        /// <summary>
        /// 仅暴露导航查询；内部世界持有不与构建者和调试视图共享的输入。
        /// </summary>
        internal sealed class FrozenNavigationWorld : INavigationWorld
        {
            private readonly NavigationWorld _world;

            internal FrozenNavigationWorld(NavigationGrid grid, NavigationWorldOptions options)
            {
                var blocked = new bool[grid.CellCount];
                for (var index = 0; index < blocked.Length; index++) blocked[index] = grid.IsBlocked(index);
                _world = new NavigationWorld(new NavigationGrid(grid.Origin, grid.CellSize, grid.Width, grid.Height, blocked),
                    CopyOptions(options));
            }

            internal NavigationGrid Grid => _world.Grid;

            internal NavigationWorldOptions CopyOptions() => CopyOptions(_world.Options);

            private static NavigationWorldOptions CopyOptions(NavigationWorldOptions options) => new NavigationWorldOptions
            {
                CellSize = options.CellSize,
                AgentRadius = options.AgentRadius,
                AllowDiagonal = options.AllowDiagonal,
                MaxIterations = options.MaxIterations,
                SimplifyPath = options.SimplifyPath,
            };

            /// <summary>
            /// 用会话冻结输入规划路径。
            /// </summary>
            public PathStatus FindPath(in Vec3 start, in Vec3 target, float agentRadius, out NavigationPath path) =>
                _world.FindPath(in start, in target, agentRadius, out path);

            /// <summary>
            /// 用会话冻结输入检查可行走性。
            /// </summary>
            public bool IsWalkable(in Vec3 position, float radius) => _world.IsWalkable(in position, radius);

            /// <summary>
            /// 用会话冻结输入投影到可行走位置。
            /// </summary>
            public bool TryProjectToWalkable(in Vec3 position, float radius, out Vec3 projected) =>
                _world.TryProjectToWalkable(in position, radius, out projected);
        }
    }
}
