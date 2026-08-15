using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.CreateWorld;

namespace AbilityKit.Game.Flow
{
    internal sealed class ExistingGatewayRoomBattleBootstrapper :
        IBattleBootstrapper,
        IFlowGateProvider,
        IMobaReliableBattleEventCheckpointStore
    {
        private readonly BattleStartPlan _sourcePlan;
        private readonly string _sessionToken;
        private readonly string _roomId;
        private readonly string _battleId;
        private readonly ulong _numericRoomId;
        private readonly ulong _worldId;
        private readonly uint _localPlayerId;
        private readonly IMobaReliableBattleEventCheckpointStore _checkpointStore;
        private readonly string _gatewayHost;
        private readonly int _gatewayPort;
        private readonly string _gatewayRegion;
        private readonly string _gatewayServerId;
        private readonly IReadOnlyList<MultiplayerRoomPlayerSnapshot> _players;
        private readonly RoomGatewayNetworkSyncCapabilities _syncCapabilities;

        public ExistingGatewayRoomBattleBootstrapper(
            IBattleBootstrapper inner,
            string sessionToken,
            string roomId,
            string battleId,
            ulong numericRoomId,
            ulong worldId,
            uint localPlayerId,
            IMobaReliableBattleEventCheckpointStore checkpointStore = null,
            DemoMultiplayerLaunchRequest launchRequest = null,
            IReadOnlyList<MultiplayerRoomPlayerSnapshot> players = null,
            RoomGatewayNetworkSyncCapabilities syncCapabilities = null)
        {
            if (inner == null) throw new ArgumentNullException(nameof(inner));

            _sourcePlan = inner.Build();
            _sessionToken = sessionToken ?? string.Empty;
            _roomId = roomId ?? string.Empty;
            _battleId = battleId ?? string.Empty;
            _numericRoomId = numericRoomId;
            _worldId = worldId;
            _localPlayerId = localPlayerId;
            _checkpointStore = checkpointStore;
            _gatewayHost = !string.IsNullOrWhiteSpace(launchRequest?.Host)
                ? launchRequest.Host
                : _sourcePlan.Gateway.Host;
            _gatewayPort = launchRequest != null && launchRequest.Port > 0
                ? launchRequest.Port
                : _sourcePlan.Gateway.Port;
            _gatewayRegion = !string.IsNullOrWhiteSpace(launchRequest?.Region)
                ? launchRequest.Region
                : _sourcePlan.Gateway.Region;
            _gatewayServerId = !string.IsNullOrWhiteSpace(launchRequest?.ServerId)
                ? launchRequest.ServerId
                : _sourcePlan.Gateway.ServerId;
            _players = players;
            _syncCapabilities = syncCapabilities;
        }

        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_sessionToken);

        public bool IsRoomReady =>
            !string.IsNullOrWhiteSpace(_roomId) &&
            !string.IsNullOrWhiteSpace(_battleId) &&
            _numericRoomId != 0UL &&
            _worldId != 0UL &&
            _localPlayerId != 0u;

        public bool IsConnectivityReady =>
            !string.IsNullOrWhiteSpace(_gatewayHost) && _gatewayPort > 0;

        public bool IsAssetsReady => true;

        public BattleStartPlan Build()
        {
            if (string.IsNullOrWhiteSpace(_sessionToken))
            {
                throw new InvalidOperationException("An authenticated Gateway session token is required.");
            }

            if (string.IsNullOrWhiteSpace(_roomId) ||
                string.IsNullOrWhiteSpace(_battleId) ||
                _numericRoomId == 0UL ||
                _worldId == 0UL ||
                _localPlayerId == 0u)
            {
                throw new InvalidOperationException(
                    "Authoritative Gateway room, battle, numeric room, world, and local player ids are required.");
            }

            var plan = _sourcePlan;
            var world = plan.World;
            var gateway = plan.Gateway;
            var auto = plan.Auto;
            var runMode = plan.RunModeOptions;
            var createWorld = plan.CreateWorld;
            var timeSync = plan.TimeSync;
            var checkpoint = default(MobaReliableBattleEventCheckpoint);
            _checkpointStore?.TryLoad(_battleId, out checkpoint);
            var launchSpec = BuildAuthoritativeLaunchSpec(in plan.LaunchSpec);
            var createWorldPayload = launchSpec.ToCreateWorldInitPayload();
            return new BattleStartPlan(
                worldId: _worldId.ToString(),
                worldType: world.WorldType,
                clientId: world.ClientId,
                playerId: _localPlayerId.ToString(CultureInfo.InvariantCulture),
                tickRate: world.TickRate,
                inputDelayFrames: world.InputDelayFrames,
                hostMode: BattleHostMode.GatewayRemote,
                useGatewayTransport: true,
                gatewayHost: _gatewayHost,
                gatewayPort: _gatewayPort,
                numericRoomId: _numericRoomId,
                gatewaySessionToken: _sessionToken,
                gatewayRegion: _gatewayRegion,
                gatewayServerId: _gatewayServerId,
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
                createWorldPayload: MobaCreateWorldInitCodec.Serialize(in createWorldPayload),
                timeSyncOpCode: timeSync.OpCode,
                timeSyncIntervalMs: timeSync.IntervalMs,
                timeSyncAlpha: timeSync.Alpha,
                timeSyncTimeoutMs: timeSync.TimeoutMs,
                idealFrameSafetyConstMarginFrames: timeSync.IdealFrameSafetyConstMarginFrames,
                idealFrameSafetyRttFactor: timeSync.IdealFrameSafetyRttFactor,
                idealFrameSafetyMinMarginFrames: timeSync.IdealFrameSafetyMinMarginFrames,
                idealFrameSafetyMaxMarginFrames: timeSync.IdealFrameSafetyMaxMarginFrames,
                enabledSnapshotRegistryIds: plan.Sync.EnabledSnapshotRegistryIds,
                launchSpec: launchSpec,
                gatewayBattleId: _battleId,
                reliableEventCheckpoint: checkpoint,
                remoteSyncCapabilities: _syncCapabilities);
        }

        private MobaBattleLaunchSpec BuildAuthoritativeLaunchSpec(in MobaBattleLaunchSpec source)
        {
            var localPlayerId = new AbilityKit.Ability.Host.PlayerId(_localPlayerId.ToString(CultureInfo.InvariantCulture));
            var players = BuildAuthoritativePlayerLoadouts(source.Players);
            return new MobaBattleLaunchSpec(
                battleId: _battleId,
                matchId: _battleId,
                worldId: _worldId.ToString(CultureInfo.InvariantCulture),
                worldType: source.WorldType,
                clientId: source.ClientId,
                localPlayerId: localPlayerId,
                mapId: source.MapId,
                gameplayId: source.GameplayId,
                ruleSetId: source.RuleSetId,
                configVersion: source.ConfigVersion,
                protocolVersion: source.ProtocolVersion,
                randomSeed: source.RandomSeed,
                tickRate: source.TickRate,
                inputDelayFrames: source.InputDelayFrames,
                launchMode: source.LaunchMode,
                syncMode: source.SyncMode,
                authorityMode: source.AuthorityMode,
                players: players,
                enterGameOpCode: source.EnterGameOpCode,
                enterGamePayload: source.EnterGamePayload);
        }

        private MobaPlayerLoadout[] BuildAuthoritativePlayerLoadouts(MobaPlayerLoadout[] configuredPlayers)
        {
            if (_players == null || _players.Count == 0)
            {
                return configuredPlayers;
            }

            var result = new MobaPlayerLoadout[_players.Count];
            for (var i = 0; i < _players.Count; i++)
            {
                var player = _players[i] ?? throw new InvalidOperationException($"Room player snapshot is null at index {i}.");
                if (player.PlayerId == 0u)
                {
                    throw new InvalidOperationException($"Room player id is missing at index {i}.");
                }

                var hasConfigured = configuredPlayers != null && i < configuredPlayers.Length;
                var configured = hasConfigured ? configuredPlayers[i] : default;
                result[i] = new MobaPlayerLoadout(
                    playerId: new AbilityKit.Ability.Host.PlayerId(player.PlayerId.ToString(CultureInfo.InvariantCulture)),
                    teamId: player.TeamId,
                    heroId: player.HeroId,
                    attributeTemplateId: player.AttributeTemplateId,
                    level: player.Level,
                    basicAttackSkillId: player.BasicAttackSkillId,
                    skillIds: CopySkillIds(player.SkillIds),
                    spawnIndex: Math.Max(0, player.SpawnPointId),
                    unitSubType: configured.UnitSubType > 0 ? configured.UnitSubType : 1,
                    mainType: configured.MainType > 0 ? configured.MainType : 1,
                    hasSpawnPosition: 0,
                    spawnX: 0f,
                    spawnY: 0f,
                    spawnZ: 0f,
                    brainId: configured.BrainId,
                    enableBrainOnSpawn: !hasConfigured || configured.EnableBrainOnSpawn);
            }

            return result;
        }

        private static int[] CopySkillIds(IReadOnlyList<int> skillIds)
        {
            if (skillIds == null || skillIds.Count == 0) return Array.Empty<int>();
            var result = new int[skillIds.Count];
            for (var i = 0; i < result.Length; i++) result[i] = skillIds[i];
            return result;
        }

        public bool TryLoad(
            string battleId,
            out MobaReliableBattleEventCheckpoint checkpoint)
        {
            checkpoint = default;
            return _checkpointStore?.TryLoad(battleId, out checkpoint) == true;
        }

        public void Save(in MobaReliableBattleEventCheckpoint checkpoint)
        {
            _checkpointStore?.Save(in checkpoint);
        }
    }
}
