using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public interface IWorldStateSnapshotProvider : IService
    {
        bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot);
    }
}
