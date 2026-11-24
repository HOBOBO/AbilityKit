using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Coordinator
{
    /// <summary>
    /// 允许项目在装配会话前调整配置，不负责启动或关闭连接。
    /// </summary>
    public interface ISessionCoordinatorConfigPolicy
    {
        /// <summary>
        /// 在调用方创建会话前就地调整配置。
        /// </summary>
        void ConfigureSession(ref SessionConfig config);
    }

    /// <summary>
    /// 由项目实现，为会话提供世界宿主、配置、服务和出生数据。
    /// </summary>
    public interface ISessionCoordinatorHost
    {
        /// <summary>
        /// 创建世界主机实例。
        /// </summary>
        IWorldHost CreateWorldHost(SessionConfig config);

        /// <summary>
        /// 在 coordinator 创建世界前配置世界创建选项。
        /// </summary>
        void ConfigureWorldCreateOptions(in SessionConfig config, WorldCreateOptions options);

        /// <summary>
        /// 向世界注册服务。
        /// </summary>
        void RegisterServices(IWorld world, SessionConfig config);

        /// <summary>
        /// 加载会话配置。
        /// </summary>
        void LoadConfig(IWorld world, SessionConfig config);

        /// <summary>
        /// 创建玩家出生数据。
        /// </summary>
        PlayerSpawnData[] CreatePlayerSpawnData(SessionConfig config);
    }
}
