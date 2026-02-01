using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public interface IClientPredictionDriverStats
    {
        int InputDelayFrames { get; }

        int MaxPredictionAheadFrames { get; }

        int MinPredictionWindow { get; }

        float BacklogEwmaAlpha { get; }

        int CurrentBacklogRaw { get; }

        float CurrentBacklogEwma { get; }

        int CurrentPredictionWindow { get; }

        bool IsPredictionStalledByWindow { get; }

        long TotalPredictionWindowStalls { get; }

        int CurrentIdealFrameLimit { get; }

        bool IsPredictionStalledByIdealFrame { get; }

        long TotalIdealFrameStalls { get; }

        bool TryGetIdealFrameStallStats(WorldId worldId, out int idealFrameLimit, out bool stalled, out long stallsTotal);

        bool TryGetPredictionWindowStats(WorldId worldId, out int backlogRaw, out float backlogEwma, out int window, out bool stalled);

        bool TryGetPredictionWindowStats(WorldId worldId, out int backlogRaw, out float backlogEwma, out int window, out bool stalled, out long stallsTotal);

        bool IsReplaying { get; }

        FrameIndex ReplayToFrame { get; }

        FrameIndex LastRollbackFrame { get; }

        long TotalRollbackCount { get; }

        long TotalRollbackRestoreFailed { get; }

        long TotalReplayTimeout { get; }

        FrameIndex LastReplayTimeoutFrame { get; }

        long TotalReconcileAutoDisabledByReplayTimeout { get; }

        FrameIndex LastReconcileAutoDisabledByReplayTimeoutFrame { get; }

        long TotalReconcileMismatch { get; }

        FrameIndex LastReconcileMismatchFrame { get; }

        WorldStateHash LastReconcilePredictedHash { get; }

        WorldStateHash LastReconcileAuthoritativeHash { get; }

        long TotalAuthoritativeHashReceived { get; }

        long TotalPredictedHashRecorded { get; }

        long TotalAuthoritativeHashSkippedNoPredictedHash { get; }

        FrameIndex LastReconcileComparedFrame { get; }

        FrameIndex LastAuthoritativeHashFrame { get; }

        WorldStateHash LastAuthoritativeHash { get; }

        long TotalAuthoritativeHashIgnoredNoReconciler { get; }

        int LastConsumedConfirmedFrames { get; }

        int LastConsumedPredictedFrames { get; }

        long TotalConsumedConfirmedFrames { get; }

        long TotalPredictedFrames { get; }

        long TotalLocalDelayQueueDroppedBatches { get; }

        bool TryGetLocalDelayQueueDepth(WorldId worldId, out int depth);

        bool TryGetFrames(WorldId worldId, out FrameIndex confirmed, out FrameIndex predicted);

        bool TryGetReconcileEnabled(WorldId worldId, out bool enabled);
    }
}
