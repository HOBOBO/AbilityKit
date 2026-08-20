using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Orleans.Grains.Battle.Gameplay;

internal interface IBattleRuntimeAdapter
{
    string RoomType { get; }

    IBattleRuntimeSession CreateSession(string battleId);
}

internal readonly record struct BattleStateSyncObserverContext(
    string ObserverKey,
    string AccountId,
    string RoomId)
{
    public ShooterCommandAcknowledgement[]? AcknowledgedCommands { get; init; }
}

internal interface IBattleRuntimeSession : IDisposable
{
    BattleRuntimeStartResult Start(BattleInitParams initParams);

    BattlePlayerJoinResult JoinPlayer(BattlePlayerJoinRequest request, int currentFrame);

    BattleInputValidationResult ValidateInput(BattleInputItem input) => BattleInputValidationResult.Valid;

    int SubmitInputs(int frame, IReadOnlyList<BattleInputItem> inputs);

    BattleBotAiMountResult MountBotAi(BattleBotAiMountRequest request, int currentFrame);

    bool Tick(int frame, int tickRate, float deltaTime);

    BattleSnapshot? GetSnapshot(int frame);

    BattleWorldDiagnostics? GetWorldDiagnostics(ulong worldId, int frame);

    StateSyncPush CreateStateSyncPush(ulong worldId, int frame, bool isFullSnapshot);
}

internal interface IBattleRuntimeInputDiagnostics
{
    string LastInputSubmitDiagnostic { get; }
}

internal interface IObserverAwareBattleRuntimeSession
{
    StateSyncPush CreateStateSyncPush(ulong worldId, int frame, bool isFullSnapshot, in BattleStateSyncObserverContext observerContext);
}

internal interface IReliableBattleEventProducer
{
    IReadOnlyList<ReliableBattleEventSource> CaptureReliableEvents(int frame);
}

internal readonly record struct ReliableBattleEventSource(
    int SourceFrame,
    int EventType,
    byte[]? Payload);

internal readonly record struct BattleRuntimeStartResult(bool Succeeded, string? Error)
{
    public static BattleRuntimeStartResult Success() => new(true, null);

    public static BattleRuntimeStartResult Fail(string error) => new(false, error);
}

internal readonly record struct BattleInputValidationResult(bool Accepted, string Status, string Message)
{
    public static BattleInputValidationResult Valid { get; } = new(true, string.Empty, string.Empty);

    public static BattleInputValidationResult Reject(string status, string message) => new(false, status, message);
}
