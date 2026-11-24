using System;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Ability.Host.Network
{
    /// <summary>
    /// 由应用将网络请求转换为 Host 或 World 命令。
    /// </summary>
    public interface IHostNetworkRequestHandler
    {
        /// <summary>
        /// 同步消费当前会话已解帧的请求；返回后不应保留借用的负载缓冲。
        /// </summary>
        void Handle(
            HostRuntime runtime,
            ServerClientId clientId,
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload);
    }
}
