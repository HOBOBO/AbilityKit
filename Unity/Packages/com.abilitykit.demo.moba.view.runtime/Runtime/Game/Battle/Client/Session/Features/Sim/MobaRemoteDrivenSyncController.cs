using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Drives the MOBA remote prediction world through the common client-sync contract.
    /// Authoritative snapshots remain owned by the battle snapshot dispatcher.
    /// </summary>
    internal sealed class MobaRemoteDrivenSyncController :
        IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample>
    {
        private readonly BattleSessionState _state;
        private readonly BattleSessionHandles _handles;
        private readonly BattleStartPlan _plan;
        private readonly SessionWorldCatchUpController _worldCatchUp;
        private readonly FrameSnapshotDispatcher _snapshots;
        private readonly Func<float> _getFixedDeltaSeconds;
        private readonly MobaPredictionReconciliationReporter _reconciliationReporter =
            new MobaPredictionReconciliationReporter();
        private IClientPredictionDriverStats _predictionStats;

        public MobaRemoteDrivenSyncController(
            BattleSessionState state,
            BattleSessionHandles handles,
            BattleStartPlan plan,
            SessionWorldCatchUpController worldCatchUp,
            FrameSnapshotDispatcher snapshots,
            Func<float> getFixedDeltaSeconds)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _handles = handles ?? throw new ArgumentNullException(nameof(handles));
            _plan = plan;
            _worldCatchUp = worldCatchUp;
            _snapshots = snapshots;
            _getFixedDeltaSeconds = getFixedDeltaSeconds ?? (() => 0f);
        }

        public NetworkSyncModel SyncModel => NetworkSyncModel.PredictRollback;

        public bool IsStarted => _handles.RemoteDriven.World != null;

        public int CurrentFrame => _state.Tick.LastFrame;

        public int LastServerAckFrame;

        public SyncTickResult Tick(float deltaSeconds)
        {
            var lastTicked = _state.Tick.LastFrame;
            var nextTicked = RemoteDrivenWorldTickDriver.Tick(new RemoteDrivenWorldTickOptions(
                _plan,
                _handles.RemoteDriven,
                _worldCatchUp,
                _snapshots,
                lastTicked,
                _getFixedDeltaSeconds(),
                SessionSimRuntimeTuning.MaxCatchUpStepsPerUpdate,
                LastServerAckFrame));

            _state.Tick.LastFrame = nextTicked;
            return new SyncTickResult(nextTicked - lastTicked, nextTicked, stateHash: 0u);
        }

        public void SubmitInput(in PlayerInputCommand input)
        {
            var sink = _handles.RemoteDriven.Sink;
            if (sink == null) return;
            sink.Add(input.Frame.Value, new[] { input });
        }

        public void ObserveRemote(in MobaRemoteSnapshotSample sample)
        {
            // Remote snapshots are dispatched independently by BattleSyncFeature.
        }

        public SyncReconciliationReport GetReconciliationReport()
        {
            var runtime = _handles.RemoteDriven.Runtime;
            if (runtime == null ||
                !runtime.Features.TryGetFeature<IClientPredictionDriverStats>(out var stats) ||
                stats == null)
            {
                ResetPredictionDiagnosticsSource();
                return SyncReconciliationReport.None;
            }

            if (!ReferenceEquals(_predictionStats, stats))
            {
                _predictionStats = stats;
                _reconciliationReporter.Reset();
            }

            var clientFrame = CurrentFrame;
            var world = _handles.RemoteDriven.World;
            if (world != null && stats.TryGetFrames(world.Id, out _, out var predictedFrame))
                clientFrame = predictedFrame.Value;

            var sample = MobaPredictionReconciliationSample.Capture(stats, clientFrame);
            return _reconciliationReporter.Observe(in sample);
        }

        private void ResetPredictionDiagnosticsSource()
        {
            if (_predictionStats == null) return;
            _predictionStats = null;
            _reconciliationReporter.Reset();
        }
    }
}
