using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow.Snapshot
{
    public interface IFrameSnapshotDeserializer
    {
        bool TryDeserializeEnterGame(in WorldStateSnapshot snap, out BattleEnterGameSnapshot enterGame);
        bool TryDeserializeActorTransform(in WorldStateSnapshot snap, out ActorTransformData[] entries);
        bool TryDeserializeStateHash(in WorldStateSnapshot snap, out StateHashData payload);
    }
}

