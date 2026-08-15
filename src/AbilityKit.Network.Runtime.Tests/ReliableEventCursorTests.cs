using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class ReliableEventCursorTests
{
    [Fact]
    public void StrictPolicy_RejectsSequenceGapWithoutAdvancingDelivery()
    {
        var cursor = CreateCursor(ReliableEventGapPolicy.Reject);
        Assert.True(cursor.TryRestore(new ReliableEventCheckpoint("battle-1", "epoch-1", 1)));

        var result = cursor.Admit(Batch(Event(3)));

        Assert.Equal(ReliableEventBatchStatus.SequenceGap, result.Status);
        Assert.Equal(2, result.ExpectedSequence);
        Assert.Equal(3, result.ReceivedSequence);
        Assert.Equal(1, cursor.LastDeliveredSequence);
    }

    [Fact]
    public void BufferedPolicy_DeliversBufferedTailWhenGapIsFilled()
    {
        var cursor = CreateCursor(ReliableEventGapPolicy.BufferWithinCapacity);
        Assert.True(cursor.TryRestore(new ReliableEventCheckpoint("battle-1", "epoch-1", 0)));

        var first = cursor.Admit(Batch(Event(2)));
        var second = cursor.Admit(Batch(Event(1), Event(2), Event(3)));

        Assert.Equal(ReliableEventBatchStatus.DuplicateOnly, first.Status);
        Assert.Equal(new long[] { 1, 2, 3 }, second.Events.Select(item => item.Sequence));
        Assert.True(cursor.CommitDelivered(second.TimelineId, second.CommitSequence));
        Assert.True(cursor.ConfirmAcknowledged(second.TimelineId, second.CommitSequence));
        Assert.Equal(3, cursor.LastAcknowledgedSequence);
    }

    [Fact]
    public void BindTimelineOnAdmission_RejectsNewTimelineWhileEventsAreBuffered()
    {
        var cursor = CreateCursor(
            ReliableEventGapPolicy.BufferWithinCapacity,
            bindTimelineOnAdmission: true);

        Assert.Equal(ReliableEventBatchStatus.DuplicateOnly, cursor.Admit(Batch(Event(2))).Status);
        var changed = new ReliableEventBatch<TestEvent>(
            "battle-1",
            "epoch-2",
            1,
            1,
            false,
            new[] { new TestEvent("event-1", "battle-1", "epoch-2", 1) });

        Assert.Equal(ReliableEventBatchStatus.TimelineChanged, cursor.Admit(changed).Status);
        Assert.Equal("epoch-1", cursor.TimelineId);
    }

    [Fact]
    public void BufferedPolicy_RejectsCapacityOverflow()
    {
        var cursor = CreateCursor(ReliableEventGapPolicy.BufferWithinCapacity, maxPendingEvents: 1);
        Assert.True(cursor.TryRestore(new ReliableEventCheckpoint("battle-1", "epoch-1", 0)));

        var result = cursor.Admit(Batch(Event(2), Event(3)));

        Assert.Equal(ReliableEventBatchStatus.CapacityExceeded, result.Status);
        Assert.True(result.ShouldRequestFullResync);
    }

    [Fact]
    public void CommitAndAcknowledgement_AreIndependentAndOlderAckIsIdempotent()
    {
        var cursor = CreateCursor(ReliableEventGapPolicy.Reject);
        var result = cursor.Admit(Batch(Event(1), Event(2), Event(3)));

        Assert.Equal(0, cursor.LastDeliveredSequence);
        Assert.True(cursor.CommitDelivered(result.TimelineId, result.CommitSequence));
        Assert.Equal(3, cursor.LastDeliveredSequence);
        Assert.Equal(0, cursor.LastAcknowledgedSequence);
        Assert.True(cursor.ConfirmAcknowledged("epoch-1", 3));
        Assert.True(cursor.ConfirmAcknowledged("epoch-1", 1));
        Assert.Equal(3, cursor.LastAcknowledgedSequence);
    }

    [Fact]
    public void ConfirmWatermarkPolicy_AdoptsSnapshotWatermarkAsAcknowledged()
    {
        var cursor = CreateCursor(
            ReliableEventGapPolicy.BufferWithinCapacity,
            baselinePolicy: ReliableEventBaselineAcknowledgementPolicy.ConfirmWatermark);

        Assert.True(cursor.AdoptAuthoritativeBaseline("epoch-2", 8));

        Assert.Equal("epoch-2", cursor.TimelineId);
        Assert.Equal(8, cursor.LastDeliveredSequence);
        Assert.Equal(8, cursor.LastAcknowledgedSequence);
    }

    [Fact]
    public void PreservePolicy_NewTimelineClearsAckAndSameTimelineClampsIt()
    {
        var cursor = CreateCursor(ReliableEventGapPolicy.Reject);
        Assert.True(cursor.TryRestore(new ReliableEventCheckpoint("battle-1", "epoch-1", 3)));
        Assert.True(cursor.AdoptAuthoritativeBaseline("epoch-1", 2));
        Assert.Equal(2, cursor.LastAcknowledgedSequence);

        Assert.True(cursor.AdoptAuthoritativeBaseline("epoch-2", 8));
        Assert.Equal(0, cursor.LastAcknowledgedSequence);
    }

    [Fact]
    public void BaselineWatermarkGuard_RejectsOlderSameTimelineSnapshot()
    {
        var cursor = CreateCursor(
            ReliableEventGapPolicy.BufferWithinCapacity,
            requireBaselineAtObservedWatermark: true);
        Assert.True(cursor.TryRestore(new ReliableEventCheckpoint("battle-1", "epoch-1", 2)));
        var gap = new ReliableEventBatch<TestEvent>("battle-1", "epoch-1", 6, 8, true, new[] { Event(6) });
        Assert.Equal(ReliableEventBatchStatus.RetentionGap, cursor.Admit(gap).Status);

        Assert.False(cursor.AdoptAuthoritativeBaseline("epoch-1", 5));
        Assert.True(cursor.AdoptAuthoritativeBaseline("epoch-1", 8));
    }

    [Fact]
    public void Checkpoint_RestoresOnlyMatchingStream()
    {
        var cursor = CreateCursor(ReliableEventGapPolicy.Reject);

        Assert.False(cursor.TryRestore(new ReliableEventCheckpoint("battle-2", "epoch-1", 4)));
        Assert.True(cursor.TryRestore(new ReliableEventCheckpoint("battle-1", "epoch-1", 4)));
        Assert.Equal(4, cursor.LastDeliveredSequence);
    }

    private static ReliableEventCursor<TestEvent> CreateCursor(
        ReliableEventGapPolicy gapPolicy,
        int maxPendingEvents = 512,
        ReliableEventBaselineAcknowledgementPolicy baselinePolicy =
            ReliableEventBaselineAcknowledgementPolicy.PreserveConfirmedWithinWatermark,
        bool requireBaselineAtObservedWatermark = false,
        bool bindTimelineOnAdmission = false)
    {
        return new ReliableEventCursor<TestEvent>(
            "battle-1",
            new ReliableEventDescriptor<TestEvent>(
                item => item.BattleId,
                item => item.Epoch,
                item => item.Sequence,
                item => !string.IsNullOrWhiteSpace(item.EventId)),
            new ReliableEventCursorOptions
            {
                GapPolicy = gapPolicy,
                MaxPendingEvents = maxPendingEvents,
                BaselineAcknowledgementPolicy = baselinePolicy,
                RequireBaselineAtObservedWatermark = requireBaselineAtObservedWatermark,
                InferRetentionGapFromFirstAvailableSequence = requireBaselineAtObservedWatermark,
                BindTimelineOnAdmission = bindTimelineOnAdmission
            });
    }

    private static ReliableEventBatch<TestEvent> Batch(params TestEvent[] events)
    {
        return new ReliableEventBatch<TestEvent>(
            "battle-1",
            "epoch-1",
            events.Length == 0 ? 0 : events.Min(item => item.Sequence),
            events.Length == 0 ? 0 : events.Max(item => item.Sequence),
            false,
            events);
    }

    private static TestEvent Event(long sequence)
    {
        return new TestEvent("event-" + sequence, "battle-1", "epoch-1", sequence);
    }

    private readonly record struct TestEvent(string EventId, string BattleId, string Epoch, long Sequence);
}
