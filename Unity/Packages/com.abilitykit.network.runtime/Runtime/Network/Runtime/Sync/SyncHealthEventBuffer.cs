#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Sync
{
    public interface ISyncHealthEventSink
    {
        void Publish(in SyncHealthEvent healthEvent);
    }

    public readonly struct SyncHealthKindSummary
    {
        public SyncHealthKindSummary(
            SyncHealthEventKind kind,
            long count,
            long infoCount,
            long warningCount,
            long errorCount)
        {
            Kind = kind;
            Count = count;
            InfoCount = infoCount;
            WarningCount = warningCount;
            ErrorCount = errorCount;
        }

        public SyncHealthEventKind Kind { get; }

        public long Count { get; }

        public long InfoCount { get; }

        public long WarningCount { get; }

        public long ErrorCount { get; }
    }

    public sealed class SyncHealthReport
    {
        private static readonly SyncHealthKindSummary[] EmptyKindSummaries =
            Array.Empty<SyncHealthKindSummary>();
        private static readonly SyncHealthEvent[] EmptyEvents =
            Array.Empty<SyncHealthEvent>();

        public SyncHealthReport(
            long eventCount,
            long infoCount,
            long warningCount,
            long errorCount,
            long ignoredEventCount,
            long overwrittenEventCount,
            SyncHealthKindSummary[]? kinds,
            SyncHealthEvent[]? retainedEvents,
            int firstFrame = -1,
            int lastFrame = -1,
            SyncHealthSeverity highestSeverity = SyncHealthSeverity.Info,
            SyncCorrelationContext firstCorrelation = default)
        {
            EventCount = eventCount;
            InfoCount = infoCount;
            WarningCount = warningCount;
            ErrorCount = errorCount;
            IgnoredEventCount = ignoredEventCount;
            OverwrittenEventCount = overwrittenEventCount;
            Kinds = kinds ?? EmptyKindSummaries;
            RetainedEvents = retainedEvents ?? EmptyEvents;
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
            HighestSeverity = highestSeverity;
            FirstCorrelation = firstCorrelation;
        }

        public static SyncHealthReport Empty { get; } = new SyncHealthReport(
            0L,
            0L,
            0L,
            0L,
            0L,
            0L,
            EmptyKindSummaries,
            EmptyEvents);

        public long EventCount { get; }

        public long InfoCount { get; }

        public long WarningCount { get; }

        public long ErrorCount { get; }

        public long IgnoredEventCount { get; }

        public long OverwrittenEventCount { get; }

        public SyncHealthKindSummary[] Kinds { get; }

        public SyncHealthEvent[] RetainedEvents { get; }

        public int SchemaVersion => 2;

        public int FirstFrame { get; }

        public int LastFrame { get; }

        public SyncHealthSeverity HighestSeverity { get; }

        public SyncCorrelationContext FirstCorrelation { get; }

        public long ObserverQueuedCount => GetKindCount(SyncHealthEventKind.ObserverSnapshotQueued);

        public long ObserverDroppedCount => GetKindCount(SyncHealthEventKind.ObserverSnapshotDropped);

        public long ObserverCoalescedCount => GetKindCount(SyncHealthEventKind.ObserverSnapshotCoalesced);

        public long ObserverBaselineInvalidatedCount => GetKindCount(SyncHealthEventKind.ObserverBaselineInvalidated);

        public long ReliableGapCount => GetKindCount(SyncHealthEventKind.ReliableEventGap);

        private long GetKindCount(SyncHealthEventKind kind)
        {
            for (var i = 0; i < Kinds.Length; i++)
            {
                if (Kinds[i].Kind == kind) return Kinds[i].Count;
            }

            return 0L;
        }
    }

    public sealed class SyncHealthEventAggregator : ISyncHealthEventSink
    {
        private readonly Dictionary<SyncHealthEventKind, KindCounter> _kindCounters =
            new Dictionary<SyncHealthEventKind, KindCounter>();

        public long EventCount { get; private set; }

        public long InfoCount { get; private set; }

        public long WarningCount { get; private set; }

        public long ErrorCount { get; private set; }

        public long IgnoredEventCount { get; private set; }

        public int FirstFrame { get; private set; } = -1;

        public int LastFrame { get; private set; } = -1;

        public SyncHealthSeverity HighestSeverity { get; private set; }

        public SyncCorrelationContext FirstCorrelation { get; private set; }

        public void Publish(in SyncHealthEvent healthEvent)
        {
            if (!healthEvent.HasEvent)
            {
                IgnoredEventCount++;
                return;
            }

            EventCount++;
            if (FirstFrame < 0 || healthEvent.Frame < FirstFrame) FirstFrame = healthEvent.Frame;
            if (LastFrame < 0 || healthEvent.Frame > LastFrame) LastFrame = healthEvent.Frame;
            if (healthEvent.Severity > HighestSeverity) HighestSeverity = healthEvent.Severity;
            if (!FirstCorrelation.HasCorrelation && healthEvent.Context.HasCorrelation)
            {
                FirstCorrelation = healthEvent.Context;
            }

            switch (healthEvent.Severity)
            {
                case SyncHealthSeverity.Warning:
                    WarningCount++;
                    break;
                case SyncHealthSeverity.Error:
                    ErrorCount++;
                    break;
                case SyncHealthSeverity.Info:
                default:
                    InfoCount++;
                    break;
            }

            _kindCounters.TryGetValue(healthEvent.Kind, out var counter);
            counter.Observe(healthEvent.Severity);
            _kindCounters[healthEvent.Kind] = counter;
        }

        public void Publish(IReadOnlyList<SyncHealthEvent> healthEvents)
        {
            if (healthEvents == null) throw new ArgumentNullException(nameof(healthEvents));

            for (var i = 0; i < healthEvents.Count; i++)
            {
                var healthEvent = healthEvents[i];
                Publish(in healthEvent);
            }
        }

        public SyncHealthReport CreateReport()
        {
            return CreateReport(0L, null);
        }

        internal SyncHealthReport CreateReport(
            long overwrittenEventCount,
            SyncHealthEvent[]? retainedEvents)
        {
            var kinds = new SyncHealthKindSummary[_kindCounters.Count];
            var index = 0;
            foreach (var pair in _kindCounters)
            {
                kinds[index++] = pair.Value.ToSummary(pair.Key);
            }

            Array.Sort(kinds, static (left, right) =>
                ((int)left.Kind).CompareTo((int)right.Kind));
            return new SyncHealthReport(
                EventCount,
                InfoCount,
                WarningCount,
                ErrorCount,
                IgnoredEventCount,
                overwrittenEventCount,
                kinds,
                retainedEvents,
                FirstFrame,
                LastFrame,
                HighestSeverity,
                FirstCorrelation);
        }

        public void Reset()
        {
            EventCount = 0L;
            InfoCount = 0L;
            WarningCount = 0L;
            ErrorCount = 0L;
            IgnoredEventCount = 0L;
            FirstFrame = -1;
            LastFrame = -1;
            HighestSeverity = SyncHealthSeverity.Info;
            FirstCorrelation = default;
            _kindCounters.Clear();
        }

        private struct KindCounter
        {
            public long Count;
            public long InfoCount;
            public long WarningCount;
            public long ErrorCount;

            public void Observe(SyncHealthSeverity severity)
            {
                Count++;
                switch (severity)
                {
                    case SyncHealthSeverity.Warning:
                        WarningCount++;
                        break;
                    case SyncHealthSeverity.Error:
                        ErrorCount++;
                        break;
                    case SyncHealthSeverity.Info:
                    default:
                        InfoCount++;
                        break;
                }
            }

            public SyncHealthKindSummary ToSummary(SyncHealthEventKind kind)
            {
                return new SyncHealthKindSummary(
                    kind,
                    Count,
                    InfoCount,
                    WarningCount,
                    ErrorCount);
            }
        }
    }

    public sealed class SyncHealthEventBuffer :
        ISyncHealthEventSink,
        IReadOnlyList<SyncHealthEvent>
    {
        private readonly SyncHealthEvent[] _events;
        private readonly SyncHealthEventAggregator _aggregator =
            new SyncHealthEventAggregator();
        private int _start;

        public SyncHealthEventBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            _events = new SyncHealthEvent[capacity];
        }

        public int Capacity => _events.Length;

        public int Count { get; private set; }

        public long OverwrittenEventCount { get; private set; }

        public SyncHealthEvent this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _events[(_start + index) % Capacity];
            }
        }

        public void Publish(in SyncHealthEvent healthEvent)
        {
            _aggregator.Publish(in healthEvent);
            if (!healthEvent.HasEvent)
            {
                return;
            }

            if (Count < Capacity)
            {
                _events[(_start + Count) % Capacity] = healthEvent;
                Count++;
                return;
            }

            _events[_start] = healthEvent;
            _start = (_start + 1) % Capacity;
            OverwrittenEventCount++;
        }

        public void Publish(IReadOnlyList<SyncHealthEvent> healthEvents)
        {
            if (healthEvents == null) throw new ArgumentNullException(nameof(healthEvents));

            for (var i = 0; i < healthEvents.Count; i++)
            {
                var healthEvent = healthEvents[i];
                Publish(in healthEvent);
            }
        }

        public SyncHealthEvent[] Snapshot()
        {
            if (Count == 0)
            {
                return Array.Empty<SyncHealthEvent>();
            }

            var snapshot = new SyncHealthEvent[Count];
            for (var i = 0; i < Count; i++)
            {
                snapshot[i] = this[i];
            }

            return snapshot;
        }

        public SyncHealthReport CreateReport()
        {
            return _aggregator.CreateReport(
                OverwrittenEventCount,
                Snapshot());
        }

        public void Reset()
        {
            Array.Clear(_events, 0, _events.Length);
            _start = 0;
            Count = 0;
            OverwrittenEventCount = 0L;
            _aggregator.Reset();
        }

        public IEnumerator<SyncHealthEvent> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public sealed class SyncHealthEventListView : IReadOnlyList<SyncHealthEvent>
    {
        private readonly Func<IReadOnlyList<SyncHealthEvent>> _getPrimary;
        private readonly Func<IReadOnlyList<SyncHealthEvent>> _getSecondary;
        private readonly Func<IReadOnlyList<SyncHealthEvent>>? _getTertiary;

        public SyncHealthEventListView(
            Func<IReadOnlyList<SyncHealthEvent>> getPrimary,
            Func<IReadOnlyList<SyncHealthEvent>> getSecondary)
            : this(getPrimary, getSecondary, null)
        {
        }

        public SyncHealthEventListView(
            Func<IReadOnlyList<SyncHealthEvent>> getPrimary,
            Func<IReadOnlyList<SyncHealthEvent>> getSecondary,
            Func<IReadOnlyList<SyncHealthEvent>>? getTertiary)
        {
            _getPrimary = getPrimary ?? throw new ArgumentNullException(nameof(getPrimary));
            _getSecondary = getSecondary ?? throw new ArgumentNullException(nameof(getSecondary));
            _getTertiary = getTertiary;
        }

        public int Count
        {
            get
            {
                var count = _getPrimary().Count + _getSecondary().Count;
                return _getTertiary == null ? count : count + _getTertiary().Count;
            }
        }

        public SyncHealthEvent this[int index]
        {
            get
            {
                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                var primary = _getPrimary();
                if (index < primary.Count)
                {
                    return primary[index];
                }

                var secondary = _getSecondary();
                var secondaryIndex = index - primary.Count;
                if ((uint)secondaryIndex >= (uint)secondary.Count)
                {
                    if (_getTertiary == null)
                        throw new ArgumentOutOfRangeException(nameof(index));

                    var tertiary = _getTertiary();
                    var tertiaryIndex = secondaryIndex - secondary.Count;
                    if ((uint)tertiaryIndex >= (uint)tertiary.Count)
                        throw new ArgumentOutOfRangeException(nameof(index));

                    return tertiary[tertiaryIndex];
                }

                return secondary[secondaryIndex];
            }
        }

        public IEnumerator<SyncHealthEvent> GetEnumerator()
        {
            var primary = _getPrimary();
            for (var i = 0; i < primary.Count; i++)
            {
                yield return primary[i];
            }

            var secondary = _getSecondary();
            for (var i = 0; i < secondary.Count; i++)
            {
                yield return secondary[i];
            }

            if (_getTertiary == null)
                yield break;

            var tertiary = _getTertiary();
            for (var i = 0; i < tertiary.Count; i++)
            {
                yield return tertiary[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
