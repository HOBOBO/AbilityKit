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
        private readonly IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample> _strategy;
        private int _lastSubmittedFrame;
        private int _lastAcknowledgedFrame;
        private int _lastObservedFrame;
        private int _submittedInputCount;
        private int _observedSnapshotCount;
        private SyncTickResult _lastTick;
        private SyncReconciliationReport _lastReconciliation;

        public MobaClientReplicationPipeline(
            IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample> strategy)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _lastReconciliation = SyncReconciliationReport.None;
        }

        public NetworkSyncModel SyncModel => _strategy.SyncModel;

        public void SubmitInput(in PlayerInputCommand input)
        {
            _strategy.SubmitInput(in input);
            _lastSubmittedFrame = input.Frame.Value;
            _submittedInputCount++;
        }

        public void AcknowledgeInput(int authoritativeFrame)
        {
            if (authoritativeFrame > _lastAcknowledgedFrame)
                _lastAcknowledgedFrame = authoritativeFrame;
        }

        public void ObserveRemote(in MobaRemoteSnapshotSample sample)
        {
            _strategy.ObserveRemote(in sample);
            _lastObservedFrame = Math.Max(_lastObservedFrame, sample.Frame);
            _observedSnapshotCount++;
        }

        public SyncTickResult Tick(float deltaSeconds)
        {
            _lastTick = _strategy.Tick(deltaSeconds);
            _lastReconciliation = _strategy.GetReconciliationReport();
            return _lastTick;
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
                _lastReconciliation);
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
            SyncReconciliationReport reconciliation)
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
        public int UnacknowledgedInputFrames => Math.Max(0, LastSubmittedFrame - LastAcknowledgedFrame);
    }
}
