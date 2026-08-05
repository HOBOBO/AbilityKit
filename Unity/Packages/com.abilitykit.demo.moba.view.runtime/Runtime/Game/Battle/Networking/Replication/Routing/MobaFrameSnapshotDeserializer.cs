using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba.CreateWorld;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow.Snapshot
{
    public sealed class MobaFrameSnapshotDeserializer : IFrameSnapshotDeserializer
    {
        public bool TryDeserializeEnterGame(in WorldStateSnapshot snap, out EnterMobaGameRes enterGame)
        {
            if (snap.OpCode != MobaOpCodes.Snapshot.EnterGame || snap.Payload == null || snap.Payload.Length == 0)
            {
                enterGame = default;
                return false;
            }

            enterGame = EnterMobaGameCodec.DeserializeRes(snap.Payload);
            return true;
        }

        public bool TryDeserializeActorTransform(in WorldStateSnapshot snap, out MobaActorTransformSnapshotEntry[] entries)
        {
            if (snap.OpCode != MobaOpCodes.Snapshot.ActorTransform || snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaActorTransformSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        public bool TryDeserializeStateHash(in WorldStateSnapshot snap, out MobaStateHashSnapshotPayload payload)
        {
            if (snap.OpCode != MobaOpCodes.Snapshot.StateHash || snap.Payload == null || snap.Payload.Length == 0)
            {
                payload = default;
                return false;
            }

            payload = MobaStateHashSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }
    }
}

