using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// Entitas 系统安装器接口。
    /// 实现此接口的模块负责将系统注册到 Entitas 系统中。
    /// </summary>
    public interface IEntitasSystemsInstaller
    {
        /// <summary>
        /// 将系统安装到 Entitas 系统容器中。
        /// </summary>
        /// <param name="contexts">Entitas 上下文集合</param>
        /// <param name="systems">Entitas 系统容器</param>
        /// <param name="services">服务解析器，用于获取依赖的服务</param>
        void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldResolver services);
    }
}