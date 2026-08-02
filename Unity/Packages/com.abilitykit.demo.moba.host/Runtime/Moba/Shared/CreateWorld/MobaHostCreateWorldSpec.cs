using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;
using AbilityKit.Protocol.Moba;
using ProtocolCreateWorldSpec = AbilityKit.Protocol.Moba.MobaCreateWorldSpec;

namespace AbilityKit.Ability.Host.Extensions.Moba.CreateWorld
{
    public readonly struct MobaHostCreateWorldSpec
    {
        public readonly string MatchId;
        public readonly int MapId;
        public readonly int RandomSeed;
        public readonly int TickRate;
        public readonly int InputDelayFrames;
        public readonly MobaRoomPlayerSlot[] Players;
        public readonly int GameplayId;

        public MobaHostCreateWorldSpec(string matchId, int mapId, int randomSeed, int tickRate, int inputDelayFrames, MobaRoomPlayerSlot[] players, int gameplayId = 0)
        {
            MatchId = matchId;
            MapId = mapId;
            GameplayId = gameplayId;
            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            Players = players;
        }

        public static MobaHostCreateWorldSpec FromRoomSpec(in MobaRoomGameStartSpec roomSpec)
        {
            return new MobaHostCreateWorldSpec(
                matchId: roomSpec.MatchId,
                mapId: roomSpec.MapId,
                randomSeed: roomSpec.RandomSeed,
                tickRate: roomSpec.TickRate,
                inputDelayFrames: roomSpec.InputDelayFrames,
                players: roomSpec.Players,
                gameplayId: roomSpec.GameplayId);
        }

        public ProtocolCreateWorldSpec ToProtocolSpec()
        {
            return new ProtocolCreateWorldSpec(
                matchId: MatchId,
                mapId: MapId,
                randomSeed: RandomSeed,
                tickRate: TickRate,
                inputDelayFrames: InputDelayFrames,
                players: BuildPlayerLoadouts(),
                gameplayId: GameplayId);
        }

        public EnterMobaGameReq ToEnterReq(PlayerId localPlayerId, int opCode, byte[] payload)
        {
            return ToProtocolSpec().ToEnterReq(localPlayerId, opCode, payload);
        }

        private MobaPlayerLoadout[] BuildPlayerLoadouts()
        {
            var ps = Players;
            if (ps == null || ps.Length == 0)
            {
                throw new System.InvalidOperationException("MOBA host create-world spec requires explicit room player loadouts.");
            }

            var loadouts = new MobaPlayerLoadout[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                loadouts[i] = ps[i].ToPlayerLoadout(spawnIndexFallback: i);
            }

            return loadouts;
        }
    }
}

