namespace AbilityKit.Game.Flow
{
    public enum BattleViewEventSourceMode
    {
        SnapshotOnly = 0,
        TriggerOnly = 1,
        Hybrid = 2,
    }

    public enum BattleSyncMode
    {
        Lockstep = 0,
        SnapshotAuthority = 1,
        HybridPredictReconcile = 2,
    }

    public interface IBattleBootstrapper
    {
        BattleStartPlan Build();
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
            uint joinRoomOpCode)
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
            BattleStartPlanTimeSyncOptions timeSync)
        {
            WorldId = worldId;
            WorldType = worldType;
            ClientId = clientId;
            PlayerId = playerId;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            HostMode = hostMode;
            SyncMode = syncMode;
            ViewEventSourceMode = viewEventSourceMode;
            EnableClientPrediction = enableClientPrediction;
            EnableConfirmedAuthorityWorld = enableConfirmedAuthorityWorld;
            EnabledSnapshotRegistryIds = enabledSnapshotRegistryIds;
            Gateway = gateway;
            Auto = auto;
            RunMode = runMode;
            CreateWorld = createWorld;
            TimeSync = timeSync;
        }
    }

    public readonly struct BattleStartPlan
    {
        public readonly string WorldId;
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly string PlayerId;

        public readonly int TickRate;

        public readonly int InputDelayFrames;

        public readonly BattleStartConfig.BattleHostMode HostMode;

        public readonly bool UseGatewayTransport;
        public readonly string GatewayHost;
        public readonly int GatewayPort;
        public readonly ulong NumericRoomId;

        public readonly string GatewaySessionToken;
        public readonly string GatewayRegion;
        public readonly string GatewayServerId;
        public readonly bool GatewayAutoCreateRoom;
        public readonly bool GatewayAutoJoinRoom;
        public readonly string GatewayJoinRoomId;
        public readonly uint GatewayCreateRoomOpCode;
        public readonly uint GatewayJoinRoomOpCode;

        public readonly bool AutoConnect;
        public readonly bool AutoCreateWorld;
        public readonly bool AutoJoin;
        public readonly bool AutoReady;

        public readonly BattleSyncMode SyncMode;

        public readonly BattleViewEventSourceMode ViewEventSourceMode;

        public readonly string[] EnabledSnapshotRegistryIds;

        public readonly bool EnableClientPrediction;

        public readonly bool EnableConfirmedAuthorityWorld;

        public readonly bool EnableInputRecording;
        public readonly string InputRecordOutputPath;

        public readonly bool EnableInputReplay;
        public readonly string InputReplayPath;

        public readonly BattleStartConfig.BattleRunMode RunMode;

        public readonly int CreateWorldOpCode;
        public readonly byte[] CreateWorldPayload;

        public readonly uint TimeSyncOpCode;
        public readonly int TimeSyncIntervalMs;
        public readonly double TimeSyncAlpha;
        public readonly int TimeSyncTimeoutMs;

        public readonly int IdealFrameSafetyConstMarginFrames;
        public readonly double IdealFrameSafetyRttFactor;
        public readonly int IdealFrameSafetyMinMarginFrames;
        public readonly int IdealFrameSafetyMaxMarginFrames;

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
                enabledSnapshotRegistryIds: options.EnabledSnapshotRegistryIds)
        {
        }

        public BattleStartPlan WithGatewaySessionToken(string gatewaySessionToken)
        {
            return new BattleStartPlan(
                worldId: WorldId,
                worldType: WorldType,
                clientId: ClientId,
                playerId: PlayerId,
                tickRate: TickRate,
                inputDelayFrames: InputDelayFrames,
                hostMode: HostMode,
                useGatewayTransport: UseGatewayTransport,
                gatewayHost: GatewayHost,
                gatewayPort: GatewayPort,
                numericRoomId: NumericRoomId,
                gatewaySessionToken: gatewaySessionToken,
                gatewayRegion: GatewayRegion,
                gatewayServerId: GatewayServerId,
                gatewayAutoCreateRoom: GatewayAutoCreateRoom,
                gatewayAutoJoinRoom: GatewayAutoJoinRoom,
                gatewayJoinRoomId: GatewayJoinRoomId,
                gatewayCreateRoomOpCode: GatewayCreateRoomOpCode,
                gatewayJoinRoomOpCode: GatewayJoinRoomOpCode,
                autoConnect: AutoConnect,
                autoCreateWorld: AutoCreateWorld,
                autoJoin: AutoJoin,
                autoReady: AutoReady,
                syncMode: SyncMode,
                viewEventSourceMode: ViewEventSourceMode,
                enableClientPrediction: EnableClientPrediction,
                enableConfirmedAuthorityWorld: EnableConfirmedAuthorityWorld,
                enableInputRecording: EnableInputRecording,
                inputRecordOutputPath: InputRecordOutputPath,
                enableInputReplay: EnableInputReplay,
                inputReplayPath: InputReplayPath,
                runMode: RunMode,
                createWorldOpCode: CreateWorldOpCode,
                createWorldPayload: CreateWorldPayload,
                timeSyncOpCode: TimeSyncOpCode,
                timeSyncIntervalMs: TimeSyncIntervalMs,
                timeSyncAlpha: TimeSyncAlpha,
                timeSyncTimeoutMs: TimeSyncTimeoutMs,
                idealFrameSafetyConstMarginFrames: IdealFrameSafetyConstMarginFrames,
                idealFrameSafetyRttFactor: IdealFrameSafetyRttFactor,
                idealFrameSafetyMinMarginFrames: IdealFrameSafetyMinMarginFrames,
                idealFrameSafetyMaxMarginFrames: IdealFrameSafetyMaxMarginFrames,
                enabledSnapshotRegistryIds: EnabledSnapshotRegistryIds);
        }

        public BattleStartPlan WithGatewayRoom(string worldId, ulong numericRoomId)
        {
            return new BattleStartPlan(
                worldId: worldId,
                worldType: WorldType,
                clientId: ClientId,
                playerId: PlayerId,
                tickRate: TickRate,
                inputDelayFrames: InputDelayFrames,
                hostMode: HostMode,
                useGatewayTransport: UseGatewayTransport,
                gatewayHost: GatewayHost,
                gatewayPort: GatewayPort,
                numericRoomId: numericRoomId,
                gatewaySessionToken: GatewaySessionToken,
                gatewayRegion: GatewayRegion,
                gatewayServerId: GatewayServerId,
                gatewayAutoCreateRoom: GatewayAutoCreateRoom,
                gatewayAutoJoinRoom: GatewayAutoJoinRoom,
                gatewayJoinRoomId: GatewayJoinRoomId,
                gatewayCreateRoomOpCode: GatewayCreateRoomOpCode,
                gatewayJoinRoomOpCode: GatewayJoinRoomOpCode,
                autoConnect: AutoConnect,
                autoCreateWorld: AutoCreateWorld,
                autoJoin: AutoJoin,
                autoReady: AutoReady,
                syncMode: SyncMode,
                viewEventSourceMode: ViewEventSourceMode,
                enableClientPrediction: EnableClientPrediction,
                enableConfirmedAuthorityWorld: EnableConfirmedAuthorityWorld,
                enableInputRecording: EnableInputRecording,
                inputRecordOutputPath: InputRecordOutputPath,
                enableInputReplay: EnableInputReplay,
                inputReplayPath: InputReplayPath,
                runMode: RunMode,
                createWorldOpCode: CreateWorldOpCode,
                createWorldPayload: CreateWorldPayload,
                timeSyncOpCode: TimeSyncOpCode,
                timeSyncIntervalMs: TimeSyncIntervalMs,
                timeSyncAlpha: TimeSyncAlpha,
                timeSyncTimeoutMs: TimeSyncTimeoutMs,
                idealFrameSafetyConstMarginFrames: IdealFrameSafetyConstMarginFrames,
                idealFrameSafetyRttFactor: IdealFrameSafetyRttFactor,
                idealFrameSafetyMinMarginFrames: IdealFrameSafetyMinMarginFrames,
                idealFrameSafetyMaxMarginFrames: IdealFrameSafetyMaxMarginFrames,
                enabledSnapshotRegistryIds: EnabledSnapshotRegistryIds);
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
            string[] enabledSnapshotRegistryIds = null)
        {
            WorldId = worldId;
            WorldType = worldType;
            ClientId = clientId;
            PlayerId = playerId;

            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;

            HostMode = hostMode;

            UseGatewayTransport = useGatewayTransport;
            GatewayHost = gatewayHost;
            GatewayPort = gatewayPort;
            NumericRoomId = numericRoomId;

            GatewaySessionToken = gatewaySessionToken;
            GatewayRegion = gatewayRegion;
            GatewayServerId = gatewayServerId;
            GatewayAutoCreateRoom = gatewayAutoCreateRoom;
            GatewayAutoJoinRoom = gatewayAutoJoinRoom;
            GatewayJoinRoomId = gatewayJoinRoomId;
            GatewayCreateRoomOpCode = gatewayCreateRoomOpCode;
            GatewayJoinRoomOpCode = gatewayJoinRoomOpCode;
            AutoConnect = autoConnect;
            AutoCreateWorld = autoCreateWorld;
            AutoJoin = autoJoin;
            AutoReady = autoReady;

            SyncMode = syncMode;
            ViewEventSourceMode = viewEventSourceMode;

            EnabledSnapshotRegistryIds = enabledSnapshotRegistryIds;

            EnableClientPrediction = enableClientPrediction;

            EnableConfirmedAuthorityWorld = enableConfirmedAuthorityWorld;

            EnableInputRecording = enableInputRecording;
            InputRecordOutputPath = inputRecordOutputPath;

            EnableInputReplay = enableInputReplay;
            InputReplayPath = inputReplayPath;

            RunMode = runMode;
            CreateWorldOpCode = createWorldOpCode;
            CreateWorldPayload = createWorldPayload;

            TimeSyncOpCode = timeSyncOpCode;
            TimeSyncIntervalMs = timeSyncIntervalMs;
            TimeSyncAlpha = timeSyncAlpha;
            TimeSyncTimeoutMs = timeSyncTimeoutMs;

            IdealFrameSafetyConstMarginFrames = idealFrameSafetyConstMarginFrames;
            IdealFrameSafetyRttFactor = idealFrameSafetyRttFactor;
            IdealFrameSafetyMinMarginFrames = idealFrameSafetyMinMarginFrames;
            IdealFrameSafetyMaxMarginFrames = idealFrameSafetyMaxMarginFrames;
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
