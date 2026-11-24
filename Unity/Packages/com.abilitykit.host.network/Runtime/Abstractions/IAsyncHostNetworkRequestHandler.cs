using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Ability.Host.Network
{
    /// <summary>
    /// 按会话串行消费网络请求，支持断连和停止时取消。
    /// </summary>
    public interface IAsyncHostNetworkRequestHandler
    {
        /// <summary>
        /// 处理当前请求且响应取消；任务完成后不应保留借用的负载缓冲。
        /// </summary>
        Task HandleAsync(
            HostRuntime runtime,
            ServerClientId clientId,
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            CancellationToken cancellationToken);
    }
}
