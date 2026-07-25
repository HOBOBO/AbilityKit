using Orleans;

namespace AbilityKit.Orleans.Contracts.Battle;

/// <summary>
/// Battle Logic Host Grain 接口
/// 在服务器端运行战斗逻辑，生成状态快照
/// </summary>
public interface IBattleLogicHostGrain : IGrainWithStringKey
{
    /// <summary>
    /// 初始化战斗世界（旧入口，保留兼容，内部委托 <see cref="InitializeBattleWithResultAsync"/>）。
    /// </summary>
    Task InitializeBattleAsync(BattleInitParams initParams);

    /// <summary>
    /// 幂等 create-or-get 初始化战斗世界，返回结构化结果（含 init spec hash 冲突检测）。
    /// </summary>
    Task<BattleInitResult> InitializeBattleWithResultAsync(BattleInitParams initParams, string? initSpecHash);

    /// <summary>
    /// 提交玩家输入。
    /// </summary>
    Task<BattleInputSubmitResult> SubmitInputAsync(ulong worldId, int frame, BattleInputItem input);

    /// <summary>
    /// 将玩家增量加入正在运行的战斗世界。
    /// </summary>
    Task<BattlePlayerJoinResult> JoinPlayerAsync(BattlePlayerJoinRequest request);

    /// <summary>
    /// 为正在运行的战斗玩家挂载服务器侧战斗 AI。
    /// </summary>
    Task<BattleBotAiMountResult> MountBotAiAsync(BattleBotAiMountRequest request);

    /// <summary>
    /// 获取当前帧
    /// </summary>
    Task<int> GetCurrentFrameAsync();

    /// <summary>
    /// 获取快照（用于调试）
    /// </summary>
    Task<BattleSnapshot?> GetSnapshotAsync();

    /// <summary>
    /// 获取战斗世界实体/组件诊断视图（用于后台检查）。
    /// </summary>
    Task<BattleWorldDiagnostics?> GetWorldDiagnosticsAsync();

    /// <summary>
    /// 获取战斗世界开始时间锚点。
    /// </summary>
    Task<WorldStartAnchor?> GetWorldStartAnchorAsync();

    /// <summary>
    /// 订阅状态同步观察者，并从客户端累计确认位置恢复可靠事件。
    /// 观察者元数据由调用方随请求传入，避免 Host 回调非可重入 Observer。
    /// </summary>
    Task SubscribeAsync(
        IStateSyncObserverGrain observer,
        StateSyncObserverInfo observerInfo,
        ReliableBattleEventSubscribeCursor eventCursor);

    /// <summary>
    /// 接受客户端可靠事件累计确认位置。
    /// </summary>
    Task<ReliableBattleEventAckResult> AcknowledgeReliableEventsAsync(string observerKey, string epoch, long sequence);

    /// <summary>
    /// 向指定观察者推送当前完整快照。
    /// </summary>
    Task RequestFullSnapshotAsync(IStateSyncObserverGrain observer, StateSyncObserverInfo observerInfo);

    /// <summary>
    /// 取消订阅状态同步观察者
    /// </summary>
    Task UnsubscribeAsync(IStateSyncObserverGrain observer);

    /// <summary>
    /// 销毁战斗世界
    /// </summary>
    Task DestroyAsync();
}

/// <summary>
/// 战斗初始化参数
/// </summary>
[GenerateSerializer]
public class BattleInitParams
{
    [Id(0)] public ulong WorldId { get; set; }
    [Id(1)] public int TickRate { get; set; }
    [Id(2)] public List<PlayerInitInfo>? Players { get; set; }
    [Id(3)] public int MapId { get; set; } = 1;
    [Id(4)] public int GameplayId { get; set; }
    [Id(5)] public int RuleSetId { get; set; }
    [Id(6)] public int ConfigVersion { get; set; }
    [Id(7)] public int ProtocolVersion { get; set; }
    [Id(8)] public int RandomSeed { get; set; }
    [Id(9)] public int InputDelayFrames { get; set; }
    [Id(10)] public string? WorldType { get; set; }
    [Id(11)] public string? ClientId { get; set; }
    [Id(12)] public string? RoomType { get; set; }
    [Id(13)] public WorldStartAnchor? WorldStartAnchor { get; set; }
    [Id(14)] public BattleSyncStartOptions? SyncOptions { get; set; }
    [Id(15)] public int DurationFrames { get; set; }
    [Id(16)] public int VictoryTargetDefeats { get; set; }
    [Id(17)] public bool ContinueAfterAllPlayersDefeated { get; set; }
}

/// <summary>
/// 战斗初始化结构化结果。支持幂等 create-or-get 与 init spec hash 冲突检测。
/// </summary>
[GenerateSerializer]
public sealed record BattleInitResult(
    [property: Id(0)] bool Initialized,
    [property: Id(1)] bool AlreadyInitialized,
    [property: Id(2)] string? InitSpecHash,
    [property: Id(3)] WorldStartAnchor? WorldStartAnchor,
    [property: Id(4)] string? Error)
{
    /// <summary>
    /// 是否成功（首次初始化成功，或已初始化且 hash 匹配的幂等命中）。
    /// </summary>
    public bool Succeeded => Error is null && (Initialized || AlreadyInitialized);

    public static BattleInitResult FromInitialized(string? initSpecHash, WorldStartAnchor? anchor) =>
        new(Initialized: true, AlreadyInitialized: false, initSpecHash, anchor, Error: null);

    public static BattleInitResult FromAlreadyInitialized(string? storedHash, WorldStartAnchor? anchor) =>
        new(Initialized: false, AlreadyInitialized: true, storedHash, anchor, Error: null);

    public static BattleInitResult FromHashMismatch(string? storedHash) =>
        new(Initialized: false, AlreadyInitialized: true, storedHash, WorldStartAnchor: null, Error: "InitSpecHashMismatch");

    public static BattleInitResult FromError(string error) =>
        new(Initialized: false, AlreadyInitialized: false, InitSpecHash: null, WorldStartAnchor: null, Error: error);
}

[GenerateSerializer]
public sealed record BattleSyncStartOptions(
    [property: Id(0)] string? SyncTemplateId,
    [property: Id(1)] int SyncModel,
    [property: Id(2)] string? NetworkEnvironmentId,
    [property: Id(3)] string? CarrierName,
    [property: Id(4)] bool EnableAuthoritativeWorld,
    [property: Id(5)] bool InterpolationEnabled,
    [property: Id(6)] int InputDelayFrames);

/// <summary>
/// 玩家初始化信息
/// </summary>
[GenerateSerializer]
public class PlayerInitInfo
{
    [Id(0)] public uint PlayerId { get; set; }
    [Id(1)] public int ActorId { get; set; }
    [Id(2)] public int HeroId { get; set; }
    [Id(3)] public float PosX { get; set; }
    [Id(4)] public float PosY { get; set; }
    [Id(5)] public float PosZ { get; set; }
    [Id(6)] public int TeamId { get; set; }
    [Id(7)] public int Level { get; set; }
    [Id(8)] public int AttributeTemplateId { get; set; }
    [Id(9)] public int BasicAttackSkillId { get; set; }
    [Id(10)] public List<int>? SkillIds { get; set; }
    [Id(11)] public string? AccountId { get; set; }
}

[GenerateSerializer]
public sealed record BattlePlayerJoinRequest(
    [property: Id(0)] ulong WorldId,
    [property: Id(1)] PlayerInitInfo Player,
    [property: Id(2)] bool IsBot = false);

[GenerateSerializer]
public sealed record BattlePlayerJoinResult(
    [property: Id(0)] bool Accepted,
    [property: Id(1)] uint PlayerId,
    [property: Id(2)] int CurrentFrame,
    [property: Id(3)] string Status,
    [property: Id(4)] string Message);

[GenerateSerializer]
public sealed record BattleBotAiMountRequest(
    [property: Id(0)] ulong WorldId,
    [property: Id(1)] uint PlayerId,
    [property: Id(2)] string? ProfileId = null);

[GenerateSerializer]
public sealed record BattleBotAiMountResult(
    [property: Id(0)] bool Accepted,
    [property: Id(1)] uint PlayerId,
    [property: Id(2)] int CurrentFrame,
    [property: Id(3)] string Status,
    [property: Id(4)] string Message);
 
/// <summary>
/// 战斗输入项
/// </summary>
[GenerateSerializer]
public class BattleInputItem
{
    [Id(0)] public uint PlayerId { get; set; }
    [Id(1)] public int OpCode { get; set; }
    [Id(2)] public byte[]? Payload { get; set; }

    /// <summary>
    /// Per-player monotonically increasing command sequence. Zero denotes a legacy caller.
    /// </summary>
    [Id(3)] public ulong CommandSequence { get; set; }
}

[GenerateSerializer]
public sealed record BattleInputSubmitResult(
    [property: Id(0)] bool Accepted,
    [property: Id(1)] int RequestedFrame,
    [property: Id(2)] int AcceptedFrame,
    [property: Id(3)] int CurrentFrame,
    [property: Id(4)] string Status,
    [property: Id(5)] string Message);

/// <summary>
/// 战斗快照
/// </summary>
[GenerateSerializer]
public class BattleSnapshot
{
    [Id(0)] public int Frame { get; set; }
    [Id(1)] public List<ActorSnapshot> Actors { get; set; } = new();
    [Id(2)] public int MatchState { get; set; }
    [Id(3)] public bool MatchFinal { get; set; }
    [Id(4)] public bool MatchVictory { get; set; }
    [Id(5)] public int MatchCompletedFrame { get; set; }
    [Id(6)] public int DefeatedEnemies { get; set; }
    [Id(7)] public int VictoryTargetDefeats { get; set; }
    [Id(8)] public int TimeLimitFrames { get; set; }
    [Id(9)] public int RemainingTimeFrames { get; set; }
}

[GenerateSerializer]
public sealed class BattleWorldDiagnostics
{
    [Id(0)] public string BattleId { get; set; } = string.Empty;
    [Id(1)] public string WorldType { get; set; } = string.Empty;
    [Id(2)] public ulong WorldId { get; set; }
    [Id(3)] public int Frame { get; set; }
    [Id(4)] public uint StateHash { get; set; }
    [Id(5)] public int EntityCount { get; set; }
    [Id(6)] public List<BattleWorldEntityDiagnostics> Entities { get; set; } = new();
    [Id(7)] public List<BattleWorldComponentChunkDiagnostics> ComponentChunks { get; set; } = new();
    [Id(8)] public List<string> Warnings { get; set; } = new();
    [Id(9)] public long ServerNowTicks { get; set; }
}

[GenerateSerializer]
public sealed class BattleWorldEntityDiagnostics
{
    [Id(0)] public string Key { get; set; } = string.Empty;
    [Id(1)] public int EntityId { get; set; }
    [Id(2)] public string EntityKind { get; set; } = string.Empty;
    [Id(3)] public string Group { get; set; } = string.Empty;
    [Id(4)] public string Label { get; set; } = string.Empty;
    [Id(5)] public bool Alive { get; set; }
    [Id(6)] public List<BattleWorldComponentDiagnostics> Components { get; set; } = new();
}

[GenerateSerializer]
public sealed class BattleWorldComponentDiagnostics
{
    [Id(0)] public string Name { get; set; } = string.Empty;
    [Id(1)] public string ComponentKind { get; set; } = string.Empty;
    [Id(2)] public Dictionary<string, string> Fields { get; set; } = new();
}

[GenerateSerializer]
public sealed class BattleWorldComponentChunkDiagnostics
{
    [Id(0)] public string ComponentKind { get; set; } = string.Empty;
    [Id(1)] public string EntityKind { get; set; } = string.Empty;
    [Id(2)] public int Count { get; set; }
}

/// <summary>
/// Actor 快照
/// </summary>
[GenerateSerializer]
public class ActorSnapshot
{
    [Id(0)] public int ActorId { get; set; }
    [Id(1)] public float X { get; set; }
    [Id(2)] public float Y { get; set; }
    [Id(3)] public float Z { get; set; }
    [Id(4)] public float Rotation { get; set; }
    [Id(5)] public float VelocityX { get; set; }
    [Id(6)] public float VelocityZ { get; set; }
    [Id(7)] public float Hp { get; set; }
    [Id(8)] public float HpMax { get; set; }
    [Id(9)] public int TeamId { get; set; }
    // Spawn 层字段（生成缺失 actor 所需）：实体类型（1=Character, 2=Projectile）、配置/模板 id、拥有者 actorId（角色为 0）
    [Id(10)] public int Kind { get; set; }
    [Id(11)] public int Code { get; set; }
    [Id(12)] public int OwnerNetId { get; set; }
}

/// <summary>
/// 战斗世界开始时间锚点。
/// </summary>
[GenerateSerializer]
public sealed record WorldStartAnchor(
    [property: Id(0)] long StartServerTicks,
    [property: Id(1)] long ServerTickFrequency,
    [property: Id(2)] int StartFrame,
    [property: Id(3)] double FixedDeltaSeconds);

/// <summary>
/// 状态同步观察者接口
/// 用于推送服务器快照到客户端
/// </summary>
public interface IStateSyncObserver
{
    /// <summary>
    /// 推送快照
    /// </summary>
    void OnSnapshotPushed(StateSyncPush push);
}

/// <summary>
/// 状态同步推送
/// </summary>
[GenerateSerializer]
public class StateSyncPush
{
    [Id(0)] public ulong WorldId { get; set; }
    [Id(1)] public int Frame { get; set; }
    [Id(2)] public double Timestamp { get; set; }
    [Id(3)] public List<ActorSnapshot> Actors { get; set; } = new();

    /// <summary>
    /// 是否为全量快照（vs 增量快照）
    /// </summary>
    [Id(4)] public bool IsFullSnapshot { get; set; } = true;

    /// <summary>
    /// 可选的玩法自定义快照 op code。
    /// </summary>
    [Id(5)] public int PayloadOpCode { get; set; }

    /// <summary>
    /// 可选的玩法自定义快照二进制负载。
    /// </summary>
    [Id(6)] public byte[]? Payload { get; set; }

    /// <summary>
    /// 服务端时间域 ticks。Timestamp 保留为兼容字段。
    /// </summary>
    [Id(7)] public long ServerTicks { get; set; }

    /// <summary>
    /// 创建该快照时已产生的可靠事件最高权威序列。
    /// </summary>
    [Id(8)] public long EventWatermark { get; set; }
}
