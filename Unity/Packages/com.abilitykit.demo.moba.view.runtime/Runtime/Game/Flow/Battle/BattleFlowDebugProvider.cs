namespace AbilityKit.Game.Flow
{
    using System.Collections.Generic;

    public static class BattleFlowDebugProvider
    {
        public static BattleContext Current { get; set; }

        public static JitterBufferStatsSnapshot JitterBufferStats { get; set; }

        public static TimeSyncStatsSnapshot TimeSyncStats { get; set; }

        public static Dictionary<string, TimeSyncStatsSnapshot> TimeSyncStatsByWorld { get; set; }
    }

    public sealed class JitterBufferStatsSnapshot
    {
        public int DelayFrames;
        public string MissingMode;
        public int TargetFrame;
        public int MaxReceivedFrame;
        public int LastConsumedFrame;
        public int BufferedCount;
        public int MinBufferedFrame;

        public long AddedCount;
        public long DuplicateCount;
        public long LateCount;
        public long ConsumedCount;
        public long FilledDefaultCount;
    }

    public sealed class TimeSyncStatsSnapshot
    {
        public uint OpCode;
        public int IntervalMs;
        public double Alpha;
        public int TimeoutMs;

        public bool HasAnchor;
        public long AnchorStartServerTicks;
        public long AnchorServerTickFrequency;
        public int AnchorStartFrame;
        public double AnchorFixedDeltaSeconds;

        public bool HasClockSync;
        public double OffsetSecondsEwma;
        public double RttSecondsEwma;
        public int Samples;

        public int IdealFrameRaw;
        public int IdealFrameSafetyMarginFrames;
        public int IdealFrameLimit;
    }
}
