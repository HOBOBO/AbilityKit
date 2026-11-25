using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /*
     * 重演模式仅属于客户端预测驱动，不决定共享世界的同步模式。
     */
    internal enum ClientPredictionReplayMode
    {
        Normal = 0,
        Replaying = 1,
    }

    /// <summary>
    /// 每世界预测上下文；运行依赖、推进数据、历史和诊断按归属集中保存。
    /// </summary>
    internal sealed class ClientPredictionWorldContext
    {
        public readonly RuntimeBindings Bindings = new RuntimeBindings();
        public readonly ProgressState Progress = new ProgressState();
        public readonly InputsState Inputs = new InputsState();
        public readonly ReconciliationState Reconciliation = new ReconciliationState();
        public readonly ReplayState Replay = new ReplayState();
        public readonly WindowState Window = new WindowState();

        /*
         * 运行依赖与待提交句柄，不参与业务快照。
         */
        internal sealed class RuntimeBindings
        {
            public IWorld World;
            public IWorldInputSink InputSink;
            public IWorldSettledInputSink SettledInputSink;
            public IConsumableRemoteFrameSource<PlayerInputCommand[]> PendingSettlement;
            public IWorldSettledTimelineSink SettledTimeline;
            public IWorldSettledFrameUpdate PendingSettledUpdate;
            public IConsumableRemoteFrameSource<WorldSettledInputFrame> PendingSettledSource;
            public IFrameTimeAligner FrameTime;
            public RollbackCoordinator Rollback;
            public Func<FrameIndex, WorldStateHash> ComputeHash;
            public ClientPredictionReconciler Reconciler;
        }

        /*
         * 当前世界的推进游标与失败门禁。
         */
        internal sealed class ProgressState
        {
            public FrameIndex ConfirmedFrame;
            public FrameIndex PredictedFrame;
            public FrameIndex PredictedBeforeTick;
            public FrameIndex ConfirmedBeforeTick;
            public bool RollbackFaulted;
            public int CaptureCounter;
            public FrameIndex LastProcessedRollbackFrame;
        }

        /*
         * 输入历史和待处理输入数据。
         */
        internal sealed class InputsState
        {
            public InputHistoryRingBuffer RawPredictionInputs;
            public WorldSettledInputFrame PendingSettledFrame;
            public SortedDictionary<int, List<LocalPlayerInputEvent>> PendingLocalInputs;
            public InputHistoryRingBuffer AppliedInputs;
            public InputHistoryRingBuffer AuthoritativeInputs;
        }

        /*
         * 哈希对账历史与启用状态。
         */
        internal sealed class ReconciliationState
        {
            public WorldStateHashRingBuffer PredictedHashes;
            public WorldStateHashRingBuffer AuthoritativeHashes;
            public WorldStateHashRingBuffer ComparedAuthoritativeHashes;
            public bool ReconcileEnabled;
        }

        /*
         * 重演范围、等待游标与输入重演标记。
         */
        internal sealed class ReplayState
        {
            public int ReplayWaitTicks;
            public int LastReplayWaitTargetFrame;
            public ClientPredictionReplayMode Mode;
            public bool InputReplayActive;
            public FrameIndex ReplayTo;
            public FrameIndex LastRollbackFrame;
            public FrameIndex LastMissingAppliedHistoryFrame;
        }

        /*
         * 预测窗口计算的派生状态与逐世界诊断。
         */
        internal sealed class WindowState
        {
            public bool HasBacklogEwma;
            public float BacklogEwma;
            public int BacklogRaw;
            public int PredictionWindow;
            public bool PredictionStalled;
            public long PredictionWindowStallsTotal;
            public int IdealFrameLimit;
            public bool IdealFrameStalled;
            public long IdealFrameStallsTotal;
            public bool IdealFrameCappedWindow;
        }
    }
}
