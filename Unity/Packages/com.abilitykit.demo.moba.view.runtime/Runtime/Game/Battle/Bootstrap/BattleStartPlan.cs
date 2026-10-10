using System;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Flow
{
    public readonly struct BattleStartPlan
    {
        public readonly BattleStartPlanWorldOptions World;
        public readonly BattleStartPlanSyncOptions Sync;
        public readonly BattleStartPlanAuthorityOptions Authority;
        public readonly BattleStartPlanGatewayOptions Gateway;
        public readonly BattleStartPlanAutoOptions Auto;
        public readonly BattleStartPlanRunModeOptions RunModeOptions;
        public readonly BattleStartPlanCreateWorldOptions CreateWorld;
        public readonly BattleStartPlanTimeSyncOptions TimeSync;
        public readonly MobaReliableBattleEventCheckpoint ReliableEventCheckpoint;
        /// <summary>Gateway 为当前战斗代际声明的远端同步能力。</summary>
        public readonly RoomGatewayNetworkSyncCapabilities RemoteSyncCapabilities;

        public readonly BattleHostMode HostMode;
        public readonly MobaBattleLaunchSpec LaunchSpec;

        public BattleStartPlan(in BattleStartPlanOptions options)
        {
            World = options.World;
            Sync = options.Sync;
            Authority = options.Authority;
            Gateway = options.Gateway;
            Auto = options.Auto;
            RunModeOptions = options.RunMode;
            CreateWorld = options.CreateWorld;
            TimeSync = options.TimeSync;
            ReliableEventCheckpoint = options.ReliableEventCheckpoint;
            RemoteSyncCapabilities = options.RemoteSyncCapabilities;
            HostMode = options.HostMode;
            LaunchSpec = options.LaunchSpec;
        }

        public bool HasCanonicalLaunchSpec => !string.IsNullOrEmpty(LaunchSpec.MatchId) || LaunchSpec.Players != null;

        public MobaBattleLaunchSpec GetCanonicalLaunchSpec()
        {
            if (!HasCanonicalLaunchSpec)
            {
                throw new InvalidOperationException("BattleStartPlan requires a canonical MobaBattleLaunchSpec before runtime world startup.");
            }

            return LaunchSpec;
        }

        public MobaBattleStartPlan ToCanonicalStartPlan()
        {
            return GetCanonicalLaunchSpec().ToStartPlan();
        }

        public MobaGameStartSpec ToGameStartSpec()
        {
            return GetCanonicalLaunchSpec().ToGameStartSpec();
        }

        public BattleStartPlan WithGatewaySessionToken(string gatewaySessionToken)
        {
            var gateway = new BattleStartPlanGatewayOptions(
                Gateway.UseGatewayTransport, Gateway.Host, Gateway.Port, Gateway.NumericRoomId,
                gatewaySessionToken, Gateway.Region, Gateway.ServerId, Gateway.AutoCreateRoom,
                Gateway.AutoJoinRoom, Gateway.JoinRoomId, Gateway.CreateRoomOpCode,
                Gateway.JoinRoomOpCode, Gateway.BattleId, Gateway.MaxPlayers);
            return Rebuild(World, gateway, Sync, Authority, RunModeOptions);
        }

        public BattleStartPlan WithGatewayRoom(string worldId, ulong numericRoomId)
        {
            var world = new BattleStartPlanWorldOptions(
                worldId, World.WorldType, World.ClientId, World.PlayerId,
                World.TickRate, World.InputDelayFrames);
            var gateway = new BattleStartPlanGatewayOptions(
                Gateway.UseGatewayTransport, Gateway.Host, Gateway.Port, numericRoomId,
                Gateway.SessionToken, Gateway.Region, Gateway.ServerId, Gateway.AutoCreateRoom,
                Gateway.AutoJoinRoom, Gateway.JoinRoomId, Gateway.CreateRoomOpCode,
                Gateway.JoinRoomOpCode, Gateway.BattleId, Gateway.MaxPlayers);
            return Rebuild(world, gateway, Sync, Authority, RunModeOptions);
        }

        public BattleStartPlan WithInputReplay(string inputReplayPath)
        {
            var gateway = new BattleStartPlanGatewayOptions(
                false, Gateway.Host, Gateway.Port, Gateway.NumericRoomId,
                Gateway.SessionToken, Gateway.Region, Gateway.ServerId, false, false,
                Gateway.JoinRoomId, Gateway.CreateRoomOpCode, Gateway.JoinRoomOpCode,
                Gateway.BattleId, Gateway.MaxPlayers);
            var sync = new BattleStartPlanSyncOptions(
                BattleSyncMode.Lockstep, Sync.ViewEventSourceMode, Sync.EnabledSnapshotRegistryIds);
            var authority = new BattleStartPlanAuthorityOptions(false, false);
            var runMode = new BattleStartPlanRunModeOptions(
                BattleRunMode.Replay, false, string.Empty, true, inputReplayPath);
            return Rebuild(World, gateway, sync, authority, runMode);
        }

        private BattleStartPlan Rebuild(
            in BattleStartPlanWorldOptions world,
            in BattleStartPlanGatewayOptions gateway,
            in BattleStartPlanSyncOptions sync,
            in BattleStartPlanAuthorityOptions authority,
            in BattleStartPlanRunModeOptions runMode)
        {
            var options = new BattleStartPlanOptions(
                in world, HostMode, in sync, in authority, in gateway, in Auto,
                in runMode, in CreateWorld, in TimeSync, LaunchSpec,
                ReliableEventCheckpoint, RemoteSyncCapabilities);
            return new BattleStartPlan(in options);
        }
        public BattleStartPlan(
            string worldId,
            string worldType,
            string clientId,
            string playerId,
            int tickRate,
            int inputDelayFrames,
            BattleHostMode hostMode,
            bool useGatewayTransport,
            string gatewayHost,
            int gatewayPort,
            ulong numericRoomId,
            string gatewaySessionToken,
            string gatewayRegion,
            string gatewayServerId,
            bool gatewayAutoCreateRoom,
            bool gatewayAutoJoinRoom,
            string gatewayJoinRoomId,
            uint gatewayCreateRoomOpCode,
            uint gatewayJoinRoomOpCode,
            bool autoConnect,
            bool autoCreateWorld,
            bool autoJoin,
            bool autoReady,
            BattleSyncMode syncMode,
            BattleViewEventSourceMode viewEventSourceMode,
            bool enableClientPrediction,
            bool enableConfirmedAuthorityWorld,
            bool enableInputRecording,
            string inputRecordOutputPath,
            bool enableInputReplay,
            string inputReplayPath,
            BattleRunMode runMode,
            int createWorldOpCode,
            byte[] createWorldPayload,
            uint timeSyncOpCode = 1300,
            int timeSyncIntervalMs = 1000,
            double timeSyncAlpha = 0.20,
            int timeSyncTimeoutMs = 2000,
            int idealFrameSafetyConstMarginFrames = 2,
            double idealFrameSafetyRttFactor = 1.0,
            int idealFrameSafetyMinMarginFrames = 0,
            int idealFrameSafetyMaxMarginFrames = 30,
            string[] enabledSnapshotRegistryIds = null,
            MobaBattleLaunchSpec launchSpec = default,
            string gatewayBattleId = "",
            MobaReliableBattleEventCheckpoint reliableEventCheckpoint = default,
            RoomGatewayNetworkSyncCapabilities remoteSyncCapabilities = null,
            int gatewayMaxPlayers = 2)
            : this(new BattleStartPlanOptions(
                new BattleStartPlanWorldOptions(worldId, worldType, clientId, playerId, tickRate, inputDelayFrames),
                hostMode,
                new BattleStartPlanSyncOptions(syncMode, viewEventSourceMode, enabledSnapshotRegistryIds),
                new BattleStartPlanAuthorityOptions(enableClientPrediction, enableConfirmedAuthorityWorld),
                new BattleStartPlanGatewayOptions(
                    useGatewayTransport, gatewayHost, gatewayPort, numericRoomId, gatewaySessionToken,
                    gatewayRegion, gatewayServerId, gatewayAutoCreateRoom, gatewayAutoJoinRoom,
                    gatewayJoinRoomId, gatewayCreateRoomOpCode, gatewayJoinRoomOpCode,
                    gatewayBattleId, gatewayMaxPlayers),
                new BattleStartPlanAutoOptions(autoConnect, autoCreateWorld, autoJoin, autoReady),
                new BattleStartPlanRunModeOptions(
                    runMode, enableInputRecording, inputRecordOutputPath, enableInputReplay, inputReplayPath),
                new BattleStartPlanCreateWorldOptions(createWorldOpCode, createWorldPayload),
                new BattleStartPlanTimeSyncOptions(
                    timeSyncOpCode, timeSyncIntervalMs, timeSyncAlpha, timeSyncTimeoutMs,
                    idealFrameSafetyConstMarginFrames, idealFrameSafetyRttFactor,
                    idealFrameSafetyMinMarginFrames, idealFrameSafetyMaxMarginFrames),
                launchSpec, reliableEventCheckpoint, remoteSyncCapabilities))
        {
        }

        public BattleStartPlan(
            string worldId,
            string worldType,
            string clientId,
            string playerId,
            int tickRate,
            int inputDelayFrames,
            bool useGatewayTransport,
            string gatewayHost,
            int gatewayPort,
            ulong numericRoomId,
            string gatewaySessionToken,
            string gatewayRegion,
            string gatewayServerId,
            bool gatewayAutoCreateRoom,
            bool gatewayAutoJoinRoom,
            string gatewayJoinRoomId,
            uint gatewayCreateRoomOpCode,
            uint gatewayJoinRoomOpCode,
            bool autoConnect,
            bool autoCreateWorld,
            bool autoJoin,
            bool autoReady,
            BattleSyncMode syncMode,
            BattleViewEventSourceMode viewEventSourceMode,
            bool enableConfirmedAuthorityWorld,
            bool enableInputRecording,
            string inputRecordOutputPath,
            bool enableInputReplay,
            string inputReplayPath,
            int createWorldOpCode,
            byte[] createWorldPayload,
            uint timeSyncOpCode = 1300,
            int timeSyncIntervalMs = 1000,
            double timeSyncAlpha = 0.20,
            int timeSyncTimeoutMs = 2000,
            int idealFrameSafetyConstMarginFrames = 2,
            double idealFrameSafetyRttFactor = 1.0,
            int idealFrameSafetyMinMarginFrames = 0,
            int idealFrameSafetyMaxMarginFrames = 30,
            string[] enabledSnapshotRegistryIds = null)
            : this(
                worldId,
                worldType,
                clientId,
                playerId,
                tickRate,
                inputDelayFrames,
                BattleHostMode.Local,
                useGatewayTransport,
                gatewayHost,
                gatewayPort,
                numericRoomId,
                gatewaySessionToken,
                gatewayRegion,
                gatewayServerId,
                gatewayAutoCreateRoom,
                gatewayAutoJoinRoom,
                gatewayJoinRoomId,
                gatewayCreateRoomOpCode,
                gatewayJoinRoomOpCode,
                autoConnect,
                autoCreateWorld,
                autoJoin,
                autoReady,
                syncMode,
                viewEventSourceMode,
                true,
                enableConfirmedAuthorityWorld,
                enableInputRecording,
                inputRecordOutputPath,
                enableInputReplay,
                inputReplayPath,
                enableInputReplay ? BattleRunMode.Replay : (enableInputRecording ? BattleRunMode.Record : BattleRunMode.Normal),
                createWorldOpCode,
                createWorldPayload,
                timeSyncOpCode,
                timeSyncIntervalMs,
                timeSyncAlpha,
                timeSyncTimeoutMs,
                idealFrameSafetyConstMarginFrames,
                idealFrameSafetyRttFactor,
                idealFrameSafetyMinMarginFrames,
                idealFrameSafetyMaxMarginFrames,
                enabledSnapshotRegistryIds)
        {
        }
    }
}
