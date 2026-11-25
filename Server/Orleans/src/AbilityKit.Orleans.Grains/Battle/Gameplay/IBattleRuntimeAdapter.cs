using AbilityKit.Orleans.Contracts.Battle;
using System;

namespace AbilityKit.Orleans.Grains.Battle.Gameplay;

/// <summary>
/// 玩法战斗运行时适配器。整个 adapter 契约（含全部可选能力接口）都是 public，
/// 以便 Grains 装配之外的玩法模块（例如 <c>AbilityKit.Demo.Tiny.Server</c>）实现；
/// 可选能力由 <c>BattleLogicHostGrain</c> 按接口模式匹配自动启用。
/// </summary>
public interface IBattleRuntimeAdapter
{
    string RoomType { get; }

    IBattleRuntimeSession CreateSession(string battleId);
}

/// <summary>
/// 玩法无关的已消费房间命令回执。玩法在构建按观察者推送的载荷时把它嵌进自己的协议类型，
/// 因此契约层不引用任何具体玩法协议。
/// </summary>
public readonly record struct BattleCommandAcknowledgement(int Frame, ulong CommandId);

public readonly record struct BattleStateSyncObserverContext(
    string ObserverKey,
    string AccountId,
    string RoomId)
{
    public BattleCommandAcknowledgement[]? AcknowledgedCommands { get; init; }
    public bool UsesFixedBaseline { get; init; }
    internal StateSyncPreparedRecipient? PreparedRecipient { get; init; }
}

public interface IBattleRuntimeSession : IDisposable
{
    BattleRuntimeStartResult Start(BattleInitParams initParams);

    BattlePlayerJoinResult JoinPlayer(BattlePlayerJoinRequest request, int currentFrame);

    BattleInputValidationResult ValidateInput(BattleInputItem input) => BattleInputValidationResult.Valid;

    int SubmitInputs(int frame, IReadOnlyList<BattleInputItem> inputs);

    BattleBotAiMountResult MountBotAi(BattleBotAiMountRequest request, int currentFrame);

    bool Tick(int frame, int tickRate, float deltaTime);

    BattleSnapshot? GetSnapshot(int frame);

    BattleWorldDiagnostics? GetWorldDiagnostics(ulong worldId, int frame);

    BattleDiagnosticEventsResult QueryDiagnosticEvents(BattleDiagnosticEventsQuery query);

    StateSyncPush CreateStateSyncPush(ulong worldId, int frame, bool isFullSnapshot);
}

/// <summary>
/// 每步响应哈希的可选快速路径。完整世界诊断会构造完整检查模型，不适合权威步进热路径，因此仅通过 GetWorldDiagnostics 获取。
/// </summary>
public interface IBattleRuntimeStateHashProvider
{
    uint ComputeStateHash();
}

/// <summary>
/// 可选的权威结算凭据：只核对刚完成的世界帧，不推进世界、不用诊断哈希代替。
/// 只由没有旁路输入、且不重写同一会话已完成历史的运行时对符合条件的帧提供。
/// </summary>
public interface IBattleRuntimeFrameCompletion
{
    bool CanFinalizeFrame(int frame);
}

/// <summary>在混合帧同步模式下，于输入分发前准备同一权威帧的模拟时钟。</summary>
public interface IBattleRuntimeExternalFrameClock
{
    bool HasInitialFrameCheckpoint => false;

    bool TryPrepareExternalFrame(int frame, float fixedDeltaTime);
}

/// <summary>
/// 仅状态同步定时宿主在派发输入前调用；不接受结算帧或客户端 Tick。
/// </summary>
internal interface IBattleRuntimeStateSyncClock
{
    bool TryBeginStateSyncTick(int tick, int tickRate);
}

/// <summary>可选：阶段耗时诊断，供服务端性能观测消费。</summary>
public interface IBattleRuntimeStageDiagnostics
{
    void SetStageTimingSink(Action<string, double>? sink);
}

/// <summary>可选：最后一次输入提交诊断，用于定位被拒绝/丢弃输入的原因。</summary>
public interface IBattleRuntimeInputDiagnostics
{
    string LastInputSubmitDiagnostic { get; }
}

/// <summary>
/// 可选：按观察者构建推送。实现后由 <c>BattleLogicHostGrain</c> 走 per-observer 推送路径
/// （AOI/旁观者兴趣裁剪的前提）；未实现则退回广播路径。
/// </summary>
public interface IObserverAwareBattleRuntimeSession
{
    StateSyncPush CreateStateSyncPush(ulong worldId, int frame, bool isFullSnapshot, in BattleStateSyncObserverContext observerContext);
}

/// <summary>
/// 提供可跳包的完整帧投影，不推进旧协议的链式差分缓存。
/// </summary>
public interface IFixedBaselineBattleSnapshotSource
{
    /// <summary>
    /// 返回该帧完整可见投影和独立可导入的玩法负载。
    /// </summary>
    StateSyncPush CaptureFixedBaselineProjection(ulong worldId, int frame);
}

/// <summary>可选：可靠事件源，供服务端可靠事件投递消费。</summary>
public interface IReliableBattleEventProducer
{
    IReadOnlyList<ReliableBattleEventSource> CaptureReliableEvents(int frame);
}

public readonly record struct ReliableBattleEventSource(
    int SourceFrame,
    int EventType,
    byte[]? Payload);

public readonly record struct BattleRuntimeStartResult(bool Succeeded, string? Error)
{
    public static BattleRuntimeStartResult Success() => new(true, null);

    public static BattleRuntimeStartResult Fail(string error) => new(false, error);
}

public readonly record struct BattleInputValidationResult(bool Accepted, string Status, string Message)
{
    public static BattleInputValidationResult Valid { get; } = new(true, string.Empty, string.Empty);

    public static BattleInputValidationResult Reject(string status, string message) => new(false, status, message);
}
