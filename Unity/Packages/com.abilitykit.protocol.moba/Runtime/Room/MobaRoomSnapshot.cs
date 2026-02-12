using AbilityKit.Ability.Host;
using MemoryPack;

namespace AbilityKit.Ability.Host.Extensions.Moba.Room
{
    [MemoryPackable]
    public readonly partial struct MobaRoomPlayerSnapshot
    {
        [MemoryPackOrder(0)] public readonly PlayerId PlayerId;
        [MemoryPackOrder(1)] public readonly int TeamId;
        [MemoryPackOrder(2)] public readonly bool Ready;

        [MemoryPackOrder(3)] public readonly int HeroId;
        [MemoryPackOrder(4)] public readonly int SpawnPointId;

        [MemoryPackOrder(5)] public readonly int Level;
        [MemoryPackOrder(6)] public readonly int AttributeTemplateId;
        [MemoryPackOrder(7)] public readonly int BasicAttackSkillId;
        [MemoryPackOrder(8)] public readonly int[] SkillIds;

        [MemoryPackConstructor]
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

    [MemoryPackable]
    public readonly partial struct MobaRoomSnapshot
    {
        [MemoryPackOrder(0)] public readonly int Revision;

        [MemoryPackOrder(1)] public readonly string MatchId;
        [MemoryPackOrder(2)] public readonly int MapId;
        [MemoryPackOrder(3)] public readonly int RandomSeed;
        [MemoryPackOrder(4)] public readonly int TickRate;
        [MemoryPackOrder(5)] public readonly int InputDelayFrames;

        [MemoryPackOrder(6)] public readonly int MinPlayers;
        [MemoryPackOrder(7)] public readonly int MaxPlayers;

        [MemoryPackOrder(8)] public readonly bool CanStart;

        [MemoryPackOrder(9)] public readonly MobaRoomPlayerSnapshot[] Players;

        [MemoryPackConstructor]
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
