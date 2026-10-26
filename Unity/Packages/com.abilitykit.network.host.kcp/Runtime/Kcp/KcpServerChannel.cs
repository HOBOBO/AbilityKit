using System;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using AbilityKit.Network.Host;
using ET;

namespace AbilityKit.Network.Host.Kcp
{
    public sealed class KcpServerChannel : IServerChannel
    {
        private readonly object _gate = new object();
        private readonly Socket _socket;
        private readonly IPEndPoint _remote;
        private readonly Action<KcpServerChannel> _closed;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private ET.Kcp _kcp;
        private int _closedFlag;

        internal KcpServerChannel(
            string id,
            Socket socket,
            IPEndPoint remote,
            uint conversationId,
            Action<KcpServerChannel> closed)
        {
            Id = id;
            _socket = socket;
            _remote = remote;
            _closed = closed;
            _kcp = new ET.Kcp(conversationId, Output);
            _kcp.SetNoDelay(1, 10, 2, 1);
            _kcp.SetWindowSize(256, 256);
            _kcp.SetMtu(1200);
            _kcp.SetMinrto(30);
        }

        public string Id { get; }
        public string RemoteEndpoint => _remote.ToString();
        public bool IsConnected => Volatile.Read(ref _closedFlag) == 0;

        public event Action<ArraySegment<byte>> BytesReceived;
        public event Action<IServerChannel> Closed;
        public event Action<Exception> Error;

        internal void AcceptDatagram(byte[] bytes, int count)
        {
            lock (_gate)
            {
                if (_kcp == null || !IsConnected) return;
                try
                {
                    _kcp.Input(bytes, 0, count);
                    DrainReceived();
                }
                catch (Exception ex)
                {
                    PublishError(ex);
                    Close();
                }
            }
        }

        internal void Update(uint now)
        {
            lock (_gate)
            {
                if (_kcp == null || !IsConnected) return;
                try { _kcp.Update(now); }
                catch (Exception ex)
                {
                    PublishError(ex);
                    Close();
                }
            }
        }

        public void Send(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count == 0) return;
            lock (_gate)
            {
                if (_kcp == null || !IsConnected) throw new InvalidOperationException("Channel is closed.");
                if (_kcp.Send(bytes) < 0) throw new InvalidOperationException("KCP send queue rejected the payload.");
                _kcp.Update(unchecked((uint)_clock.ElapsedMilliseconds));
                _kcp.Flush();
            }
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closedFlag, 1) != 0) return;
            lock (_gate)
            {
                _kcp?.Dispose();
                _kcp = null;
            }
            _closed?.Invoke(this);
            Closed?.Invoke(this);
        }

        public void Dispose() => Close();

        private void Output(byte[] bytes, int count)
        {
            if (count <= 0 || !IsConnected) return;
            try { _socket.SendTo(bytes, checked((int)ET.KCPBASIC.REVERSED_HEAD), count, SocketFlags.None, _remote); }
            catch (Exception ex) { PublishError(ex); Close(); }
        }

        private void DrainReceived()
        {
            while (_kcp != null)
            {
                var size = _kcp.PeekSize();
                if (size <= 0) return;
                var payload = new byte[size];
                var count = _kcp.Receive(payload, size);
                if (count <= 0) return;
                BytesReceived?.Invoke(new ArraySegment<byte>(payload, 0, count));
            }
        }

        private void PublishError(Exception exception)
        {
            try { Error?.Invoke(exception); } catch { }
        }
    }
}
