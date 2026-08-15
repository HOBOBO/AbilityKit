using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using AbilityKit.Network.Protocol;
using AbilityKit.Orleans.Gateway.Abstractions;

namespace AbilityKit.Orleans.Gateway.Networking;

/// <summary>
/// WebSocket 传输层配置
/// </summary>
public sealed class WebSocketTransportOptions : GatewayTransportOptions
{
    public string Path { get; set; } = "/gateway";
    public int MaxFrameLength { get; set; } = 1024 * 1024;
    public int RequestTimeoutMs { get; set; } = 30000;
}

/// <summary>
/// WebSocket 传输层会话
/// </summary>
public sealed class WebSocketTransportSession : IGatewayTransportSession
{
    private readonly WebSocket _socket;
    private readonly TimeSpan _writeTimeout;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public long ConnectionId { get; }
    public string TransportName => "WebSocket";
    public bool IsConnected => _socket.State == WebSocketState.Open;
    public GatewaySessionContext Context { get; }

    internal WebSocketTransportSession(long connectionId, WebSocket socket, TimeSpan writeTimeout)
    {
        ConnectionId = connectionId;
        _socket = socket;
        _writeTimeout = writeTimeout;
        Context = new GatewaySessionContext(connectionId);
    }

    public Task SendResponseAsync(uint opCode, uint seq, byte[] payload, CancellationToken cancellationToken = default)
    {
        var header = new NetworkPacketHeader(NetworkPacketFlags.Response, opCode, seq, (uint)payload.Length);
        return WriteFrameAsync(header, payload, cancellationToken);
    }

    public Task SendServerPushAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default)
    {
        var header = new NetworkPacketHeader(NetworkPacketFlags.ServerPush, opCode, 0, (uint)payload.Length);
        return WriteFrameAsync(header, payload, cancellationToken);
    }

    private async Task WriteFrameAsync(NetworkPacketHeader header, byte[] payload, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open) return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_writeTimeout);
        await _writeLock.WaitAsync(timeout.Token);
        try
        {
            var frameSize = NetworkFrameCodec.GetFrameSize(payload.Length);
            var frame = new byte[frameSize];
            NetworkFrameCodec.WriteFrame(frame, header, payload);
            await _socket.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, timeout.Token);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal Task CloseAsync() => _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "server close", CancellationToken.None);
}

/// <summary>
/// WebSocket 传输层服务器。使用 HttpListener 接受 WebSocket 升级请求。
/// 与 TcpTransportServer 共享相同的 NetworkFrameCodec 帧格式。
/// </summary>
public sealed class WebSocketTransportServer : IGatewayTransportServer
{
    public string Name => "WebSocket";
    public bool IsEnabled => _options.Enabled;

    private readonly WebSocketTransportOptions _options;
    private readonly IGatewayTransportEvents _events;
    private readonly ILogger<WebSocketTransportServer> _logger;
    private readonly object _lifecycleGate = new();
    private readonly ConcurrentDictionary<long, WebSocket> _sockets = new();
    private HttpListener? _listener;
    private long _nextConnectionId;

    public WebSocketTransportServer(
        IOptions<WebSocketTransportOptions> options,
        IGatewayTransportEvents events,
        ILogger<WebSocketTransportServer> logger)
    {
        _options = options.Value;
        _events = events;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("WebSocketTransport is disabled.");
            return;
        }

        var prefix = $"http://{_options.Host}:{_options.Port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);

        lock (_lifecycleGate)
        {
            if (_listener is not null)
                throw new InvalidOperationException("WebSocketTransport is already running.");

            listener.Start();
            _listener = listener;
        }

        _logger.LogInformation("WebSocketTransport listening on {Prefix} path={Path}", prefix, _options.Path);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
                if (!IsConfiguredPath(context.Request.Url?.AbsolutePath))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(null);
                var connectionId = Interlocked.Increment(ref _nextConnectionId);
                _sockets.TryAdd(connectionId, wsContext.WebSocket);

                TrackTask(Task.Run(() => HandleWebSocketAsync(connectionId, wsContext.WebSocket, cancellationToken)), CancellationToken.None);
            }
        }
        catch (OperationCanceledException) { }
        catch (HttpListenerException) when (!ReferenceEquals(GetListener(), listener)) { }
        catch (ObjectDisposedException) when (!ReferenceEquals(GetListener(), listener)) { }
        finally
        {
            lock (_lifecycleGate) { if (ReferenceEquals(_listener, listener)) _listener = null; }
            listener.Close();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        HttpListener? listener;
        lock (_lifecycleGate) { listener = _listener; _listener = null; }
        listener?.Close();
        foreach (var socket in _sockets.Values)
        {
            try { if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "stopping", cancellationToken); }
            catch { }
        }
    }

    private HttpListener? GetListener()
    {
        lock (_lifecycleGate)
        {
            return _listener;
        }
    }

    private bool IsConfiguredPath(string? requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return false;
        }

        var configuredPath = string.IsNullOrWhiteSpace(_options.Path)
            ? "/"
            : _options.Path;
        if (!configuredPath.StartsWith('/'))
        {
            configuredPath = "/" + configuredPath;
        }

        return string.Equals(requestPath, configuredPath, StringComparison.OrdinalIgnoreCase);
    }

    private async Task HandleWebSocketAsync(long connectionId, WebSocket socket, CancellationToken cancellationToken)
    {
        var session = new WebSocketTransportSession(
            connectionId, socket,
            TimeSpan.FromMilliseconds(_options.RequestTimeoutMs));
        _events.OnConnected(session);
        _logger.LogInformation("WebSocket client connected: ConnectionId={ConnectionId}", connectionId);

        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(64 * 1024);
        var buffered = new System.IO.MemoryStream();

        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                buffered.SetLength(0);
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (result.Count > 0) buffered.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;
                if (buffered.Length == 0) continue;

                // WebSocket messages are frame-complete — parse all frames in the message
                var data = buffered.GetBuffer();
                var dataLen = (int)buffered.Length;
                var offset = 0;
                while (offset < dataLen)
                {
                    if (!NetworkFrameCodec.TryParseFrame(
                        new ReadOnlySpan<byte>(data, offset, dataLen - offset),
                        out var header,
                        out var payloadSpan))
                        break;

                    var totalSize = NetworkFrameCodec.GetFrameSize((int)header.PayloadLength);
                    if (totalSize > _options.MaxFrameLength)
                    {
                        _logger.LogError("Frame too large: {Size}", totalSize);
                        break;
                    }

                    _events.OnRequest(connectionId, header.OpCode, header.Seq, payloadSpan.ToArray());
                    offset += totalSize;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket client error: ConnectionId={ConnectionId}", connectionId);
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            _events.OnClosed(connectionId);
            _sockets.TryRemove(connectionId, out _);
            if (socket.State == WebSocketState.Open)
            {
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client disconnected", CancellationToken.None); } catch { }
            }
            socket.Dispose();
            _logger.LogInformation("WebSocket client disconnected: ConnectionId={ConnectionId}", connectionId);
        }
    }

    private readonly List<Task> _trackedTasks = new();
    private readonly object _taskGate = new();

    private void TrackTask(Task task, CancellationToken cancellationToken)
    {
        lock (_taskGate) { _trackedTasks.Add(task); }
        _ = task.ContinueWith(t =>
        {
            lock (_taskGate) { _trackedTasks.Remove(t); }
        }, TaskScheduler.Default);
    }
}
