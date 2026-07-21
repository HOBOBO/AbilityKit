using System;

namespace AbilityKit.Game.Flow
{
    internal sealed class ExistingGatewayRoomBattleBootstrapper : IBattleBootstrapper
    {
        private readonly IBattleBootstrapper _inner;
        private readonly string _sessionToken;
        private readonly string _roomId;
        private readonly string _battleId;
        private readonly ulong _numericRoomId;
        private readonly ulong _worldId;

        public ExistingGatewayRoomBattleBootstrapper(
            IBattleBootstrapper inner,
            string sessionToken,
            string roomId,
            string battleId,
            ulong numericRoomId,
            ulong worldId)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _sessionToken = sessionToken ?? string.Empty;
            _roomId = roomId ?? string.Empty;
            _battleId = battleId ?? string.Empty;
            _numericRoomId = numericRoomId;
            _worldId = worldId;
        }

        public BattleStartPlan Build()
        {
            if (string.IsNullOrWhiteSpace(_sessionToken))
            {
                throw new InvalidOperationException("An authenticated Gateway session token is required.");
            }

            if (string.IsNullOrWhiteSpace(_roomId) ||
                string.IsNullOrWhiteSpace(_battleId) ||
                _numericRoomId == 0UL ||
                _worldId == 0UL)
            {
                throw new InvalidOperationException(
                    "Authoritative Gateway room, battle, numeric room, and world ids are required.");
            }

            var plan = _inner.Build();
            var world = plan.World;
            var gateway = plan.Gateway;
            var auto = plan.Auto;
            var runMode = plan.RunModeOptions;
            var createWorld = plan.CreateWorld;
            var timeSync = plan.TimeSync;

            return new BattleStartPlan(
                worldId: _worldId.ToString(),
                worldType: world.WorldType,
                clientId: world.ClientId,
                playerId: world.PlayerId,
                tickRate: world.TickRate,
                inputDelayFrames: world.InputDelayFrames,
                hostMode: BattleStartConfig.BattleHostMode.GatewayRemote,
                useGatewayTransport: true,
                gatewayHost: gateway.Host,
                gatewayPort: gateway.Port,
                numericRoomId: _numericRoomId,
                gatewaySessionToken: _sessionToken,
                gatewayRegion: gateway.Region,
                gatewayServerId: gateway.ServerId,
                gatewayAutoCreateRoom: false,
                gatewayAutoJoinRoom: false,
                gatewayJoinRoomId: _roomId,
                gatewayCreateRoomOpCode: gateway.CreateRoomOpCode,
                gatewayJoinRoomOpCode: gateway.JoinRoomOpCode,
                autoConnect: auto.AutoConnect,
                autoCreateWorld: auto.AutoCreateWorld,
                autoJoin: auto.AutoJoin,
                autoReady: auto.AutoReady,
                syncMode: plan.Sync.SyncMode,
                viewEventSourceMode: plan.Sync.ViewEventSourceMode,
                enableClientPrediction: plan.Authority.EnableClientPrediction,
                enableConfirmedAuthorityWorld: plan.Authority.EnableConfirmedAuthorityWorld,
                enableInputRecording: runMode.EnableInputRecording,
                inputRecordOutputPath: runMode.InputRecordOutputPath,
                enableInputReplay: runMode.EnableInputReplay,
                inputReplayPath: runMode.InputReplayPath,
                runMode: runMode.RunMode,
                createWorldOpCode: createWorld.OpCode,
                createWorldPayload: createWorld.Payload,
                timeSyncOpCode: timeSync.OpCode,
                timeSyncIntervalMs: timeSync.IntervalMs,
                timeSyncAlpha: timeSync.Alpha,
                timeSyncTimeoutMs: timeSync.TimeoutMs,
                idealFrameSafetyConstMarginFrames: timeSync.IdealFrameSafetyConstMarginFrames,
                idealFrameSafetyRttFactor: timeSync.IdealFrameSafetyRttFactor,
                idealFrameSafetyMinMarginFrames: timeSync.IdealFrameSafetyMinMarginFrames,
                idealFrameSafetyMaxMarginFrames: timeSync.IdealFrameSafetyMaxMarginFrames,
                enabledSnapshotRegistryIds: plan.Sync.EnabledSnapshotRegistryIds,
                launchSpec: plan.LaunchSpec,
                gatewayBattleId: _battleId);
        }
    }
}
