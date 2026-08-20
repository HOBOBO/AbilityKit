using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Grains.Battle;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class StateSyncObserverDrainPolicyTests
{
    [Fact]
    public void TryDequeueAndDetectBaselineInvalidation_WhenDeltaExpiresAndCriticalDequeues_ReportsBoth()
    {
        var queue = CreateQueue(TimeSpan.FromTicks(10));
        var staleDelta = CreateItem(
            value: 1,
            frame: 10,
            priority: SnapshotDeliveryPriority.Delta,
            replaceable: true,
            producedAtTicks: 0);
        var critical = CreateItem(
            value: 2,
            frame: 11,
            priority: SnapshotDeliveryPriority.Critical,
            replaceable: false,
            producedAtTicks: 9);
        queue.Enqueue(in staleDelta, nowTicks: 0);
        queue.Enqueue(in critical, nowTicks: 9);

        var dequeued = StateSyncObserverGrain.TryDequeueAndDetectBaselineInvalidation(
            queue,
            nowTicks: 11,
            out var item,
            out var baselineInvalidated);

        Assert.True(dequeued);
        Assert.True(baselineInvalidated);
        Assert.Equal(2, item.Value);
        Assert.Equal(1, queue.CreateMetrics(nowTicks: 11).ResyncCount);
    }

    [Fact]
    public void ReliableEventQueue_WhenSnapshotBaselineInvalidates_RemainsIndependentAndFifo()
    {
        var snapshotQueue = CreateQueue(TimeSpan.FromTicks(10));
        var staleDelta = CreateItem(
            value: 1,
            frame: 10,
            priority: SnapshotDeliveryPriority.Delta,
            replaceable: true,
            producedAtTicks: 0);
        snapshotQueue.Enqueue(in staleDelta, nowTicks: 0);
        var reliableQueue = new Queue<int>(new[] { 101, 102 });

        var snapshotDequeued = StateSyncObserverGrain.TryDequeueAndDetectBaselineInvalidation(
            snapshotQueue,
            nowTicks: 11,
            out _,
            out var baselineInvalidated);
        var reliableAvailable = StateSyncObserverGrain.TryPeekReliableEvent(
            reliableQueue,
            out var firstReliable);

        Assert.False(snapshotDequeued);
        Assert.True(baselineInvalidated);
        Assert.True(reliableAvailable);
        Assert.Equal(101, firstReliable);
        StateSyncObserverGrain.CompleteReliableEventDelivery(reliableQueue);
        Assert.True(StateSyncObserverGrain.TryPeekReliableEvent(reliableQueue, out var secondReliable));
        Assert.Equal(102, secondReliable);
    }

    [Fact]
    public void EnqueueBoundedReliableEvent_BelowCapacity_PreservesFifo()
    {
        var queue = new Queue<ReliableQueueItem>();

        StateSyncObserverGrain.EnqueueBoundedReliableEvent(
            queue,
            new ReliableQueueItem(101, IsGap: false),
            capacity: 2,
            () => new ReliableQueueItem(0, IsGap: true),
            static item => item.IsGap);
        StateSyncObserverGrain.EnqueueBoundedReliableEvent(
            queue,
            new ReliableQueueItem(102, IsGap: false),
            capacity: 2,
            () => new ReliableQueueItem(0, IsGap: true),
            static item => item.IsGap);

        Assert.Equal(new[] { 101, 102 }, queue.Select(item => item.Value));
    }

    [Fact]
    public void EnqueueBoundedReliableEvent_WhenCapacityExceeded_CollapsesToGap()
    {
        var queue = new Queue<ReliableQueueItem>(new[]
        {
            new ReliableQueueItem(101, IsGap: false),
            new ReliableQueueItem(102, IsGap: false)
        });

        StateSyncObserverGrain.EnqueueBoundedReliableEvent(
            queue,
            new ReliableQueueItem(103, IsGap: false),
            capacity: 2,
            () => new ReliableQueueItem(103, IsGap: true),
            static item => item.IsGap);

        var gap = Assert.Single(queue);
        Assert.True(gap.IsGap);
        Assert.Equal(103, gap.Value);
    }

    [Fact]
    public void EnqueueBoundedReliableEvent_WhileGapPending_DoesNotRegrowQueue()
    {
        var queue = new Queue<ReliableQueueItem>(new[]
        {
            new ReliableQueueItem(103, IsGap: true)
        });

        StateSyncObserverGrain.EnqueueBoundedReliableEvent(
            queue,
            new ReliableQueueItem(104, IsGap: false),
            capacity: 2,
            () => new ReliableQueueItem(104, IsGap: true),
            static item => item.IsGap);

        var gap = Assert.Single(queue);
        Assert.True(gap.IsGap);
        Assert.Equal(103, gap.Value);
    }

    [Fact]
    public void TryDequeueAndDetectBaselineInvalidation_WhenItemIsValid_DoesNotReportInvalidation()
    {
        var queue = CreateQueue(TimeSpan.FromTicks(10));
        var delta = CreateItem(
            value: 1,
            frame: 10,
            priority: SnapshotDeliveryPriority.Delta,
            replaceable: true,
            producedAtTicks: 15);
        queue.Enqueue(in delta, nowTicks: 15);

        var dequeued = StateSyncObserverGrain.TryDequeueAndDetectBaselineInvalidation(
            queue,
            nowTicks: 20,
            out var item,
            out var baselineInvalidated);

        Assert.True(dequeued);
        Assert.False(baselineInvalidated);
        Assert.Equal(1, item.Value);
        Assert.Equal(0, queue.CreateMetrics(nowTicks: 20).ResyncCount);
    }

    [Fact]
    public void OversizedSnapshot_DoesNotCreateDebtBeyondBurstBudget()
    {
        const int bytesPerSecond = 16 * 1024;
        const int burstBytes = 32 * 1024;
        var queue = new SnapshotSendQueue<int>(
            new SnapshotSendQueuePolicy(
                bytesPerSecond,
                burstBytes,
                maxQueueLength: 4,
                maxQueueAge: TimeSpan.FromSeconds(10)),
            nowTicks: 1);
        var oversized = new SnapshotSendQueueItem<int>(
            value: 1,
            frame: 1,
            byteCount: 100 * 1024,
            SnapshotDeliveryPriority.FullBaseline,
            replaceable: false,
            producedAtTicks: 1);
        var next = new SnapshotSendQueueItem<int>(
            value: 2,
            frame: 2,
            byteCount: burstBytes,
            SnapshotDeliveryPriority.FullBaseline,
            replaceable: false,
            producedAtTicks: 2);

        queue.Enqueue(in oversized, nowTicks: 1);
        Assert.True(queue.TryDequeue(nowTicks: 1, out var first));
        Assert.Equal(1, first.Value);
        queue.Enqueue(in next, nowTicks: 2);

        Assert.False(queue.TryDequeue(nowTicks: TimeSpan.TicksPerSecond, out _));
        Assert.True(queue.TryDequeue(nowTicks: 2 * TimeSpan.TicksPerSecond + 1, out var second));
        Assert.Equal(2, second.Value);
    }

    private static SnapshotSendQueue<int> CreateQueue(TimeSpan maxQueueAge)
    {
        return new SnapshotSendQueue<int>(
            new SnapshotSendQueuePolicy(
                bytesPerSecond: 0,
                burstBytes: 0,
                maxQueueLength: 4,
                maxQueueAge: maxQueueAge),
            nowTicks: 0);
    }

    private static SnapshotSendQueueItem<int> CreateItem(
        int value,
        int frame,
        SnapshotDeliveryPriority priority,
        bool replaceable,
        long producedAtTicks)
    {
        return new SnapshotSendQueueItem<int>(
            value,
            frame,
            byteCount: 10,
            priority,
            replaceable,
            producedAtTicks);
    }

    private readonly record struct ReliableQueueItem(int Value, bool IsGap);
}
