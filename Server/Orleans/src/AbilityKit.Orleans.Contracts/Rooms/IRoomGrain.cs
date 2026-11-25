using Orleans;

namespace AbilityKit.Orleans.Contracts.Rooms;

public interface IRoomGrain : IGrainWithStringKey
{
    Task InitializeAsync(RoomSummary summary, string directoryKey);

    Task<RoomSnapshot> GetSnapshotAsync();

    Task<RoomRuntimeState> GetRuntimeStateAsync();

    Task<JoinRoomResponse> JoinAsync(string accountId);

    Task<JoinRoomResponse> JoinMemberAsync(JoinRoomMemberRequest request);

    Task<RestoreRoomResponse> RestoreAsync(string accountId);

    Task MarkOfflineAsync(string accountId);

    Task<RoomOperationResult> MarkOfflineWithResultAsync(string accountId);

    Task LeaveAsync(string accountId);

    Task<RoomOperationResult> LeaveWithResultAsync(string accountId);

    Task SetReadyAsync(RoomReadyRequest request);

    Task<RoomOperationResult> SetLobbyReadyWithResultAsync(RoomReadyRequest request);

    Task SubmitGameplayCommandAsync(RoomGameplayCommandRequest request);

    Task<RoomOperationResult> SubmitGameplayCommandWithResultAsync(RoomGameplayCommandRequest request);

    Task<StartRoomBattleResponse> StartBattleAsync(StartRoomBattleRequest request);

    Task CloseAsync(string accountId);

    Task<RoomOperationResult> BeginLoadingWithResultAsync(BeginLoadingRequest request);

    Task<RoomOperationResult> ReportLoadingProgressWithResultAsync(ReportLoadingProgressRequest request);

    Task<RoomOperationResult> ReportAssetsLoadedWithResultAsync(ReportAssetsLoadedRequest request);

    Task<RoomOperationResult> CancelLoadingWithResultAsync(CancelLoadingRequest request);

    Task<RoomOperationResult> TickAsync(RoomTickRequest request);

    Task BindStatePushObserverAsync(
        string accountId,
        string bindingId,
        IRoomStateGatewayPushObserver observer);

    Task UnbindStatePushObserverAsync(string accountId, string bindingId);
}

/// <summary>
/// 网关本地房间状态观察者，通过 Orleans 对象引用公开给 Silo。
/// </summary>
public interface IRoomStateGatewayPushObserver : IGrainObserver
{
    void OnPush(uint opCode, byte[] payload);
}
