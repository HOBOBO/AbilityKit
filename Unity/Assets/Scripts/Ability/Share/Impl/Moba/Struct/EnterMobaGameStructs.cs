using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Share.Impl.Moba.Struct
{
    public readonly struct MobaPlayerEntry
    {
        public readonly PlayerId PlayerId;
        public readonly int TeamId;
        public readonly int HeroId;
        public readonly int SpawnIndex;

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
        public readonly PlayerId PlayerId;
        public readonly string MatchId;
        public readonly int MapId;
        public readonly int TeamId;
        public readonly int HeroId;

        public readonly int RandomSeed;
        public readonly int TickRate;
        public readonly int InputDelayFrames;

        public readonly int OpCode;
        public readonly byte[] Payload;

        public EnterMobaGameReq(
            PlayerId playerId,
            string matchId,
            int mapId,
            int teamId,
            int heroId,
            int randomSeed,
            int tickRate,
            int inputDelayFrames,
            int opCode = 0,
            byte[] payload = null)
        {
            PlayerId = playerId;
            MatchId = matchId;
            MapId = mapId;
            TeamId = teamId;
            HeroId = heroId;
            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            OpCode = opCode;
            Payload = payload;
        }
    }

    public readonly struct EnterMobaGameRes
    {
        public readonly WorldId WorldId;
        public readonly PlayerId PlayerId;
        public readonly int LocalActorId;

        public readonly int RandomSeed;
        public readonly int TickRate;
        public readonly int InputDelayFrames;

        public readonly MobaPlayerEntry[] Players;

        public readonly int OpCode;
        public readonly byte[] Payload;

        public EnterMobaGameRes(
            WorldId worldId,
            PlayerId playerId,
            int localActorId,
            int randomSeed,
            int tickRate,
            int inputDelayFrames,
            MobaPlayerEntry[] players = null,
            int opCode = 0,
            byte[] payload = null)
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
        }
    }
}
