using Orleans;

namespace AbilityKit.Orleans.Contracts.Rooms;

public interface IRoomGrain : IGrainWithStringKey
{
    Task InitializeAsync(RoomSummary summary, string directoryKey);

    Task<RoomSnapshot> GetSnapshotAsync();

    Task JoinAsync(string accountId);

    Task LeaveAsync(string accountId);

    Task CloseAsync(string accountId);
}
