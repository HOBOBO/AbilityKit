using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync.CatchUp
{
    public interface IFrameSyncInputHistory
    {
        bool TryBuildCatchUp(in FrameSyncCatchUpRequest request, out FrameSyncCatchUpPayload payload);

        void Append(WorldId worldId, FrameIndex frame, PlayerInputCommand[] inputs);

        void TrimBefore(WorldId worldId, FrameIndex frameExclusive);
    }
}
