using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.Share;

namespace AbilityKit.Ability.Share.Impl.Moba.Struct
{
    public readonly struct MobaPlayerLoadout
    {
        [BinaryMember(0)] public readonly PlayerId PlayerId;
        [BinaryMember(1)] public readonly int TeamId;
        [BinaryMember(2)] public readonly int HeroId;
        [BinaryMember(3)] public readonly int Level;
        [BinaryMember(4)] public readonly int BasicAttackSkillId;
        [BinaryMember(5)] public readonly int[] SkillIds;
        [BinaryMember(6)] public readonly int SpawnIndex;
        [BinaryMember(7)] public readonly int UnitSubType;
        [BinaryMember(8)] public readonly int MainType;
        [BinaryMember(9)] public readonly int HasSpawnPosition;
        [BinaryMember(10)] public readonly float SpawnX;
        [BinaryMember(11)] public readonly float SpawnY;
        [BinaryMember(12)] public readonly float SpawnZ;
        [BinaryMember(13)] public readonly int AttributeTemplateId;

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

    public readonly struct MobaPlayerEntry
    {
        [BinaryMember(0)] public readonly PlayerId PlayerId;
        [BinaryMember(1)] public readonly int TeamId;
        [BinaryMember(2)] public readonly int HeroId;
        [BinaryMember(3)] public readonly int SpawnIndex;

        public MobaPlayerEntry(PlayerId playerId, int teamId, int heroId, int spawnIndex)
        {
            PlayerId = playerId;
            TeamId = teamId;
            HeroId = heroId;
            SpawnIndex = spawnIndex;
        }
    }

    public readonly struct EnterMobaGameReq
    {
        [BinaryMember(0)] public readonly PlayerId PlayerId;
        [BinaryMember(1)] public readonly string MatchId;
        [BinaryMember(2)] public readonly int MapId;

        [BinaryMember(3)] public readonly int RandomSeed;
        [BinaryMember(4)] public readonly int TickRate;
        [BinaryMember(5)] public readonly int InputDelayFrames;

        [BinaryMember(6)] public readonly int OpCode;
        [BinaryMember(7)] public readonly byte[] Payload;

        [BinaryMember(8)] public readonly MobaPlayerLoadout[] Players;

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

    public readonly struct EnterMobaGameRes
    {
        [BinaryMember(0)] public readonly WorldId WorldId;
        [BinaryMember(1)] public readonly PlayerId PlayerId;
        [BinaryMember(2)] public readonly int LocalActorId;

        [BinaryMember(3)] public readonly int RandomSeed;
        [BinaryMember(4)] public readonly int TickRate;
        [BinaryMember(5)] public readonly int InputDelayFrames;

        [BinaryMember(6)] public readonly MobaPlayerEntry[] Players;

        [BinaryMember(9)] public readonly MobaPlayerLoadout[] PlayersLoadout;

        [BinaryMember(7)] public readonly int OpCode;
        [BinaryMember(8)] public readonly byte[] Payload;

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
