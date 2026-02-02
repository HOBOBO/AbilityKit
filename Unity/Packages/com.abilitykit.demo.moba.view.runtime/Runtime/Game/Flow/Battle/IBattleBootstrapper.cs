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
            int idealFrameSafetyMaxMarginFrames = 30)
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
            int idealFrameSafetyMaxMarginFrames = 30)
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
                idealFrameSafetyMaxMarginFrames)
        {
        }
    }
}
