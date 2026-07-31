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

        /// <summary>根据当前已加载地图烘焙导航网格。需在 <see cref="IMobaMapRuntimeService.Load"/> 之后调用。</summary>
        public void Build()
        {
            var map = _maps.CurrentMap;
            if (map == null)
            {
                World = null;
                return;
            }

            var grid = MobaNavigationBake.Build(map, _collisionWorld, _options);
            World = new NavigationWorld(grid, _options);

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
        }
    }
}
