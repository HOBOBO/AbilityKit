using System;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class NetworkSessionDiagnosticsTests
{
    [Fact]
    public void CountersTrackFramedBytesAndCompletePacketsAcrossPartialReceives()
    {
        var transport = new TestTransport();
        using var session = new NetworkSession(transport);
        var received = 0;
        session.PacketReceived += (_, _, _) => received++;
        session.Start();

        session.Send(42, new ArraySegment<byte>(new byte[] { 1, 2, 3 }));
        var outbound = transport.LastSent!.Value;
        Assert.Equal(outbound.Count, session.BytesSentCount);
        Assert.Equal(1, session.PacketsSentCount);

        var split = outbound.Count / 2;
        transport.Receive(new ArraySegment<byte>(outbound.Array!, outbound.Offset, split));
        Assert.Equal(0, received);
        Assert.Equal(split, session.BytesReceivedCount);
        Assert.Equal(0, session.PacketsReceivedCount);

        transport.Receive(new ArraySegment<byte>(outbound.Array!, outbound.Offset + split, outbound.Count - split));
        Assert.Equal(1, received);
        Assert.Equal(outbound.Count, session.BytesReceivedCount);
        Assert.Equal(1, session.PacketsReceivedCount);
    }

    [Fact]
    public void StopAndStart_DropsOldIoBeforeDecodingAndKeepsNewFrame()
    {
        var transport = new TestTransport();
        var io = new ManualSessionDispatcher();
        using var session = new NetworkSession(transport, InlineDispatcher.Instance, io);
        var packets = new List<uint>();
        session.PacketReceived += (opcode, _, _) => packets.Add(opcode);

        session.Start();
        transport.Receive(Frame(NetworkPacketFlags.Response, 10));
        session.Stop();
        session.Start();
        var current = Frame(NetworkPacketFlags.Response, 11);
        transport.Receive(current);
        io.Drain();

        Assert.Equal(new uint[] { 11 }, packets);
        Assert.Equal(current.Count, session.BytesReceivedCount);
        Assert.Equal(1, session.PacketsReceivedCount);
    }

    [Fact]
    public void StopAndStart_DropsOldNotificationsButDeliversCurrentCallbacks()
    {
        var transport = new TestTransport();
        var callbacks = new ManualSessionDispatcher();
        using var session = new NetworkSession(transport, callbacks, InlineDispatcher.Instance);
        var delivered = new List<string>();
        session.Connected += () => delivered.Add("connected");
        session.Disconnected += () => delivered.Add("disconnected");
        session.Error += error => delivered.Add(error.Message);
        session.PacketReceived += (opcode, _, _) => delivered.Add($"packet:{opcode}");
        session.ServerPushReceived += (opcode, _) => delivered.Add($"push:{opcode}");

        session.Start();
        transport.RaiseConnected();
        transport.Receive(Frame(NetworkPacketFlags.Response, 10));
        transport.Receive(Frame(NetworkPacketFlags.ServerPush, 11));
        transport.RaiseError("old error");
        transport.RaiseDisconnected();
        session.Stop();
        session.Start();
        transport.RaiseConnected();
        transport.Receive(Frame(NetworkPacketFlags.Response, 20));
        transport.Receive(Frame(NetworkPacketFlags.ServerPush, 21));
        transport.RaiseError("current error");
        transport.RaiseDisconnected();

        callbacks.Drain();
        Assert.Equal(new[] { "connected", "packet:20", "push:21", "current error", "disconnected" }, delivered);
    }

    [Fact]
    public void StopAndStart_DoesNotKeepPartialFrameFromPreviousDecoder()
    {
        var transport = new TestTransport();
        using var session = new NetworkSession(transport);
        var packets = new List<uint>();
        session.PacketReceived += (opcode, _, _) => packets.Add(opcode);
        var partial = Frame(NetworkPacketFlags.Response, 10);

        session.Start();
        transport.Receive(new ArraySegment<byte>(partial.Array!, partial.Offset, 3));
        session.Stop();
        session.Start();
        transport.Receive(Frame(NetworkPacketFlags.Response, 11));

        Assert.Equal(new uint[] { 11 }, packets);
    }

    [Fact]
    public void Dispose_InvalidatesPendingCallbacksAndDisposesTransportOnce()
    {
        var transport = new TestTransport();
        var callbacks = new ManualSessionDispatcher();
        var session = new NetworkSession(transport, callbacks, InlineDispatcher.Instance);
        var connected = 0;
        session.Connected += () => connected++;
        session.Start();
        transport.RaiseConnected();

        session.Dispose();
        session.Dispose();
        callbacks.Drain();

        Assert.Equal(0, connected);
        Assert.Equal(1, transport.DisposeCount);
        Assert.Throws<ObjectDisposedException>(session.Start);
    }

    [Fact]
    public void StopAndStart_DropsPreviousMiddlewareContinuationsAtSessionBoundary()
    {
        var transport = new TestTransport();
        using var session = new NetworkSession(transport);
        var delay = new DeferredMiddleware();
        session.Pipeline.Add(delay);
        var delivered = new List<uint>();
        session.PacketReceived += (opcode, _, _) => delivered.Add(opcode);

        session.Start();
        transport.Receive(Frame(NetworkPacketFlags.Response, 10));
        session.Send(20, default);
        session.Stop();
        session.Start();
        delay.Drain();

        Assert.Empty(delivered);
        Assert.Null(transport.LastSent);

        transport.Receive(Frame(NetworkPacketFlags.Response, 11));
        session.Send(21, default);
        delay.Drain();
        Assert.Equal(new uint[] { 11 }, delivered);
        Assert.NotNull(transport.LastSent);
    }

    private static ArraySegment<byte> Frame(NetworkPacketFlags flags, uint opcode) =>
        LengthPrefixedFrameCodec.Instance.Encode(new NetworkPacketHeader(flags, opcode, 0, 1), new ArraySegment<byte>(new byte[] { 7 }));

    private sealed class DeferredMiddleware : INetworkMiddleware
    {
        private readonly ManualSessionDispatcher _queue = new();

        public void OnInbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload,
            Action<NetworkPacketHeader, ArraySegment<byte>> next) => _queue.Post(() => next(header, payload));

        public void OnOutbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload,
            Action<NetworkPacketHeader, ArraySegment<byte>> next) => _queue.Post(() => next(header, payload));

        public void Drain() => _queue.Drain();
    }

    private sealed class TestTransport : ITransport
    {
        public int DisposeCount { get; private set; }
        public bool IsConnected { get; private set; }
        public ArraySegment<byte>? LastSent { get; private set; }
        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<ArraySegment<byte>>? BytesReceived;

        public void Connect(string host, int port) => IsConnected = true;
        public void Close() => IsConnected = false;
        public void Dispose()
        {
            DisposeCount++;
            Close();
        }

        public void RaiseConnected() => Connected?.Invoke();
        public void RaiseDisconnected() => Disconnected?.Invoke();
        public void RaiseError(string message) => Error?.Invoke(new InvalidOperationException(message));

        public void Send(ArraySegment<byte> bytes)
        {
            var copy = new byte[bytes.Count];
            Buffer.BlockCopy(bytes.Array!, bytes.Offset, copy, 0, bytes.Count);
            LastSent = new ArraySegment<byte>(copy);
        }

        public void Receive(ArraySegment<byte> bytes) => BytesReceived?.Invoke(bytes);
    }
}

internal sealed class ManualSessionDispatcher : IDispatcher
{
    private readonly Queue<Action> _pending = new();

    public void Post(Action action) => _pending.Enqueue(action);

    public void RunNext() => _pending.Dequeue().Invoke();

    public void Drain()
    {
        while (_pending.Count != 0) RunNext();
    }
}
