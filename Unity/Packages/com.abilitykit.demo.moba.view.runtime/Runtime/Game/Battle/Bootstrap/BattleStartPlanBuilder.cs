using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;

namespace AbilityKit.Game.Flow
{
    public readonly struct BattleStartPlanRequiredData
    {
        public readonly string WorldId;
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly string PlayerId;
        public readonly int TickRate;
        public readonly int InputDelayFrames;

        public BattleStartPlanRequiredData(
            string worldId,
            string worldType,
            string clientId,
            string playerId,
            int tickRate,
            int inputDelayFrames)
        {
            WorldId = worldId;
            WorldType = worldType;
            ClientId = clientId;
            PlayerId = playerId;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
        }
    }

    public sealed class BattleStartPlanBuilder
    {
        private readonly BattleStartPlanRequiredData _required;

        private BattleHostMode _hostMode = BattleHostMode.Local;
        private BattleSyncMode _syncMode = BattleSyncMode.Lockstep;
        private BattleViewEventSourceMode _viewEventSourceMode = BattleViewEventSourceMode.SnapshotOnly;
        private bool _enableClientPrediction = true;
        private bool _enableConfirmedAuthorityWorld;
        private string[] _enabledSnapshotRegistryIds;

        private BattleStartPlanGatewayOptions _gateway = new BattleStartPlanGatewayOptions(
            useGatewayTransport: false,
            host: "127.0.0.1",
            port: 4000,
            numericRoomId: 0,
            sessionToken: string.Empty,
            region: "dev",
            serverId: "local",
            autoCreateRoom: false,
            autoJoinRoom: false,
            joinRoomId: string.Empty,
            createRoomOpCode: 110,
            joinRoomOpCode: 111);

        private BattleStartPlanAutoOptions _auto = new BattleStartPlanAutoOptions(
            autoConnect: false,
            autoCreateWorld: false,
            autoJoin: false,
            autoReady: false);

        private BattleStartPlanRunModeOptions _runMode = new BattleStartPlanRunModeOptions(
            runMode: BattleRunMode.Normal,
            enableInputRecording: false,
            inputRecordOutputPath: string.Empty,
            enableInputReplay: false,
            inputReplayPath: string.Empty);

        private BattleStartPlanCreateWorldOptions _createWorld = new BattleStartPlanCreateWorldOptions(0, null);

        private BattleStartPlanTimeSyncOptions _timeSync = new BattleStartPlanTimeSyncOptions(
            opCode: 1300,
            intervalMs: 1000,
            alpha: 0.20,
            timeoutMs: 2000,
            idealFrameSafetyConstMarginFrames: 2,
            idealFrameSafetyRttFactor: 1.0,
            idealFrameSafetyMinMarginFrames: 0,
            idealFrameSafetyMaxMarginFrames: 30);

        private MobaBattleLaunchSpec _launchSpec;

        public BattleStartPlanBuilder(in BattleStartPlanRequiredData required)
        {
            _required = required;
        }

        public static BattleStartPlanBuilder ForWorld(
            string worldId,
            string worldType,
            string clientId,
            string playerId,
            int tickRate,
            int inputDelayFrames)
        {
            var required = new BattleStartPlanRequiredData(
                worldId,
                worldType,
                clientId,
                playerId,
                tickRate,
                inputDelayFrames);
            return new BattleStartPlanBuilder(in required);
        }

        public BattleStartPlanBuilder WithHostMode(BattleHostMode hostMode)
        {
            _hostMode = hostMode;
            return this;
        }

        public BattleStartPlanBuilder WithSync(BattleSyncMode syncMode, BattleViewEventSourceMode viewEventSourceMode)
        {
            _syncMode = syncMode;
            _viewEventSourceMode = viewEventSourceMode;
            return this;
        }

        public BattleStartPlanBuilder WithAuthority(bool enableClientPrediction, bool enableConfirmedAuthorityWorld)
        {
            _enableClientPrediction = enableClientPrediction;
            _enableConfirmedAuthorityWorld = enableConfirmedAuthorityWorld;
            return this;
        }

        public BattleStartPlanBuilder WithSnapshotRegistries(string[] enabledSnapshotRegistryIds)
        {
            _enabledSnapshotRegistryIds = enabledSnapshotRegistryIds;
            return this;
        }

        public BattleStartPlanBuilder WithGateway(in BattleStartPlanGatewayOptions gateway)
        {
            _gateway = gateway;
            return this;
        }

        public BattleStartPlanBuilder WithGateway(
            bool useGatewayTransport,
            string host,
            int port,
            ulong numericRoomId,
            string sessionToken,
            string region,
            string serverId,
            bool autoCreateRoom,
            bool autoJoinRoom,
            string joinRoomId,
            uint createRoomOpCode,
            uint joinRoomOpCode)
        {
            _gateway = new BattleStartPlanGatewayOptions(
                useGatewayTransport,
                host,
                port,
                numericRoomId,
                sessionToken,
                region,
                serverId,
                autoCreateRoom,
                autoJoinRoom,
                joinRoomId,
                createRoomOpCode,
                joinRoomOpCode);
            return this;
        }

        public BattleStartPlanBuilder WithAutoFlow(bool autoConnect, bool autoCreateWorld, bool autoJoin, bool autoReady)
        {
            _auto = new BattleStartPlanAutoOptions(autoConnect, autoCreateWorld, autoJoin, autoReady);
            return this;
        }

        public BattleStartPlanBuilder WithRunMode(
            BattleRunMode runMode,
            bool enableInputRecording,
            string inputRecordOutputPath,
            bool enableInputReplay,
            string inputReplayPath)
        {
            _runMode = new BattleStartPlanRunModeOptions(
                runMode,
                enableInputRecording,
                inputRecordOutputPath,
                enableInputReplay,
                inputReplayPath);
            return this;
        }

        public BattleStartPlanBuilder WithCreateWorld(int opCode, byte[] payload)
        {
            _createWorld = new BattleStartPlanCreateWorldOptions(opCode, payload);
            return this;
        }

        public BattleStartPlanBuilder WithTimeSync(
            uint opCode,
            int intervalMs,
            double alpha,
            int timeoutMs,
            int idealFrameSafetyConstMarginFrames,
            double idealFrameSafetyRttFactor,
            int idealFrameSafetyMinMarginFrames,
            int idealFrameSafetyMaxMarginFrames)
        {
            _timeSync = new BattleStartPlanTimeSyncOptions(
                opCode,
                intervalMs,
                alpha,
                timeoutMs,
                idealFrameSafetyConstMarginFrames,
                idealFrameSafetyRttFactor,
                idealFrameSafetyMinMarginFrames,
                idealFrameSafetyMaxMarginFrames);
            return this;
        }

        public BattleStartPlanBuilder WithLaunchSpec(in MobaBattleLaunchSpec launchSpec)
        {
            _launchSpec = launchSpec;
            return this;
        }

        public BattleStartPlanOptions ToOptions()
        {
            var world = new BattleStartPlanWorldOptions(
                _required.WorldId,
                _required.WorldType,
                _required.ClientId,
                _required.PlayerId,
                _required.TickRate,
                _required.InputDelayFrames);
            var sync = new BattleStartPlanSyncOptions(_syncMode, _viewEventSourceMode, _enabledSnapshotRegistryIds);
            var authority = new BattleStartPlanAuthorityOptions(_enableClientPrediction, _enableConfirmedAuthorityWorld);
            var options = new BattleStartPlanOptions(
                in world,
                _hostMode,
                in sync,
                in authority,
                in _gateway,
                in _auto,
                in _runMode,
                in _createWorld,
                in _timeSync,
                _launchSpec);
            BattleStartPlanValidator.Validate(in options);
            return options;
        }

        public BattleStartPlan Build()
        {
            var options = ToOptions();
            return new BattleStartPlan(in options);
        }
    }
}
