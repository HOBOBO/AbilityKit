using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    [Flags]
    public enum PresentationRateChannel
    {
        None = 0,
        Action = 1 << 0,
        SkillTimeline = 1 << 1,
        Vfx = 1 << 2,
        CueLifetime = 1 << 3,
        Camera = 1 << 4,
        FloatingText = 1 << 5,
        ProjectileFollow = 1 << 6,
        All = int.MaxValue,
    }

    public enum PresentationRateMode
    {
        Multiply = 0,
        Override = 1,
        Pause = 2,
    }

    /// <summary>
    /// One named rate contribution. Sources are intentionally data-only so they
    /// can be owned by a session, view, actor, skill, or individual cue.
    /// </summary>
    public readonly struct PresentationRateSource
    {
        public PresentationRateSource(
            string sourceId,
            PresentationRateChannel channels,
            float multiplier = 1f,
            PresentationRateMode mode = PresentationRateMode.Multiply,
            int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("A presentation rate source requires an ID.", nameof(sourceId));
            if (channels == PresentationRateChannel.None)
                throw new ArgumentOutOfRangeException(nameof(channels));
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
                throw new ArgumentOutOfRangeException(nameof(multiplier));

            SourceId = sourceId;
            Channels = channels;
            Multiplier = multiplier;
            Mode = mode;
            Priority = priority;
        }

        public string SourceId { get; }
        public PresentationRateChannel Channels { get; }
        public float Multiplier { get; }
        public PresentationRateMode Mode { get; }
        public int Priority { get; }
    }

    /// <summary>Stable explanation of the rate selected for one channel.</summary>
    public readonly struct PresentationRateResolution
    {
        public PresentationRateResolution(float effectiveRate, float multiplier, string controlSourceId,
            PresentationRateMode controlMode, int controlPriority, int sourceCount)
        {
            EffectiveRate = effectiveRate;
            Multiplier = multiplier;
            ControlSourceId = controlSourceId ?? string.Empty;
            ControlMode = controlMode;
            ControlPriority = controlPriority;
            SourceCount = sourceCount;
        }

        public float EffectiveRate { get; }
        public float Multiplier { get; }
        public string ControlSourceId { get; }
        public PresentationRateMode ControlMode { get; }
        public int ControlPriority { get; }
        public int SourceCount { get; }
    }

    public readonly struct PresentationTimeContext
    {
        public PresentationTimeContext(
            float wallDeltaSeconds,
            float deltaSeconds,
            float effectiveRate,
            PresentationRateChannel channel)
        {
            WallDeltaSeconds = wallDeltaSeconds;
            DeltaSeconds = deltaSeconds;
            EffectiveRate = effectiveRate;
            Channel = channel;
        }

        public float WallDeltaSeconds { get; }
        public float DeltaSeconds { get; }
        public float EffectiveRate { get; }
        public PresentationRateChannel Channel { get; }
        public bool IsPaused => EffectiveRate <= 0f;
    }

    /// <summary>
    /// Resolves all active rate sources for a presentation channel.
    /// Multiply sources compose multiplicatively. The highest-priority control
    /// source (Override or Pause) selects the control rate before multiplication.
    /// </summary>
    public sealed class PresentationRateResolver
    {
        private readonly Dictionary<string, PresentationRateSource> _sources =
            new Dictionary<string, PresentationRateSource>(StringComparer.Ordinal);

        public int Count => _sources.Count;

        public void Set(in PresentationRateSource source)
        {
            _sources[source.SourceId] = source;
        }

        public bool Remove(string sourceId)
        {
            return !string.IsNullOrEmpty(sourceId) && _sources.Remove(sourceId);
        }

        public void Clear() => _sources.Clear();

        public void CopySources(List<PresentationRateSource> destination, PresentationRateChannel channels = PresentationRateChannel.All)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            foreach (var source in _sources.Values)
                if ((source.Channels & channels) != 0) destination.Add(source);
            destination.Sort((a, b) => string.CompareOrdinal(a.SourceId, b.SourceId));
        }

        public PresentationRateResolution Explain(PresentationRateChannel channel)
        {
            var controlPriority = int.MinValue;
            var controlId = string.Empty;
            var controlMode = PresentationRateMode.Multiply;
            var controlRate = 1f;
            var multiplier = 1f;
            var count = 0;
            foreach (var source in _sources.Values)
            {
                if ((source.Channels & channel) == 0) continue;
                count++;
                if (source.Mode == PresentationRateMode.Multiply) multiplier *= source.Multiplier;
                else if (source.Priority > controlPriority ||
                    (source.Priority == controlPriority && string.CompareOrdinal(source.SourceId, controlId) > 0))
                {
                    controlPriority = source.Priority;
                    controlId = source.SourceId;
                    controlMode = source.Mode;
                    controlRate = source.Mode == PresentationRateMode.Pause ? 0f : source.Multiplier;
                }
            }
            var effective = multiplier * controlRate;
            if (float.IsNaN(effective) || float.IsInfinity(effective)) effective = 0f;
            return new PresentationRateResolution(Math.Max(0f, effective), multiplier, controlId,
                controlMode, controlPriority, count);
        }

        public float ResolveRate(PresentationRateChannel channel)
        {
            if (channel == PresentationRateChannel.None) return 1f;

            var controlPriority = int.MinValue;
            var controlId = string.Empty;
            var controlRate = 1f;
            var multiplier = 1f;

            foreach (var pair in _sources)
            {
                var source = pair.Value;
                if ((source.Channels & channel) == 0) continue;

                if (source.Mode == PresentationRateMode.Multiply)
                {
                    multiplier *= source.Multiplier;
                    continue;
                }

                if (source.Priority > controlPriority ||
                    (source.Priority == controlPriority &&
                     string.CompareOrdinal(source.SourceId, controlId) > 0))
                {
                    controlPriority = source.Priority;
                    controlId = source.SourceId;
                    controlRate = source.Mode == PresentationRateMode.Pause
                        ? 0f
                        : source.Multiplier;
                }
            }

            var rate = multiplier * controlRate;
            if (float.IsNaN(rate) || float.IsInfinity(rate)) return 0f;
            return Math.Max(0f, rate);
        }

        public PresentationTimeContext Resolve(float wallDeltaSeconds, PresentationRateChannel channel)
        {
            var safeWallDelta = IsFiniteNonNegative(wallDeltaSeconds) ? wallDeltaSeconds : 0f;
            var rate = ResolveRate(channel);
            return new PresentationTimeContext(
                safeWallDelta,
                safeWallDelta * rate,
                rate,
                channel);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
