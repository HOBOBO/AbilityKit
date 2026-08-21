using System;
using System.Linq;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterClientBattleHandleTests
{
    [Fact]
    public async Task ClientBattleHandleSubmitsGatewayInputWithCurrentFrameContext()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var transport = new RecordingShooterRoomGatewayTransport(
            new WireSubmitBattleInputRes
            {
                Success = true,
                AcceptedFrame = 2,
                Message = "accepted"
            });
        var gateway = new ShooterRoomGatewayClient(transport);
        var session = new ShooterClientSession(runtime, presentation, tickRate: 30, decoder: null, gateway);
        var start = new ShooterStartGamePayload(
            "battle-handle-session",
            30,
            4902,
            new[]
            {
                new ShooterStartPlayer(11, "P11", 0f, 0f),
                new ShooterStartPlayer(12, "P12", 5f, 0f)
            });
        Assert.True(session.StartGame(in start));
        Assert.Equal(1, session.Tick(1f / 30f).Ticks);
        var anchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 0, 1d / 30d);
        var flow = new ShooterRoomGatewayFlowResult(
            "session-token",
            "room-9",
            1009ul,
            "battle-9",
            9009ul,
            11u,
            in anchor,
            223456L,
            ShooterRoomGatewayEntryKind.TeamLobby,
            canStart: true,
            started: true,
            subscribed: true,
            "ready");
        var handle = new ShooterClientBattleHandle(session, flow);

        var context = handle.CreateCurrentFrameInputContext();
        var result = await handle.SubmitLocalInputToGatewayAsync(moveX: 1f, moveY: 0f, aimX: 0f, aimY: 1f, fire: true, attackSlot: ShooterPlayerAttackSlots.Spread);

        Assert.Equal(session, handle.Session);
        Assert.Equal("room-9", handle.RoomId);
        Assert.Equal("battle-9", handle.BattleId);
        Assert.Equal(9009ul, handle.WorldId);
        Assert.Equal(11u, handle.PlayerId);
        Assert.Equal(session.CurrentFrame, handle.CurrentFrame);
        Assert.Equal(session.CurrentFrame, context.Frame);
        Assert.Equal("session-token", context.SessionToken);
        Assert.Equal("battle-9", context.BattleId);
        Assert.Equal(9009ul, context.WorldId);
        Assert.Equal(11u, context.PlayerId);
        Assert.Equal(0, flow.TargetFrame);
        Assert.Equal(0, flow.CatchUpFrames);
        Assert.Equal(context.Frame, result.Local.RequestedFrame);
        Assert.True(result.Remote.Success);
        Assert.Equal(2, result.Remote.AcceptedFrame);
        Assert.Equal(RoomGatewayOpCodes.SubmitBattleInput, transport.LastOpCode);

        var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(transport.LastPayload);
        Assert.Equal("session-token", wire.SessionToken);
        Assert.Equal("battle-9", wire.BattleId);
        Assert.Equal(9009ul, wire.WorldId);
        Assert.Equal(session.CurrentFrame, wire.Frame);
        Assert.Equal(11u, wire.PlayerId);
        Assert.Equal(ShooterOpCodes.Input.PlayerCommand, wire.InputOpCode);
        Assert.NotNull(wire.Payload);
        Assert.Equal(1UL, wire.CommandSequence);
        Assert.Equal(result.Local.Packet.Payload, wire.Payload!);
        var commands = ShooterInputCodec.Deserialize(wire.Payload!);
        Assert.Single(commands);
        Assert.Equal(11, commands[0].PlayerId);
        Assert.True(commands[0].Fire);
        Assert.Equal(ShooterPlayerAttackSlots.Spread, commands[0].AttackSlot);

        await handle.SubmitLocalInputToGatewayAsync(moveX: 0f, moveY: 1f, aimX: 1f, aimY: 0f, fire: false);
        var secondWire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(transport.LastPayload);
        Assert.Equal(2UL, secondWire.CommandSequence);
    }
    [Fact]
    public async Task ClientBattleHandleRetriesTooFarFutureInputAtAuthoritativeCurrentFrame()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var transport = new RecordingShooterRoomGatewayTransport(
            new WireSubmitBattleInputRes
            {
                Success = false,
                AcceptedFrame = 481,
                Message = "Input frame is too far ahead.",
                CurrentFrame = 40,
                Status = "RejectedTooFarFuture",
                ShouldResync = true,
                ServerTicks = 987654321L
            },
            new WireSubmitBattleInputRes
            {
                Success = true,
                AcceptedFrame = 42,
                Message = "accepted",
                CurrentFrame = 40,
                Status = "Accepted",
                ShouldResync = false,
                ServerTicks = 987654322L
            });
        var gateway = new ShooterRoomGatewayClient(transport);
        var session = new ShooterClientSession(runtime, presentation, tickRate: 30, decoder: null, gateway);
        var start = new ShooterStartGamePayload(
            "battle-handle-future-frame-retry",
            30,
            4903,
            new[]
            {
                new ShooterStartPlayer(11, "P11", 0f, 0f)
            });
        Assert.True(session.StartGame(in start));
        session.CatchUpToFrame(481);
        var anchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 0, 1d / 30d);
        var flow = new ShooterRoomGatewayFlowResult(
            "session-token",
            "room-9",
            1009ul,
            "battle-9",
            9009ul,
            11u,
            in anchor,
            223456L,
            ShooterRoomGatewayEntryKind.LateJoin,
            canStart: false,
            started: true,
            subscribed: true,
            "ready");
        var handle = new ShooterClientBattleHandle(session, flow);

        var result = await handle.SubmitLocalInputToGatewayAsync(
            moveX: 1f,
            moveY: 0f,
            aimX: 1f,
            aimY: 0f,
            fire: false);

        Assert.True(result.Remote.Success);
        Assert.Equal(40, result.Local.RequestedFrame);
        Assert.Equal(42, result.Remote.AcceptedFrame);
        Assert.Equal(2, transport.RequestCount);
        var firstRequest = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(transport.Payloads[0]);
        var retryRequest = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(transport.Payloads[1]);
        Assert.Equal(481, firstRequest.Frame);
        Assert.Equal(40, retryRequest.Frame);
        Assert.Equal(1UL, firstRequest.CommandSequence);
        Assert.Equal(2UL, retryRequest.CommandSequence);
        Assert.Equal(firstRequest.Payload, retryRequest.Payload);
        Assert.False(session.NeedsFullSnapshotResync);
    }

    [Fact]
    public void ClientBattleHandleUsesAppliedPureStateFrameForGatewayInputWithoutRewindingRuntime()
    {
        var start = new ShooterStartGamePayload(
            "battle-handle-late-join-input-frame",
            30,
            4903,
            new[]
            {
                new ShooterStartPlayer(11, "P11", 0f, 0f),
                new ShooterStartPlayer(12, "P12", 5f, 0f)
            });
        var source = new ShooterBattleRuntimePort();
        Assert.True(source.StartGame(in start));
        for (var frame = 0; frame < 168; frame++)
        {
            Assert.True(source.Tick(1f / 30f));
        }

        var baseline = source.ExportPureStateSnapshot(9009ul, isFullBaseline: true);
        var presentation = new ShooterPresentationFacade();
        var session = new ShooterClientSession(
            new ShooterBattleRuntimePort(),
            ShooterPresentationSessionContext.CreateFromFacade(presentation),
            tickRate: 30,
            decoder: null,
            gateway: null,
            NetworkSyncModel.AuthoritativeInterpolation);
        Assert.True(session.StartGame(in start));
        session.CatchUpToFrame(293);
        var anchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 0, 1d / 30d);
        var flow = new ShooterRoomGatewayFlowResult(
            "session-token",
            "room-9",
            1009ul,
            "battle-9",
            9009ul,
            11u,
            in anchor,
            223456L,
            ShooterRoomGatewayEntryKind.LateJoin,
            canStart: false,
            started: true,
            subscribed: true,
            "ready");
        var handle = new ShooterClientBattleHandle(session, flow);

        var applyResult = session.ApplyGatewayPush(
            RoomGatewayOpCodes.SnapshotPushed,
            CreatePureStateGatewayPayload(in baseline, ShooterOpCodes.Snapshot.PureState, isFullSnapshot: true));
        var context = handle.CreateCurrentFrameInputContext();

        Assert.Equal(ShooterSnapshotApplyResult.AppliedActorSnapshot, applyResult);
        Assert.Equal(293, session.CurrentFrame);
        Assert.Equal(baseline.Frame, session.GatewayInputFrame);
        Assert.Equal(baseline.Frame, context.Frame);
    }

    [Fact]
    public async Task ClientBattleHandleAutomaticallyRequestsFullStateSyncWhenInputResponseRequiresResync()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var transport = new RecordingShooterRoomGatewayTransport(
            new WireSubmitBattleInputRes
            {
                Success = false,
                AcceptedFrame = 12,
                Message = "Input frame is too far ahead.",
                CurrentFrame = 8,
                Status = "RejectedTooFarFuture",
                ShouldResync = true,
                ServerTicks = 987654321L
            });
        var gateway = new ShooterRoomGatewayClient(transport);
        var roomClient = new ScriptedShooterRoomClient();
        var session = new ShooterClientSession(runtime, presentation, tickRate: 30, decoder: null, gateway);
        var start = new ShooterStartGamePayload(
            "battle-handle-resync-session",
            30,
            4906,
            new[]
            {
                new ShooterStartPlayer(11, "P11", 0f, 0f)
            });
        Assert.True(session.StartGame(in start));
        Assert.Equal(1, session.Tick(1f / 30f).Ticks);
        var anchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 0, 1d / 30d);
        var flow = new ShooterRoomGatewayFlowResult(
            "session-token",
            "room-9",
            1009ul,
            "battle-9",
            9009ul,
            11u,
            in anchor,
            223456L,
            ShooterRoomGatewayEntryKind.TeamLobby,
            canStart: true,
            started: true,
            subscribed: true,
            "ready");
        var handle = new ShooterClientBattleHandle(session, flow, roomClient);

        var result = await handle.SubmitLocalInputToGatewayAsync(moveX: 1f, moveY: 0f, aimX: 0f, aimY: 1f, fire: true);

        Assert.False(result.Remote.Success);
        Assert.True(result.Remote.ShouldResync);
        Assert.True(session.NeedsFullSnapshotResync);
        Assert.Equal(ShooterClientResyncReason.ClientHashRejectedByServer, session.LastResyncReason);
        Assert.Equal("session-token", roomClient.LastFullStateSyncRequest.SessionToken);
        Assert.Equal("battle-9", roomClient.LastFullStateSyncRequest.BattleId);
        Assert.Equal("room-9", roomClient.LastFullStateSyncRequest.RoomId);
        Assert.Equal(9009ul, roomClient.LastFullStateSyncRequest.WorldId);
        Assert.Equal(1, roomClient.LastFullStateSyncRequest.ClientFrame);
        Assert.Equal(8, roomClient.LastFullStateSyncRequest.LastAuthoritativeFrame);
        Assert.Equal(ShooterClientResyncReason.ClientHashRejectedByServer.ToString(), roomClient.LastFullStateSyncRequest.Reason);
        Assert.Contains("request-full-state:room-9:battle-9:ClientHashRejectedByServer", roomClient.Calls);
    }

    [Fact]
    public async Task ClientBattleHandleRequestsFullStateSyncWhenPureStateDeltaNeedsBaseline()
    {
        var source = new ShooterBattleRuntimePort();
        var sourceStart = new ShooterStartGamePayload(
            "battle-handle-pure-state-source",
            30,
            4908,
            new[]
            {
                new ShooterStartPlayer(11, "P11", 0f, 0f),
                new ShooterStartPlayer(12, "P12", 5f, 0f)
            });
        Assert.True(source.StartGame(in sourceStart));
        Assert.True(source.Tick(1f / 30f));
        var delta = source.ExportPureStateSnapshot(9009ul, isFullBaseline: false, baselineFrame: 99, baselineHash: 123u);
        var presentation = new ShooterPresentationFacade();
        var session = new ShooterClientSession(new ShooterBattleRuntimePort(), presentation, tickRate: 30);
        Assert.True(session.StartGame(in sourceStart));
        var roomClient = new ScriptedShooterRoomClient();
        var anchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 0, 1d / 30d);
        var flow = new ShooterRoomGatewayFlowResult(
            "session-token",
            "room-9",
            1009ul,
            "battle-9",
            9009ul,
            11u,
            in anchor,
            223456L,
            ShooterRoomGatewayEntryKind.TeamLobby,
            canStart: true,
            started: true,
            subscribed: true,
            "ready");
        var handle = new ShooterClientBattleHandle(session, flow, roomClient);

        var applyResult = session.ApplyGatewayPush(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreatePureStateGatewayPayload(in delta, ShooterOpCodes.Snapshot.PureStateDelta, isFullSnapshot: false));
        var requestResult = await handle.RequestFullSnapshotResyncIfNeededAsync();

        Assert.Equal(ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded, applyResult);
        Assert.False(session.NeedsFullSnapshotResync);
        Assert.True(presentation.NeedsPureStateFullBaselineResync);
        Assert.True(requestResult.Accepted);
        Assert.Equal("session-token", roomClient.LastFullStateSyncRequest.SessionToken);
        Assert.Equal("battle-9", roomClient.LastFullStateSyncRequest.BattleId);
        Assert.Equal("room-9", roomClient.LastFullStateSyncRequest.RoomId);
        Assert.Equal(9009ul, roomClient.LastFullStateSyncRequest.WorldId);
        Assert.Equal(0, roomClient.LastFullStateSyncRequest.ClientFrame);
        Assert.Equal(delta.Frame, roomClient.LastFullStateSyncRequest.LastAuthoritativeFrame);
        Assert.Equal(0u, roomClient.LastFullStateSyncRequest.ClientStateHash);
        Assert.Equal(delta.StateHash, roomClient.LastFullStateSyncRequest.AuthoritativeStateHash);
        Assert.Equal("PureStateMissingBaseline", roomClient.LastFullStateSyncRequest.Reason);
        Assert.Contains("request-full-state:room-9:battle-9:PureStateMissingBaseline", roomClient.Calls);
    }

    [Fact]
    public async Task ClientBattleHandleApplyGatewayPushAndRequestsFullStateSyncWhenPureStateDeltaNeedsBaseline()
    {
        var source = new ShooterBattleRuntimePort();
        var sourceStart = new ShooterStartGamePayload(
            "battle-handle-pure-state-source-apply",
            30,
            4909,
            new[]
            {
                new ShooterStartPlayer(11, "P11", 0f, 0f),
                new ShooterStartPlayer(12, "P12", 5f, 0f)
            });
        Assert.True(source.StartGame(in sourceStart));
        Assert.True(source.Tick(1f / 30f));
        var delta = source.ExportPureStateSnapshot(9010ul, isFullBaseline: false, baselineFrame: 99, baselineHash: 123u);
        var presentation = new ShooterPresentationFacade();
        var session = new ShooterClientSession(new ShooterBattleRuntimePort(), presentation, tickRate: 30);
        Assert.True(session.StartGame(in sourceStart));
        var roomClient = new ScriptedShooterRoomClient();
        var anchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 0, 1d / 30d);
        var flow = new ShooterRoomGatewayFlowResult(
            "session-token",
            "room-9",
            1009ul,
            "battle-9",
            9010ul,
            11u,
            in anchor,
            223456L,
            ShooterRoomGatewayEntryKind.TeamLobby,
            canStart: true,
            started: true,
            subscribed: true,
            "ready");
        var handle = new ShooterClientBattleHandle(session, flow, roomClient);

        var result = await handle.ApplyGatewayPushAndRequestFullSnapshotResyncIfNeededAsync(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreatePureStateGatewayPayload(in delta, ShooterOpCodes.Snapshot.PureStateDelta, isFullSnapshot: false));

        Assert.Equal(ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded, result);
        Assert.True(presentation.NeedsPureStateFullBaselineResync);
        Assert.Equal("PureStateMissingBaseline", roomClient.LastFullStateSyncRequest.Reason);
        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("request-full-state:")));
    }

    [Fact]
    public async Task ClientBattleHandleDoesNotRepeatSameFullStateSyncRequestWhileWaitingForSnapshot()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var transport = new RecordingShooterRoomGatewayTransport(
            new WireSubmitBattleInputRes
            {
                Success = false,
                AcceptedFrame = 12,
                Message = "Input frame is too far ahead.",
                CurrentFrame = 8,
                Status = "RejectedTooFarFuture",
                ShouldResync = true,
                ServerTicks = 987654321L
            });
        var gateway = new ShooterRoomGatewayClient(transport);
        var roomClient = new ScriptedShooterRoomClient();
        var session = new ShooterClientSession(runtime, presentation, tickRate: 30, decoder: null, gateway);
        var start = new ShooterStartGamePayload(
            "battle-handle-repeat-resync-session",
            30,
            4907,
            new[]
            {
                new ShooterStartPlayer(11, "P11", 0f, 0f)
            });
        Assert.True(session.StartGame(in start));
        Assert.Equal(1, session.Tick(1f / 30f).Ticks);
        var anchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 0, 1d / 30d);
        var flow = new ShooterRoomGatewayFlowResult(
            "session-token",
            "room-9",
            1009ul,
            "battle-9",
            9009ul,
            11u,
            in anchor,
            223456L,
            ShooterRoomGatewayEntryKind.TeamLobby,
            canStart: true,
            started: true,
            subscribed: true,
            "ready");
        var handle = new ShooterClientBattleHandle(session, flow, roomClient);

        await handle.SubmitLocalInputToGatewayAsync(moveX: 1f, moveY: 0f, aimX: 0f, aimY: 1f, fire: true);
        var repeated = await handle.RequestFullSnapshotResyncIfNeededAsync();

        Assert.False(repeated.Success);
        Assert.False(repeated.Accepted);
        Assert.Equal("not requested", repeated.Message);
        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("request-full-state:")));
    }

    [Fact]
    public async Task ClientBattleHandleCoalescesChangingPureStateGapsUntilBaselineRecovers()
    {
        var source = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "battle-handle-coalesced-pure-state-recovery",
            30,
            4910,
            new[]
            {
                new ShooterStartPlayer(11, "P11", 0f, 0f),
                new ShooterStartPlayer(12, "P12", 5f, 0f)
            });
        Assert.True(source.StartGame(in start));
        Assert.True(source.Tick(1f / 30f));
        var firstDelta = source.ExportPureStateSnapshot(9011ul, isFullBaseline: false, baselineFrame: 99, baselineHash: 123u);
        Assert.True(source.Tick(1f / 30f));
        var laterDelta = source.ExportPureStateSnapshot(9011ul, isFullBaseline: false, baselineFrame: 100, baselineHash: 124u);

        var presentation = new ShooterPresentationFacade();
        var session = new ShooterClientSession(new ShooterBattleRuntimePort(), presentation, tickRate: 30);
        Assert.True(session.StartGame(in start));
        var roomClient = new ScriptedShooterRoomClient();
        var anchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 0, 1d / 30d);
        var flow = new ShooterRoomGatewayFlowResult(
            "session-token",
            "room-9",
            1009ul,
            "battle-9",
            9011ul,
            11u,
            in anchor,
            223456L,
            ShooterRoomGatewayEntryKind.TeamLobby,
            canStart: true,
            started: true,
            subscribed: true,
            "ready");
        var handle = new ShooterClientBattleHandle(session, flow, roomClient);

        var firstResult = await handle.ApplyGatewayPushAndRequestFullSnapshotResyncIfNeededAsync(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreatePureStateGatewayPayload(in firstDelta, ShooterOpCodes.Snapshot.PureStateDelta, isFullSnapshot: false));
        var firstRequestedFrame = roomClient.LastFullStateSyncRequest.LastAuthoritativeFrame;
        var laterResult = await handle.ApplyGatewayPushAndRequestFullSnapshotResyncIfNeededAsync(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreatePureStateGatewayPayload(in laterDelta, ShooterOpCodes.Snapshot.PureStateDelta, isFullSnapshot: false));

        Assert.Equal(ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded, firstResult);
        Assert.Equal(ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded, laterResult);
        Assert.NotEqual(firstDelta.Frame, laterDelta.Frame);
        Assert.Equal(firstDelta.Frame, firstRequestedFrame);
        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("request-full-state:")));
        Assert.Equal(1, handle.AutomaticFullStateSyncCoalescedRequestCount);

        var recoveredBaseline = source.ExportPureStateSnapshot(9011ul, isFullBaseline: true);
        var recoveredResult = await handle.ApplyGatewayPushAndRequestFullSnapshotResyncIfNeededAsync(
            RoomGatewayOpCodes.SnapshotPushed,
            CreatePureStateGatewayPayload(in recoveredBaseline, ShooterOpCodes.Snapshot.PureState, isFullSnapshot: true));
        Assert.Equal(ShooterSnapshotApplyResult.AppliedActorSnapshot, recoveredResult);
        Assert.False(presentation.NeedsPureStateFullBaselineResync);

        Assert.True(source.Tick(1f / 30f));
        var newGap = source.ExportPureStateSnapshot(9011ul, isFullBaseline: false, baselineFrame: 101, baselineHash: 125u);
        var newGapResult = await handle.ApplyGatewayPushAndRequestFullSnapshotResyncIfNeededAsync(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreatePureStateGatewayPayload(in newGap, ShooterOpCodes.Snapshot.PureStateDelta, isFullSnapshot: false));

        Assert.Equal(ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded, newGapResult);
        Assert.Equal(2, roomClient.Calls.Count(call => call.StartsWith("request-full-state:")));
        Assert.Equal(1, handle.AutomaticFullStateSyncCoalescedRequestCount);
    }

    [Fact]
    public async Task ClientBattleHandleExecutesFrameworkRecoveryDecisionThroughActionRouter()
    {
        var session = new ShooterClientSession(
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade(),
            tickRate: 30);
        var roomClient = new ScriptedShooterRoomClient();
        var anchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 0, 1d / 30d);
        var flow = new ShooterRoomGatewayFlowResult(
            "session-token",
            "room-9",
            1009ul,
            "battle-9",
            9009ul,
            11u,
            in anchor,
            223456L,
            ShooterRoomGatewayEntryKind.TeamLobby,
            canStart: true,
            started: true,
            subscribed: true,
            "ready");
        var handle = new ShooterClientBattleHandle(session, flow, roomClient);
        var signal = new NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
            SyncHealthSeverity.Error,
            frame: 12,
            correlationContext: "battle-9");
        Assert.True(session.TryReportRecoverySignal(in signal, out _));

        var execution = await handle.ExecuteRecoveryDecisionAsync();

        Assert.Equal(NetworkSessionRecoveryExecutionStatus.Executed, execution.Status);
        Assert.True(execution.HasValue);
        Assert.True(execution.Value.Accepted);
        Assert.Contains("request-full-state:room-9:battle-9:None", roomClient.Calls);
        Assert.Equal(1, handle.RecoveryRuntimeDiagnostics.StartedExecutionCount);
        Assert.Equal(1, handle.RecoveryRuntimeDiagnostics.CompletedExecutionCount);
    }

    private static ArraySegment<byte> CreatePureStateGatewayPayload(in ShooterPureStateSnapshotPayload pureState, int payloadOpCode, bool isFullSnapshot)
    {
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = pureState.WorldId,
            Frame = pureState.Frame,
            Timestamp = 456.5,
            IsFullSnapshot = isFullSnapshot,
            Actors = null,
            PayloadOpCode = payloadOpCode,
            Payload = ShooterPureStateSyncCodec.Serialize(in pureState),
            ServerTicks = pureState.ServerTick
        };
        return WireRoomGatewayBinary.Serialize(in wire);
    }
}
