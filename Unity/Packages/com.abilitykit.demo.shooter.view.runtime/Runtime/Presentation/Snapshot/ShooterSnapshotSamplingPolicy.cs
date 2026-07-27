#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public enum ShooterSnapshotComponentSamplingMode
    {
        Step = 1,
        Interpolate = 2
    }

    public sealed class ShooterSnapshotSamplingPolicyOptions
    {
        public ShooterSnapshotComponentSamplingMode TransformMode { get; set; } = ShooterSnapshotComponentSamplingMode.Interpolate;

        public ShooterSnapshotComponentSamplingMode HealthMode { get; set; } = ShooterSnapshotComponentSamplingMode.Step;

        public ShooterSnapshotComponentSamplingMode ScoreMode { get; set; } = ShooterSnapshotComponentSamplingMode.Step;

        public ShooterSnapshotComponentSamplingMode ProjectileLifetimeMode { get; set; } = ShooterSnapshotComponentSamplingMode.Step;
    }

    public sealed class ShooterSnapshotSamplingPolicy
    {
        public static ShooterSnapshotSamplingPolicy Default { get; } = new ShooterSnapshotSamplingPolicy(new ShooterSnapshotSamplingPolicyOptions());

        private readonly ShooterSnapshotSamplingPolicyOptions _options;
        // The key lookup and index mapping are rebuilt once per snapshot window, then reused by render-frame samples.
        private readonly Dictionary<ShooterViewEntityKey, int> _toIndexByKeyBuffer = new();
        private readonly ReusableTransformList _transientTransformBuffer = new ReusableTransformList();
        private int[] _toIndexByFromIndex = Array.Empty<int>();
        private IReadOnlyList<ShooterViewTransformComponentChange>? _mappedFromChanges;
        private IReadOnlyList<ShooterViewTransformComponentChange>? _mappedToChanges;
        private ulong _mappedFromSequence;
        private ulong _mappedToSequence;
        private int _mappedFromFrame;
        private int _mappedToFrame;
        private ShooterViewBatchSource _mappedFromSource;
        private ShooterViewBatchSource _mappedToSource;
        private ShooterViewSnapshotKind _mappedFromKind;
        private ShooterViewSnapshotKind _mappedToKind;

        public ShooterSnapshotSamplingPolicy(ShooterSnapshotSamplingPolicyOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public ShooterSnapshotViewBatch Sample(
            in ShooterSnapshotViewBatch from,
            in ShooterSnapshotViewBatch to,
            float playbackFrame,
            out bool isContinuousSample)
        {
            return SampleCore(in from, in to, playbackFrame, useTransientBuffer: false, out isContinuousSample);
        }

        internal ShooterSnapshotViewBatch SampleTransient(
            in ShooterSnapshotViewBatch from,
            in ShooterSnapshotViewBatch to,
            float playbackFrame,
            out bool isContinuousSample)
        {
            return SampleCore(in from, in to, playbackFrame, useTransientBuffer: true, out isContinuousSample);
        }

        private ShooterSnapshotViewBatch SampleCore(
            in ShooterSnapshotViewBatch from,
            in ShooterSnapshotViewBatch to,
            float playbackFrame,
            bool useTransientBuffer,
            out bool isContinuousSample)
        {
            isContinuousSample = false;
            if (IsSameBatch(in from, in to) || from.Frame >= to.Frame || playbackFrame <= from.Frame)
            {
                return from;
            }

            if (playbackFrame >= to.Frame)
            {
                return to;
            }

            var t = (playbackFrame - from.Frame) / (to.Frame - from.Frame);
            var transformChanges = SampleTransforms(
                in from,
                in to,
                t,
                useTransientBuffer,
                ref isContinuousSample);

            return new ShooterSnapshotViewBatch(
                from.WorldId,
                from.Frame,
                from.Sequence,
                from.SnapshotKind,
                from.Source,
                from.EntityChanges,
                from.RemovedEntities,
                transformChanges,
                SampleStep(from.HealthChanges, to.HealthChanges, _options.HealthMode),
                SampleStep(from.ScoreChanges, to.ScoreChanges, _options.ScoreMode),
                SampleStep(from.ProjectileLifetimeChanges, to.ProjectileLifetimeChanges, _options.ProjectileLifetimeMode),
                from.Events);
        }

        private IReadOnlyList<ShooterViewTransformComponentChange> SampleTransforms(
            in ShooterSnapshotViewBatch fromBatch,
            in ShooterSnapshotViewBatch toBatch,
            float t,
            bool useTransientBuffer,
            ref bool isContinuousSample)
        {
            var fromChanges = fromBatch.TransformChanges;
            var toChanges = toBatch.TransformChanges;
            if (_options.TransformMode != ShooterSnapshotComponentSamplingMode.Interpolate || fromChanges.Count == 0 || toChanges.Count == 0)
            {
                return fromChanges;
            }

            EnsureTransformIndexMapping(in fromBatch, in toBatch);

            if (useTransientBuffer)
            {
                return SampleTransformsTransient(fromChanges, toChanges, t, ref isContinuousSample);
            }

            ShooterViewTransformComponentChange[]? sampled = null;
            for (var i = 0; i < fromChanges.Count; i++)
            {
                var from = fromChanges[i];
                var toIndex = _toIndexByFromIndex[i];
                if (toIndex < 0)
                {
                    continue;
                }

                var to = toChanges[toIndex];
                sampled ??= CopyTransforms(fromChanges);
                sampled[i] = new ShooterViewTransformComponentChange(
                    from.Key,
                    Lerp(from.X, to.X, t),
                    Lerp(from.Y, to.Y, t),
                    Lerp(from.FacingX, to.FacingX, t),
                    Lerp(from.FacingY, to.FacingY, t),
                    Lerp(from.VelocityX, to.VelocityX, t),
                    Lerp(from.VelocityY, to.VelocityY, t));
                isContinuousSample = true;
            }

            return sampled ?? fromChanges;
        }

        private IReadOnlyList<ShooterViewTransformComponentChange> SampleTransformsTransient(
            IReadOnlyList<ShooterViewTransformComponentChange> fromChanges,
            IReadOnlyList<ShooterViewTransformComponentChange> toChanges,
            float t,
            ref bool isContinuousSample)
        {
            _transientTransformBuffer.Resize(fromChanges.Count);
            for (var i = 0; i < fromChanges.Count; i++)
            {
                var from = fromChanges[i];
                var toIndex = _toIndexByFromIndex[i];
                if (toIndex < 0)
                {
                    _transientTransformBuffer.Set(i, in from);
                    continue;
                }

                var to = toChanges[toIndex];
                var sampled = new ShooterViewTransformComponentChange(
                    from.Key,
                    Lerp(from.X, to.X, t),
                    Lerp(from.Y, to.Y, t),
                    Lerp(from.FacingX, to.FacingX, t),
                    Lerp(from.FacingY, to.FacingY, t),
                    Lerp(from.VelocityX, to.VelocityX, t),
                    Lerp(from.VelocityY, to.VelocityY, t));
                _transientTransformBuffer.Set(i, in sampled);
                isContinuousSample = true;
            }

            return isContinuousSample ? _transientTransformBuffer : fromChanges;
        }

        private void EnsureTransformIndexMapping(
            in ShooterSnapshotViewBatch fromBatch,
            in ShooterSnapshotViewBatch toBatch)
        {
            var fromChanges = fromBatch.TransformChanges;
            var toChanges = toBatch.TransformChanges;
            if (ReferenceEquals(_mappedFromChanges, fromChanges) &&
                ReferenceEquals(_mappedToChanges, toChanges) &&
                _mappedFromSequence == fromBatch.Sequence &&
                _mappedToSequence == toBatch.Sequence &&
                _mappedFromFrame == fromBatch.Frame &&
                _mappedToFrame == toBatch.Frame &&
                _mappedFromSource == fromBatch.Source &&
                _mappedToSource == toBatch.Source &&
                _mappedFromKind == fromBatch.SnapshotKind &&
                _mappedToKind == toBatch.SnapshotKind)
            {
                return;
            }

            if (_toIndexByFromIndex.Length < fromChanges.Count)
            {
                var capacity = Math.Max(fromChanges.Count, Math.Max(16, _toIndexByFromIndex.Length * 2));
                _toIndexByFromIndex = new int[capacity];
            }

            _toIndexByKeyBuffer.Clear();
            for (var i = 0; i < toChanges.Count; i++)
            {
                _toIndexByKeyBuffer[toChanges[i].Key] = i;
            }

            for (var i = 0; i < fromChanges.Count; i++)
            {
                _toIndexByFromIndex[i] = _toIndexByKeyBuffer.TryGetValue(fromChanges[i].Key, out var toIndex)
                    ? toIndex
                    : -1;
            }

            _mappedFromChanges = fromChanges;
            _mappedToChanges = toChanges;
            _mappedFromSequence = fromBatch.Sequence;
            _mappedToSequence = toBatch.Sequence;
            _mappedFromFrame = fromBatch.Frame;
            _mappedToFrame = toBatch.Frame;
            _mappedFromSource = fromBatch.Source;
            _mappedToSource = toBatch.Source;
            _mappedFromKind = fromBatch.SnapshotKind;
            _mappedToKind = toBatch.SnapshotKind;
        }

        private static IReadOnlyList<T> SampleStep<T>(
            IReadOnlyList<T> fromChanges,
            IReadOnlyList<T> toChanges,
            ShooterSnapshotComponentSamplingMode mode)
        {
            return mode == ShooterSnapshotComponentSamplingMode.Step ? fromChanges : toChanges;
        }

        private static ShooterViewTransformComponentChange[] CopyTransforms(IReadOnlyList<ShooterViewTransformComponentChange> changes)
        {
            var copy = new ShooterViewTransformComponentChange[changes.Count];
            for (var i = 0; i < changes.Count; i++)
            {
                copy[i] = changes[i];
            }

            return copy;
        }

        private static bool IsSameBatch(in ShooterSnapshotViewBatch from, in ShooterSnapshotViewBatch to)
        {
            return from.Sequence == to.Sequence &&
                from.Frame == to.Frame &&
                from.Source == to.Source &&
                from.SnapshotKind == to.SnapshotKind;
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + ((to - from) * t);
        }

        private sealed class ReusableTransformList : IReadOnlyList<ShooterViewTransformComponentChange>
        {
            private ShooterViewTransformComponentChange[] _items = Array.Empty<ShooterViewTransformComponentChange>();

            public int Count { get; private set; }

            public ShooterViewTransformComponentChange this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                    return _items[index];
                }
            }

            public void Resize(int count)
            {
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
                if (_items.Length < count)
                {
                    var capacity = Math.Max(count, Math.Max(16, _items.Length * 2));
                    _items = new ShooterViewTransformComponentChange[capacity];
                }

                Count = count;
            }

            public void Set(int index, in ShooterViewTransformComponentChange value)
            {
                _items[index] = value;
            }

            public IEnumerator<ShooterViewTransformComponentChange> GetEnumerator()
            {
                for (var i = 0; i < Count; i++)
                {
                    yield return _items[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
