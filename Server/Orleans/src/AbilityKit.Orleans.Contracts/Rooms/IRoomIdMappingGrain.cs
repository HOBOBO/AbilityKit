using Orleans;

namespace AbilityKit.Orleans.Contracts.Rooms;

public interface IRoomIdMappingGrain : IGrainWithStringKey
{
    Task<ulong> GetOrCreateNumericIdAsync(string roomId);

    Task<string?> TryGetRoomIdAsync(ulong numericRoomId);
}
