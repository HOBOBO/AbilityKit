using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Coordinator.Core;

namespace AbilityKit.Coordinator
{
    /// <summary>
    /// 会话协调器接口。session engine（SessionCoordinator + sync-adapter 体系）已移除；
    /// 此接口当前无实现者，仅保留为端口契约聚合 —— 唯一外部引用是 MobaBattleDriverHost.Bind 的一个未使用参数。
    /// （SetViewEventSink/ViewEventSink/IViewEventSink 与 Hooks/SessionHooks 随 session engine 移除。）
    /// </summary>
    public interface ISessionCoordinator : IDisposable
    {
        // ============== 标识 ==============

        /// <summary>会话标识。</summary>
        SessionId SessionId { get; }

        /// <summary>会话配置。</summary>
        SessionConfig Config { get; }

        /// <summary>当前会话状态。</summary>
        SessionState State { get; }

        // ============== 世界访问 ==============

        /// <summary>世界宿主实例。</summary>
        IWorldHost WorldHost { get; }

        /// <summary>当前世界实例。</summary>
        IWorld World { get; }

        /// <summary>用于服务访问的世界解析器。</summary>
        IWorldResolver WorldResolver { get; }

        // ============== 驱动 ==============

        /// <summary>设置逻辑世界驱动桥接器。</summary>
        void SetLogicWorldDriver(ILogicWorldDriverBridge driverHost);

        /// <summary>获取逻辑世界驱动桥接器。</summary>
        ILogicWorldDriverBridge? LogicWorldDriver { get; }

        // ============== 生命周期 ==============

        /// <summary>初始化会话。</summary>
        void Initialize(SessionConfig config, ISessionCoordinatorHost host);

        /// <summary>启动会话。</summary>
        void Start();

        /// <summary>停止会话。</summary>
        void Stop();

        /// <summary>销毁会话并释放资源。</summary>
        void Destroy();

        // ============== 输入 ==============

        /// <summary>提交本地玩家输入。</summary>
        void SubmitLocalInput(PlayerInput input);

        // ============== 服务访问 ==============

        /// <summary>从世界中解析服务。</summary>
        T Resolve<T>() where T : class;

        /// <summary>尝试从世界中解析服务。</summary>
        bool TryResolve<T>(out T service) where T : class;
    }
}
