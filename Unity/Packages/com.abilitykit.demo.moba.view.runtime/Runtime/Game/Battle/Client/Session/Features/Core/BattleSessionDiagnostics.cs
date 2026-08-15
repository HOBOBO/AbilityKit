using System;
using System.Collections.Generic;
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
        }
    }
}
