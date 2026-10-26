using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Core.Buffers;
using AbilityKit.Network.Abstractions;
#if UNITY_WEBGL && !UNITY_EDITOR
using AbilityKit.Network.Transport.WebSocket.WebGL;
#endif

namespace AbilityKit.Network.Transport.WebSocket
{
    /// <summary>
    /// <see cref="ITransport"/> over WebSocket (<see cref="ClientWebSocket"/>).
    /// 适用桌面/移动/服务端（**WebGL 不支持 ClientWebSocket** —— WebGL 需平台特化版本，因浏览器 WS 走 JS 桥）。
    ///
    /// 连接 ws://host:port/path（path/scheme 由 ctor 配置，默认 ws + "/"）。WebSocket 是消息边界协议：
    /// 每条二进制消息作为一次 <see cref="BytesReceived"/> 上抛 —— 载荷即 <c>ConnectionManager</c> 的成帧字节
    /// （length-prefix 保留），所以对上层 <c>LengthPrefixedFrameCodec</c> 是透明替换 TCP。
    /// </summary>
    public sealed class WebSocketTransport : ITransport
    {
        private readonly object _gate = new object();
        private readonly string _path;
        private readonly string _scheme;

#if UNITY_WEBGL && !UNITY_EDITOR
        private WebGlWebSocketBridge _webGlSocket;
#else
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private Task _receiveLoop;
#endif

        public WebSocketTransport(string path = "/", bool secure = false)
        {
            _path = string.IsNullOrEmpty(path) ? "/" : path;
            _scheme = secure ? "wss" : "ws";
        }

        public bool IsConnected
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return _webGlSocket != null && _webGlSocket.IsConnected;
#else
                var s = _socket;
                return s != null && s.State == WebSocketState.Open;
#endif
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
#if UNITY_WEBGL && !UNITY_EDITOR
                if (_webGlSocket != null) throw new InvalidOperationException("Transport already started.");
                _webGlSocket = new WebGlWebSocketBridge();
                _webGlSocket.Connected += () => Connected?.Invoke();
                _webGlSocket.Disconnected += () => Disconnected?.Invoke();
                _webGlSocket.Error += ex => Error?.Invoke(ex);
                _webGlSocket.BytesReceived += bytes => BytesReceived?.Invoke(bytes);
                _webGlSocket.Connect(new Uri($"{_scheme}://{host}:{port}{_path}"));
#else
                if (_socket != null) throw new InvalidOperationException("Transport already started.");
                _cts = new CancellationTokenSource();
                _socket = new ClientWebSocket();
                var socket = _socket;
                _receiveLoop = Task.Run(() => RunAsync(socket, host, port, _cts.Token));
#endif
            }
        }

        public void Send(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count <= 0) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            WebGlWebSocketBridge socket;
            lock (_gate) { socket = _webGlSocket; }
            if (socket == null || !socket.IsConnected) throw new InvalidOperationException("Not connected.");
            socket.Send(bytes);
#else
            ClientWebSocket socket;
            lock (_gate) { socket = _socket; }
            if (socket == null || socket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("Not connected.");
            }

            try
            {
                socket.SendAsync(bytes, WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
                Close();
                throw;
            }
#endif
        }

        public void Close()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGlWebSocketBridge socket;
            lock (_gate)
            {
                socket = _webGlSocket;
                _webGlSocket = null;
            }
            socket?.Dispose();
#else
            ClientWebSocket socket;
            CancellationTokenSource cts;
            lock (_gate)
            {
                socket = _socket;
                cts = _cts;
                _socket = null;
                _cts = null;
                _receiveLoop = null;
            }

            try { cts?.Cancel(); } catch { }

            if (socket != null)
            {
                try
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client close", CancellationToken.None).GetAwaiter().GetResult();
                    }
                }
                catch { }
                try { socket.Dispose(); } catch { }
            }
#endif
        }

        public void Dispose() => Close();

#if !UNITY_WEBGL || UNITY_EDITOR
        private async Task RunAsync(ClientWebSocket socket, string host, int port, CancellationToken ct)
        {
            try
            {
                var uri = new Uri($"{_scheme}://{host}:{port}{_path}");
                await socket.ConnectAsync(uri, ct).ConfigureAwait(false);
                if (ct.IsCancellationRequested) return;

                Connected?.Invoke();

                using var receiveOwner = PooledBufferOwner<byte>.Rent(64 * 1024);
                var receiveBuffer = receiveOwner.Segment;
                while (!ct.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    using var message = new MemoryStream();
                    do
                    {
                        result = await socket.ReceiveAsync(receiveBuffer, ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

                        if (result.Count > 0)
                        {
                            message.Write(receiveBuffer.Array!, 0, result.Count);
                        }
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (message.Length > 0)
                    {
                        // Use pooled buffer instead of ToArray() to avoid per-message allocation
                        var length = (int)message.Length;
                        var rented = ArrayPool<byte>.Shared.Rent(length);
                        try
                        {
                            message.GetBuffer().AsSpan(0, length).CopyTo(rented);
                            BytesReceived?.Invoke(new ArraySegment<byte>(rented, 0, length));
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(rented);
                        }
                    }
                }

                if (!ct.IsCancellationRequested)
                {
                    Disconnected?.Invoke();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
                Disconnected?.Invoke();
            }
            finally
            {
                Close();
            }
        }
#endif
    }
}
