using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow.Snapshot
{
    public interface IFrameSnapshotDeserializer
    {
        bool TryDeserializeEnterGame(in WorldStateSnapshot snap, out EnterMobaGameRes enterGame);
        bool TryDeserializeActorTransform(in WorldStateSnapshot snap, out MobaActorTransformSnapshotEntry[] entries);
        bool TryDeserializeStateHash(in WorldStateSnapshot snap, out MobaStateHashSnapshotPayload payload);
    }
}

