using System;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public enum BattleDiagnosticCaptureMode
    {
        Off = 0,
        Metrics = 1,
        Events = 2,
        Full = 3
    }

    public readonly struct BattleDiagnosticCaptureOptions : IEquatable<BattleDiagnosticCaptureOptions>
    {
        public BattleDiagnosticCaptureOptions(
            BattleDiagnosticCaptureMode mode,
            BattleDiagnosticEventChannel enabledChannels,
            int stateSampleIntervalFrames = 1,
            int eventCapacity = BattleDiagnosticEventRingStore.DefaultCapacity,
            int retainedReadViewCount = BattleDiagnosticEventRingStore.DefaultRetainedReadViewCount,
            int metricCapacity = BattleDiagnosticMetricRingStore.DefaultCapacity)
        {
            if (!Enum.IsDefined(typeof(BattleDiagnosticCaptureMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (stateSampleIntervalFrames <= 0)
                throw new ArgumentOutOfRangeException(nameof(stateSampleIntervalFrames));
            if (eventCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(eventCapacity));
            if (retainedReadViewCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(retainedReadViewCount));
            if (metricCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(metricCapacity));

            Mode = mode;
            EnabledChannels = enabledChannels;
            StateSampleIntervalFrames = stateSampleIntervalFrames;
            EventCapacity = eventCapacity;
            RetainedReadViewCount = retainedReadViewCount;
            MetricCapacity = metricCapacity;
        }

        public BattleDiagnosticCaptureMode Mode { get; }
        public BattleDiagnosticEventChannel EnabledChannels { get; }
        public int StateSampleIntervalFrames { get; }
        public int EventCapacity { get; }
        public int RetainedReadViewCount { get; }
        public int MetricCapacity { get; }
        public bool CapturesMetrics => Mode == BattleDiagnosticCaptureMode.Metrics || Mode == BattleDiagnosticCaptureMode.Full;
        public bool CapturesEvents => Mode == BattleDiagnosticCaptureMode.Events || Mode == BattleDiagnosticCaptureMode.Full;
        public bool CapturesState => Mode == BattleDiagnosticCaptureMode.Full;

        public bool Equals(BattleDiagnosticCaptureOptions other)
        {
            return Mode == other.Mode && EnabledChannels == other.EnabledChannels &&
                   StateSampleIntervalFrames == other.StateSampleIntervalFrames &&
                   EventCapacity == other.EventCapacity &&
                   RetainedReadViewCount == other.RetainedReadViewCount &&
                   MetricCapacity == other.MetricCapacity;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticCaptureOptions other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Mode;
                hashCode = (hashCode * 397) ^ (int)EnabledChannels;
                hashCode = (hashCode * 397) ^ StateSampleIntervalFrames;
                hashCode = (hashCode * 397) ^ EventCapacity;
                hashCode = (hashCode * 397) ^ RetainedReadViewCount;
                hashCode = (hashCode * 397) ^ MetricCapacity;
                return hashCode;
            }
        }

        public static BattleDiagnosticCaptureOptions Full => new BattleDiagnosticCaptureOptions(
            BattleDiagnosticCaptureMode.Full,
            BattleDiagnosticEventChannel.All);

        public static BattleDiagnosticCaptureOptions RecommendedDefault
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Full;
#else
                return new BattleDiagnosticCaptureOptions(
                    BattleDiagnosticCaptureMode.Metrics,
                    BattleDiagnosticEventChannel.None,
                    stateSampleIntervalFrames: 30,
                    eventCapacity: 1024,
                    retainedReadViewCount: 1);
#endif
            }
        }
    }
}
