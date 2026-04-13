using AbilityKit.Ability.Host;
using AbilityKit.Core.Generic;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Demo.Moba.CreateWorld
{
    public readonly struct MobaCreateWorldSpec
    {
        [BinaryMember(0)] public readonly string MatchId;
        [BinaryMember(1)] public readonly int MapId;

        [BinaryMember(2)] public readonly int RandomSeed;
        [BinaryMember(3)] public readonly int TickRate;
        [BinaryMember(4)] public readonly int InputDelayFrames;

        [BinaryMember(5)] public readonly MobaRoomPlayerSlot[] Players;

        public MobaCreateWorldSpec(string matchId, int mapId, int randomSeed, int tickRate, int inputDelayFrames, MobaRoomPlayerSlot[] players)
        {
            MatchId = matchId;
            MapId = mapId;
            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            Players = players;
        }

        public static MobaCreateWorldSpec FromRoomSpec(in MobaRoomGameStartSpec roomSpec)
        {
            return new MobaCreateWorldSpec(
                matchId: roomSpec.MatchId,
                mapId: roomSpec.MapId,
                randomSeed: roomSpec.RandomSeed,
                tickRate: roomSpec.TickRate,
                inputDelayFrames: roomSpec.InputDelayFrames,
                players: roomSpec.Players);
        }

        public EnterMobaGameReq ToLegacyEnterReq(PlayerId localPlayerId, int opCode, byte[] payload)
        {
            var ps = Players;
            if (ps == null || ps.Length == 0)
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
                    players: null);
            }

            var loadouts = new MobaPlayerLoadout[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                loadouts[i] = ps[i].ToLegacyLoadout(spawnIndexFallback: i);
            }

            return new EnterMobaGameReq(
                playerId: localPlayerId,
                matchId: MatchId,
                mapId: MapId,
                randomSeed: RandomSeed,
                tickRate: TickRate,
                inputDelayFrames: InputDelayFrames,
                opCode: opCode,
                payload: payload,
                players: loadouts);
        }
    }
}
