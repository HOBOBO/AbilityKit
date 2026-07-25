#nullable enable

using System;
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
        // 采样查找缓冲（成员复用，避免每帧 new Dictionary —— 高单位量下每帧 ~16KB 临时分配）。
        private readonly Dictionary<ShooterViewEntityKey, ShooterViewTransformComponentChange> _toByKeyBuffer = new();

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
            var transformChanges = SampleTransforms(from.TransformChanges, to.TransformChanges, t, ref isContinuousSample);

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
            IReadOnlyList<ShooterViewTransformComponentChange> fromChanges,
            IReadOnlyList<ShooterViewTransformComponentChange> toChanges,
            float t,
            ref bool isContinuousSample)
        {
            if (_options.TransformMode != ShooterSnapshotComponentSamplingMode.Interpolate || fromChanges.Count == 0 || toChanges.Count == 0)
            {
                return fromChanges;
            }

            var toByKey = _toByKeyBuffer;
            toByKey.Clear();
            for (var i = 0; i < toChanges.Count; i++)
            {
                toByKey[toChanges[i].Key] = toChanges[i];
            }

            ShooterViewTransformComponentChange[]? sampled = null;
            for (var i = 0; i < fromChanges.Count; i++)
            {
                var from = fromChanges[i];
                if (!toByKey.TryGetValue(from.Key, out var to))
                {
                    continue;
                }

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
    }
}
