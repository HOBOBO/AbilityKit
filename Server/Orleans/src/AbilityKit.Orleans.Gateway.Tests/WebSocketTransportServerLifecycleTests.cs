using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using AbilityKit.Network.Protocol;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Networking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class WebSocketTransportServerLifecycleTests
{
    [Fact]
    public async Task AcceptsConfiguredPathAndUsesNetworkFrameCodec()
    {
        var port = ReserveTcpPort();
        var events = new RecordingWebSocketTransportEvents();
        var server = CreateServer(port, events);

        using var runCancellation = new CancellationTokenSource();
        var run = server.StartAsync(runCancellation.Token);
        try
        {
            using var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/gateway"), CancellationToken.None);

            var session = await events.WaitForConnectedAsync(TimeSpan.FromSeconds(5));
            await client.SendAsync(
                CreateFrame(NetworkPacketFlags.Request, opCode: 42, seq: 7, payload: [1, 2, 3]),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                CancellationToken.None);

            var request = await events.WaitForRequestAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(session.ConnectionId, request.ConnectionId);
            Assert.Equal(42u, request.OpCode);
            Assert.Equal(7u, request.Seq);
            Assert.Equal(new byte[] { 1, 2, 3 }, request.Payload);

            await session.SendResponseAsync(opCode: 42, seq: 7, payload: [9, 8]);

            var responseFrame = await ReceiveBinaryMessageAsync(client, TimeSpan.FromSeconds(5));
            Assert.True(NetworkFrameCodec.TryParseFrame(responseFrame, out var responseHeader, out var responsePayload));
            Assert.Equal(NetworkPacketFlags.Response, responseHeader.Flags);
            Assert.Equal(42u, responseHeader.OpCode);
            Assert.Equal(7u, responseHeader.Seq);
            Assert.Equal(new byte[] { 9, 8 }, responsePayload.ToArray());
        }
        finally
        {
            runCancellation.Cancel();
            await server.StopAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task RejectsWebSocketRequestsOutsideConfiguredPath()
    {
        var port = ReserveTcpPort();
        var events = new RecordingWebSocketTransportEvents();
        var server = CreateServer(port, events);

        using var runCancellation = new CancellationTokenSource();
        var run = server.StartAsync(runCancellation.Token);
        try
        {
            using var client = new ClientWebSocket();

            await Assert.ThrowsAsync<WebSocketException>(() =>
                client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/wrong"), CancellationToken.None));

            Assert.False(events.HasConnected);
        }
        finally
        {
            runCancellation.Cancel();
            await server.StopAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task StopDisconnectsClientsAndSameInstanceCanRestart()
    {
        var port = ReserveTcpPort();
        var events = new RecordingWebSocketTransportEvents();
        var server = CreateServer(port, events);

        using var firstRunCancellation = new CancellationTokenSource();
        var firstRun = server.StartAsync(firstRunCancellation.Token);
        try
        {
            using var firstClient = new ClientWebSocket();
            await firstClient.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/gateway"), CancellationToken.None);
            var firstSession = await events.WaitForConnectedAsync(TimeSpan.FromSeconds(5));

            await server.StopAsync();
            await firstRun.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(firstSession.ConnectionId, await events.WaitForClosedAsync(TimeSpan.FromSeconds(5)));

            using var secondRunCancellation = new CancellationTokenSource();
            var secondRun = server.StartAsync(secondRunCancellation.Token);
            try
            {
                using var secondClient = new ClientWebSocket();
                await secondClient.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/gateway"), CancellationToken.None);
                var secondSession = await events.WaitForConnectedAsync(TimeSpan.FromSeconds(5));

                Assert.NotEqual(firstSession.ConnectionId, secondSession.ConnectionId);
            }
            finally
            {
                secondRunCancellation.Cancel();
                await server.StopAsync();
                await secondRun.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            firstRunCancellation.Cancel();
            await server.StopAsync();
        }
    }

    private static WebSocketTransportServer CreateServer(
        int port,
        RecordingWebSocketTransportEvents events)
    {
        return new WebSocketTransportServer(
            Options.Create(new WebSocketTransportOptions
            {
                Enabled = true,
                Host = IPAddress.Loopback.ToString(),
                Port = port,
                Path = "/gateway",
            }),
            events,
            NullLogger<WebSocketTransportServer>.Instance);
    }

    private static ArraySegment<byte> CreateFrame(
        NetworkPacketFlags flags,
        uint opCode,
        uint seq,
        byte[] payload)
    {
        var frame = new byte[NetworkFrameCodec.GetFrameSize(payload.Length)];
        NetworkFrameCodec.WriteFrame(
            frame,
            new NetworkPacketHeader(flags, opCode, seq, (uint)payload.Length),
            payload);
        return frame;
    }

    private static async Task<byte[]> ReceiveBinaryMessageAsync(
        ClientWebSocket client,
        TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var buffer = new byte[4096];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await client.ReceiveAsync(buffer, cancellation.Token);
            Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
            message.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return message.ToArray();
    }

    private static int ReserveTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class RecordingWebSocketTransportEvents : IGatewayTransportEvents
    {
        private TaskCompletionSource<IGatewayTransportSession> _connected = NewCompletion<IGatewayTransportSession>();
        private TaskCompletionSource<RequestRecord> _request = NewCompletion<RequestRecord>();
        private TaskCompletionSource<long> _closed = NewCompletion<long>();

        public bool HasConnected => _connected.Task.IsCompletedSuccessfully;

        public void OnConnected(IGatewayTransportSession session)
        {
            _connected.TrySetResult(session);
        }

        public void OnRequest(long connectionId, uint opCode, uint seq, byte[] payload)
        {
            _request.TrySetResult(new RequestRecord(connectionId, opCode, seq, payload));
        }

        public void OnClosed(long connectionId)
        {
            _closed.TrySetResult(connectionId);
        }

        public async Task<IGatewayTransportSession> WaitForConnectedAsync(TimeSpan timeout)
        {
            var completion = _connected;
            var result = await completion.Task.WaitAsync(timeout);
            Interlocked.CompareExchange(ref _connected, NewCompletion<IGatewayTransportSession>(), completion);
            return result;
        }

        public async Task<RequestRecord> WaitForRequestAsync(TimeSpan timeout)
        {
            var completion = _request;
            var result = await completion.Task.WaitAsync(timeout);
            Interlocked.CompareExchange(ref _request, NewCompletion<RequestRecord>(), completion);
            return result;
        }

        public async Task<long> WaitForClosedAsync(TimeSpan timeout)
        {
            var completion = _closed;
            var result = await completion.Task.WaitAsync(timeout);
            Interlocked.CompareExchange(ref _closed, NewCompletion<long>(), completion);
            return result;
        }

        private static TaskCompletionSource<T> NewCompletion<T>() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record RequestRecord(long ConnectionId, uint OpCode, uint Seq, byte[] Payload);
}
