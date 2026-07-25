using System.Collections.Generic;
using System.Linq;
using AbilityKit.Demo.Shooter.View;
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
