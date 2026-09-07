#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Sync
{
    public enum SnapshotDeliveryPriority
    {
        Delta = 0,
        Critical = 1,
        FullBaseline = 2
    }

    public enum SnapshotDeliveryStatus
    {
        Accepted = 0,
        Queued = 1,
        DroppedStale = 2,
        Backpressured = 3,
        Offline = 4,
        Failed = 5
    }

    public readonly struct SnapshotSendQueuePolicy
    {
        public SnapshotSendQueuePolicy(
            int bytesPerSecond,
            int burstBytes,
            int maxQueueLength,
            TimeSpan maxQueueAge)
        {
            if (bytesPerSecond < 0)
                throw new ArgumentOutOfRangeException(nameof(bytesPerSecond));
            if (burstBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(burstBytes));
            if (maxQueueLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxQueueLength));
            if (maxQueueAge < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxQueueAge));

            BytesPerSecond = bytesPerSecond;
            BurstBytes = bytesPerSecond == 0 ? int.MaxValue : Math.Max(1, burstBytes);
            MaxQueueLength = maxQueueLength;
            MaxQueueAge = maxQueueAge;
        }

        public int BytesPerSecond { get; }
        public int BurstBytes { get; }
        public int MaxQueueLength { get; }
        public TimeSpan MaxQueueAge { get; }

        public static SnapshotSendQueuePolicy Default { get; } = new SnapshotSendQueuePolicy(
            bytesPerSecond: 128 * 1024 / 8,
            burstBytes: 32 * 1024,
            maxQueueLength: 32,
            maxQueueAge: TimeSpan.FromMilliseconds(500));
    }

    public readonly struct SnapshotSendQueueItem<T>
    {
        public SnapshotSendQueueItem(
            T value,
            int frame,
            int byteCount,
            SnapshotDeliveryPriority priority,
            bool replaceable,
            long producedAtTicks)
        {
            if (byteCount < 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            Value = value;
            Frame = frame;
            ByteCount = byteCount;
            Priority = priority;
            Replaceable = replaceable;
            ProducedAtTicks = producedAtTicks;
        }

        public T Value { get; }
        public int Frame { get; }
        public int ByteCount { get; }
        public SnapshotDeliveryPriority Priority { get; }
        public bool Replaceable { get; }
        public long ProducedAtTicks { get; }
    }

    public readonly struct SnapshotSendQueueResult
    {
        public SnapshotSendQueueResult(
            SnapshotDeliveryStatus status,
            int queueLength,
            int droppedItems,
            long droppedBytes,
            bool baselineInvalidated)
        {
            Status = status;
            QueueLength = queueLength;
            DroppedItems = droppedItems;
            DroppedBytes = droppedBytes;
            BaselineInvalidated = baselineInvalidated;
        }

        public SnapshotDeliveryStatus Status { get; }
        public int QueueLength { get; }
        public int DroppedItems { get; }
        public long DroppedBytes { get; }
        public bool BaselineInvalidated { get; }
    }

    public readonly struct SnapshotSendQueueMetrics
    {
        public SnapshotSendQueueMetrics(
            long producedBytes,
            long sentBytes,
            long droppedBytes,
            long mergedBytes,
            int queueLength,
            long queueAgeTicks,
            long baselineAgeTicks,
            long resyncCount)
        {
            ProducedBytes = producedBytes;
            SentBytes = sentBytes;
            DroppedBytes = droppedBytes;
            MergedBytes = mergedBytes;
            QueueLength = queueLength;
            QueueAgeTicks = queueAgeTicks;
            BaselineAgeTicks = baselineAgeTicks;
            ResyncCount = resyncCount;
        }

        public long ProducedBytes { get; }
        public long SentBytes { get; }
        public long DroppedBytes { get; }
        public long MergedBytes { get; }
        public int QueueLength { get; }
        public long QueueAgeTicks { get; }
        public long BaselineAgeTicks { get; }
        public long ResyncCount { get; }
    }

    public sealed class SnapshotSendQueue<T>
    {
        private readonly SnapshotSendQueuePolicy _policy;
        private readonly LinkedList<SnapshotSendQueueItem<T>> _items = new LinkedList<SnapshotSendQueueItem<T>>();
        private double _availableBytes;
        private long _lastBudgetTicks;
        private long _producedBytes;
        private long _sentBytes;
        private long _droppedBytes;
        private long _mergedBytes;
        private long _lastBaselineSentTicks;
        private long _resyncCount;

        public SnapshotSendQueue(SnapshotSendQueuePolicy policy, long nowTicks = 0)
        {
            _policy = policy;
            _availableBytes = policy.BurstBytes;
            _lastBudgetTicks = nowTicks;
        }

        public int Count => _items.Count;

        public SnapshotSendQueueResult Enqueue(in SnapshotSendQueueItem<T> item, long nowTicks)
        {
            _producedBytes += item.ByteCount;
            var expired = DropExpired(nowTicks, countResync: false);
            var droppedItems = expired.DroppedItems;
            var droppedBytes = expired.DroppedBytes;
            var baselineInvalidated = expired.BaselineInvalidated;

            if (item.Priority == SnapshotDeliveryPriority.FullBaseline)
            {
                var removed = RemoveSupersededByFullBaseline();
                droppedItems += removed.DroppedItems;
                droppedBytes += removed.DroppedBytes;
            }
            else if (item.Replaceable)
            {
                var merged = RemoveOlderReplaceable(item.Frame);
                droppedItems += merged.DroppedItems;
                droppedBytes += merged.DroppedBytes;
            }

            while (_items.Count >= _policy.MaxQueueLength)
            {
                var candidate = FindLowestPriorityNode();
                if (candidate == null || candidate.Value.Priority >= item.Priority)
                {
                    _droppedBytes += item.ByteCount;
                    if (item.Priority == SnapshotDeliveryPriority.Delta)
                    {
                        baselineInvalidated = true;
                        _resyncCount++;
                    }

                    return new SnapshotSendQueueResult(
                        SnapshotDeliveryStatus.Backpressured,
                        _items.Count,
                        droppedItems + 1,
                        droppedBytes + item.ByteCount,
                        baselineInvalidated);
                }

                var evicted = candidate.Value;
                _items.Remove(candidate);
                _droppedBytes += evicted.ByteCount;
                droppedItems++;
                droppedBytes += evicted.ByteCount;
                baselineInvalidated |= evicted.Priority == SnapshotDeliveryPriority.Delta;
            }

            InsertByPriority(item);
            if (baselineInvalidated)
                _resyncCount++;

            return new SnapshotSendQueueResult(
                _items.Count == 1 ? SnapshotDeliveryStatus.Accepted : SnapshotDeliveryStatus.Queued,
                _items.Count,
                droppedItems,
                droppedBytes,
                baselineInvalidated);
        }

        public bool TryDequeue(long nowTicks, out SnapshotSendQueueItem<T> item)
        {
            DropExpired(nowTicks, countResync: true);
            RefillBudget(nowTicks);
            if (_items.First == null)
            {
                item = default;
                return false;
            }

            var requiredBudget = Math.Min(_items.First.Value.ByteCount, _policy.BurstBytes);
            if (_availableBytes < requiredBudget)
            {
                item = default;
                return false;
            }

            item = _items.First.Value;
            _items.RemoveFirst();
            if (_policy.BytesPerSecond > 0)
                _availableBytes -= item.ByteCount;
            return true;
        }

        public void MarkSent(in SnapshotSendQueueItem<T> item, long nowTicks)
        {
            _sentBytes += item.ByteCount;
            if (item.Priority == SnapshotDeliveryPriority.FullBaseline)
                _lastBaselineSentTicks = nowTicks;
        }

        public void MarkDeliveryFailed(in SnapshotSendQueueItem<T> item, bool baselineInvalidated)
        {
            _droppedBytes += item.ByteCount;
            if (baselineInvalidated)
                _resyncCount++;
        }

        public SnapshotSendQueueMetrics CreateMetrics(long nowTicks)
        {
            var oldestProducedAtTicks = nowTicks;
            var node = _items.First;
            while (node != null)
            {
                oldestProducedAtTicks = Math.Min(oldestProducedAtTicks, node.Value.ProducedAtTicks);
                node = node.Next;
            }

            var queueAge = _items.Count == 0 ? 0L : Math.Max(0L, nowTicks - oldestProducedAtTicks);
            var baselineAge = _lastBaselineSentTicks <= 0 ? 0L : Math.Max(0L, nowTicks - _lastBaselineSentTicks);
            return new SnapshotSendQueueMetrics(
                _producedBytes,
                _sentBytes,
                _droppedBytes,
                _mergedBytes,
                _items.Count,
                queueAge,
                baselineAge,
                _resyncCount);
        }

        public void Clear()
        {
            _items.Clear();
            _availableBytes = _policy.BurstBytes;
            _lastBudgetTicks = 0;
            _lastBaselineSentTicks = 0;
        }

        private SnapshotSendQueueResult DropExpired(long nowTicks, bool countResync)
        {
            var maxAgeTicks = _policy.MaxQueueAge.Ticks;
            var droppedItems = 0;
            long droppedBytes = 0;
            var baselineInvalidated = false;
            var node = _items.First;
            while (node != null)
            {
                var next = node.Next;
                if (node.Value.Priority != SnapshotDeliveryPriority.FullBaseline
                    && nowTicks - node.Value.ProducedAtTicks > maxAgeTicks)
                {
                    var expired = node.Value;
                    _items.Remove(node);
                    _droppedBytes += expired.ByteCount;
                    droppedBytes += expired.ByteCount;
                    droppedItems++;
                    baselineInvalidated |= expired.Priority == SnapshotDeliveryPriority.Delta;
                }
                node = next;
            }

            if (baselineInvalidated && countResync)
                _resyncCount++;

            return new SnapshotSendQueueResult(
                droppedItems == 0 ? SnapshotDeliveryStatus.Accepted : SnapshotDeliveryStatus.DroppedStale,
                _items.Count,
                droppedItems,
                droppedBytes,
                baselineInvalidated);
        }

        private SnapshotSendQueueResult RemoveSupersededByFullBaseline()
        {
            var droppedItems = 0;
            long droppedBytes = 0;
            var node = _items.First;
            while (node != null)
            {
                var next = node.Next;
                if (node.Value.Priority is SnapshotDeliveryPriority.Delta or SnapshotDeliveryPriority.FullBaseline)
                {
                    _items.Remove(node);
                    droppedItems++;
                    droppedBytes += node.Value.ByteCount;
                    _mergedBytes += node.Value.ByteCount;
                }
                node = next;
            }

            return new SnapshotSendQueueResult(
                SnapshotDeliveryStatus.Accepted,
                _items.Count,
                droppedItems,
                droppedBytes,
                baselineInvalidated: false);
        }

        private SnapshotSendQueueResult RemoveOlderReplaceable(int frame)
        {
            var droppedItems = 0;
            long droppedBytes = 0;
            var node = _items.First;
            while (node != null)
            {
                var next = node.Next;
                if (node.Value.Replaceable && node.Value.Frame <= frame)
                {
                    _items.Remove(node);
                    droppedItems++;
                    droppedBytes += node.Value.ByteCount;
                    _mergedBytes += node.Value.ByteCount;
                }
                node = next;
            }

            return new SnapshotSendQueueResult(
                SnapshotDeliveryStatus.Accepted,
                _items.Count,
                droppedItems,
                droppedBytes,
                baselineInvalidated: false);
        }

        private LinkedListNode<SnapshotSendQueueItem<T>>? FindLowestPriorityNode()
        {
            var node = _items.Last;
            var candidate = node;
            while (node != null)
            {
                if (candidate == null || node.Value.Priority < candidate.Value.Priority)
                    candidate = node;
                node = node.Previous;
            }
            return candidate;
        }

        private void InsertByPriority(in SnapshotSendQueueItem<T> item)
        {
            var node = _items.First;
            while (node != null && node.Value.Priority >= item.Priority)
                node = node.Next;

            if (node == null)
                _items.AddLast(item);
            else
                _items.AddBefore(node, item);
        }

        private void RefillBudget(long nowTicks)
        {
            if (_policy.BytesPerSecond == 0)
            {
                _availableBytes = int.MaxValue;
                _lastBudgetTicks = nowTicks;
                return;
            }

            if (_lastBudgetTicks == 0)
            {
                _lastBudgetTicks = nowTicks;
                return;
            }

            var elapsedTicks = Math.Max(0L, nowTicks - _lastBudgetTicks);
            _lastBudgetTicks = nowTicks;
            _availableBytes = Math.Min(
                _policy.BurstBytes,
                _availableBytes + elapsedTicks * (double)_policy.BytesPerSecond / TimeSpan.TicksPerSecond);
        }
    }
}
