using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.TcpGateway;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class RequestClientTests
{
    [Fact]
    public async Task ConcurrentRequests_CorrelateOutOfOrderResponsesAndDecodeEnvelope()
    {
        var connection = new TestConnection();
        using var client = new RequestClient(connection);

        var first = client.SendRequestAsync(101, Bytes(1));
        var second = client.SendRequestAsync(102, Bytes(2));
        var sends = connection.Sends.ToArray();

        Assert.Equal(2, sends.Length);
        Assert.All(sends, send => Assert.Equal((ushort)NetworkPacketFlags.Request, send.Flags));
        Assert.All(sends, send => Assert.NotEqual(0u, send.Seq));
        Assert.NotEqual(sends[0].Seq, sends[1].Seq);

        connection.RaisePacket(sends[1].OpCode, sends[1].Seq, Response(TcpGatewayStatusCode.Ok, 22));
        connection.RaisePacket(sends[0].OpCode, sends[0].Seq, Response(TcpGatewayStatusCode.Ok, 11));

        Assert.Equal(new byte[] { 11 }, (await first).ToArray());
        Assert.Equal(new byte[] { 22 }, (await second).ToArray());
    }

    [Fact]
    public async Task Timeout_RemovesPendingRequestAndIgnoresLateResponse()
    {
        var connection = new TestConnection();
        using var client = new RequestClient(connection);

        var request = client.SendRequestAsync(201, default, TimeSpan.FromMilliseconds(50));
        var send = Assert.Single(connection.Sends);

        var exception = await Assert.ThrowsAsync<TimeoutException>(() => request);
        Assert.Contains("opCode=201", exception.Message);
        Assert.Contains($"seq={send.Seq}", exception.Message);

        connection.RaisePacket(send.OpCode, send.Seq, Response(TcpGatewayStatusCode.Ok, 1));

        var next = client.SendRequestAsync(202, default);
        var nextSend = connection.Sends.Last();
        connection.RaisePacket(nextSend.OpCode, nextSend.Seq, Response(TcpGatewayStatusCode.Ok, 2));
        Assert.Equal(new byte[] { 2 }, (await next).ToArray());
    }

    [Fact]
    public async Task CallerCancellation_PreservesCancellationToken()
    {
        var connection = new TestConnection();
        using var client = new RequestClient(connection);
        using var cancellation = new CancellationTokenSource();

        var request = client.SendRequestAsync(301, default, cancellationToken: cancellation.Token);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledRequest_DoesNotSend()
    {
        var connection = new TestConnection();
        using var client = new RequestClient(connection);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
        {
            _ = client.SendRequestAsync(302, default, cancellationToken: cancellation.Token);
        });
        Assert.Empty(connection.Sends);
    }

    [Fact]
    public async Task Disconnect_FailsAllPendingRequests()
    {
        var connection = new TestConnection();
        using var client = new RequestClient(connection);
        var first = client.SendRequestAsync(401, default);
        var second = client.SendRequestAsync(402, default);

        connection.RaiseDisconnected();

        var firstError = await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        var secondError = await Assert.ThrowsAsync<InvalidOperationException>(() => second);
        Assert.Equal("Connection disconnected.", firstError.Message);
        Assert.Equal("Connection disconnected.", secondError.Message);
    }

    [Fact]
    public async Task ConnectionError_FailsAllPendingRequestsWithOriginalException()
    {
        var connection = new TestConnection();
        using var client = new RequestClient(connection);
        var request = client.SendRequestAsync(501, default);
        var expected = new IOException("transport failed");

        connection.RaiseError(expected);

        var actual = await Assert.ThrowsAsync<IOException>(() => request);
        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task ErrorEnvelope_ThrowsWithStatusAndSanitizedMessage()
    {
        var connection = new TestConnection();
        using var client = new RequestClient(connection);
        var request = client.SendRequestAsync(601, default);
        var send = Assert.Single(connection.Sends);

        connection.RaisePacket(
            send.OpCode,
            send.Seq,
            Response(TcpGatewayStatusCode.BadRequest, Encoding.UTF8.GetBytes("invalid\r\nrequest")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => request);
        Assert.Contains("statusCode=BadRequest", exception.Message);
        Assert.Contains("message=invalid  request", exception.Message);
    }

    [Fact]
    public void SendFailure_IsPropagatedSynchronously()
    {
        var connection = new TestConnection
        {
            SendException = new IOException("send failed")
        };
        using var client = new RequestClient(connection);

        var exception = Assert.Throws<IOException>(() =>
        {
            _ = client.SendRequestAsync(701, default);
        });
        Assert.Equal("send failed", exception.Message);
    }

    [Fact]
    public async Task Dispose_FailsPendingRequestAndRejectsNewRequests()
    {
        var connection = new TestConnection();
        var client = new RequestClient(connection);
        var pending = client.SendRequestAsync(801, default);

        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = client.SendRequestAsync(802, default);
        });
    }

    private static ArraySegment<byte> Bytes(params byte[] bytes) => new(bytes);

    private static ArraySegment<byte> Response(TcpGatewayStatusCode statusCode, params byte[] payload)
    {
        var bytes = new byte[sizeof(int) + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, (int)statusCode);
        payload.CopyTo(bytes, sizeof(int));
        return new ArraySegment<byte>(bytes);
    }

    private sealed class TestConnection : IConnection
    {
        public readonly ConcurrentQueue<SendRecord> Sends = new();

        public Exception? SendException { get; init; }
        public ConnectionState State { get; private set; } = ConnectionState.Connected;
        public bool IsConnected => State == ConnectionState.Connected;

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<uint, uint, ArraySegment<byte>>? PacketReceived;
        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;
        public event Action<string, string>? Kicked;

        public void Open(string host, int port)
        {
            State = ConnectionState.Connected;
            Connected?.Invoke();
        }

        public void Close()
        {
            State = ConnectionState.Disconnected;
            Disconnected?.Invoke();
        }

        public void Tick(float deltaTime)
        {
        }

        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
        {
            if (SendException != null)
            {
                throw SendException;
            }

            var bytes = payload.Array == null ? Array.Empty<byte>() : payload.ToArray();
            Sends.Enqueue(new SendRecord(opCode, seq, flags, bytes));
        }

        public void RaisePacket(uint opCode, uint seq, ArraySegment<byte> payload) =>
            PacketReceived?.Invoke(opCode, seq, payload);

        public void RaiseDisconnected()
        {
            State = ConnectionState.Disconnected;
            Disconnected?.Invoke();
        }

        public void RaiseError(Exception exception) => Error?.Invoke(exception);

        public void Dispose()
        {
            State = ConnectionState.Disconnected;
        }

        public readonly record struct SendRecord(uint OpCode, uint Seq, ushort Flags, byte[] Payload);
    }
}
