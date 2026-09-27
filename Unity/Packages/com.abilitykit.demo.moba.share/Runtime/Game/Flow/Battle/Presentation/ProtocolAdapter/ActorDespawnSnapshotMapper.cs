using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Maps MOBA wire actor despawns into platform-neutral presentation contracts.
    /// </summary>
    public static class ActorDespawnSnapshotMapper
    {
        public static ActorDespawnData[] Map(MobaActorDespawnSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return Array.Empty<ActorDespawnData>();
            }

            var despawns = new ActorDespawnData[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                despawns[i] = Map(in entries[i]);
            }

            return despawns;
        }

        public static ActorDespawnData Map(in MobaActorDespawnSnapshotEntry entry)
        {
            return new ActorDespawnData(
                actorId: entry.ActorId,
                reason: (ActorDespawnReason)entry.Reason);
        }
    }
}
