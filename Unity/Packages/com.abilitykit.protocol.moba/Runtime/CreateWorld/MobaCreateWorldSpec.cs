using AbilityKit.Ability.Host;
using MemoryPack;

namespace AbilityKit.Protocol.Moba
{
    [MemoryPackable]
    public readonly partial struct MobaCreateWorldSpec
    {
        [MemoryPackOrder(0)] public readonly string MatchId;
        [MemoryPackOrder(1)] public readonly int MapId;

        [MemoryPackOrder(2)] public readonly int RandomSeed;
        [MemoryPackOrder(3)] public readonly int TickRate;
        [MemoryPackOrder(4)] public readonly int InputDelayFrames;

        [MemoryPackOrder(5)] public readonly MobaPlayerLoadout[] Players;
        [MemoryPackOrder(6)] public readonly int GameplayId;

        [MemoryPackConstructor]
        public MobaCreateWorldSpec(string matchId, int mapId, int randomSeed, int tickRate, int inputDelayFrames, MobaPlayerLoadout[] players, int gameplayId = 0)
        {
            MatchId = matchId;
            MapId = mapId;
            GameplayId = gameplayId;
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
                players: Players,
                gameplayId: GameplayId);
        }

        public static MobaCreateWorldSpec FromEnterReq(in EnterMobaGameReq req)
        {
            return new MobaCreateWorldSpec(
                matchId: req.MatchId,
                mapId: req.MapId,
                randomSeed: req.RandomSeed,
                tickRate: req.TickRate,
                inputDelayFrames: req.InputDelayFrames,
                players: req.Players,
                gameplayId: req.GameplayId);
        }
    }
}
