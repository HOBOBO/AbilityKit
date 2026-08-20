using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Aoi
{
    public readonly struct AoiEntityKey : IEquatable<AoiEntityKey>, IComparable<AoiEntityKey>
    {
        public AoiEntityKey(int kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        public int Kind { get; }

        public int Id { get; }

        public bool IsValid => Id > 0;

        public bool Equals(AoiEntityKey other)
        {
            return Kind == other.Kind && Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is AoiEntityKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Kind * 397) ^ Id;
            }
        }

        public int CompareTo(AoiEntityKey other)
        {
            var byKind = Kind.CompareTo(other.Kind);
            return byKind != 0 ? byKind : Id.CompareTo(other.Id);
        }

        public override string ToString()
        {
            return Kind + ":" + Id;
        }
    }

    public readonly struct AoiInterestScope
    {
        public AoiInterestScope(float centerX, float centerY, float visibleRadius, float boundaryRadius = 0f, int maxEntities = 0)
        {
            CenterX = centerX;
            CenterY = centerY;
            VisibleRadius = Math.Max(0f, visibleRadius);
            BoundaryRadius = Math.Max(VisibleRadius, boundaryRadius <= 0f ? visibleRadius : boundaryRadius);
            MaxEntities = maxEntities;
        }

        public float CenterX { get; }

        public float CenterY { get; }

        public float VisibleRadius { get; }

        public float BoundaryRadius { get; }

        public int MaxEntities { get; }

        public bool HasRadius => VisibleRadius > 0f;
    }

    public readonly struct AoiEntitySample
    {
        public AoiEntitySample(AoiEntityKey key, float x, float y, int priority = 0, int layer = 0, int ownerId = 0, byte flags = 0)
        {
            Key = key;
            X = x;
            Y = y;
            Priority = priority;
            Layer = layer;
            OwnerId = ownerId;
            Flags = flags;
        }

        public AoiEntityKey Key { get; }

        public float X { get; }

        public float Y { get; }

        public int Priority { get; }

        public int Layer { get; }

        public int OwnerId { get; }

        public byte Flags { get; }
    }

    public enum AoiInterestTransition
    {
        None = 0,
        Enter = 1,
        Stay = 2,
        Leave = 3
    }

    public readonly struct AoiInterestChange
    {
        public AoiInterestChange(AoiEntityKey key, AoiInterestTransition transition, float distanceSquared)
            : this(key, transition, distanceSquared, 0, 0, 0, -1)
        {
        }

        public AoiInterestChange(AoiEntityKey key, AoiInterestTransition transition, float distanceSquared, int layer, int ownerId, byte flags)
            : this(key, transition, distanceSquared, layer, ownerId, flags, -1)
        {
        }

        public AoiInterestChange(
            AoiEntityKey key,
            AoiInterestTransition transition,
            float distanceSquared,
            int layer,
            int ownerId,
            byte flags,
            int sourceIndex)
        {
            Key = key;
            Transition = transition;
            DistanceSquared = distanceSquared;
            Layer = layer;
            OwnerId = ownerId;
            Flags = flags;
            SourceIndex = sourceIndex;
        }

        public AoiEntityKey Key { get; }

        public AoiInterestTransition Transition { get; }

        public float DistanceSquared { get; }

        public int Layer { get; }

        public int OwnerId { get; }

        public byte Flags { get; }

        /// <summary>Index of the source sample for visible changes, or -1 for synthetic leaves.</summary>
        public int SourceIndex { get; }

        public bool IsVisible => Transition == AoiInterestTransition.Enter || Transition == AoiInterestTransition.Stay;
    }

    public sealed class AoiInterestEvaluation
    {
        private static readonly IReadOnlyList<AoiInterestChange> EmptyChanges = Array.Empty<AoiInterestChange>();

        public AoiInterestEvaluation(IReadOnlyList<AoiInterestChange> changes, int visibleCount)
        {
            Changes = changes ?? EmptyChanges;
            VisibleCount = visibleCount;
        }

        public IReadOnlyList<AoiInterestChange> Changes { get; private set; }

        public int VisibleCount { get; private set; }

        internal void Reset(IReadOnlyList<AoiInterestChange> changes, int visibleCount)
        {
            Changes = changes ?? EmptyChanges;
            VisibleCount = visibleCount;
        }

    }

    public sealed class AoiInterestSet
    {
        private readonly Dictionary<AoiEntityKey, VisibleEntry> _visible = new Dictionary<AoiEntityKey, VisibleEntry>();
        private readonly List<AoiInterestChange> _transientChanges = new List<AoiInterestChange>();
        private readonly List<AoiEntityKey> _transientLeaves = new List<AoiEntityKey>();
        private readonly AoiInterestEvaluation _transientEvaluation = new AoiInterestEvaluation(Array.Empty<AoiInterestChange>(), 0);
        private int _evaluationGeneration;

        public int VisibleCount => _visible.Count;

        public bool IsVisible(AoiEntityKey key)
        {
            return key.IsValid && _visible.ContainsKey(key);
        }

        public void Clear()
        {
            _visible.Clear();
            _evaluationGeneration = 0;
        }

        public AoiInterestEvaluation Evaluate(IReadOnlyList<AoiEntitySample> samples, AoiInterestScope scope, bool forceFullBaseline = false)
        {
            return EvaluateCore(samples, scope, forceFullBaseline, useTransientBuffers: false);
        }

        /// <summary>
        /// Evaluates into reusable change buffers. Consume the result before the next transient
        /// evaluation on this interest set.
        /// </summary>
        public AoiInterestEvaluation EvaluateTransient(IReadOnlyList<AoiEntitySample> samples, AoiInterestScope scope, bool forceFullBaseline = false)
        {
            return EvaluateCore(samples, scope, forceFullBaseline, useTransientBuffers: true);
        }

        private AoiInterestEvaluation EvaluateCore(
            IReadOnlyList<AoiEntitySample> samples,
            AoiInterestScope scope,
            bool forceFullBaseline,
            bool useTransientBuffers)
        {
            if (forceFullBaseline)
            {
                _visible.Clear();
            }

            var generation = NextEvaluationGeneration();

            if (samples == null || samples.Count == 0)
            {
                return RemoveUnseenEntities(generation, useTransientBuffers);
            }

            var changes = useTransientBuffers
                ? PrepareTransientChanges(samples.Count)
                : new List<AoiInterestChange>(samples.Count);
            for (int i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];
                if (!sample.Key.IsValid)
                {
                    continue;
                }

                var distanceSquared = ComputeDistanceSquared(sample.X, sample.Y, scope);
                var wasVisible = _visible.ContainsKey(sample.Key);
                var shouldBeVisible = ShouldBeVisible(wasVisible, distanceSquared, scope);
                if (!shouldBeVisible)
                {
                    continue;
                }

                _visible[sample.Key] = new VisibleEntry(sample, generation);
                if (wasVisible)
                {
                    changes.Add(new AoiInterestChange(sample.Key, AoiInterestTransition.Stay, distanceSquared, sample.Layer, sample.OwnerId, sample.Flags, i));
                    continue;
                }

                changes.Add(new AoiInterestChange(sample.Key, AoiInterestTransition.Enter, distanceSquared, sample.Layer, sample.OwnerId, sample.Flags, i));
            }

            AppendUnseenLeaves(changes, generation, useTransientBuffers);
            return CreateEvaluation(changes, _visible.Count, useTransientBuffers);
        }

        private static bool ShouldBeVisible(bool wasVisible, float distanceSquared, AoiInterestScope scope)
        {
            if (!scope.HasRadius)
            {
                return true;
            }

            var radius = wasVisible ? scope.BoundaryRadius : scope.VisibleRadius;
            return distanceSquared <= radius * radius;
        }

        private static float ComputeDistanceSquared(float x, float y, AoiInterestScope scope)
        {
            if (!scope.HasRadius)
            {
                return 0f;
            }

            var dx = x - scope.CenterX;
            var dy = y - scope.CenterY;
            return dx * dx + dy * dy;
        }

        private AoiInterestEvaluation RemoveUnseenEntities(int generation, bool useTransientBuffers)
        {
            if (_visible.Count == 0)
            {
                var empty = useTransientBuffers
                    ? PrepareTransientChanges(0)
                    : (IReadOnlyList<AoiInterestChange>)Array.Empty<AoiInterestChange>();
                return CreateEvaluation(empty, 0, useTransientBuffers);
            }

            var changes = useTransientBuffers
                ? PrepareTransientChanges(_visible.Count)
                : new List<AoiInterestChange>(_visible.Count);
            AppendUnseenLeaves(changes, generation, useTransientBuffers);
            return CreateEvaluation(changes, _visible.Count, useTransientBuffers);
        }

        private void AppendUnseenLeaves(List<AoiInterestChange> changes, int generation, bool useTransientBuffers)
        {
            if (_visible.Count == 0)
            {
                return;
            }

            var leaves = useTransientBuffers ? _transientLeaves : new List<AoiEntityKey>();
            leaves.Clear();
            foreach (var pair in _visible)
            {
                if (pair.Value.SeenGeneration != generation)
                {
                    leaves.Add(pair.Key);
                }
            }

            leaves.Sort();

            for (int i = 0; i < leaves.Count; i++)
            {
                var key = leaves[i];
                if (_visible.TryGetValue(key, out var entry))
                {
                    _visible.Remove(key);
                    var sample = entry.Sample;
                    changes.Add(new AoiInterestChange(key, AoiInterestTransition.Leave, 0f, sample.Layer, sample.OwnerId, sample.Flags));
                    continue;
                }

                changes.Add(new AoiInterestChange(key, AoiInterestTransition.Leave, 0f));
            }
        }

        private int NextEvaluationGeneration()
        {
            if (_evaluationGeneration == int.MaxValue)
            {
                _evaluationGeneration = 0;
                if (_visible.Count > 0)
                {
                    var keys = new List<AoiEntityKey>(_visible.Keys);
                    for (var i = 0; i < keys.Count; i++)
                    {
                        var key = keys[i];
                        var entry = _visible[key];
                        _visible[key] = new VisibleEntry(entry.Sample, 0);
                    }
                }
            }

            return ++_evaluationGeneration;
        }

        private readonly struct VisibleEntry
        {
            public VisibleEntry(AoiEntitySample sample, int seenGeneration)
            {
                Sample = sample;
                SeenGeneration = seenGeneration;
            }

            public AoiEntitySample Sample { get; }

            public int SeenGeneration { get; }
        }

        private List<AoiInterestChange> PrepareTransientChanges(int capacity)
        {
            _transientChanges.Clear();
            if (_transientChanges.Capacity < capacity)
            {
                _transientChanges.Capacity = capacity;
            }

            return _transientChanges;
        }

        private AoiInterestEvaluation CreateEvaluation(
            IReadOnlyList<AoiInterestChange> changes,
            int visibleCount,
            bool useTransientBuffers)
        {
            if (!useTransientBuffers)
            {
                return new AoiInterestEvaluation(changes, visibleCount);
            }

            _transientEvaluation.Reset(changes, visibleCount);
            return _transientEvaluation;
        }
    }
}
