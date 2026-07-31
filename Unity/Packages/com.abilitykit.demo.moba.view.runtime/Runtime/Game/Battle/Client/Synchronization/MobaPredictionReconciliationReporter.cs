using System;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Flow
{
    internal readonly struct MobaPredictionReconciliationSample
    {
        public MobaPredictionReconciliationSample(
            long totalMismatchCount,
            long totalRollbackCount,
            bool isReplaying,
            int clientFrame,
            int replayToFrame,
            int lastRollbackFrame,
            int mismatchFrame,
            uint predictedHash,
            uint authoritativeHash)
        {
            TotalMismatchCount = totalMismatchCount;
            TotalRollbackCount = totalRollbackCount;
            IsReplaying = isReplaying;
            ClientFrame = clientFrame;
            ReplayToFrame = replayToFrame;
            LastRollbackFrame = lastRollbackFrame;
            MismatchFrame = mismatchFrame;
            PredictedHash = predictedHash;
            AuthoritativeHash = authoritativeHash;
        }

        public long TotalMismatchCount { get; }
        public long TotalRollbackCount { get; }
        public bool IsReplaying { get; }
        public int ClientFrame { get; }
        public int ReplayToFrame { get; }
        public int LastRollbackFrame { get; }
        public int MismatchFrame { get; }
        public uint PredictedHash { get; }
        public uint AuthoritativeHash { get; }

        public static MobaPredictionReconciliationSample Capture(
            IClientPredictionDriverStats stats,
            int clientFrame)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));

            return new MobaPredictionReconciliationSample(
                stats.TotalReconcileMismatch,
                stats.TotalRollbackCount,
                stats.IsReplaying,
                clientFrame,
                stats.ReplayToFrame.Value,
                stats.LastRollbackFrame.Value,
                stats.LastReconcileMismatchFrame.Value,
                stats.LastReconcilePredictedHash.Value,
                stats.LastReconcileAuthoritativeHash.Value);
        }
    }

    /// <summary>
    /// Converts cumulative prediction-driver statistics into edge-triggered sync reports.
    /// </summary>
    internal sealed class MobaPredictionReconciliationReporter
    {
        private long _lastMismatchCount;
        private long _lastRollbackCount;
        private bool _wasReplaying;

        public SyncReconciliationReport Observe(in MobaPredictionReconciliationSample sample)
        {
            if (sample.TotalMismatchCount < _lastMismatchCount ||
                sample.TotalRollbackCount < _lastRollbackCount)
            {
                Reset();
            }

            var hasNewMismatch = sample.TotalMismatchCount > _lastMismatchCount;
            var hasNewRollback = sample.TotalRollbackCount > _lastRollbackCount;
            var recoveryState = ResolveRecoveryState(in sample, hasNewRollback);

            _lastMismatchCount = sample.TotalMismatchCount;
            _lastRollbackCount = sample.TotalRollbackCount;
            _wasReplaying = sample.IsReplaying;

            if (!hasNewMismatch && recoveryState == SyncRecoveryState.Normal)
                return SyncReconciliationReport.None;

            return new SyncReconciliationReport(
                hasNewMismatch
                    ? SyncReconciliationReason.AuthoritativeHashMismatch
                    : SyncReconciliationReason.None,
                recoveryState,
                needsFullSnapshot: false,
                sample.ClientFrame,
                sample.MismatchFrame,
                sample.PredictedHash,
                sample.AuthoritativeHash,
                CalculateReplayTicks(in sample, recoveryState));
        }

        public void Reset()
        {
            _lastMismatchCount = 0;
            _lastRollbackCount = 0;
            _wasReplaying = false;
        }

        private SyncRecoveryState ResolveRecoveryState(
            in MobaPredictionReconciliationSample sample,
            bool hasNewRollback)
        {
            if (sample.IsReplaying)
                return SyncRecoveryState.CatchUp;

            return _wasReplaying || hasNewRollback
                ? SyncRecoveryState.Recovered
                : SyncRecoveryState.Normal;
        }

        private static int CalculateReplayTicks(
            in MobaPredictionReconciliationSample sample,
            SyncRecoveryState recoveryState)
        {
            if (recoveryState == SyncRecoveryState.CatchUp && sample.IsReplaying)
                return Math.Max(0, sample.ClientFrame - sample.LastRollbackFrame);

            if (recoveryState == SyncRecoveryState.Recovered)
                return Math.Max(0, sample.ReplayToFrame - sample.LastRollbackFrame);

            return 0;
        }
    }
}
