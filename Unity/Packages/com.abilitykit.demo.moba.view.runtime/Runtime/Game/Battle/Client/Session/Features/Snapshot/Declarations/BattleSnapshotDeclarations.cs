using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using AbilityKit.Game.Flow.Battle.Snapshot;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow.Snapshot
{
    internal static class BattleSnapshotDeclarations
    {
        [SnapshotDecoder("battle", MobaOpCodes.Snapshot.EnterGame, typeof(BattleEnterGameSnapshot))]
        internal static bool DecodeEnterGame(in WorldStateSnapshot snap, out BattleEnterGameSnapshot res)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                res = default;
                return false;
            }

            res = BattleEnterGameSnapshotDecoder.Decode(snap.Payload);
            return true;
        }

        [SnapshotDecoder("battle", MobaOpCodes.Snapshot.PlayerHeroChanged, typeof(MobaPlayerHeroChangedSnapshotEntry[]))]
        internal static bool DecodePlayerHeroChanged(
            in WorldStateSnapshot snap,
            out MobaPlayerHeroChangedSnapshotEntry[] entries)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaPlayerHeroChangedSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        internal static bool DecodeActionAck(
            in WorldStateSnapshot snap,
            out MobaActionAckEntry[] entries)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaActionAckCodec.Deserialize(snap.Payload);
            return true;
        }

        [SnapshotCmdHandler("battle", MobaOpCodes.Snapshot.EnterGame, typeof(BattleEnterGameSnapshot))]
        internal static void HandleEnterGame(object ctx, ISnapshotEnvelope packet, BattleEnterGameSnapshot res)
        {
            if (ctx is not BattleContext battleCtx) return;
            BattleEnterGameApplier.Apply(battleCtx, res);
        }

        [SnapshotCmdHandler("battle", MobaOpCodes.Snapshot.ActorSpawn, typeof(ActorSpawnData[]))]
        internal static void HandleActorSpawn(object ctx, ISnapshotEnvelope packet, ActorSpawnData[] entries)
        {
            if (ctx is not BattleContext battleCtx) return;
            BattleActorSpawnApplier.Apply(battleCtx, entries);
        }

        [SnapshotCmdHandler("battle", MobaOpCodes.Snapshot.PlayerHeroChanged, typeof(MobaPlayerHeroChangedSnapshotEntry[]))]
        internal static void HandlePlayerHeroChanged(
            object ctx,
            ISnapshotEnvelope packet,
            MobaPlayerHeroChangedSnapshotEntry[] entries)
        {
            if (ctx is not BattleContext battleCtx)
            {
                Log.Warning($"[BattleSnapshotDeclarations] PlayerHeroChanged context mismatch. contextType={ctx?.GetType().FullName ?? "null"}");
                return;
            }

            if (entries == null || entries.Length == 0)
            {
                Log.Warning("[BattleSnapshotDeclarations] PlayerHeroChanged handler received no entries.");
                return;
            }

            for (var i = 0; i < entries.Length; i++)
            {
                battleCtx.ApplyPlayerHeroChanged(in entries[i]);
            }
        }

        internal static void HandleActionAck(
            object ctx,
            ISnapshotEnvelope packet,
            MobaActionAckEntry[] entries)
        {
            if (ctx is BattleContext battleCtx)
            {
                battleCtx.ApplyActionAcks(entries);
            }
        }

        [SnapshotCmdHandler("battle", MobaOpCodes.Snapshot.ActorDespawn, typeof(ActorDespawnData[]))]
        internal static void HandleActorDespawn(object ctx, ISnapshotEnvelope packet, ActorDespawnData[] entries)
        {
            if (ctx is not BattleContext battleCtx) return;
            BattleActorDespawnApplier.Apply(battleCtx, entries);
        }
    }
}
