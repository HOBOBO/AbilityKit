using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Scenario;
using Xunit;

namespace AbilityKit.Demo.Moba.Acceptance.Tests;

public sealed class VirtualNetworkScenarioDslTests
{
    private const string Script = """
        {
          "caseId": "battle-recovery-virtual",
          "seed": 47,
          "commands": [
            { "atMs": 0,   "name": "network.packet", "parameters": { "direction": "inbound", "opCode": "7", "seq": "1" } },
            { "atMs": 30,  "name": "network.packet", "parameters": { "direction": "inbound", "opCode": "7", "seq": "2" } },
            { "atMs": 40,  "name": "network.disconnect" },
            { "atMs": 50,  "name": "network.packet", "parameters": { "direction": "inbound", "opCode": "7", "seq": "3" } },
            { "atMs": 100, "name": "network.reconnect" },
            { "atMs": 110, "name": "network.packet", "parameters": { "direction": "inbound", "opCode": "7", "seq": "2" } },
            { "atMs": 130, "name": "network.packet", "parameters": { "direction": "inbound", "opCode": "7", "seq": "4" } },
            { "atMs": 170, "name": "network.packet", "parameters": { "direction": "inbound", "opCode": "7", "seq": "4" } },
            { "atMs": 120, "name": "network.phase", "parameters": { "until": "170", "direction": "inbound", "opCode": "7", "loss": "1" } }
          ]
        }
        """;

    [Fact]
    public void JsonDslDisconnectLossAndCatchUpReplayIdentically()
    {
        var scenario = TestScenarioCodec.Parse(Script);
        Assert.Equal(9, scenario.Commands.Count);

        var first = Run(scenario);
        var second = Run(TestScenarioCodec.Parse(TestScenarioCodec.Serialize(scenario)));

        Assert.Equal(first, second);
        Assert.Contains("delivered:1@30", first);
        Assert.Contains("delivered:2@140", first);
        Assert.Contains("delivered:4@200", first);
        Assert.DoesNotContain(first, line => line.StartsWith("delivered:3@", StringComparison.Ordinal));
        Assert.Contains("event:BlockedInbound:50:3", first);
        Assert.Contains("decision:4:RandomLoss:130", first);
    }

    [Fact]
    public void BattleFlowDslCompilesToTheSameVirtualNetworkTrace()
    {
        var jsonScenario = TestScenarioCodec.Parse(Script);
        var blocks = BattleFlowDslParser.Parse("""
            network packet inbound opcode=7 seq=1 at=0
            network packet inbound opcode=7 seq=2 at=30
            network disconnect at=40
            network packet inbound opcode=7 seq=3 at=50
            network reconnect at=100
            network packet inbound opcode=7 seq=2 at=110
            network packet inbound opcode=7 seq=4 at=130
            network packet inbound opcode=7 seq=4 at=170
            network phase at=120 until=170 direction=inbound opcode=7 loss=1
            """);
        var battleFlowScenario = BattleFlowCompiler.Compile("battle-recovery-virtual", blocks);

        Assert.Equal(Run(jsonScenario, seed: 47), Run(battleFlowScenario, seed: 47));
    }

    private static string[] Run(TestScenario script, int? seed = null)
    {
        var commands = script.Commands.Select(command =>
            new VirtualNetworkCommand(command.AtMs, command.Name, command.Parameters));
        var plan = VirtualNetworkScenarioPlan.Compile(
            commands,
            new NetworkConditionProfile(30, 0, 0, 0, 0));
        using var link = new VirtualNetworkConditionLink(plan.Scenario, seed: seed ?? script.Seed);
        var trace = new List<string>();
        var player = new VirtualNetworkScenarioPlayer(link, (virtualLink, command) =>
        {
            var opCode = Parse(command, "opCode");
            var seq = Parse(command, "seq");
            var payload = new ArraySegment<byte>(new byte[] { (byte)seq });
            var header = new NetworkPacketHeader(NetworkPacketFlags.None, opCode, seq, 1);
            Action<NetworkPacketHeader, ArraySegment<byte>> deliver = (h, _) =>
                trace.Add($"delivered:{h.Seq}@{virtualLink.NowMs}");
            if (TryGet(command.Parameters, "direction", out var direction) && direction == "outbound")
                virtualLink.InjectOutbound(header, payload, deliver);
            else virtualLink.InjectInbound(header, payload, deliver);
        });

        player.Play(plan.Commands, finishAtMs: 220);
        foreach (var entry in link.Events)
            trace.Add($"event:{entry.Kind}:{entry.AtMs}:{entry.Sequence}");
        foreach (var decision in link.Middleware.SnapshotDecisions())
            trace.Add($"decision:{decision.Sequence}:{decision.DropReason}:{decision.ObservedAtMs}");
        return trace.ToArray();
    }

    private static uint Parse(VirtualNetworkCommand command, string key)
    {
        var value = command.RequireParameter(key);
        if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"Invalid {key} in {command.Name} atMs={command.AtMs}");
        return parsed;
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> parameters, string key, out string value)
    {
        foreach (var pair in parameters)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
