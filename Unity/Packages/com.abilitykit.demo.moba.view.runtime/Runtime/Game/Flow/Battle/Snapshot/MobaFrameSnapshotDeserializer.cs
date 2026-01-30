using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;

namespace AbilityKit.Game.Flow.Snapshot
{
    public sealed class MobaFrameSnapshotDeserializer : IFrameSnapshotDeserializer
    {
        public bool TryDeserializeLobby(in WorldStateSnapshot snap, out LobbySnapshot lobby)
        {
            if (snap.OpCode != (int)MobaOpCode.LobbySnapshot || snap.Payload == null || snap.Payload.Length == 0)
            {
                lobby = default;
                return false;
            }

            lobby = MobaLobbyCodec.DeserializeSnapshot(snap.Payload);
            return true;
        }

        public bool TryDeserializeEnterGame(in WorldStateSnapshot snap, out EnterMobaGameRes enterGame)
        {
            if (snap.OpCode != (int)MobaOpCode.EnterGameSnapshot || snap.Payload == null || snap.Payload.Length == 0)
            {
                enterGame = default;
                return false;
            }

            enterGame = EnterMobaGameCodec.DeserializeRes(snap.Payload);
            return true;
        }

        public bool TryDeserializeActorTransform(in WorldStateSnapshot snap, out (int actorId, float x, float y, float z)[] entries)
        {
            if (snap.OpCode != (int)MobaOpCode.ActorTransformSnapshot || snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaActorTransformSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        public bool TryDeserializeStateHash(in WorldStateSnapshot snap, out MobaStateHashSnapshotCodec.SnapshotPayload payload)
        {
            if (snap.OpCode != (int)MobaOpCode.StateHashSnapshot || snap.Payload == null || snap.Payload.Length == 0)
            {
                payload = default;
                return false;
            }

            payload = MobaStateHashSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }
    }
}
