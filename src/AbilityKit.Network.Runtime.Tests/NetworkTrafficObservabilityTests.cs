using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Observability;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class NetworkTrafficObservabilityTests
{
    [Fact]
    public void Probe_ObservesBothDirectionsAndCopiesOnlyBoundedPreview()
    {
        var events = new List<NetworkTrafficEvent>();
        var timestamp = new DateTimeOffset(2026, 8, 21, 1, 2, 3, TimeSpan.Zero);
        var probe = new NetworkTrafficProbeMiddleware(
            new NetworkTrafficConnectionContext(
                "battle-1", 2, "battle", "moba.battle", "127.0.0.1:7100", "tcp"),
            new DelegateObserver(events.Add),
            maximumPayloadPreviewBytes: 2,
            utcNowProvider: () => timestamp);
        var header = new NetworkPacketHeader(
            NetworkPacketFlags.Request,
            opCode: 901,
            seq: 42,
            payloadLength: 4);
        var payload = new ArraySegment<byte>(new byte[] { 9, 1, 2, 3 });
        var forwarded = 0;

        probe.OnOutbound(null!, header, payload, (_, _) => forwarded++);
        probe.OnInbound(null!, header, payload, (_, _) => forwarded++);

        Assert.Equal(2, forwarded);
        Assert.Equal(
            new[] { NetworkTrafficDirection.Outbound, NetworkTrafficDirection.Inbound },
            events.Select(item => item.Direction));
        var traffic = events[0];
        Assert.Equal("battle-1", traffic.ConnectionId);
        Assert.Equal(2, traffic.Generation);
        Assert.Equal("battle", traffic.Role);
        Assert.Equal("moba.battle", traffic.CatalogId);
        Assert.Equal("127.0.0.1:7100", traffic.Endpoint);
        Assert.Equal("tcp", traffic.Transport);
        Assert.Equal(timestamp, traffic.TimestampUtc);
        Assert.Equal(901u, traffic.OpCode);
        Assert.Equal(42u, traffic.Sequence);
        Assert.Equal(NetworkPacketFlags.Request, traffic.Flags);
        Assert.Equal(4, traffic.PayloadLength);
        Assert.Equal(new byte[] { 9, 1 }, traffic.PayloadPreview.ToArray());
        Assert.True(traffic.IsPayloadPreviewTruncated);
    }

    [Fact]
    public void Probe_WhenObserverOrErrorHandlerThrows_StillForwardsPacket()
    {
        var probe = new NetworkTrafficProbeMiddleware(
            new NetworkTrafficConnectionContext("room-1", 1, "room", "room", "host:1", "test"),
            new DelegateObserver(_ => throw new InvalidOperationException("observer")),
            observerErrorHandler: _ => throw new InvalidOperationException("handler"));
        var forwarded = false;

        probe.OnInbound(
            null!,
            new NetworkPacketHeader(NetworkPacketFlags.None, 10, 0, 0),
            default,
            (_, _) => forwarded = true);

        Assert.True(forwarded);
    }

    [Fact]
    public void RingBuffer_WhenFull_EvictsOldestAndCountsDroppedEvents()
    {
        var buffer = new NetworkTrafficRingBuffer(capacity: 2);
        var probe = new NetworkTrafficProbeMiddleware(
            new NetworkTrafficConnectionContext("room-1", 1, "room", "room", "host:1", "test"),
            buffer);

        for (uint opCode = 1; opCode <= 3; opCode++)
        {
            probe.OnOutbound(
                null!,
                new NetworkPacketHeader(NetworkPacketFlags.None, opCode, 0, 0),
                default,
                static (_, _) => { });
        }

        Assert.Equal(2, buffer.Count);
        Assert.Equal(1, buffer.DroppedCount);
        Assert.Equal(new uint[] { 2, 3 }, buffer.Snapshot().Select(item => item.OpCode));
    }

    [Fact]
    public void Reconnect_ReinstallsProbeWithStableConnectionIdAndNewGeneration()
    {
        var transports = new List<ControllableTransport>();
        var contexts = new List<NetworkTrafficConnectionContext>();
        var filterContexts = new List<NetworkTrafficConnectionContext>();
        var options = new ConnectionOptions
        {
            ReconnectInitialDelay = TimeSpan.Zero,
            ReconnectMaxDelay = TimeSpan.Zero,
            TrafficCapture = new NetworkTrafficCaptureOptions
            {
                ConnectionId = "battle-primary",
                Role = "battle",
                CatalogId = "shooter.battle",
                ObserverFactory = context =>
                {
                    contexts.Add(context);
                    return new DelegateObserver(_ => { });
                },
                FilterFactory = context =>
                {
                    filterContexts.Add(context);
                    return static (_, _) => true;
                }
            }
        };
        using var connection = new ConnectionManager(
            () =>
            {
                var transport = new ControllableTransport();
                transports.Add(transport);
                return transport;
            },
            options);

        connection.Open("gateway.example", 7200);
        transports[0].RaiseDisconnected();
        connection.Tick(0f);

        Assert.Equal(2, contexts.Count);
        Assert.Equal(new[] { 1, 2 }, contexts.Select(item => item.Generation));
        Assert.Equal(new[] { 1, 2 }, filterContexts.Select(item => item.Generation));
        Assert.All(contexts, item => Assert.Equal("battle-primary", item.ConnectionId));
        Assert.All(contexts, item => Assert.Equal("gateway.example:7200", item.Endpoint));
    }

    private sealed class DelegateObserver : INetworkTrafficObserver
    {
        private readonly Action<NetworkTrafficEvent> _observe;

        public DelegateObserver(Action<NetworkTrafficEvent> observe) => _observe = observe;

        public void OnTraffic(NetworkTrafficEvent trafficEvent) => _observe(trafficEvent);
    }

    private sealed class ControllableTransport : ITransport
    {
        public bool IsConnected { get; private set; }

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<ArraySegment<byte>>? BytesReceived;

        public void Connect(string host, int port)
        {
            IsConnected = true;
            Connected?.Invoke();
        }

        public void RaiseDisconnected()
        {
            IsConnected = false;
            Disconnected?.Invoke();
        }

        public void Close() => IsConnected = false;
        public void Send(ArraySegment<byte> bytes) { }
        public void Dispose() => IsConnected = false;
    }
}
