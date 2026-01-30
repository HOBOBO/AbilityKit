using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;

namespace AbilityKit.Game.Flow.Snapshot
{
    public interface IFrameSnapshotDeserializer
    {
        bool TryDeserializeLobby(in WorldStateSnapshot snap, out LobbySnapshot lobby);
        bool TryDeserializeEnterGame(in WorldStateSnapshot snap, out EnterMobaGameRes enterGame);
        bool TryDeserializeActorTransform(in WorldStateSnapshot snap, out (int actorId, float x, float y, float z)[] entries);
        bool TryDeserializeStateHash(in WorldStateSnapshot snap, out MobaStateHashSnapshotCodec.SnapshotPayload payload);
    }
}
