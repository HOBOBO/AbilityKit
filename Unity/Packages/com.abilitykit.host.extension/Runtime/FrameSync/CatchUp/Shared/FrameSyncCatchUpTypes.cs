using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync.CatchUp
{
    public readonly struct FrameSyncCatchUpRequest
    {
        public readonly WorldId WorldId;
        public readonly FrameIndex FromFrameExclusive;
        public readonly FrameIndex ToFrameInclusive;

        public FrameSyncCatchUpRequest(WorldId worldId, FrameIndex fromFrameExclusive, FrameIndex toFrameInclusive)
        {
            WorldId = worldId;
            FromFrameExclusive = fromFrameExclusive;
            ToFrameInclusive = toFrameInclusive;
        }
    }

    public readonly struct FrameSyncCatchUpPayload
    {
        public readonly WorldId WorldId;
        public readonly FrameIndex StartFrame;
        public readonly PlayerInputCommand[][] Inputs;

        public FrameSyncCatchUpPayload(WorldId worldId, FrameIndex startFrame, PlayerInputCommand[][] inputs)
        {
            WorldId = worldId;
            StartFrame = startFrame;
            Inputs = inputs;
        }
    }
}
