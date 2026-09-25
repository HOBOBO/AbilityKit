using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using Orleans;

namespace AbilityKit.Orleans.Grains.Rooms;

public sealed class RoomIdMappingGrain : Grain, IRoomIdMappingGrain
{
    private readonly IRoomStateStore _roomStateStore;

    public RoomIdMappingGrain(IRoomStateStore roomStateStore)
    {
        _roomStateStore = roomStateStore ?? throw new ArgumentNullException(nameof(roomStateStore));
    }

    public Task<ulong> GetOrCreateNumericIdAsync(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required", nameof(roomId));
        return _roomStateStore.GetOrCreateNumericRoomIdAsync(roomId);
    }

    public Task<string?> TryGetRoomIdAsync(ulong numericRoomId)
    {
        if (numericRoomId == 0) return Task.FromResult<string?>(null);
        return _roomStateStore.TryGetRoomIdAsync(numericRoomId);
    }

    public Task BindAccountRoomAsync(string accountId, string roomId)
    {
        if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("accountId is required", nameof(accountId));
        if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required", nameof(roomId));

        return _roomStateStore.BindAccountRoomAsync(accountId, roomId);
    }

    public Task<bool> TryBindAccountRoomIfActiveAsync(string accountId, string roomId)
    {
        if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("accountId is required", nameof(accountId));
        if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required", nameof(roomId));

        return _roomStateStore.TryBindAccountRoomIfActiveAsync(accountId, roomId);
    }

    public Task<string?> TryGetAccountRoomAsync(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return Task.FromResult<string?>(null);
        return _roomStateStore.TryGetAccountRoomAsync(accountId);
    }

    public Task ClearAccountRoomAsync(string accountId, string roomId)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(roomId))
        {
            return Task.CompletedTask;
        }

        return _roomStateStore.ClearAccountRoomAsync(accountId, roomId);
    }
}
