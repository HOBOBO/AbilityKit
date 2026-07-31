using AbilityKit.Ability.World.Services;

namespace AbilityKit.Combat.Navigation
{
    /// <summary>
    /// 导航服务：持有 <see cref="Core.Mathematics.INavigationWorld"/> 的 DI 单例。
    /// 镜像 <c>ICollisionService</c>：消费方通过 DI 注册，包内不自注册。
    /// </summary>
    public interface INavigationService : IService
    {
        Core.Mathematics.INavigationWorld World { get; }
    }
}
