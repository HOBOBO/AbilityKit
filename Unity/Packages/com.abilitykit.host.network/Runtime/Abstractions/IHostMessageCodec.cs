using System;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Ability.Host.Network
{
    /// <summary>
    /// 将 Host 消息映射到共享网络信封，不决定认证或会话生命周期。
    /// </summary>
    public interface IHostMessageCodec
    {
        /// <summary>
        /// 尝试编码消息；不支持此类型时返回假且不发送。
        /// </summary>
        bool TryEncode(
            ServerMessage message,
            out NetworkPacketHeader header,
            out ArraySegment<byte> payload);
    }
}
