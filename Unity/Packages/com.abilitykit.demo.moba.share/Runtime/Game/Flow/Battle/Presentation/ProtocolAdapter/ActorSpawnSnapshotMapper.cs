using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Maps MOBA wire actor spawns into platform-neutral presentation contracts.
    /// </summary>
    public static class ActorSpawnSnapshotMapper
    {
        public static ActorSpawnData[] Map(MobaActorSpawnSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return Array.Empty<ActorSpawnData>();
            }

            var spawns = new ActorSpawnData[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                spawns[i] = Map(in entries[i]);
            }

            return spawns;
        }

        public static ActorSpawnData Map(in MobaActorSpawnSnapshotEntry entry)
        {
            return new ActorSpawnData(
                kind: (ActorSpawnKind)entry.Kind,
                actorId: entry.NetId,
                ownerActorId: entry.OwnerNetId,
                entityCode: entry.Code,
                characterId: entry.Code,
                name: string.Empty,
                x: entry.X,
                y: entry.Y,
                z: entry.Z,
                rotationY: 0f,
                scale: 1f,
                teamId: 0,
                maxHp: 0f,
                hp: 0f,
                playerId: entry.OwnerNetId == 0 ? null : entry.OwnerNetId.ToString(),
                entityVersion: entry.EntityVersion);
        }
    }
}
