using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Orleans.Contracts.Rooms;
using Orleans;

namespace AbilityKit.Orleans.Grains.Rooms;

public sealed class RoomGrain : Grain, IRoomGrain
{
    private RoomSummary? _summary;
    private string? _directoryKey;
    private readonly HashSet<string> _members = new(StringComparer.Ordinal);
    private bool _closed;

    public Task InitializeAsync(RoomSummary summary, string directoryKey)
    {
        if (_summary is not null)
        {
            return Task.CompletedTask;
        }

        _summary = summary;
        _directoryKey = directoryKey;
        return Task.CompletedTask;
    }

    public Task<RoomSnapshot> GetSnapshotAsync()
    {
        if (_summary is null)
        {
            throw new InvalidOperationException("Room not initialized.");
        }

        return Task.FromResult(new RoomSnapshot(_summary with { PlayerCount = _members.Count }, _members.ToList()));
    }

    public async Task JoinAsync(string accountId)
    {
        if (_summary is null || _directoryKey is null)
        {
            throw new InvalidOperationException("Room not initialized.");
        }

        if (_closed)
        {
            throw new InvalidOperationException("Room is closed.");
        }

        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new ArgumentException("accountId is required", nameof(accountId));
        }

        if (_members.Contains(accountId))
        {
            return;
        }

        if (_members.Count >= _summary.MaxPlayers)
        {
            throw new InvalidOperationException("Room is full.");
        }

        _members.Add(accountId);

        var directory = GrainFactory.GetGrain<IRoomDirectoryGrain>(_directoryKey);
        await directory.NotifyRoomChangedAsync(_summary.RoomId, _members.Count);
    }

    public async Task LeaveAsync(string accountId)
    {
        if (_summary is null || _directoryKey is null)
        {
            throw new InvalidOperationException("Room not initialized.");
        }

        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new ArgumentException("accountId is required", nameof(accountId));
        }

        if (!_members.Remove(accountId))
        {
            return;
        }

        var directory = GrainFactory.GetGrain<IRoomDirectoryGrain>(_directoryKey);
        await directory.NotifyRoomChangedAsync(_summary.RoomId, _members.Count);

        if (_members.Count == 0)
        {
            await directory.RemoveRoomAsync(_summary.RoomId);
            DeactivateOnIdle();
        }
    }

    public async Task CloseAsync(string accountId)
    {
        if (_summary is null || _directoryKey is null)
        {
            throw new InvalidOperationException("Room not initialized.");
        }

        if (!string.Equals(accountId, _summary.OwnerAccountId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only owner can close the room.");
        }

        if (_closed)
        {
            return;
        }

        _closed = true;
        _members.Clear();

        var directory = GrainFactory.GetGrain<IRoomDirectoryGrain>(_directoryKey);
        await directory.NotifyRoomChangedAsync(_summary.RoomId, 0);
        await directory.RemoveRoomAsync(_summary.RoomId);
        DeactivateOnIdle();
    }
}
