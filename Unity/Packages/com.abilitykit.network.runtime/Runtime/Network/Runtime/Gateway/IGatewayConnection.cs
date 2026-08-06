using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Runtime.Gateway
{
    /// <summary>
    /// 统一的网关连接抽象。封装请求/响应发送和推送接收，
    /// 同时适用于房间管理协议和战斗数据传输。
    ///
    /// 使用方式：
    /// <code>
    /// var conn = GatewayConnection.Create(connection);
    /// conn.RegisterPushHandler(opCode, payload => { ... });
    /// var response = await conn.SendRequestAsync(opCode, payload);
    /// </code>
    /// </summary>
    public interface IGatewayConnection
    {
        /// <summary>底层原始连接。</summary>
        IConnection RawConnection { get; }

        /// <summary>连接是否活跃。</summary>
        bool IsConnected { get; }

        /// <summary>发送请求并等待响应。</summary>
        Task<byte[]> SendRequestAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default);

        /// <summary>发送服务器推送（单向，不等待响应）。</summary>
        Task SendPushAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default);

        /// <summary>注册服务器推送处理器。</summary>
        void RegisterPushHandler(uint opCode, Action<byte[]> handler);

        /// <summary>取消注册服务器推送处理器。</summary>
        void UnregisterPushHandler(uint opCode, Action<byte[]> handler);
    }
}
