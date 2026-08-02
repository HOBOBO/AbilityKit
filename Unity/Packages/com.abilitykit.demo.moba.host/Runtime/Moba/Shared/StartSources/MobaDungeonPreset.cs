namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public readonly struct MobaDungeonPreset
    {
        public readonly int DungeonId;
        public readonly int PresetId;

        public readonly string MatchId;
        public readonly int MapId;
        public readonly int RandomSeed;
        public readonly int TickRate;
        public readonly int InputDelayFrames;

        public readonly int TeamId;
        public readonly int HeroId;
        public readonly int SpawnPointId;

        public readonly int Level;
        public readonly int AttributeTemplateId;
        public readonly int BasicAttackSkillId;
        public readonly int[] SkillIds;

        public MobaDungeonPreset(
            int dungeonId,
            int presetId,
            string matchId,
            int mapId,
            int randomSeed,
            int tickRate,
            int inputDelayFrames,
            int teamId,
            int heroId,
            int spawnPointId,
            int level,
            int attributeTemplateId,
            int basicAttackSkillId,
            int[] skillIds)
        {
            DungeonId = dungeonId;
            PresetId = presetId;
            MatchId = matchId;
            MapId = mapId;
            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            TeamId = teamId;
            HeroId = heroId;
            SpawnPointId = spawnPointId;
            Level = level;
            AttributeTemplateId = attributeTemplateId;
            BasicAttackSkillId = basicAttackSkillId;
            SkillIds = skillIds;
        }
    }
}
