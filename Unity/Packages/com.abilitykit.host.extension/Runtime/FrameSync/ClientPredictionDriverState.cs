using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /// <summary>
    /// 驱动器的跨世界诊断与本次宿主推进标记；保持原有统计和重置口径。
    /// </summary>
    internal sealed class ClientPredictionDriverState
    {
        public int LastConsumedConfirmedFrames;
        public int LastConsumedPredictedFrames;
        public long TotalConsumedConfirmedFrames;
        public long TotalPredictedFrames;
        public long TotalLocalDelayQueueDroppedBatches;
        public int CurrentPredictionWindow;
        public bool IsPredictionStalledByWindow;
        public long TotalPredictionWindowStalls;
        public int CurrentIdealFrameLimit;
        public bool IsPredictionStalledByIdealFrame;
        public long TotalIdealFrameStalls;
        public int CurrentBacklogRaw;
        public float CurrentBacklogEwma;
        public long TotalRollbackCount;
        public long TotalRollbackRestoreFailed;
        public long TotalReplayTimeout;
        public FrameIndex LastReplayTimeoutFrame;
        public long TotalReconcileAutoDisabledByReplayTimeout;
        public FrameIndex LastReconcileAutoDisabledByReplayTimeoutFrame;
        public long TotalReconcileMismatch;
        public long TotalPredictedHashRecorded;
        public long TotalAuthoritativeHashSkippedNoPredictedHash;
        public FrameIndex LastReconcileMismatchFrame;
        public FrameIndex LastReconcileComparedFrame;
        public WorldStateHash LastReconcilePredictedHash;
        public WorldStateHash LastReconcileAuthoritativeHash;
        public long TotalAuthoritativeHashReceived;
        public FrameIndex LastAuthoritativeHashFrame;
        public WorldStateHash LastAuthoritativeHash;
        public long TotalAuthoritativeHashIgnoredNoReconciler;
        public bool IsReplaying;
        public FrameIndex ReplayToFrame;
        public FrameIndex LastRollbackFrame;
        public bool ShouldRunWorldTick;
    }
}
