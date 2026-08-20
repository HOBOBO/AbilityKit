using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class SnapshotSendQueueTests
{
    [Fact]
    public void TryDequeue_Respects128KbpsBudgetAfterBurstIsConsumed()
    {
        var policy = new SnapshotSendQueuePolicy(
            bytesPerSecond: 16_384,
            burstBytes: 4_096,
            maxQueueLength: 8,
            maxQueueAge: TimeSpan.FromSeconds(5));
        var queue = new SnapshotSendQueue<int>(policy, nowTicks: 1);
        Enqueue(queue, value: 1, frame: 1, bytes: 4_096, SnapshotDeliveryPriority.Delta, replaceable: false, nowTicks: 1);
        Enqueue(queue, value: 2, frame: 2, bytes: 4_096, SnapshotDeliveryPriority.Delta, replaceable: false, nowTicks: 1);

        Assert.True(queue.TryDequeue(1, out var first));
        Assert.Equal(1, first.Value);
        Assert.False(queue.TryDequeue(1, out _));
        Assert.False(queue.TryDequeue(TimeSpan.FromMilliseconds(249).Ticks + 1, out _));
        Assert.True(queue.TryDequeue(TimeSpan.FromMilliseconds(250).Ticks + 1, out var second));
        Assert.Equal(2, second.Value);
    }

    [Fact]
    public void Enqueue_FullBaselineEvictsAllDeltaAndMovesAheadOfCriticalItem()
    {
        var queue = CreateUnlimitedQueue(maxQueueLength: 4);
        Enqueue(queue, 1, 1, 100, SnapshotDeliveryPriority.Delta, replaceable: false, nowTicks: 1);
        Enqueue(queue, 2, 2, 200, SnapshotDeliveryPriority.Critical, replaceable: false, nowTicks: 2);

        var result = Enqueue(queue, 3, 3, 300, SnapshotDeliveryPriority.FullBaseline, replaceable: false, nowTicks: 3);

        Assert.Equal(1, result.DroppedItems);
        Assert.Equal(100, result.DroppedBytes);
        Assert.False(result.BaselineInvalidated);
        Assert.True(queue.TryDequeue(3, out var baseline));
        Assert.Equal(3, baseline.Value);
        Assert.True(queue.TryDequeue(3, out var critical));
        Assert.Equal(2, critical.Value);
    }

    [Fact]
    public void Enqueue_WhenFullBaselineAlreadyQueued_ReplacesItWithLatestBaseline()
    {
        var queue = CreateUnlimitedQueue(maxQueueLength: 1);
        Enqueue(queue, 1, 10, 100, SnapshotDeliveryPriority.FullBaseline, replaceable: false, nowTicks: 1);

        var result = Enqueue(queue, 2, 11, 120, SnapshotDeliveryPriority.FullBaseline, replaceable: false, nowTicks: 2);
        var metrics = queue.CreateMetrics(2);

        Assert.Equal(SnapshotDeliveryStatus.Accepted, result.Status);
        Assert.Equal(1, result.DroppedItems);
        Assert.Equal(100, result.DroppedBytes);
        Assert.Equal(100, metrics.MergedBytes);
        Assert.Equal(0, metrics.DroppedBytes);
        Assert.True(queue.TryDequeue(2, out var latest));
        Assert.Equal(2, latest.Value);
    }

    [Fact]
    public void TryDequeue_WhenFullBaselineExceedsMaxQueueAge_RetainsBaselineUntilDelivery()
    {
        var policy = new SnapshotSendQueuePolicy(
            bytesPerSecond: 256,
            burstBytes: 32_768,
            maxQueueLength: 1,
            maxQueueAge: TimeSpan.FromMilliseconds(100));
        var queue = new SnapshotSendQueue<int>(policy, nowTicks: 1);
        Enqueue(queue, 1, 10, 100, SnapshotDeliveryPriority.FullBaseline, replaceable: false, nowTicks: 1);

        Assert.True(queue.TryDequeue(TimeSpan.FromMilliseconds(250).Ticks + 1, out var baseline));
        Assert.Equal(1, baseline.Value);
        Assert.Equal(0, queue.CreateMetrics(TimeSpan.FromMilliseconds(250).Ticks + 1).DroppedBytes);
    }

    [Fact]
    public void Enqueue_ReplaceableDeltaMergesOlderReplaceableDelta()
    {
        var queue = CreateUnlimitedQueue(maxQueueLength: 4);
        Enqueue(queue, 1, 10, 100, SnapshotDeliveryPriority.Delta, replaceable: true, nowTicks: 1);

        var result = Enqueue(queue, 2, 11, 120, SnapshotDeliveryPriority.Delta, replaceable: true, nowTicks: 2);
        var metrics = queue.CreateMetrics(2);

        Assert.Equal(SnapshotDeliveryStatus.Accepted, result.Status);
        Assert.Equal(1, result.DroppedItems);
        Assert.Equal(100, metrics.MergedBytes);
        Assert.Equal(0, metrics.DroppedBytes);
        Assert.True(queue.TryDequeue(2, out var latest));
        Assert.Equal(2, latest.Value);
    }

    [Fact]
    public void Enqueue_DropsExpiredDeltaAndInvalidatesBaselineOnce()
    {
        var policy = new SnapshotSendQueuePolicy(
            bytesPerSecond: 0,
            burstBytes: 0,
            maxQueueLength: 4,
            maxQueueAge: TimeSpan.FromMilliseconds(100));
        var queue = new SnapshotSendQueue<int>(policy, nowTicks: 1);
        Enqueue(queue, 1, 10, 100, SnapshotDeliveryPriority.Delta, replaceable: false, nowTicks: 1);

        var result = Enqueue(
            queue,
            new SnapshotSendQueueItem<int>(2, 11, 120, SnapshotDeliveryPriority.Delta, replaceable: false, producedAtTicks: TimeSpan.FromMilliseconds(101).Ticks + 2),
            TimeSpan.FromMilliseconds(101).Ticks + 2);
        var metrics = queue.CreateMetrics(TimeSpan.FromMilliseconds(101).Ticks + 2);

        Assert.Equal(1, result.DroppedItems);
        Assert.True(result.BaselineInvalidated);
        Assert.Equal(1, metrics.ResyncCount);
        Assert.Equal(100, metrics.DroppedBytes);
    }

    [Fact]
    public void Enqueue_WhenQueueIsFullRejectsEqualPriorityItemAsBackpressured()
    {
        var queue = CreateUnlimitedQueue(maxQueueLength: 1);
        Enqueue(queue, 1, 1, 100, SnapshotDeliveryPriority.Critical, replaceable: false, nowTicks: 1);

        var result = Enqueue(queue, 2, 2, 120, SnapshotDeliveryPriority.Critical, replaceable: false, nowTicks: 2);
        var metrics = queue.CreateMetrics(2);

        Assert.Equal(SnapshotDeliveryStatus.Backpressured, result.Status);
        Assert.Equal(1, result.QueueLength);
        Assert.Equal(120, metrics.DroppedBytes);
        Assert.True(queue.TryDequeue(2, out var retained));
        Assert.Equal(1, retained.Value);
    }

    [Fact]
    public void MarkSent_TracksSentBytesAndBaselineAge()
    {
        var queue = CreateUnlimitedQueue(maxQueueLength: 2);
        Enqueue(queue, 1, 1, 256, SnapshotDeliveryPriority.FullBaseline, replaceable: false, nowTicks: 10);
        Assert.True(queue.TryDequeue(10, out var item));

        queue.MarkSent(in item, nowTicks: 20);
        var metrics = queue.CreateMetrics(nowTicks: 50);

        Assert.Equal(256, metrics.ProducedBytes);
        Assert.Equal(256, metrics.SentBytes);
        Assert.Equal(30, metrics.BaselineAgeTicks);
    }

    [Fact]
    public void TryDequeue_WhenDeltaExpiresWithoutNewEnqueue_CountsOneResync()
    {
        var policy = new SnapshotSendQueuePolicy(
            bytesPerSecond: 0,
            burstBytes: 0,
            maxQueueLength: 2,
            maxQueueAge: TimeSpan.FromTicks(50));
        var queue = new SnapshotSendQueue<int>(policy, nowTicks: 1);
        Enqueue(queue, 1, 1, 100, SnapshotDeliveryPriority.Delta, replaceable: false, nowTicks: 1);

        Assert.False(queue.TryDequeue(nowTicks: 100, out _));
        var metrics = queue.CreateMetrics(100);

        Assert.Equal(1, metrics.ResyncCount);
        Assert.Equal(100, metrics.DroppedBytes);
        Assert.Equal(0, metrics.QueueLength);
    }

    [Fact]
    public void MarkDeliveryFailed_TracksDroppedBytesAndRequestedResync()
    {
        var queue = CreateUnlimitedQueue(maxQueueLength: 2);
        Enqueue(queue, 1, 1, 300, SnapshotDeliveryPriority.Delta, replaceable: false, nowTicks: 1);
        Assert.True(queue.TryDequeue(1, out var failed));

        queue.MarkDeliveryFailed(in failed, baselineInvalidated: true);
        var metrics = queue.CreateMetrics(1);

        Assert.Equal(300, metrics.DroppedBytes);
        Assert.Equal(1, metrics.ResyncCount);
        Assert.Equal(0, metrics.SentBytes);
    }

    [Fact]
    public void TryDequeue_OversizedItemSendsOnceAndRepaysBudgetDebt()
    {
        var policy = new SnapshotSendQueuePolicy(
            bytesPerSecond: 1_000,
            burstBytes: 500,
            maxQueueLength: 4,
            maxQueueAge: TimeSpan.FromSeconds(5));
        var queue = new SnapshotSendQueue<int>(policy, nowTicks: 1);
        Enqueue(queue, 1, 1, 750, SnapshotDeliveryPriority.FullBaseline, replaceable: false, nowTicks: 1);
        Enqueue(queue, 2, 2, 250, SnapshotDeliveryPriority.Critical, replaceable: false, nowTicks: 1);

        Assert.True(queue.TryDequeue(1, out var oversized));
        Assert.Equal(1, oversized.Value);
        Assert.False(queue.TryDequeue(TimeSpan.FromMilliseconds(499).Ticks + 1, out _));
        Assert.True(queue.TryDequeue(TimeSpan.FromMilliseconds(500).Ticks + 1, out var next));
        Assert.Equal(2, next.Value);
    }

    [Fact]
    public void TryDequeue_OversizedItemRepaysFullDebtAcrossMultipleRefills()
    {
        var policy = new SnapshotSendQueuePolicy(
            bytesPerSecond: 1_000,
            burstBytes: 500,
            maxQueueLength: 4,
            maxQueueAge: TimeSpan.FromSeconds(5));
        var queue = new SnapshotSendQueue<int>(policy, nowTicks: 1);
        Enqueue(queue, 1, 1, 1_000, SnapshotDeliveryPriority.FullBaseline, replaceable: false, nowTicks: 1);
        Enqueue(queue, 2, 2, 100, SnapshotDeliveryPriority.Critical, replaceable: false, nowTicks: 1);

        Assert.True(queue.TryDequeue(1, out var oversized));
        Assert.Equal(1, oversized.Value);
        Assert.False(queue.TryDequeue(TimeSpan.FromMilliseconds(500).Ticks + 1, out _));
        Assert.False(queue.TryDequeue(TimeSpan.FromMilliseconds(599).Ticks + 1, out _));
        Assert.True(queue.TryDequeue(TimeSpan.FromMilliseconds(600).Ticks + 1, out var next));
        Assert.Equal(2, next.Value);
    }

    [Fact]
    public void CreateMetrics_UsesOldestQueuedItemAcrossPriorityOrder()
    {
        var queue = CreateUnlimitedQueue(maxQueueLength: 4);
        Enqueue(queue, 1, 1, 100, SnapshotDeliveryPriority.Delta, replaceable: false, nowTicks: 10);
        Enqueue(queue, 2, 2, 100, SnapshotDeliveryPriority.Critical, replaceable: false, nowTicks: 40);

        var metrics = queue.CreateMetrics(nowTicks: 100);

        Assert.Equal(90, metrics.QueueAgeTicks);
    }

    [Fact]
    public void TryDequeue_DropsExpiredItemBehindNewerHigherPriorityItem()
    {
        var policy = new SnapshotSendQueuePolicy(
            bytesPerSecond: 0,
            burstBytes: 0,
            maxQueueLength: 4,
            maxQueueAge: TimeSpan.FromTicks(50));
        var queue = new SnapshotSendQueue<int>(policy, nowTicks: 1);
        Enqueue(queue, 1, 1, 100, SnapshotDeliveryPriority.Delta, replaceable: false, nowTicks: 1);
        Enqueue(queue, 2, 2, 100, SnapshotDeliveryPriority.Critical, replaceable: false, nowTicks: 75);

        Assert.True(queue.TryDequeue(nowTicks: 100, out var retained));
        Assert.Equal(2, retained.Value);
        Assert.Equal(1, queue.CreateMetrics(100).ResyncCount);
        Assert.Equal(100, queue.CreateMetrics(100).DroppedBytes);
    }

    [Fact]
    public void Enqueue_HigherPriorityOverflowEvictsDeltaAndCountsOneResync()
    {
        var queue = CreateUnlimitedQueue(maxQueueLength: 1);
        Enqueue(queue, 1, 1, 100, SnapshotDeliveryPriority.Delta, replaceable: false, nowTicks: 1);

        var result = Enqueue(queue, 2, 2, 200, SnapshotDeliveryPriority.Critical, replaceable: false, nowTicks: 2);
        var metrics = queue.CreateMetrics(2);

        Assert.True(result.BaselineInvalidated);
        Assert.Equal(1, result.DroppedItems);
        Assert.Equal(1, metrics.ResyncCount);
        Assert.Equal(100, metrics.DroppedBytes);
    }

    [Fact]
    public void Clear_ResetsLiveStateAndPreservesCumulativeMetrics()
    {
        var queue = CreateUnlimitedQueue(maxQueueLength: 2);
        Enqueue(queue, 1, 1, 256, SnapshotDeliveryPriority.FullBaseline, replaceable: false, nowTicks: 10);
        Assert.True(queue.TryDequeue(10, out var sent));
        queue.MarkSent(in sent, nowTicks: 20);
        Enqueue(queue, 2, 2, 128, SnapshotDeliveryPriority.Delta, replaceable: false, nowTicks: 30);

        queue.Clear();
        var metrics = queue.CreateMetrics(nowTicks: 100);

        Assert.Equal(0, queue.Count);
        Assert.Equal(0, metrics.QueueAgeTicks);
        Assert.Equal(0, metrics.BaselineAgeTicks);
        Assert.Equal(384, metrics.ProducedBytes);
        Assert.Equal(256, metrics.SentBytes);
    }

    private static SnapshotSendQueue<int> CreateUnlimitedQueue(int maxQueueLength)
    {
        return new SnapshotSendQueue<int>(
            new SnapshotSendQueuePolicy(
                bytesPerSecond: 0,
                burstBytes: 0,
                maxQueueLength: maxQueueLength,
                maxQueueAge: TimeSpan.FromSeconds(5)),
            nowTicks: 1);
    }

    private static SnapshotSendQueueResult Enqueue(
        SnapshotSendQueue<int> queue,
        int value,
        int frame,
        int bytes,
        SnapshotDeliveryPriority priority,
        bool replaceable,
        long nowTicks)
    {
        var item = new SnapshotSendQueueItem<int>(value, frame, bytes, priority, replaceable, nowTicks);
        return Enqueue(queue, item, nowTicks);
    }

    private static SnapshotSendQueueResult Enqueue(
        SnapshotSendQueue<int> queue,
        SnapshotSendQueueItem<int> item,
        long nowTicks)
    {
        return queue.Enqueue(in item, nowTicks);
    }
}
