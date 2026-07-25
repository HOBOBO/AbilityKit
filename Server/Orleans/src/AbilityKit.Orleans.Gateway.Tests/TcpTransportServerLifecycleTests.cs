using System.Net;
using System.Net.Sockets;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Networking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class TcpTransportSessionTests
{
    [Fact]
    public async Task SendServerPushAsync_WhenWriteStalls_UsesConfiguredTimeout()
    {
        var session = new TcpTransportSession(
            connectionId: 7,
            new BlockingWriteStream(),
            writeTimeout: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            session.SendServerPushAsync(opCode: 100, payload: [1, 2, 3]));
    }

    private sealed class BlockingWriteStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        }
    }
}

public sealed class TcpTransportServerLifecycleTests
{
    [Fact]
    public async Task StopDisconnectsClientsAndSameInstanceCanRestart()
    {
        var port = ReserveTcpPort();
        var events = new RecordingTransportEvents();
        var server = new TcpTransportServer(
            Options.Create(new TcpTransportOptions
            {
                Enabled = true,
                Host = IPAddress.Loopback.ToString(),
                Port = port,
            }),
            events,
            NullLogger<TcpTransportServer>.Instance);

        using var firstRunCancellation = new CancellationTokenSource();
        var firstRun = server.StartAsync(firstRunCancellation.Token);
        try
        {
            using var firstClient = new TcpClient();
            await firstClient.ConnectAsync(IPAddress.Loopback, port);
            var firstConnectionId = await events.WaitForConnectedAsync(TimeSpan.FromSeconds(5));

            await server.StopAsync();
            await firstRun.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(firstConnectionId, await events.WaitForClosedAsync(TimeSpan.FromSeconds(5)));

            using var secondRunCancellation = new CancellationTokenSource();
            var secondRun = server.StartAsync(secondRunCancellation.Token);
            try
            {
                using var secondClient = new TcpClient();
                await secondClient.ConnectAsync(IPAddress.Loopback, port);
                var secondConnectionId = await events.WaitForConnectedAsync(TimeSpan.FromSeconds(5));

                Assert.NotEqual(firstConnectionId, secondConnectionId);
            }
            finally
            {
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

    private sealed class RecordingTransportEvents : IGatewayTransportEvents
    {
        private TaskCompletionSource<long> _connected = NewCompletion();
        private TaskCompletionSource<long> _closed = NewCompletion();

        public void OnConnected(IGatewayTransportSession session)
        {
            _connected.TrySetResult(session.ConnectionId);
        }

        public void OnRequest(long connectionId, uint opCode, uint seq, byte[] payload)
        {
        }

        public void OnClosed(long connectionId)
        {
            _closed.TrySetResult(connectionId);
        }

        public async Task<long> WaitForConnectedAsync(TimeSpan timeout)
        {
            var completion = _connected;
            var result = await completion.Task.WaitAsync(timeout);
            Interlocked.CompareExchange(ref _connected, NewCompletion(), completion);
            return result;
        }

        public async Task<long> WaitForClosedAsync(TimeSpan timeout)
        {
            var completion = _closed;
            var result = await completion.Task.WaitAsync(timeout);
            Interlocked.CompareExchange(ref _closed, NewCompletion(), completion);
            return result;
        }

        private static TaskCompletionSource<long> NewCompletion() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
