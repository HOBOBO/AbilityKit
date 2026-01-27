using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Orleans.Contracts.Rooms;
using Orleans;

namespace AbilityKit.Orleans.Grains.Rooms;

public sealed class RoomDirectoryGrain : Grain, IRoomDirectoryGrain
{
    private readonly Dictionary<string, RoomSummary> _rooms = new(StringComparer.Ordinal);

    public async Task<CreateRoomResponse> CreateRoomAsync(CreateRoomRequest request)
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

        _rooms[roomId] = summary;

        return new CreateRoomResponse(roomId);
    }

    public Task<ListRoomsResponse> ListRoomsAsync(ListRoomsRequest request)
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

        IEnumerable<RoomSummary> query = _rooms.Values;
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
        return Task.FromResult(new ListRoomsResponse(rooms, nextOffset));
    }

    public Task NotifyRoomChangedAsync(string roomId, int playerCount)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Task.CompletedTask;
        }

        if (_rooms.TryGetValue(roomId, out var summary))
        {
            _rooms[roomId] = summary with { PlayerCount = Math.Max(0, playerCount) };
        }

        return Task.CompletedTask;
    }

    public Task RemoveRoomAsync(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Task.CompletedTask;
        }

        _rooms.Remove(roomId);
        return Task.CompletedTask;
    }

    public static string BuildDirectoryKey(string region, string serverId) => $"{region}:{serverId}";
}
