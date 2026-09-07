#nullable enable

using System;

using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    /// <summary>Shared observation sampling bounds used by controllers, UI, and recording metadata.</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationSettings")]
    public sealed class ObservationSettings
    {
        public const double DefaultSampleIntervalSeconds = 0.2d;
        public const double MinSampleIntervalSeconds = 0.01d;
        public const double MaxSampleIntervalSeconds = 10d;

        public const int DefaultTimelineCapacity = 200;
        public const int MinTimelineCapacity = 1;
        public const int MaxTimelineCapacity = 10000;

        private double _sampleIntervalSeconds = DefaultSampleIntervalSeconds;
        private int _timelineCapacity = DefaultTimelineCapacity;

        public double SampleIntervalSeconds
        {
            get => _sampleIntervalSeconds;
            set => _sampleIntervalSeconds = ClampSampleIntervalSeconds(value);
        }

        public int TimelineCapacity
        {
            get => _timelineCapacity;
            set => _timelineCapacity = ClampTimelineCapacity(value);
        }

        public ObservationSettings() { }

        public ObservationSettings(int timelineCapacity, double sampleIntervalSeconds)
        {
            TimelineCapacity = timelineCapacity;
            SampleIntervalSeconds = sampleIntervalSeconds;
        }

        public static double ClampSampleIntervalSeconds(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (value < MinSampleIntervalSeconds) return MinSampleIntervalSeconds;
            return value > MaxSampleIntervalSeconds ? MaxSampleIntervalSeconds : value;
        }

        public static int ClampTimelineCapacity(int value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value < MinTimelineCapacity) return MinTimelineCapacity;
            return value > MaxTimelineCapacity ? MaxTimelineCapacity : value;
        }
    }
}
