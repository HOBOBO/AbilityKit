using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using Orleans;
using System.Security.Cryptography;
using System.Text;
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

        var commandId = request.CommandId ?? string.Empty;
        if (commandId.Length > 128 || (commandId.Length > 0 && string.IsNullOrWhiteSpace(commandId)))
            throw new ArgumentException("Invalid create room command ID.", nameof(request));
        if (commandId.Length > 0)
        {
            var completed = await _roomStateStore.TryGetCreateCommandAsync(directoryKey, request.AccountId, commandId);
            if (completed is not null)
            {
                if (!SameRequest(completed.Request, request))
                    throw new InvalidOperationException("Create room command ID conflicts with another request.");
                var completedState = await _roomStateStore.TryGetRuntimeStateAsync(completed.RoomId);
                if (completedState is null || !IsActiveForCreator(completedState, request.AccountId))
                    throw new InvalidOperationException("The room created by this command is no longer active for its creator.");
                return new ContractCreateRoomResponse(completed.RoomId);
            }
        }

        var roomId = commandId.Length == 0
            ? Guid.NewGuid().ToString("N")
            : DeterministicRoomId(directoryKey, request.AccountId, commandId);
        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var roomType = GameplayRoomTypes.Normalize(request.RoomType);

        var summary = new RoomSummary(
            request.Region,
            request.ServerId,
            roomId,
            roomType,
            request.Title ?? string.Empty,
            request.IsPublic,
            request.MaxPlayers,
            0,
            request.AccountId,
            createdAt,
            request.Tags);

        var room = GrainFactory.GetGrain<IRoomGrain>(roomId);
        if (commandId.Length > 0)
        {
            var existing = await _roomStateStore.TryGetRuntimeStateAsync(roomId);
            if (existing is not null && !SameSummary(existing.Summary, request, roomType))
                throw new InvalidOperationException("Create room command ID conflicts with another request.");
            if (existing is not null && !IsActiveForCreator(existing, request.AccountId))
                throw new InvalidOperationException("The room created by this command is no longer active for its creator.");
        }
        await room.InitializeAsync(summary, directoryKey);

        if (!await _roomStateStore.TryPublishActiveRoomAsync(directoryKey, summary))
            throw new InvalidOperationException("The room created by this command is no longer active for its creator.");

        if (commandId.Length > 0)
        {
            var recorded = await _roomStateStore.RecordCreateCommandAsync(directoryKey,
                request.AccountId, commandId, new RoomCreateCommandState(roomId, request));
            if (!SameRequest(recorded.Request, request) || recorded.RoomId != roomId)
                throw new InvalidOperationException("Create room command ID conflicts with another request.");
        }

        return new ContractCreateRoomResponse(roomId);
    }

    private static string DeterministicRoomId(string directoryKey, string accountId, string commandId)
    {
        var bytes = Encoding.UTF8.GetBytes(directoryKey + "\0" + accountId + "\0" + commandId);
        var hash = SHA256.HashData(bytes);
        return new Guid(hash.AsSpan(0, 16)).ToString("N");
    }

    private static bool SameRequest(ContractCreateRoomRequest left, ContractCreateRoomRequest right) =>
        left.AccountId == right.AccountId && left.Region == right.Region &&
        left.ServerId == right.ServerId &&
        GameplayRoomTypes.Normalize(left.RoomType) == GameplayRoomTypes.Normalize(right.RoomType) &&
        left.Title == right.Title && left.IsPublic == right.IsPublic &&
        left.MaxPlayers == right.MaxPlayers && SameTags(left.Tags, right.Tags);

    private static bool SameSummary(RoomSummary summary, ContractCreateRoomRequest request, string roomType) =>
        summary.OwnerAccountId == request.AccountId && summary.Region == request.Region &&
        summary.ServerId == request.ServerId && summary.RoomType == roomType &&
        summary.Title == (request.Title ?? string.Empty) && summary.IsPublic == request.IsPublic &&
        summary.MaxPlayers == request.MaxPlayers && SameTags(summary.Tags, request.Tags);

    private static bool IsActiveForCreator(RoomPersistentState state, string accountId) =>
        state.Phase is not (RoomPhase.Closing or RoomPhase.Closed or RoomPhase.Expired) &&
        state.Members.Any(member => member.AccountId == accountId);

    private static bool SameTags(IReadOnlyDictionary<string, string>? left, IReadOnlyDictionary<string, string>? right)
    {
        if (left is null || left.Count == 0) return right is null || right.Count == 0;
        if (right is null || left.Count != right.Count) return false;
        foreach (var item in left)
            if (!right.TryGetValue(item.Key, out var value) || value != item.Value) return false;
        return true;
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
            var roomType = GameplayRoomTypes.Normalize(request.RoomType);
            query = query.Where(r => string.Equals(
                GameplayRoomTypes.Normalize(r.RoomType),
                roomType,
                StringComparison.OrdinalIgnoreCase));
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
