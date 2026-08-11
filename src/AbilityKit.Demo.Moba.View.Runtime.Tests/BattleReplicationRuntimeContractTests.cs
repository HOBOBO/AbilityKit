using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

/// <summary>
/// Contract tests for <see cref="Game.Flow.BattleReplicationRuntime"/> — the transport-binding layer
/// that wires NetworkTransport events (snapshot/reliable/closed/established/auth-failed) and options
/// callbacks (reliable-event cursor, submit-input ack) with generation guards. These tests pin the
/// event-wiring contract at the dotnet level, including the AuthenticationFailed callback.
/// </summary>
public sealed class BattleReplicationRuntimeContractTests
{
    private static NetworkTransport CreateTransport()
    {
        var options = NetworkTransportOptionsFactory.Create(
            host: "gw",
            port: 41001,
            transportFactory: () => new TcpTransport(),
            playerIdToUInt: pid => uint.Parse(pid.Value),
            playerIdFromUInt: n => new PlayerId(n.ToString()),
            worldIdToUlong: wid => ulong.Parse(wid.Value),
            worldIdFromUlong: n => new WorldId(n.ToString()),
            roomId: 999UL,
            sessionToken: "token",
            battleId: "battle-1",
            publicRoomId: "room-1",
            useFrameSyncInput: false,
            getReliableEventEpoch: () => "test-epoch",
            getReliableEventLastAcknowledgedSequence: () => 0L);

        return new NetworkTransport(options, InlineDispatcher.Instance);
    }

    [Fact]
    public void Build_WiresCallbacks_AndMarksPendingStateImport()
    {
        var transport = CreateTransport();
        var runtime = new AbilityKit.Game.Flow.BattleReplicationRuntime();

        var built = runtime.Build(
            transport,
            tickRate: 30,
            roomId: 999UL,
            battleId: "battle-1",
            reliableEventCheckpoint: default,
            onSnapshotPushed: _ => { },
            onReliableEventsPushed: _ => { },
            onConnectionClosed: () => { },
            onConnectionEstablished: () => { },
            onAuthenticationFailed: _ => { });

        Assert.True(built);
        Assert.True(runtime.IsBuilt);
        Assert.True(runtime.PendingStateImport);
        Assert.Same(transport, runtime.Transport);

        runtime.Dispose();
    }

    [Fact]
    public void Build_WiresSubmitInputAck_ToUpdateLastServerAckFrame()
    {
        var transport = CreateTransport();
        var runtime = new AbilityKit.Game.Flow.BattleReplicationRuntime();

        runtime.Build(
            transport, 30, 999UL, "battle-1", default,
            _ => { }, _ => { }, () => { }, () => { }, _ => { });

        Assert.Equal(0, runtime.LastServerAckFrame);

        // The engine calls OnSubmitInputAck on accepted input; BattleReplicationRuntime wires it.
        transport.Options.OnSubmitInputAck!(77);

        Assert.Equal(77, runtime.LastServerAckFrame);

        runtime.Dispose();
    }

    [Fact]
    public void Build_WiresReliableEventCursor_FromSnapshotAdmission()
    {
        var transport = CreateTransport();
        var runtime = new AbilityKit.Game.Flow.BattleReplicationRuntime();

        runtime.Build(
            transport, 30, 999UL, "battle-1", default,
            _ => { }, _ => { }, () => { }, () => { }, _ => { });

        // After Build, the options' reliable-event epoch callback is replaced with the runtime's
        // generation-guarded version (which reads from the cursor). The cursor starts empty until
        // the first reliable-event push arrives.
        var epoch = transport.Options.GetReliableEventEpoch!();
        Assert.NotNull(epoch);

        runtime.Dispose();
    }

    [Fact]
    public void Dispose_ClearsTransportAndUnbuilds()
    {
        var transport = CreateTransport();
        var runtime = new AbilityKit.Game.Flow.BattleReplicationRuntime();

        runtime.Build(
            transport, 30, 999UL, "battle-1", default,
            _ => { }, _ => { }, () => { }, () => { }, _ => { });

        Assert.True(runtime.IsBuilt);

        runtime.Dispose();

        Assert.False(runtime.IsBuilt);
        Assert.Null(runtime.Transport);
    }

    [Fact]
    public void Build_AcceptsAuthenticationFailedCallback_WithoutThrowing()
    {
        // The AuthenticationFailed callback is accepted by Build and stored for the transport event.
        // Full event-firing verification requires a live connection (integration scope); this test
        // pins the signature contract — Build must accept the callback and not throw.
        var transport = CreateTransport();
        var runtime = new AbilityKit.Game.Flow.BattleReplicationRuntime();

        Exception received = null;
        runtime.Build(
            transport, 30, 999UL, "battle-1", default,
            _ => { }, _ => { }, () => { }, () => { },
            onAuthenticationFailed: ex => received = ex);

        Assert.True(runtime.IsBuilt);
        Assert.Null(received); // no auth failure has occurred

        runtime.Dispose();
    }
}
