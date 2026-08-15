using System.Collections.Generic;
using AbilityKit.Protocol;
using MemoryPack;

namespace AbilityKit.Protocol.Room
{
    [ProtocolOpCode(RoomGatewayOpCodes.GuestLogin, ProtocolDirection.ClientToServer, nameof(WireRoomGuestLoginReq))]
    [MemoryPackable]
    public partial struct WireRoomGuestLoginReq
    {
        [MemoryPackOrder(0)] public string GuestId { get; set; }
    }

    [MemoryPackable]
    public partial struct WireRoomGuestLoginRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public string SessionToken { get; set; }
        [MemoryPackOrder(2)] public string AccountId { get; set; }
        [MemoryPackOrder(3)] public string Message { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.AccountLogin, ProtocolDirection.ClientToServer, nameof(WireRoomAccountLoginReq))]
    [MemoryPackable]
    public partial struct WireRoomAccountLoginReq
    {
        [MemoryPackOrder(0)] public string AccountId { get; set; }
        [MemoryPackOrder(1)] public int ExpireSeconds { get; set; }
        [MemoryPackOrder(2)] public bool KickExisting { get; set; }
    }

    [MemoryPackable]
    public partial struct WireRoomAccountLoginRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public string SessionToken { get; set; }
        [MemoryPackOrder(2)] public string AccountId { get; set; }
        [MemoryPackOrder(3)] public long ExpireAtUnixMs { get; set; }
        [MemoryPackOrder(4)] public string KickedSessionToken { get; set; }
        [MemoryPackOrder(5)] public string Message { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.RenewSession, ProtocolDirection.ClientToServer, nameof(WireRenewSessionReq))]
    [MemoryPackable]
    public partial struct WireRenewSessionReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public int ExtendSeconds { get; set; }
        [MemoryPackOrder(2)] public bool RotateToken { get; set; }
    }

    [MemoryPackable]
    public partial struct WireRenewSessionRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public string SessionToken { get; set; }
        [MemoryPackOrder(2)] public string AccountId { get; set; }
        [MemoryPackOrder(3)] public long ExpireAtUnixMs { get; set; }
        [MemoryPackOrder(4)] public string Message { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.CreateRoom, ProtocolDirection.ClientToServer, nameof(WireCreateRoomReq))]
    [MemoryPackable]
    public partial struct WireCreateRoomReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string Region { get; set; }
        [MemoryPackOrder(2)] public string ServerId { get; set; }
        [MemoryPackOrder(3)] public string RoomType { get; set; }
        [MemoryPackOrder(4)] public string Title { get; set; }
        [MemoryPackOrder(5)] public bool IsPublic { get; set; }
        [MemoryPackOrder(6)] public int MaxPlayers { get; set; }
        [MemoryPackOrder(7)] public Dictionary<string, string>? Tags { get; set; }
    }

    [MemoryPackable]
    public partial struct WireCreateRoomRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
        [MemoryPackOrder(2)] public ulong NumericRoomId { get; set; }
        [MemoryPackOrder(3)] public string Message { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.JoinRoom, ProtocolDirection.ClientToServer, nameof(WireJoinRoomReq))]
    [MemoryPackable]
    public partial struct WireJoinRoomReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string Region { get; set; }
        [MemoryPackOrder(2)] public string ServerId { get; set; }
        [MemoryPackOrder(3)] public string RoomId { get; set; }
    }

    public enum WireRoomJoinKind
    {
        TeamLobby = 0,
        Reconnect = 1,
        LateJoin = 2
    }

    public enum WireRoomRestoreStatus
    {
        Restored = 0,
        NoActiveRoom = 1,
        NotMember = 2,
        RoomClosed = 3,
        RoomExpired = 4,
        InvalidSession = 5,
        Failed = 100
    }

    public enum WireRoomRestoreErrorCode
    {
        None = 0,
        NoAccountRoomMapping = 1,
        AccountNotInRoom = 2,
        RoomClosed = 3,
        RoomExpired = 4,
        InvalidSession = 5,
        InternalError = 100
    }

    [MemoryPackable]
    public partial struct WireJoinRoomRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
        [MemoryPackOrder(2)] public ulong NumericRoomId { get; set; }
        [MemoryPackOrder(3)] public WireRoomSnapshot Snapshot { get; set; }
        [MemoryPackOrder(4)] public WireWorldStartAnchor WorldStartAnchor { get; set; }
        [MemoryPackOrder(5)] public string Message { get; set; }
        [MemoryPackOrder(6)] public WireRoomJoinKind JoinKind { get; set; }
        [MemoryPackOrder(7)] public long ServerNowTicks { get; set; }
        [MemoryPackOrder(8)] public uint CurrentPlayerId { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.RestoreRoom, ProtocolDirection.ClientToServer, nameof(WireRestoreRoomReq))]
    [MemoryPackable]
    public partial struct WireRestoreRoomReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string Region { get; set; }
        [MemoryPackOrder(2)] public string ServerId { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.ListRooms, ProtocolDirection.ClientToServer, nameof(WireListRoomsReq))]
    [MemoryPackable]
    public partial struct WireListRoomsReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string Region { get; set; }
        [MemoryPackOrder(2)] public string ServerId { get; set; }
        [MemoryPackOrder(3)] public int Offset { get; set; }
        [MemoryPackOrder(4)] public int Limit { get; set; }
        [MemoryPackOrder(5)] public string RoomType { get; set; }
    }

    [MemoryPackable]
    public partial struct WireListRoomsRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public List<WireRoomSummary>? Rooms { get; set; }
        [MemoryPackOrder(2)] public int NextOffset { get; set; }
        [MemoryPackOrder(3)] public string Message { get; set; }
    }

    [MemoryPackable]
    public partial struct WireRestoreRoomRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public bool HasActiveRoom { get; set; }
        [MemoryPackOrder(2)] public bool IsInBattle { get; set; }
        [MemoryPackOrder(3)] public string RoomId { get; set; }
        [MemoryPackOrder(4)] public ulong NumericRoomId { get; set; }
        [MemoryPackOrder(5)] public WireRoomSnapshot Snapshot { get; set; }
        [MemoryPackOrder(6)] public WireWorldStartAnchor WorldStartAnchor { get; set; }
        [MemoryPackOrder(7)] public string Message { get; set; }
        [MemoryPackOrder(8)] public WireRoomJoinKind JoinKind { get; set; }
        [MemoryPackOrder(9)] public long ServerNowTicks { get; set; }
        [MemoryPackOrder(10)] public WireRoomRestoreStatus Status { get; set; }
        [MemoryPackOrder(11)] public WireRoomRestoreErrorCode ErrorCode { get; set; }
        [MemoryPackOrder(12)] public uint CurrentPlayerId { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.SetReady, ProtocolDirection.ClientToServer, nameof(WireRoomReadyReq))]
    [MemoryPackable]
    public partial struct WireRoomReadyReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
        [MemoryPackOrder(2)] public bool Ready { get; set; }
    }

    [MemoryPackable]
    public partial struct WireRoomSnapshotRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
        [MemoryPackOrder(2)] public ulong NumericRoomId { get; set; }
        [MemoryPackOrder(3)] public WireRoomSnapshot Snapshot { get; set; }
        [MemoryPackOrder(4)] public string Message { get; set; }
        [MemoryPackOrder(5)] public long ServerNowTicks { get; set; }
        [MemoryPackOrder(6)] public bool Applied { get; set; }
        [MemoryPackOrder(7)] public int ErrorCode { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.PickHero, ProtocolDirection.ClientToServer, nameof(WireRoomPickHeroReq))]
    [MemoryPackable]
    public partial struct WireRoomPickHeroReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
        [MemoryPackOrder(2)] public int HeroId { get; set; }
        [MemoryPackOrder(3)] public int TeamId { get; set; }
        [MemoryPackOrder(4)] public int SpawnPointId { get; set; }
        [MemoryPackOrder(5)] public int Level { get; set; }
        [MemoryPackOrder(6)] public int AttributeTemplateId { get; set; }
        [MemoryPackOrder(7)] public int BasicAttackSkillId { get; set; }
        [MemoryPackOrder(8)] public List<int>? SkillIds { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.StartBattle, ProtocolDirection.ClientToServer, nameof(WireStartRoomBattleReq))]
    [MemoryPackable]
    public partial struct WireStartRoomBattleReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
        [MemoryPackOrder(2)] public int GameplayId { get; set; }
        [MemoryPackOrder(3)] public int RuleSetId { get; set; }
        [MemoryPackOrder(4)] public int ConfigVersion { get; set; }
        [MemoryPackOrder(5)] public int ProtocolVersion { get; set; }
        [MemoryPackOrder(6)] public string WorldType { get; set; }
        [MemoryPackOrder(7)] public string ClientId { get; set; }
        [MemoryPackOrder(8)] public string SyncTemplateId { get; set; }
        [MemoryPackOrder(9)] public int SyncModel { get; set; }
        [MemoryPackOrder(10)] public string NetworkEnvironmentId { get; set; }
        [MemoryPackOrder(11)] public string CarrierName { get; set; }
        [MemoryPackOrder(12)] public bool EnableAuthoritativeWorld { get; set; }
        [MemoryPackOrder(13)] public bool InterpolationEnabled { get; set; }
        [MemoryPackOrder(14)] public int InputDelayFrames { get; set; }
    }

    [MemoryPackable]
    public partial struct WireNetworkSyncCapabilities
    {
        [MemoryPackOrder(0)] public int MetadataVersion { get; set; }
        [MemoryPackOrder(1)] public string ProfileName { get; set; }
        [MemoryPackOrder(2)] public int MinimumSchemaVersion { get; set; }
        [MemoryPackOrder(3)] public int MaximumSchemaVersion { get; set; }
        [MemoryPackOrder(4)] public int ClientPlayback { get; set; }
        [MemoryPackOrder(5)] public int Input { get; set; }
        [MemoryPackOrder(6)] public int Snapshot { get; set; }
        [MemoryPackOrder(7)] public int Interest { get; set; }
        [MemoryPackOrder(8)] public int Recovery { get; set; }
        [MemoryPackOrder(9)] public int ServerValidation { get; set; }
        // 仅追加字段：旧端缺少该字段时按不支持可靠事件扩展能力处理。
        [MemoryPackOrder(10)] public int ReliableEvent { get; set; }
    }

    [MemoryPackable]
    public partial struct WireStartRoomBattleRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public string BattleId { get; set; }
        [MemoryPackOrder(2)] public ulong WorldId { get; set; }
        [MemoryPackOrder(3)] public bool Started { get; set; }
        [MemoryPackOrder(4)] public string Message { get; set; }
        [MemoryPackOrder(5)] public WireWorldStartAnchor WorldStartAnchor { get; set; }
        [MemoryPackOrder(6)] public long ServerNowTicks { get; set; }
        // 仅追加字段：旧服务端响应缺少该字段时按未声明能力处理。
        [MemoryPackOrder(7)] public WireNetworkSyncCapabilities? SyncCapabilities { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.SubmitBattleInput, ProtocolDirection.ClientToServer, nameof(WireSubmitBattleInputReq))]
    [MemoryPackable]
    public partial struct WireSubmitBattleInputReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string BattleId { get; set; }
        [MemoryPackOrder(2)] public ulong WorldId { get; set; }
        [MemoryPackOrder(3)] public int Frame { get; set; }
        [MemoryPackOrder(4)] public uint PlayerId { get; set; }
        [MemoryPackOrder(5)] public int InputOpCode { get; set; }
        [MemoryPackOrder(6)] public byte[]? Payload { get; set; }
        [MemoryPackOrder(7)] public ulong CommandSequence { get; set; }
    }

    [MemoryPackable]
    public partial struct WireSubmitBattleInputRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public int AcceptedFrame { get; set; }
        [MemoryPackOrder(2)] public string Message { get; set; }
        [MemoryPackOrder(3)] public int CurrentFrame { get; set; }
        [MemoryPackOrder(4)] public string Status { get; set; }
        [MemoryPackOrder(5)] public bool ShouldResync { get; set; }
        [MemoryPackOrder(6)] public long ServerTicks { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.SubscribeStateSync, ProtocolDirection.ClientToServer, nameof(WireSubscribeStateSyncReq))]
    [MemoryPackable]
    public partial struct WireSubscribeStateSyncReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string BattleId { get; set; }
        [MemoryPackOrder(2)] public string RoomId { get; set; }
        [MemoryPackOrder(3)] public string EventEpoch { get; set; }
        [MemoryPackOrder(4)] public long LastEventAck { get; set; }
    }

    [MemoryPackable]
    public partial struct WireSubscribeStateSyncRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public string Message { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.RequestFullStateSync, ProtocolDirection.ClientToServer, nameof(WireRequestFullStateSyncReq))]
    [MemoryPackable]
    public partial struct WireRequestFullStateSyncReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string BattleId { get; set; }
        [MemoryPackOrder(2)] public string RoomId { get; set; }
        [MemoryPackOrder(3)] public ulong WorldId { get; set; }
        [MemoryPackOrder(4)] public int ClientFrame { get; set; }
        [MemoryPackOrder(5)] public int LastAuthoritativeFrame { get; set; }
        [MemoryPackOrder(6)] public uint ClientStateHash { get; set; }
        [MemoryPackOrder(7)] public uint AuthoritativeStateHash { get; set; }
        [MemoryPackOrder(8)] public string Reason { get; set; }
    }

    [MemoryPackable]
    public partial struct WireRequestFullStateSyncRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public bool Accepted { get; set; }
        [MemoryPackOrder(2)] public string Message { get; set; }
        [MemoryPackOrder(3)] public long ServerTicks { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.GetStateSyncDeliveryMetrics, ProtocolDirection.ClientToServer, nameof(WireGetStateSyncDeliveryMetricsReq))]
    [MemoryPackable]
    public partial struct WireGetStateSyncDeliveryMetricsReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string BattleId { get; set; }
        [MemoryPackOrder(2)] public string RoomId { get; set; }
    }

    [MemoryPackable]
    public partial struct WireGetStateSyncDeliveryMetricsRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public long ProducedBytes { get; set; }
        [MemoryPackOrder(2)] public long SentBytes { get; set; }
        [MemoryPackOrder(3)] public long DroppedBytes { get; set; }
        [MemoryPackOrder(4)] public long MergedBytes { get; set; }
        [MemoryPackOrder(5)] public int QueueLength { get; set; }
        [MemoryPackOrder(6)] public long QueueAgeTicks { get; set; }
        [MemoryPackOrder(7)] public long BaselineAgeTicks { get; set; }
        [MemoryPackOrder(8)] public long ResyncCount { get; set; }
        [MemoryPackOrder(9)] public string Message { get; set; }
    }

    [MemoryPackable]
    public partial struct WireStateSyncSnapshotPush
    {
        [MemoryPackOrder(0)] public ulong WorldId { get; set; }
        [MemoryPackOrder(1)] public int Frame { get; set; }
        [MemoryPackOrder(2)] public double Timestamp { get; set; }
        [MemoryPackOrder(3)] public bool IsFullSnapshot { get; set; }
        [MemoryPackOrder(4)] public List<WireStateSyncActorSnapshot>? Actors { get; set; }
        [MemoryPackOrder(5)] public int PayloadOpCode { get; set; }
        [MemoryPackOrder(6)] public byte[]? Payload { get; set; }
        [MemoryPackOrder(7)] public long ServerTicks { get; set; }
        [MemoryPackOrder(8)] public long EventWatermark { get; set; }
        [MemoryPackOrder(9)] public int SchemaVersion { get; set; }
        [MemoryPackOrder(10)] public List<int>? RemovedActorIds { get; set; }
        [MemoryPackOrder(11)] public string EventEpoch { get; set; }
    }

    [MemoryPackable]
    public partial struct WireStateSyncActorSnapshot
    {
        [MemoryPackOrder(0)] public int ActorId { get; set; }
        [MemoryPackOrder(1)] public float X { get; set; }
        [MemoryPackOrder(2)] public float Y { get; set; }
        [MemoryPackOrder(3)] public float Z { get; set; }
        [MemoryPackOrder(4)] public float Rotation { get; set; }
        [MemoryPackOrder(5)] public float VelocityX { get; set; }
        [MemoryPackOrder(6)] public float VelocityZ { get; set; }
        [MemoryPackOrder(7)] public float Hp { get; set; }
        [MemoryPackOrder(8)] public float HpMax { get; set; }
        [MemoryPackOrder(9)] public int TeamId { get; set; }
        [MemoryPackOrder(10)] public int Kind { get; set; }
        [MemoryPackOrder(11)] public int Code { get; set; }
        [MemoryPackOrder(12)] public int OwnerNetId { get; set; }
    }

    [MemoryPackable]
    public partial struct WireRoomSummary
    {
        [MemoryPackOrder(0)] public string Region { get; set; }
        [MemoryPackOrder(1)] public string ServerId { get; set; }
        [MemoryPackOrder(2)] public string RoomId { get; set; }
        [MemoryPackOrder(3)] public string RoomType { get; set; }
        [MemoryPackOrder(4)] public string Title { get; set; }
        [MemoryPackOrder(5)] public bool IsPublic { get; set; }
        [MemoryPackOrder(6)] public int MaxPlayers { get; set; }
        [MemoryPackOrder(7)] public int PlayerCount { get; set; }
        [MemoryPackOrder(8)] public string OwnerAccountId { get; set; }
        [MemoryPackOrder(9)] public long CreatedAtUnixMs { get; set; }
        [MemoryPackOrder(10)] public Dictionary<string, string>? Tags { get; set; }
    }

    [MemoryPackable]
    public partial struct WireRoomPlayerSnapshot
    {
        [MemoryPackOrder(0)] public string AccountId { get; set; }
        [MemoryPackOrder(1)] public int TeamId { get; set; }
        [MemoryPackOrder(2)] public bool Ready { get; set; }
        [MemoryPackOrder(3)] public int HeroId { get; set; }
        [MemoryPackOrder(4)] public int SpawnPointId { get; set; }
        [MemoryPackOrder(5)] public int Level { get; set; }
        [MemoryPackOrder(6)] public int AttributeTemplateId { get; set; }
        [MemoryPackOrder(7)] public int BasicAttackSkillId { get; set; }
        [MemoryPackOrder(8)] public List<int>? SkillIds { get; set; }
        [MemoryPackOrder(9)] public uint PlayerId { get; set; }
        // 阶段 4 append-only（10-15）
        [MemoryPackOrder(10)] public bool LobbyReady { get; set; }
        [MemoryPackOrder(11)] public bool AssetsLoaded { get; set; }
        [MemoryPackOrder(12)] public bool IsOnline { get; set; }
        [MemoryPackOrder(13)] public long JoinOrdinal { get; set; }
        [MemoryPackOrder(14)] public int LoadedManifestVersion { get; set; }
        [MemoryPackOrder(15)] public string LoadedManifestHash { get; set; }
        [MemoryPackOrder(16)] public long LastSeenTicks { get; set; }
        [MemoryPackOrder(17)] public long OfflineSinceTicks { get; set; }
        [MemoryPackOrder(18)] public int LoadingProgress { get; set; }
    }

    [MemoryPackable]
    public partial struct WireRoomSnapshot
    {
        [MemoryPackOrder(0)] public WireRoomSummary Summary { get; set; }
        [MemoryPackOrder(1)] public List<string>? Members { get; set; }
        [MemoryPackOrder(2)] public List<WireRoomPlayerSnapshot>? Players { get; set; }
        [MemoryPackOrder(3)] public bool CanStart { get; set; }
        [MemoryPackOrder(4)] public string BattleId { get; set; }
        [MemoryPackOrder(5)] public ulong WorldId { get; set; }
        // 阶段 4 append-only（6-17）
        [MemoryPackOrder(6)] public WireWorldStartAnchor WorldStartAnchor { get; set; }
        [MemoryPackOrder(7)] public int SchemaVersion { get; set; }
        [MemoryPackOrder(8)] public long RoomRevision { get; set; }
        [MemoryPackOrder(9)] public long LastEventSequence { get; set; }
        [MemoryPackOrder(10)] public int Phase { get; set; }              // RoomPhase int
        [MemoryPackOrder(11)] public string PhaseReason { get; set; }
        [MemoryPackOrder(12)] public long LaunchGeneration { get; set; }
        [MemoryPackOrder(13)] public long LoadingDeadlineUnixMs { get; set; }
        [MemoryPackOrder(14)] public string LaunchManifestHash { get; set; }
        [MemoryPackOrder(15)] public int LaunchManifestVersion { get; set; }
        [MemoryPackOrder(16)] public string LastStartFailureCode { get; set; }
        // 战斗代际固定的同步能力只随房间元数据传输，不进入逐帧快照。
        [MemoryPackOrder(17)] public WireNetworkSyncCapabilities? SyncCapabilities { get; set; }
    }

    [MemoryPackable]
    public partial struct WireWorldStartAnchor
    {
        [MemoryPackOrder(0)] public long StartServerTicks { get; set; }
        [MemoryPackOrder(1)] public long ServerTickFrequency { get; set; }
        [MemoryPackOrder(2)] public int StartFrame { get; set; }
        [MemoryPackOrder(3)] public double FixedDeltaSeconds { get; set; }
    }

    // ===== 阶段 4：资源加载屏障 / 状态查询 wire 结构体 =====

    /// <summary>
    /// Owner 发起资源加载阶段请求（Lobby -> Loading）。
    /// </summary>
    [ProtocolOpCode(RoomGatewayOpCodes.BeginLoading, ProtocolDirection.ClientToServer, nameof(WireBeginLoadingReq))]
    [MemoryPackable]
    public partial struct WireBeginLoadingReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
        [MemoryPackOrder(2)] public long? ExpectedRevision { get; set; }
        [MemoryPackOrder(3)] public string CommandId { get; set; }
    }

    /// <summary>
    /// Room 操作统一结果（BeginLoading / ReportAssetsLoaded / CancelLoading 共用）。
    /// 附带操作后的最新快照，便于客户端一次性刷新本地视图。
    /// </summary>
    [MemoryPackable]
    public partial struct WireRoomOperationRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public bool Applied { get; set; }
        [MemoryPackOrder(2)] public int ErrorCode { get; set; }   // RoomOperationErrorCode int
        [MemoryPackOrder(3)] public string Message { get; set; }
        [MemoryPackOrder(4)] public long RoomRevision { get; set; }
        [MemoryPackOrder(5)] public WireRoomSnapshot Snapshot { get; set; }  // 操作后的最新快照
    }

    /// <summary>
    /// 成员上报资源加载完成。
    /// </summary>
    [ProtocolOpCode(RoomGatewayOpCodes.ReportAssetsLoaded, ProtocolDirection.ClientToServer, nameof(WireReportAssetsLoadedReq))]
    [MemoryPackable]
    public partial struct WireReportAssetsLoadedReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
        [MemoryPackOrder(2)] public long LaunchGeneration { get; set; }
        [MemoryPackOrder(3)] public int ManifestVersion { get; set; }
        [MemoryPackOrder(4)] public string ManifestHash { get; set; }
        [MemoryPackOrder(5)] public string CommandId { get; set; }
    }

    /// <summary>
    /// Monotonic loading progress for the current launch generation.
    /// </summary>
    [ProtocolOpCode(RoomGatewayOpCodes.ReportLoadingProgress, ProtocolDirection.ClientToServer, nameof(WireReportLoadingProgressReq))]
    [MemoryPackable]
    public partial struct WireReportLoadingProgressReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
        [MemoryPackOrder(2)] public long LaunchGeneration { get; set; }
        [MemoryPackOrder(3)] public int ManifestVersion { get; set; }
        [MemoryPackOrder(4)] public string ManifestHash { get; set; }
        [MemoryPackOrder(5)] public int Progress { get; set; }
    }

    /// <summary>
    /// Owner 取消加载阶段，回到 Lobby。
    /// </summary>
    [ProtocolOpCode(RoomGatewayOpCodes.CancelLoading, ProtocolDirection.ClientToServer, nameof(WireCancelLoadingReq))]
    [MemoryPackable]
    public partial struct WireCancelLoadingReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
        [MemoryPackOrder(2)] public long? ExpectedRevision { get; set; }
        [MemoryPackOrder(3)] public string CommandId { get; set; }
    }

    /// <summary>
    /// Leaves the authoritative room membership. The operation result is returned
    /// using <see cref="WireRoomOperationRes"/>.
    /// </summary>
    [ProtocolOpCode(RoomGatewayOpCodes.LeaveRoom, ProtocolDirection.ClientToServer, nameof(WireLeaveRoomReq))]
    [MemoryPackable]
    public partial struct WireLeaveRoomReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
        [MemoryPackOrder(2)] public long? ExpectedRevision { get; set; }
        [MemoryPackOrder(3)] public string CommandId { get; set; }
    }

    /// <summary>
    /// 查询 Room 当前快照。
    /// </summary>
    [ProtocolOpCode(RoomGatewayOpCodes.GetSnapshot, ProtocolDirection.ClientToServer, nameof(WireGetSnapshotReq))]
    [MemoryPackable]
    public partial struct WireGetSnapshotReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string RoomId { get; set; }
    }

    /// <summary>
    /// Room 状态变更推送（server -> client）。
    /// </summary>
    [ProtocolOpCode(RoomGatewayOpCodes.RoomStateChanged, ProtocolDirection.ServerToClient, nameof(WireRoomStateChangedPush))]
    [MemoryPackable]
    public partial struct WireRoomStateChangedPush
    {
        [MemoryPackOrder(0)] public string RoomId { get; set; }
        [MemoryPackOrder(1)] public WireRoomSnapshot Snapshot { get; set; }
        [MemoryPackOrder(2)] public long ServerNowTicks { get; set; }
    }
}
