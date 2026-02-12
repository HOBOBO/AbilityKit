using AbilityKit.Ability.Host;
using MemoryPack;

namespace AbilityKit.Ability.Host.Extensions.Moba.Room
{
    public enum MobaRoomCommandKind
    {
        Unknown = 0,
        Join = 1,
        Leave = 2,
        SetReady = 3,
        PickHero = 4,
        SetSpawnPoint = 5,
    }

    [MemoryPackable]
    public readonly partial struct MobaRoomCommand
    {
        [MemoryPackOrder(0)] public readonly MobaRoomCommandKind Kind;
        [MemoryPackOrder(1)] public readonly PlayerId PlayerId;

        [MemoryPackOrder(2)] public readonly int ClientSeq;
        [MemoryPackOrder(3)] public readonly int ExpectedRevision;

        [MemoryPackOrder(4)] public readonly int TeamId;
        [MemoryPackOrder(5)] public readonly int Ready;

        [MemoryPackOrder(6)] public readonly int HeroId;
        [MemoryPackOrder(7)] public readonly int SpawnPointId;

        [MemoryPackOrder(8)] public readonly int Level;
        [MemoryPackOrder(9)] public readonly int AttributeTemplateId;
        [MemoryPackOrder(10)] public readonly int BasicAttackSkillId;
        [MemoryPackOrder(11)] public readonly int[] SkillIds;

        [MemoryPackConstructor]
        private MobaRoomCommand(
            MobaRoomCommandKind kind,
            PlayerId playerId,
            int clientSeq,
            int expectedRevision,
            int teamId,
            int ready,
            int heroId,
            int spawnPointId,
            int level,
            int attributeTemplateId,
            int basicAttackSkillId,
            int[] skillIds)
        {
            Kind = kind;
            PlayerId = playerId;
            ClientSeq = clientSeq;
            ExpectedRevision = expectedRevision;
            TeamId = teamId;
            Ready = ready;
            HeroId = heroId;
            SpawnPointId = spawnPointId;
            Level = level;
            AttributeTemplateId = attributeTemplateId;
            BasicAttackSkillId = basicAttackSkillId;
            SkillIds = skillIds;
        }

        public static MobaRoomCommand Join(PlayerId playerId, int teamId = 0, int clientSeq = 0, int expectedRevision = 0)
            => new MobaRoomCommand(MobaRoomCommandKind.Join, playerId, clientSeq, expectedRevision, teamId, 0, 0, 0, 0, 0, 0, null);

        public static MobaRoomCommand Leave(PlayerId playerId, int clientSeq = 0, int expectedRevision = 0)
            => new MobaRoomCommand(MobaRoomCommandKind.Leave, playerId, clientSeq, expectedRevision, 0, 0, 0, 0, 0, 0, 0, null);

        public static MobaRoomCommand SetReady(PlayerId playerId, bool ready, int clientSeq = 0, int expectedRevision = 0)
            => new MobaRoomCommand(MobaRoomCommandKind.SetReady, playerId, clientSeq, expectedRevision, 0, ready ? 1 : 0, 0, 0, 0, 0, 0, null);

        public static MobaRoomCommand PickHero(
            PlayerId playerId,
            int heroId,
            int attributeTemplateId = 0,
            int level = 1,
            int basicAttackSkillId = 0,
            int[] skillIds = null,
            int clientSeq = 0,
            int expectedRevision = 0)
            => new MobaRoomCommand(MobaRoomCommandKind.PickHero, playerId, clientSeq, expectedRevision, 0, 0, heroId, 0, level, attributeTemplateId, basicAttackSkillId, skillIds);

        public static MobaRoomCommand SetSpawnPoint(PlayerId playerId, int spawnPointId, int clientSeq = 0, int expectedRevision = 0)
            => new MobaRoomCommand(MobaRoomCommandKind.SetSpawnPoint, playerId, clientSeq, expectedRevision, 0, 0, 0, spawnPointId, 0, 0, 0, null);
    }
}
