using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Server
{
    public interface IWorldStateSnapshotProvider : IService
    {
        bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot);
    }
}
