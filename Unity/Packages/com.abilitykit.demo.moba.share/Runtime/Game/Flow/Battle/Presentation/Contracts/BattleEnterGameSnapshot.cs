namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>Platform-neutral presentation snapshot produced when a player enters a battle.</summary>
    public readonly struct BattleEnterGameSnapshot
    {
        public readonly string WorldId;
        public readonly string PlayerId;
        public readonly int LocalActorId;
        public readonly int RandomSeed;
        public readonly int TickRate;
        public readonly int InputDelayFrames;
        public readonly BattleEnterGamePlayer[] Players;
        public readonly int OpCode;
        public readonly byte[] Payload;
        public readonly BattlePlayerLoadout[] PlayersLoadout;
        public readonly bool HasLocalActorPosition;
        public readonly float LocalActorX;
        public readonly float LocalActorY;
        public readonly float LocalActorZ;

        public BattleEnterGameSnapshot(
            string worldId,
            string playerId,
            int localActorId,
            int randomSeed,
            int tickRate,
            int inputDelayFrames,
            BattleEnterGamePlayer[] players = null,
            int opCode = 0,
            byte[] payload = null,
            BattlePlayerLoadout[] playersLoadout = null,
            bool hasLocalActorPosition = false,
            float localActorX = 0f,
            float localActorY = 0f,
            float localActorZ = 0f)
        {
            WorldId = worldId ?? string.Empty;
            PlayerId = playerId ?? string.Empty;
            LocalActorId = localActorId;
            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            Players = players ?? System.Array.Empty<BattleEnterGamePlayer>();
            OpCode = opCode;
            Payload = payload ?? System.Array.Empty<byte>();
            PlayersLoadout = playersLoadout ?? System.Array.Empty<BattlePlayerLoadout>();
            HasLocalActorPosition = hasLocalActorPosition;
            LocalActorX = localActorX;
            LocalActorY = localActorY;
            LocalActorZ = localActorZ;
        }
    }

    public readonly struct BattleEnterGamePlayer
    {
        public readonly string PlayerId;
        public readonly int TeamId;
        public readonly int HeroId;
        public readonly int SpawnIndex;

        public BattleEnterGamePlayer(string playerId, int teamId, int heroId, int spawnIndex)
        {
            PlayerId = playerId ?? string.Empty;
            TeamId = teamId;
            HeroId = heroId;
            SpawnIndex = spawnIndex;
        }
    }

    public readonly struct BattlePlayerLoadout
    {
        public readonly string PlayerId;
        public readonly int TeamId;
        public readonly int HeroId;
        public readonly int AttributeTemplateId;
        public readonly int Level;
        public readonly int BasicAttackSkillId;
        public readonly int[] SkillIds;
        public readonly int SpawnIndex;
        public readonly int UnitSubType;
        public readonly int MainType;
        public readonly int HasSpawnPosition;
        public readonly float SpawnX;
        public readonly float SpawnY;
        public readonly float SpawnZ;
        public readonly int BrainId;
        public readonly bool EnableBrainOnSpawn;

        public BattlePlayerLoadout(
            string playerId,
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
            float spawnZ = 0f,
            int brainId = 0,
            bool enableBrainOnSpawn = true)
        {
            PlayerId = playerId ?? string.Empty;
            TeamId = teamId;
            HeroId = heroId;
            AttributeTemplateId = attributeTemplateId;
            Level = level;
            BasicAttackSkillId = basicAttackSkillId;
            SkillIds = skillIds ?? System.Array.Empty<int>();
            SpawnIndex = spawnIndex;
            UnitSubType = unitSubType;
            MainType = mainType;
            HasSpawnPosition = hasSpawnPosition;
            SpawnX = spawnX;
            SpawnY = spawnY;
            SpawnZ = spawnZ;
            BrainId = brainId;
            EnableBrainOnSpawn = enableBrainOnSpawn;
        }
    }
}
