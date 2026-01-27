using Orleans;

namespace AbilityKit.Orleans.Contracts.Rooms;

public interface IRoomDirectoryGrain : IGrainWithStringKey
{
    Task<CreateRoomResponse> CreateRoomAsync(CreateRoomRequest request);

    Task<ListRoomsResponse> ListRoomsAsync(ListRoomsRequest request);

    Task NotifyRoomChangedAsync(string roomId, int playerCount);

    Task RemoveRoomAsync(string roomId);
}
