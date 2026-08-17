using System;
using System.Linq;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterRoomGatewayConnectionTests
{
    [Fact]
    public void BattleDataPlaneDrainHonorsPushBudgetAndReportsBacklogWithoutReordering()
    {
        var connection = new FakeGatewayConnection();
        using var battleTransport = CreateBattleTransport(connection);
        var options = new ShooterBattleDataPlaneOptions(
            maxPushesPerDrain: 1,
            maxDrainDuration: TimeSpan.FromSeconds(1));
        using var battleDataPlane = new ShooterBattleDataPlane(battleTransport, options);
        var received = new List<uint>();
        battleDataPlane.ServerPushReceived += (opCode, _) => received.Add(opCode);

        connection.Push(101u, Array.Empty<byte>());
        connection.Push(102u, Array.Empty<byte>());
        connection.Push(103u, Array.Empty<byte>());

        Assert.Equal(1, battleDataPlane.Drain());
        var firstDrain = battleDataPlane.Diagnostics;
        Assert.Equal(new[] { 101u }, received);
        Assert.Equal(2, firstDrain.QueueDepth);
        Assert.Equal(3, firstDrain.PeakQueueDepth);
        Assert.Equal(1, firstDrain.BudgetLimitedDrainCount);

        Assert.Equal(1, battleDataPlane.Drain());
        Assert.Equal(1, battleDataPlane.Drain());
        var completed = battleDataPlane.Diagnostics;
        Assert.Equal(new[] { 101u, 102u, 103u }, received);
        Assert.Equal(0, completed.QueueDepth);
        Assert.Equal(3, completed.EnqueuedPushCount);
        Assert.Equal(3, completed.ProcessedPushCount);
    }

    [Fact]
    public void RoomStatePushUpdatesShooterRoomSnapshotFeedWithoutBattleSession()
    {
        var connection = new FakeGatewayConnection();
        using var gatewayConnection = new ShooterRoomGatewayConnection(connection);
        var roomClient = new ShooterRoomGatewayRoomClient(gatewayConnection);
        var feed = Assert.IsAssignableFrom<IShooterRoomGatewaySnapshotFeed>(roomClient);
        var changed = 0;
        feed.SnapshotChanged += _ => changed++;
        var push = new WireRoomStateChangedPush
        {
            RoomId = "room-push",
            Snapshot = new WireRoomSnapshot
            {
                Summary = new WireRoomSummary
                {
                    RoomId = "room-push",
                    OwnerAccountId = "owner"
                },
                Phase = 1,
                RoomRevision = 7,
                LaunchGeneration = 3,
                LaunchManifestVersion = 2,
                LaunchManifestHash = "manifest"
            }
        };

        connection.Push(
            RoomGatewayOpCodes.RoomStateChanged,
            WireRoomGatewayBinary.Serialize(in push));

        Assert.Equal(1, changed);
        Assert.NotNull(feed.Current);
        Assert.Equal("room-push", feed.Current!.RoomId);
        Assert.Equal(7, feed.Current.RoomRevision);
        Assert.Equal(3, feed.Current.LaunchGeneration);
    }

    [Fact]
    public async Task RoomConnectionHandlesRequestsWhileBattleDataPlaneHandlesSnapshotPushes()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var connection = new FakeGatewayConnection();
        using var gatewayConnection = new ShooterRoomGatewayConnection(connection);
        var gateway = new ShooterRoomGatewayClient(gatewayConnection);
        var session = new ShooterClientSession(runtime, presentation, tickRate: 30, decoder: null, gateway);
        gatewayConnection.AttachSession(session);
        using var battleTransport = CreateBattleTransport(connection);
        using var battleDataPlane = new ShooterBattleDataPlane(battleTransport);

        var start = new ShooterStartGamePayload(
            "connection-session",
            30,
            5901,
            new[]
            {
                new ShooterStartPlayer(21, "P21", 0f, 0f),
                new ShooterStartPlayer(22, "P22", 5f, 0f)
            });
        Assert.True(session.StartGame(in start));
        battleDataPlane.AttachBattle(CreateBattleHandle(session, new ScriptedShooterRoomClient()));

        var context = new ShooterGatewayBattleInputContext("session-token", "battle-2", 9010ul, frame: 2, playerId: 21u);
        var command = new ShooterPlayerCommand(21, 1f, 0f, 1f, 0f, false);
        var requestTask = session.SubmitLocalInputToGatewayAsync(context, command);
        Assert.Equal(RoomGatewayOpCodes.SubmitBattleInput, connection.LastSentOpCode);
        Assert.Equal(NetworkPacketFlags.Request, connection.LastSentFlags);
        Assert.True(connection.LastSentSeq > 0);
        var requestWire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(connection.LastSentPayload);
        Assert.Equal("session-token", requestWire.SessionToken);
        Assert.Equal("battle-2", requestWire.BattleId);
        Assert.Equal(9010ul, requestWire.WorldId);
        Assert.Equal(2, requestWire.Frame);
        Assert.Equal(21u, requestWire.PlayerId);
        Assert.Equal(ShooterOpCodes.Input.PlayerCommand, requestWire.InputOpCode);
        Assert.NotNull(requestWire.Payload);

        connection.CompleteResponse(
            connection.LastSentOpCode,
            connection.LastSentSeq,
            new WireSubmitBattleInputRes
            {
                Success = true,
                AcceptedFrame = 3,
                Message = "ok",
                CurrentFrame = 2,
                Status = "RemappedLate",
                ShouldResync = false,
                ServerTicks = 22334455L
            });
        var submitResult = await requestTask;
        Assert.True(submitResult.Remote.Success);
        Assert.Equal(3, submitResult.Remote.AcceptedFrame);
        Assert.Equal("ok", submitResult.Remote.Message);
        Assert.Equal(2, submitResult.Remote.CurrentFrame);
        Assert.Equal("RemappedLate", submitResult.Remote.Status);
        Assert.False(submitResult.Remote.ShouldResync);
        Assert.Equal(22334455L, submitResult.Remote.ServerTicks);

        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        authority.SubmitInput(0, new[] { new ShooterPlayerCommand(21, 0f, 1f, 1f, 0f, true) });
        Assert.True(authority.Tick(1f / 30f));
        var packed = authority.ExportPackedSnapshot(9010ul, isFullSnapshot: true, authorityOverride: true);
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = packed.WorldId,
            Frame = packed.Frame,
            Timestamp = 9010.5,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = ShooterPackedSnapshotCodec.Serialize(in packed)
        };
        var pushPayload = WireRoomGatewayBinary.Serialize(in wire);
        var dispatchedCount = 0;
        var dispatchedResult = ShooterSnapshotApplyResult.Ignored;
        battleDataPlane.SnapshotPushDispatched += (_, _, result) =>
        {
            dispatchedCount++;
            dispatchedResult = result;
        };

        connection.Push(RoomGatewayOpCodes.SnapshotPushed, pushPayload);
        battleDataPlane.Drain();

        Assert.Equal(1, dispatchedCount);
        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, dispatchedResult);
        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, battleDataPlane.LastPushResult);
        Assert.Equal(authority.CurrentFrame, session.CurrentFrame);
        Assert.Equal(authority.ComputeStateHash(), runtime.ComputeStateHash());
        Assert.Equal(authority.CurrentFrame, presentation.ViewModel.Frame);
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Key.Equals(new ShooterViewEntityKey(ShooterViewEntityKind.Player, 21)));
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Key.Equals(new ShooterViewEntityKey(ShooterViewEntityKind.Player, 22)));
    }

    [Fact]
    public void BattleDataPlaneRequestsFullStateSyncWhenPushNeedsPureStateBaseline()
    {
        var source = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "connection-pure-state-session",
            30,
            5902,
            new[]
            {
                new ShooterStartPlayer(21, "P21", 0f, 0f),
                new ShooterStartPlayer(22, "P22", 5f, 0f)
            });
        Assert.True(source.StartGame(in start));
        Assert.True(source.Tick(1f / 30f));
        var delta = source.ExportPureStateSnapshot(9011ul, isFullBaseline: false, baselineFrame: 99, baselineHash: 123u);
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var connection = new FakeGatewayConnection();
        using var gatewayConnection = new ShooterRoomGatewayConnection(connection);
        using var battleTransport = CreateBattleTransport(connection);
        using var battleDataPlane = new ShooterBattleDataPlane(battleTransport);
        var gateway = new ShooterRoomGatewayClient(gatewayConnection);
        var session = new ShooterClientSession(runtime, presentation, tickRate: 30, decoder: null, gateway);
        Assert.True(session.StartGame(in start));
        var roomClient = new ScriptedShooterRoomClient();
        var anchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 0, 1d / 30d);
        var flow = new ShooterRoomGatewayFlowResult(
            "session-token",
            "room-9",
            1009ul,
            "battle-9",
            9011ul,
            21u,
            in anchor,
            223456L,
            ShooterRoomGatewayEntryKind.TeamLobby,
            canStart: true,
            started: true,
            subscribed: true,
            "ready");
        var battle = new ShooterClientBattleHandle(session, flow, roomClient);
        var dispatchedCount = 0;
        var dispatchedResult = ShooterSnapshotApplyResult.Ignored;
        battleDataPlane.SnapshotPushDispatched += (_, _, result) =>
        {
            dispatchedCount++;
            dispatchedResult = result;
        };
        battleDataPlane.AttachBattle(battle);

        connection.Push(
            RoomGatewayOpCodes.DeltaSnapshotPushed,
            CreatePureStateGatewayPayload(in delta, ShooterOpCodes.Snapshot.PureStateDelta, isFullSnapshot: false));
        battleDataPlane.Drain();

        Assert.Equal(1, dispatchedCount);
        Assert.Equal(ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded, dispatchedResult);
        Assert.Equal(ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded, battleDataPlane.LastPushResult);
        Assert.True(presentation.NeedsPureStateFullBaselineResync);
        Assert.Equal("PureStateMissingBaseline", roomClient.LastFullStateSyncRequest.Reason);
        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("request-full-state:")));
    }

    [Fact]
    public void BattleDataPlaneAcknowledgesOnlyNewContiguousReliableEvents()
    {
        var start = CreateStartGamePayload("connection-reliable-events-session");
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var connection = new FakeGatewayConnection();
        using var gatewayConnection = new ShooterRoomGatewayConnection(connection);
        using var battleTransport = CreateBattleTransport(connection);
        using var battleDataPlane = new ShooterBattleDataPlane(battleTransport);
        var session = new ShooterClientSession(runtime, presentation, tickRate: 30);
        Assert.True(session.StartGame(in start));
        var roomClient = new ScriptedShooterRoomClient();
        var battle = CreateBattleHandle(session, roomClient);
        battleDataPlane.AttachBattle(battle);

        var push = CreateReliableEventPush("epoch-1", retentionGap: false, 1L, 2L);
        var payload = WireRoomGatewayBinary.Serialize(in push);
        connection.Push(RoomGatewayOpCodes.ReliableBattleEventsPushed, payload);
        battleDataPlane.Drain();

        Assert.Equal(2L, session.LastReliableEventAck);
        Assert.Equal("epoch-1", session.ReliableEventEpoch);
        Assert.Equal(2L, roomClient.LastReliableBattleEventAckRequest.AckSequence);
        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("ack-reliable-events:")));

        connection.Push(RoomGatewayOpCodes.ReliableBattleEventsPushed, payload);
        battleDataPlane.Drain();

        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("ack-reliable-events:")));
        Assert.DoesNotContain(roomClient.Calls, call => call.StartsWith("request-full-state:"));
    }

    [Fact]
    public void BattleDataPlaneRequestsFullBaselineWhenReliableEventAckRequiresResync()
    {
        var start = CreateStartGamePayload("connection-reliable-ack-failure-session");
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var connection = new FakeGatewayConnection();
        using var gatewayConnection = new ShooterRoomGatewayConnection(connection);
        using var battleTransport = CreateBattleTransport(connection);
        using var battleDataPlane = new ShooterBattleDataPlane(battleTransport);
        var session = new ShooterClientSession(runtime, presentation, tickRate: 30);
        Assert.True(session.StartGame(in start));
        var roomClient = new ScriptedShooterRoomClient
        {
            ReliableBattleEventAckResult = new ShooterGatewayReliableBattleEventAckResult(
                success: false,
                acceptedAckSequence: 1L,
                message: "resync required")
        };
        var battle = CreateBattleHandle(session, roomClient);
        battleDataPlane.AttachBattle(battle);

        var push = CreateReliableEventPush("epoch-1", retentionGap: false, 1L, 2L);
        connection.Push(RoomGatewayOpCodes.ReliableBattleEventsPushed, WireRoomGatewayBinary.Serialize(in push));
        battleDataPlane.Drain();

        Assert.Equal(2L, session.LastReliableEventAck);
        Assert.Equal(2L, roomClient.LastReliableBattleEventAckRequest.AckSequence);
        Assert.Equal("ReliableEventGap", roomClient.LastFullStateSyncRequest.Reason);
        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("ack-reliable-events:")));
        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("request-full-state:")));
    }

    [Fact]
    public void BattleDataPlaneRestoresReliableCursorFromFullSnapshotWatermarkAfterGap()
    {
        var start = CreateStartGamePayload("connection-reliable-gap-session");
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var connection = new FakeGatewayConnection();
        using var gatewayConnection = new ShooterRoomGatewayConnection(connection);
        using var battleTransport = CreateBattleTransport(connection);
        using var battleDataPlane = new ShooterBattleDataPlane(battleTransport);
        var session = new ShooterClientSession(runtime, presentation, tickRate: 30);
        Assert.True(session.StartGame(in start));
        var roomClient = new ScriptedShooterRoomClient();
        var battle = CreateBattleHandle(session, roomClient);
        battleDataPlane.AttachBattle(battle);

        var gap = CreateReliableEventPush("epoch-2", retentionGap: true);
        connection.Push(RoomGatewayOpCodes.ReliableBattleEventsPushed, WireRoomGatewayBinary.Serialize(in gap));
        battleDataPlane.Drain();

        Assert.True(session.NeedsReliableEventResync);
        Assert.Equal("ReliableEventGap", roomClient.LastFullStateSyncRequest.Reason);
        Assert.DoesNotContain(roomClient.Calls, call => call.StartsWith("ack-reliable-events:"));

        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(authority.Tick(1f / 30f));
        var packed = authority.ExportPackedSnapshot(9011ul, isFullSnapshot: true, authorityOverride: true);
        var baseline = new WireStateSyncSnapshotPush
        {
            WorldId = packed.WorldId,
            Frame = packed.Frame,
            Timestamp = 9011.5,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = ShooterPackedSnapshotCodec.Serialize(in packed),
            EventWatermark = 6L
        };
        connection.Push(RoomGatewayOpCodes.SnapshotPushed, WireRoomGatewayBinary.Serialize(in baseline));
        battleDataPlane.Drain();

        Assert.False(session.NeedsReliableEventResync);
        Assert.Equal("epoch-2", session.ReliableEventEpoch);
        Assert.Equal(6L, session.LastReliableEventAck);
        Assert.Equal("epoch-2", roomClient.LastReliableBattleEventAckRequest.Epoch);
        Assert.Equal(6L, roomClient.LastReliableBattleEventAckRequest.AckSequence);
        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("request-full-state:")));
        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("ack-reliable-events:")));
    }

    [Fact]
    public void BattleDataPlaneRestoresReliableCursorFromDuplicateFullSnapshotAfterGap()
    {
        var start = CreateStartGamePayload("connection-reliable-duplicate-baseline-session");
        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(authority.Tick(1f / 30f));
        var packed = authority.ExportPackedSnapshot(9011ul, isFullSnapshot: true, authorityOverride: true);
        var baseline = new WireStateSyncSnapshotPush
        {
            WorldId = packed.WorldId,
            Frame = packed.Frame,
            Timestamp = 9011.5,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = ShooterPackedSnapshotCodec.Serialize(in packed),
            EventWatermark = 6L
        };
        var baselinePayload = WireRoomGatewayBinary.Serialize(in baseline);
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var connection = new FakeGatewayConnection();
        using var gatewayConnection = new ShooterRoomGatewayConnection(connection);
        using var battleTransport = CreateBattleTransport(connection);
        using var battleDataPlane = new ShooterBattleDataPlane(battleTransport);
        var session = new ShooterClientSession(runtime, presentation, tickRate: 30);
        Assert.True(session.StartGame(in start));
        var roomClient = new ScriptedShooterRoomClient();
        var battle = CreateBattleHandle(session, roomClient);
        battleDataPlane.AttachBattle(battle);

        connection.Push(RoomGatewayOpCodes.SnapshotPushed, baselinePayload);
        battleDataPlane.Drain();
        var gap = CreateReliableEventPush("epoch-2", retentionGap: true);
        connection.Push(
            RoomGatewayOpCodes.ReliableBattleEventsPushed,
            WireRoomGatewayBinary.Serialize(in gap));
        battleDataPlane.Drain();

        Assert.True(session.NeedsReliableEventResync);
        Assert.Equal("ReliableEventGap", roomClient.LastFullStateSyncRequest.Reason);

        connection.Push(RoomGatewayOpCodes.SnapshotPushed, baselinePayload);
        battleDataPlane.Drain();

        Assert.Equal(ShooterSnapshotApplyResult.IgnoredStaleSnapshot, battleDataPlane.LastPushResult);
        Assert.False(session.NeedsReliableEventResync);
        Assert.Equal("epoch-2", session.ReliableEventEpoch);
        Assert.Equal(6L, session.LastReliableEventAck);
        Assert.Equal(6L, roomClient.LastReliableBattleEventAckRequest.AckSequence);
        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("request-full-state:")));
        Assert.Equal(1, roomClient.Calls.Count(call => call.StartsWith("ack-reliable-events:")));
    }

    private static ShooterStartGamePayload CreateStartGamePayload(string sessionId)
    {
        return new ShooterStartGamePayload(
            sessionId,
            30,
            5903,
            new[]
            {
                new ShooterStartPlayer(21, "P21", 0f, 0f),
                new ShooterStartPlayer(22, "P22", 5f, 0f)
            });
    }

    private static NetworkTransport CreateBattleTransport(FakeGatewayConnection connection)
    {
        return new NetworkTransport(
            new NetworkTransportOptions
            {
                ConnectionFactory = () => connection,
                OpSnapshotPushed = RoomGatewayOpCodes.SnapshotPushed,
                OpDeltaSnapshotPushed = RoomGatewayOpCodes.DeltaSnapshotPushed,
                OpReliableEventsPushed = RoomGatewayOpCodes.ReliableBattleEventsPushed
            },
            InlineDispatcher.Instance);
    }

    private static ShooterClientBattleHandle CreateBattleHandle(ShooterClientSession session, ScriptedShooterRoomClient roomClient)
    {
        var anchor = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 0, 1d / 30d);
        var flow = new ShooterRoomGatewayFlowResult(
            "session-token",
            "room-9",
            1009ul,
            "battle-9",
            9011ul,
            21u,
            in anchor,
            223456L,
            ShooterRoomGatewayEntryKind.TeamLobby,
            canStart: true,
            started: true,
            subscribed: true,
            "ready");
        return new ShooterClientBattleHandle(session, flow, roomClient);
    }

    private static WireReliableBattleEventPush CreateReliableEventPush(string epoch, bool retentionGap, params long[] sequences)
    {
        return new WireReliableBattleEventPush
        {
            BattleId = "battle-9",
            Epoch = epoch,
            FirstAvailableSequence = sequences.Length == 0 ? 1L : sequences.Min(),
            Watermark = sequences.Length == 0 ? 6L : sequences.Max(),
            RetentionGap = retentionGap,
            Events = sequences.Select(sequence =>
            {
                var battleEvent = new ShooterEventSnapshot(ShooterEventType.Fire, 21, 0, checked((int)sequence), 1f, 2f, 0);
                return new WireReliableBattleEvent
                {
                    EventId = $"battle-9:{epoch}:{sequence}",
                    BattleId = "battle-9",
                    Epoch = epoch,
                    Sequence = sequence,
                    SourceFrame = checked((int)sequence),
                    EventType = (int)ShooterEventType.Fire,
                    Payload = ShooterStateSnapshotCodec.SerializeEvent(in battleEvent)
                };
            }).ToList()
        };
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
