using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterClientGatewayLauncherTests
{
    [Fact]
    public async Task ClientGatewayLauncherCreatesRoomSessionAndBattleHandle()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var transport = new ScriptedShooterGatewayLaunchTransport
        {
            JoinCurrentPlayerId = 41u
        };
        var launcher = new ShooterClientGatewayLauncher(transport);
        var start = new ShooterStartGamePayload(
            "launcher-session",
            30,
            4903,
            new[]
            {
                new ShooterStartPlayer(41, "P41", 0f, 0f),
                new ShooterStartPlayer(42, "P42", 5f, 0f)
            });

        var launched = await launcher.CreateReadyStartAndSubscribeAsync(
            runtime,
            presentation,
            start,
            "session-token",
            ShooterRoomLaunchSpec.CreateDefault("client-launcher"),
            playerId: 41u);

        Assert.True(launched.Session.IsStarted);
        Assert.True(launched.Session.HasGateway);
        Assert.Equal(0, launched.Session.CurrentFrame);
        Assert.Equal(0, presentation.ViewModel.Frame);
        Assert.Equal(9041ul, runtime.StartSpec.WorldId);
        Assert.True(runtime.StartSpec.HasWorldStartAnchor);
        Assert.Equal(transport.StartServerTicks, runtime.StartSpec.StartServerTicks);
        Assert.Equal(transport.ServerTickFrequency, runtime.StartSpec.ServerTickFrequency);
        Assert.Equal(transport.StartFrame, runtime.StartSpec.StartFrame);
        Assert.Equal(transport.FixedDeltaSeconds, runtime.StartSpec.FixedDeltaSeconds);
        Assert.Equal("room-launch", launched.Flow.RoomId);
        Assert.Equal("battle-launch", launched.Flow.BattleId);
        Assert.Equal(9041ul, launched.Flow.WorldId);
        Assert.Equal(41u, launched.Flow.PlayerId);
        Assert.Equal(ShooterRoomGatewayEntryKind.TeamLobby, launched.Flow.EntryKind);
        Assert.Equal(launched.Session, launched.Battle.Session);
        Assert.Equal("battle-launch", launched.Battle.BattleId);
        Assert.True(launched.Summary.IsClosed);
        Assert.False(launched.Summary.IsRunningEntry);
        Assert.Equal(ShooterRoomGatewayEntryKind.TeamLobby, launched.Summary.EntryKind);
        Assert.Equal("room-launch", launched.Summary.RoomId);
        Assert.Equal("battle-launch", launched.Summary.BattleId);
        Assert.Equal(9041ul, launched.Summary.WorldId);
        Assert.Equal(41u, launched.Summary.PlayerId);
        Assert.Equal(RoomGatewayOpCodes.CreateRoom, transport.OpCodes[0]);
        Assert.Equal(RoomGatewayOpCodes.JoinRoom, transport.OpCodes[1]);
        Assert.Equal(RoomGatewayOpCodes.SetReady, transport.OpCodes[2]);
        Assert.Equal(RoomGatewayOpCodes.BeginLoading, transport.OpCodes[3]);
        var progressCount = transport.OpCodes.Count(opCode => opCode == RoomGatewayOpCodes.ReportLoadingProgress);
        Assert.True(progressCount >= 1);
        Assert.All(
            transport.OpCodes.GetRange(4, progressCount),
            opCode => Assert.Equal(RoomGatewayOpCodes.ReportLoadingProgress, opCode));
        var assetsIndex = 4 + progressCount;
        Assert.Equal(RoomGatewayOpCodes.ReportAssetsLoaded, transport.OpCodes[assetsIndex]);
        Assert.Equal(RoomGatewayOpCodes.GetSnapshot, transport.OpCodes[assetsIndex + 1]);
        Assert.Equal(RoomGatewayOpCodes.SubscribeStateSync, transport.OpCodes[assetsIndex + 2]);
        Assert.Equal(assetsIndex + 3, transport.OpCodes.Count);
        Assert.DoesNotContain(RoomGatewayOpCodes.StartBattle, transport.OpCodes);
        Assert.Equal(7L, transport.LastReportAssetsLoadedRequest.LaunchGeneration);
        Assert.Equal(3, transport.LastReportAssetsLoadedRequest.ManifestVersion);
        Assert.Equal("manifest-shooter-v3", transport.LastReportAssetsLoadedRequest.ManifestHash);

        var submit = await launched.Battle.SubmitLocalInputToGatewayAsync(moveX: 1f, moveY: 0f, aimX: 1f, aimY: 0f, fire: false);

        Assert.True(submit.Remote.Success);
        Assert.Equal(RoomGatewayOpCodes.SubmitBattleInput, transport.OpCodes[transport.OpCodes.Count - 1]);
        var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(transport.LastPayload);
        Assert.Equal("session-token", wire.SessionToken);
        Assert.Equal("battle-launch", wire.BattleId);
        Assert.Equal(9041ul, wire.WorldId);
        Assert.Equal(launched.Session.CurrentFrame, wire.Frame);
        Assert.Equal(41u, wire.PlayerId);
    }

    [Fact]
    public async Task ClientGatewayLauncherCatchesUpSessionToFlowTargetFrame()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var transport = new ScriptedShooterGatewayLaunchTransport
        {
            StartServerNowTicks = 123456L + 1500000L,
            JoinCurrentPlayerId = 41u
        };
        var launcher = new ShooterClientGatewayLauncher(transport);
        var start = new ShooterStartGamePayload(
            "launcher-catch-up-session",
            30,
            4904,
            new[]
            {
                new ShooterStartPlayer(41, "P41", 0f, 0f),
                new ShooterStartPlayer(42, "P42", 5f, 0f)
            });

        var launched = await launcher.CreateReadyStartAndSubscribeAsync(
            runtime,
            presentation,
            start,
            "session-token",
            ShooterRoomLaunchSpec.CreateDefault("client-launcher"),
            playerId: 41u);

        Assert.Equal(4, launched.Flow.TargetFrame);
        Assert.Equal(4, launched.Flow.CatchUpFrames);
        Assert.Equal(launched.Flow.TargetFrame, launched.Session.CurrentFrame);
        Assert.Equal(launched.Flow.TargetFrame, presentation.ViewModel.Frame);

        Assert.Equal(9041ul, runtime.StartSpec.WorldId);
        Assert.True(runtime.StartSpec.HasWorldStartAnchor);
        Assert.Equal(transport.StartServerTicks, runtime.StartSpec.StartServerTicks);
        Assert.Equal(transport.ServerTickFrequency, runtime.StartSpec.ServerTickFrequency);
        Assert.Equal(transport.StartFrame, runtime.StartSpec.StartFrame);
        Assert.Equal(transport.FixedDeltaSeconds, runtime.StartSpec.FixedDeltaSeconds);

        var submit = await launched.Battle.SubmitLocalInputToGatewayAsync(moveX: 1f, moveY: 0f, aimX: 1f, aimY: 0f, fire: false);

        Assert.True(submit.Remote.Success);
        var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(transport.LastPayload);
        Assert.Equal(launched.Flow.TargetFrame, wire.Frame);
    }

    [Fact]
    public async Task ClientGatewayLauncherReconnectsRunningBattleWithoutReadyOrStart()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var transport = new ScriptedShooterGatewayLaunchTransport
        {
            JoinKind = WireRoomJoinKind.Reconnect,
            JoinBattleId = "battle-running-reconnect",
            JoinWorldId = 9101ul,
            JoinCanStart = false,
            StartFrame = 18,
            JoinServerNowTicks = 123456L + 1000000L,
            JoinCurrentPlayerId = 41u
        };
        var launcher = new ShooterClientGatewayLauncher(transport);

        var launched = await launcher.JoinReadyStartAndSubscribeAsync(
            runtime,
            presentation,
            CreateStartPayload("launcher-reconnect-session"),
            "session-token",
            "running-room",
            ShooterRoomLaunchSpec.CreateDefault("client-reconnect"),
            playerId: 41u);

        Assert.True(launched.Session.IsStarted);
        Assert.True(launched.Session.HasGateway);
        Assert.Equal(ShooterRoomGatewayEntryKind.Reconnect, launched.Flow.EntryKind);
        Assert.Equal("battle-running-reconnect", launched.Flow.BattleId);
        Assert.Equal("battle-running-reconnect", launched.Battle.BattleId);
        Assert.Equal(9101ul, launched.Flow.WorldId);
        Assert.Equal(9101ul, runtime.StartSpec.WorldId);
        Assert.Equal(21, launched.Flow.TargetFrame);
        Assert.Equal(3, launched.Flow.CatchUpFrames);
        Assert.Equal(launched.Flow.TargetFrame, launched.Session.CurrentFrame);
        Assert.Equal(launched.Flow.TargetFrame, presentation.ViewModel.Frame);
        Assert.True(launched.Summary.IsClosed);
        Assert.True(launched.Summary.IsRunningEntry);
        Assert.Equal(ShooterRoomGatewayEntryKind.Reconnect, launched.Summary.EntryKind);
        Assert.Equal("running-room", launched.Summary.RoomId);
        Assert.Equal("battle-running-reconnect", launched.Summary.BattleId);
        Assert.Equal(9101ul, launched.Summary.WorldId);
        Assert.Equal(41u, launched.Summary.PlayerId);
        Assert.Equal(2, transport.OpCodes.Count);
        Assert.Equal(RoomGatewayOpCodes.JoinRoom, transport.OpCodes[0]);
        Assert.Equal(RoomGatewayOpCodes.SubscribeStateSync, transport.OpCodes[1]);
        Assert.DoesNotContain(RoomGatewayOpCodes.SetReady, transport.OpCodes);
        Assert.DoesNotContain(RoomGatewayOpCodes.StartBattle, transport.OpCodes);

        var submit = await launched.Battle.SubmitLocalInputToGatewayAsync(moveX: 1f, moveY: 0f, aimX: 1f, aimY: 0f, fire: false);

        Assert.True(submit.Remote.Success);
        var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(transport.LastPayload);
        Assert.Equal("session-token", wire.SessionToken);
        Assert.Equal("battle-running-reconnect", wire.BattleId);
        Assert.Equal(9101ul, wire.WorldId);
        Assert.Equal(launched.Flow.TargetFrame, wire.Frame);
        Assert.Equal(41u, wire.PlayerId);
    }

    [Fact]
    public async Task ClientGatewayLauncherLateJoinsRunningBattleWithoutReadyOrStart()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var transport = new ScriptedShooterGatewayLaunchTransport
        {
            JoinKind = WireRoomJoinKind.LateJoin,
            JoinBattleId = "battle-running-late",
            JoinWorldId = 9201ul,
            JoinCanStart = false,
            StartFrame = 24,
            JoinServerNowTicks = 123456L + 2000000L,
            JoinCurrentPlayerId = 42u
        };
        var launcher = new ShooterClientGatewayLauncher(transport);

        var launched = await launcher.JoinReadyStartAndSubscribeAsync(
            runtime,
            presentation,
            CreateStartPayload("launcher-late-session"),
            "session-token",
            "running-room",
            ShooterRoomLaunchSpec.CreateDefault("client-late"),
            playerId: 42u);

        Assert.True(launched.Session.IsStarted);
        Assert.True(launched.Session.HasGateway);
        Assert.Equal(ShooterRoomGatewayEntryKind.LateJoin, launched.Flow.EntryKind);
        Assert.Equal("battle-running-late", launched.Flow.BattleId);
        Assert.Equal("battle-running-late", launched.Battle.BattleId);
        Assert.Equal(9201ul, launched.Flow.WorldId);
        Assert.Equal(9201ul, runtime.StartSpec.WorldId);
        Assert.Equal(30, launched.Flow.TargetFrame);
        Assert.Equal(6, launched.Flow.CatchUpFrames);
        Assert.Equal(launched.Flow.TargetFrame, launched.Session.CurrentFrame);
        Assert.Equal(launched.Flow.TargetFrame, presentation.ViewModel.Frame);
        Assert.True(launched.Summary.IsClosed);
        Assert.True(launched.Summary.IsRunningEntry);
        Assert.Equal(ShooterRoomGatewayEntryKind.LateJoin, launched.Summary.EntryKind);
        Assert.Equal("running-room", launched.Summary.RoomId);
        Assert.Equal("battle-running-late", launched.Summary.BattleId);
        Assert.Equal(9201ul, launched.Summary.WorldId);
        Assert.Equal(42u, launched.Summary.PlayerId);
        Assert.Equal(2, transport.OpCodes.Count);
        Assert.Equal(RoomGatewayOpCodes.JoinRoom, transport.OpCodes[0]);
        Assert.Equal(RoomGatewayOpCodes.SubscribeStateSync, transport.OpCodes[1]);
        Assert.DoesNotContain(RoomGatewayOpCodes.SetReady, transport.OpCodes);
        Assert.DoesNotContain(RoomGatewayOpCodes.StartBattle, transport.OpCodes);

        var submit = await launched.Battle.SubmitLocalInputToGatewayAsync(moveX: 0f, moveY: 1f, aimX: 0f, aimY: 1f, fire: false);

        Assert.True(submit.Remote.Success);
        var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(transport.LastPayload);
        Assert.Equal("session-token", wire.SessionToken);
        Assert.Equal("battle-running-late", wire.BattleId);
        Assert.Equal(9201ul, wire.WorldId);
        Assert.Equal(launched.Flow.TargetFrame, wire.Frame);
        Assert.Equal(42u, wire.PlayerId);
    }

    private static ShooterStartGamePayload CreateStartPayload(string sessionId)
    {
        return new ShooterStartGamePayload(
            sessionId,
            30,
            4905,
            new[]
            {
                new ShooterStartPlayer(41, "P41", 0f, 0f),
                new ShooterStartPlayer(42, "P42", 5f, 0f)
            });
    }
}
