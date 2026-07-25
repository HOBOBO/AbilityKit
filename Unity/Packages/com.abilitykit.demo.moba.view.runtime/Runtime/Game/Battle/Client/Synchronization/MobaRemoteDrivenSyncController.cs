using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// MOBA RemoteDriven（预测回滚）同步策略控制器。
    ///
    /// 实现框架 <see cref="IClientSyncStrategy{TInput,TSample}"/> 契约，
    /// 将 MOBA 的 RemoteDriven 预测路径包装为统一入口，与 Shooter 的
    /// <c>ShooterClientPredictRollbackSyncController</c> 对齐。
    ///
    /// 调用方（BattleSessionFeature）持有本控制器实例，
    /// 通过 <see cref="Tick"/> 驱动 RemoteDriven world 推进，
    /// 通过 <see cref="SubmitInput"/> 喂入本地输入。
    ///
    /// ObserveRemote 是空操作——RemoteDriven 的权威快照通过
    /// FrameSnapshotDispatcher（BattleSyncFeature）独立分发，
    /// 不经过本策略控制器（与 Shooter PredictRollback 一致）。
    /// </summary>
    internal sealed class MobaRemoteDrivenSyncController : IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample>
    {
        private readonly BattleSessionState _state;
        private readonly BattleSessionHandles _handles;
        private readonly BattleStartPlan _plan;
        private readonly SessionWorldCatchUpController _worldCatchUp;
        private readonly FrameSnapshotDispatcher _snapshots;
        private readonly Func<float> _getFixedDeltaSeconds;

        public NetworkSyncModel SyncModel => NetworkSyncModel.PredictRollback;

        public bool IsStarted => _handles.RemoteDriven.World != null;

        public int CurrentFrame => _state.Tick.LastFrame;

        /// <summary>
        /// 最近一次 SubmitInput 的服务端 ACK 帧号。
        /// 由 BattleSessionFeature.TransportFactory 的 OnSubmitInputAck 回调更新。
        /// </summary>
        public int LastServerAckFrame;

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

        /// <summary>
        /// 推进 RemoteDriven world 一个 tick。
        /// 委托给 <see cref="RemoteDrivenWorldTickDriver.Tick"/>，
        /// 将结果映射为框架 <see cref="SyncTickResult"/>。
        /// </summary>
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

            var ticks = nextTicked - lastTicked;
            return new SyncTickResult(ticks, nextTicked, stateHash: 0u);
        }

        /// <summary>
        /// 喂入本地输入到 jitter buffer（RemoteDriven 输入队列）。
        /// </summary>
        public void SubmitInput(in PlayerInputCommand input)
        {
            var sink = _handles.RemoteDriven.Sink;
            if (sink == null) return;
            sink.Add(input.Frame.Value, new[] { input });
        }

        /// <summary>
        /// RemoteDriven 不通过本策略消费远端快照——
        /// 权威快照由 FrameSnapshotDispatcher 独立分发给 BattleSyncFeature。
        /// 与 Shooter PredictRollback 的 ObserveRemote 一致（空操作）。
        /// </summary>
        public void ObserveRemote(in MobaRemoteSnapshotSample sample)
        {
        }

        public SyncReconciliationReport GetReconciliationReport()
        {
            // RemoteDriven 的对账由 ClientPredictionDriverModule 内部驱动
            // （hash mismatch → 自动 rollback + replay）。
            // 当前无显式报告机制，返回 None。
            return SyncReconciliationReport.None;
        }
    }
}
