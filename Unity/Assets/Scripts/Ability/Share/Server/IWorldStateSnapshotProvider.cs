using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Server
{
    public interface IWorldStateSnapshotProvider
    {
        bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot);
    }
}
