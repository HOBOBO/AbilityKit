using AbilityKit.Ability.Host;
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
        public enum BattleRunMode
        {
            Normal = 0,
            Record = 1,
            Replay = 2,
        }

        public enum BattleHostMode
        {
            Local = 0,
            GatewayRemote = 1,
        }

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

            public BattleSyncMode SyncMode = BattleSyncMode.Lockstep;

            public BattleViewEventSourceMode ViewEventSourceMode = BattleViewEventSourceMode.SnapshotOnly;

            public bool EnableInputRecording = false;
            public string InputRecordOutputPath = "battle_record.json";

            public bool EnableInputReplay = false;
            public string InputReplayPath = "battle_record.json";
        }

        [System.Serializable]
        public sealed class BattleWorldPlanConfig
        {
            public string WorldId = "room_1";
            public string WorldType = "battle";
        }

        [System.Serializable]
        public sealed class HostConfig
        {
            public BattleHostMode Mode = BattleHostMode.Local;
        }

        [System.Serializable]
        public sealed class ClientPlanConfig
        {
            public string ClientId = "battle_client";
            public bool AutoConnect = true;
        }

        [System.Serializable]
        public sealed class WorldLifecyclePlanConfig
        {
            public int SelectedWorldIndex = 0;
            public List<BattleWorldPlanConfig> Worlds = new List<BattleWorldPlanConfig>
            {
                new BattleWorldPlanConfig { WorldId = "room_1", WorldType = "battle" }
            };

            public bool AutoCreateWorld = true;
            public bool AutoJoin = true;
            public bool AutoReady = true;
        }

        [System.Serializable]
        public sealed class RunModeConfig
        {
            public BattleRunMode Mode = BattleRunMode.Normal;

            public string RecordOutputPath = "battle_record.json";
            public string ReplayInputPath = "battle_record.json";
        }

        [System.Serializable]
        public sealed class EnterGameConfig
        {
            public int MapId = 1;
            public int RandomSeed = 12345;
            public int TickRate = 30;
            public int InputDelayFrames = 2;

            public int OpCode = 0;
            public string PayloadBase64;
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

        [System.Serializable]
        public sealed class GatewayConfig
        {
            public bool UseGatewayTransport = false;
            public string Host = "127.0.0.1";
            public int Port = 4000;
            public ulong NumericRoomId = 0;

            public string SessionToken = string.Empty;
            public string Region = "dev";
            public string ServerId = "local";

            public bool AutoCreateRoom = false;
            public bool AutoJoinRoom = false;
            public string JoinRoomId = string.Empty;

            public uint CreateRoomOpCode = 110;
            public uint JoinRoomOpCode = 111;
        }

        [System.Serializable]
        public sealed class BattleStartProfileConfig
        {
            public HostConfig Host = new HostConfig();
            public ClientPlanConfig Client = new ClientPlanConfig();
            public WorldLifecyclePlanConfig World = new WorldLifecyclePlanConfig();
            public RunModeConfig RunMode = new RunModeConfig();

            public BattleSyncMode SyncMode = BattleSyncMode.Lockstep;
            public BattleViewEventSourceMode ViewEventSourceMode = BattleViewEventSourceMode.SnapshotOnly;
        }

        [Header("Battle Start Plan")]
        public BattleStartPlanConfig StartPlan = new BattleStartPlanConfig();

        [Header("Battle Start Profile")]
        public BattleStartProfileConfig Profile = new BattleStartProfileConfig();

        [Header("Enter Game")]
        public EnterGameConfig EnterGame = new EnterGameConfig();

        [Header("Players")]
        public PlayersConfig Players = new PlayersConfig();

        [Header("Gateway")]
        public GatewayConfig Gateway = new GatewayConfig();

        public bool TryGetSelectedWorldPlan(out BattleWorldPlanConfig world)
        {
            world = null;

            var plans = Profile?.World?.Worlds;
            if (plans == null || plans.Count == 0) return false;

            var idx = Profile.World.SelectedWorldIndex;
            if (idx < 0) idx = 0;
            if (idx >= plans.Count) idx = plans.Count - 1;

            world = plans[idx];
            return world != null;
        }

        public bool TryBuildCreateWorldPayload(out int opCode, out byte[] payload)
        {
            opCode = 0;
            payload = null;

            var cfg = EnterGame;
            if (cfg == null) return false;

            opCode = cfg.OpCode;

            if (string.IsNullOrEmpty(cfg.PayloadBase64))
            {
                payload = null;
                return true;
            }

            try
            {
                payload = Convert.FromBase64String(cfg.PayloadBase64);
                return true;
            }
            catch
            {
                payload = null;
                return false;
            }
        }

        public EnterMobaGameReq BuildEnterMobaGameReq()
        {
            var playerId = Players != null && !string.IsNullOrEmpty(Players.LocalPlayerId) ? Players.LocalPlayerId : "p1";
            var loadouts = BuildPlayersLoadout(Players);

            var matchId = StartPlan != null ? StartPlan.WorldId : "room_1";
            if (TryGetSelectedWorldPlan(out var world) && !string.IsNullOrEmpty(world.WorldId))
            {
                matchId = world.WorldId;
            }

            var opCode = EnterGame != null ? EnterGame.OpCode : 0;
            byte[] payload = null;
            TryBuildCreateWorldPayload(out _, out payload);

            return new EnterMobaGameReq(
                playerId: new PlayerId(playerId),
                matchId: matchId,
                mapId: EnterGame != null ? EnterGame.MapId : 1,
                randomSeed: EnterGame != null ? EnterGame.RandomSeed : 12345,
                tickRate: EnterGame != null ? EnterGame.TickRate : 30,
                inputDelayFrames: EnterGame != null ? EnterGame.InputDelayFrames : 2,
                opCode: opCode,
                payload: payload,
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
