using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Sdk.Observability;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkPacketInterceptionTests
{
    [Fact]
    public void HoldsEveryOutboundPacketUntilReleasedAndPreservesHeaderWhenEdited()
    {
        var transport = new RecordingTransport();
        using var client = new NetworkSdkBuilder().UseTransportFactory(() => transport).Build();
        var interception = new NetworkPacketInterception { Enabled = true };
        using var binding = interception.Attach(client, "abilitykit.room");
        client.Open("localhost", 7100);

        client.SendPacket(42, new ArraySegment<byte>(new byte[] { 1, 2 }),
            (ushort)NetworkPacketFlags.Request, 99);
        client.SendPacket(43, new ArraySegment<byte>(new byte[] { 3 }));

        Assert.Empty(transport.Sent);
        var pending = interception.Snapshot();
        Assert.Equal(2, pending.Count);
        Assert.Equal("abilitykit.room", pending[0].CatalogId);
        Assert.Equal(99u, pending[0].Header.Seq);
        Assert.True(interception.Release(pending[0].Id, new byte[] { 7, 8, 9 }));
        Assert.Single(transport.Sent);
        var frame = transport.Sent[0];
        var header = NetworkPacketHeader.Read(frame.AsSpan(4, NetworkPacketHeader.Size));
        Assert.Equal(42u, header.OpCode);
        Assert.Equal(99u, header.Seq);
        Assert.Equal(3u, header.PayloadLength);
        Assert.Equal(new byte[] { 7, 8, 9 }, frame.AsSpan(4 + NetworkPacketHeader.Size).ToArray());
        Assert.True(interception.Discard(pending[1].Id));
        Assert.Single(transport.Sent);
    }

    [Fact]
    public void ReconnectInvalidatesOldPendingPacketsAndReinstallsGate()
    {
        var transports = new List<RecordingTransport>();
        using var client = new NetworkSdkBuilder()
            .UseTransportFactory(() =>
            {
                var transport = new RecordingTransport();
                transports.Add(transport);
                return transport;
            }).Build();
        var interception = new NetworkPacketInterception { Enabled = true };
        using var binding = interception.Attach(client);
        client.Open("localhost", 7100);
        client.SendPacket(42, new ArraySegment<byte>(new byte[] { 1 }));
        var oldId = Assert.Single(interception.Snapshot()).Id;

        client.Close();
        client.Open("localhost", 7100);

        Assert.False(interception.Release(oldId));
        client.SendPacket(43, new ArraySegment<byte>(new byte[] { 2 }));
        Assert.Single(interception.Snapshot());
        Assert.Empty(transports[0].Sent);
        Assert.Empty(transports[1].Sent);
    }

    private sealed class RecordingTransport : ITransport
    {
        public bool IsConnected { get; private set; }
        public List<byte[]> Sent { get; } = new();
        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<ArraySegment<byte>>? BytesReceived;

        public void Connect(string host, int port)
        {
            IsConnected = true;
            Connected?.Invoke();
        }

        public void Close()
        {
            IsConnected = false;
            Disconnected?.Invoke();
        }

        public void Send(ArraySegment<byte> bytes) => Sent.Add(bytes.ToArray());
        public void Dispose() => IsConnected = false;
    }
}
