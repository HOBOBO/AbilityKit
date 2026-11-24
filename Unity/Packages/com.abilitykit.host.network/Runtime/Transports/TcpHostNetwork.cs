using System;
using System.Net;
using AbilityKit.Network.Host;
using AbilityKit.Network.Host.Tcp;

namespace AbilityKit.Ability.Host.Network
{
    /// <summary>
    /// 提供 TCP 监听装配，不是连接桥唯一支持的传输。
    /// </summary>
    public static class TcpHostNetwork
    {
        /// <summary>
        /// 创建使用已有监听配置的连接桥，由调用方调用 Start 启动。
        /// </summary>
        public static HostNetworkConnectionManager CreateConfigured(
            TcpChannelListenerOptions listenerOptions,
            IHostMessageCodec messageCodec,
            IHostNetworkRequestHandler requestHandler = null,
            IHostClientIdResolver clientIdResolver = null,
            NetworkHostOptions networkOptions = null)
        {
            if (listenerOptions == null) throw new ArgumentNullException(nameof(listenerOptions));
            return new HostNetworkConnectionManager(
                () => new TcpChannelListener(listenerOptions),
                messageCodec,
                requestHandler,
                clientIdResolver,
                networkOptions);
        }

        /// <summary>
        /// 创建接受显式监听地址与端口的连接桥，由调用方调用 StartListen 启动。
        /// </summary>
        public static HostNetworkConnectionManager Create(
            IHostMessageCodec messageCodec,
            IHostNetworkRequestHandler requestHandler = null,
            IHostClientIdResolver clientIdResolver = null,
            NetworkHostOptions networkOptions = null,
            Action<TcpChannelListenerOptions> configure = null)
        {
            return new HostNetworkConnectionManager(
                (address, port) =>
                {
                    var options = new TcpChannelListenerOptions
                    {
                        Address = ResolveAddress(address),
                        Port = port
                    };
                    configure?.Invoke(options);
                    return new TcpChannelListener(options);
                },
                messageCodec,
                requestHandler,
                clientIdResolver,
                networkOptions);
        }

        private static IPAddress ResolveAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address) || address == "*" || address == "0.0.0.0")
                return IPAddress.Any;
            if (string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase))
                return IPAddress.Loopback;
            if (IPAddress.TryParse(address, out var parsed)) return parsed;
            throw new ArgumentException($"TCP listen address is not a valid IP address: {address}", nameof(address));
        }
    }
}
