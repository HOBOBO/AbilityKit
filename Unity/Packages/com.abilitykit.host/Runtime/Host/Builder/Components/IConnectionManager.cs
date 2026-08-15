using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;

namespace AbilityKit.Ability.Host.Builder.Components
{
    /// <summary>
    /// 连接管理器接口
    /// 负责管理客户端连接
    /// </summary>
    public interface IConnectionManager
    {
        /// <summary>
        /// 附加到 Runtime
        /// </summary>
        void Attach(HostRuntime runtime);

        /// <summary>
        /// 从 Runtime 分离
        /// </summary>
        void Detach();

        /// <summary>
        /// 获取所有连接
        /// </summary>
        IReadOnlyCollection<IServerConnection> Connections { get; }

        /// <summary>
        /// 连接事件
        /// </summary>
        event Action<IServerConnection> OnClientConnected;
        event Action<ServerClientId> OnClientDisconnected;
    }

    /// <summary>
    /// 传输无关的连接接入生命周期。具体 endpoint 由实现自己的配置持有。
    /// </summary>
    public interface IConnectionManagerLifecycle
    {
        void Start();
        void Stop();
    }

    /// <summary>
    /// 可选的 address/port 端点能力，只适用于 TCP、UDP 等 IP 传输。
    /// </summary>
    public interface IEndpointConnectionManager
    {
        void StartListen(string address, int port);
    }
}
