using System;
using System.Collections.Generic;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Conditioning;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class VirtualNetworkConditionLinkTests
{
    private static readonly byte[] Payload = { 1 };

    [Fact]
    public void DisconnectCancelsPendingAndRecoveryAcceptsCatchUpFrames()
    {
        var profile = new NetworkConditionProfile(100, 0, 0, 0, 0);
        using var link = new VirtualNetworkConditionLink(new NetworkConditionScenario(profile), seed: 12);
        var delivered = new List<uint>();

        Send(link, 1, delivered);
        link.AdvanceTo(20);
        link.Disconnect();
        Send(link, 2, delivered);
        link.AdvanceTo(200);
        Assert.Empty(delivered);
        Assert.Equal(0, link.Middleware.GetStats().PendingCount);
        Assert.Equal(VirtualNetworkLinkEventKind.BlockedInbound, link.Events[1].Kind);

        link.Reconnect();
        Send(link, 3, delivered);
        link.AdvanceTo(300);
        Assert.Equal(new uint[] { 3 }, delivered);
        Assert.Equal(200, link.Events[2].AtMs);
        Assert.Throws<ArgumentOutOfRangeException>(() => link.AdvanceTo(299));
    }

    [Fact]
    public void ScenarioOriginIsScriptZeroEvenWhenFirstPacketArrivesLater()
    {
        var loss = new NetworkConditionProfile(0, 0, 1, 0, 0);
        var scenario = new NetworkConditionScenario(NetworkConditionProfile.Ideal,
            new NetworkConditionScenario.Phase(100, 200, loss, NetworkConditionDirection.Inbound, 7));
        using var link = new VirtualNetworkConditionLink(scenario, seed: 1);
        var delivered = new List<uint>();

        link.AdvanceTo(150);
        Send(link, 1, delivered);
        link.AdvanceTo(150);
        Assert.Empty(delivered);
        Assert.Equal(NetworkConditionDropReason.RandomLoss, link.Middleware.SnapshotDecisions()[0].DropReason);

        link.AdvanceTo(200);
        Send(link, 2, delivered);
        link.AdvanceTo(200);
        Assert.Equal(new uint[] { 2 }, delivered);
    }

    [Fact]
    public void JitterPartialLossAndReorderHaveStableSeededTrace()
    {
        var first = RunRandomTrace(27);
        Assert.Equal(first, RunRandomTrace(27));
        Assert.NotEqual(first, RunRandomTrace(28));
    }

    [Fact]
    public void ScenarioPlayerSortsStablyAndFlushesBeforeCommandsAtSameTime()
    {
        using var link = new VirtualNetworkConditionLink(
            new NetworkConditionScenario(new NetworkConditionProfile(10, 0, 0, 0, 0)), seed: 5);
        var trace = new List<string>();
        var player = new VirtualNetworkScenarioPlayer(link, (virtualLink, command) =>
        {
            var sequence = uint.Parse(command.RequireParameter("seq"));
            var header = new NetworkPacketHeader(NetworkPacketFlags.None, 7, sequence, 1);
            virtualLink.InjectInbound(header, new ArraySegment<byte>(Payload),
                (delivered, _) => trace.Add($"packet:{delivered.Seq}@{virtualLink.NowMs}"));
            trace.Add($"inject:{sequence}@{virtualLink.NowMs}");
        });

        player.Play(new[]
        {
            Command(20, VirtualNetworkScenarioPlayer.ReconnectCommand),
            Command(0, VirtualNetworkScenarioPlayer.PacketCommand, "1"),
            Command(10, VirtualNetworkScenarioPlayer.DisconnectCommand),
            Command(20, VirtualNetworkScenarioPlayer.PacketCommand, "2"),
        }, finishAtMs: 30);

        Assert.Equal(new[]
        {
            "inject:1@0",
            "packet:1@10",
            "inject:2@20",
            "packet:2@30",
        }, trace);
        Assert.Equal(30, link.NowMs);
    }

    [Fact]
    public void ScenarioPlayerRejectsUnknownCommands()
    {
        using var link = new VirtualNetworkConditionLink(new NetworkConditionScenario(NetworkConditionProfile.Ideal));
        var player = new VirtualNetworkScenarioPlayer(link, (_, _) => { });

        var error = Assert.Throws<InvalidOperationException>(() =>
            player.Play(new[] { Command(0, "network.typo") }));

        Assert.Contains("network.typo", error.Message);
    }

    private static string[] RunRandomTrace(int seed)
    {
        var profile = new NetworkConditionProfile(20, 10, 0.25, 0.1, 0);
        using var link = new VirtualNetworkConditionLink(new NetworkConditionScenario(profile), seed: seed);
        var delivered = new List<uint>();
        for (uint i = 0; i < 100; i++) Send(link, i, delivered);
        link.AdvanceTo(100);

        var decisions = link.Middleware.SnapshotDecisions();
        var trace = new string[decisions.Length];
        for (int i = 0; i < decisions.Length; i++)
            trace[i] = $"{decisions[i].Sequence}:{decisions[i].DropReason}:{decisions[i].DeliverAtMs}:{decisions[i].Reordered}";
        return trace;
    }

    private static void Send(VirtualNetworkConditionLink link, uint sequence, List<uint> delivered)
    {
        var header = new NetworkPacketHeader(NetworkPacketFlags.None, 7, sequence, 1);
        link.InjectInbound(header, new ArraySegment<byte>(Payload), (h, _) => delivered.Add(h.Seq));
    }

    private static VirtualNetworkCommand Command(long atMs, string name, string? sequence = null)
    {
        IReadOnlyDictionary<string, string>? parameters = sequence == null
            ? null
            : new Dictionary<string, string> { ["seq"] = sequence };
        return new VirtualNetworkCommand(atMs, name, parameters);
    }
}
