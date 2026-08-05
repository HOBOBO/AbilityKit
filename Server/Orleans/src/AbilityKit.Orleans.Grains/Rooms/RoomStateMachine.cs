using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;

namespace AbilityKit.Orleans.Grains.Rooms;

internal sealed record RoomTransitionResult(RoomPersistentState State, RoomOperationResult Result)
{
    public bool Applied => Result.Applied;
}

internal static class RoomStateMachine
{
    public static RoomTransitionResult Join(RoomPersistentState state, string accountId, bool isBot, long nowTicks, long nowUnixMs)
    {
        if (state.Phase != RoomPhase.Lobby)
        {
            return Reject(state, RoomOperationErrorCode.InvalidPhase, $"Join is not allowed in phase {state.Phase}.");
        }

        var existingIndex = FindMemberIndex(state.Members, accountId);
        if (existingIndex >= 0)
        {
            var existing = state.Members[existingIndex];
            if (existing.State.IsOnline && existing.State.IsBot == isBot)
            {
                return NoChange(state);
            }

            var members = CloneMembers(state.Members);
            members[existingIndex] = existing with
            {
                State = existing.State with
                {
                    IsOnline = true,
                    LastSeenTicks = nowTicks,
                    OfflineSinceTicks = 0,
                    IsBot = isBot
                }
            };
            var reactivatedOwner = ResolveOwner(members, state.Summary.OwnerAccountId);
            return Apply(state with
            {
                Members = members,
                Summary = state.Summary with { OwnerAccountId = reactivatedOwner }
            }, nowUnixMs);
        }

        if (state.Summary.MaxPlayers > 0 && state.Members.Count >= state.Summary.MaxPlayers)
        {
            return Reject(state, RoomOperationErrorCode.InvalidOperation, "Room is full.");
        }

        var joinOrdinal = Math.Max(1, state.NextJoinOrdinal);
        var joinedMembers = CloneMembers(state.Members);
        joinedMembers.Add(new RoomPersistentMember(
            accountId,
            new RoomMemberState(true, nowTicks, 0, isBot, joinOrdinal)));
        var ownerAccountId = ResolveOwner(joinedMembers, state.Summary.OwnerAccountId);
        var next = state with
        {
            Summary = state.Summary with { OwnerAccountId = ownerAccountId, PlayerCount = joinedMembers.Count },
            Members = joinedMembers,
            NextJoinOrdinal = joinOrdinal + 1
        };
        return Apply(next, nowUnixMs);
    }

    public static RoomTransitionResult Reconnect(RoomPersistentState state, string accountId, bool isBot, long nowTicks, long nowUnixMs)
    {
        var memberIndex = FindMemberIndex(state.Members, accountId);
        if (memberIndex < 0)
        {
            return Reject(state, RoomOperationErrorCode.NotMember, "Account is not in room.");
        }

        var member = state.Members[memberIndex];
        if (member.State.IsOnline && member.State.IsBot == isBot)
        {
            return NoChange(state);
        }

        var members = CloneMembers(state.Members);
        members[memberIndex] = member with
        {
            State = member.State with
            {
                IsOnline = true,
                LastSeenTicks = nowTicks,
                OfflineSinceTicks = 0,
                IsBot = isBot
            }
        };
        return Apply(state with { Members = members }, nowUnixMs);
    }

    public static RoomTransitionResult MarkOffline(RoomPersistentState state, string accountId, long nowTicks, long nowUnixMs)
    {
        var memberIndex = FindMemberIndex(state.Members, accountId);
        if (memberIndex < 0)
        {
            return Reject(state, RoomOperationErrorCode.NotMember, "Account is not in room.");
        }

        var member = state.Members[memberIndex];
        if (!member.State.IsOnline)
        {
            return NoChange(state);
        }

        var members = CloneMembers(state.Members);
        members[memberIndex] = member with
        {
            State = member.State with { IsOnline = false, LastSeenTicks = nowTicks, OfflineSinceTicks = nowTicks }
        };
        var offlineOwner = ResolveOwner(members, state.Summary.OwnerAccountId);
        return Apply(state with
        {
            Members = members,
            Summary = state.Summary with { OwnerAccountId = offlineOwner }
        }, nowUnixMs);
    }

    public static RoomTransitionResult SetLobbyReady(RoomPersistentState state, string accountId, bool ready, long nowTicks, long nowUnixMs)
    {
        if (state.Phase != RoomPhase.Lobby)
        {
            return Reject(state, RoomOperationErrorCode.InvalidPhase, $"Lobby ready is not allowed in phase {state.Phase}.");
        }

        var memberIndex = FindMemberIndex(state.Members, accountId);
        if (memberIndex < 0)
        {
            return Reject(state, RoomOperationErrorCode.NotMember, "Account is not in room.");
        }

        var member = state.Members[memberIndex];
        if (member.State.LobbyReady == ready && member.State.IsOnline)
        {
            return NoChange(state);
        }

        var members = CloneMembers(state.Members);
        members[memberIndex] = member with
        {
            State = member.State with
            {
                LobbyReady = ready,
                IsOnline = true,
                LastSeenTicks = nowTicks,
                OfflineSinceTicks = 0
            }
        };
        return Apply(state with { Members = members }, nowUnixMs);
    }

    public static RoomTransitionResult GameplayChanged(RoomPersistentState state, string accountId, long nowTicks, long nowUnixMs)
    {
        if (state.Phase != RoomPhase.Lobby)
        {
            return Reject(state, RoomOperationErrorCode.InvalidPhase, $"Gameplay configuration is not allowed in phase {state.Phase}.");
        }

        var memberIndex = FindMemberIndex(state.Members, accountId);
        if (memberIndex < 0)
        {
            return Reject(state, RoomOperationErrorCode.NotMember, "Account is not in room.");
        }

        var member = state.Members[memberIndex];
        var members = CloneMembers(state.Members);
        members[memberIndex] = member with
        {
            State = member.State with
            {
                LobbyReady = false,
                IsOnline = true,
                LastSeenTicks = nowTicks,
                OfflineSinceTicks = 0
            }
        };
        return Apply(state with { Members = members }, nowUnixMs);
    }

    public static RoomTransitionResult BeginLoading(
        RoomPersistentState state,
        string accountId,
        long? expectedRevision,
        int manifestVersion,
        string? manifestHash,
        long nowTicks,
        long nowUnixMs,
        long loadingTimeoutMs)
    {
        if (state.Phase != RoomPhase.Lobby)
        {
            return Reject(state, RoomOperationErrorCode.InvalidPhase, $"Begin loading is not allowed in phase {state.Phase}.");
        }

        if (!string.Equals(accountId, state.Summary.OwnerAccountId, StringComparison.Ordinal))
        {
            return Reject(state, RoomOperationErrorCode.NotOwner, "Only owner can begin loading.");
        }

        if (expectedRevision.HasValue && expectedRevision.Value != state.Revision)
        {
            return Reject(state, RoomOperationErrorCode.RevisionConflict, $"Expected revision {expectedRevision.Value} but current is {state.Revision}.");
        }

        if (state.Members.Count == 0)
        {
            return Reject(state, RoomOperationErrorCode.InvalidOperation, "Cannot begin loading with no members.");
        }

        if (state.Members.Any(member => !member.State.IsOnline))
        {
            return Reject(state, RoomOperationErrorCode.InvalidOperation, "All roster members must be online to begin loading.");
        }

        var lockedRoster = state.Members
            .OrderBy(member => member.State.JoinOrdinal)
            .ThenBy(member => member.AccountId, StringComparer.Ordinal)
            .Select(member => member.AccountId)
            .ToList();

        var clearedMembers = ClearAssetsLoaded(state.Members);
        var generation = Math.Max(state.Launch.Generation + 1, 1);
        var deadline = loadingTimeoutMs > 0 ? nowUnixMs + loadingTimeoutMs : 0;
        var launch = new RoomLaunchPersistentState(generation, deadline, manifestVersion, manifestHash, lockedRoster);
        var next = state with
        {
            Members = clearedMembers,
            Phase = RoomPhase.Loading,
            PhaseReason = "BeginLoading",
            Launch = launch
        };
        return Apply(next, nowUnixMs);
    }

    public static RoomTransitionResult ReportAssetsLoaded(
        RoomPersistentState state,
        string accountId,
        long generation,
        int manifestVersion,
        string? manifestHash,
        long nowTicks,
        long nowUnixMs)
    {
        if (generation != state.Launch.Generation)
        {
            return NoChange(state, "Stale launch generation ignored.");
        }

        if (state.Phase is not (RoomPhase.Loading or RoomPhase.Starting))
        {
            return Reject(state, RoomOperationErrorCode.InvalidPhase, $"Assets loaded is not allowed in phase {state.Phase}.");
        }

        if (manifestVersion != state.Launch.ManifestVersion ||
            !string.Equals(manifestHash, state.Launch.ManifestHash, StringComparison.Ordinal))
        {
            return Reject(state, RoomOperationErrorCode.InvalidOperation, "Loaded manifest does not match the active launch manifest.");
        }

        if (!state.Launch.LockedRoster.Contains(accountId, StringComparer.Ordinal))
        {
            return Reject(state, RoomOperationErrorCode.NotMember, "Account is not in the locked roster.");
        }

        var memberIndex = FindMemberIndex(state.Members, accountId);
        if (memberIndex < 0)
        {
            return Reject(state, RoomOperationErrorCode.NotMember, "Account is not in room.");
        }

        var member = state.Members[memberIndex];
        var alreadyLoaded = member.State.AssetsLoaded &&
            member.State.LoadedManifestVersion == manifestVersion &&
            string.Equals(member.State.LoadedManifestHash, manifestHash, StringComparison.Ordinal);

        // Starting 之后再次 report：幂等返回，绝不二次触发。
        if (state.Phase == RoomPhase.Starting)
        {
            return NoChange(state, alreadyLoaded ? "Already starting." : "Room already starting.");
        }

        if (alreadyLoaded)
        {
            return NoChange(state);
        }

        var members = CloneMembers(state.Members);
        members[memberIndex] = member with
        {
            State = member.State with
            {
                AssetsLoaded = true,
                LoadingProgress = 100,
                LoadedManifestVersion = manifestVersion,
                LoadedManifestHash = manifestHash,
                LastSeenTicks = nowTicks,
                IsOnline = true,
                OfflineSinceTicks = 0
            }
        };

        var allLoaded = state.Launch.LockedRoster.All(rosterAccountId =>
        {
            var index = FindMemberIndex(members, rosterAccountId);
            return index >= 0
                && members[index].State.AssetsLoaded
                && members[index].State.LoadingProgress == 100
                && members[index].State.LoadedManifestVersion == state.Launch.ManifestVersion
                && string.Equals(
                    members[index].State.LoadedManifestHash,
                    state.Launch.ManifestHash,
                    StringComparison.Ordinal);
        });

        if (!allLoaded)
        {
            return Apply(state with { Members = members }, nowUnixMs);
        }

        var starting = state with
        {
            Members = members,
            Phase = RoomPhase.Starting,
            PhaseReason = "AllAssetsLoaded",
            BattleCommit = state.BattleCommit with
            {
                Generation = state.Launch.Generation,
                Status = RoomBattleCommitStatus.Pending,
                LastError = null
            }
        };
        return Apply(starting, nowUnixMs);
    }

    public static RoomTransitionResult ReportLoadingProgress(
        RoomPersistentState state,
        string accountId,
        long generation,
        int manifestVersion,
        string? manifestHash,
        int progress,
        long nowTicks,
        long nowUnixMs)
    {
        if (progress is < 0 or > 100)
        {
            return Reject(state, RoomOperationErrorCode.InvalidOperation, "Loading progress must be between 0 and 100.");
        }

        if (generation != state.Launch.Generation)
        {
            return NoChange(state, "Stale launch generation ignored.");
        }

        if (state.Phase != RoomPhase.Loading)
        {
            return Reject(state, RoomOperationErrorCode.InvalidPhase, $"Loading progress is not allowed in phase {state.Phase}.");
        }

        if (manifestVersion != state.Launch.ManifestVersion ||
            !string.Equals(manifestHash, state.Launch.ManifestHash, StringComparison.Ordinal))
        {
            return Reject(state, RoomOperationErrorCode.InvalidOperation, "Loading progress manifest does not match the active launch manifest.");
        }

        if (!state.Launch.LockedRoster.Contains(accountId, StringComparer.Ordinal))
        {
            return Reject(state, RoomOperationErrorCode.NotMember, "Account is not in the locked roster.");
        }

        var memberIndex = FindMemberIndex(state.Members, accountId);
        if (memberIndex < 0)
        {
            return Reject(state, RoomOperationErrorCode.NotMember, "Account is not in room.");
        }

        var member = state.Members[memberIndex];
        if (progress <= member.State.LoadingProgress)
        {
            return NoChange(state, progress == member.State.LoadingProgress
                ? "Loading progress is unchanged."
                : "Regressive loading progress ignored.");
        }

        if (progress < 100 && progress - member.State.LoadingProgress < 2)
        {
            return NoChange(state, "Loading progress delta is below the server reporting threshold.");
        }

        var members = CloneMembers(state.Members);
        members[memberIndex] = member with
        {
            State = member.State with
            {
                LoadingProgress = progress,
                LastSeenTicks = nowTicks,
                IsOnline = true,
                OfflineSinceTicks = 0
            }
        };
        return Apply(state with { Members = members }, nowUnixMs);
    }

    public static RoomTransitionResult CancelLoading(
        RoomPersistentState state,
        string accountId,
        long? expectedRevision,
        long nowUnixMs,
        string phaseReason = "CancelLoading")
    {
        if (state.Phase != RoomPhase.Loading)
        {
            return Reject(state, RoomOperationErrorCode.InvalidPhase, $"Cancel loading is not allowed in phase {state.Phase}.");
        }

        if (!string.Equals(accountId, state.Summary.OwnerAccountId, StringComparison.Ordinal))
        {
            return Reject(state, RoomOperationErrorCode.NotOwner, "Only owner can cancel loading.");
        }

        if (expectedRevision.HasValue && expectedRevision.Value != state.Revision)
        {
            return Reject(state, RoomOperationErrorCode.RevisionConflict, $"Expected revision {expectedRevision.Value} but current is {state.Revision}.");
        }

        return ResetToLobby(state, phaseReason, nowUnixMs);
    }

    public static RoomTransitionResult Tick(
        RoomPersistentState state,
        long nowTicks,
        long nowUnixMs,
        long offlineGraceMs)
    {
        if (state.Phase == RoomPhase.Loading &&
            state.Launch.DeadlineUnixMs > 0 &&
            nowUnixMs > state.Launch.DeadlineUnixMs)
        {
            return ResetToLobby(state, "LoadingTimeout", nowUnixMs);
        }

        if (state.Phase is RoomPhase.Lobby or RoomPhase.Loading &&
            offlineGraceMs > 0 &&
            state.Members.Count > 0)
        {
            var expired = state.Members
                .Where(member => !member.State.IsOnline && member.State.OfflineSinceTicks > 0 &&
                                 nowTicks - member.State.OfflineSinceTicks >= offlineGraceMs)
                .Select(member => member.AccountId)
                .ToList();

            if (expired.Count > 0)
            {
                var current = state;
                foreach (var accountId in expired)
                {
                    var transition = Leave(current, accountId, nowUnixMs);
                    if (transition.Applied)
                    {
                        current = transition.State;
                    }
                }

                if (!ReferenceEquals(current, state))
                {
                    return new RoomTransitionResult(current, RoomOperationResult.AppliedAt(current.Revision));
                }
            }
        }

        return NoChange(state);
    }

    public static RoomTransitionResult Leave(RoomPersistentState state, string accountId, long nowUnixMs)
    {
        if (state.Phase is not (RoomPhase.Lobby or RoomPhase.Loading))
        {
            return Reject(state, RoomOperationErrorCode.InvalidPhase, $"Leave is not allowed in phase {state.Phase}.");
        }

        var memberIndex = FindMemberIndex(state.Members, accountId);
        if (memberIndex < 0)
        {
            return NoChange(state);
        }

        var members = CloneMembers(state.Members);
        members.RemoveAt(memberIndex);
        var nextPhase = state.Phase;
        var phaseReason = state.PhaseReason;
        var launch = state.Launch;
        if (state.Phase == RoomPhase.Loading)
        {
            nextPhase = RoomPhase.Lobby;
            phaseReason = "LockedMemberLeft";
            members = ClearAssetsLoaded(members);
            launch = launch with
            {
                DeadlineUnixMs = 0,
                ManifestVersion = 0,
                ManifestHash = null,
                LockedRoster = new List<string>()
            };
        }

        var ownerAccountId = state.Summary.OwnerAccountId;
        if (string.Equals(ownerAccountId, accountId, StringComparison.Ordinal))
        {
            ownerAccountId = SelectOwner(members);
        }

        if (members.Count == 0 || string.IsNullOrEmpty(ownerAccountId))
        {
            nextPhase = RoomPhase.Closing;
            phaseReason = members.Count == 0 ? "RoomEmpty" : "NoOnlineOwnerCandidate";
        }

        var next = state with
        {
            Summary = state.Summary with { OwnerAccountId = ownerAccountId ?? string.Empty, PlayerCount = members.Count },
            Members = members,
            Phase = nextPhase,
            PhaseReason = phaseReason,
            Launch = launch
        };
        return Apply(next, nowUnixMs);
    }

    /// <summary>
    /// 在首次进入 commit 尝试时记录 CommitId 和 InitSpecHash 到 BattleCommit（幂等）。
    /// </summary>
    public static RoomTransitionResult PrepareCommit(
        RoomPersistentState state,
        string commitId,
        string initSpecHash,
        long nowUnixMs)
    {
        if (state.Phase != RoomPhase.Starting)
        {
            return Reject(state, RoomOperationErrorCode.InvalidPhase, $"Prepare commit is not allowed in phase {state.Phase}.");
        }

        var commit = state.BattleCommit;
        var alreadyHasCommitId = !string.IsNullOrEmpty(commit.CommitId);
        var alreadyHasHash = !string.IsNullOrEmpty(commit.InitSpecHash);

        if (alreadyHasCommitId
            && alreadyHasHash
            && string.Equals(commit.CommitId, commitId, System.StringComparison.Ordinal)
            && string.Equals(commit.InitSpecHash, initSpecHash, System.StringComparison.Ordinal))
        {
            return NoChange(state);
        }

        if (alreadyHasCommitId && !string.Equals(commit.CommitId, commitId, System.StringComparison.Ordinal))
        {
            return Reject(state, RoomOperationErrorCode.InvalidOperation, "CommitId already set to a different value.");
        }

        var next = state with
        {
            BattleCommit = commit with
            {
                CommitId = alreadyHasCommitId ? commit.CommitId : commitId,
                InitSpecHash = alreadyHasHash ? commit.InitSpecHash : initSpecHash
            }
        };
        return Apply(next, nowUnixMs);
    }

    public static RoomTransitionResult CommitBattleStarted(
        RoomPersistentState state,
        string commitId,
        string battleId,
        ulong worldId,
        WorldStartAnchor? worldStartAnchor,
        string initSpecHash,
        long nowUnixMs)
    {
        if (state.Phase != RoomPhase.Starting)
        {
            return Reject(state, RoomOperationErrorCode.InvalidPhase, $"Commit battle is not allowed in phase {state.Phase}.");
        }

        if (state.BattleCommit.Generation != state.Launch.Generation)
        {
            return Reject(state, RoomOperationErrorCode.InvalidOperation, "Stale commit generation ignored.");
        }

        var next = state with
        {
            Phase = RoomPhase.InBattle,
            PhaseReason = "BattleCommitted",
            BattleCommit = state.BattleCommit with
            {
                Status = RoomBattleCommitStatus.Committed,
                CommitId = commitId,
                BattleId = battleId,
                WorldId = worldId,
                WorldStartAnchor = worldStartAnchor,
                InitSpecHash = initSpecHash,
                LastError = null
            }
        };
        return Apply(next, nowUnixMs);
    }

    public static RoomTransitionResult RollbackBattleCommit(
        RoomPersistentState state,
        string error,
        long nowUnixMs,
        int maxAttempts)
    {
        if (state.Phase != RoomPhase.Starting)
        {
            return Reject(state, RoomOperationErrorCode.InvalidPhase, $"Rollback commit is not allowed in phase {state.Phase}.");
        }

        var attemptCount = state.BattleCommit.AttemptCount + 1;
        var effectiveMax = maxAttempts > 0 ? maxAttempts : 1;

        if (attemptCount < effectiveMax)
        {
            var retry = state with
            {
                PhaseReason = "CommitRetry",
                BattleCommit = state.BattleCommit with
                {
                    AttemptCount = attemptCount,
                    LastError = error
                }
            };
            return Apply(retry, nowUnixMs);
        }

        var clearedMembers = ClearAssetsLoaded(state.Members);
        var launch = state.Launch with
        {
            DeadlineUnixMs = 0,
            ManifestVersion = 0,
            ManifestHash = null,
            LockedRoster = new List<string>()
        };
        var rolledBack = state with
        {
            Members = clearedMembers,
            Phase = RoomPhase.Lobby,
            PhaseReason = "CommitFailed",
            Launch = launch,
            BattleCommit = state.BattleCommit with
            {
                Status = RoomBattleCommitStatus.Failed,
                BattleId = null,
                WorldId = 0,
                WorldStartAnchor = null,
                CommitId = null,
                InitSpecHash = null,
                AttemptCount = attemptCount,
                LastError = error
            }
        };
        return Apply(rolledBack, nowUnixMs);
    }

    public static bool IsTransitionAllowed(RoomPhase from, RoomPhase to)
    {
        if (from == to)
        {
            return true;
        }

        return (from, to) switch
        {
            (RoomPhase.Lobby, RoomPhase.Loading) => true,
            (RoomPhase.Lobby, RoomPhase.Closing) => true,
            (RoomPhase.Lobby, RoomPhase.Expired) => true,
            (RoomPhase.Loading, RoomPhase.Lobby) => true,
            (RoomPhase.Loading, RoomPhase.Starting) => true,
            (RoomPhase.Loading, RoomPhase.Closing) => true,
            (RoomPhase.Loading, RoomPhase.Expired) => true,
            (RoomPhase.Starting, RoomPhase.Lobby) => true,
            (RoomPhase.Starting, RoomPhase.InBattle) => true,
            (RoomPhase.InBattle, RoomPhase.Closing) => true,
            (RoomPhase.Closing, RoomPhase.Closed) => true,
            _ => false
        };
    }

    private static RoomTransitionResult Apply(RoomPersistentState state, long nowUnixMs)
    {
        var revision = state.Revision + 1;
        var next = state with
        {
            Revision = revision,
            EventSequence = state.EventSequence + 1,
            UpdatedAtUnixMs = nowUnixMs
        };
        return new RoomTransitionResult(next, RoomOperationResult.AppliedAt(revision));
    }

    private static RoomTransitionResult NoChange(RoomPersistentState state, string message = "") =>
        new(state, RoomOperationResult.NoChange(state.Revision, message));

    private static RoomTransitionResult Reject(RoomPersistentState state, RoomOperationErrorCode errorCode, string message) =>
        new(state, RoomOperationResult.Rejected(errorCode, message, state.Revision));

    private static int FindMemberIndex(IReadOnlyList<RoomPersistentMember> members, string accountId)
    {
        for (var index = 0; index < members.Count; index++)
        {
            if (string.Equals(members[index].AccountId, accountId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static List<RoomPersistentMember> CloneMembers(IReadOnlyList<RoomPersistentMember> members) =>
        members.Select(member => member with { State = member.State with { } }).ToList();

    private static List<RoomPersistentMember> ClearAssetsLoaded(IReadOnlyList<RoomPersistentMember> members) =>
        members.Select(member => member with
        {
            State = member.State with
            {
                AssetsLoaded = false,
                LoadingProgress = 0,
                LoadedManifestVersion = 0,
                LoadedManifestHash = null
            }
        }).ToList();

    private static string? SelectOwner(IEnumerable<RoomPersistentMember> members) =>
        members
            .Where(member => member.State.IsOnline)
            .OrderBy(member => member.State.JoinOrdinal)
            .ThenBy(member => member.AccountId, StringComparer.Ordinal)
            .Select(member => member.AccountId)
            .FirstOrDefault();

    /// <summary>
    /// 保证房间永远不会在“有在线成员”时持有一个离线的 Owner：
    /// 当前 Owner 仍是在线成员则保留；若是成员但已离线，则把 Owner 交给最早的在线成员
    /// （无在线成员时保留原 Owner，以便其断线重连后恢复）。
    /// 若 Owner 不是成员（不应出现于生产，仅测试构造），则保持不变。
    /// </summary>
    private static string ResolveOwner(
        IReadOnlyList<RoomPersistentMember> members,
        string currentOwnerAccountId)
    {
        if (string.IsNullOrWhiteSpace(currentOwnerAccountId))
        {
            // Owner 尚未指定：交给最早的在线成员（首个加入者成为 Owner）。
            return SelectOwner(members) ?? string.Empty;
        }

        var ownerIsMember = false;
        for (var index = 0; index < members.Count; index++)
        {
            var member = members[index];
            if (!string.Equals(member.AccountId, currentOwnerAccountId, StringComparison.Ordinal))
            {
                continue;
            }

            ownerIsMember = true;
            if (member.State.IsOnline)
            {
                return currentOwnerAccountId;
            }
        }

        return ownerIsMember
            ? SelectOwner(members) ?? currentOwnerAccountId
            : currentOwnerAccountId;
    }

    private static RoomTransitionResult ResetToLobby(RoomPersistentState state, string phaseReason, long nowUnixMs)
    {
        var clearedMembers = ClearAssetsLoaded(state.Members);
        // generation 不回退：保留递增后的值，但清空 roster/deadline/manifest。
        var launch = state.Launch with
        {
            DeadlineUnixMs = 0,
            ManifestVersion = 0,
            ManifestHash = null,
            LockedRoster = new List<string>()
        };
        var next = state with
        {
            Members = clearedMembers,
            Phase = RoomPhase.Lobby,
            PhaseReason = phaseReason,
            Launch = launch,
            BattleCommit = state.BattleCommit with
            {
                Status = RoomBattleCommitStatus.None,
                LastError = null
            }
        };
        return Apply(next, nowUnixMs);
    }
}
