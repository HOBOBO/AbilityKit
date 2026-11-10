using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    [Flags]
    public enum PresentationScaleChannel
    {
        None = 0,
        Avatar = 1 << 0,
        Model = 1 << 1,
        Vfx = 1 << 2,
        Cue = 1 << 3,
        All = int.MaxValue,
    }

    public enum PresentationScaleMode
    {
        Multiply = 0,
        Override = 1,
    }

    /// <summary>
    /// A named spatial scale contribution. The source id is the ownership key:
    /// setting the same id replaces the previous contribution instead of
    /// multiplying it again on every refresh.
    /// </summary>
    public readonly struct PresentationScaleSource
    {
        public PresentationScaleSource(
            string sourceId,
            PresentationScaleChannel channels,
            PresentationVector3 multiplier,
            PresentationScaleMode mode = PresentationScaleMode.Multiply,
            int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("A presentation scale source requires an ID.", nameof(sourceId));
            if (channels == PresentationScaleChannel.None)
                throw new ArgumentOutOfRangeException(nameof(channels));
            if (!IsFiniteNonNegative(multiplier.X) ||
                !IsFiniteNonNegative(multiplier.Y) ||
                !IsFiniteNonNegative(multiplier.Z))
                throw new ArgumentOutOfRangeException(nameof(multiplier));

            SourceId = sourceId;
            Channels = channels;
            Multiplier = multiplier;
            Mode = mode;
            Priority = priority;
        }

        public PresentationScaleSource(
            string sourceId,
            PresentationScaleChannel channels,
            float multiplier,
            PresentationScaleMode mode = PresentationScaleMode.Multiply,
            int priority = 0)
            : this(
                sourceId,
                channels,
                new PresentationVector3(multiplier, multiplier, multiplier),
                mode,
                priority)
        {
        }

        public string SourceId { get; }
        public PresentationScaleChannel Channels { get; }
        public PresentationVector3 Multiplier { get; }
        public PresentationScaleMode Mode { get; }
        public int Priority { get; }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }

    /// <summary>Stable explanation of scale composition for one channel.</summary>
    public readonly struct PresentationScaleResolution
    {
        public PresentationScaleResolution(PresentationVector3 effective, PresentationVector3 multiplier,
            string controlSourceId, int controlPriority, int sourceCount)
        {
            Effective = effective;
            Multiplier = multiplier;
            ControlSourceId = controlSourceId ?? string.Empty;
            ControlPriority = controlPriority;
            SourceCount = sourceCount;
        }

        public PresentationVector3 Effective { get; }
        public PresentationVector3 Multiplier { get; }
        public string ControlSourceId { get; }
        public int ControlPriority { get; }
        public int SourceCount { get; }
    }

    /// <summary>
    /// Resolves spatial scale from a stable base and named contributions.
    /// Resolution is always recomputed from the source table, so updates and
    /// removals cannot accumulate floating point multiplications.
    /// </summary>
    public sealed class PresentationScaleResolver
    {
        private readonly Dictionary<string, PresentationScaleSource> _sources =
            new Dictionary<string, PresentationScaleSource>(StringComparer.Ordinal);

        public int Count => _sources.Count;

        public void Set(in PresentationScaleSource source)
        {
            _sources[source.SourceId] = source;
        }

        public bool Remove(string sourceId)
        {
            return !string.IsNullOrEmpty(sourceId) && _sources.Remove(sourceId);
        }

        public void Clear() => _sources.Clear();

        public PresentationScaleResolution Explain(PresentationScaleChannel channel,
            PresentationVector3 baseScale = default)
        {
            var controlPriority = int.MinValue;
            var controlId = string.Empty;
            var control = PresentationVector3.One;
            var multiplier = PresentationVector3.One;
            var count = 0;
            foreach (var source in _sources.Values)
            {
                if ((source.Channels & channel) == 0) continue;
                count++;
                if (source.Mode == PresentationScaleMode.Multiply)
                    multiplier = Multiply(multiplier, source.Multiplier);
                else if (source.Priority > controlPriority ||
                    (source.Priority == controlPriority && string.CompareOrdinal(source.SourceId, controlId) > 0))
                {
                    controlPriority = source.Priority;
                    controlId = source.SourceId;
                    control = source.Multiplier;
                }
            }
            var effective = Multiply(Sanitize(baseScale, PresentationVector3.One), Multiply(control, multiplier));
            return new PresentationScaleResolution(effective, multiplier, controlId, controlPriority, count);
        }

        /// <summary>Copies a stable diagnostic snapshot without exposing the mutable source table.</summary>
        public void CopySources(List<PresentationScaleSource> destination, PresentationScaleChannel channels = PresentationScaleChannel.All)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            foreach (var source in _sources.Values)
                if ((source.Channels & channels) != 0) destination.Add(source);
            destination.Sort((a, b) => string.CompareOrdinal(a.SourceId, b.SourceId));
        }

        public PresentationVector3 Resolve(
            PresentationScaleChannel channel,
            PresentationVector3 baseScale = default)
        {
            if (channel == PresentationScaleChannel.None)
                return Sanitize(baseScale, PresentationVector3.One);

            var controlPriority = int.MinValue;
            var controlId = string.Empty;
            var control = PresentationVector3.One;
            var multiplier = PresentationVector3.One;

            foreach (var pair in _sources)
            {
                var source = pair.Value;
                if ((source.Channels & channel) == 0) continue;

                if (source.Mode == PresentationScaleMode.Multiply)
                {
                    multiplier = Multiply(multiplier, source.Multiplier);
                    continue;
                }

                if (source.Priority > controlPriority ||
                    (source.Priority == controlPriority &&
                     string.CompareOrdinal(source.SourceId, controlId) > 0))
                {
                    controlPriority = source.Priority;
                    controlId = source.SourceId;
                    control = source.Multiplier;
                }
            }

            return Multiply(Sanitize(baseScale, PresentationVector3.One), Multiply(control, multiplier));
        }

        public float ResolveScalar(PresentationScaleChannel channel, float baseScale = 1f)
        {
            var resolved = Resolve(channel, new PresentationVector3(baseScale, baseScale, baseScale));
            return resolved.X;
        }

        private static PresentationVector3 Multiply(PresentationVector3 a, PresentationVector3 b)
        {
            return new PresentationVector3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        }

        private static PresentationVector3 Sanitize(PresentationVector3 value, PresentationVector3 fallback)
        {
            return IsFiniteNonNegative(value.X) && IsFiniteNonNegative(value.Y) && IsFiniteNonNegative(value.Z)
                ? value
                : fallback;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
