using System;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
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

        public readonly BattleStartConfig.BattleHostMode HostMode;
        public readonly MobaBattleLaunchSpec LaunchSpec;

        public BattleStartPlan(in BattleStartPlanOptions options)
            : this(
                worldId: options.WorldId,
                worldType: options.WorldType,
                clientId: options.ClientId,
                playerId: options.PlayerId,
                tickRate: options.TickRate,
                inputDelayFrames: options.InputDelayFrames,
                hostMode: options.HostMode,
                useGatewayTransport: options.Gateway.UseGatewayTransport,
                gatewayHost: options.Gateway.Host,
                gatewayPort: options.Gateway.Port,
                numericRoomId: options.Gateway.NumericRoomId,
                gatewaySessionToken: options.Gateway.SessionToken,
                gatewayRegion: options.Gateway.Region,
                gatewayServerId: options.Gateway.ServerId,
                gatewayAutoCreateRoom: options.Gateway.AutoCreateRoom,
                gatewayAutoJoinRoom: options.Gateway.AutoJoinRoom,
                gatewayJoinRoomId: options.Gateway.JoinRoomId,
                gatewayCreateRoomOpCode: options.Gateway.CreateRoomOpCode,
                gatewayJoinRoomOpCode: options.Gateway.JoinRoomOpCode,
                autoConnect: options.Auto.AutoConnect,
                autoCreateWorld: options.Auto.AutoCreateWorld,
                autoJoin: options.Auto.AutoJoin,
                autoReady: options.Auto.AutoReady,
                syncMode: options.SyncMode,
                viewEventSourceMode: options.ViewEventSourceMode,
                enableClientPrediction: options.EnableClientPrediction,
                enableConfirmedAuthorityWorld: options.EnableConfirmedAuthorityWorld,
                enableInputRecording: options.RunMode.EnableInputRecording,
                inputRecordOutputPath: options.RunMode.InputRecordOutputPath,
                enableInputReplay: options.RunMode.EnableInputReplay,
                inputReplayPath: options.RunMode.InputReplayPath,
                runMode: options.RunMode.RunMode,
                createWorldOpCode: options.CreateWorld.OpCode,
                createWorldPayload: options.CreateWorld.Payload,
                timeSyncOpCode: options.TimeSync.OpCode,
                timeSyncIntervalMs: options.TimeSync.IntervalMs,
                timeSyncAlpha: options.TimeSync.Alpha,
                timeSyncTimeoutMs: options.TimeSync.TimeoutMs,
                idealFrameSafetyConstMarginFrames: options.TimeSync.IdealFrameSafetyConstMarginFrames,
                idealFrameSafetyRttFactor: options.TimeSync.IdealFrameSafetyRttFactor,
                idealFrameSafetyMinMarginFrames: options.TimeSync.IdealFrameSafetyMinMarginFrames,
                idealFrameSafetyMaxMarginFrames: options.TimeSync.IdealFrameSafetyMaxMarginFrames,
                enabledSnapshotRegistryIds: options.EnabledSnapshotRegistryIds,
                launchSpec: options.LaunchSpec,
                gatewayBattleId: options.Gateway.BattleId)
        {
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
            return new BattleStartPlan(
                worldId: World.WorldId,
                worldType: World.WorldType,
                clientId: World.ClientId,
                playerId: World.PlayerId,
                tickRate: World.TickRate,
                inputDelayFrames: World.InputDelayFrames,
                hostMode: HostMode,
                useGatewayTransport: Gateway.UseGatewayTransport,
                gatewayHost: Gateway.Host,
                gatewayPort: Gateway.Port,
                numericRoomId: Gateway.NumericRoomId,
                gatewaySessionToken: gatewaySessionToken,
                gatewayRegion: Gateway.Region,
                gatewayServerId: Gateway.ServerId,
                gatewayAutoCreateRoom: Gateway.AutoCreateRoom,
                gatewayAutoJoinRoom: Gateway.AutoJoinRoom,
                gatewayJoinRoomId: Gateway.JoinRoomId,
                gatewayCreateRoomOpCode: Gateway.CreateRoomOpCode,
                gatewayJoinRoomOpCode: Gateway.JoinRoomOpCode,
                autoConnect: Auto.AutoConnect,
                autoCreateWorld: Auto.AutoCreateWorld,
                autoJoin: Auto.AutoJoin,
                autoReady: Auto.AutoReady,
                syncMode: Sync.SyncMode,
                viewEventSourceMode: Sync.ViewEventSourceMode,
                enableClientPrediction: Authority.EnableClientPrediction,
                enableConfirmedAuthorityWorld: Authority.EnableConfirmedAuthorityWorld,
                enableInputRecording: RunModeOptions.EnableInputRecording,
                inputRecordOutputPath: RunModeOptions.InputRecordOutputPath,
                enableInputReplay: RunModeOptions.EnableInputReplay,
                inputReplayPath: RunModeOptions.InputReplayPath,
                runMode: RunModeOptions.RunMode,
                createWorldOpCode: CreateWorld.OpCode,
                createWorldPayload: CreateWorld.Payload,
                timeSyncOpCode: TimeSync.OpCode,
                timeSyncIntervalMs: TimeSync.IntervalMs,
                timeSyncAlpha: TimeSync.Alpha,
                timeSyncTimeoutMs: TimeSync.TimeoutMs,
                idealFrameSafetyConstMarginFrames: TimeSync.IdealFrameSafetyConstMarginFrames,
                idealFrameSafetyRttFactor: TimeSync.IdealFrameSafetyRttFactor,
                idealFrameSafetyMinMarginFrames: TimeSync.IdealFrameSafetyMinMarginFrames,
                idealFrameSafetyMaxMarginFrames: TimeSync.IdealFrameSafetyMaxMarginFrames,
                enabledSnapshotRegistryIds: Sync.EnabledSnapshotRegistryIds,
                launchSpec: LaunchSpec,
                gatewayBattleId: Gateway.BattleId);
        }

        public BattleStartPlan WithGatewayRoom(string worldId, ulong numericRoomId)
        {
            return new BattleStartPlan(
                worldId: worldId,
                worldType: World.WorldType,
                clientId: World.ClientId,
                playerId: World.PlayerId,
                tickRate: World.TickRate,
                inputDelayFrames: World.InputDelayFrames,
                hostMode: HostMode,
                useGatewayTransport: Gateway.UseGatewayTransport,
                gatewayHost: Gateway.Host,
                gatewayPort: Gateway.Port,
                numericRoomId: numericRoomId,
                gatewaySessionToken: Gateway.SessionToken,
                gatewayRegion: Gateway.Region,
                gatewayServerId: Gateway.ServerId,
                gatewayAutoCreateRoom: Gateway.AutoCreateRoom,
                gatewayAutoJoinRoom: Gateway.AutoJoinRoom,
                gatewayJoinRoomId: Gateway.JoinRoomId,
                gatewayCreateRoomOpCode: Gateway.CreateRoomOpCode,
                gatewayJoinRoomOpCode: Gateway.JoinRoomOpCode,
                autoConnect: Auto.AutoConnect,
                autoCreateWorld: Auto.AutoCreateWorld,
                autoJoin: Auto.AutoJoin,
                autoReady: Auto.AutoReady,
                syncMode: Sync.SyncMode,
                viewEventSourceMode: Sync.ViewEventSourceMode,
                enableClientPrediction: Authority.EnableClientPrediction,
                enableConfirmedAuthorityWorld: Authority.EnableConfirmedAuthorityWorld,
                enableInputRecording: RunModeOptions.EnableInputRecording,
                inputRecordOutputPath: RunModeOptions.InputRecordOutputPath,
                enableInputReplay: RunModeOptions.EnableInputReplay,
                inputReplayPath: RunModeOptions.InputReplayPath,
                runMode: RunModeOptions.RunMode,
                createWorldOpCode: CreateWorld.OpCode,
                createWorldPayload: CreateWorld.Payload,
                timeSyncOpCode: TimeSync.OpCode,
                timeSyncIntervalMs: TimeSync.IntervalMs,
                timeSyncAlpha: TimeSync.Alpha,
                timeSyncTimeoutMs: TimeSync.TimeoutMs,
                idealFrameSafetyConstMarginFrames: TimeSync.IdealFrameSafetyConstMarginFrames,
                idealFrameSafetyRttFactor: TimeSync.IdealFrameSafetyRttFactor,
                idealFrameSafetyMinMarginFrames: TimeSync.IdealFrameSafetyMinMarginFrames,
                idealFrameSafetyMaxMarginFrames: TimeSync.IdealFrameSafetyMaxMarginFrames,
                enabledSnapshotRegistryIds: Sync.EnabledSnapshotRegistryIds,
                launchSpec: LaunchSpec,
                gatewayBattleId: Gateway.BattleId);
        }

        public BattleStartPlan WithInputReplay(string inputReplayPath)
        {
            return new BattleStartPlan(
                worldId: World.WorldId,
                worldType: World.WorldType,
                clientId: World.ClientId,
                playerId: World.PlayerId,
                tickRate: World.TickRate,
                inputDelayFrames: World.InputDelayFrames,
                hostMode: HostMode,
                useGatewayTransport: false,
                gatewayHost: Gateway.Host,
                gatewayPort: Gateway.Port,
                numericRoomId: Gateway.NumericRoomId,
                gatewaySessionToken: Gateway.SessionToken,
                gatewayRegion: Gateway.Region,
                gatewayServerId: Gateway.ServerId,
                gatewayAutoCreateRoom: false,
                gatewayAutoJoinRoom: false,
                gatewayJoinRoomId: Gateway.JoinRoomId,
                gatewayCreateRoomOpCode: Gateway.CreateRoomOpCode,
                gatewayJoinRoomOpCode: Gateway.JoinRoomOpCode,
                autoConnect: Auto.AutoConnect,
                autoCreateWorld: Auto.AutoCreateWorld,
                autoJoin: Auto.AutoJoin,
                autoReady: Auto.AutoReady,
                syncMode: BattleSyncMode.Lockstep,
                viewEventSourceMode: Sync.ViewEventSourceMode,
                enableClientPrediction: false,
                enableConfirmedAuthorityWorld: false,
                enableInputRecording: false,
                inputRecordOutputPath: string.Empty,
                enableInputReplay: true,
                inputReplayPath: inputReplayPath,
                runMode: BattleStartConfig.BattleRunMode.Replay,
                createWorldOpCode: CreateWorld.OpCode,
                createWorldPayload: CreateWorld.Payload,
                timeSyncOpCode: TimeSync.OpCode,
                timeSyncIntervalMs: TimeSync.IntervalMs,
                timeSyncAlpha: TimeSync.Alpha,
                timeSyncTimeoutMs: TimeSync.TimeoutMs,
                idealFrameSafetyConstMarginFrames: TimeSync.IdealFrameSafetyConstMarginFrames,
                idealFrameSafetyRttFactor: TimeSync.IdealFrameSafetyRttFactor,
                idealFrameSafetyMinMarginFrames: TimeSync.IdealFrameSafetyMinMarginFrames,
                idealFrameSafetyMaxMarginFrames: TimeSync.IdealFrameSafetyMaxMarginFrames,
                enabledSnapshotRegistryIds: Sync.EnabledSnapshotRegistryIds,
                launchSpec: LaunchSpec,
                gatewayBattleId: Gateway.BattleId);
        }

        public BattleStartPlan(
            string worldId,
            string worldType,
            string clientId,
            string playerId,
            int tickRate,
            int inputDelayFrames,
            BattleStartConfig.BattleHostMode hostMode,
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
            BattleStartConfig.BattleRunMode runMode,
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
            string gatewayBattleId = "")
        {
            World = new BattleStartPlanWorldOptions(
                worldId,
                worldType,
                clientId,
                playerId,
                tickRate,
                inputDelayFrames);
            Sync = new BattleStartPlanSyncOptions(syncMode, viewEventSourceMode, enabledSnapshotRegistryIds);
            Authority = new BattleStartPlanAuthorityOptions(enableClientPrediction, enableConfirmedAuthorityWorld);
            Gateway = new BattleStartPlanGatewayOptions(
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
                gatewayBattleId);
            Auto = new BattleStartPlanAutoOptions(autoConnect, autoCreateWorld, autoJoin, autoReady);
            RunModeOptions = new BattleStartPlanRunModeOptions(
                runMode,
                enableInputRecording,
                inputRecordOutputPath,
                enableInputReplay,
                inputReplayPath);
            CreateWorld = new BattleStartPlanCreateWorldOptions(createWorldOpCode, createWorldPayload);
            TimeSync = new BattleStartPlanTimeSyncOptions(
                timeSyncOpCode,
                timeSyncIntervalMs,
                timeSyncAlpha,
                timeSyncTimeoutMs,
                idealFrameSafetyConstMarginFrames,
                idealFrameSafetyRttFactor,
                idealFrameSafetyMinMarginFrames,
                idealFrameSafetyMaxMarginFrames);

            HostMode = hostMode;
            LaunchSpec = launchSpec;
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
                BattleStartConfig.BattleHostMode.Local,
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
                enableInputReplay ? BattleStartConfig.BattleRunMode.Replay : (enableInputRecording ? BattleStartConfig.BattleRunMode.Record : BattleStartConfig.BattleRunMode.Normal),
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
