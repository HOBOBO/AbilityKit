using System;
using System.Collections.Generic;
using System.Diagnostics;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Flow
{
    public enum SessionLifecycleDiagnosticState
    {
        Created = 0,
        Starting = 1,
        Running = 2,
        Recovering = 3,
        Stopping = 4,
        Stopped = 5,
        Faulted = 6,
    }

    public readonly struct SessionLifecycleDiagnosticsSnapshot
    {
        public SessionLifecycleDiagnosticsSnapshot(
            int generation,
            SessionLifecycleDiagnosticState previousState,
            SessionLifecycleDiagnosticState state,
            int retryCount,
            string pendingOperation,
            TimeSpan lastStopLatency,
            string teardownFailure)
        {
            Generation = generation;
            PreviousState = previousState;
            State = state;
            RetryCount = retryCount;
            PendingOperation = pendingOperation ?? string.Empty;
            LastStopLatency = lastStopLatency;
            TeardownFailure = teardownFailure ?? string.Empty;
        }

        public int Generation { get; }
        public SessionLifecycleDiagnosticState PreviousState { get; }
        public SessionLifecycleDiagnosticState State { get; }
        public int RetryCount { get; }
        public bool HasPendingOperation => !string.IsNullOrEmpty(PendingOperation);
        public string PendingOperation { get; }
        public TimeSpan LastStopLatency { get; }
        public bool HasTeardownFailure => !string.IsNullOrEmpty(TeardownFailure);
        public string TeardownFailure { get; }
    }

    internal sealed class SessionLifecycleDiagnosticsRecorder
    {
        private readonly object _gate = new object();
        private int _generation;
        private SessionLifecycleDiagnosticState _previousState;
        private SessionLifecycleDiagnosticState _state = SessionLifecycleDiagnosticState.Created;
        private int _retryCount;
        private string _pendingOperation = string.Empty;
        private TimeSpan _lastStopLatency;
        private string _teardownFailure = string.Empty;

        internal SessionLifecycleDiagnosticsSnapshot Snapshot
        {
            get
            {
                lock (_gate)
                {
                    return new SessionLifecycleDiagnosticsSnapshot(
                        _generation,
                        _previousState,
                        _state,
                        _retryCount,
                        _pendingOperation,
                        _lastStopLatency,
                        _teardownFailure);
                }
            }
        }

        internal void BeginGeneration(int generation, SessionLifecycleDiagnosticState state)
        {
            lock (_gate)
            {
                _generation = generation;
                _previousState = _state;
                _state = state;
                _retryCount = 0;
                _pendingOperation = string.Empty;
                _lastStopLatency = TimeSpan.Zero;
                _teardownFailure = string.Empty;
            }
        }

        internal void Transition(SessionLifecycleDiagnosticState state)
        {
            lock (_gate)
            {
                if (_state == state) return;
                _previousState = _state;
                _state = state;
            }
        }

        internal void SetRetryCount(int retryCount)
        {
            lock (_gate) _retryCount = Math.Max(0, retryCount);
        }

        internal int BeginPendingOperation(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                throw new ArgumentException("A pending operation name is required.", nameof(operation));
            }

            lock (_gate)
            {
                _pendingOperation = operation;
                return _generation;
            }
        }

        internal void CompletePendingOperation(
            int generation,
            TimeSpan stopLatency,
            Exception teardownFailure = null,
            SessionLifecycleDiagnosticState? finalState = null)
        {
            lock (_gate)
            {
                if (_generation != generation) return;
                _pendingOperation = string.Empty;
                _lastStopLatency = stopLatency < TimeSpan.Zero ? TimeSpan.Zero : stopLatency;
                _teardownFailure = teardownFailure?.ToString() ?? string.Empty;
                if (finalState.HasValue && _state != finalState.Value)
                {
                    _previousState = _state;
                    _state = finalState.Value;
                }
            }
        }

        internal void RecordFailure(Exception failure)
        {
            lock (_gate) _teardownFailure = failure?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Owns compatibility diagnostics publications for one battle session.
    /// Source runtimes retain ownership of transports, worlds, and evaluators.
    /// </summary>
    internal sealed class BattleSessionDiagnostics : IDisposable
    {
        private static BattleSessionDiagnostics _debugControlOwner;
        private static bool _debugForceClientHashMismatch;

        private readonly BattleReplicationRuntime _replication;
        private string _scope = string.Empty;
        private bool _forceClientHashMismatch;
        private JitterBufferStatsSnapshot _jitterBufferStats;
        private TimeSyncStatsSnapshot _timeSyncStats;
        private Dictionary<string, TimeSyncStatsSnapshot> _timeSyncStatsByWorld;
        private ConfirmedAuthorityWorldStatsSnapshot _confirmedAuthorityWorldStats;
        private IWorld _metricSinkWorld;
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

        internal void BindScope(string scope)
        {
            var normalized = scope ?? string.Empty;
            if (string.Equals(_scope, normalized, StringComparison.Ordinal)) return;

            WithdrawScopedPublications();
            _scope = normalized;
            PublishScopedPublications();
        }

        internal void BindMetricSink(IWorld world, IBattleDiagnosticMetricSink metricSink)
        {
            if (ReferenceEquals(_metricSinkWorld, world) && ReferenceEquals(_metricSink, metricSink)) return;

            _metricSinkWorld = world;
            _metricSink = metricSink;
            _lastMetricFrame = BattleDiagnosticFrames.Invalid;
        }

        internal bool ClearMetricSink(IWorld ownerWorld)
        {
            if (!ReferenceEquals(_metricSinkWorld, ownerWorld)) return false;

            _metricSinkWorld = null;
            _metricSink = null;
            _lastMetricFrame = BattleDiagnosticFrames.Invalid;
            return true;
        }

        internal void RecordFrameMetrics(
            BattleContext context,
            in BattleSessionTickProjection tick)
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

                Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Simulation,
                    BattleDiagnosticFrameMetricKeys.SimulationLastUpdateSteps, tick.LastUpdateSteps);
                Gauge(sink, frame, timestamp, BattleDiagnosticMetricCategory.Simulation,
                    BattleDiagnosticFrameMetricKeys.SimulationBacklogSteps, tick.BacklogSteps);
                Counter(sink, frame, timestamp, BattleDiagnosticMetricCategory.Simulation,
                    BattleDiagnosticFrameMetricKeys.SimulationOverBudgetUpdateTotal,
                    tick.OverBudgetUpdateCount);
                Counter(sink, frame, timestamp, BattleDiagnosticMetricCategory.Simulation,
                    BattleDiagnosticFrameMetricKeys.SimulationDroppedTimeSecondsTotal,
                    tick.DroppedTimeSeconds);
                Counter(sink, frame, timestamp, BattleDiagnosticMetricCategory.Simulation,
                    BattleDiagnosticFrameMetricKeys.SimulationInvalidDeltaTotal,
                    tick.InvalidDeltaCount);

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
            var previous = _jitterBufferStats;
            _jitterBufferStats = snapshot;
            BattleFlowDebugProvider.WithdrawJitterBufferStats(_scope, previous);
            BattleFlowDebugProvider.PublishJitterBufferStats(_scope, snapshot);
            BattleFlowDebugProvider.JitterBufferStats = snapshot;
        }

        internal void PublishTimeSync(
            TimeSyncStatsSnapshot current,
            Dictionary<string, TimeSyncStatsSnapshot> byWorld)
        {
            var previous = _timeSyncStats;
            var previousByWorld = _timeSyncStatsByWorld;
            _timeSyncStats = current;
            _timeSyncStatsByWorld = byWorld;
            BattleFlowDebugProvider.WithdrawTimeSyncStats(_scope, previous, previousByWorld);
            BattleFlowDebugProvider.PublishTimeSyncStats(_scope, current, byWorld);
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
            var previous = _confirmedAuthorityWorldStats;
            _confirmedAuthorityWorldStats = snapshot;
            if (string.IsNullOrWhiteSpace(_scope)) _scope = worldId ?? string.Empty;
            BattleFlowDebugProvider.WithdrawConfirmedAuthorityStats(_scope, previous);
            BattleFlowDebugProvider.PublishConfirmedAuthorityStats(_scope, snapshot);
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
            BattleFlowDebugProvider.WithdrawJitterBufferStats(_scope, _jitterBufferStats);
            if (ReferenceEquals(BattleFlowDebugProvider.JitterBufferStats, _jitterBufferStats))
            {
                BattleFlowDebugProvider.JitterBufferStats = null;
            }
            _jitterBufferStats = null;
        }

        internal void ClearTimeSync()
        {
            BattleFlowDebugProvider.WithdrawTimeSyncStats(
                _scope,
                _timeSyncStats,
                _timeSyncStatsByWorld);
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
            BattleFlowDebugProvider.WithdrawConfirmedAuthorityStats(
                _scope,
                _confirmedAuthorityWorldStats);
            if (ReferenceEquals(
                    BattleFlowDebugProvider.ConfirmedAuthorityWorldStats,
                    _confirmedAuthorityWorldStats))
            {
                BattleFlowDebugProvider.ConfirmedAuthorityWorldStats = null;
            }
            _confirmedAuthorityWorldStats = null;
        }

        private void WithdrawScopedPublications()
        {
            BattleFlowDebugProvider.WithdrawJitterBufferStats(_scope, _jitterBufferStats);
            BattleFlowDebugProvider.WithdrawTimeSyncStats(
                _scope,
                _timeSyncStats,
                _timeSyncStatsByWorld);
            BattleFlowDebugProvider.WithdrawConfirmedAuthorityStats(
                _scope,
                _confirmedAuthorityWorldStats);
        }

        private void PublishScopedPublications()
        {
            BattleFlowDebugProvider.PublishJitterBufferStats(_scope, _jitterBufferStats);
            BattleFlowDebugProvider.PublishTimeSyncStats(
                _scope,
                _timeSyncStats,
                _timeSyncStatsByWorld);
            BattleFlowDebugProvider.PublishConfirmedAuthorityStats(
                _scope,
                _confirmedAuthorityWorldStats);
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
            _scope = string.Empty;
            _metricSinkWorld = null;
            _metricSink = null;
            _lastMetricFrame = BattleDiagnosticFrames.Invalid;
        }
    }
}
