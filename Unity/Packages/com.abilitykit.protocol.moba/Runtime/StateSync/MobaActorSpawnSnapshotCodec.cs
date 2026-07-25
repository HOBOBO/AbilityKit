using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    public enum SpawnEntityKind : byte
    {
        Character = 1,
        Projectile = 2,
    }

    [MemoryPackable]
    public partial struct MobaActorSpawnSnapshotEntry
    {
        [MemoryPackOrder(0)] public int NetId;
        [MemoryPackOrder(1)] public int Kind;
        [MemoryPackOrder(2)] public int Code;
        [MemoryPackOrder(3)] public int OwnerNetId;
        [MemoryPackOrder(4)] public float X;
        [MemoryPackOrder(5)] public float Y;
        [MemoryPackOrder(6)] public float Z;

        public MobaActorSpawnSnapshotEntry(int netId, int kind, int code, int ownerNetId, float x, float y, float z)
        {
            NetId = netId;
            Kind = kind;
            Code = code;
            OwnerNetId = ownerNetId;
            X = x;
            Y = y;
            Z = z;
        }
    }

    [MemoryPackable]
    public partial struct MobaActorSpawnSnapshotPayload
    {
        [MemoryPackOrder(0)] public MobaActorSpawnSnapshotEntry[] Entries;

        [MemoryPackConstructor]
        public MobaActorSpawnSnapshotPayload(MobaActorSpawnSnapshotEntry[] entries)
        {
            Entries = entries;
        }
    }

    public static class MobaActorSpawnSnapshotCodec
    {
        public static byte[] Serialize(MobaActorSpawnSnapshotEntry[] entries)
        {
            entries ??= Array.Empty<MobaActorSpawnSnapshotEntry>();
            var payload = new MobaActorSpawnSnapshotPayload { Entries = entries };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaActorSpawnSnapshotEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return Array.Empty<MobaActorSpawnSnapshotEntry>();

            var p = MemoryPackSerializer.Deserialize<MobaActorSpawnSnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<MobaActorSpawnSnapshotEntry>();
        }
    }

    public enum MobaDebugSpawnUnitRelation : byte
    {
        Ally = 1,
        Enemy = 2,
    }

    [MemoryPackable]
    public partial struct MobaDebugSpawnUnitPayload
    {
        public const byte CurrentVersion = 1;

        [MemoryPackOrder(0)] public byte Version;
        [MemoryPackOrder(1)] public MobaDebugSpawnUnitRelation Relation;

        public MobaDebugSpawnUnitPayload(byte version, MobaDebugSpawnUnitRelation relation)
        {
            Version = version;
            Relation = relation;
        }
    }

    public static class MobaDebugSpawnUnitCodec
    {
        public static byte[] Serialize(MobaDebugSpawnUnitRelation relation)
        {
            if (!IsSupportedRelation(relation))
            {
                throw new ArgumentOutOfRangeException(nameof(relation), relation, "debug spawn relation is invalid");
            }

            var payload = new MobaDebugSpawnUnitPayload(
                MobaDebugSpawnUnitPayload.CurrentVersion,
                relation);
            return MemoryPackSerializer.Serialize(payload);
        }

        public static bool TryDeserialize(
            byte[] payload,
            out MobaDebugSpawnUnitRelation relation,
            out string error)
        {
            relation = default;
            error = null;

            if (payload == null || payload.Length == 0)
            {
                error = "payload is null or empty";
                return false;
            }

            try
            {
                var decoded = MemoryPackSerializer.Deserialize<MobaDebugSpawnUnitPayload>(payload);
                if (decoded.Version != MobaDebugSpawnUnitPayload.CurrentVersion)
                {
                    error = $"unsupported payload version: {decoded.Version}";
                    return false;
                }

                if (!IsSupportedRelation(decoded.Relation))
                {
                    error = $"invalid spawn relation: {(byte)decoded.Relation}";
                    return false;
                }

                relation = decoded.Relation;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool IsSupportedRelation(MobaDebugSpawnUnitRelation relation)
        {
            return relation == MobaDebugSpawnUnitRelation.Ally ||
                   relation == MobaDebugSpawnUnitRelation.Enemy;
        }
    }

    [MemoryPackable]
    public partial struct MobaDebugReplaceHeroPayload
    {
        public const byte CurrentVersion = 1;

        [MemoryPackOrder(0)] public byte Version;
        [MemoryPackOrder(1)] public int HeroId;

        public MobaDebugReplaceHeroPayload(byte version, int heroId)
        {
            Version = version;
            HeroId = heroId;
        }
    }

    public static class MobaDebugReplaceHeroCodec
    {
        public static byte[] Serialize(int heroId)
        {
            if (heroId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(heroId), heroId, "hero id must be positive");
            }

            var payload = new MobaDebugReplaceHeroPayload(
                MobaDebugReplaceHeroPayload.CurrentVersion,
                heroId);
            return MemoryPackSerializer.Serialize(payload);
        }

        public static bool TryDeserialize(byte[] payload, out int heroId, out string error)
        {
            heroId = 0;
            error = null;
            if (payload == null || payload.Length == 0)
            {
                error = "payload is null or empty";
                return false;
            }

            try
            {
                var decoded = MemoryPackSerializer.Deserialize<MobaDebugReplaceHeroPayload>(payload);
                if (decoded.Version != MobaDebugReplaceHeroPayload.CurrentVersion)
                {
                    error = $"unsupported payload version: {decoded.Version}";
                    return false;
                }

                if (decoded.HeroId <= 0)
                {
                    error = $"invalid hero id: {decoded.HeroId}";
                    return false;
                }

                heroId = decoded.HeroId;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }
    }

    [MemoryPackable]
    public partial struct MobaPlayerHeroChangedSnapshotEntry
    {
        [MemoryPackOrder(0)] public string PlayerId;
        [MemoryPackOrder(1)] public int PreviousActorId;
        [MemoryPackOrder(2)] public int ActorId;
        [MemoryPackOrder(3)] public int TeamId;
        [MemoryPackOrder(4)] public int HeroId;
        [MemoryPackOrder(5)] public int AttributeTemplateId;
        [MemoryPackOrder(6)] public int Level;
        [MemoryPackOrder(7)] public int BasicAttackSkillId;
        [MemoryPackOrder(8)] public int[] SkillIds;

        public MobaPlayerHeroChangedSnapshotEntry(
            string playerId,
            int previousActorId,
            int actorId,
            int teamId,
            int heroId,
            int attributeTemplateId,
            int level,
            int basicAttackSkillId,
            int[] skillIds)
        {
            PlayerId = playerId;
            PreviousActorId = previousActorId;
            ActorId = actorId;
            TeamId = teamId;
            HeroId = heroId;
            AttributeTemplateId = attributeTemplateId;
            Level = level;
            BasicAttackSkillId = basicAttackSkillId;
            SkillIds = skillIds;
        }
    }

    [MemoryPackable]
    public partial struct MobaPlayerHeroChangedSnapshotPayload
    {
        [MemoryPackOrder(0)] public MobaPlayerHeroChangedSnapshotEntry[] Entries;
    }

    public static class MobaPlayerHeroChangedSnapshotCodec
    {
        public static byte[] Serialize(MobaPlayerHeroChangedSnapshotEntry[] entries)
        {
            var payload = new MobaPlayerHeroChangedSnapshotPayload
            {
                Entries = entries ?? Array.Empty<MobaPlayerHeroChangedSnapshotEntry>(),
            };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaPlayerHeroChangedSnapshotEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<MobaPlayerHeroChangedSnapshotEntry>();
            }

            var decoded = MemoryPackSerializer.Deserialize<MobaPlayerHeroChangedSnapshotPayload>(payload);
            return decoded.Entries ?? Array.Empty<MobaPlayerHeroChangedSnapshotEntry>();
        }
    }
}
