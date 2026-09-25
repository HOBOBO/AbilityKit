using Orleans;

namespace AbilityKit.Orleans.Contracts.Rooms;

public interface IRoomIdMappingGrain : IGrainWithStringKey
{
    Task<ulong> GetOrCreateNumericIdAsync(string roomId);

    Task<string?> TryGetRoomIdAsync(ulong numericRoomId);

    Task BindAccountRoomAsync(string accountId, string roomId);

    Task<bool> TryBindAccountRoomIfActiveAsync(string accountId, string roomId);

    Task<string?> TryGetAccountRoomAsync(string accountId);

    Task ClearAccountRoomAsync(string accountId, string roomId);
}
