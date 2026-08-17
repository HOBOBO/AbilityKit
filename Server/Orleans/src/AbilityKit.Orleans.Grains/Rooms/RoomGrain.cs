using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Grains.Rooms.Gameplay;
using AbilityKit.Protocol.Room;
using Orleans;
using ContractJoinRoomMemberRequest = AbilityKit.Orleans.Contracts.Rooms.JoinRoomMemberRequest;
using ContractJoinRoomResponse = AbilityKit.Orleans.Contracts.Rooms.JoinRoomResponse;

namespace AbilityKit.Orleans.Grains.Rooms;

public sealed class RoomGrain : Grain, IRoomGrain
{
    private const long DefaultLoadingTimeoutMs = 120_000L; // 2 鍒嗛挓
    private const long DefaultOfflineGraceMs = 60_000L; // 1 鍒嗛挓
    private const int DefaultBattleCommitMaxAttempts = 3;
    private static readonly TimeSpan AbandonedRoomCleanupRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AbandonedRoomCleanupKeepAliveBuffer = TimeSpan.FromMinutes(1);

    private static readonly RoomGameplayRegistry GameplayRegistry = new();

    private readonly IRoomStateStore _roomStateStore;
    private RoomPersistentState? _persistentState;
    private RoomSummary? _summary;
    private string? _directoryKey;
    private IRoomGameplayAdapter? _gameplay;
    private object? _gameplayState;
    private readonly RoomMemberTracker _members = new();
    private readonly Dictionary<string, RoomStatePushBinding> _statePushBindings =
        new(StringComparer.Ordinal);
    private bool _closed;
    private string? _battleId;
    private ulong _worldId;
    private WorldStartAnchor? _worldStartAnchor;
    private IDisposable? _abandonedRoomCleanupTimer;
    private bool _timersEnabled;

    public RoomGrain(IRoomStateStore roomStateStore)
    {
        _roomStateStore = roomStateStore ?? throw new ArgumentNullException(nameof(roomStateStore));
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var restored = await _roomStateStore.TryGetRuntimeStateAsync(this.GetPrimaryKeyString(), cancellationToken);
        if (restored is not null)
        {
            RestoreActivation(restored);
        }

        await base.OnActivateAsync(cancellationToken);
        _timersEnabled = true;
        RefreshAbandonedRoomCleanupTimer();
    }

    public override Task OnDeactivateAsync(
        DeactivationReason reason,
        CancellationToken cancellationToken)
    {
        _timersEnabled = false;
        CancelAbandonedRoomCleanupTimer();
        return base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task InitializeAsync(RoomSummary summary, string directoryKey)
    {
        if (_persistentState is not null)
        {
            return;
        }

        var gameplay = GameplayRegistry.Resolve(summary.RoomType);
        var gameplayState = gameplay.CreateState(summary);
        var nowTicks = DateTime.UtcNow.Ticks;
        var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var members = new List<RoomPersistentMember>
        {
            new(summary.OwnerAccountId, new RoomMemberState(true, nowTicks, 0, false, 1))
        };
        var initializedSummary = summary with { PlayerCount = members.Count };
        gameplay.Join(gameplayState, initializedSummary, new[] { summary.OwnerAccountId }, summary.OwnerAccountId);
        var state = new RoomPersistentState(
            RoomPersistentState.CurrentSchemaVersion,
            initializedSummary,
            directoryKey,
            RoomPhase.Lobby,
            string.Empty,
            members,
            2,
            gameplay.ExportPersistentState(gameplayState),
            0,
            0,
            new RoomLaunchPersistentState(0, 0, 0, null, new List<string>()),
            new RoomBattleCommitPersistentState(0, null, RoomBattleCommitStatus.None, null, null, 0, null, 0, null),
            new List<RoomCommandDedupEntry>(),
            null,
            nowUnixMs);
        await _roomStateStore.WriteRuntimeStateAsync(summary.RoomId, state);
        RestoreActivation(state);
    }

    public Task<RoomSnapshot> GetSnapshotAsync()
    {
        var state = RequirePersistentState();
        var gameplay = RequireGameplay();
        var gameplayState = RequireGameplayState();
        var memberStates = state.Members.ToDictionary(member => member.AccountId, member => member.State, StringComparer.Ordinal);
        var players = gameplay.BuildPlayerSnapshots(gameplayState);
        for (var index = 0; index < players.Count; index++)
        {
            var player = players[index];
            if (memberStates.TryGetValue(player.AccountId, out var memberState))
            {
                players[index] = player with
                {
                    Ready = memberState.LobbyReady,
                    LobbyReady = memberState.LobbyReady,
                    AssetsLoaded = memberState.AssetsLoaded,
                    LoadingProgress = memberState.LoadingProgress,
                    IsOnline = memberState.IsOnline,
                    JoinOrdinal = memberState.JoinOrdinal,
                    LoadedManifestVersion = memberState.LoadedManifestVersion,
                    LoadedManifestHash = memberState.LoadedManifestHash,
                    LastSeenTicks = memberState.LastSeenTicks,
                    OfflineSinceTicks = memberState.OfflineSinceTicks
                };
            }
        }
        players.Sort((left, right) => left.JoinOrdinal.CompareTo(right.JoinOrdinal));

        return Task.FromResult(new RoomSnapshot(
            state.Summary with { PlayerCount = state.Members.Count },
            state.Members.OrderBy(member => member.State.JoinOrdinal).Select(member => member.AccountId).ToList(),
            players,
            gameplay.CanStart(gameplayState),
            state.BattleCommit.BattleId,
            state.BattleCommit.WorldStartAnchor,
            state.BattleCommit.WorldId,
            memberStates.Count == 0 ? null : memberStates,
            state.SchemaVersion,
            state.Revision,
            state.EventSequence,
            state.Phase,
            state.PhaseReason,
            state.Launch.Generation,
            state.Launch.DeadlineUnixMs,
            state.Launch.ManifestHash,
            state.Launch.ManifestVersion,
            state.BattleCommit.LastError,
            state.BattleCommit.SyncCapabilities));
    }

    public async Task BindStatePushObserverAsync(
        string accountId,
        string bindingId,
        IRoomStateGatewayPushObserver observer)
    {
        EnsureAccountId(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
        ArgumentNullException.ThrowIfNull(observer);

        var state = RequirePersistentState();
        if (!state.Members.Any(member =>
                string.Equals(member.AccountId, accountId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Only a room member can bind room state pushes.");
        }

        _statePushBindings[accountId] = new RoomStatePushBinding(bindingId, observer);
        var snapshot = await GetSnapshotAsync();
        var payload = RoomStatePushBuilder.BuildRoomStateChangedPayload(
            snapshot,
            DateTime.UtcNow.Ticks);
        observer.OnPush(RoomGatewayOpCodes.RoomStateChanged, payload);
    }

    public Task UnbindStatePushObserverAsync(string accountId, string bindingId)
    {
        EnsureAccountId(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
        if (_statePushBindings.TryGetValue(accountId, out var current) &&
            string.Equals(current.BindingId, bindingId, StringComparison.Ordinal))
        {
            _statePushBindings.Remove(accountId);
        }

        return Task.CompletedTask;
    }

    public Task<RoomRuntimeState> GetRuntimeStateAsync()
    {
        var state = RequirePersistentState();
        return Task.FromResult(new RoomRuntimeState(
            state.Summary.RoomId,
            state.Summary.RoomType,
            state.BattleCommit.BattleId,
            state.BattleCommit.WorldId,
            state.Phase is RoomPhase.Closing or RoomPhase.Closed or RoomPhase.Expired,
            state.Phase == RoomPhase.InBattle,
            state.Members.OrderBy(member => member.State.JoinOrdinal).Select(member => member.AccountId).ToList(),
            state.Members.ToDictionary(member => member.AccountId, member => member.State, StringComparer.Ordinal),
            DateTime.UtcNow.Ticks,
            state.Summary.Tags == null ? null : new Dictionary<string, string>(state.Summary.Tags)));
    }

    public Task<ContractJoinRoomResponse> JoinAsync(string accountId)
    {
        return JoinMemberAsync(new ContractJoinRoomMemberRequest(accountId));
    }

    public async Task<ContractJoinRoomResponse> JoinMemberAsync(ContractJoinRoomMemberRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        EnsureAccountId(request.AccountId);

        var state = RequirePersistentState();
        var alreadyMember = state.Members.Any(member => string.Equals(member.AccountId, request.AccountId, StringComparison.Ordinal));
        if (state.Phase == RoomPhase.InBattle)
        {
            return await JoinExistingBattleAsync(state, request, alreadyMember);
        }

        var nowTicks = DateTime.UtcNow.Ticks;
        var transition = RoomStateMachine.Join(state, request.AccountId, request.IsBot, nowTicks, NowUnixMs());
        ThrowIfRejected(transition.Result);
        if (transition.Applied)
        {
            var next = transition.State;
            if (!alreadyMember)
            {
                var gameplay = RequireGameplay();
                var gameplayState = gameplay.RestorePersistentState(state.Summary, state.GameplayState);
                gameplay.Join(
                    gameplayState,
                    next.Summary,
                    next.Members.OrderBy(member => member.State.JoinOrdinal).Select(member => member.AccountId).ToList(),
                    request.AccountId);
                next = next with { GameplayState = gameplay.ExportPersistentState(gameplayState) };
            }

            await PersistAndRestoreAsync(next);
            await NotifyRoomChangedAsync();
        }

        return new ContractJoinRoomResponse(
            await GetSnapshotAsync(),
            alreadyMember ? RoomJoinKind.Reconnect : RoomJoinKind.TeamLobby,
            DateTime.UtcNow.Ticks);
    }

    public async Task<RestoreRoomResponse> RestoreAsync(string accountId)
    {
        EnsureAccountId(accountId);
        var state = RequirePersistentState();
        var member = state.Members.FirstOrDefault(candidate => string.Equals(candidate.AccountId, accountId, StringComparison.Ordinal));
        if (member is null)
        {
            return RestoreRoomResponse.Failed(RoomRestoreStatus.NotMember, RoomRestoreErrorCode.AccountNotInRoom, "Account is not in room.");
        }

        var transition = RoomStateMachine.Reconnect(state, accountId, member.State.IsBot, DateTime.UtcNow.Ticks, NowUnixMs());
        if (transition.Applied)
        {
            await PersistAndRestoreAsync(transition.State);
            await NotifyRoomChangedAsync();
        }

        var snapshot = await GetSnapshotAsync();
        var isInBattle = state.Phase == RoomPhase.InBattle;
        return RestoreRoomResponse.Active(
            snapshot,
            isInBattle ? RoomJoinKind.Reconnect : RoomJoinKind.TeamLobby,
            isInBattle,
            DateTime.UtcNow.Ticks);
    }

    public async Task MarkOfflineAsync(string accountId)
    {
        var result = await MarkOfflineWithResultAsync(accountId);
        ThrowIfRejected(result);
    }

    public async Task<RoomOperationResult> MarkOfflineWithResultAsync(string accountId)
    {
        EnsureAccountId(accountId);
        var transition = RoomStateMachine.MarkOffline(
            RequirePersistentState(),
            accountId,
            DateTime.UtcNow.Ticks,
            NowUnixMs());
        if (transition.Applied)
        {
            await PersistAndRestoreAsync(transition.State);
            await NotifyRoomChangedAsync();
        }

        return transition.Result;
    }

    public async Task LeaveAsync(string accountId)
    {
        var result = await LeaveWithResultAsync(accountId);
        ThrowIfRejected(result);
    }

    public async Task<RoomOperationResult> LeaveWithResultAsync(string accountId)
    {
        EnsureAccountId(accountId);
        var state = RequirePersistentState();
        var transition = RoomStateMachine.Leave(state, accountId, NowUnixMs());
        if (!transition.Applied)
        {
            return transition.Result;
        }

        var gameplay = RequireGameplay();
        var gameplayState = gameplay.RestorePersistentState(state.Summary, state.GameplayState);
        gameplay.Leave(gameplayState, accountId);
        var next = transition.State with { GameplayState = gameplay.ExportPersistentState(gameplayState) };
        await PersistAndRestoreAsync(next);
        await ClearAccountRoomMappingAsync(accountId, state.Summary.RoomId);
        await NotifyRoomChangedAsync();

        if (next.Phase == RoomPhase.Closing)
        {
            await RemoveFromDirectoryAsync();
            DeactivateOnIdle();
        }

        return transition.Result;
    }

    public async Task SetReadyAsync(RoomReadyRequest request)
    {
        var result = await SetLobbyReadyWithResultAsync(request);
        ThrowIfRejected(result);
    }

    public async Task<RoomOperationResult> SetLobbyReadyWithResultAsync(RoomReadyRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        EnsureAccountId(request.AccountId);
        var state = RequirePersistentState();
        var transition = RoomStateMachine.SetLobbyReady(
            state,
            request.AccountId,
            request.Ready,
            DateTime.UtcNow.Ticks,
            NowUnixMs());
        if (!transition.Applied)
        {
            return transition.Result;
        }

        var gameplay = RequireGameplay();
        var gameplayState = gameplay.RestorePersistentState(state.Summary, state.GameplayState);
        gameplay.SetReady(gameplayState, request);
        var next = transition.State with { GameplayState = gameplay.ExportPersistentState(gameplayState) };
        await PersistAndRestoreAsync(next);
        await NotifyRoomChangedAsync();
        return transition.Result;
    }

    public async Task SubmitGameplayCommandAsync(RoomGameplayCommandRequest request)
    {
        var result = await SubmitGameplayCommandWithResultAsync(request);
        ThrowIfRejected(result);
    }

    public async Task<RoomOperationResult> SubmitGameplayCommandWithResultAsync(
        RoomGameplayCommandRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        EnsureAccountId(request.AccountId);
        var state = RequirePersistentState();
        var transition = RoomStateMachine.GameplayChanged(
            state,
            request.AccountId,
            DateTime.UtcNow.Ticks,
            NowUnixMs());
        if (!transition.Applied)
        {
            return transition.Result;
        }

        var gameplay = RequireGameplay();
        var gameplayState = gameplay.RestorePersistentState(state.Summary, state.GameplayState);
        var commandResult = gameplay.SubmitCommand(gameplayState, request);
        if (!commandResult.Success)
        {
            return RoomOperationResult.Rejected(
                commandResult.ErrorCode,
                commandResult.Message,
                state.Revision);
        }

        var next = transition.State with { GameplayState = gameplay.ExportPersistentState(gameplayState) };
        await PersistAndRestoreAsync(next);
        await NotifyRoomChangedAsync();
        return transition.Result;
    }

    public async Task<StartRoomBattleResponse> StartBattleAsync(StartRoomBattleRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var summary = RequireSummary();
        var gameplay = RequireGameplay();
        var gameplayState = RequireGameplayState();
        EnsureOwner(request.AccountId, summary);

        if (!string.IsNullOrEmpty(_battleId))
        {
            return new StartRoomBattleResponse(
                _battleId,
                _worldId,
                true,
                _worldStartAnchor,
                DateTime.UtcNow.Ticks,
                RequirePersistentState().BattleCommit.SyncCapabilities);
        }

        EnsureOpen();
        if (!gameplay.CanStart(gameplayState))
        {
            throw new InvalidOperationException("Room is not ready to start.");
        }

        var state = RequirePersistentState();
        if (state.Phase != RoomPhase.Starting)
        {
            var generation = Math.Max(state.Launch.Generation + 1, 1);
            var starting = state with
            {
                Phase = RoomPhase.Starting,
                PhaseReason = "LegacyStartBattle",
                Launch = state.Launch with { Generation = generation },
                BattleCommit = state.BattleCommit with
                {
                    Generation = generation,
                    Status = RoomBattleCommitStatus.Pending,
                    LastError = null
                },
                Revision = state.Revision + 1,
                EventSequence = state.EventSequence + 1,
                UpdatedAtUnixMs = NowUnixMs()
            };
            await PersistAndRestoreAsync(starting);
            state = starting;
        }

        await CommitBattleAsync(state, request);
        var committed = RequirePersistentState();
        if (committed.Phase != RoomPhase.InBattle)
        {
            var lastError = committed.BattleCommit.LastError ?? "Battle commit failed.";
            throw new InvalidOperationException($"Legacy start battle failed: {lastError}");
        }

        return new StartRoomBattleResponse(
            _battleId,
            _worldId,
            true,
            _worldStartAnchor,
            DateTime.UtcNow.Ticks,
            committed.BattleCommit.SyncCapabilities);
    }

    public async Task CloseAsync(string accountId)
    {
        var state = RequirePersistentState();
        var summary = state.Summary;
        var gameplay = RequireGameplay();
        EnsureOwner(accountId, summary);

        if (state.Phase is RoomPhase.Closed or RoomPhase.Expired)
        {
            return;
        }

        var removedMembers = state.Members.Select(member => member.AccountId).ToList();
        var gameplayState = gameplay.CreateState(summary);
        var closed = state with
        {
            Phase = RoomPhase.Closed,
            PhaseReason = "OwnerClosed",
            Members = new List<RoomPersistentMember>(),
            Summary = summary with { PlayerCount = 0 },
            GameplayState = gameplay.ExportPersistentState(gameplayState),
            TerminalReason = "OwnerClosed",
            Revision = state.Revision + 1,
            EventSequence = state.EventSequence + 1,
            UpdatedAtUnixMs = NowUnixMs()
        };
        await PersistAndRestoreAsync(closed);
        await ClearAccountRoomMappingsAsync(removedMembers, summary.RoomId);
        await NotifyRoomChangedAsync();
        await RemoveFromDirectoryAsync();
        DeactivateOnIdle();
    }

    public async Task<RoomOperationResult> BeginLoadingWithResultAsync(BeginLoadingRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        EnsureAccountId(request.AccountId);

        var state = RequirePersistentState();
        var nowTicks = DateTime.UtcNow.Ticks;
        var nowUnixMs = NowUnixMs();

        var dedup = RoomCommandDedup.Find(state.CommandDedupEntries, request.AccountId, request.CommandId);
        if (dedup is not null)
        {
            return ToDedupResult(dedup);
        }

        var gameplay = RequireGameplay();
        var gameplayState = RequireGameplayState();
        if (!gameplay.ValidateBeginLoading(gameplayState))
        {
            var rejected = RoomOperationResult.Rejected(
                RoomOperationErrorCode.InvalidOperation,
                "Room gameplay is not ready to begin loading.",
                state.Revision);
            await RecordDedupAsync(state, request.AccountId, request.CommandId, "BeginLoading", rejected, nowUnixMs);
            return rejected;
        }

        var manifest = gameplay.BuildLaunchManifest(gameplayState, state.Summary);
        var transition = RoomStateMachine.BeginLoading(
            state,
            request.AccountId,
            request.ExpectedRevision,
            manifest.ManifestVersion,
            manifest.ManifestHash,
            nowTicks,
            nowUnixMs,
            DefaultLoadingTimeoutMs);

        if (!transition.Result.Success)
        {
            await RecordDedupAsync(state, request.AccountId, request.CommandId, "BeginLoading", transition.Result, nowUnixMs);
            return transition.Result;
        }

        var next = transition.State;
        if (transition.Applied)
        {
            next = next with { CommandDedupEntries = RoomCommandDedup.Record(next.CommandDedupEntries, request.AccountId, request.CommandId, "BeginLoading", transition.Result, nowUnixMs) };
            await PersistAndRestoreAsync(next);
            await NotifyRoomChangedAsync();
        }

        return transition.Result;
    }

    public async Task<RoomOperationResult> ReportAssetsLoadedWithResultAsync(ReportAssetsLoadedRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        EnsureAccountId(request.AccountId);

        var state = RequirePersistentState();
        var nowTicks = DateTime.UtcNow.Ticks;
        var nowUnixMs = NowUnixMs();

        var dedup = RoomCommandDedup.Find(state.CommandDedupEntries, request.AccountId, request.CommandId);
        if (dedup is not null)
        {
            return ToDedupResult(dedup);
        }

        var transition = RoomStateMachine.ReportAssetsLoaded(
            state,
            request.AccountId,
            request.LaunchGeneration,
            request.ManifestVersion,
            request.ManifestHash,
            nowTicks,
            nowUnixMs);

        if (!transition.Result.Success)
        {
            await RecordDedupAsync(state, request.AccountId, request.CommandId, "ReportAssetsLoaded", transition.Result, nowUnixMs);
            return transition.Result;
        }

        var next = transition.State;
        if (transition.Applied)
        {
            await PersistAndRestoreAsync(next);
            await NotifyRoomChangedAsync();
        }

        var result = transition.Result;
        if (next.Phase == RoomPhase.Starting && next.BattleCommit.Status == RoomBattleCommitStatus.Pending)
        {
            await CommitBattleAsync(next);
            result = ResolveReportAssetsLoadedCommitResult(RequirePersistentState());
        }

        await RecordDedupAsync(
            RequirePersistentState(),
            request.AccountId,
            request.CommandId,
            "ReportAssetsLoaded",
            result,
            NowUnixMs());
        return result;
    }

    internal static RoomOperationResult ResolveReportAssetsLoadedCommitResult(RoomPersistentState state)
    {
        if (state.Phase == RoomPhase.InBattle &&
            state.BattleCommit.Status == RoomBattleCommitStatus.Committed)
        {
            return RoomOperationResult.AppliedAt(state.Revision);
        }

        var detail = string.IsNullOrWhiteSpace(state.BattleCommit.LastError)
            ? "Battle commit did not reach InBattle."
            : state.BattleCommit.LastError;
        return RoomOperationResult.Rejected(
            RoomOperationErrorCode.InvalidOperation,
            $"Battle commit failed: {detail}",
            state.Revision);
    }

    public async Task<RoomOperationResult> ReportLoadingProgressWithResultAsync(ReportLoadingProgressRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        EnsureAccountId(request.AccountId);

        var state = RequirePersistentState();
        var transition = RoomStateMachine.ReportLoadingProgress(
            state,
            request.AccountId,
            request.LaunchGeneration,
            request.ManifestVersion,
            request.ManifestHash,
            request.Progress,
            DateTime.UtcNow.Ticks,
            NowUnixMs());

        if (transition.Applied)
        {
            await PersistAndRestoreAsync(transition.State);
            await NotifyRoomChangedAsync();
        }

        return transition.Result;
    }

    public async Task<RoomOperationResult> CancelLoadingWithResultAsync(CancelLoadingRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        EnsureAccountId(request.AccountId);

        var state = RequirePersistentState();
        var nowUnixMs = NowUnixMs();

        var dedup = RoomCommandDedup.Find(state.CommandDedupEntries, request.AccountId, request.CommandId);
        if (dedup is not null)
        {
            return ToDedupResult(dedup);
        }

        var transition = RoomStateMachine.CancelLoading(
            state,
            request.AccountId,
            request.ExpectedRevision,
            nowUnixMs);

        if (!transition.Result.Success)
        {
            await RecordDedupAsync(state, request.AccountId, request.CommandId, "CancelLoading", transition.Result, nowUnixMs);
            return transition.Result;
        }

        var next = transition.State;
        if (transition.Applied)
        {
            next = next with { CommandDedupEntries = RoomCommandDedup.Record(next.CommandDedupEntries, request.AccountId, request.CommandId, "CancelLoading", transition.Result, nowUnixMs) };
            await PersistAndRestoreAsync(next);
            await NotifyRoomChangedAsync();
        }

        return transition.Result;
    }

    public async Task<RoomOperationResult> TickAsync(RoomTickRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var state = RequirePersistentState();
        if (AbandonedRoomCleanupPolicy.ShouldCleanup(state, request.NowTicks))
        {
            var cleanupRevision = state.Revision + 1;
            await CleanupAbandonedRoomAsync(request.NowTicks);
            return RoomOperationResult.AppliedAt(cleanupRevision);
        }

        var loadingTimeoutMs = request.LoadingTimeoutMs > 0 ? request.LoadingTimeoutMs : DefaultLoadingTimeoutMs;
        var offlineGraceMs = request.OfflineGraceMs >= 0 ? request.OfflineGraceMs : DefaultOfflineGraceMs;

        var transition = RoomStateMachine.Tick(state, request.NowTicks, request.NowUnixMs, offlineGraceMs);
        if (!transition.Applied)
        {
            return transition.Result;
        }

        var next = transition.State;
        // 鑻?Tick 瑙﹀彂浜嗙绾挎垚鍛樻竻鐞嗭紝闇€瑕佸悓姝?gameplay state銆?
        if (!ReferenceEquals(next.Members, state.Members))
        {
            var gameplay = RequireGameplay();
            var gameplayState = gameplay.RestorePersistentState(state.Summary, state.GameplayState);
            var removed = state.Members
                .Where(member => !next.Members.Any(m => string.Equals(m.AccountId, member.AccountId, StringComparison.Ordinal)))
                .Select(member => member.AccountId)
                .ToList();
            foreach (var accountId in removed)
            {
                gameplay.Leave(gameplayState, accountId);
            }
            next = next with { GameplayState = gameplay.ExportPersistentState(gameplayState) };
            await ClearAccountRoomMappingsAsync(removed, state.Summary.RoomId);
        }

        await PersistAndRestoreAsync(next);
        await NotifyRoomChangedAsync();

        if (next.Phase == RoomPhase.Starting && next.BattleCommit.Status == RoomBattleCommitStatus.Pending)
        {
            await CommitBattleAsync(next);
        }

        return transition.Result;
    }

    private async Task CommitBattleAsync(RoomPersistentState state, StartRoomBattleRequest? request = null)
    {
        if (state.Phase != RoomPhase.Starting)
        {
            return;
        }

        if (state.BattleCommit.Status == RoomBattleCommitStatus.Committed && !string.IsNullOrEmpty(state.BattleCommit.BattleId))
        {
            return;
        }

        var summary = RequireSummary();
        var gameplay = RequireGameplay();
        var gameplayState = RequireGameplayState();
        var nowUnixMs = NowUnixMs();

        var commitId = !string.IsNullOrEmpty(state.BattleCommit.CommitId)
            ? state.BattleCommit.CommitId!
            : summary.RoomId + ":" + state.Launch.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var effectiveRequest = request ?? BuildFallbackStartRequest(summary);
        var initParams = gameplay.BuildBattleInitParams(gameplayState, summary, effectiveRequest);
        initParams.SyncOptions = RoomBattleSyncOptionsMapper.Resolve(summary, effectiveRequest);

        var initSpecHash = RoomBattleInitSpecHasher.Compute(initParams);

        var prepareTransition = RoomStateMachine.PrepareCommit(state, commitId, initSpecHash, nowUnixMs);
        if (!prepareTransition.Result.Success)
        {
            var rollbackForPrepare = RoomStateMachine.RollbackBattleCommit(state, prepareTransition.Result.Message, nowUnixMs, DefaultBattleCommitMaxAttempts);
            await PersistAndRestoreAsync(rollbackForPrepare.State);
            await NotifyRoomChangedAsync();
            return;
        }

        var prepared = prepareTransition.Applied ? prepareTransition.State : state;
        if (prepareTransition.Applied)
        {
            await PersistAndRestoreAsync(prepared);
        }

        await ExecuteCommitAsync(prepared, commitId, summary.RoomId, initParams.WorldId, initParams, initSpecHash);
    }
    private async Task ExecuteCommitAsync(
        RoomPersistentState state,
        string commitId,
        string battleId,
        ulong worldId,
        BattleInitParams initParams,
        string initSpecHash)
    {
        var summary = RequireSummary();
        var nowUnixMs = NowUnixMs();
        var startRoute = RoomFrameSyncRoute.ResolveStartRoute(summary, battleId, initParams);
        if (startRoute.FrameSyncOptions is { } frameSyncOptions)
        {
            var frameSyncGrain = GrainFactory.GetGrain<IBattleFrameSyncGrain>(frameSyncOptions.RoomId.ToString());
            await frameSyncGrain.InitializeAsync(frameSyncOptions);
        }

        WorldStartAnchor? worldStartAnchor;
        if (startRoute.RequiresBattleRuntime)
        {
            worldStartAnchor = await InitializeBattleRuntimeAsync(state, battleId, initParams, initSpecHash, nowUnixMs);
            if (worldStartAnchor is null)
            {
                return;
            }
        }
        else
        {
            worldStartAnchor = CreateWorldStartAnchor(initParams.TickRate);
            initParams.WorldStartAnchor = worldStartAnchor;
        }

        var syncCapabilities = RoomNetworkSyncCapabilityResolver.Resolve(summary, initParams, startRoute.SyncTemplateId);
        var commitTransition = RoomStateMachine.CommitBattleStarted(
            state,
            commitId,
            battleId,
            worldId,
            worldStartAnchor,
            initSpecHash,
            nowUnixMs,
            syncCapabilities);
        if (!commitTransition.Result.Success)
        {
            await RollbackCommitAsync(state, commitTransition.Result.Message, nowUnixMs);
            return;
        }

        _closed = true;
        await PersistAndRestoreAsync(commitTransition.State);
        await NotifyRoomChangedAsync();
    }

    private async Task<WorldStartAnchor?> InitializeBattleRuntimeAsync(
        RoomPersistentState state,
        string battleId,
        BattleInitParams initParams,
        string initSpecHash,
        long nowUnixMs)
    {
        var battleGrain = GrainFactory.GetGrain<IBattleLogicHostGrain>(battleId);
        BattleInitResult initResult;
        try
        {
            initResult = await battleGrain.InitializeBattleWithResultAsync(initParams, initSpecHash);
        }
        catch (Exception ex)
        {
            await RollbackCommitAsync(state, "BattleInitException:" + ex.Message, nowUnixMs);
            return null;
        }

        if (!initResult.Succeeded)
        {
            if (string.Equals(initResult.Error, "InitSpecHashMismatch", System.StringComparison.Ordinal))
            {
                await RollbackCommitAsync(state, initResult.Error ?? "InitSpecHashMismatch", nowUnixMs, forceMax: true);
                return null;
            }

            await RollbackCommitAsync(state, initResult.Error ?? "BattleInitFailed", nowUnixMs);
            return null;
        }

        return initResult.WorldStartAnchor ?? await battleGrain.GetWorldStartAnchorAsync();
    }

    private async Task RollbackCommitAsync(RoomPersistentState state, string error, long nowUnixMs, bool forceMax = false)
    {
        var maxAttempts = forceMax ? 1 : DefaultBattleCommitMaxAttempts;
        var effectiveState = RequirePersistentState();
        var rollback = RoomStateMachine.RollbackBattleCommit(effectiveState, error, nowUnixMs, maxAttempts);
        if (rollback.Applied)
        {
            await PersistAndRestoreAsync(rollback.State);
            await NotifyRoomChangedAsync();
        }
    }

    private static StartRoomBattleRequest BuildFallbackStartRequest(RoomSummary summary)
    {
        var tags = summary.Tags;
        return new StartRoomBattleRequest(
            summary.OwnerAccountId,
            ReadIntTag(tags, RoomTagKeys.GameplayId, 0),
            ReadIntTag(tags, RoomTagKeys.RuleSetId, 0),
            ReadIntTag(tags, RoomTagKeys.ConfigVersion, 0),
            ReadIntTag(tags, RoomTagKeys.ProtocolVersion, 0),
            ReadTag(tags, RoomTagKeys.WorldType),
            ReadTag(tags, RoomTagKeys.ClientId));
    }

    private static string? ReadTag(Dictionary<string, string>? tags, string key)
    {
        return tags != null && tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static int ReadIntTag(Dictionary<string, string>? tags, string key, int fallback)
    {
        return tags != null && tags.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }


    private async Task RecordDedupAsync(
        RoomPersistentState state,
        string accountId,
        string? commandId,
        string commandName,
        RoomOperationResult result,
        long nowUnixMs)
    {
        if (string.IsNullOrEmpty(commandId))
        {
            return;
        }

        var next = state with
        {
            CommandDedupEntries = RoomCommandDedup.Record(state.CommandDedupEntries, accountId, commandId, commandName, result, nowUnixMs)
        };
        await PersistAndRestoreAsync(next);
    }

    private static RoomOperationResult ToDedupResult(RoomCommandDedupEntry entry)
    {
        if (entry.Success)
        {
            return entry.Applied
                ? RoomOperationResult.AppliedAt(entry.AppliedRevision)
                : RoomOperationResult.NoChange(entry.AppliedRevision, "Deduplicated command.");
        }

        return RoomOperationResult.Rejected(entry.ErrorCode, "Deduplicated command.", entry.AppliedRevision);
    }

    private RoomSummary RequireSummary()
    {
        if (_summary is null)
        {
            throw new InvalidOperationException("Room not initialized.");
        }

        return _summary;
    }

    private IRoomGameplayAdapter RequireGameplay()
    {
        if (_gameplay is null)
        {
            throw new InvalidOperationException("Room gameplay adapter not initialized.");
        }

        return _gameplay;
    }

    private object RequireGameplayState()
    {
        if (_gameplayState is null)
        {
            throw new InvalidOperationException("Room gameplay state not initialized.");
        }

        return _gameplayState;
    }

    private void EnsureOpen()
    {
        if (!GetLifecycleSnapshot().IsOpenForLobbyActions)
        {
            throw new InvalidOperationException("Room is closed.");
        }
    }

    private RoomLifecycleSnapshot GetLifecycleSnapshot()
    {
        var state = RequirePersistentState();
        return RoomLifecyclePolicy.Evaluate(state.Phase, state.BattleCommit.BattleId);
    }

    private void EnsureMember(string accountId)
    {
        EnsureAccountId(accountId);
        if (!_members.Contains(accountId))
        {
            throw new InvalidOperationException("Account is not in room.");
        }
    }

    private static WorldStartAnchor CreateWorldStartAnchor(int tickRate)
    {
        var resolvedTickRate = tickRate > 0 ? tickRate : 30;
        return new WorldStartAnchor(
            DateTime.UtcNow.Ticks,
            TimeSpan.TicksPerSecond,
            0,
            1.0 / resolvedTickRate);
    }

    private static void EnsureOwner(string accountId, RoomSummary summary)
    {
        EnsureAccountId(accountId);
        if (!string.Equals(accountId, summary.OwnerAccountId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only owner can operate the room.");
        }
    }

    private static void EnsureAccountId(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new ArgumentException("accountId is required", nameof(accountId));
        }
    }

    private async Task<ContractJoinRoomResponse> JoinExistingBattleAsync(
        RoomPersistentState state,
        ContractJoinRoomMemberRequest request,
        bool alreadyMember)
    {
        if (alreadyMember)
        {
            var member = state.Members.First(candidate => string.Equals(candidate.AccountId, request.AccountId, StringComparison.Ordinal));
            var transition = RoomStateMachine.Reconnect(state, request.AccountId, member.State.IsBot, DateTime.UtcNow.Ticks, NowUnixMs());
            if (transition.Applied)
            {
                await PersistAndRestoreAsync(transition.State);
            }

            return new ContractJoinRoomResponse(await GetSnapshotAsync(), RoomJoinKind.Reconnect, DateTime.UtcNow.Ticks);
        }

        if (state.Summary.MaxPlayers > 0 && state.Members.Count >= state.Summary.MaxPlayers)
        {
            throw new InvalidOperationException("Room is full.");
        }

        var joinOrdinal = Math.Max(1, state.NextJoinOrdinal);
        var members = state.Members.Select(member => member with { State = member.State with { } }).ToList();
        members.Add(new RoomPersistentMember(
            request.AccountId,
            new RoomMemberState(true, DateTime.UtcNow.Ticks, 0, request.IsBot, joinOrdinal)));
        var gameplay = RequireGameplay();
        var gameplayState = gameplay.RestorePersistentState(state.Summary, state.GameplayState);
        gameplay.Join(gameplayState, state.Summary, members.Select(member => member.AccountId).ToList(), request.AccountId);
        await JoinRunningBattleAsync(gameplay, gameplayState, state.Summary, request);
        var next = state with
        {
            Members = members,
            Summary = state.Summary with { PlayerCount = members.Count },
            NextJoinOrdinal = joinOrdinal + 1,
            GameplayState = gameplay.ExportPersistentState(gameplayState),
            Revision = state.Revision + 1,
            EventSequence = state.EventSequence + 1,
            UpdatedAtUnixMs = NowUnixMs()
        };
        await PersistAndRestoreAsync(next);
        await NotifyRoomChangedAsync();
        return new ContractJoinRoomResponse(await GetSnapshotAsync(), RoomJoinKind.LateJoin, DateTime.UtcNow.Ticks);
    }

    private async Task JoinRunningBattleAsync(
        IRoomGameplayAdapter gameplay,
        object gameplayState,
        RoomSummary summary,
        ContractJoinRoomMemberRequest request)
    {
        if (string.IsNullOrWhiteSpace(_battleId))
        {
            return;
        }

        var player = gameplay.BuildLateJoinPlayer(gameplayState, summary, request.AccountId);
        if (player is null)
        {
            return;
        }

        var battleGrain = GrainFactory.GetGrain<IBattleLogicHostGrain>(_battleId);
        var joinResult = await battleGrain.JoinPlayerAsync(new BattlePlayerJoinRequest(_worldId, player, request.IsBot));
        if (!joinResult.Accepted)
        {
            gameplay.Leave(gameplayState, request.AccountId);
            throw new InvalidOperationException($"Battle late join rejected. Status={joinResult.Status}, Message={joinResult.Message}");
        }
    }

    private async Task NotifyRoomChangedAsync()
    {
        var summary = RequireSummary();
        if (_directoryKey is null)
        {
            throw new InvalidOperationException("Room directory not initialized.");
        }

        await CleanupExpiredOfflineMembersAsync(summary);

        var directory = GrainFactory.GetGrain<IRoomDirectoryGrain>(_directoryKey);
        await directory.NotifyRoomChangedAsync(summary.RoomId, _members.Count);

        // 阶段 4：向所有在线成员推送 RoomStateChanged（fire-and-forget，失败不影响主流程）。
        await PushRoomStateChangedToOnlineMembersAsync();
    }

    /// <summary>
    /// 向所有在线成员推送 RoomStateChanged 事件。
    /// push 失败被静默吞掉（fire-and-forget 语义），保证 Room 主流程不受推送通道影响。
    /// </summary>
    private async Task PushRoomStateChangedToOnlineMembersAsync()
    {
        try
        {
            RoomSnapshot snapshot;
            try
            {
                snapshot = await GetSnapshotAsync();
            }
            catch
            {
                // Room 尚未初始化完成或处于不可快照状态，跳过推送。
                return;
            }

            var onlineAccounts = RoomStatePushBuilder.CollectOnlineAccountIds(snapshot);
            if (onlineAccounts.Count == 0)
            {
                return;
            }

            var payload = RoomStatePushBuilder.BuildRoomStateChangedPayload(snapshot, DateTime.UtcNow.Ticks);

            foreach (var accountId in onlineAccounts)
            {
                try
                {
                    if (_statePushBindings.TryGetValue(accountId, out var binding))
                    {
                        binding.Observer.OnPush(RoomGatewayOpCodes.RoomStateChanged, payload);
                        continue;
                    }

                    var pushTarget = GrainFactory.GetGrain<IGatewayPushTargetGrain>(accountId);
                    await pushTarget.PushToAccountAsync(accountId, RoomGatewayOpCodes.RoomStateChanged, payload);
                }
                catch
                {
                    // 单个成员 push 失败不影响其他成员与主流程。
                }
            }
        }
        catch
        {
            // push 整体失败不影响 Room 主流程。
        }
    }

    private sealed record RoomStatePushBinding(
        string BindingId,
        IRoomStateGatewayPushObserver Observer);

    private async Task RemoveFromDirectoryAsync()
    {
        var summary = RequireSummary();
        if (_directoryKey is null)
        {
            throw new InvalidOperationException("Room directory not initialized.");
        }

        var directory = GrainFactory.GetGrain<IRoomDirectoryGrain>(_directoryKey);
        await directory.RemoveRoomAsync(summary.RoomId);
    }

    private async Task CleanupExpiredOfflineMembersAsync(RoomSummary summary)
    {
        if (!_members.HasMemberStates)
        {
            return;
        }

        var expired = _members.CollectExpiredOfflineMembers(summary, DateTime.UtcNow.Ticks);
        if (expired.Count == 0)
        {
            return;
        }

        var state = RequirePersistentState();
        if (state.Phase is not (RoomPhase.Lobby or RoomPhase.Loading))
        {
            return;
        }

        var gameplay = RequireGameplay();
        var gameplayState = gameplay.RestorePersistentState(state.Summary, state.GameplayState);
        var next = state;
        foreach (var accountId in expired)
        {
            var transition = RoomStateMachine.Leave(next, accountId, NowUnixMs());
            if (!transition.Applied)
            {
                continue;
            }

            gameplay.Leave(gameplayState, accountId);
            next = transition.State;
        }

        next = next with { GameplayState = gameplay.ExportPersistentState(gameplayState) };
        await PersistAndRestoreAsync(next);
        await ClearAccountRoomMappingsAsync(expired, summary.RoomId);
    }

    internal IReadOnlyList<string> CollectExpiredOfflineMembersForTests(RoomSummary summary, long nowTicks)
    {
        return _members.CollectExpiredOfflineMembers(summary, nowTicks);
    }

    private RoomPersistentState RequirePersistentState()
    {
        if (_persistentState is null)
        {
            throw new InvalidOperationException("Room not initialized.");
        }

        return _persistentState;
    }

    private void RestoreActivation(RoomPersistentState state)
    {
        if (state.SchemaVersion != RoomPersistentState.CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported room state schema version: {state.SchemaVersion}.");
        }

        var gameplay = GameplayRegistry.Resolve(state.Summary.RoomType);
        var gameplayState = gameplay.RestorePersistentState(state.Summary, state.GameplayState);
        _persistentState = state;
        _summary = state.Summary;
        _directoryKey = state.DirectoryKey;
        _gameplay = gameplay;
        _gameplayState = gameplayState;
        _closed = state.Phase is RoomPhase.InBattle or RoomPhase.Closing or RoomPhase.Closed or RoomPhase.Expired;
        _battleId = state.BattleCommit.BattleId;
        _worldId = state.BattleCommit.WorldId;
        _worldStartAnchor = state.BattleCommit.WorldStartAnchor;
        _members.Restore(state.Members.Select(member =>
            new KeyValuePair<string, RoomMemberState>(member.AccountId, member.State)));
    }

    private async Task PersistAndRestoreAsync(RoomPersistentState state)
    {
        await _roomStateStore.WriteRuntimeStateAsync(state.Summary.RoomId, state);
        RestoreActivation(state);
        RefreshAbandonedRoomCleanupTimer();
    }

    private void RefreshAbandonedRoomCleanupTimer(TimeSpan? minimumDelay = null)
    {
        CancelAbandonedRoomCleanupTimer();
        if (!_timersEnabled || _persistentState is null ||
            !AbandonedRoomCleanupPolicy.TryGetCleanupDeadlineTicks(
                _persistentState,
                out var cleanupDeadlineTicks))
        {
            return;
        }

        var remainingTicks = Math.Max(0L, cleanupDeadlineTicks - DateTime.UtcNow.Ticks);
        var dueTime = TimeSpan.FromTicks(remainingTicks);
        if (minimumDelay.HasValue && dueTime < minimumDelay.Value)
        {
            dueTime = minimumDelay.Value;
        }

        var keepAliveDuration = dueTime > TimeSpan.MaxValue - AbandonedRoomCleanupKeepAliveBuffer
            ? TimeSpan.MaxValue
            : dueTime + AbandonedRoomCleanupKeepAliveBuffer;
        DelayDeactivation(keepAliveDuration);
        _abandonedRoomCleanupTimer = RegisterTimer(
            _ => OnAbandonedRoomCleanupTimerAsync(),
            state: null,
            dueTime,
            Timeout.InfiniteTimeSpan);
    }

    private void CancelAbandonedRoomCleanupTimer()
    {
        _abandonedRoomCleanupTimer?.Dispose();
        _abandonedRoomCleanupTimer = null;
        if (_timersEnabled)
        {
            DelayDeactivation(TimeSpan.FromMilliseconds(-1));
        }
    }

    private async Task OnAbandonedRoomCleanupTimerAsync()
    {
        CancelAbandonedRoomCleanupTimer();
        try
        {
            await CleanupAbandonedRoomAsync(DateTime.UtcNow.Ticks);
        }
        catch
        {
            RefreshAbandonedRoomCleanupTimer(AbandonedRoomCleanupRetryDelay);
            throw;
        }
    }

    private async Task CleanupAbandonedRoomAsync(long nowTicks)
    {
        var state = RequirePersistentState();
        if (!AbandonedRoomCleanupPolicy.ShouldCleanup(state, nowTicks))
        {
            RefreshAbandonedRoomCleanupTimer();
            return;
        }

        var roomId = state.Summary.RoomId;
        var removedMembers = state.Members.Select(member => member.AccountId).ToList();

        if (!string.IsNullOrWhiteSpace(state.BattleCommit.BattleId))
        {
            var battle = GrainFactory.GetGrain<IBattleLogicHostGrain>(state.BattleCommit.BattleId);
            await battle.DestroyAsync();
        }

        if (state.BattleCommit.WorldId != 0UL)
        {
            var frameSync = GrainFactory.GetGrain<IBattleFrameSyncGrain>(
                state.BattleCommit.WorldId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await frameSync.DestroyAsync();
        }

        await ClearAccountRoomMappingsAsync(removedMembers, roomId);
        await RemoveFromDirectoryAsync();
        await _roomStateStore.RemoveRuntimeStateAsync(roomId);

        var gameplay = RequireGameplay();
        var expired = state with
        {
            Phase = RoomPhase.Expired,
            PhaseReason = "AllClientsDisconnectedTimeout",
            Members = new List<RoomPersistentMember>(),
            Summary = state.Summary with { PlayerCount = 0 },
            GameplayState = gameplay.ExportPersistentState(gameplay.CreateState(state.Summary)),
            TerminalReason = "AllClientsDisconnectedTimeout",
            Revision = state.Revision + 1,
            EventSequence = state.EventSequence + 1,
            UpdatedAtUnixMs = NowUnixMs()
        };
        RestoreActivation(expired);
        _statePushBindings.Clear();
        DeactivateOnIdle();
    }

    private static long NowUnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static void ThrowIfRejected(RoomOperationResult result)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException($"Room operation rejected. Code={result.ErrorCode}, Message={result.Message}");
        }
    }

    private Task ClearAccountRoomMappingsAsync(IReadOnlyCollection<string> accountIds, string roomId)
    {
        if (accountIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var mapping = GrainFactory.GetGrain<IRoomIdMappingGrain>("global");
        return Task.WhenAll(accountIds.Select(accountId => mapping.ClearAccountRoomAsync(accountId, roomId)));
    }

    private Task ClearAccountRoomMappingAsync(string accountId, string roomId)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(roomId))
        {
            return Task.CompletedTask;
        }

        var mapping = GrainFactory.GetGrain<IRoomIdMappingGrain>("global");
        return mapping.ClearAccountRoomAsync(accountId, roomId);
    }

}
