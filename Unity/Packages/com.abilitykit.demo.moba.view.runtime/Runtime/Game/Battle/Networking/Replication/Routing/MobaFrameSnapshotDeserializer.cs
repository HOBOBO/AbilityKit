using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow.Snapshot
{
    public sealed class MobaFrameSnapshotDeserializer : IFrameSnapshotDeserializer
    {
        public bool TryDeserializeEnterGame(in WorldStateSnapshot snap, out BattleEnterGameSnapshot enterGame)
        {
            if (snap.OpCode != MobaOpCodes.Snapshot.EnterGame || snap.Payload == null || snap.Payload.Length == 0)
            {
                enterGame = default;
                return false;
            }

            enterGame = BattleEnterGameSnapshotDecoder.Decode(snap.Payload);
            return true;
        }

        public bool TryDeserializeActorTransform(in WorldStateSnapshot snap, out ActorTransformData[] entries)
        {
            if (snap.OpCode != ActorTransformSnapshotRoute.OpCode)
            {
                entries = System.Array.Empty<ActorTransformData>();
                return false;
            }

            return ActorTransformSnapshotRoute.TryDecode(in snap, out entries);
        }

        public bool TryDeserializeStateHash(in WorldStateSnapshot snap, out StateHashData payload)
        {
            if (snap.OpCode != StateHashSnapshotRoute.OpCode)
            {
                payload = default;
                return false;
            }

            return StateHashSnapshotRoute.TryDecode(in snap, out payload);
        }
    }
}

