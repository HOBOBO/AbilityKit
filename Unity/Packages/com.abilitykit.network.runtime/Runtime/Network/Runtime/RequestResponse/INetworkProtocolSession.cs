using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 通用请求会话抽象。封装请求/响应发送和推送接收。
    ///
    /// 使用方式：
    /// <code>
    /// using var session = NetworkProtocolSession.Create(connection, codec);
    /// session.RegisterPushHandler(opCode, HandlePush);
    /// var response = await session.SendRequestAsync(opCode, payload);
    /// </code>
    /// </summary>
    public interface INetworkProtocolSession
    {
        /// <summary>
        /// 获取调用方持有的底层连接；释放会话不转移或释放该连接。
        /// </summary>
        IConnection RawConnection { get; }

        /// <summary>
        /// 指示会话是否可用且底层连接活跃。
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 发送请求并等待响应；连接报告断开或错误时，失败尚未完成的请求。
        /// </summary>
        Task<byte[]> SendRequestAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送单向推送；任务完成仅表示已调用底层发送，不等待对端确认。
        /// </summary>
        Task SendPushAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default);

        /// <summary>
        /// 注册服务器推送处理器；处理器收到传输缓冲区的副本。
        /// </summary>
        void RegisterPushHandler(uint opCode, Action<byte[]> handler);

        /// <summary>
        /// 取消注册服务器推送处理器；不等待已开始的回调退出。
        /// </summary>
        void UnregisterPushHandler(uint opCode, Action<byte[]> handler);
    }
}
