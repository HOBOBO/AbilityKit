using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host.Extensions.WorldStart
{
    /// <summary>
    /// 世界提供的自动启动策略，由宿主自动启动模块在帧尾查询与调用。
    /// </summary>
    public interface IWorldAutoStartHandler : IService
    {
        /// <summary>
        /// 尝试启动世界。返回真表示已完成；返回假表示后续宿主帧可以重试。
        /// </summary>
        bool TryAutoStart(IWorld world, float deltaTime);
    }
}
