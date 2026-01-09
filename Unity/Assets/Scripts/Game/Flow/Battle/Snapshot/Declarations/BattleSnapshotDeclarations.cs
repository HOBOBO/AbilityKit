using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.Flow.Snapshot
{
    internal static class BattleSnapshotDeclarations
    {
        [SnapshotDecoder("battle", (int)MobaOpCode.LobbySnapshot, typeof(LobbySnapshot))]
        internal static bool DecodeLobby(in WorldStateSnapshot snap, out LobbySnapshot lobby)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                lobby = default;
                return false;
            }

            lobby = MobaLobbyCodec.DeserializeSnapshot(snap.Payload);
            return true;
        }

        [SnapshotDecoder("battle", (int)MobaOpCode.EnterGameSnapshot, typeof(EnterMobaGameRes))]
        internal static bool DecodeEnterGame(in WorldStateSnapshot snap, out EnterMobaGameRes res)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                res = default;
                return false;
            }

            res = EnterMobaGameCodec.DeserializeRes(snap.Payload);
            return true;
        }

        [SnapshotDecoder("battle", (int)MobaOpCode.ActorTransformSnapshot, typeof((int actorId, float x, float y, float z)[]))]
        internal static bool DecodeActorTransform(in WorldStateSnapshot snap, out (int actorId, float x, float y, float z)[] entries)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaActorTransformSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        [SnapshotDecoder("battle", (int)MobaOpCode.StateHashSnapshot, typeof(MobaStateHashSnapshotCodec.SnapshotPayload))]
        internal static bool DecodeStateHash(in WorldStateSnapshot snap, out MobaStateHashSnapshotCodec.SnapshotPayload payload)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                payload = default;
                return false;
            }

            payload = MobaStateHashSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        [SnapshotCmdHandler("battle", (int)MobaOpCode.EnterGameSnapshot, typeof(EnterMobaGameRes))]
        internal static void HandleEnterGame(object ctx, FramePacket packet, EnterMobaGameRes res)
        {
            if (ctx is not BattleContext battleCtx) return;
            BattleEnterGameApplier.Apply(battleCtx, res);
        }
    }
}
