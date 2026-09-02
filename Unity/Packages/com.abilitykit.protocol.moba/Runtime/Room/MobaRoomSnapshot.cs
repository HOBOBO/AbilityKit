using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Host.Extensions.Moba.Room
{
    public partial struct MobaRoomPlayerSnapshot
    {
        public MobaRoomPlayerSnapshot(
            PlayerId playerId,
            int teamId,
            bool ready,
            int heroId,
            int spawnPointId,
            int level,
            int attributeTemplateId,
            int basicAttackSkillId,
            int[] skillIds)
        {
            PlayerId = playerId;
            TeamId = teamId;
            Ready = ready;
            HeroId = heroId;
            SpawnPointId = spawnPointId;
            Level = level;
            AttributeTemplateId = attributeTemplateId;
            BasicAttackSkillId = basicAttackSkillId;
            SkillIds = skillIds;
        }
    }

    public partial struct MobaRoomSnapshot
    {
        public MobaRoomSnapshot(
            int revision,
            string matchId,
            int mapId,
            int randomSeed,
            int tickRate,
            int inputDelayFrames,
            int minPlayers,
            int maxPlayers,
            bool canStart,
            MobaRoomPlayerSnapshot[] players)
        {
            Revision = revision;
            MatchId = matchId;
            MapId = mapId;
            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            MinPlayers = minPlayers;
            MaxPlayers = maxPlayers;
            CanStart = canStart;
            Players = players;
        }
    }
}
