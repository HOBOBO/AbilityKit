using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Navigation
{
    /// <summary>
    /// 导航世界工厂。镜像 <c>CollisionWorldFactory</c>。
    /// </summary>
    public static class NavigationWorldFactory
    {
        public static INavigationWorld Create(NavigationGrid grid, NavigationWorldOptions options = null)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            return new NavigationWorld(grid, options ?? new NavigationWorldOptions());
        }
    }

    /// <summary>
    /// 默认导航服务实现：持有单个导航世界，烘焙后通过 <see cref="Rebuild"/> 替换。
    /// demo 侧（需 map 数据烘焙）可自行实现 <see cref="INavigationService"/>，复用本类型做组装。
    /// </summary>
    public sealed class NavigationService : INavigationService
    {
        public NavigationService()
        {
        }

        public INavigationWorld World { get; private set; }

        public void Rebuild(NavigationGrid grid, NavigationWorldOptions options = null)
        {
            World = grid == null ? null : new NavigationWorld(grid, options);
        }

        public void Dispose()
        {
            World = null;
        }
    }
}
