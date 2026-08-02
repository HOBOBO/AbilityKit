using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.CreateWorld;

namespace AbilityKit.Ability.Host.Extensions.Moba.CreateWorld
{
    public enum MobaBattleLaunchMode
    {
        Unspecified = 0,
        ViewFastEnter = 1,
        RoomFlow = 2,
        EtServer = 3,
        ConsoleSimulation = 4,
        Replay = 5,
    }

    public enum MobaBattleLaunchSyncMode
    {
        Unspecified = 0,
        FrameSync = 1,
        StateSync = 2,
        Hybrid = 3,
        Replay = 4,
    }

    public enum MobaBattleLaunchAuthorityMode
    {
        Unspecified = 0,
        LocalAuthority = 1,
        ServerAuthority = 2,
        ClientPrediction = 3,
    }

    public readonly struct MobaBattleLaunchSpec
    {
        public readonly string BattleId;
        public readonly string MatchId;
        public readonly string WorldId;
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly PlayerId LocalPlayerId;
        public readonly int MapId;
        public readonly int GameplayId;
        public readonly int RuleSetId;
        public readonly int ConfigVersion;
        public readonly int ProtocolVersion;
        public readonly int RandomSeed;
        public readonly int TickRate;
        public readonly int InputDelayFrames;
        public readonly MobaBattleLaunchMode LaunchMode;
        public readonly MobaBattleLaunchSyncMode SyncMode;
        public readonly MobaBattleLaunchAuthorityMode AuthorityMode;
        public readonly MobaPlayerLoadout[] Players;
        public readonly int EnterGameOpCode;
        public readonly byte[] EnterGamePayload;

        public MobaBattleLaunchSpec(
            string battleId,
            string matchId,
            string worldId,
            string worldType,
            string clientId,
            PlayerId localPlayerId,
            int mapId,
            int gameplayId,
            int ruleSetId,
            int configVersion,
            int protocolVersion,
            int randomSeed,
            int tickRate,
            int inputDelayFrames,
            MobaBattleLaunchMode launchMode,
            MobaBattleLaunchSyncMode syncMode,
            MobaBattleLaunchAuthorityMode authorityMode,
            MobaPlayerLoadout[] players,
            int enterGameOpCode = 0,
            byte[] enterGamePayload = null)
        {
            BattleId = string.IsNullOrEmpty(battleId) ? matchId : battleId;
            MatchId = matchId;
            WorldId = string.IsNullOrEmpty(worldId) ? matchId : worldId;
            WorldType = string.IsNullOrEmpty(worldType) ? "battle" : worldType;
            ClientId = clientId;
            LocalPlayerId = localPlayerId;
            MapId = mapId;
            GameplayId = gameplayId;
            RuleSetId = ruleSetId;
            ConfigVersion = configVersion;
            ProtocolVersion = protocolVersion;
            RandomSeed = randomSeed;
            TickRate = tickRate <= 0 ? 30 : tickRate;
            InputDelayFrames = inputDelayFrames < 0 ? 0 : inputDelayFrames;
            LaunchMode = launchMode;
            SyncMode = syncMode;
            AuthorityMode = authorityMode;
            Players = players;
            EnterGameOpCode = enterGameOpCode;
            EnterGamePayload = enterGamePayload;
        }

        public static MobaBattleLaunchSpec FromEnterReq(
            in EnterMobaGameReq req,
            string worldId = null,
            string worldType = null,
            string clientId = null,
            MobaBattleLaunchMode launchMode = MobaBattleLaunchMode.Unspecified,
            MobaBattleLaunchSyncMode syncMode = MobaBattleLaunchSyncMode.Unspecified,
            MobaBattleLaunchAuthorityMode authorityMode = MobaBattleLaunchAuthorityMode.Unspecified,
            int ruleSetId = 0,
            int configVersion = 0,
            int protocolVersion = 0)
        {
            return new MobaBattleLaunchSpec(
                battleId: req.MatchId,
                matchId: req.MatchId,
                worldId: worldId,
                worldType: worldType,
                clientId: clientId,
                localPlayerId: req.PlayerId,
                mapId: req.MapId,
                gameplayId: req.GameplayId,
                ruleSetId: ruleSetId,
                configVersion: configVersion,
                protocolVersion: protocolVersion,
                randomSeed: req.RandomSeed,
                tickRate: req.TickRate,
                inputDelayFrames: req.InputDelayFrames,
                launchMode: launchMode,
                syncMode: syncMode,
                authorityMode: authorityMode,
                players: req.Players,
                enterGameOpCode: req.OpCode,
                enterGamePayload: req.Payload);
        }

        public static MobaBattleLaunchSpec FromCreateWorldSpec(
            PlayerId localPlayerId,
            in MobaCreateWorldSpec createWorldSpec,
            string worldId = null,
            string worldType = null,
            string clientId = null,
            MobaBattleLaunchMode launchMode = MobaBattleLaunchMode.Unspecified,
            MobaBattleLaunchSyncMode syncMode = MobaBattleLaunchSyncMode.Unspecified,
            MobaBattleLaunchAuthorityMode authorityMode = MobaBattleLaunchAuthorityMode.Unspecified,
            int ruleSetId = 0,
            int configVersion = 0,
            int protocolVersion = 0,
            int enterGameOpCode = 0,
            byte[] enterGamePayload = null)
        {
            return new MobaBattleLaunchSpec(
                battleId: createWorldSpec.MatchId,
                matchId: createWorldSpec.MatchId,
                worldId: worldId,
                worldType: worldType,
                clientId: clientId,
                localPlayerId: localPlayerId,
                mapId: createWorldSpec.MapId,
                gameplayId: createWorldSpec.GameplayId,
                ruleSetId: ruleSetId,
                configVersion: configVersion,
                protocolVersion: protocolVersion,
                randomSeed: createWorldSpec.RandomSeed,
                tickRate: createWorldSpec.TickRate,
                inputDelayFrames: createWorldSpec.InputDelayFrames,
                launchMode: launchMode,
                syncMode: syncMode,
                authorityMode: authorityMode,
                players: createWorldSpec.Players,
                enterGameOpCode: enterGameOpCode,
                enterGamePayload: enterGamePayload);
        }

        public MobaCreateWorldSpec ToCreateWorldSpec()
        {
            return new MobaCreateWorldSpec(
                matchId: MatchId,
                mapId: MapId,
                randomSeed: RandomSeed,
                tickRate: TickRate,
                inputDelayFrames: InputDelayFrames,
                players: Players,
                gameplayId: GameplayId);
        }

        public EnterMobaGameReq ToEnterReq()
        {
            return ToCreateWorldSpec().ToEnterReq(LocalPlayerId, EnterGameOpCode, EnterGamePayload);
        }

        public MobaBattleStartPlan ToStartPlan()
        {
            var createWorldSpec = ToCreateWorldSpec();
            return new MobaBattleStartPlan(LocalPlayerId, in createWorldSpec, EnterGameOpCode, EnterGamePayload);
        }

        public MobaCreateWorldInitPayload ToCreateWorldInitPayload()
        {
            return ToStartPlan().ToCreateWorldInitPayload();
        }

        public WorldInitData ToWorldInitData(int initOpCode)
        {
            return ToStartPlan().ToWorldInitData(initOpCode);
        }

        public MobaGameStartSpec ToGameStartSpec()
        {
            return ToStartPlan().ToGameStartSpec();
        }
    }

    public readonly struct MobaBattleLaunchProfile
    {
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly MobaBattleLaunchMode LaunchMode;
        public readonly MobaBattleLaunchSyncMode SyncMode;
        public readonly MobaBattleLaunchAuthorityMode AuthorityMode;
        public readonly int RuleSetId;
        public readonly int ConfigVersion;
        public readonly int ProtocolVersion;
        public readonly int TickRate;
        public readonly int InputDelayFrames;

        public MobaBattleLaunchProfile(
            string worldType,
            string clientId,
            MobaBattleLaunchMode launchMode,
            MobaBattleLaunchSyncMode syncMode,
            MobaBattleLaunchAuthorityMode authorityMode,
            int ruleSetId = 0,
            int configVersion = 0,
            int protocolVersion = 0,
            int tickRate = 30,
            int inputDelayFrames = 0)
        {
            WorldType = string.IsNullOrEmpty(worldType) ? "battle" : worldType;
            ClientId = clientId;
            LaunchMode = launchMode;
            SyncMode = syncMode;
            AuthorityMode = authorityMode;
            RuleSetId = ruleSetId;
            ConfigVersion = configVersion;
            ProtocolVersion = protocolVersion;
            TickRate = tickRate <= 0 ? 30 : tickRate;
            InputDelayFrames = inputDelayFrames < 0 ? 0 : inputDelayFrames;
        }

        public static MobaBattleLaunchProfile Create(
            string clientId,
            MobaBattleLaunchMode launchMode,
            MobaBattleLaunchSyncMode syncMode,
            MobaBattleLaunchAuthorityMode authorityMode,
            string worldType = "battle",
            int tickRate = 30,
            int inputDelayFrames = 0,
            int ruleSetId = 0,
            int configVersion = 0,
            int protocolVersion = 0)
        {
            return new MobaBattleLaunchProfile(
                worldType,
                clientId,
                launchMode,
                syncMode,
                authorityMode,
                ruleSetId,
                configVersion,
                protocolVersion,
                tickRate,
                inputDelayFrames);
        }
    }

    public static class MobaBattleLaunchSpecBuilder
    {
        public static MobaBattleLaunchSpec FromLoadouts(
            string battleId,
            PlayerId localPlayerId,
            int mapId,
            MobaPlayerLoadout[] players,
            in MobaBattleLaunchProfile profile,
            string matchId = null,
            string worldId = null,
            int gameplayId = 0,
            int randomSeed = 0)
        {
            if (players == null || players.Length == 0)
            {
                throw new System.InvalidOperationException("MOBA battle launch requires explicit player loadouts.");
            }

            var effectiveBattleId = string.IsNullOrEmpty(battleId) ? matchId : battleId;
            var effectiveMatchId = string.IsNullOrEmpty(matchId) ? effectiveBattleId : matchId;
            var seed = randomSeed != 0 ? randomSeed : System.Environment.TickCount;

            return new MobaBattleLaunchSpec(
                battleId: effectiveBattleId,
                matchId: effectiveMatchId,
                worldId: string.IsNullOrEmpty(worldId) ? effectiveBattleId : worldId,
                worldType: profile.WorldType,
                clientId: profile.ClientId,
                localPlayerId: localPlayerId,
                mapId: mapId > 0 ? mapId : 1,
                gameplayId: gameplayId,
                ruleSetId: profile.RuleSetId,
                configVersion: profile.ConfigVersion,
                protocolVersion: profile.ProtocolVersion,
                randomSeed: seed,
                tickRate: profile.TickRate,
                inputDelayFrames: profile.InputDelayFrames,
                launchMode: profile.LaunchMode,
                syncMode: profile.SyncMode,
                authorityMode: profile.AuthorityMode,
                players: players);
        }

        public static MobaBattleLaunchSpec FromEnterReq(
            in EnterMobaGameReq req,
            string worldId = null,
            string worldType = null,
            string clientId = null,
            MobaBattleLaunchMode launchMode = MobaBattleLaunchMode.Unspecified,
            MobaBattleLaunchSyncMode syncMode = MobaBattleLaunchSyncMode.Unspecified,
            MobaBattleLaunchAuthorityMode authorityMode = MobaBattleLaunchAuthorityMode.Unspecified)
        {
            return MobaBattleLaunchSpec.FromEnterReq(in req, worldId, worldType, clientId, launchMode, syncMode, authorityMode);
        }

        public static MobaBattleLaunchSpec FromStartPlan(
            in MobaBattleStartPlan startPlan,
            string worldId = null,
            string worldType = null,
            string clientId = null,
            MobaBattleLaunchMode launchMode = MobaBattleLaunchMode.Unspecified,
            MobaBattleLaunchSyncMode syncMode = MobaBattleLaunchSyncMode.Unspecified,
            MobaBattleLaunchAuthorityMode authorityMode = MobaBattleLaunchAuthorityMode.Unspecified)
        {
            return MobaBattleLaunchSpec.FromCreateWorldSpec(
                startPlan.LocalPlayerId,
                in startPlan.CreateWorldSpec,
                worldId,
                worldType,
                clientId,
                launchMode,
                syncMode,
                authorityMode,
                enterGameOpCode: startPlan.EnterGameOpCode,
                enterGamePayload: startPlan.EnterGamePayload);
        }
    }
}
