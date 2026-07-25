using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public sealed class BattleDiagnosticEventTrackSnapshot
    {
        public BattleDiagnosticEventTrackSnapshot(
            long revision,
            in BattleDiagnosticStoreMetrics metrics,
            IList<BattleDiagnosticEvent> events)
        {
            Revision = revision;
            Metrics = metrics;
            Events = Copy(events);
        }

        public long Revision { get; }
        public BattleDiagnosticStoreMetrics Metrics { get; }
        public IReadOnlyList<BattleDiagnosticEvent> Events { get; }
        public long FirstSequence => Events.Count == 0 ? 0L : Events[0].Sequence;
        public long LastSequence => Events.Count == 0 ? 0L : Events[Events.Count - 1].Sequence;

        private static IReadOnlyList<BattleDiagnosticEvent> Copy(IList<BattleDiagnosticEvent> source)
        {
            return new ReadOnlyCollection<BattleDiagnosticEvent>(
                source == null
                    ? new List<BattleDiagnosticEvent>()
                    : new List<BattleDiagnosticEvent>(source));
        }
    }

    public sealed class BattleDiagnosticStateTrackSnapshot
    {
        public BattleDiagnosticStateTrackSnapshot(
            long revision,
            int frame,
            BattleDiagnosticWorldSummary? world,
            IList<BattleDiagnosticActorSummary> actors)
        {
            Revision = revision;
            Frame = frame;
            World = world;
            Actors = new ReadOnlyCollection<BattleDiagnosticActorSummary>(
                actors == null
                    ? new List<BattleDiagnosticActorSummary>()
                    : new List<BattleDiagnosticActorSummary>(actors));
        }

        public long Revision { get; }
        public int Frame { get; }
        public BattleDiagnosticWorldSummary? World { get; }
        public IReadOnlyList<BattleDiagnosticActorSummary> Actors { get; }
        public bool HasSnapshot => World.HasValue;
    }

    public sealed class BattleDiagnosticAttributeTrackSnapshot
    {
        public BattleDiagnosticAttributeTrackSnapshot(
            long revision,
            int frame,
            IList<BattleDiagnosticActorAttribute> attributes,
            IList<BattleDiagnosticActorAttributeModifier> modifiers)
        {
            Revision = revision;
            Frame = frame;
            Attributes = Copy(attributes);
            Modifiers = Copy(modifiers);
        }

        public long Revision { get; }
        public int Frame { get; }
        public IReadOnlyList<BattleDiagnosticActorAttribute> Attributes { get; }
        public IReadOnlyList<BattleDiagnosticActorAttributeModifier> Modifiers { get; }
        public bool HasSnapshot => Frame != BattleDiagnosticFrames.Invalid;

        private static IReadOnlyList<T> Copy<T>(IList<T> source)
        {
            return new ReadOnlyCollection<T>(source == null ? new List<T>() : new List<T>(source));
        }
    }

    public sealed class BattleDiagnosticLatestTrackSnapshot<T>
    {
        public BattleDiagnosticLatestTrackSnapshot(long revision, int frame, IList<T> items)
        {
            Revision = revision;
            Frame = frame;
            Items = new ReadOnlyCollection<T>(items == null ? new List<T>() : new List<T>(items));
        }

        public long Revision { get; }
        public int Frame { get; }
        public IReadOnlyList<T> Items { get; }
        public bool HasSnapshot => Frame != BattleDiagnosticFrames.Invalid;
    }

    public sealed class BattleDiagnosticTraceTrackSnapshot
    {
        public BattleDiagnosticTraceTrackSnapshot(
            long revision,
            IList<BattleDiagnosticTraceNodeSummary> nodes,
            bool truncated,
            bool isStable = true)
        {
            Revision = revision;
            Nodes = new ReadOnlyCollection<BattleDiagnosticTraceNodeSummary>(
                nodes == null
                    ? new List<BattleDiagnosticTraceNodeSummary>()
                    : new List<BattleDiagnosticTraceNodeSummary>(nodes));
            Truncated = truncated;
            IsStable = isStable;
        }

        public long Revision { get; }
        public IReadOnlyList<BattleDiagnosticTraceNodeSummary> Nodes { get; }
        public bool Truncated { get; }
        public bool IsStable { get; }
    }

    public sealed class BattleDiagnosticSessionSnapshot
    {
        public BattleDiagnosticSessionSnapshot(
            in BattleDiagnosticSessionInfo sessionInfo,
            long capturedAtTimestamp,
            BattleDiagnosticEventTrackSnapshot events,
            BattleDiagnosticStateTrackSnapshot state,
            BattleDiagnosticTraceTrackSnapshot trace,
            BattleDiagnosticAttributeTrackSnapshot attributes,
            BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff> buffs,
            BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag> tags,
            BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect> effects)
        {
            SessionInfo = sessionInfo;
            CapturedAtTimestamp = capturedAtTimestamp;
            Events = events ?? throw new ArgumentNullException(nameof(events));
            State = state ?? throw new ArgumentNullException(nameof(state));
            Trace = trace ?? throw new ArgumentNullException(nameof(trace));
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            Buffs = buffs ?? throw new ArgumentNullException(nameof(buffs));
            Tags = tags ?? throw new ArgumentNullException(nameof(tags));
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }

        public BattleDiagnosticSessionInfo SessionInfo { get; }
        public long CapturedAtTimestamp { get; }
        public BattleDiagnosticEventTrackSnapshot Events { get; }
        public BattleDiagnosticStateTrackSnapshot State { get; }
        public BattleDiagnosticTraceTrackSnapshot Trace { get; }
        public BattleDiagnosticAttributeTrackSnapshot Attributes { get; }
        public BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff> Buffs { get; }
        public BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag> Tags { get; }
        public BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect> Effects { get; }

        public bool LatestStateFramesAligned
        {
            get
            {
                if (!State.HasSnapshot) return false;
                return IsAligned(Attributes.HasSnapshot, Attributes.Frame) &&
                       IsAligned(Buffs.HasSnapshot, Buffs.Frame) &&
                       IsAligned(Tags.HasSnapshot, Tags.Frame) &&
                       IsAligned(Effects.HasSnapshot, Effects.Frame);
            }
        }

        private bool IsAligned(bool hasSnapshot, int frame)
        {
            return !hasSnapshot || frame == State.Frame;
        }
    }
}
