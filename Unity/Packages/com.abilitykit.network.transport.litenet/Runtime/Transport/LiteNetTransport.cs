using System;
using AbilityKit.Network.Abstractions;
using LiteNetLib;
using LiteNetLib.Utils;

namespace AbilityKit.Network.Transport.LiteNet
{
    /// <summary>
    /// <see cref="ITransport"/> over LiteNetLib's reliable-ordered UDP (<see cref="DeliveryMethod.ReliableOrdered"/>).
    /// 适合快节奏、丢包场景（比 TCP 低延迟）。用 <c>UnsyncedEvents = true</c> —— 事件在 LiteNetLib 内部线程触发，
    /// 无需外部 PollEvents/tick，符合 <see cref="ITransport"/> 无 tick 接口。
    ///
    /// 注意：LiteNetLib 自带连接管理 + 可靠传输；作为 ITransport 使用时，<c>ConnectionManager</c> 的成帧字节
    /// 由 LiteNetLib 可靠通道承载（可靠传输两层叠加，冗余但兼容 —— 与 WebSocket 同理）。connection key 需客户端
    /// 与服务端一致（ctor 配置，默认 "abilitykit"）。
    /// </summary>
    public sealed class LiteNetTransport : ITransport
    {
        private readonly object _gate = new object();
        private readonly string _connectionKey;

        private NetManager _manager;
        private NetPeer _peer;

        public LiteNetTransport(string connectionKey = "abilitykit")
        {
            _connectionKey = connectionKey ?? string.Empty;
        }

        public bool IsConnected
        {
            get
            {
                var p = _peer;
                return p != null && p.ConnectionState == LiteNetLib.ConnectionState.Connected;
            }
        }

        public event Action Connected;
        public event Action Disconnected;
        public event Action<Exception> Error;
        public event Action<ArraySegment<byte>> BytesReceived;

        public void Connect(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));

            lock (_gate)
            {
                if (_manager != null) throw new InvalidOperationException("Transport already started.");

                var listener = new EventBasedNetListener();
                listener.PeerConnectedEvent += peer =>
                {
                    lock (_gate) { _peer = peer; }
                    Connected?.Invoke();
                };
                listener.PeerDisconnectedEvent += (peer, info) => Disconnected?.Invoke();
                listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
                {
                    var bytes = reader.GetRemainingBytes();
                    if (bytes != null && bytes.Length > 0)
                    {
                        BytesReceived?.Invoke(new ArraySegment<byte>(bytes));
                    }
                };

                _manager = new NetManager(listener) { UnsyncedEvents = true };
                _manager.Start();
                _manager.Connect(host, port, _connectionKey);
            }
        }

        public void Send(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count <= 0) return;

            NetPeer peer;
            lock (_gate) { peer = _peer; }
            if (peer == null)
            {
                throw new InvalidOperationException("Not connected.");
            }

            try
            {
                peer.Send(bytes.Array, bytes.Offset, bytes.Count, DeliveryMethod.ReliableOrdered);
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
                Close();
                throw;
            }
        }

        public void Close()
        {
            NetManager manager;
            lock (_gate)
            {
                manager = _manager;
                _manager = null;
                _peer = null;
            }

            try { manager?.Stop(); } catch { }
        }

        public void Dispose() => Close();
    }
}
