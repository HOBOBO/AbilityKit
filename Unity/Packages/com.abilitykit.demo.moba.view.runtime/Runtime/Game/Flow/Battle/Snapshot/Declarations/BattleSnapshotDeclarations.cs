using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.SnapshotRouting;
using AbilityKit.Ability.Share.Impl.Moba.CreateWorld;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Flow.Battle.Snapshot;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.Flow.Snapshot
{
    internal static class BattleSnapshotDeclarations
    {
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

        [SnapshotDecoder("battle", (int)MobaOpCode.ActorSpawnSnapshot, typeof(MobaActorSpawnSnapshotCodec.Entry[]))]
        internal static bool DecodeActorSpawn(in WorldStateSnapshot snap, out MobaActorSpawnSnapshotCodec.Entry[] entries)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaActorSpawnSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        [SnapshotDecoder("battle", (int)MobaOpCode.ActorDespawnSnapshot, typeof(MobaActorDespawnSnapshotCodec.Entry[]))]
        internal static bool DecodeActorDespawn(in WorldStateSnapshot snap, out MobaActorDespawnSnapshotCodec.Entry[] entries)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaActorDespawnSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        [SnapshotCmdHandler("battle", (int)MobaOpCode.EnterGameSnapshot, typeof(EnterMobaGameRes))]
        internal static void HandleEnterGame(object ctx, ISnapshotEnvelope packet, EnterMobaGameRes res)
        {
            if (ctx is not BattleContext battleCtx) return;
            BattleEnterGameApplier.Apply(battleCtx, res);
        }

        [SnapshotCmdHandler("battle", (int)MobaOpCode.ActorSpawnSnapshot, typeof(MobaActorSpawnSnapshotCodec.Entry[]))]
        internal static void HandleActorSpawn(object ctx, ISnapshotEnvelope packet, MobaActorSpawnSnapshotCodec.Entry[] entries)
        {
            if (ctx is not BattleContext battleCtx) return;
            BattleActorSpawnApplier.Apply(battleCtx, entries);
        }

        [SnapshotCmdHandler("battle", (int)MobaOpCode.ActorDespawnSnapshot, typeof(MobaActorDespawnSnapshotCodec.Entry[]))]
        internal static void HandleActorDespawn(object ctx, ISnapshotEnvelope packet, MobaActorDespawnSnapshotCodec.Entry[] entries)
        {
            if (ctx is not BattleContext battleCtx) return;
            BattleActorDespawnApplier.Apply(battleCtx, entries);
        }
    }
}
