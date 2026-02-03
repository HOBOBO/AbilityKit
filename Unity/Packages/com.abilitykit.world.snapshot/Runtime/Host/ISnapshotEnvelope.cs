
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host
{
    public interface ISnapshotEnvelope
    {
        WorldId WorldId { get; }
        WorldStateSnapshot? Snapshot { get; }
    }
}
