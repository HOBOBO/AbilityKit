using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Test Battle Start Config", fileName = "TestBattleStartConfig")]
    public sealed class TestBattleStartConfig : ScriptableObject
    {
        [Header("Battle Start Plan")]
        public string WorldId = "room_1";
        public string WorldType = "battle";
        public string ClientId = "battle_client";
        public string PlayerId = "p1";

        public bool AutoConnect = true;
        public bool AutoCreateWorld = true;
        public bool AutoJoin = true;
        public bool AutoReady = true;

        [Header("Enter Moba Game")]
        public int MapId = 1;
        public int TeamId = 1;
        public int HeroId = 10001;
        public int RandomSeed = 12345;
        public int TickRate = 30;
        public int InputDelayFrames = 2;

        [Header("Extra Player")]
        public string ExtraPlayerId = "p2";
        public int ExtraTeamId = 2;
        public int ExtraHeroId = 10002;
        public int ExtraSpawnIndex = 0;

        public EnterMobaGameReq BuildEnterMobaGameReq()
        {
            return new EnterMobaGameReq(
                playerId: new PlayerId(PlayerId),
                matchId: WorldId,
                mapId: MapId,
                teamId: TeamId,
                heroId: HeroId,
                randomSeed: RandomSeed,
                tickRate: TickRate,
                inputDelayFrames: InputDelayFrames,
                opCode: 0,
                payload: null,
                extraPlayerId: string.IsNullOrEmpty(ExtraPlayerId) ? default : new PlayerId(ExtraPlayerId),
                extraTeamId: ExtraTeamId,
                extraHeroId: ExtraHeroId,
                extraSpawnIndex: ExtraSpawnIndex
            );
        }
    }
}
