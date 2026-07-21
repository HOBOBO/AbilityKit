using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;

namespace AbilityKit.Game.Flow
{
    public readonly struct BattleStartPlanWorldOptions
    {
        public readonly string WorldId;
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly string PlayerId;
        public readonly int TickRate;
        public readonly int InputDelayFrames;

        public BattleStartPlanWorldOptions(
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

    public readonly struct BattleStartPlanSyncOptions
    {
        public readonly BattleSyncMode SyncMode;
        public readonly BattleViewEventSourceMode ViewEventSourceMode;
        public readonly string[] EnabledSnapshotRegistryIds;

        public BattleStartPlanSyncOptions(
            BattleSyncMode syncMode,
            BattleViewEventSourceMode viewEventSourceMode,
            string[] enabledSnapshotRegistryIds)
        {
            SyncMode = syncMode;
            ViewEventSourceMode = viewEventSourceMode;
            EnabledSnapshotRegistryIds = enabledSnapshotRegistryIds;
        }
    }

    public readonly struct BattleStartPlanAuthorityOptions
    {
        public readonly bool EnableClientPrediction;
        public readonly bool EnableConfirmedAuthorityWorld;

        public BattleStartPlanAuthorityOptions(bool enableClientPrediction, bool enableConfirmedAuthorityWorld)
        {
            EnableClientPrediction = enableClientPrediction;
            EnableConfirmedAuthorityWorld = enableConfirmedAuthorityWorld;
        }
    }

    public readonly struct BattleStartPlanGatewayOptions
    {
        public readonly bool UseGatewayTransport;
        public readonly string Host;
        public readonly int Port;
        public readonly ulong NumericRoomId;
        public readonly string SessionToken;
        public readonly string Region;
        public readonly string ServerId;
        public readonly bool AutoCreateRoom;
        public readonly bool AutoJoinRoom;
        public readonly string JoinRoomId;
        public readonly uint CreateRoomOpCode;
        public readonly uint JoinRoomOpCode;
        public readonly string BattleId;

        public BattleStartPlanGatewayOptions(
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
            uint joinRoomOpCode,
            string battleId = "")
        {
            UseGatewayTransport = useGatewayTransport;
            Host = host;
            Port = port;
            NumericRoomId = numericRoomId;
            SessionToken = sessionToken;
            Region = region;
            ServerId = serverId;
            AutoCreateRoom = autoCreateRoom;
            AutoJoinRoom = autoJoinRoom;
            JoinRoomId = joinRoomId;
            CreateRoomOpCode = createRoomOpCode;
            JoinRoomOpCode = joinRoomOpCode;
            BattleId = battleId ?? string.Empty;
        }
    }

    public readonly struct BattleStartPlanAutoOptions
    {
        public readonly bool AutoConnect;
        public readonly bool AutoCreateWorld;
        public readonly bool AutoJoin;
        public readonly bool AutoReady;

        public BattleStartPlanAutoOptions(bool autoConnect, bool autoCreateWorld, bool autoJoin, bool autoReady)
        {
            AutoConnect = autoConnect;
            AutoCreateWorld = autoCreateWorld;
            AutoJoin = autoJoin;
            AutoReady = autoReady;
        }
    }

    public readonly struct BattleStartPlanRunModeOptions
    {
        public readonly BattleStartConfig.BattleRunMode RunMode;
        public readonly bool EnableInputRecording;
        public readonly string InputRecordOutputPath;
        public readonly bool EnableInputReplay;
        public readonly string InputReplayPath;

        public BattleStartPlanRunModeOptions(
            BattleStartConfig.BattleRunMode runMode,
            bool enableInputRecording,
            string inputRecordOutputPath,
            bool enableInputReplay,
            string inputReplayPath)
        {
            RunMode = runMode;
            EnableInputRecording = enableInputRecording;
            InputRecordOutputPath = inputRecordOutputPath;
            EnableInputReplay = enableInputReplay;
            InputReplayPath = inputReplayPath;
        }
    }

    public readonly struct BattleStartPlanCreateWorldOptions
    {
        public readonly int OpCode;
        public readonly byte[] Payload;

        public BattleStartPlanCreateWorldOptions(int opCode, byte[] payload)
        {
            OpCode = opCode;
            Payload = payload;
        }
    }

    public readonly struct BattleStartPlanTimeSyncOptions
    {
        public readonly uint OpCode;
        public readonly int IntervalMs;
        public readonly double Alpha;
        public readonly int TimeoutMs;

        public readonly int IdealFrameSafetyConstMarginFrames;
        public readonly double IdealFrameSafetyRttFactor;
        public readonly int IdealFrameSafetyMinMarginFrames;
        public readonly int IdealFrameSafetyMaxMarginFrames;

        public BattleStartPlanTimeSyncOptions(
            uint opCode,
            int intervalMs,
            double alpha,
            int timeoutMs,
            int idealFrameSafetyConstMarginFrames,
            double idealFrameSafetyRttFactor,
            int idealFrameSafetyMinMarginFrames,
            int idealFrameSafetyMaxMarginFrames)
        {
            OpCode = opCode;
            IntervalMs = intervalMs;
            Alpha = alpha;
            TimeoutMs = timeoutMs;
            IdealFrameSafetyConstMarginFrames = idealFrameSafetyConstMarginFrames;
            IdealFrameSafetyRttFactor = idealFrameSafetyRttFactor;
            IdealFrameSafetyMinMarginFrames = idealFrameSafetyMinMarginFrames;
            IdealFrameSafetyMaxMarginFrames = idealFrameSafetyMaxMarginFrames;
        }
    }

    public readonly struct BattleStartPlanOptions
    {
        public readonly BattleStartPlanWorldOptions World;
        public readonly BattleStartPlanSyncOptions Sync;
        public readonly BattleStartPlanAuthorityOptions Authority;

        public readonly string WorldId;
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly string PlayerId;

        public readonly int TickRate;
        public readonly int InputDelayFrames;

        public readonly BattleStartConfig.BattleHostMode HostMode;
        public readonly BattleSyncMode SyncMode;
        public readonly BattleViewEventSourceMode ViewEventSourceMode;

        public readonly bool EnableClientPrediction;
        public readonly bool EnableConfirmedAuthorityWorld;

        public readonly string[] EnabledSnapshotRegistryIds;

        public readonly BattleStartPlanGatewayOptions Gateway;
        public readonly BattleStartPlanAutoOptions Auto;
        public readonly BattleStartPlanRunModeOptions RunMode;
        public readonly BattleStartPlanCreateWorldOptions CreateWorld;
        public readonly BattleStartPlanTimeSyncOptions TimeSync;
        public readonly MobaBattleLaunchSpec LaunchSpec;

        public BattleStartPlanOptions(
            in BattleStartPlanWorldOptions world,
            BattleStartConfig.BattleHostMode hostMode,
            in BattleStartPlanSyncOptions sync,
            in BattleStartPlanAuthorityOptions authority,
            in BattleStartPlanGatewayOptions gateway,
            in BattleStartPlanAutoOptions auto,
            in BattleStartPlanRunModeOptions runMode,
            in BattleStartPlanCreateWorldOptions createWorld,
            in BattleStartPlanTimeSyncOptions timeSync,
            MobaBattleLaunchSpec launchSpec = default)
        {
            World = world;
            Sync = sync;
            Authority = authority;
            WorldId = world.WorldId;
            WorldType = world.WorldType;
            ClientId = world.ClientId;
            PlayerId = world.PlayerId;
            TickRate = world.TickRate;
            InputDelayFrames = world.InputDelayFrames;
            HostMode = hostMode;
            SyncMode = sync.SyncMode;
            ViewEventSourceMode = sync.ViewEventSourceMode;
            EnableClientPrediction = authority.EnableClientPrediction;
            EnableConfirmedAuthorityWorld = authority.EnableConfirmedAuthorityWorld;
            EnabledSnapshotRegistryIds = sync.EnabledSnapshotRegistryIds;
            Gateway = gateway;
            Auto = auto;
            RunMode = runMode;
            CreateWorld = createWorld;
            TimeSync = timeSync;
            LaunchSpec = launchSpec;
        }

        public BattleStartPlanOptions(
            string worldId,
            string worldType,
            string clientId,
            string playerId,
            int tickRate,
            int inputDelayFrames,
            BattleStartConfig.BattleHostMode hostMode,
            BattleSyncMode syncMode,
            BattleViewEventSourceMode viewEventSourceMode,
            bool enableClientPrediction,
            bool enableConfirmedAuthorityWorld,
            string[] enabledSnapshotRegistryIds,
            BattleStartPlanGatewayOptions gateway,
            BattleStartPlanAutoOptions auto,
            BattleStartPlanRunModeOptions runMode,
            BattleStartPlanCreateWorldOptions createWorld,
            BattleStartPlanTimeSyncOptions timeSync,
            MobaBattleLaunchSpec launchSpec = default)
            : this(
                new BattleStartPlanWorldOptions(
                    worldId,
                    worldType,
                    clientId,
                    playerId,
                    tickRate,
                    inputDelayFrames),
                hostMode,
                new BattleStartPlanSyncOptions(syncMode, viewEventSourceMode, enabledSnapshotRegistryIds),
                new BattleStartPlanAuthorityOptions(enableClientPrediction, enableConfirmedAuthorityWorld),
                gateway,
                auto,
                runMode,
                createWorld,
                timeSync,
                launchSpec)
        {
        }
    }
}
