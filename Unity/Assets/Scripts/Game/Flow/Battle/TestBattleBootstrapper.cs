using AbilityKit.Ability.Impl.Moba.Systems;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;

namespace AbilityKit.Game.Flow
{
    public sealed class TestBattleBootstrapper : IBattleBootstrapper
    {
        public BattleStartPlan Build()
        {
            var worldId = "room_1";
            var playerId = "p1";

            var req = new EnterMobaGameReq(
                playerId: new PlayerId(playerId),
                matchId: worldId,
                mapId: 1,
                teamId: 1,
                heroId: 10001,
                randomSeed: 12345,
                tickRate: 30,
                inputDelayFrames: 2,
                opCode: 0,
                payload: null,
                extraPlayerId: new PlayerId("p2"),
                extraTeamId: 2,
                extraHeroId: 10002,
                extraSpawnIndex: 0
            );

            var payload = EnterMobaGameCodec.SerializeReq(req);

            return new BattleStartPlan(
                worldId: worldId,
                worldType: "battle",
                clientId: "battle_client",
                playerId: playerId,
                autoConnect: true,
                autoCreateWorld: true,
                autoJoin: true,
                autoReady: true,
                createWorldOpCode: MobaWorldBootstrapModule.InitOpCode,
                createWorldPayload: payload
            );
        }
    }
}
