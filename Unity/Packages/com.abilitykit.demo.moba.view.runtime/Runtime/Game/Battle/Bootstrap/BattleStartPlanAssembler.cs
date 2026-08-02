using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Protocol.Moba;
using System;
using System.IO;
using UnityEngine;
using RoomGatewayOpCodes = AbilityKit.Protocol.Room.RoomGatewayOpCodes;

namespace AbilityKit.Game.Flow
{
    public static class BattleStartPlanAssembler
    {
        public static BattleStartPlan BuildPlan(
            BattleStartConfig config,
            in EnterMobaGameReq req,
            byte[] createWorldPayload,
            int createWorldOpCode,
            MobaBattleLaunchSpec launchSpec = default)
        {
            var options = BuildPlanOptions(config, in req, createWorldPayload, createWorldOpCode, launchSpec);
            return new BattleStartPlan(in options);
        }

        public static BattleStartPlanOptions BuildPlanOptions(
            BattleStartConfig config,
            in EnterMobaGameReq req,
            byte[] createWorldPayload,
            int createWorldOpCode,
            MobaBattleLaunchSpec launchSpec = default)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var preset = config.Preset;
            var runModeSo = preset != null ? preset.RunModeSO : config.RunModeSO;
            var gatewaySo = preset != null ? preset.GatewaySO : config.GatewaySO;
            var overrides = config.RuntimeOverrides;

            var runMode = runModeSo != null ? runModeSo.Mode : BattleStartConfig.BattleRunMode.Normal;
            var enableInputRecording = runMode == BattleStartConfig.BattleRunMode.Record;
            var enableInputReplay = runMode == BattleStartConfig.BattleRunMode.Replay;

            var recordPath = BuildRecordPath(runModeSo, overrides);
            var replayPath = runModeSo != null ? runModeSo.ReplayInputFilePath : string.Empty;
            if (overrides != null && overrides.HasReplayInputFilePath) replayPath = overrides.ReplayInputFilePath;

            var hostMode = preset != null ? preset.HostMode : config.HostMode;
            var gateway = gatewaySo;
            if (hostMode == BattleStartConfig.BattleHostMode.GatewayRemote && gateway == null)
            {
                throw new InvalidOperationException("GatewaySO is required when HostMode is GatewayRemote.");
            }

            var numericRoomId = 0UL;
            var joinRoomId = string.Empty;
            if (overrides != null && overrides.HasNumericRoomId) numericRoomId = overrides.NumericRoomId;
            if (overrides != null && overrides.HasGatewayJoinRoomId) joinRoomId = overrides.GatewayJoinRoomId;

            var autoConnect = preset != null ? preset.AutoConnect : config.AutoConnect;
            var autoCreateWorld = preset != null ? preset.AutoCreateWorld : config.AutoCreateWorld;
            var autoJoin = preset != null ? preset.AutoJoin : config.AutoJoin;
            var autoReady = preset != null ? preset.AutoReady : config.AutoReady;

            return BattleStartPlanBuilder
                .ForWorld(
                    worldId: GetEffectiveWorldId(config),
                    worldType: preset != null ? preset.WorldType : config.WorldType,
                    clientId: GetEffectiveClientId(config),
                    playerId: req.PlayerId.Value,
                    tickRate: req.TickRate,
                    inputDelayFrames: req.InputDelayFrames)
                .WithHostMode(hostMode)
                .WithSync(
                    syncMode: preset != null ? preset.SyncMode : config.SyncMode,
                    viewEventSourceMode: preset != null ? preset.ViewEventSourceMode : config.ViewEventSourceMode)
                .WithAuthority(
                    enableClientPrediction: preset != null ? preset.EnableClientPrediction : config.EnableClientPrediction,
                    enableConfirmedAuthorityWorld: preset != null ? preset.EnableConfirmedAuthorityWorld : config.EnableConfirmedAuthorityWorld)
                .WithSnapshotRegistries(preset != null ? preset.EnabledSnapshotRegistryIds : config.EnabledSnapshotRegistryIds)
                .WithGateway(
                    useGatewayTransport: gateway != null && gateway.UseGatewayTransport,
                    host: gateway != null ? gateway.Host : "127.0.0.1",
                    port: gateway != null ? gateway.Port : 4000,
                    numericRoomId: numericRoomId,
                    sessionToken: string.Empty,
                    region: gateway != null ? gateway.Region : "dev",
                    serverId: gateway != null ? gateway.ServerId : "local",
                    autoCreateRoom: false,
                    autoJoinRoom: false,
                    joinRoomId: joinRoomId,
                    createRoomOpCode: RoomGatewayOpCodes.CreateRoom,
                    joinRoomOpCode: RoomGatewayOpCodes.JoinRoom)
                .WithAutoFlow(autoConnect, autoCreateWorld, autoJoin, autoReady)
                .WithRunMode(
                    runMode: runMode,
                    enableInputRecording: enableInputRecording,
                    inputRecordOutputPath: recordPath,
                    enableInputReplay: enableInputReplay,
                    inputReplayPath: replayPath)
                .WithCreateWorld(createWorldOpCode, createWorldPayload)
                .WithTimeSync(
                    opCode: gateway != null ? gateway.TimeSyncOpCode : 1300u,
                    intervalMs: gateway != null ? gateway.TimeSyncIntervalMs : 1000,
                    alpha: gateway != null ? gateway.TimeSyncAlpha : 0.20,
                    timeoutMs: gateway != null ? gateway.TimeSyncTimeoutMs : 2000,
                    idealFrameSafetyConstMarginFrames: gateway != null ? gateway.IdealFrameSafetyConstMarginFrames : 2,
                    idealFrameSafetyRttFactor: gateway != null ? gateway.IdealFrameSafetyRttFactor : 1.0,
                    idealFrameSafetyMinMarginFrames: gateway != null ? gateway.IdealFrameSafetyMinMarginFrames : 0,
                    idealFrameSafetyMaxMarginFrames: gateway != null ? gateway.IdealFrameSafetyMaxMarginFrames : 30)
                .WithLaunchSpec(in launchSpec)
                .ToOptions();
        }

        private static string BuildRecordPath(BattleRunModeConfigSO runModeSo, BattleStartRuntimeOverrides overrides)
        {
            var recordDirectory = runModeSo != null ? runModeSo.RecordOutputDirectory : "battle_records";
            if (overrides != null && overrides.HasRecordOutputDirectory) recordDirectory = overrides.RecordOutputDirectory;
            if (string.IsNullOrEmpty(recordDirectory)) recordDirectory = "battle_records";

            var recordExt = (runModeSo != null && runModeSo.RecordFormat == BattleRunModeConfigSO.InputRecordFormat.Binary) ? "bin" : "json";
            var recordFileName = $"battle_record_{DateTime.Now:yyyyMMdd_HHmmss}.{recordExt}";
            var recordPath = Path.Combine(recordDirectory, recordFileName);
            return Path.IsPathRooted(recordPath) ? recordPath : Path.Combine(Application.persistentDataPath, recordPath);
        }

        private static string GetEffectiveWorldId(BattleStartConfig config)
        {
            if (config.RuntimeOverrides != null && config.RuntimeOverrides.HasWorldId) return config.RuntimeOverrides.WorldId;
            return config.Preset != null ? config.Preset.WorldId : config.WorldId;
        }

        private static string GetEffectiveClientId(BattleStartConfig config)
        {
            if (config.RuntimeOverrides != null && config.RuntimeOverrides.HasClientId) return config.RuntimeOverrides.ClientId;
            return config.Preset != null ? config.Preset.ClientId : config.ClientId;
        }
    }
}
