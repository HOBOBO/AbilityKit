namespace AbilityKit.Protocol.Room
{
    /// <summary>
    /// Room gateway opcodes used by Orleans Gateway room entrypoints.
    /// </summary>
    public static class RoomGatewayOpCodes
    {
        public const uint GuestLogin = 100;
        public const uint AccountLogin = 111;
        public const uint CreateRoom = 101;
        public const uint JoinRoom = 102;
        public const uint SubscribeStateSync = 103;
        public const uint SetReady = 104;
        public const uint PickHero = 105;
        public const uint StartBattle = 106;
        public const uint SubmitBattleInput = 107;
        public const uint RequestFullStateSync = 108;
        public const uint RestoreRoom = 109;
        public const uint ListRooms = 110;
        public const uint RenewSession = 120;

        // 阶段 4：资源加载屏障 / 状态查询入口（append-only，112-115）
        public const uint BeginLoading = 112;
        public const uint ReportAssetsLoaded = 113;
        public const uint CancelLoading = 114;
        public const uint GetSnapshot = 115;
        public const uint AckReliableBattleEvents = 116;
        public const uint GetStateSyncDeliveryMetrics = 117;
        public const uint ReportLoadingProgress = 118;
        public const uint LeaveRoom = 119;

        public const uint SessionKicked = 9000;
        public const uint SnapshotPushed = 9002;
        public const uint DeltaSnapshotPushed = 9003;
        // 阶段 4：Room 状态变更推送（push）
        public const uint RoomStateChanged = 9004;
        public const uint ReliableBattleEventsPushed = 9005;
    }
}
