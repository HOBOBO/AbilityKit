namespace AbilityKit.Game.Flow
{
    using System;
    using System.Collections.Generic;

    public static class BattleFlowDebugProvider
    {
        private static readonly object ScopeGate = new object();
        private static readonly Dictionary<string, BattleContext> ContextsByScope =
            new Dictionary<string, BattleContext>(StringComparer.Ordinal);
        private static readonly Dictionary<string, JitterBufferStatsSnapshot> JitterBufferStatsByScope =
            new Dictionary<string, JitterBufferStatsSnapshot>(StringComparer.Ordinal);
        private static readonly Dictionary<string, TimeSyncStatsSnapshot> TimeSyncStatsByScope =
            new Dictionary<string, TimeSyncStatsSnapshot>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Dictionary<string, TimeSyncStatsSnapshot>> TimeSyncWorldStatsByScope =
            new Dictionary<string, Dictionary<string, TimeSyncStatsSnapshot>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, ConfirmedAuthorityWorldStatsSnapshot> ConfirmedAuthorityStatsByScope =
            new Dictionary<string, ConfirmedAuthorityWorldStatsSnapshot>(StringComparer.Ordinal);
        private static readonly Dictionary<string, BattleHudFeature> HudsByScope =
            new Dictionary<string, BattleHudFeature>(StringComparer.Ordinal);
        private static readonly Dictionary<string, BattleViewFeature> ViewsByScope =
            new Dictionary<string, BattleViewFeature>(StringComparer.Ordinal);
        private static readonly Dictionary<string, ConfirmedBattleViewFeature> ConfirmedViewsByScope =
            new Dictionary<string, ConfirmedBattleViewFeature>(StringComparer.Ordinal);

        /// <summary>仅用于 development single-active 兼容；正式调用方应按 scope 查询。</summary>
        public static BattleContext Current { get; set; }

        /// <summary>仅用于 development single-active 兼容；正式调用方应按 scope 查询。</summary>
        public static BattleHudFeature CurrentHud { get; set; }

        /// <summary>仅用于 development single-active 兼容；正式调用方应按 scope 查询。</summary>
        public static BattleViewFeature CurrentView { get; set; }

        /// <summary>仅用于 development single-active 兼容；正式调用方应按 scope 查询。</summary>
        public static ConfirmedBattleViewFeature CurrentConfirmedView { get; set; }

        public static bool TryGetContext(string scope, out BattleContext context) =>
            TryGetScoped(ContextsByScope, scope, out context);

        public static bool TryGetHud(string scope, out BattleHudFeature hud) =>
            TryGetScoped(HudsByScope, scope, out hud);

        public static bool TryGetView(string scope, out BattleViewFeature view) =>
            TryGetScoped(ViewsByScope, scope, out view);

        public static bool TryGetConfirmedView(
            string scope,
            out ConfirmedBattleViewFeature confirmedView) =>
            TryGetScoped(ConfirmedViewsByScope, scope, out confirmedView);

        public static bool TryGetJitterBufferStats(
            string scope,
            out JitterBufferStatsSnapshot snapshot) =>
            TryGetScoped(JitterBufferStatsByScope, scope, out snapshot);

        public static bool TryGetTimeSyncStats(
            string scope,
            out TimeSyncStatsSnapshot current,
            out Dictionary<string, TimeSyncStatsSnapshot> byWorld)
        {
            var hasCurrent = TryGetScoped(TimeSyncStatsByScope, scope, out current);
            var hasByWorld = TryGetScoped(TimeSyncWorldStatsByScope, scope, out byWorld);
            return hasCurrent && hasByWorld;
        }

        public static bool TryGetConfirmedAuthorityStats(
            string scope,
            out ConfirmedAuthorityWorldStatsSnapshot snapshot) =>
            TryGetScoped(ConfirmedAuthorityStatsByScope, scope, out snapshot);

        internal static void PublishContext(string scope, BattleContext context) =>
            PublishScoped(ContextsByScope, scope, context);

        internal static void PublishHud(string scope, BattleHudFeature hud) =>
            PublishScoped(HudsByScope, scope, hud);

        internal static void PublishView(string scope, BattleViewFeature view) =>
            PublishScoped(ViewsByScope, scope, view);

        internal static void PublishConfirmedView(
            string scope,
            ConfirmedBattleViewFeature confirmedView) =>
            PublishScoped(ConfirmedViewsByScope, scope, confirmedView);

        internal static void WithdrawContext(string scope, BattleContext context) =>
            WithdrawScoped(ContextsByScope, scope, context);

        internal static void WithdrawHud(string scope, BattleHudFeature hud) =>
            WithdrawScoped(HudsByScope, scope, hud);

        internal static void WithdrawView(string scope, BattleViewFeature view) =>
            WithdrawScoped(ViewsByScope, scope, view);

        internal static void WithdrawConfirmedView(
            string scope,
            ConfirmedBattleViewFeature confirmedView) =>
            WithdrawScoped(ConfirmedViewsByScope, scope, confirmedView);

        internal static void PublishJitterBufferStats(
            string scope,
            JitterBufferStatsSnapshot snapshot) =>
            PublishScoped(JitterBufferStatsByScope, scope, snapshot);

        internal static void PublishTimeSyncStats(
            string scope,
            TimeSyncStatsSnapshot current,
            Dictionary<string, TimeSyncStatsSnapshot> byWorld)
        {
            PublishScoped(TimeSyncStatsByScope, scope, current);
            PublishScoped(TimeSyncWorldStatsByScope, scope, byWorld);
        }

        internal static void PublishConfirmedAuthorityStats(
            string scope,
            ConfirmedAuthorityWorldStatsSnapshot snapshot) =>
            PublishScoped(ConfirmedAuthorityStatsByScope, scope, snapshot);

        internal static void WithdrawJitterBufferStats(
            string scope,
            JitterBufferStatsSnapshot snapshot) =>
            WithdrawScoped(JitterBufferStatsByScope, scope, snapshot);

        internal static void WithdrawTimeSyncStats(
            string scope,
            TimeSyncStatsSnapshot current,
            Dictionary<string, TimeSyncStatsSnapshot> byWorld)
        {
            WithdrawScoped(TimeSyncStatsByScope, scope, current);
            WithdrawScoped(TimeSyncWorldStatsByScope, scope, byWorld);
        }

        internal static void WithdrawConfirmedAuthorityStats(
            string scope,
            ConfirmedAuthorityWorldStatsSnapshot snapshot) =>
            WithdrawScoped(ConfirmedAuthorityStatsByScope, scope, snapshot);

        /// <summary>仅用于 development single-active 兼容；正式调用方应按 scope 查询。</summary>
        public static JitterBufferStatsSnapshot JitterBufferStats { get; set; }

        /// <summary>仅用于 development single-active 兼容；正式调用方应按 scope 查询。</summary>
        public static TimeSyncStatsSnapshot TimeSyncStats { get; set; }

        /// <summary>仅用于 development single-active 兼容；正式调用方应按 scope 查询。</summary>
        public static Dictionary<string, TimeSyncStatsSnapshot> TimeSyncStatsByWorld { get; set; }

        /// <summary>仅用于 development single-active 兼容；正式调用方应按 scope 查询。</summary>
        public static ConfirmedAuthorityWorldStatsSnapshot ConfirmedAuthorityWorldStats { get; set; }

        public static InputSubmissionStatsSnapshot InputSubmissionStats
        {
            get => InputSubmissionStatsProvider.Current;
            set => InputSubmissionStatsProvider.Current = value;
        }

        private static bool TryGetScoped<T>(
            Dictionary<string, T> publications,
            string scope,
            out T value)
            where T : class
        {
            value = null;
            if (string.IsNullOrWhiteSpace(scope)) return false;
            lock (ScopeGate)
            {
                return publications.TryGetValue(scope, out value);
            }
        }

        private static void PublishScoped<T>(
            Dictionary<string, T> publications,
            string scope,
            T value)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(scope) || value == null) return;
            lock (ScopeGate)
            {
                publications[scope] = value;
            }
        }

        private static void WithdrawScoped<T>(
            Dictionary<string, T> publications,
            string scope,
            T owner)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(scope) || owner == null) return;
            lock (ScopeGate)
            {
                if (publications.TryGetValue(scope, out var current) &&
                    ReferenceEquals(current, owner))
                {
                    publications.Remove(scope);
                }
            }
        }
    }

    public sealed class ConfirmedAuthorityWorldStatsSnapshot
    {
        public string WorldId;

        public int ConfirmedFrame;
        public int PredictedFrame;

        public int AuthorityInputTargetFrame;
        public int AuthorityDriveTargetFrame;
        public int AuthorityLastTickedFrame;

        public int ViewEventTotal;
        public string[] RecentViewEvents;
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
