using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public interface IClientPredictionDriverStats
    {
        int InputDelayFrames { get; }

        int MaxConsumePredictedFramesPerTick { get; }

        int MaxConsumeConfirmedFramesPerTick { get; }

        int LastConsumedConfirmedFrames { get; }

        int LastConsumedPredictedFrames { get; }

        long TotalConsumedConfirmedFrames { get; }

        long TotalPredictedFrames { get; }

        long TotalLocalDelayQueueDroppedBatches { get; }

        bool TryGetLocalDelayQueueDepth(WorldId worldId, out int depth);

        bool TryGetFrames(WorldId worldId, out FrameIndex confirmed, out FrameIndex predicted);
    }
}
