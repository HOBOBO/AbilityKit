using System;
using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.Rooms;
using Orleans;

namespace AbilityKit.Orleans.Grains.Rooms;

public sealed class RoomIdMappingGrain : Grain, IRoomIdMappingGrain
{
    private const string StateKey = "global";

    private readonly Dictionary<string, ulong> _roomToNum = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, string> _numToRoom = new();

    private ulong _next = 1;

    public Task<ulong> GetOrCreateNumericIdAsync(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required", nameof(roomId));

        if (_roomToNum.TryGetValue(roomId, out var existing))
        {
            return Task.FromResult(existing);
        }

        var id = _next++;
        _roomToNum[roomId] = id;
        _numToRoom[id] = roomId;
        return Task.FromResult(id);
    }

    public Task<string?> TryGetRoomIdAsync(ulong numericRoomId)
    {
        if (numericRoomId == 0) return Task.FromResult<string?>(null);
        return Task.FromResult(_numToRoom.TryGetValue(numericRoomId, out var roomId) ? roomId : null);
    }
}
