using System.Net;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Host.Tcp;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.Network.Host.Tests;

public sealed class NetworkHostIntegrationTests
{
    [Fact]
    public async Task InProcessListener_UsesSharedFramingAndRequestResponse()
    {
        var listener = new InProcessChannelListener();
        var router = new ServerRequestRouter()
            .Register(42, (session, header, payload) =>
                session.SendResponse(header.OpCode, header.Seq, payload));
        using var host = new NetworkHost(listener, new NetworkHostOptions
        {
            RequestHandler = router
        });
        host.Start();

        var connection = new ConnectionManager(() => listener.CreateClientTransport());
        connection.Open("inprocess", 1);

        var payload = Bytes(1, 2, 3);
        var response = await SendRequestAsync(connection, 42, 11, payload);

        Assert.Equal(payload.ToArray(), response.ToArray());
        Assert.Equal(1, host.SessionCount);
        connection.Dispose();
    }

    [Fact]
    public async Task TcpListener_AcceptsAbilityKitTcpClientAndReplies()
    {
        var listener = new TcpChannelListener(new TcpChannelListenerOptions
        {
            Address = IPAddress.Loopback,
            Port = 0
        });
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var router = new ServerRequestRouter()
            .Register(7, (session, header, payload) =>
            {
                received.TrySetResult(payload.ToArray());
                session.SendResponse(header.OpCode, header.Seq, payload);
            });
        using var host = new NetworkHost(listener, new NetworkHostOptions { RequestHandler = router });
        host.Start();

        var port = int.Parse(host.Endpoint[(host.Endpoint.LastIndexOf(':') + 1)..]);
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new ConnectionManager(() => new TcpTransport());
        connection.Connected += () => connected.TrySetResult();
        connection.Open("127.0.0.1", port);
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var response = await SendRequestAsync(connection, 7, 12, Bytes(9, 8));

        Assert.Equal(new byte[] { 9, 8 }, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(new byte[] { 9, 8 }, response.ToArray());
        connection.Dispose();
    }

    [Fact]
    public async Task Pipeline_RunsOnInboundAndOutboundServerTraffic()
    {
        var listener = new InProcessChannelListener();
        var middleware = new RecordingMiddleware();
        var router = new ServerRequestRouter()
            .Register(3, (session, header, payload) =>
                session.SendResponse(header.OpCode, header.Seq, payload));
        using var host = new NetworkHost(listener, new NetworkHostOptions
        {
            RequestHandler = router,
            ConfigurePipeline = pipeline => pipeline.Use(middleware)
        });
        host.Start();

        var connection = new ConnectionManager(() => listener.CreateClientTransport());
        connection.Open("inprocess", 1);
        await SendRequestAsync(connection, 3, 13, Bytes(5));

        Assert.Equal(1, middleware.InboundCount);
        Assert.Equal(1, middleware.OutboundCount);
        connection.Dispose();
    }

    [Fact]
    public void MaxConnections_RejectsAdditionalChannelWithoutTrackingIt()
    {
        var listener = new InProcessChannelListener();
        using var host = new NetworkHost(listener, new NetworkHostOptions { MaxConnections = 1 });
        host.Start();

        var first = listener.CreateClientTransport();
        first.Connect("inprocess", 1);
        var second = listener.CreateClientTransport();

        Assert.Equal(1, host.SessionCount);
        Assert.Throws<InvalidOperationException>(() => second.Connect("inprocess", 1));
        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public void Lifecycle_RejectsDuplicateStartAndAllowsRestartAfterStop()
    {
        var listener = new InProcessChannelListener();
        using var host = new NetworkHost(listener);

        host.Start();
        Assert.Throws<InvalidOperationException>(() => host.Start());
        host.Stop();
        host.Start();

        Assert.True(host.IsListening);
    }

    private static ArraySegment<byte> Bytes(params byte[] bytes) => new(bytes);

    private static async Task<ArraySegment<byte>> SendRequestAsync(
        IConnection connection,
        uint opCode,
        uint sequence,
        ArraySegment<byte> payload)
    {
        var completion = new TaskCompletionSource<ArraySegment<byte>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnPacket(uint receivedOpCode, uint receivedSequence, ArraySegment<byte> receivedPayload)
        {
            if (receivedOpCode == opCode && receivedSequence == sequence)
            {
                completion.TrySetResult(receivedPayload);
            }
        }

        connection.PacketReceived += OnPacket;
        try
        {
            connection.Send(opCode, payload, (ushort)NetworkPacketFlags.Request, sequence);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            connection.PacketReceived -= OnPacket;
        }
    }

    private sealed class RecordingMiddleware : INetworkMiddleware
    {
        public int InboundCount { get; private set; }
        public int OutboundCount { get; private set; }

        public void OnInbound(
            ISessionContext context,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            Action<NetworkPacketHeader, ArraySegment<byte>> next)
        {
            InboundCount++;
            next(header, payload);
        }

        public void OnOutbound(
            ISessionContext context,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            Action<NetworkPacketHeader, ArraySegment<byte>> next)
        {
            OutboundCount++;
            next(header, payload);
        }
    }
}
