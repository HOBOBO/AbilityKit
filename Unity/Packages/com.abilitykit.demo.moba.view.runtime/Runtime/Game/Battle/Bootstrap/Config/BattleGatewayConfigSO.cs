using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle Gateway Config", fileName = "BattleGatewayConfig")]
    public sealed class BattleGatewayConfigSO : ScriptableObject
    {
        [LabelText("Use Gateway Transport")]
        public bool UseGatewayTransport;

        [LabelText("Host")]
        public string Host = "127.0.0.1";

        [LabelText("Port")]
        public int Port = 4000;

        [LabelText("Restore Fallback Player Id")]
        public uint RestoreFallbackPlayerId = 1u;

        [LabelText("Region")]
        public string Region = "dev";

        [LabelText("Server Id")]
        public string ServerId = "local";

        [Title("Formal Lobby")]
        [LabelText("Room Type")]
        public string RoomType = "moba";

        [LabelText("Room Title")]
        public string RoomTitle = "MOBA Room";

        [LabelText("Max Players")]
        public int MaxPlayers = 2;

        [LabelText("Min Players")]
        public int MinPlayers = 2;

        [LabelText("Room List Limit")]
        public int RoomListLimit = 10;

        [LabelText("Gameplay Id")]
        public int GameplayId = 1;

        [LabelText("Rule Set Id")]
        public int RuleSetId = 1;

        [LabelText("Config Version")]
        public int ConfigVersion = 1;

        [LabelText("Protocol Version")]
        public int ProtocolVersion = 1;

        [LabelText("World Type")]
        public string WorldType = "moba";

        [LabelText("Client Id")]
        public string ClientId = "moba-client";

        [LabelText("Restore Room On Entry")]
        public bool RestoreRoomOnEntry = true;

        [LabelText("Auto Ready Default Loadout")]
        public bool AutoReadyDefaultLoadout = true;

        [LabelText("Auto Start When Ready")]
        public bool AutoStartWhenReady;

        [LabelText("Auto Create When Empty")]
        public bool AutoCreateWhenEmpty = true;

        [LabelText("Default Hero Id")]
        public int DefaultHeroId = 1001;

        [LabelText("Default Team Id")]
        public int DefaultTeamId = 1;

        [LabelText("Default Spawn Point Id")]
        public int DefaultSpawnPointId;

        [LabelText("Default Hero Level")]
        public int DefaultHeroLevel = 1;

        [LabelText("Default Attribute Template Id")]
        public int DefaultAttributeTemplateId = 1001;

        [LabelText("Default Basic Attack Skill Id")]
        public int DefaultBasicAttackSkillId = 10010001;

        [LabelText("Default Skill Ids")]
        public int[] DefaultSkillIds = { 10010101, 10010201, 10010301 };

        [LabelText("Second Player Hero Id")]
        public int SecondPlayerHeroId = 1002;

        [LabelText("Second Player Team Id")]
        public int SecondPlayerTeamId = 2;

        [LabelText("Second Player Spawn Point Id")]
        public int SecondPlayerSpawnPointId;

        [LabelText("Second Player Hero Level")]
        public int SecondPlayerHeroLevel = 1;

        [LabelText("Second Player Attribute Template Id")]
        public int SecondPlayerAttributeTemplateId = 1002;

        [LabelText("Second Player Basic Attack Skill Id")]
        public int SecondPlayerBasicAttackSkillId = 10020001;

        [LabelText("Second Player Skill Ids")]
        public int[] SecondPlayerSkillIds = { 10020101, 10020201, 10020301 };

        [LabelText("Starter Scene")]
        public string StarterSceneName = "StarterScene";

        [Title("Network Timing")]
        [LabelText("TimeSync OpCode")]
        public uint TimeSyncOpCode = 1300;

        [LabelText("TimeSync Interval (ms)")]
        public int TimeSyncIntervalMs = 1000;

        [LabelText("TimeSync Alpha")]
        public double TimeSyncAlpha = 0.20;

        [LabelText("TimeSync Timeout (ms)")]
        public int TimeSyncTimeoutMs = 2000;

        [LabelText("Ideal Frame Safety Constant")]
        public int IdealFrameSafetyConstMarginFrames = 2;

        [LabelText("Ideal Frame Safety RTT Factor")]
        public double IdealFrameSafetyRttFactor = 1.0;

        [LabelText("Ideal Frame Safety Minimum")]
        public int IdealFrameSafetyMinMarginFrames;

        [LabelText("Ideal Frame Safety Maximum")]
        public int IdealFrameSafetyMaxMarginFrames = 30;

        public MultiplayerRoomLaunchSpec BuildRoomLaunchSpec(
            string sessionToken,
            string regionOverride = null,
            string serverIdOverride = null)
        {
            if (!TryValidateFormalLobby(out var error))
            {
                throw new InvalidOperationException(error);
            }

            return new MultiplayerRoomLaunchSpec
            {
                SessionToken = sessionToken ?? string.Empty,
                Region = Prefer(regionOverride, Region),
                ServerId = Prefer(serverIdOverride, ServerId),
                RoomType = RoomType.Trim(),
                RoomTitle = RoomTitle.Trim(),
                MaxPlayers = MaxPlayers,
                MinPlayers = MinPlayers,
                GameplayId = GameplayId,
                RuleSetId = RuleSetId,
                ConfigVersion = ConfigVersion,
                ProtocolVersion = ProtocolVersion,
                WorldType = WorldType.Trim(),
                ClientId = ClientId?.Trim() ?? string.Empty,
                SyncTemplateId = "frame-sync-authority",
                SyncModel = 1
            };
        }

        public MultiplayerLoadoutSpec BuildDefaultLoadout()
        {
            return new MultiplayerLoadoutSpec(
                Math.Max(1, DefaultHeroId),
                Math.Max(1, DefaultTeamId),
                Math.Max(0, DefaultSpawnPointId),
                Math.Max(1, DefaultHeroLevel),
                Math.Max(1, DefaultAttributeTemplateId),
                Math.Max(1, DefaultBasicAttackSkillId),
                DefaultSkillIds);
        }

        public MultiplayerLoadoutSpec BuildSecondPlayerLoadout()
        {
            return new MultiplayerLoadoutSpec(
                Math.Max(1, SecondPlayerHeroId),
                Math.Max(1, SecondPlayerTeamId),
                Math.Max(0, SecondPlayerSpawnPointId),
                Math.Max(1, SecondPlayerHeroLevel),
                Math.Max(1, SecondPlayerAttributeTemplateId),
                Math.Max(1, SecondPlayerBasicAttackSkillId),
                SecondPlayerSkillIds);
        }

        public bool TryValidateFormalLobby(out string error)
        {
            if (!UseGatewayTransport)
            {
                error = "Formal multiplayer requires Gateway transport.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(Host))
            {
                error = "Gateway host is required.";
                return false;
            }
            if (Port <= 0 || Port > 65535)
            {
                error = "Gateway port must be between 1 and 65535.";
                return false;
            }
            if (RestoreFallbackPlayerId == 0u)
            {
                error = "Restore fallback player id must be greater than zero.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(Region) || string.IsNullOrWhiteSpace(ServerId))
            {
                error = "Region and server id are required.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(RoomType) || string.IsNullOrWhiteSpace(RoomTitle))
            {
                error = "Room type and room title are required.";
                return false;
            }
            if (MaxPlayers <= 0)
            {
                error = "Maximum players must be greater than zero.";
                return false;
            }
            if (MinPlayers <= 0 || MinPlayers > MaxPlayers)
            {
                error = "Minimum players must be between 1 and maximum players.";
                return false;
            }
            if (GameplayId <= 0)
            {
                error = "Gameplay id must be greater than zero.";
                return false;
            }
            if (RuleSetId < 0 || ConfigVersion < 0 || ProtocolVersion < 0)
            {
                error = "Rule set, config, and protocol versions cannot be negative.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(WorldType))
            {
                error = "World type is required.";
                return false;
            }
            if (RoomListLimit <= 0 || RoomListLimit > 100)
            {
                error = "Room list limit must be between 1 and 100.";
                return false;
            }
            if (DefaultHeroId <= 0 ||
                DefaultTeamId <= 0 ||
                DefaultSpawnPointId < 0 ||
                DefaultHeroLevel <= 0 ||
                DefaultAttributeTemplateId <= 0 ||
                DefaultBasicAttackSkillId <= 0 ||
                DefaultSkillIds == null ||
                DefaultSkillIds.Length == 0 ||
                SecondPlayerHeroId <= 0 ||
                SecondPlayerTeamId <= 0 ||
                SecondPlayerSpawnPointId < 0 ||
                SecondPlayerHeroLevel <= 0 ||
                SecondPlayerAttributeTemplateId <= 0 ||
                SecondPlayerBasicAttackSkillId <= 0 ||
                SecondPlayerSkillIds == null ||
                SecondPlayerSkillIds.Length == 0)
            {
                error = "Default multiplayer loadouts are invalid.";
                return false;
            }
            if (!AllSkillIdsAreValid(DefaultSkillIds) || !AllSkillIdsAreValid(SecondPlayerSkillIds))
            {
                error = "Default multiplayer loadout skill ids must be greater than zero.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(StarterSceneName))
            {
                error = "Starter scene name is required.";
                return false;
            }
            if (TimeSyncOpCode == 0u || TimeSyncIntervalMs <= 0 || TimeSyncTimeoutMs <= 0)
            {
                error = "Time synchronization settings are invalid.";
                return false;
            }
            if (TimeSyncAlpha <= 0d || TimeSyncAlpha > 1d || IdealFrameSafetyRttFactor < 0d)
            {
                error = "Time synchronization smoothing settings are invalid.";
                return false;
            }
            if (IdealFrameSafetyConstMarginFrames < 0 ||
                IdealFrameSafetyMinMarginFrames < 0 ||
                IdealFrameSafetyMaxMarginFrames < IdealFrameSafetyMinMarginFrames)
            {
                error = "Ideal frame safety margins are invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool AllSkillIdsAreValid(int[] skillIds)
        {
            for (var i = 0; i < skillIds.Length; i++)
            {
                if (skillIds[i] <= 0) return false;
            }

            return true;
        }

        private static string Prefer(string preferred, string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred)
                ? preferred.Trim()
                : fallback.Trim();
        }
    }
}
