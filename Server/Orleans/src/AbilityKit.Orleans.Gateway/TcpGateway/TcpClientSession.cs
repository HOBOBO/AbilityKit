using System.Buffers;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class TcpClientSession
{
    private readonly long _connectionId;
    private readonly TcpClient _client;
    private readonly TcpGatewayOptions _options;
    private readonly TcpGatewayRequestRouter _router;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private NetworkStream? _stream;
    private readonly TcpClientSessionContext _context;

    public TcpClientSession(long connectionId, TcpClient client, TcpGatewayOptions options, TcpGatewayRequestRouter router, ILogger logger)
    {
        _connectionId = connectionId;
        _client = client;
        _options = options;
        _router = router;
        _logger = logger;
        _context = new TcpClientSessionContext(connectionId);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var _ = _client;

        _stream = _client.GetStream();
        var stream = _stream;
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        var buffered = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                EnsureCapacity(ref buffer, buffered, _options.MaxFrameLength);

                var n = await stream.ReadAsync(buffer.AsMemory(buffered, buffer.Length - buffered), cancellationToken);
                if (n <= 0) break;

                buffered += n;

                var offset = 0;
                while (true)
                {
                    var span = new ReadOnlySpan<byte>(buffer, offset, buffered - offset);
                    if (!NetworkFrameCodec.TryParseFrame(span, out var totalSize, out var header, out var payload)) break;

                    if (totalSize > _options.MaxFrameLength)
                    {
                        throw new InvalidOperationException($"Frame too large: {totalSize}");
                    }

                    var payloadOffset = offset + 4 + NetworkPacketHeader.Size;
                    var payloadMemory = new ReadOnlyMemory<byte>(buffer, payloadOffset, (int)header.PayloadLength);
                    await HandlePacketAsync(stream, header, payloadMemory, cancellationToken);

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
            _logger.LogError(ex, "Tcp client session error");
        }
        finally
        {
            _stream = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task SendServerPushAsync(uint opCode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var stream = _stream;
        if (stream is null)
        {
            return;
        }

        var header = new NetworkPacketHeader(NetworkPacketFlags.ServerPush, opCode, 0, (uint)payload.Length);
        await SendFrameAsync(stream, header, payload, cancellationToken);
    }

    private static void EnsureCapacity(ref byte[] buffer, int buffered, int maxFrameLength)
    {
        var maxAllowed = Math.Max(4 + NetworkPacketHeader.Size, maxFrameLength);

        if (buffered < buffer.Length) return;
        if (buffer.Length >= maxAllowed) throw new InvalidOperationException($"Receive buffer overflow. Max={maxAllowed}");

        var newSize = Math.Min(maxAllowed, buffer.Length * 2);
        var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        Buffer.BlockCopy(buffer, 0, newBuffer, 0, buffered);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = newBuffer;
    }

    private async Task HandlePacketAsync(NetworkStream stream, NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if ((header.Flags & NetworkPacketFlags.Heartbeat) != 0)
        {
            await SendFrameAsync(stream, new NetworkPacketHeader(NetworkPacketFlags.Heartbeat | NetworkPacketFlags.Response, header.OpCode, header.Seq, 0), ReadOnlyMemory<byte>.Empty, cancellationToken);

            return;
        }

        if ((header.Flags & NetworkPacketFlags.Request) != 0)
        {
            TcpGatewayResponseEnvelope envelope;
            try
            {
                envelope = await _router.RouteAsync(_context, header, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RouteAsync failed. OpCode={OpCode} Seq={Seq}", header.OpCode, header.Seq);
                envelope = new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.Exception, ReadOnlyMemory<byte>.Empty);
            }

            var responsePayload = TcpGatewayResponseCodec.Serialize(envelope);

            var respHeader = new NetworkPacketHeader(NetworkPacketFlags.Response, header.OpCode, header.Seq, (uint)responsePayload.Length);
            await SendFrameAsync(stream, respHeader, responsePayload, cancellationToken);

            return;
        }

        _logger.LogInformation("Received packet: OpCode={OpCode} Seq={Seq} Flags={Flags} PayloadLength={PayloadLength}", header.OpCode, header.Seq, (ushort)header.Flags, header.PayloadLength);
    }

    private async Task SendFrameAsync(NetworkStream stream, NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var frameSize = NetworkFrameCodec.GetFrameSize((int)header.PayloadLength);
            var outBytes = ArrayPool<byte>.Shared.Rent(frameSize);
            try
            {
                NetworkFrameCodec.WriteFrame(outBytes.AsSpan(0, frameSize), header, payload.Span);
                await stream.WriteAsync(outBytes.AsMemory(0, frameSize), cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(outBytes);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
