using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// Entitas 世界上下文接口，继承自 IWorldContext。
    /// 提供 Entitas 特有的上下文和系统容器访问。
    /// </summary>
    public interface IEntitasWorldContext : IWorldContext
    {
        /// <summary>
        /// 获取 Entitas 上下文集合。
        /// </summary>
        global::Entitas.IContexts Contexts { get; }

        /// <summary>
        /// 获取 Entitas 系统容器。
        /// </summary>
        global::Entitas.Systems Systems { get; }
    }
}