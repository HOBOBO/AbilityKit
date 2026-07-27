using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using Orleans;
using ContractCreateRoomRequest = AbilityKit.Orleans.Contracts.Rooms.CreateRoomRequest;
using ContractCreateRoomResponse = AbilityKit.Orleans.Contracts.Rooms.CreateRoomResponse;

namespace AbilityKit.Orleans.Grains.Rooms;

public sealed class RoomDirectoryGrain : Grain, IRoomDirectoryGrain
{
    private readonly IRoomStateStore _roomStateStore;

    public RoomDirectoryGrain(IRoomStateStore roomStateStore)
    {
        _roomStateStore = roomStateStore ?? throw new ArgumentNullException(nameof(roomStateStore));
    }

    public async Task<ContractCreateRoomResponse> CreateRoomAsync(ContractCreateRoomRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.AccountId)) throw new ArgumentException("AccountId is required", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Region)) throw new ArgumentException("Region is required", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ServerId)) throw new ArgumentException("ServerId is required", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RoomType)) throw new ArgumentException("RoomType is required", nameof(request));

        var directoryKey = this.GetPrimaryKeyString();
        var expectedKey = BuildDirectoryKey(request.Region, request.ServerId);
        if (!string.Equals(directoryKey, expectedKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Directory key mismatch. Expected={expectedKey} Actual={directoryKey}");
        }

        var roomId = Guid.NewGuid().ToString("N");
        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var summary = new RoomSummary(
            request.Region,
            request.ServerId,
            roomId,
            request.RoomType,
            request.Title ?? string.Empty,
            request.IsPublic,
            request.MaxPlayers,
            0,
            request.AccountId,
            createdAt,
            request.Tags);

        var room = GrainFactory.GetGrain<IRoomGrain>(roomId);
        await room.InitializeAsync(summary, directoryKey);

        var snapshot = await room.GetSnapshotAsync();
        await _roomStateStore.UpsertRoomAsync(directoryKey, snapshot.Summary);

        return new ContractCreateRoomResponse(roomId);
    }

    public async Task<ListRoomsResponse> ListRoomsAsync(ListRoomsRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var directoryKey = this.GetPrimaryKeyString();
        var expectedKey = BuildDirectoryKey(request.Region, request.ServerId);
        if (!string.Equals(directoryKey, expectedKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Directory key mismatch. Expected={expectedKey} Actual={directoryKey}");
        }

        var offset = Math.Max(0, request.Offset);
        var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 200);

        IEnumerable<RoomSummary> query = await _roomStateStore.ListRoomsAsync(directoryKey);
        if (!string.IsNullOrWhiteSpace(request.RoomType))
        {
            query = query.Where(r => string.Equals(r.RoomType, request.RoomType, StringComparison.Ordinal));
        }

        query = query.Where(r => r.IsPublic);
        var rooms = query
            .OrderByDescending(r => r.CreatedAtUnixMs)
            .Skip(offset)
            .Take(limit)
            .ToList();

        var nextOffset = offset + rooms.Count;
        return new ListRoomsResponse(rooms, nextOffset);
    }

    public Task NotifyRoomChangedAsync(string roomId, int playerCount)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Task.CompletedTask;
        }

        return _roomStateStore.UpdateRoomPlayerCountAsync(this.GetPrimaryKeyString(), roomId, playerCount);
    }

    public Task RemoveRoomAsync(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Task.CompletedTask;
        }

        return _roomStateStore.RemoveRoomAsync(this.GetPrimaryKeyString(), roomId);
    }

    public static string BuildDirectoryKey(string region, string serverId) => $"{region}:{serverId}";
}
