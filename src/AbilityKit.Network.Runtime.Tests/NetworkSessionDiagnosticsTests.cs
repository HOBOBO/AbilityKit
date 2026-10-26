using System;
using AbilityKit.Network.Abstractions;
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

    private sealed class TestTransport : ITransport
    {
        public bool IsConnected { get; private set; }
        public ArraySegment<byte>? LastSent { get; private set; }
        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<ArraySegment<byte>>? BytesReceived;

        public void Connect(string host, int port) => IsConnected = true;
        public void Close() => IsConnected = false;
        public void Dispose() => Close();

        public void Send(ArraySegment<byte> bytes)
        {
            var copy = new byte[bytes.Count];
            Buffer.BlockCopy(bytes.Array!, bytes.Offset, copy, 0, bytes.Count);
            LastSent = new ArraySegment<byte>(copy);
        }

        public void Receive(ArraySegment<byte> bytes) => BytesReceived?.Invoke(bytes);
    }
}
