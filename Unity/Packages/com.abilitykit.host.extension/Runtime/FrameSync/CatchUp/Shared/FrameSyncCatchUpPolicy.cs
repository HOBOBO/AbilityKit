using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync.CatchUp
{
    public enum FrameSyncCatchUpDecisionKind
    {
        None = 0,
        SendInputs = 1,
        SendSnapshot = 2,
    }

    public readonly struct FrameSyncCatchUpDecision
    {
        public readonly FrameSyncCatchUpDecisionKind Kind;
        public readonly FrameSyncCatchUpRequest CatchUpRequest;

        public FrameSyncCatchUpDecision(FrameSyncCatchUpDecisionKind kind, in FrameSyncCatchUpRequest catchUpRequest)
        {
            Kind = kind;
            CatchUpRequest = catchUpRequest;
        }

        public static FrameSyncCatchUpDecision None(WorldId worldId)
            => new FrameSyncCatchUpDecision(FrameSyncCatchUpDecisionKind.None, new FrameSyncCatchUpRequest(worldId, new FrameIndex(0), new FrameIndex(0)));

        public static FrameSyncCatchUpDecision SendSnapshot(WorldId worldId)
            => new FrameSyncCatchUpDecision(FrameSyncCatchUpDecisionKind.SendSnapshot, new FrameSyncCatchUpRequest(worldId, new FrameIndex(0), new FrameIndex(0)));

        public static FrameSyncCatchUpDecision SendInputs(in FrameSyncCatchUpRequest req)
            => new FrameSyncCatchUpDecision(FrameSyncCatchUpDecisionKind.SendInputs, in req);
    }

    public readonly struct FrameSyncCatchUpPolicyOptions
    {
        public readonly int MaxCatchUpFrames;
        public readonly int MaxBatchFrames;
        public readonly int SafetyMarginFrames;

        public FrameSyncCatchUpPolicyOptions(int maxCatchUpFrames, int maxBatchFrames, int safetyMarginFrames)
        {
            MaxCatchUpFrames = maxCatchUpFrames;
            MaxBatchFrames = maxBatchFrames;
            SafetyMarginFrames = safetyMarginFrames;
        }

        public static FrameSyncCatchUpPolicyOptions Default => new FrameSyncCatchUpPolicyOptions(maxCatchUpFrames: 600, maxBatchFrames: 120, safetyMarginFrames: 2);
    }

    public static class FrameSyncCatchUpPolicy
    {
        public static FrameSyncCatchUpDecision Decide(
            WorldId worldId,
            FrameIndex authorityFrame,
            FrameIndex clientLastConfirmedFrame,
            in FrameSyncCatchUpPolicyOptions options)
        {
            var safety = options.SafetyMarginFrames < 0 ? 0 : options.SafetyMarginFrames;

            var maxCatchUp = options.MaxCatchUpFrames;
            if (maxCatchUp <= 0) maxCatchUp = 600;

            var maxBatch = options.MaxBatchFrames;
            if (maxBatch <= 0) maxBatch = 120;

            var from = clientLastConfirmedFrame.Value;
            var to = authorityFrame.Value - safety;

            if (to <= from) return FrameSyncCatchUpDecision.None(worldId);

            var gap = to - from;
            if (gap > maxCatchUp) return FrameSyncCatchUpDecision.SendSnapshot(worldId);

            if (gap > maxBatch) to = from + maxBatch;

            var req = new FrameSyncCatchUpRequest(worldId, new FrameIndex(from), new FrameIndex(to));
            return FrameSyncCatchUpDecision.SendInputs(in req);
        }
    }
}
