using AbilityKit.Orleans.Contracts.Rooms;

namespace AbilityKit.Orleans.Grains.Persistence;

public interface IRoomStateStore
{
    Task<RoomCreateCommandState?> TryGetCreateCommandAsync(string directoryKey, string accountId, string commandId, CancellationToken cancellationToken = default);

    Task<RoomCreateCommandState> RecordCreateCommandAsync(string directoryKey, string accountId, string commandId, RoomCreateCommandState command, CancellationToken cancellationToken = default);

    Task UpsertRoomAsync(string directoryKey, RoomSummary summary, CancellationToken cancellationToken = default);

    Task<bool> TryPublishActiveRoomAsync(string directoryKey, RoomSummary summary, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RoomSummary>> ListRoomsAsync(string directoryKey, CancellationToken cancellationToken = default);

    Task UpdateRoomPlayerCountAsync(string directoryKey, string roomId, int playerCount, CancellationToken cancellationToken = default);

    Task RemoveRoomAsync(string directoryKey, string roomId, CancellationToken cancellationToken = default);

    Task<ulong> GetOrCreateNumericRoomIdAsync(string roomId, CancellationToken cancellationToken = default);

    Task<string?> TryGetRoomIdAsync(ulong numericRoomId, CancellationToken cancellationToken = default);

    Task BindAccountRoomAsync(string accountId, string roomId, CancellationToken cancellationToken = default);

    Task<bool> TryBindAccountRoomIfActiveAsync(string accountId, string roomId, CancellationToken cancellationToken = default);

    Task<string?> TryGetAccountRoomAsync(string accountId, CancellationToken cancellationToken = default);

    Task ClearAccountRoomAsync(string accountId, string roomId, CancellationToken cancellationToken = default);

    Task<RoomPersistentState?> TryGetRuntimeStateAsync(string roomId, CancellationToken cancellationToken = default);

    Task WriteRuntimeStateAsync(string roomId, RoomPersistentState state, CancellationToken cancellationToken = default);

    Task RemoveRuntimeStateAsync(string roomId, CancellationToken cancellationToken = default);
}

public sealed record RoomCreateCommandState(string RoomId, CreateRoomRequest Request);
