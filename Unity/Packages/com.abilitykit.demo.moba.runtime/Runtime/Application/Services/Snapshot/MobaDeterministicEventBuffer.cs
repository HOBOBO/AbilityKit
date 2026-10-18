using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// Unmanaged-friendly event data contract for high-frequency simulation output.
    /// It contains only deterministic scalar values and can be copied into a native/job buffer later.
    /// </summary>
    public readonly struct MobaDeterministicEventRecord
    {
        public MobaDeterministicEventRecord(
            int frame,
            int kind,
            int sourceActorId,
            int targetActorId,
            int sequence,
            int value)
        {
            Frame = frame;
            Kind = kind;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            Sequence = sequence;
            Value = value;
        }

        public int Frame { get; }
        public int Kind { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public int Sequence { get; }
        public int Value { get; }
    }

    /// <summary>
    /// Deterministic main-thread staging buffer. Producers append records during simulation;
    /// the commit boundary drains them in a stable order before managed publication.
    /// </summary>
    internal sealed class MobaDeterministicEventBuffer
    {
        private readonly int _initialCapacity;
        private readonly int _maxRetainedCapacity;
        private readonly List<MobaDeterministicEventRecord> _records;

        public MobaDeterministicEventBuffer(int initialCapacity = 32, int maxRetainedCapacity = 512)
        {
            if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            if (maxRetainedCapacity < initialCapacity) throw new ArgumentOutOfRangeException(nameof(maxRetainedCapacity));

            _initialCapacity = initialCapacity;
            _maxRetainedCapacity = maxRetainedCapacity;
            _records = new List<MobaDeterministicEventRecord>(initialCapacity);
        }

        public int Count => _records.Count;

        public void Add(in MobaDeterministicEventRecord record)
        {
            _records.Add(record);
        }

        public int DrainSortedTo(IList<MobaDeterministicEventRecord> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            _records.Sort(Compare);
            for (var i = 0; i < _records.Count; i++)
            {
                destination.Add(_records[i]);
            }

            var count = _records.Count;
            _records.Clear();
            TrimIfNeeded();
            return count;
        }

        public void Clear()
        {
            _records.Clear();
            TrimIfNeeded();
        }

        private static int Compare(MobaDeterministicEventRecord left, MobaDeterministicEventRecord right)
        {
            var compare = left.Frame.CompareTo(right.Frame);
            if (compare != 0) return compare;

            compare = left.Kind.CompareTo(right.Kind);
            if (compare != 0) return compare;

            compare = left.SourceActorId.CompareTo(right.SourceActorId);
            if (compare != 0) return compare;

            compare = left.TargetActorId.CompareTo(right.TargetActorId);
            if (compare != 0) return compare;

            compare = left.Sequence.CompareTo(right.Sequence);
            if (compare != 0) return compare;

            return left.Value.CompareTo(right.Value);
        }

        private void TrimIfNeeded()
        {
            if (_records.Capacity <= _maxRetainedCapacity) return;
            _records.Capacity = _initialCapacity;
        }
    }
}
