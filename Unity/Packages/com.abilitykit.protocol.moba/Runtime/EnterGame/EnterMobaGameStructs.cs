using AbilityKit.Ability.Host;
using AbilityKit.Core.Generic;
using AbilityKit.Ability.World.Abstractions;
using MemoryPack;

namespace AbilityKit.Ability.Share.Impl.Moba.Struct
{
    [MemoryPackable]
    public readonly partial struct MobaPlayerLoadout
    {
        [MemoryPackOrder(0), BinaryMember(0)] public readonly PlayerId PlayerId;
        [MemoryPackOrder(1), BinaryMember(1)] public readonly int TeamId;
        [MemoryPackOrder(2), BinaryMember(2)] public readonly int HeroId;
        [MemoryPackOrder(3), BinaryMember(3)] public readonly int Level;
        [MemoryPackOrder(4), BinaryMember(4)] public readonly int BasicAttackSkillId;
        [MemoryPackOrder(5), BinaryMember(5)] public readonly int[] SkillIds;
        [MemoryPackOrder(6), BinaryMember(6)] public readonly int SpawnIndex;
        [MemoryPackOrder(7), BinaryMember(7)] public readonly int UnitSubType;
        [MemoryPackOrder(8), BinaryMember(8)] public readonly int MainType;
        [MemoryPackOrder(9), BinaryMember(9)] public readonly int HasSpawnPosition;
        [MemoryPackOrder(10), BinaryMember(10)] public readonly float SpawnX;
        [MemoryPackOrder(11), BinaryMember(11)] public readonly float SpawnY;
        [MemoryPackOrder(12), BinaryMember(12)] public readonly float SpawnZ;
        [MemoryPackOrder(13), BinaryMember(13)] public readonly int AttributeTemplateId;

        [MemoryPackConstructor]
        public MobaPlayerLoadout(
            PlayerId playerId,
            int teamId,
            int heroId,
            int attributeTemplateId,
            int level,
            int basicAttackSkillId,
            int[] skillIds,
            int spawnIndex,
            int unitSubType = 1,
            int mainType = 1,
            int hasSpawnPosition = 0,
            float spawnX = 0f,
            float spawnY = 0f,
            float spawnZ = 0f)
        {
            PlayerId = playerId;
            TeamId = teamId;
            HeroId = heroId;
            AttributeTemplateId = attributeTemplateId;
            Level = level;
            BasicAttackSkillId = basicAttackSkillId;
            SkillIds = skillIds;
            SpawnIndex = spawnIndex;
            UnitSubType = unitSubType;
            MainType = mainType;

            HasSpawnPosition = hasSpawnPosition;
            SpawnX = spawnX;
            SpawnY = spawnY;
            SpawnZ = spawnZ;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaPlayerEntry
    {
        [MemoryPackOrder(0), BinaryMember(0)] public readonly PlayerId PlayerId;
        [MemoryPackOrder(1), BinaryMember(1)] public readonly int TeamId;
        [MemoryPackOrder(2), BinaryMember(2)] public readonly int HeroId;
        [MemoryPackOrder(3), BinaryMember(3)] public readonly int SpawnIndex;

        [MemoryPackConstructor]
        public MobaPlayerEntry(PlayerId playerId, int teamId, int heroId, int spawnIndex)
        {
            PlayerId = playerId;
            TeamId = teamId;
            HeroId = heroId;
            SpawnIndex = spawnIndex;
        }
    }

    [MemoryPackable]
    public readonly partial struct EnterMobaGameReq
    {
        [MemoryPackOrder(0), BinaryMember(0)] public readonly PlayerId PlayerId;
        [MemoryPackOrder(1), BinaryMember(1)] public readonly string MatchId;
        [MemoryPackOrder(2), BinaryMember(2)] public readonly int MapId;

        [MemoryPackOrder(3), BinaryMember(3)] public readonly int RandomSeed;
        [MemoryPackOrder(4), BinaryMember(4)] public readonly int TickRate;
        [MemoryPackOrder(5), BinaryMember(5)] public readonly int InputDelayFrames;

        [MemoryPackOrder(6), BinaryMember(6)] public readonly int OpCode;
        [MemoryPackOrder(7), BinaryMember(7)] public readonly byte[] Payload;

        [MemoryPackOrder(8), BinaryMember(8)] public readonly MobaPlayerLoadout[] Players;

        [MemoryPackConstructor]
        public EnterMobaGameReq(
            PlayerId playerId,
            string matchId,
            int mapId,
            int randomSeed,
            int tickRate,
            int inputDelayFrames,
            int opCode = 0,
            byte[] payload = null,
            MobaPlayerLoadout[] players = null)
        {
            PlayerId = playerId;
            MatchId = matchId;
            MapId = mapId;

            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            OpCode = opCode;
            Payload = payload;

            Players = players;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaGameStartSpec
    {
        [MemoryPackOrder(0), BinaryMember(0)] public readonly EnterMobaGameReq EnterReq;

        [MemoryPackConstructor]
        public MobaGameStartSpec(in EnterMobaGameReq enterReq)
        {
            EnterReq = enterReq;
        }
    }

    [MemoryPackable]
    public readonly partial struct EnterMobaGameRes
    {
        [MemoryPackOrder(0), BinaryMember(0)] public readonly WorldId WorldId;
        [MemoryPackOrder(1), BinaryMember(1)] public readonly PlayerId PlayerId;
        [MemoryPackOrder(2), BinaryMember(2)] public readonly int LocalActorId;

        [MemoryPackOrder(3), BinaryMember(3)] public readonly int RandomSeed;
        [MemoryPackOrder(4), BinaryMember(4)] public readonly int TickRate;
        [MemoryPackOrder(5), BinaryMember(5)] public readonly int InputDelayFrames;

        [MemoryPackOrder(6), BinaryMember(6)] public readonly MobaPlayerEntry[] Players;

        [MemoryPackOrder(9), BinaryMember(9)] public readonly MobaPlayerLoadout[] PlayersLoadout;

        [MemoryPackOrder(7), BinaryMember(7)] public readonly int OpCode;
        [MemoryPackOrder(8), BinaryMember(8)] public readonly byte[] Payload;

        [MemoryPackConstructor]
        public EnterMobaGameRes(
            WorldId worldId,
            PlayerId playerId,
            int localActorId,
            int randomSeed,
            int tickRate,
            int inputDelayFrames,
            MobaPlayerEntry[] players = null,
            int opCode = 0,
            byte[] payload = null,
            MobaPlayerLoadout[] playersLoadout = null)
        {
            WorldId = worldId;
            PlayerId = playerId;
            LocalActorId = localActorId;
            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            Players = players;
            OpCode = opCode;
            Payload = payload;
            PlayersLoadout = playersLoadout;
        }
    }
}
