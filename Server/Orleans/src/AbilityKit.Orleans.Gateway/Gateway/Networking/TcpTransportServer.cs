using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using AbilityKit.Network.Protocol;
using AbilityKit.Orleans.Gateway.Abstractions;

namespace AbilityKit.Orleans.Gateway.Networking;

/// <summary>
/// TCP 传输层配置
/// </summary>
public sealed class TcpTransportOptions : GatewayTransportOptions
{
    public int MaxFrameLength { get; set; } = 1024 * 1024;
    public int RequestTimeoutMs { get; set; } = 30000;
}

/// <summary>
/// TCP 传输层会话
/// </summary>
public sealed class TcpTransportSession : IGatewayTransportSession
{
    private readonly Stream _stream;
    private readonly TimeSpan _writeTimeout;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public long ConnectionId { get; }
    public string TransportName => "TCP";

    public bool IsConnected => _stream.CanWrite;

    public GatewaySessionContext Context { get; }

    internal TcpTransportSession(
        long connectionId,
        Stream stream,
        TimeSpan writeTimeout)
    {
        if (writeTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(writeTimeout));

        ConnectionId = connectionId;
        _stream = stream;
        _writeTimeout = writeTimeout;
        Context = new GatewaySessionContext(connectionId);
    }

    public Task SendResponseAsync(uint opCode, uint seq, byte[] payload, CancellationToken cancellationToken = default)
    {
        var header = new NetworkPacketHeader(NetworkPacketFlags.Response, opCode, seq, (uint)payload.Length);
        return WriteFrameWithTimeoutAsync(header, payload, cancellationToken);
    }

    public Task SendServerPushAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default)
    {
        var header = new NetworkPacketHeader(NetworkPacketFlags.ServerPush, opCode, 0, (uint)payload.Length);
        return WriteFrameWithTimeoutAsync(header, payload, cancellationToken);
    }

    private async Task WriteFrameWithTimeoutAsync(
        NetworkPacketHeader header,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        if (!_stream.CanWrite) return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_writeTimeout);
        await _writeLock.WaitAsync(timeout.Token);
        try
        {
            await WriteFrameAsync(header, payload, timeout.Token);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task WriteFrameAsync(NetworkPacketHeader header, byte[] payload, CancellationToken cancellationToken)
    {
        var frameSize = NetworkFrameCodec.GetFrameSize(payload.Length);
        var frame = new byte[frameSize];
        NetworkFrameCodec.WriteFrame(frame, header, payload);
        await _stream.WriteAsync(frame, cancellationToken);
    }

    internal void Close() => _stream.Close();
}

/// <summary>
/// TCP 传输层服务器
/// </summary>
public sealed class TcpTransportServer : IGatewayTransportServer
{
    public string Name => "TCP";
    public bool IsEnabled => _options.Enabled;

    private readonly TcpTransportOptions _options;
    private readonly IGatewayTransportEvents _events;
    private readonly ILogger<TcpTransportServer> _logger;
    private readonly object _lifecycleGate = new();
    private readonly ConcurrentDictionary<long, System.Net.Sockets.TcpClient> _clients = new();
    private System.Net.Sockets.TcpListener? _listener;

    public TcpTransportServer(
        IOptions<TcpTransportOptions> options,
        IGatewayTransportEvents events,
        ILogger<TcpTransportServer> logger)
    {
        _options = options.Value;
        _events = events;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("TcpTransport is disabled.");
            return;
        }

        var ip = System.Net.IPAddress.TryParse(_options.Host, out var parsed) ? parsed : System.Net.IPAddress.Any;
        var listener = new System.Net.Sockets.TcpListener(ip, _options.Port);
        lock (_lifecycleGate)
        {
            if (_listener is not null)
            {
                throw new InvalidOperationException("TcpTransport is already running.");
            }

            listener.Start();
            _listener = listener;
        }

        _logger.LogInformation("TcpTransport listening on {Host}:{Port}", _options.Host, _options.Port);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                client.NoDelay = true;
                var connectionId = GenerateConnectionId();
                if (!_clients.TryAdd(connectionId, client))
                {
                    client.Dispose();
                    throw new InvalidOperationException($"Duplicate TCP connection id: {connectionId}.");
                }

                TrackClientTask(Task.Run(
                    () => HandleClientAsync(connectionId, client, cancellationToken),
                    CancellationToken.None));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (System.Net.Sockets.SocketException) when (!ReferenceEquals(GetListener(), listener))
        {
        }
        finally
        {
            lock (_lifecycleGate)
            {
                if (ReferenceEquals(_listener, listener))
                {
                    _listener = null;
                }
            }

            listener.Stop();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        System.Net.Sockets.TcpListener? listener;
        lock (_lifecycleGate)
        {
            listener = _listener;
            _listener = null;
        }

        listener?.Stop();
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }

        return Task.CompletedTask;
    }

    private System.Net.Sockets.TcpListener? GetListener()
    {
        lock (_lifecycleGate)
        {
            return _listener;
        }
    }

    private async Task HandleClientAsync(
        long connectionId,
        System.Net.Sockets.TcpClient client,
        CancellationToken cancellationToken)
    {
        var session = new TcpTransportSession(
            connectionId,
            client.GetStream(),
            TimeSpan.FromMilliseconds(_options.RequestTimeoutMs));
        _events.OnConnected(session);

        _logger.LogInformation("TCP client connected: ConnectionId={ConnectionId}", connectionId);

        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(64 * 1024);
        var buffered = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var n = await client.GetStream().ReadAsync(buffer.AsMemory(buffered, buffer.Length - buffered), cancellationToken);
                if (n <= 0) break;

                buffered += n;

                var offset = 0;
                while (true)
                {
                    if (!NetworkFrameCodec.TryParseFrame(
                        new ReadOnlySpan<byte>(buffer, offset, buffered - offset),
                        out var header,
                        out var payloadSpan))
                    {
                        break;
                    }

                    var totalSize = NetworkFrameCodec.GetFrameSize((int)header.PayloadLength);
                    if (totalSize > _options.MaxFrameLength)
                    {
                        _logger.LogError("Frame too large: {Size}", totalSize);
                        break;
                    }

                    _events.OnRequest(connectionId, header.OpCode, header.Seq, payloadSpan.ToArray());

                    offset += totalSize;
                    if (offset >= buffered) break;
                }

                if (offset > 0)
                {
                    Buffer.BlockCopy(buffer, offset, buffer, 0, buffered - offset);
                    buffered -= offset;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCP client error: ConnectionId={ConnectionId}", connectionId);
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            _events.OnClosed(connectionId);
            _clients.TryRemove(connectionId, out _);
            client.Close();
            _logger.LogInformation("TCP client disconnected: ConnectionId={ConnectionId}", connectionId);
        }
    }

    private void TrackClientTask(Task task)
    {
        _ = task.ContinueWith(
            completed => _logger.LogError(completed.Exception, "TCP client task failed."),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static long _nextConnectionId;
    private static long GenerateConnectionId() => Interlocked.Increment(ref _nextConnectionId);
}

