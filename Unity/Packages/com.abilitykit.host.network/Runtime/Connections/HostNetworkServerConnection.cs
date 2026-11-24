using System;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Network.Host;

namespace AbilityKit.Ability.Host.Network
{
    /// <summary>
    /// 将 Host 出站消息编码后交给已接受的网络会话，不拥有会话。
    /// </summary>
    public sealed class HostNetworkServerConnection : IServerConnection
    {
        private readonly object _identityGate = new object();
        private readonly IServerNetworkSession _session;
        private readonly IHostMessageCodec _codec;
        private ServerClientId _clientId;

        /// <summary>
        /// 为给定身份、会话和编码器创建出站连接。
        /// </summary>
        public HostNetworkServerConnection(
            ServerClientId clientId,
            IServerNetworkSession session,
            IHostMessageCodec codec)
        {
            _clientId = clientId;
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        /// <summary>
        /// 获取当前 Host 身份；身份可在认证后由管理器重新绑定。
        /// </summary>
        public ServerClientId ClientId { get { lock (_identityGate) return _clientId; } }
        /// <summary>
        /// 获取承载当前连接的网络会话。
        /// </summary>
        public IServerNetworkSession Session => _session;

        /// <summary>
        /// 编码并发送 Host 消息；找不到编码时抛出异常。
        /// </summary>
        public void Send(ServerMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (!_codec.TryEncode(message, out var header, out var payload))
            {
                throw new InvalidOperationException(
                    $"No host message encoding is available for '{message.GetType().FullName}'.");
            }
            _session.Send(header, payload);
        }

        internal void Rebind(ServerClientId clientId)
        {
            lock (_identityGate) _clientId = clientId;
        }
    }
}
