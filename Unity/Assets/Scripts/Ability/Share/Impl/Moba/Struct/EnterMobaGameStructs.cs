using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.Share;

namespace AbilityKit.Ability.Share.Impl.Moba.Struct
{
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
        [BinaryMember(3)] public readonly int TeamId;
        [BinaryMember(4)] public readonly int HeroId;

        [BinaryMember(5)] public readonly int RandomSeed;
        [BinaryMember(6)] public readonly int TickRate;
        [BinaryMember(7)] public readonly int InputDelayFrames;

        [BinaryMember(8)] public readonly int OpCode;
        [BinaryMember(9)] public readonly byte[] Payload;

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
        [BinaryMember(0)] public readonly WorldId WorldId;
        [BinaryMember(1)] public readonly PlayerId PlayerId;
        [BinaryMember(2)] public readonly int LocalActorId;

        [BinaryMember(3)] public readonly int RandomSeed;
        [BinaryMember(4)] public readonly int TickRate;
        [BinaryMember(5)] public readonly int InputDelayFrames;

        [BinaryMember(6)] public readonly MobaPlayerEntry[] Players;

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
