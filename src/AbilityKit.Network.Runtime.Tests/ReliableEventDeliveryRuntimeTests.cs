using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class ReliableEventDeliveryRuntimeTests
{
    [Fact]
    public void InvalidAcknowledgementStrategyFailsFast()
    {
        var options = new ReliableEventDeliveryOptions
        {
            AcknowledgementStrategy = (ReliableEventAcknowledgementStrategy)999
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReliableEventDeliveryRuntime<TestEvent>(options));
    }

    [Fact]
    public void EventSinkFailureDoesNotCommitOrConfirm()
    {
        var cursor = CreateCursor();
        var failures = new List<ReliableEventDeliveryFailure>();
        using var runtime = CreateExternalRuntime(invalidateOnSinkFailure: true);
        runtime.BeginGeneration(
            cursor,
            _ => throw new InvalidOperationException("sink failed"),
            failures.Add,
            failureObserved: failures.Add,
            awaitAuthoritativeBaseline: false);
        var batch = Batch(Event(1));

        runtime.Handle(in batch);

        Assert.Equal(0L, cursor.LastDeliveredSequence);
        Assert.Equal(0L, cursor.LastAcknowledgedSequence);
        Assert.True(runtime.AwaitingBaseline);
        Assert.Contains(failures, item => item.Kind == ReliableEventDeliveryFailureKind.EventSinkFailed);
    }

    [Fact]
    public async Task AutomaticAcknowledgementRetriesAndPersistsOnlyAfterSuccess()
    {
        var cursor = CreateCursor();
        var attempts = 0;
        var checkpoints = new List<ReliableEventCheckpoint>();
        using var runtime = new ReliableEventDeliveryRuntime<TestEvent>(
            new ReliableEventDeliveryOptions
            {
                AcknowledgementStrategy = ReliableEventAcknowledgementStrategy.Automatic,
                MaxAcknowledgementAttempts = 3,
                AcknowledgementRetryDelay = _ => Task.CompletedTask
            });
        runtime.BeginGeneration(
            cursor,
            _ => { },
            _ => { },
            (_, sequence) =>
            {
                attempts++;
                return attempts < 3
                    ? Task.FromException<long>(new InvalidOperationException("ack failed"))
                    : Task.FromResult(sequence);
            },
            checkpoints.Add,
            awaitAuthoritativeBaseline: false);
        var batch = Batch(Event(1));

        runtime.Handle(in batch);
        await WaitUntilAsync(() => cursor.LastAcknowledgedSequence == 1L);

        Assert.Equal(3, attempts);
        Assert.Single(checkpoints);
        Assert.Equal(1L, checkpoints[0].LastAcknowledgedSequence);
    }

    [Fact]
    public async Task ReplacedGenerationIgnoresStaleAcknowledgementCompletion()
    {
        var staleCursor = CreateCursor();
        var staleAck = new TaskCompletionSource<long>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var staleCheckpoints = new List<ReliableEventCheckpoint>();
        using var runtime = new ReliableEventDeliveryRuntime<TestEvent>(
            new ReliableEventDeliveryOptions
            {
                AcknowledgementStrategy = ReliableEventAcknowledgementStrategy.Automatic,
                AcknowledgementRetryDelay = _ => Task.CompletedTask
            });
        runtime.BeginGeneration(
            staleCursor,
            _ => { },
            _ => { },
            (_, _) => staleAck.Task,
            staleCheckpoints.Add,
            awaitAuthoritativeBaseline: false);
        var batch = Batch(Event(1));
        runtime.Handle(in batch);

        var activeCursor = CreateCursor();
        runtime.BeginGeneration(
            activeCursor,
            _ => { },
            _ => { },
            (_, sequence) => Task.FromResult(sequence),
            awaitAuthoritativeBaseline: false);
        staleAck.SetResult(1L);
        await Task.Delay(25);

        Assert.Equal(0L, staleCursor.LastAcknowledgedSequence);
        Assert.Empty(staleCheckpoints);
        Assert.Equal(0L, activeCursor.LastAcknowledgedSequence);
    }

    [Fact]
    public void PendingBatchesReplayAfterMatchingBaseline()
    {
        var cursor = CreateCursor();
        var delivered = new List<long>();
        using var runtime = CreateExternalRuntime();
        runtime.BeginGeneration(cursor, item => delivered.Add(item.Sequence), _ => { });
        var first = Batch(Event(6));
        var second = Batch(Event(7));

        runtime.Handle(in first);
        runtime.Handle(in second);
        Assert.Empty(delivered);

        Assert.True(runtime.AdoptAuthoritativeBaseline(TimelineId, 5L));

        Assert.Equal(new[] { 6L, 7L }, delivered);
        Assert.Equal(7L, cursor.LastDeliveredSequence);
        Assert.Equal(7L, cursor.LastAcknowledgedSequence);
        Assert.Equal(0, runtime.PendingBatchCount);
    }

    [Fact]
    public void ExternalAcknowledgementAdvancesOnlyAfterSinkSucceeds()
    {
        var cursor = CreateCursor();
        using var runtime = CreateExternalRuntime();
        runtime.BeginGeneration(cursor, _ => { }, _ => { }, awaitAuthoritativeBaseline: false);
        var batch = Batch(Event(1));

        runtime.Handle(in batch);

        Assert.Equal(1L, cursor.LastDeliveredSequence);
        Assert.Equal(1L, cursor.LastAcknowledgedSequence);
    }

    [Fact]
    public async Task OlderIncompleteAcknowledgementDoesNotInvalidateAfterNewerSuccess()
    {
        var cursor = CreateCursor();
        var firstAck = new TaskCompletionSource<long>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var invalidations = new List<ReliableEventDeliveryFailure>();
        using var runtime = new ReliableEventDeliveryRuntime<TestEvent>(
            new ReliableEventDeliveryOptions
            {
                AcknowledgementStrategy = ReliableEventAcknowledgementStrategy.Automatic,
                MaxAcknowledgementAttempts = 1
            });
        runtime.BeginGeneration(
            cursor,
            _ => { },
            invalidations.Add,
            (_, sequence) => sequence == 1L
                ? firstAck.Task
                : Task.FromResult(sequence),
            awaitAuthoritativeBaseline: false);
        var first = Batch(Event(1));
        var second = Batch(Event(2));

        runtime.Handle(in first);
        runtime.Handle(in second);
        await WaitUntilAsync(() => cursor.LastAcknowledgedSequence == 2L);
        firstAck.SetResult(0L);
        await Task.Delay(25);

        Assert.Empty(invalidations);
        Assert.False(runtime.AwaitingBaseline);
        Assert.Equal(2L, cursor.LastAcknowledgedSequence);
    }

    [Fact]
    public void ReentrantGenerationChangeDoesNotQueueRejectedOldBatch()
    {
        var staleCursor = CreateCursor();
        var activeCursor = CreateCursor();
        using var runtime = CreateExternalRuntime();
        runtime.BeginGeneration(
            staleCursor,
            _ => { },
            _ => runtime.BeginGeneration(
                activeCursor,
                _ => { },
                _ => { },
                awaitAuthoritativeBaseline: false),
            awaitAuthoritativeBaseline: false);
        var rejected = new ReliableEventBatch<TestEvent>(
            StreamId,
            TimelineId,
            2L,
            2L,
            retentionGap: true,
            new[] { Event(2) });

        runtime.Handle(in rejected);

        Assert.Equal(0, runtime.PendingBatchCount);
        Assert.False(runtime.AwaitingBaseline);
        Assert.Equal(0L, activeCursor.LastDeliveredSequence);
    }

    private static ReliableEventDeliveryRuntime<TestEvent> CreateExternalRuntime(
        bool invalidateOnSinkFailure = false)
    {
        return new ReliableEventDeliveryRuntime<TestEvent>(
            new ReliableEventDeliveryOptions
            {
                AcknowledgementStrategy = ReliableEventAcknowledgementStrategy.External,
                InvalidateOnEventSinkFailure = invalidateOnSinkFailure
            });
    }

    private static ReliableEventCursor<TestEvent> CreateCursor()
    {
        return new ReliableEventCursor<TestEvent>(
            StreamId,
            new ReliableEventDescriptor<TestEvent>(
                item => item.StreamId,
                item => item.TimelineId,
                item => item.Sequence),
            new ReliableEventCursorOptions
            {
                BaselineAcknowledgementPolicy =
                    ReliableEventBaselineAcknowledgementPolicy.PreserveConfirmedWithinWatermark
            });
    }

    private static ReliableEventBatch<TestEvent> Batch(params TestEvent[] events)
    {
        return new ReliableEventBatch<TestEvent>(
            StreamId,
            TimelineId,
            events.Length == 0 ? 0L : events[0].Sequence,
            events.Length == 0 ? 0L : events[^1].Sequence,
            retentionGap: false,
            events);
    }

    private static TestEvent Event(long sequence)
    {
        return new TestEvent(StreamId, TimelineId, sequence);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var i = 0; i < 100; i++)
        {
            if (predicate()) return;
            await Task.Delay(10);
        }

        Assert.Fail("Condition was not satisfied before timeout.");
    }

    private readonly record struct TestEvent(
        string StreamId,
        string TimelineId,
        long Sequence);

    private const string StreamId = "battle-1";
    private const string TimelineId = "epoch-1";
}
