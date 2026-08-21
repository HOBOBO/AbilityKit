using System.Collections.Generic;
using System.Linq;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterReliableBattleEventConsumerTests
{
    [Fact]
    public void ConsumeBuffersOutOfOrderEventsAndCommitsExactlyOnceInSequence()
    {
        var consumer = new ShooterReliableBattleEventConsumer();

        var first = consumer.Consume(CreatePush(3L, Event(2L)));
        Assert.Empty(first.CommittedEvents);
        Assert.Equal(0L, first.AcknowledgedSequence);
        Assert.False(first.ShouldAcknowledge);

        var second = consumer.Consume(CreatePush(3L, Event(1L), Event(2L), Event(3L)));
        Assert.Equal(new[] { 1L, 2L, 3L }, second.CommittedEvents.Select(item => item.Sequence));
        Assert.Equal(3L, second.AcknowledgedSequence);
        Assert.True(second.ShouldAcknowledge);

        var duplicate = consumer.Consume(CreatePush(3L, Event(1L), Event(3L)));
        Assert.Empty(duplicate.CommittedEvents);
        Assert.Equal(3L, duplicate.AcknowledgedSequence);
        Assert.False(duplicate.ShouldAcknowledge);
    }

    [Fact]
    public void ConsumeAndDispatchDoesNotMaterializeCommittedEventList()
    {
        var consumer = new ShooterReliableBattleEventConsumer();
        var delivered = new List<long>();
        var push = CreatePush(2L, Event(1L), Event(2L));

        var result = consumer.ConsumeAndDispatch(in push, item => delivered.Add(item.Sequence));

        Assert.Equal(new[] { 1L, 2L }, delivered);
        Assert.Empty(result.CommittedEvents);
        Assert.Equal(2L, result.AcknowledgedSequence);
        Assert.False(result.RequiresResync);
        Assert.True(result.HasCommittedEvents);
        Assert.True(result.ShouldAcknowledge);
    }

    [Fact]
    public void GatewaySnapshotMapperPreservesEventWatermark()
    {
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = 7UL,
            Frame = 42,
            Timestamp = 1.5d,
            ServerTicks = 123L,
            EventWatermark = 19L,
            IsFullSnapshot = true,
            Actors = new List<WireStateSyncActorSnapshot>()
        };

        var snapshot = ShooterGatewaySnapshotMapper.ToGatewaySnapshot(in wire);

        Assert.Equal(19L, snapshot.EventWatermark);
        Assert.True(snapshot.IsFullSnapshot);
    }

    [Fact]
    public void ReliableEventPushDecodeBufferPreservesEnvelopeAndEvents()
    {
        var wire = CreatePush(2L, Event(1L), Event(2L));
        var payload = WireRoomGatewayBinary.Serialize(in wire);
        var decoder = new WireReliableBattleEventPushDecodeBuffer();

        var decoded = decoder.Decode(payload);

        Assert.Equal(BattleId, decoded.BattleId);
        Assert.Equal(Epoch, decoded.Epoch);
        Assert.Equal(2L, decoded.Watermark);
        Assert.NotNull(decoded.Events);
        Assert.Equal(new[] { 1L, 2L }, decoded.Events!.Select(item => item.Sequence));
    }

    [Fact]
    public void NegotiatedSessionPersistsCheckpointAfterCommittedEvents()
    {
        var clientSession = new ShooterClientSession(
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade(),
            tickRate: 30);
        var saved = new List<ReliableEventCheckpoint>();
        var consumer = new ShooterReliableBattleEventConsumer(
            clientSession.SyncSession,
            saveCheckpoint: saved.Add);

        var result = consumer.Consume(CreatePush(2L, Event(1L), Event(2L)));

        Assert.False(result.RequiresResync);
        Assert.Equal(2L, result.AcknowledgedSequence);
        var checkpoint = Assert.IsType<ReliableEventCheckpoint>(consumer.LatestCheckpoint);
        Assert.Equal(BattleId, checkpoint.StreamId);
        Assert.Equal(Epoch, checkpoint.TimelineId);
        Assert.Equal(2L, checkpoint.LastAcknowledgedSequence);
        Assert.Contains(saved, item => item.Equals(checkpoint));
    }

    [Fact]
    public void SharedCheckpointStoreRestoresNewConsumerWithoutManualCursorRestore()
    {
        var clientSession = new ShooterClientSession(
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade(),
            tickRate: 30);
        var store = new InMemoryReliableEventCheckpointStore();
        var first = new ShooterReliableBattleEventConsumer(
            clientSession.SyncSession,
            checkpointStore: store);
        first.Consume(CreatePush(2L, Event(1L), Event(2L)));

        var resumed = new ShooterReliableBattleEventConsumer(
            clientSession.SyncSession,
            checkpointStore: store);
        var result = resumed.Consume(CreatePush(3L, Event(3L)));

        Assert.False(result.RequiresResync);
        Assert.Single(result.CommittedEvents);
        Assert.Equal(3L, result.AcknowledgedSequence);
    }

    [Fact]
    public void RetentionGapRequiresBaselineAndFullSnapshotWatermarkRestoresCursor()
    {
        var consumer = new ShooterReliableBattleEventConsumer();
        consumer.RestoreCursor(BattleId, Epoch, 2L);

        var gap = consumer.Consume(new WireReliableBattleEventPush
        {
            BattleId = BattleId,
            Epoch = Epoch,
            FirstAvailableSequence = 6L,
            Watermark = 8L,
            RetentionGap = true,
            Events = new List<WireReliableBattleEvent> { Event(6L) }
        });

        Assert.True(gap.RequiresResync);
        Assert.True(consumer.RequiresResync);
        Assert.Equal(2L, consumer.LastAcknowledgedSequence);
        Assert.True(consumer.TryApplyFullSnapshotBaseline(8L));
        Assert.False(consumer.RequiresResync);
        Assert.Equal(8L, consumer.LastAcknowledgedSequence);

        var replay = consumer.Consume(CreatePush(9L, Event(9L)));
        Assert.Single(replay.CommittedEvents);
        Assert.Equal(9L, replay.AcknowledgedSequence);
    }

    [Fact]
    public void OlderFullSnapshotWatermarkDoesNotClearGapOrMoveAcknowledgementBackward()
    {
        var consumer = new ShooterReliableBattleEventConsumer();
        consumer.RestoreCursor(BattleId, Epoch, 8L);
        consumer.Invalidate();

        Assert.False(consumer.TryApplyFullSnapshotBaseline(5L));
        Assert.True(consumer.RequiresResync);
        Assert.Equal(8L, consumer.LastAcknowledgedSequence);
    }

    [Fact]
    public void EpochChangeRequiresBaselineAndAdoptsNewEpochOnlyAfterBaseline()
    {
        var consumer = new ShooterReliableBattleEventConsumer();
        consumer.RestoreCursor(BattleId, Epoch, 4L);

        var mismatch = consumer.Consume(new WireReliableBattleEventPush
        {
            BattleId = BattleId,
            Epoch = "epoch-2",
            FirstAvailableSequence = 1L,
            Watermark = 5L,
            Events = new List<WireReliableBattleEvent>()
        });

        Assert.True(mismatch.RequiresResync);
        Assert.Equal(Epoch, consumer.Epoch);
        Assert.True(consumer.TryApplyFullSnapshotBaseline(5L));
        Assert.Equal("epoch-2", consumer.Epoch);
        Assert.Equal(5L, consumer.LastAcknowledgedSequence);
    }

    [Fact]
    public void PendingCapacityOverflowRequiresResyncInsteadOfDroppingEvents()
    {
        var consumer = new ShooterReliableBattleEventConsumer(maxPendingEvents: 1);

        var result = consumer.Consume(CreatePush(3L, Event(2L), Event(3L)));

        Assert.True(result.RequiresResync);
        Assert.True(consumer.RequiresResync);
        Assert.Equal(0L, consumer.LastAcknowledgedSequence);
    }

    [Fact]
    public void SinkFailureRequiresBaselineWithoutAdvancingAcknowledgement()
    {
        var consumer = new ShooterReliableBattleEventConsumer();
        var push = CreatePush(1L, Event(1L));

        var result = consumer.Consume(
            in push,
            _ => throw new InvalidOperationException("sink failed"));

        Assert.True(result.RequiresResync);
        Assert.True(consumer.RequiresResync);
        Assert.Equal(0L, consumer.LastAcknowledgedSequence);
        Assert.Empty(result.CommittedEvents);
    }

    [Fact]
    public async Task CheckpointLifecyclePublishesFailureAndPreservesTriggerDiagnostics()
    {
        var expected = new InvalidOperationException("flush failed");
        var store = new DelegatingReliableEventCheckpointStore(
            _ => null,
            _ => { },
            _ => false,
            _ => Task.FromException(expected));
        var clientSession = new ShooterClientSession(
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade(),
            tickRate: 30);
        var consumer = new ShooterReliableBattleEventConsumer(
            clientSession.SyncSession,
            checkpointStore: store);
        ReliableEventCheckpointLifecycleFailure? publishedFailure = null;
        consumer.CheckpointLifecycleFailure += failure => publishedFailure = failure;

        var result = await consumer.FlushCheckpointStoreAsync(
            ReliableEventCheckpointFlushTrigger.ApplicationPause);
        var diagnostics = consumer.CheckpointLifecycleDiagnostics;

        Assert.Equal(ReliableEventCheckpointFlushStatus.Failed, result.Status);
        Assert.Equal(ReliableEventCheckpointFlushTrigger.ApplicationPause, result.Trigger);
        Assert.Same(expected, result.Failure);
        Assert.True(publishedFailure.HasValue);
        Assert.Equal(ReliableEventCheckpointFlushTrigger.ApplicationPause, publishedFailure.Value.Trigger);
        Assert.Equal(1, diagnostics.AttemptCount);
        Assert.Equal(1, diagnostics.FailureCount);
        Assert.Equal(ReliableEventCheckpointFlushTrigger.ApplicationPause, diagnostics.LastTrigger);
        Assert.Same(expected, diagnostics.LastFailure);
    }

    private static WireReliableBattleEventPush CreatePush(long watermark, params WireReliableBattleEvent[] events)
    {
        return new WireReliableBattleEventPush
        {
            BattleId = BattleId,
            Epoch = Epoch,
            FirstAvailableSequence = 1L,
            Watermark = watermark,
            Events = events.ToList()
        };
    }

    private static WireReliableBattleEvent Event(long sequence)
    {
        return new WireReliableBattleEvent
        {
            EventId = $"{BattleId}:{Epoch}:{sequence}",
            BattleId = BattleId,
            Epoch = Epoch,
            Sequence = sequence,
            SourceFrame = checked((int)sequence),
            EventType = 1,
            Payload = new byte[] { checked((byte)sequence) }
        };
    }

    private const string BattleId = "battle-1";
    private const string Epoch = "epoch-1";
}
