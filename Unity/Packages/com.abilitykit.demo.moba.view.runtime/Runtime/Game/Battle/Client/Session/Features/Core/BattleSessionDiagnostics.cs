using System;
using System.Collections.Generic;
using System.Diagnostics;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns compatibility diagnostics publications for one battle session.
    /// Source runtimes retain ownership of transports, worlds, and evaluators.
    /// </summary>
    internal sealed class BattleSessionDiagnostics : IDisposable
    {
        private static BattleSessionDiagnostics _debugControlOwner;
        private static bool _debugForceClientHashMismatch;

        private readonly BattleReplicationRuntime _replication;
        private bool _forceClientHashMismatch;
        private JitterBufferStatsSnapshot _jitterBufferStats;
        private TimeSyncStatsSnapshot _timeSyncStats;
        private Dictionary<string, TimeSyncStatsSnapshot> _timeSyncStatsByWorld;
        private ConfirmedAuthorityWorldStatsSnapshot _confirmedAuthorityWorldStats;
        private IBattleDiagnosticMetricSink _metricSink;
        private int _lastMetricFrame = BattleDiagnosticFrames.Invalid;

        private const int FrameMetricSampleInterval = 5;

        internal BattleSessionDiagnostics(BattleReplicationRuntime replication)
        {
            _replication = replication ?? throw new ArgumentNullException(nameof(replication));
        }

        internal static bool DebugForceClientHashMismatch
        {
            get => _debugControlOwner != null
                ? _debugControlOwner._forceClientHashMismatch
                : _debugForceClientHashMismatch;
            set
            {
                _debugForceClientHashMismatch = value;
                if (_debugControlOwner != null)
                {
                    _debugControlOwner._forceClientHashMismatch = value;
                }
            }
        }

        internal bool ShouldForceClientHashMismatch => _forceClientHashMismatch;

        internal MobaSynchronizationHealthSnapshot SynchronizationHealth =>
            _replication.SynchronizationHealth;

        internal SyncHealthReport SynchronizationHealthReport =>
            _replication.SynchronizationHealthReport;

        internal void TryBindMetricSink(IWorld world)
        {
            if (_metricSink != null || world?.Services == null) return;
            world.Services.TryResolve(out _metricSink);
        }

        internal void RecordFrameMetrics(BattleContext context)
        {
            var sink = _metricSink;
            if (sink == null || !sink.IsEnabled || context == null ||
                !BattleDiagnosticFrames.IsValid(context.LastFrame)) return;
            if (BattleDiagnosticFrames.IsValid(_lastMetricFrame) &&
                context.LastFrame - _lastMetricFrame < FrameMetricSampleInterval) return;

            try
            {
                var frame = context.LastFrame;
                var timestamp = Stopwatch.GetTimestamp();
                var prediction = context.PredictionStats;
                var worldId = new WorldId(context.Plan.World.WorldId);
                if (prediction != null)
                {
                    if (prediction.TryGetFrames(worldId, out var confirmed, out var predicted))
                    {
                        Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Prediction,
                            BattleDiagnosticFrameMetricKeys.PredictionConfirmedFrame, confirmed.Value);
                        Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Prediction,
                            BattleDiagnosticFrameMetricKeys.PredictionPredictedFrame, predicted.Value);
                        Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Prediction,
                            BattleDiagnosticFrameMetricKeys.PredictionAheadFrames, predicted.Value - confirmed.Value);
                    }

                    if (prediction.TryGetPredictionWindowStats(
                            worldId,
                            out var backlog,
                            out _,
                            out var window,
                            out var stalled))
                    {
                        Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Prediction,
                            BattleDiagnosticFrameMetricKeys.PredictionBacklog, backlog);
                        Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Prediction,
                            BattleDiagnosticFrameMetricKeys.PredictionWindow, window);
                        Flag(sink, frame, timestamp, BattleDiagnosticMetricCategory.Prediction,
                            BattleDiagnosticFrameMetricKeys.PredictionStalled, stalled);
                    }

                    Flag(sink, frame, timestamp, BattleDiagnosticMetricCategory.Rollback,
                        BattleDiagnosticFrameMetricKeys.RollbackActive, prediction.IsReplaying);
                    Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Rollback,
                        BattleDiagnosticFrameMetricKeys.RollbackReplayToFrame, prediction.ReplayToFrame.Value);
                    Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Rollback,
                        BattleDiagnosticFrameMetricKeys.RollbackLastFrame, prediction.LastRollbackFrame.Value);
                    Counter(sink, frame, timestamp, BattleDiagnosticMetricCategory.Rollback,
                        BattleDiagnosticFrameMetricKeys.RollbackTotal, prediction.TotalRollbackCount);
                    Counter(sink, frame, timestamp, BattleDiagnosticMetricCategory.Rollback,
                        BattleDiagnosticFrameMetricKeys.RollbackRestoreFailedTotal,
                        prediction.TotalRollbackRestoreFailed);
                }

                var network = _jitterBufferStats;
                if (network != null)
                {
                    Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Network,
                        BattleDiagnosticFrameMetricKeys.NetworkDelayFrames, network.DelayFrames);
                    Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Network,
                        BattleDiagnosticFrameMetricKeys.NetworkBufferedCount, network.BufferedCount);
                    Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Network,
                        BattleDiagnosticFrameMetricKeys.NetworkTargetGap,
                        network.TargetFrame - network.LastConsumedFrame);
                    Counter(sink, frame, timestamp, BattleDiagnosticMetricCategory.Network,
                        BattleDiagnosticFrameMetricKeys.NetworkDuplicateTotal, network.DuplicateCount);
                    Counter(sink, frame, timestamp, BattleDiagnosticMetricCategory.Network,
                        BattleDiagnosticFrameMetricKeys.NetworkLateTotal, network.LateCount);
                }

                _lastMetricFrame = frame;
            }
            catch
            {
                // Diagnostics sampling must never affect battle session ticks.
            }
        }

        private static void Gauge(IBattleDiagnosticMetricSink sink, int frame, long timestamp,
            BattleDiagnosticMetricCategory category, string metric, double value) =>
            sink.TryRecordMetric(frame, timestamp, category, BattleDiagnosticMetricValueKind.Gauge, metric, value);

        private static void Counter(IBattleDiagnosticMetricSink sink, int frame, long timestamp,
            BattleDiagnosticMetricCategory category, string metric, double value) =>
            sink.TryRecordMetric(frame, timestamp, category, BattleDiagnosticMetricValueKind.Counter, metric, value);

        private static void Flag(IBattleDiagnosticMetricSink sink, int frame, long timestamp,
            BattleDiagnosticMetricCategory category, string metric, bool value) =>
            sink.TryRecordMetric(frame, timestamp, category, BattleDiagnosticMetricValueKind.Flag, metric, value ? 1d : 0d);

        internal void PublishDebugControls()
        {
            _forceClientHashMismatch = _debugForceClientHashMismatch;
            _debugControlOwner = this;
        }

        internal void PublishJitterBuffer(JitterBufferStatsSnapshot snapshot)
        {
            _jitterBufferStats = snapshot;
            BattleFlowDebugProvider.JitterBufferStats = snapshot;
        }

        internal void PublishTimeSync(
            TimeSyncStatsSnapshot current,
            Dictionary<string, TimeSyncStatsSnapshot> byWorld)
        {
            _timeSyncStats = current;
            _timeSyncStatsByWorld = byWorld;
            BattleFlowDebugProvider.TimeSyncStats = current;
            BattleFlowDebugProvider.TimeSyncStatsByWorld = byWorld;
        }

        internal void InitializeConfirmedAuthority(string worldId)
        {
            var snapshot = new ConfirmedAuthorityWorldStatsSnapshot
            {
                WorldId = worldId,
                RecentViewEvents = null,
            };
            _confirmedAuthorityWorldStats = snapshot;
            BattleFlowDebugProvider.ConfirmedAuthorityWorldStats = snapshot;
        }

        internal void UpdateConfirmedAuthority(
            int confirmedFrame,
            int predictedFrame,
            int inputTargetFrame,
            int driveTargetFrame,
            int lastTickedFrame,
            int viewEventTotal,
            string[] recentViewEvents)
        {
            var snapshot = _confirmedAuthorityWorldStats;
            if (snapshot == null) return;

            snapshot.ConfirmedFrame = confirmedFrame;
            snapshot.PredictedFrame = predictedFrame;
            snapshot.AuthorityInputTargetFrame = inputTargetFrame;
            snapshot.AuthorityDriveTargetFrame = driveTargetFrame;
            snapshot.AuthorityLastTickedFrame = lastTickedFrame;
            snapshot.ViewEventTotal = viewEventTotal;
            snapshot.RecentViewEvents = recentViewEvents;
        }

        internal void ClearJitterBuffer()
        {
            if (ReferenceEquals(BattleFlowDebugProvider.JitterBufferStats, _jitterBufferStats))
            {
                BattleFlowDebugProvider.JitterBufferStats = null;
            }
            _jitterBufferStats = null;
        }

        internal void ClearTimeSync()
        {
            if (ReferenceEquals(BattleFlowDebugProvider.TimeSyncStats, _timeSyncStats))
            {
                BattleFlowDebugProvider.TimeSyncStats = null;
            }
            if (ReferenceEquals(
                    BattleFlowDebugProvider.TimeSyncStatsByWorld,
                    _timeSyncStatsByWorld))
            {
                BattleFlowDebugProvider.TimeSyncStatsByWorld = null;
            }
            _timeSyncStats = null;
            _timeSyncStatsByWorld = null;
        }

        internal void ClearConfirmedAuthority()
        {
            if (ReferenceEquals(
                    BattleFlowDebugProvider.ConfirmedAuthorityWorldStats,
                    _confirmedAuthorityWorldStats))
            {
                BattleFlowDebugProvider.ConfirmedAuthorityWorldStats = null;
            }
            _confirmedAuthorityWorldStats = null;
        }

        public void Dispose()
        {
            if (ReferenceEquals(_debugControlOwner, this))
            {
                _debugControlOwner = null;
            }

            ClearJitterBuffer();
            ClearTimeSync();
            ClearConfirmedAuthority();
            _metricSink = null;
            _lastMetricFrame = BattleDiagnosticFrames.Invalid;
        }
    }
}
