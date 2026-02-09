using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public interface IWorldAuthorityFramesSource
    {
        bool TryGetFrames(WorldId worldId, out FrameIndex confirmed, out FrameIndex predicted);
    }
}
