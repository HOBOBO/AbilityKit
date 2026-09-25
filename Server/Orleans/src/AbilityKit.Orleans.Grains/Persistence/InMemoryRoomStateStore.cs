using System.Collections.Concurrent;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Orleans.Grains.Persistence;

public sealed class InMemoryRoomStateStore : IRoomStateStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, RoomSummary>> _roomsByDirectory = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ulong> _roomToNumericId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<ulong, string> _numericIdToRoom = new();
    private readonly ConcurrentDictionary<string, string> _accountToRoom = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RoomPersistentState> _runtimeStates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string DirectoryKey, string AccountId, string CommandId), RoomCreateCommandState> _createCommands = new();
    private readonly object _roomLifecycleSync = new();

    public Task<RoomCreateCommandState?> TryGetCreateCommandAsync(string directoryKey, string accountId, string commandId, CancellationToken cancellationToken = default)
    {
        _createCommands.TryGetValue((directoryKey, accountId, commandId), out var command);
        return Task.FromResult(command is null ? null : CloneCreateCommand(command));
    }

    public Task<RoomCreateCommandState> RecordCreateCommandAsync(string directoryKey, string accountId, string commandId, RoomCreateCommandState command, CancellationToken cancellationToken = default)
    {
        var recorded = _createCommands.GetOrAdd((directoryKey, accountId, commandId),
            _ => CloneCreateCommand(command));
        return Task.FromResult(CloneCreateCommand(recorded));
    }

    private static RoomCreateCommandState CloneCreateCommand(RoomCreateCommandState command) =>
        new(command.RoomId, command.Request with
        {
            Tags = command.Request.Tags is null
                ? null : new Dictionary<string, string>(command.Request.Tags, StringComparer.Ordinal)
        });

    public Task UpsertRoomAsync(string directoryKey, RoomSummary summary, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryKey))
        {
            throw new ArgumentException("Directory key is required.", nameof(directoryKey));
        }

        if (summary is null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        RegisterNumericRoomId(summary.RoomId);
        var rooms = _roomsByDirectory.GetOrAdd(directoryKey, _ => new ConcurrentDictionary<string, RoomSummary>(StringComparer.Ordinal));
        rooms[summary.RoomId] = CloneSummary(summary);
        return Task.CompletedTask;
    }

    public Task<bool> TryPublishActiveRoomAsync(string directoryKey, RoomSummary summary, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryKey)) throw new ArgumentException("Directory key is required.", nameof(directoryKey));
        if (summary is null) throw new ArgumentNullException(nameof(summary));
        lock (_roomLifecycleSync)
        {
            if (!_runtimeStates.TryGetValue(summary.RoomId, out var state) ||
                state.DirectoryKey != directoryKey || !IsActiveMember(state, summary.OwnerAccountId))
                return Task.FromResult(false);

            RegisterNumericRoomId(summary.RoomId);
            var rooms = _roomsByDirectory.GetOrAdd(directoryKey,
                _ => new ConcurrentDictionary<string, RoomSummary>(StringComparer.Ordinal));
            rooms[summary.RoomId] = CloneSummary(state.Summary);
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyCollection<RoomSummary>> ListRoomsAsync(string directoryKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryKey) || !_roomsByDirectory.TryGetValue(directoryKey, out var rooms))
        {
            return Task.FromResult<IReadOnlyCollection<RoomSummary>>(Array.Empty<RoomSummary>());
        }

        IReadOnlyCollection<RoomSummary> result = rooms.Values.Select(CloneSummary).ToArray();
        return Task.FromResult(result);
    }

    public Task UpdateRoomPlayerCountAsync(string directoryKey, string roomId, int playerCount, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryKey) || string.IsNullOrWhiteSpace(roomId))
        {
            return Task.CompletedTask;
        }

        if (_roomsByDirectory.TryGetValue(directoryKey, out var rooms) && rooms.TryGetValue(roomId, out var summary))
        {
            rooms[roomId] = summary with { PlayerCount = Math.Max(0, playerCount) };
        }

        return Task.CompletedTask;
    }

    public Task RemoveRoomAsync(string directoryKey, string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryKey) || string.IsNullOrWhiteSpace(roomId))
        {
            return Task.CompletedTask;
        }

        if (_roomsByDirectory.TryGetValue(directoryKey, out var rooms))
        {
            rooms.TryRemove(roomId, out _);
        }

        return Task.CompletedTask;
    }

    public Task<ulong> GetOrCreateNumericRoomIdAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new ArgumentException("Room id is required.", nameof(roomId));
        }

        return Task.FromResult(RegisterNumericRoomId(roomId));
    }

    public Task<string?> TryGetRoomIdAsync(ulong numericRoomId, CancellationToken cancellationToken = default)
    {
        if (numericRoomId == 0)
        {
            return Task.FromResult<string?>(null);
        }

        _numericIdToRoom.TryGetValue(numericRoomId, out var roomId);
        return Task.FromResult(roomId);
    }

    public Task BindAccountRoomAsync(string accountId, string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new ArgumentException("Account id is required.", nameof(accountId));
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new ArgumentException("Room id is required.", nameof(roomId));
        }

        _accountToRoom[accountId] = roomId;
        return Task.CompletedTask;
    }

    public Task<bool> TryBindAccountRoomIfActiveAsync(string accountId, string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account id is required.", nameof(accountId));
        if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("Room id is required.", nameof(roomId));
        lock (_roomLifecycleSync)
        {
            if (!_runtimeStates.TryGetValue(roomId, out var state) || !IsActiveMember(state, accountId))
                return Task.FromResult(false);
            _accountToRoom[accountId] = roomId;
            return Task.FromResult(true);
        }
    }

    public Task<string?> TryGetAccountRoomAsync(string accountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Task.FromResult<string?>(null);
        }

        _accountToRoom.TryGetValue(accountId, out var roomId);
        return Task.FromResult(roomId);
    }

    public Task ClearAccountRoomAsync(string accountId, string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(roomId))
        {
            return Task.CompletedTask;
        }

        if (_accountToRoom.TryGetValue(accountId, out var currentRoomId) && string.Equals(currentRoomId, roomId, StringComparison.Ordinal))
        {
            _accountToRoom.TryRemove(accountId, out _);
        }

        return Task.CompletedTask;
    }

    public Task<RoomPersistentState?> TryGetRuntimeStateAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId) || !_runtimeStates.TryGetValue(roomId, out var state))
        {
            return Task.FromResult<RoomPersistentState?>(null);
        }

        return Task.FromResult<RoomPersistentState?>(CloneRuntimeState(state));
    }

    public Task WriteRuntimeStateAsync(string roomId, RoomPersistentState state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("Room id is required.", nameof(roomId));
        if (state is null) throw new ArgumentNullException(nameof(state));
        lock (_roomLifecycleSync)
        {
            _runtimeStates[roomId] = CloneRuntimeState(state);
            if (state.Phase is RoomPhase.Closing or RoomPhase.Closed or RoomPhase.Expired &&
                _roomsByDirectory.TryGetValue(state.DirectoryKey, out var rooms))
                rooms.TryRemove(roomId, out _);
        }
        return Task.CompletedTask;
    }

    public Task RemoveRuntimeStateAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(roomId))
        {
            lock (_roomLifecycleSync)
            {
                if (_runtimeStates.TryRemove(roomId, out var state) &&
                    _roomsByDirectory.TryGetValue(state.DirectoryKey, out var rooms))
                    rooms.TryRemove(roomId, out _);
            }
        }
        return Task.CompletedTask;
    }

    private static bool IsActiveMember(RoomPersistentState state, string accountId) =>
        state.Phase is not (RoomPhase.Closing or RoomPhase.Closed or RoomPhase.Expired) &&
        state.Members.Any(member => member.AccountId == accountId);

    private ulong RegisterNumericRoomId(string roomId)
    {
        var numericRoomId = RoomGatewayIds.CreateNumericRoomId(roomId);
        var mappedRoomId = _numericIdToRoom.GetOrAdd(numericRoomId, roomId);
        if (!string.Equals(mappedRoomId, roomId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Numeric room id collision. numericRoomId={numericRoomId}, existingRoomId={mappedRoomId}, requestedRoomId={roomId}");
        }

        _roomToNumericId[roomId] = numericRoomId;
        return numericRoomId;
    }

    private static RoomPersistentState CloneRuntimeState(RoomPersistentState state)
    {
        return state with
        {
            Summary = CloneSummary(state.Summary),
            Members = state.Members.Select(member => member with { State = member.State with { } }).ToList(),
            GameplayState = state.GameplayState with { Payload = (byte[])state.GameplayState.Payload.Clone() },
            Launch = state.Launch with { LockedRoster = new List<string>(state.Launch.LockedRoster) },
            BattleCommit = state.BattleCommit with { },
            CommandDedupEntries = state.CommandDedupEntries.Select(entry => entry with { }).ToList()
        };
    }

    private static RoomSummary CloneSummary(RoomSummary summary)
    {
        return summary with
        {
            Tags = summary.Tags is null ? null : new Dictionary<string, string>(summary.Tags, StringComparer.Ordinal)
        };
    }
}
