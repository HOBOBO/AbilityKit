using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share;
using MemoryPack;

namespace AbilityKit.Ability.Share.Impl.Moba.Struct
{
    [MemoryPackable]
    public readonly partial struct MobaCreateWorldSpec
    {
        [MemoryPackOrder(0), BinaryMember(0)] public readonly string MatchId;
        [MemoryPackOrder(1), BinaryMember(1)] public readonly int MapId;

        [MemoryPackOrder(2), BinaryMember(2)] public readonly int RandomSeed;
        [MemoryPackOrder(3), BinaryMember(3)] public readonly int TickRate;
        [MemoryPackOrder(4), BinaryMember(4)] public readonly int InputDelayFrames;

        [MemoryPackOrder(5), BinaryMember(5)] public readonly MobaPlayerLoadout[] Players;

        [MemoryPackConstructor]
        public MobaCreateWorldSpec(string matchId, int mapId, int randomSeed, int tickRate, int inputDelayFrames, MobaPlayerLoadout[] players)
        {
            MatchId = matchId;
            MapId = mapId;
            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            Players = players;
        }

        public EnterMobaGameReq ToEnterReq(PlayerId localPlayerId, int opCode, byte[] payload)
        {
            return new EnterMobaGameReq(
                playerId: localPlayerId,
                matchId: MatchId,
                mapId: MapId,
                randomSeed: RandomSeed,
                tickRate: TickRate,
                inputDelayFrames: InputDelayFrames,
                opCode: opCode,
                payload: payload,
                players: Players);
        }

        public static MobaCreateWorldSpec FromEnterReq(in EnterMobaGameReq req)
        {
            return new MobaCreateWorldSpec(
                matchId: req.MatchId,
                mapId: req.MapId,
                randomSeed: req.RandomSeed,
                tickRate: req.TickRate,
                inputDelayFrames: req.InputDelayFrames,
                players: req.Players);
        }
    }
}
