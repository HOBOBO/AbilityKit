using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Impl.Moba;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle Start Config", fileName = "BattleStartConfig")]
    public sealed class BattleStartConfig : ScriptableObject
    {
        [System.Serializable]
        public sealed class BattleStartPlanConfig
        {
            public string WorldId = "room_1";
            public string WorldType = "battle";
            public string ClientId = "battle_client";

            public bool AutoConnect = true;
            public bool AutoCreateWorld = true;
            public bool AutoJoin = true;
            public bool AutoReady = true;
        }

        [System.Serializable]
        public sealed class EnterGameConfig
        {
            public int MapId = 1;
            public int RandomSeed = 12345;
            public int TickRate = 30;
            public int InputDelayFrames = 2;
        }

        [System.Serializable]
        public sealed class PlayerConfig
        {
            public string PlayerId;
            public Team TeamId = Team.Team1;
            public EntityMainType MainType = EntityMainType.Unit;
            public UnitSubType UnitSubType = UnitSubType.Hero;
            public int HeroId = 10001;
            public int AttributeTemplateId = 0;
            public int Level = 1;
            public int BasicAttackSkillId = 1;
            public int[] SkillIds;
            public int SpawnIndex = 0;
            public Vector3 SpawnPosition = default;
        }

        [System.Serializable]
        public sealed class PlayersConfig
        {
            public string LocalPlayerId = "p1";
            public List<PlayerConfig> Team1Players = new List<PlayerConfig> { new PlayerConfig { PlayerId = "p1", TeamId = Team.Team1, HeroId = 10001, SpawnIndex = 0 } };
            public List<PlayerConfig> Team2Players = new List<PlayerConfig> { new PlayerConfig { PlayerId = "p2", TeamId = Team.Team2, HeroId = 10002, SpawnIndex = 0 } };
        }

        [Header("Battle Start Plan")]
        public BattleStartPlanConfig StartPlan = new BattleStartPlanConfig();

        [Header("Enter Game")]
        public EnterGameConfig EnterGame = new EnterGameConfig();

        [Header("Players")]
        public PlayersConfig Players = new PlayersConfig();

        public EnterMobaGameReq BuildEnterMobaGameReq()
        {
            var playerId = Players != null && !string.IsNullOrEmpty(Players.LocalPlayerId) ? Players.LocalPlayerId : "p1";
            var loadouts = BuildPlayersLoadout(Players);

            return new EnterMobaGameReq(
                playerId: new PlayerId(playerId),
                matchId: StartPlan != null ? StartPlan.WorldId : "room_1",
                mapId: EnterGame != null ? EnterGame.MapId : 1,
                randomSeed: EnterGame != null ? EnterGame.RandomSeed : 12345,
                tickRate: EnterGame != null ? EnterGame.TickRate : 30,
                inputDelayFrames: EnterGame != null ? EnterGame.InputDelayFrames : 2,
                opCode: 0,
                payload: null,
                players: loadouts
            );
        }

        private static MobaPlayerLoadout[] BuildPlayersLoadout(PlayersConfig cfg)
        {
            if (cfg == null) return null;

            var list = new List<MobaPlayerLoadout>(4);

            if (cfg.Team1Players != null)
            {
                for (int i = 0; i < cfg.Team1Players.Count; i++)
                {
                    var p = cfg.Team1Players[i];
                    if (p == null || string.IsNullOrEmpty(p.PlayerId)) continue;
                    list.Add(new MobaPlayerLoadout(
                        new PlayerId(p.PlayerId),
                        (int)p.TeamId,
                        p.HeroId,
                        p.AttributeTemplateId,
                        p.Level,
                        p.BasicAttackSkillId,
                        p.SkillIds,
                        p.SpawnIndex,
                        (int)p.UnitSubType,
                        (int)p.MainType,
                        hasSpawnPosition: 1,
                        spawnX: p.SpawnPosition.x,
                        spawnY: p.SpawnPosition.y,
                        spawnZ: p.SpawnPosition.z));
                }
            }

            if (cfg.Team2Players != null)
            {
                for (int i = 0; i < cfg.Team2Players.Count; i++)
                {
                    var p = cfg.Team2Players[i];
                    if (p == null || string.IsNullOrEmpty(p.PlayerId)) continue;
                    list.Add(new MobaPlayerLoadout(
                        new PlayerId(p.PlayerId),
                        (int)p.TeamId,
                        p.HeroId,
                        p.AttributeTemplateId,
                        p.Level,
                        p.BasicAttackSkillId,
                        p.SkillIds,
                        p.SpawnIndex,
                        (int)p.UnitSubType,
                        (int)p.MainType,
                        hasSpawnPosition: 1,
                        spawnX: p.SpawnPosition.x,
                        spawnY: p.SpawnPosition.y,
                        spawnZ: p.SpawnPosition.z));
                }
            }

            return list.Count == 0 ? null : list.ToArray();
        }
    }
}
