using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Conditioning;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class NetworkConditionScenarioTests
{
    private static readonly byte[] Payload = { 1 };

    [Fact]
    public void BurstLossCanTargetOnlyOutboundInputAndRecover()
    {
        long now = 1000;
        var loss = new NetworkConditionProfile(0, 0, 1, 0, 0);
        var scenario = new NetworkConditionScenario(NetworkConditionProfile.Ideal,
            new NetworkConditionScenario.Phase(100, 200, loss, NetworkConditionDirection.Outbound, opCode: 42));
        var middleware = new NetworkConditioningMiddleware(scenario, () => now, seed: 7);
        var delivered = new ConcurrentBag<uint>();
        Action<NetworkPacketHeader, ArraySegment<byte>> next = (header, _) => delivered.Add(header.Seq);

        Send(middleware, true, 42, 1, next);
        middleware.Advance(now);
        now = 1100;
        Send(middleware, false, 42, 2, next);
        Send(middleware, false, 43, 3, next);
        Send(middleware, true, 42, 4, next);
        middleware.Advance(now);
        now = 1200;
        Send(middleware, false, 42, 5, next);
        middleware.Advance(now);

        Assert.DoesNotContain(2u, delivered);
        Assert.Equal(4, delivered.Count);
        Assert.Equal(1, middleware.GetStats().OutboundDropped);
    }

    [Fact]
    public void ClearDiscardsAllQueuedPackets()
    {
        long now = 0;
        var middleware = new NetworkConditioningMiddleware(new NetworkConditionProfile(100, 0, 0, 0, 0), () => now);
        var delivered = new ConcurrentBag<uint>();
        Send(middleware, true, 1, 1, (header, _) => delivered.Add(header.Seq));

        middleware.ClearPending();
        now = 100;
        middleware.Advance(now);

        Assert.Empty(delivered);
        Assert.Equal(0, middleware.GetStats().PendingCount);
    }

    [Fact]
    public void DecisionLogIsBoundedAndRecordsDroppedPackets()
    {
        long now = 0;
        var middleware = new NetworkConditioningMiddleware(new NetworkConditionProfile(0, 0, 1, 0, 0),
            () => now, seed: 12, decisionCapacity: 2);
        for (uint i = 0; i < 3; i++) Send(middleware, true, 7, i, (_, _) => { });

        var decisions = middleware.SnapshotDecisions();
        Assert.Equal(2, decisions.Length);
        Assert.Equal(1u, decisions[0].Sequence);
        Assert.Equal(2u, decisions[1].Sequence);
        Assert.All(decisions, decision => Assert.True(decision.Dropped));
        Assert.Equal(12, middleware.Seed);
    }

    [Fact]
    public void PendingLimitRejectsOverflowWithoutDiscardingExistingPackets()
    {
        long now = 0;
        var delivered = new ConcurrentBag<uint>();
        var middleware = new NetworkConditioningMiddleware(new NetworkConditionProfile(100, 0, 0, 0, 0),
            () => now, decisionCapacity: 2, maxPendingPackets: 1);
        Send(middleware, true, 7, 1, (header, _) => delivered.Add(header.Seq));
        Send(middleware, true, 7, 2, (header, _) => delivered.Add(header.Seq));

        Assert.Equal(NetworkConditionDropReason.QueueOverflow, middleware.SnapshotDecisions()[1].DropReason);
        Assert.Equal(1, middleware.GetStats().InboundDropped);
        now = 100;
        middleware.Advance(now);
        Assert.Single(delivered);
        Assert.Contains(1u, delivered);
    }

    [Fact]
    public void ClearingInsideDeliveryCancelsRemainingDuePackets()
    {
        long now = 0;
        var middleware = new NetworkConditioningMiddleware(NetworkConditionProfile.Ideal, () => now);
        var delivered = new ConcurrentBag<uint>();
        Send(middleware, true, 1, 1, (header, _) =>
        {
            delivered.Add(header.Seq);
            middleware.ClearPending();
        });
        Send(middleware, true, 1, 2, (header, _) => delivered.Add(header.Seq));

        middleware.Advance(now);
        Assert.Single(delivered);
        Assert.Equal(1u, delivered.Single());
    }

    [Fact]
    public async Task ConcurrentProducersAndAdvanceDoNotLoseOrDuplicatePackets()
    {
        long now = 0;
        var middleware = new NetworkConditioningMiddleware(NetworkConditionProfile.Ideal, () => now);
        var delivered = new ConcurrentBag<uint>();
        Action<NetworkPacketHeader, ArraySegment<byte>> next = (header, _) => delivered.Add(header.Seq);

        var producers = new Task[4];
        for (int worker = 0; worker < producers.Length; worker++)
        {
            int index = worker;
            producers[worker] = Task.Run(() =>
            {
                for (uint i = 0; i < 100; i++)
                {
                    Send(middleware, index % 2 == 0, 1, (uint)(index * 100) + i, next);
                    middleware.Advance(now);
                }
            });
        }

        await Task.WhenAll(producers);
        middleware.Advance(now);
        Assert.Equal(400, delivered.Count);
        Assert.Equal(400, new System.Collections.Generic.HashSet<uint>(delivered).Count);
        Assert.Equal(0, middleware.GetStats().PendingCount);
    }

    [Fact]
    public void VirtualPlanCompilesPhaseCommandsWithoutLeavingThemInPlayback()
    {
        var commands = new[]
        {
            new VirtualNetworkCommand(100, VirtualNetworkScenarioPlayer.PhaseCommand,
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["until"] = "200",
                    ["direction"] = "inbound",
                    ["opCode"] = "7",
                    ["latency"] = "40",
                    ["jitter"] = "5",
                    ["loss"] = "0.25",
                    ["reorder"] = "0.1",
                    ["bandwidth"] = "128",
                }),
            new VirtualNetworkCommand(120, VirtualNetworkScenarioPlayer.PacketCommand),
        };

        var plan = VirtualNetworkScenarioPlan.Compile(commands, NetworkConditionProfile.Ideal);

        Assert.Single(plan.Commands);
        var phase = Assert.Single(plan.Scenario.Phases);
        Assert.Equal(100, phase.StartMs);
        Assert.Equal(200, phase.EndMs);
        Assert.Equal(NetworkConditionDirection.Inbound, phase.Direction);
        Assert.Equal(7u, phase.OpCode);
        Assert.Equal(40, phase.Profile.BaseLatencyMs);
        Assert.Equal(0.25, phase.Profile.PacketLossRate);
        Assert.Equal(128, phase.Profile.BandwidthKbps);
    }

    private static void Send(NetworkConditioningMiddleware middleware, bool inbound, uint opCode, uint seq,
        Action<NetworkPacketHeader, ArraySegment<byte>> next)
    {
        var header = new NetworkPacketHeader(NetworkPacketFlags.None, opCode, seq, 1);
        if (inbound) middleware.OnInbound(null!, header, new ArraySegment<byte>(Payload), next);
        else middleware.OnOutbound(null!, header, new ArraySegment<byte>(Payload), next);
    }
}
