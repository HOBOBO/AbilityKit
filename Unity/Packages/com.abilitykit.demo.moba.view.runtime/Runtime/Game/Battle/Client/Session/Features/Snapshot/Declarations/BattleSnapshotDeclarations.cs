using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba.CreateWorld;
using AbilityKit.Protocol.Moba;
using AbilityKit.Game.Flow.Battle.Snapshot;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow.Snapshot
{
    internal static class BattleSnapshotDeclarations
    {
        [SnapshotDecoder("battle", MobaOpCodes.Snapshot.EnterGame, typeof(EnterMobaGameRes))]
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

        [SnapshotDecoder("battle", MobaOpCodes.Snapshot.ActorSpawn, typeof(MobaActorSpawnSnapshotEntry[]))]
        internal static bool DecodeActorSpawn(in WorldStateSnapshot snap, out MobaActorSpawnSnapshotEntry[] entries)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaActorSpawnSnapshotCodec.Deserialize(snap.Payload);
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

        [SnapshotDecoder("battle", MobaOpCodes.Snapshot.ActorDespawn, typeof(MobaActorDespawnSnapshotEntry[]))]
        internal static bool DecodeActorDespawn(in WorldStateSnapshot snap, out MobaActorDespawnSnapshotEntry[] entries)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                entries = null;
                return false;
            }

            entries = MobaActorDespawnSnapshotCodec.Deserialize(snap.Payload);
            return true;
        }

        [SnapshotCmdHandler("battle", MobaOpCodes.Snapshot.EnterGame, typeof(EnterMobaGameRes))]
        internal static void HandleEnterGame(object ctx, ISnapshotEnvelope packet, EnterMobaGameRes res)
        {
            if (ctx is not BattleContext battleCtx) return;
            BattleEnterGameApplier.Apply(battleCtx, res);
        }

        [SnapshotCmdHandler("battle", MobaOpCodes.Snapshot.ActorSpawn, typeof(MobaActorSpawnSnapshotEntry[]))]
        internal static void HandleActorSpawn(object ctx, ISnapshotEnvelope packet, MobaActorSpawnSnapshotEntry[] entries)
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

        [SnapshotCmdHandler("battle", MobaOpCodes.Snapshot.ActorDespawn, typeof(MobaActorDespawnSnapshotEntry[]))]
        internal static void HandleActorDespawn(object ctx, ISnapshotEnvelope packet, MobaActorDespawnSnapshotEntry[] entries)
        {
            if (ctx is not BattleContext battleCtx) return;
            BattleActorDespawnApplier.Apply(battleCtx, entries);
        }
    }
}

