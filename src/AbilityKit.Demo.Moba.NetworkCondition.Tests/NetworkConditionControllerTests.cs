using System;
using System.Collections.Generic;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using Xunit;

namespace AbilityKit.Demo.Moba.NetworkCondition.Tests;

public sealed class NetworkConditionControllerTests
{
    [Fact]
    public void AttachIsPassiveUntilProfileIsAppliedAndDisableRestoresPipeline()
    {
        var transports = new List<MemoryTransport>();
        using var manager = CreateManager(transports);
        var controller = new NetworkConditionController();

        Assert.True(controller.Attach(manager));
        manager.Open("localhost", 1);

        Assert.False(controller.IsEnabled);
        Assert.Null(controller.Middleware);
        Assert.Equal(1, manager.Pipeline.Count);

        controller.ApplyPreset(NetworkConditionPreset.Mobile4G);
        var firstMiddleware = controller.Middleware;

        Assert.True(controller.IsEnabled);
        Assert.NotNull(firstMiddleware);
        Assert.Equal(2, manager.Pipeline.Count);
        Assert.Equal(NetworkConditionProfile.Mobile4G.BaseLatencyMs, controller.ActiveProfile.BaseLatencyMs);

        controller.ApplyProfile(NetworkConditionProfile.LimitedBandwidth);

        Assert.NotSame(firstMiddleware, controller.Middleware);
        Assert.Equal(2, manager.Pipeline.Count);
        Assert.Equal(128, controller.ActiveProfile.BandwidthKbps);

        controller.Disable();

        Assert.False(controller.IsEnabled);
        Assert.Null(controller.Middleware);
        Assert.Equal(1, manager.Pipeline.Count);
    }

    [Fact]
    public void ReconnectInstallsEnabledConditioningIntoReplacementPipeline()
    {
        var transports = new List<MemoryTransport>();
        using var manager = CreateManager(transports);
        var controller = new NetworkConditionController();
        controller.ApplyPreset(NetworkConditionPreset.PoorWifi);
        controller.Attach(manager);

        manager.Open("localhost", 1);
        var firstPipeline = manager.Pipeline;
        var firstMiddleware = controller.Middleware;

        transports[0].DisconnectFromRemote();
        manager.Tick(0f);

        Assert.Equal(2, transports.Count);
        Assert.NotSame(firstPipeline, manager.Pipeline);
        Assert.NotSame(firstMiddleware, controller.Middleware);
        Assert.Equal(2, manager.Pipeline.Count);
        Assert.True(controller.IsEnabled);
    }

    [Fact]
    public void DetachUnsubscribesAndRemovesInstalledMiddleware()
    {
        var transports = new List<MemoryTransport>();
        using var manager = CreateManager(transports);
        var controller = new NetworkConditionController();
        controller.ApplyPreset(NetworkConditionPreset.Lan);
        controller.Attach(manager);
        manager.Open("localhost", 1);

        controller.Detach();

        Assert.Null(controller.Middleware);
        Assert.Equal(1, manager.Pipeline.Count);

        transports[0].DisconnectFromRemote();
        manager.Tick(0f);

        Assert.Equal(1, manager.Pipeline.Count);
        Assert.Null(controller.Middleware);
    }

    [Fact]
    public void ProfileSwitchDiscardsOldPendingPacketsAndScenarioSurvivesReconnect()
    {
        var transports = new List<MemoryTransport>();
        using var manager = CreateManager(transports);
        var controller = new NetworkConditionController();
        controller.Attach(manager);
        manager.Open("localhost", 1);
        controller.ApplyProfile(new NetworkConditionProfile(1000, 0, 0, 0, 0));
        var previous = controller.Middleware;
        var delivered = 0;
        var header = new NetworkPacketHeader(NetworkPacketFlags.None, 1, 1, 1);
        previous.OnOutbound(null!, header, new ArraySegment<byte>(new byte[] { 1 }), (_, _) => delivered++);
        Assert.Equal(1, previous.GetStats().PendingCount);

        var scenario = new NetworkConditionScenario(NetworkConditionProfile.Ideal,
            new NetworkConditionScenario.Phase(0, 100, new NetworkConditionProfile(0, 0, 1, 0, 0)));
        controller.ApplyScenario(scenario);
        Assert.Equal(0, previous.GetStats().PendingCount);
        previous.Advance(long.MaxValue);
        Assert.Equal(0, delivered);

        transports[0].DisconnectFromRemote();
        manager.Tick(0f);
        Assert.NotSame(previous, controller.Middleware);
        Assert.True(controller.IsEnabled);
    }

    private static ConnectionManager CreateManager(ICollection<MemoryTransport> transports)
    {
        var options = new ConnectionOptions
        {
            FrameCodec = LengthPrefixedFrameCodec.Instance,
            HeartbeatInterval = TimeSpan.Zero,
            HeartbeatTimeout = TimeSpan.Zero,
            ReconnectInitialDelay = TimeSpan.Zero,
            ReconnectMaxDelay = TimeSpan.Zero,
        };

        return new ConnectionManager(
            () =>
            {
                var transport = new MemoryTransport();
                transports.Add(transport);
                return transport;
            },
            options);
    }

    private sealed class MemoryTransport : ITransport
    {
        public bool IsConnected { get; private set; }

        public event Action? Connected;
        public event Action? Disconnected;
#pragma warning disable CS0067
        public event Action<Exception>? Error;
        public event Action<ArraySegment<byte>>? BytesReceived;
#pragma warning restore CS0067

        public void Connect(string host, int port)
        {
            IsConnected = true;
            Connected?.Invoke();
        }

        public void Close()
        {
            IsConnected = false;
        }

        public void Send(ArraySegment<byte> bytes) { }

        public void DisconnectFromRemote()
        {
            IsConnected = false;
            Disconnected?.Invoke();
        }

        public void Dispose()
        {
            IsConnected = false;
        }
    }
}
