#nullable enable

using System;
using AbilityKit.Ability.Host;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>
    /// 客户端复制路径的统一协调器。
    ///
    /// 它将本地输入、服务端确认、权威远端样本和策略推进收敛到同一条可观测的管线中，
    /// 而不要求预测回滚和纯权威插值策略拥有相同的内部实现。
    /// </summary>
    public sealed class MobaClientReplicationPipeline
    {
        private const int HealthEventCapacity = 64;

        private readonly IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample> _strategy;
        private readonly SyncHealthEventBuffer _healthEvents = new SyncHealthEventBuffer(HealthEventCapacity);
        private int _lastSubmittedFrame;
        private int _lastAcknowledgedFrame;
        private int _lastObservedFrame;
        private int _submittedInputCount;
        private int _observedSnapshotCount;
        private SyncTickResult _lastTick;
        private SyncReconciliationReport _lastReconciliation;
        private bool _hasObservedSnapshot;

        public MobaClientReplicationPipeline(
            IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample> strategy)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _lastReconciliation = SyncReconciliationReport.None;
        }

        public NetworkSyncModel SyncModel => _strategy.SyncModel;

        public SyncHealthReport CreateHealthReport() => _healthEvents.CreateReport();

        public void SubmitInput(in PlayerInputCommand input)
        {
            _strategy.SubmitInput(in input);
            _lastSubmittedFrame = input.Frame.Value;
            _submittedInputCount++;
        }

        public void AcknowledgeInput(int authoritativeFrame)
        {
            if (authoritativeFrame <= _lastAcknowledgedFrame) return;

            _lastAcknowledgedFrame = authoritativeFrame;
            var healthEvent = SyncHealthEvent.Info(
                SyncHealthEventKind.InputAccepted,
                authoritativeFrame,
                Math.Max(0, _lastSubmittedFrame - authoritativeFrame));
            _healthEvents.Publish(in healthEvent);
        }

        public void ObserveRemote(in MobaRemoteSnapshotSample sample)
        {
            _strategy.ObserveRemote(in sample);
            _observedSnapshotCount++;

            if (_hasObservedSnapshot && sample.Frame <= _lastObservedFrame)
            {
                var stale = SyncHealthEvent.Warning(
                    SyncHealthEventKind.SnapshotStale,
                    sample.Frame,
                    _lastObservedFrame);
                _healthEvents.Publish(in stale);
                return;
            }

            if (_hasObservedSnapshot && sample.Frame > _lastObservedFrame + 1)
            {
                var gap = SyncHealthEvent.Warning(
                    SyncHealthEventKind.SnapshotGap,
                    sample.Frame,
                    sample.Frame - _lastObservedFrame - 1L);
                _healthEvents.Publish(in gap);
            }

            _hasObservedSnapshot = true;
            _lastObservedFrame = sample.Frame;
            var received = SyncHealthEvent.Info(
                SyncHealthEventKind.SnapshotReceived,
                sample.Frame,
                sample.Actors.Count);
            _healthEvents.Publish(in received);
        }

        public SyncTickResult Tick(float deltaSeconds)
        {
            var previousRecoveryState = _lastReconciliation.RecoveryState;
            _lastTick = _strategy.Tick(deltaSeconds);
            _lastReconciliation = _strategy.GetReconciliationReport();
            PublishReconciliationHealth(previousRecoveryState, in _lastReconciliation);
            return _lastTick;
        }

        private void PublishReconciliationHealth(
            SyncRecoveryState previousRecoveryState,
            in SyncReconciliationReport reconciliation)
        {
            if (reconciliation.DidReconcile ||
                (reconciliation.RecoveryState == SyncRecoveryState.CatchUp &&
                 previousRecoveryState != SyncRecoveryState.CatchUp))
            {
                var rollback = SyncHealthEvent.Warning(
                    SyncHealthEventKind.RollbackStarted,
                    reconciliation.AuthoritativeFrame,
                    reconciliation.ReplayTicks);
                _healthEvents.Publish(in rollback);
            }

            if (reconciliation.RecoveryState == SyncRecoveryState.Recovered &&
                previousRecoveryState != SyncRecoveryState.Recovered)
            {
                var replay = SyncHealthEvent.Info(
                    SyncHealthEventKind.ReplayCompleted,
                    reconciliation.ClientFrame,
                    reconciliation.ReplayTicks);
                _healthEvents.Publish(in replay);
            }
        }

        /// <summary>
        /// 清除当前运行周期的管线诊断。策略自身的状态由策略所有者负责重置，
        /// 因为 <see cref="IClientSyncStrategy{TInput, TSample}"/> 不要求所有模型都支持重置。
        /// </summary>
        public void ResetDiagnostics()
        {
            _lastSubmittedFrame = 0;
            _lastAcknowledgedFrame = 0;
            _lastObservedFrame = 0;
            _submittedInputCount = 0;
            _observedSnapshotCount = 0;
            _lastTick = default;
            _lastReconciliation = SyncReconciliationReport.None;
            _hasObservedSnapshot = false;
            _healthEvents.Reset();
        }

        public MobaReplicationDiagnostics GetDiagnostics()
        {
            return new MobaReplicationDiagnostics(
                SyncModel,
                _strategy.IsStarted,
                _lastSubmittedFrame,
                _lastAcknowledgedFrame,
                _lastObservedFrame,
                _submittedInputCount,
                _observedSnapshotCount,
                _lastTick,
                _lastReconciliation,
                _healthEvents.CreateReport());
        }
    }

    /// <summary>统一复制管线的只读运行诊断快照。</summary>
    public readonly struct MobaReplicationDiagnostics
    {
        public MobaReplicationDiagnostics(
            NetworkSyncModel syncModel,
            bool isStarted,
            int lastSubmittedFrame,
            int lastAcknowledgedFrame,
            int lastObservedFrame,
            int submittedInputCount,
            int observedSnapshotCount,
            SyncTickResult lastTick,
            SyncReconciliationReport reconciliation,
            SyncHealthReport health)
        {
            SyncModel = syncModel;
            IsStarted = isStarted;
            LastSubmittedFrame = lastSubmittedFrame;
            LastAcknowledgedFrame = lastAcknowledgedFrame;
            LastObservedFrame = lastObservedFrame;
            SubmittedInputCount = submittedInputCount;
            ObservedSnapshotCount = observedSnapshotCount;
            LastTick = lastTick;
            Reconciliation = reconciliation;
            Health = health ?? SyncHealthReport.Empty;
        }

        public NetworkSyncModel SyncModel { get; }
        public bool IsStarted { get; }
        public int LastSubmittedFrame { get; }
        public int LastAcknowledgedFrame { get; }
        public int LastObservedFrame { get; }
        public int SubmittedInputCount { get; }
        public int ObservedSnapshotCount { get; }
        public SyncTickResult LastTick { get; }
        public SyncReconciliationReport Reconciliation { get; }
        public SyncHealthReport Health { get; }
        public int UnacknowledgedInputFrames => Math.Max(0, LastSubmittedFrame - LastAcknowledgedFrame);
    }
}
