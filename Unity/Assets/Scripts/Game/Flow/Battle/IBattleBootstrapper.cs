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

        public readonly bool AutoConnect;
        public readonly bool AutoCreateWorld;
        public readonly bool AutoJoin;
        public readonly bool AutoReady;

        public readonly BattleSyncMode SyncMode;

        public readonly BattleViewEventSourceMode ViewEventSourceMode;

        public readonly bool EnableInputRecording;
        public readonly string InputRecordOutputPath;

        public readonly bool EnableInputReplay;
        public readonly string InputReplayPath;

        public readonly int CreateWorldOpCode;
        public readonly byte[] CreateWorldPayload;

        public BattleStartPlan(
            string worldId,
            string worldType,
            string clientId,
            string playerId,
            bool autoConnect,
            bool autoCreateWorld,
            bool autoJoin,
            bool autoReady,
            BattleSyncMode syncMode,
            BattleViewEventSourceMode viewEventSourceMode,
            bool enableInputRecording,
            string inputRecordOutputPath,
            bool enableInputReplay,
            string inputReplayPath,
            int createWorldOpCode,
            byte[] createWorldPayload)
        {
            WorldId = worldId;
            WorldType = worldType;
            ClientId = clientId;
            PlayerId = playerId;
            AutoConnect = autoConnect;
            AutoCreateWorld = autoCreateWorld;
            AutoJoin = autoJoin;
            AutoReady = autoReady;

            SyncMode = syncMode;
            ViewEventSourceMode = viewEventSourceMode;

            EnableInputRecording = enableInputRecording;
            InputRecordOutputPath = inputRecordOutputPath;

            EnableInputReplay = enableInputReplay;
            InputReplayPath = inputReplayPath;
            CreateWorldOpCode = createWorldOpCode;
            CreateWorldPayload = createWorldPayload;
        }
    }
}
