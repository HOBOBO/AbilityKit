using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;

namespace AbilityKit.Orleans.Grains.Rooms.Gameplay;

internal interface IRoomGameplayAdapter
{
    string RoomType { get; }

    object CreateState(RoomSummary summary);

    RoomGameplayPersistentState ExportPersistentState(object state);

    object RestorePersistentState(RoomSummary summary, RoomGameplayPersistentState persistentState);

    void Join(object state, RoomSummary summary, IReadOnlyCollection<string> members, string accountId);

    void Leave(object state, string accountId);

    void SetReady(object state, RoomReadyRequest request);

    RoomGameplayCommandResult SubmitCommand(object state, RoomGameplayCommandRequest request);

    bool CanStart(object state);

    bool ValidateBeginLoading(object state);

    RoomLaunchManifest BuildLaunchManifest(object state, RoomSummary summary);

    List<RoomPlayerSnapshot> BuildPlayerSnapshots(object state);

    BattleInitParams BuildBattleInitParams(object state, RoomSummary summary, StartRoomBattleRequest request);

    PlayerInitInfo? BuildLateJoinPlayer(object state, RoomSummary summary, string accountId);
}

internal readonly record struct RoomGameplayCommandResult(
    bool Success,
    RoomOperationErrorCode ErrorCode,
    string Message)
{
    public static RoomGameplayCommandResult Accepted() =>
        new(true, RoomOperationErrorCode.None, string.Empty);

    public static RoomGameplayCommandResult Rejected(
        RoomOperationErrorCode errorCode,
        string message) =>
        new(false, errorCode, message ?? string.Empty);
}
