using System;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Network.Battle
{
    /// <summary>
    /// 网络客户端接口
    /// 抽象与服务器通信的底层能力
    /// 支持请求-响应模式和服务器推送模式
    /// </summary>
    public interface INetworkClient : IDisposable
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接成功事件
        /// </summary>
        event Action OnConnected;

        /// <summary>
        /// 断开连接事件
        /// </summary>
        event Action<string> OnDisconnected;

        /// <summary>
        /// 错误事件
        /// </summary>
        event Action<Exception> OnError;

        /// <summary>
        /// 服务器推送接收事件
        /// </summary>
        event Action<uint, byte[]> OnServerPush;

        /// <summary>
        /// 连接到服务器
        /// </summary>
        void Connect(string host, int port);

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 发送请求并等待响应（请求-响应模式）
        /// </summary>
        Task<byte[]> SendRequestAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送服务器推送（单向发送，无需等待响应）
        /// </summary>
        Task SendServerPushAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default);
    }
}
