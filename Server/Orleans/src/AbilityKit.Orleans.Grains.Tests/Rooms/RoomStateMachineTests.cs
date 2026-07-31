using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Grains.Rooms;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

public sealed class RoomStateMachineTests
{
    private const long NowTicks = 1_000L;
    private const long NowUnixMs = 42L;

    [Fact]
    public void Join_AssignsMonotonicJoinOrdinalAndIncrementsRevision()
    {
        var state = CreateState();

        var first = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);
        var second = RoomStateMachine.Join(first.State, "b", false, NowTicks + 1, NowUnixMs + 1);

        Assert.True(first.Applied);
        Assert.True(second.Applied);
        Assert.Equal(1, first.State.Members[0].State.JoinOrdinal);
        Assert.Equal(2, second.State.Members[1].State.JoinOrdinal);
        Assert.Equal(3, second.State.NextJoinOrdinal);
        Assert.Equal(1, first.State.Revision);
        Assert.Equal(2, second.State.Revision);
        Assert.Equal(2, second.State.EventSequence);
    }

    [Fact]
    public void Join_ReconnectKeepsJoinOrdinalAndTouchesOnline()
    {
        var state = CreateState();
        var joined = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);
        var offline = RoomStateMachine.MarkOffline(joined.State, "a", NowTicks + 10, NowUnixMs + 10);

        var reconnect = RoomStateMachine.Join(offline.State, "a", false, NowTicks + 20, NowUnixMs + 20);

        Assert.True(reconnect.Applied);
        Assert.True(reconnect.State.Members[0].State.IsOnline);
        Assert.Equal(1, reconnect.State.Members[0].State.JoinOrdinal);
    }

    [Fact]
    public void Join_WhenRoomIsFull_ReturnsStructuredRejection()
    {
        var state = CreateState(maxPlayers: 1);
        var first = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);

        var second = RoomStateMachine.Join(first.State, "b", false, NowTicks, NowUnixMs);

        Assert.False(second.Applied);
        Assert.False(second.Result.Success);
        Assert.Equal(RoomOperationErrorCode.InvalidOperation, second.Result.ErrorCode);
        Assert.Single(second.State.Members);
    }

    [Fact]
    public void Leave_OwnerLeaves_MigratesOwnerToEarliestOnlineMemberByJoinOrdinal()
    {
        var state = CreateState(ownerAccountId: string.Empty);
        var afterA = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);
        var afterB = RoomStateMachine.Join(afterA.State, "b", false, NowTicks, NowUnixMs);
        var afterC = RoomStateMachine.Join(afterB.State, "c", false, NowTicks, NowUnixMs);

        var ownerLeaving = RoomStateMachine.Leave(afterC.State, "a", NowUnixMs);

        Assert.True(ownerLeaving.Applied);
        Assert.Equal("b", ownerLeaving.State.Summary.OwnerAccountId);
        Assert.Equal(RoomPhase.Lobby, ownerLeaving.State.Phase);
    }

    [Fact]
    public void Leave_LastMember_TransitionsToClosing()
    {
        var state = CreateState();
        var joined = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);

        var left = RoomStateMachine.Leave(joined.State, "a", NowUnixMs);

        Assert.True(left.Applied);
        Assert.Empty(left.State.Members);
        Assert.Equal(RoomPhase.Closing, left.State.Phase);
    }

    [Fact]
    public void Leave_FromLoading_ClearsAssetsLoadedAndReturnsToLobby()
    {
        var state = CreateLoadingState("a", "b");

        var left = RoomStateMachine.Leave(state, "a", NowUnixMs);

        Assert.True(left.Applied);
        Assert.Equal(RoomPhase.Lobby, left.State.Phase);
        Assert.Equal("LockedMemberLeft", left.State.PhaseReason);
        Assert.All(left.State.Members, member => Assert.False(member.State.AssetsLoaded));
        Assert.Empty(left.State.Launch.LockedRoster);
        Assert.Equal(0, left.State.Launch.DeadlineUnixMs);
    }

    [Fact]
    public void Leave_FromInBattle_ReturnsStructuredInvalidPhase()
    {
        var state = CreateState(phase: RoomPhase.InBattle);
        var joined = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);

        var left = RoomStateMachine.Leave(joined.State, "a", NowUnixMs);

        Assert.False(left.Applied);
        Assert.False(left.Result.Success);
        Assert.Equal(RoomOperationErrorCode.InvalidPhase, left.Result.ErrorCode);
    }

    [Fact]
    public void ReportAssetsLoaded_WithStaleGeneration_ReturnsSuccessWithoutMutation()
    {
        var state = CreateLoadingState("a");
        var beforeRevision = state.Revision;

        var report = RoomStateMachine.ReportAssetsLoaded(
            state,
            "a",
            generation: state.Launch.Generation + 99,
            manifestVersion: 1,
            manifestHash: "hash",
            nowTicks: NowTicks,
            nowUnixMs: NowUnixMs);

        Assert.True(report.Result.Success);
        Assert.False(report.Applied);
        Assert.Equal(beforeRevision, report.State.Revision);
        Assert.False(report.State.Members[0].State.AssetsLoaded);
    }

    [Fact]
    public void ReportAssetsLoaded_WithCurrentGeneration_MarksMemberLoaded()
    {
        var state = CreateLoadingState("a");

        var report = RoomStateMachine.ReportAssetsLoaded(
            state,
            "a",
            generation: state.Launch.Generation,
            manifestVersion: 1,
            manifestHash: "hash",
            nowTicks: NowTicks,
            nowUnixMs: NowUnixMs);

        Assert.True(report.Applied);
        Assert.True(report.State.Members[0].State.AssetsLoaded);
        Assert.Equal(1, report.State.Members[0].State.LoadedManifestVersion);
        Assert.Equal("hash", report.State.Members[0].State.LoadedManifestHash);
    }

    [Fact]
    public void ReportAssetsLoaded_WithMismatchedManifest_IsRejectedWithoutMutation()
    {
        var state = CreateLoadingState("a");

        var report = RoomStateMachine.ReportAssetsLoaded(
            state,
            "a",
            generation: state.Launch.Generation,
            manifestVersion: state.Launch.ManifestVersion + 1,
            manifestHash: "wrong-hash",
            nowTicks: NowTicks,
            nowUnixMs: NowUnixMs);

        Assert.False(report.Result.Success);
        Assert.False(report.Applied);
        Assert.Equal(RoomOperationErrorCode.InvalidOperation, report.Result.ErrorCode);
        Assert.Equal(RoomPhase.Loading, report.State.Phase);
        Assert.False(report.State.Members[0].State.AssetsLoaded);
        Assert.Equal(0, report.State.Members[0].State.LoadingProgress);
    }

    [Fact]
    public void ReportAssetsLoaded_OutsideLoading_ReturnsInvalidPhase()
    {
        // generation 匹配但 phase=Lobby，应拒绝 InvalidPhase。
        var state = CreateState(phase: RoomPhase.Lobby) with
        {
            Launch = new RoomLaunchPersistentState(1, 0, 0, null, new List<string> { "a" })
        };
        var joined = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);

        var report = RoomStateMachine.ReportAssetsLoaded(
            joined.State,
            "a",
            generation: 1,
            manifestVersion: 1,
            manifestHash: "hash",
            nowTicks: NowTicks,
            nowUnixMs: NowUnixMs);

        Assert.False(report.Result.Success);
        Assert.Equal(RoomOperationErrorCode.InvalidPhase, report.Result.ErrorCode);
    }

    [Fact]
    public void ReportAssetsLoaded_NotInLockedRoster_ReturnsNotMember()
    {
        var state = CreateLoadingState("a");

        var report = RoomStateMachine.ReportAssetsLoaded(
            state,
            "intruder",
            generation: state.Launch.Generation,
            manifestVersion: 1,
            manifestHash: "hash",
            nowTicks: NowTicks,
            nowUnixMs: NowUnixMs);

        Assert.False(report.Result.Success);
        Assert.Equal(RoomOperationErrorCode.NotMember, report.Result.ErrorCode);
    }

    [Fact]
    public void ReportAssetsLoaded_DuplicateManifest_IsIdempotent()
    {
        var state = CreateLoadingState("a");
        var first = RoomStateMachine.ReportAssetsLoaded(
            state, "a", state.Launch.Generation, 1, "hash", NowTicks, NowUnixMs);

        var second = RoomStateMachine.ReportAssetsLoaded(
            first.State, "a", state.Launch.Generation, 1, "hash", NowTicks, NowUnixMs);

        Assert.True(first.Applied);
        Assert.False(second.Applied);
        Assert.True(second.Result.Success);
    }

    [Fact]
    public void ReportLoadingProgress_IsMonotonicAndDoesNotMarkAssetsLoaded()
    {
        var state = CreateLoadingState("a");

        var first = RoomStateMachine.ReportLoadingProgress(
            state, "a", state.Launch.Generation, 1, "hash", 35, NowTicks, NowUnixMs);
        var regressive = RoomStateMachine.ReportLoadingProgress(
            first.State, "a", state.Launch.Generation, 1, "hash", 20, NowTicks, NowUnixMs);

        Assert.True(first.Applied);
        Assert.Equal(35, first.State.Members[0].State.LoadingProgress);
        Assert.False(first.State.Members[0].State.AssetsLoaded);
        Assert.False(regressive.Applied);
        Assert.Equal(35, regressive.State.Members[0].State.LoadingProgress);
    }

    [Fact]
    public void ReportLoadingProgress_CoalescesSubThresholdDeltaButAlwaysAcceptsCompletion()
    {
        var state = CreateLoadingState("a");
        var first = RoomStateMachine.ReportLoadingProgress(
            state, "a", state.Launch.Generation, 1, "hash", 35, NowTicks, NowUnixMs);
        var coalesced = RoomStateMachine.ReportLoadingProgress(
            first.State, "a", state.Launch.Generation, 1, "hash", 36, NowTicks, NowUnixMs);
        var completion = RoomStateMachine.ReportLoadingProgress(
            first.State, "a", state.Launch.Generation, 1, "hash", 100, NowTicks, NowUnixMs);

        Assert.False(coalesced.Applied);
        Assert.Equal(35, coalesced.State.Members[0].State.LoadingProgress);
        Assert.True(completion.Applied);
        Assert.Equal(100, completion.State.Members[0].State.LoadingProgress);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ReportLoadingProgress_RejectsOutOfRange(int progress)
    {
        var state = CreateLoadingState("a");

        var report = RoomStateMachine.ReportLoadingProgress(
            state, "a", state.Launch.Generation, 1, "hash", progress, NowTicks, NowUnixMs);

        Assert.False(report.Result.Success);
        Assert.Equal(RoomOperationErrorCode.InvalidOperation, report.Result.ErrorCode);
    }

    [Fact]
    public void ReportAssetsLoaded_AtomicallyCompletesProgress()
    {
        var state = CreateLoadingState("a");
        var partial = RoomStateMachine.ReportLoadingProgress(
            state, "a", state.Launch.Generation, 1, "hash", 75, NowTicks, NowUnixMs);

        var loaded = RoomStateMachine.ReportAssetsLoaded(
            partial.State, "a", state.Launch.Generation, 1, "hash", NowTicks, NowUnixMs);

        Assert.True(loaded.State.Members[0].State.AssetsLoaded);
        Assert.Equal(100, loaded.State.Members[0].State.LoadingProgress);
        Assert.Equal(RoomPhase.Starting, loaded.State.Phase);
    }

    [Fact]
    public void ReportAssetsLoaded_LastMember_TriggersStartingExactlyOnce()
    {
        var state = CreateLoadingState("a", "b");
        var generation = state.Launch.Generation;

        var first = RoomStateMachine.ReportAssetsLoaded(
            state, "a", generation, 1, "hash", NowTicks, NowUnixMs);
        Assert.Equal(RoomPhase.Loading, first.State.Phase);

        var second = RoomStateMachine.ReportAssetsLoaded(
            first.State, "b", generation, 1, "hash", NowTicks, NowUnixMs);
        Assert.True(second.Applied);
        Assert.Equal(RoomPhase.Starting, second.State.Phase);
        Assert.Equal("AllAssetsLoaded", second.State.PhaseReason);
        Assert.Equal(generation, second.State.BattleCommit.Generation);
        Assert.Equal(RoomBattleCommitStatus.Pending, second.State.BattleCommit.Status);

        // Starting 后再 report：幂等，不二次触发。
        var third = RoomStateMachine.ReportAssetsLoaded(
            second.State, "a", generation, 1, "hash", NowTicks, NowUnixMs);
        Assert.False(third.Applied);
        Assert.Equal(RoomPhase.Starting, third.State.Phase);
    }

    [Fact]
    public void ReportAssetsLoaded_ConcurrentLastTwo_OnlyOneStarting()
    {
        var state = CreateLoadingState("a", "b");
        var generation = state.Launch.Generation;

        // 模拟并发：两个 report 都基于同一中间状态（仅 a loaded）。
        var afterA = RoomStateMachine.ReportAssetsLoaded(
            state, "a", generation, 1, "hash", NowTicks, NowUnixMs);

        var branch1 = RoomStateMachine.ReportAssetsLoaded(
            afterA.State, "b", generation, 1, "hash", NowTicks, NowUnixMs);
        var branch2 = RoomStateMachine.ReportAssetsLoaded(
            afterA.State, "b", generation, 1, "hash", NowTicks, NowUnixMs);

        Assert.Equal(RoomPhase.Starting, branch1.State.Phase);
        Assert.Equal(RoomPhase.Starting, branch2.State.Phase);
        // 合并后只有一个 Starting（两者都从同一状态出发，结果一致）。
        Assert.Equal(branch1.State.Phase, branch2.State.Phase);
    }

    [Fact]
    public void BeginLoading_OwnerFreezesRosterAndIncrementsGeneration()
    {
        var state = CreateState();
        var afterA = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);
        var afterB = RoomStateMachine.Join(afterA.State, "b", false, NowTicks, NowUnixMs);

        var begin = RoomStateMachine.BeginLoading(
            afterB.State,
            "owner",
            expectedRevision: null,
            manifestVersion: 1,
            manifestHash: "hash",
            nowTicks: NowTicks,
            nowUnixMs: NowUnixMs,
            loadingTimeoutMs: 1000);

        Assert.True(begin.Applied);
        Assert.Equal(RoomPhase.Loading, begin.State.Phase);
        Assert.Equal("BeginLoading", begin.State.PhaseReason);
        Assert.Equal(1, begin.State.Launch.Generation);
        Assert.Equal(NowUnixMs + 1000, begin.State.Launch.DeadlineUnixMs);
        Assert.Equal(1, begin.State.Launch.ManifestVersion);
        Assert.Equal("hash", begin.State.Launch.ManifestHash);
        Assert.Equal(2, begin.State.Launch.LockedRoster.Count);
        Assert.Equal("a", begin.State.Launch.LockedRoster[0]);
        Assert.Equal("b", begin.State.Launch.LockedRoster[1]);
    }

    [Fact]
    public void BeginLoading_NonOwner_RejectsWithNotOwner()
    {
        var state = CreateState();
        var joined = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);

        var begin = RoomStateMachine.BeginLoading(
            joined.State, "a", null, 1, "hash", NowTicks, NowUnixMs, 1000);

        Assert.False(begin.Result.Success);
        Assert.Equal(RoomOperationErrorCode.NotOwner, begin.Result.ErrorCode);
    }

    [Fact]
    public void BeginLoading_OutsideLobby_RejectsWithInvalidPhase()
    {
        var state = CreateLoadingState("a");

        var begin = RoomStateMachine.BeginLoading(
            state, "a", null, 1, "hash", NowTicks, NowUnixMs, 1000);

        Assert.False(begin.Result.Success);
        Assert.Equal(RoomOperationErrorCode.InvalidPhase, begin.Result.ErrorCode);
    }

    [Fact]
    public void BeginLoading_RevisionMismatch_RejectsWithRevisionConflict()
    {
        var state = CreateState();
        var joined = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);

        var begin = RoomStateMachine.BeginLoading(
            joined.State, "owner", expectedRevision: 999, 1, "hash", NowTicks, NowUnixMs, 1000);

        Assert.False(begin.Result.Success);
        Assert.Equal(RoomOperationErrorCode.RevisionConflict, begin.Result.ErrorCode);
    }

    [Fact]
    public void BeginLoading_WithOfflineMember_Rejects()
    {
        var state = CreateState();
        var afterA = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);
        var offline = RoomStateMachine.MarkOffline(afterA.State, "a", NowTicks, NowUnixMs);

        var begin = RoomStateMachine.BeginLoading(
            offline.State, "owner", null, 1, "hash", NowTicks, NowUnixMs, 1000);

        Assert.False(begin.Result.Success);
        Assert.Equal(RoomOperationErrorCode.InvalidOperation, begin.Result.ErrorCode);
    }

    [Fact]
    public void CancelLoading_OwnerResetsToLobbyAndKeepsGeneration()
    {
        var state = CreateLoadingState("a", "b");
        var generation = state.Launch.Generation;
        var loaded = RoomStateMachine.ReportAssetsLoaded(
            state, "a", generation, 1, "hash", NowTicks, NowUnixMs);

        var cancel = RoomStateMachine.CancelLoading(loaded.State, "a", null, NowUnixMs);

        Assert.True(cancel.Applied);
        Assert.Equal(RoomPhase.Lobby, cancel.State.Phase);
        Assert.Equal("CancelLoading", cancel.State.PhaseReason);
        Assert.All(cancel.State.Members, member => Assert.False(member.State.AssetsLoaded));
        Assert.Empty(cancel.State.Launch.LockedRoster);
        Assert.Equal(0, cancel.State.Launch.DeadlineUnixMs);
        // generation 不回退。
        Assert.Equal(generation, cancel.State.Launch.Generation);
    }

    [Fact]
    public void CancelLoading_NonOwner_Rejects()
    {
        var state = CreateLoadingState("a", "b");

        var cancel = RoomStateMachine.CancelLoading(state, "b", null, NowUnixMs);

        Assert.False(cancel.Result.Success);
        Assert.Equal(RoomOperationErrorCode.NotOwner, cancel.Result.ErrorCode);
    }

    [Fact]
    public void CancelLoading_OutsideLoading_RejectsInvalidPhase()
    {
        var state = CreateState();

        var cancel = RoomStateMachine.CancelLoading(state, "owner", null, NowUnixMs);

        Assert.False(cancel.Result.Success);
        Assert.Equal(RoomOperationErrorCode.InvalidPhase, cancel.Result.ErrorCode);
    }

    [Fact]
    public void Tick_LoadingDeadlineExpired_ResetsToLobby()
    {
        var state = CreateLoadingState("a");
        var withDeadline = state with
        {
            Launch = state.Launch with { DeadlineUnixMs = 100 }
        };

        var tick = RoomStateMachine.Tick(withDeadline, NowTicks, nowUnixMs: 200, offlineGraceMs: 0);

        Assert.True(tick.Applied);
        Assert.Equal(RoomPhase.Lobby, tick.State.Phase);
        Assert.Equal("LoadingTimeout", tick.State.PhaseReason);
        Assert.All(tick.State.Members, member => Assert.False(member.State.AssetsLoaded));
    }

    [Fact]
    public void Tick_LoadingDeadlineNotExpired_NoChange()
    {
        var state = CreateLoadingState("a");
        var withDeadline = state with
        {
            Launch = state.Launch with { DeadlineUnixMs = 1000 }
        };

        var tick = RoomStateMachine.Tick(withDeadline, NowTicks, nowUnixMs: 200, offlineGraceMs: 0);

        Assert.False(tick.Applied);
        Assert.Equal(RoomPhase.Loading, tick.State.Phase);
    }

    [Fact]
    public void Tick_OfflineGraceExpired_RemovesMember()
    {
        var state = CreateState();
        var afterA = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);
        var offline = RoomStateMachine.MarkOffline(afterA.State, "a", NowTicks, NowUnixMs);

        var tick = RoomStateMachine.Tick(offline.State, nowTicks: NowTicks + 10_000_000, nowUnixMs: NowUnixMs + 10_000, offlineGraceMs: 1);

        Assert.True(tick.Applied);
        Assert.Empty(tick.State.Members);
        Assert.Equal(RoomPhase.Closing, tick.State.Phase);
    }

    [Fact]
    public void SetLobbyReady_OutsideLobby_ReturnsStructuredInvalidPhase()
    {
        var state = CreateLoadingState("a");

        var ready = RoomStateMachine.SetLobbyReady(state, "a", true, NowTicks, NowUnixMs);

        Assert.False(ready.Applied);
        Assert.False(ready.Result.Success);
        Assert.Equal(RoomOperationErrorCode.InvalidPhase, ready.Result.ErrorCode);
    }

    [Fact]
    public void GameplayChanged_ResetsLobbyReadyForThatMemberOnly()
    {
        var state = CreateState();
        var afterA = RoomStateMachine.Join(state, "a", false, NowTicks, NowUnixMs);
        var afterB = RoomStateMachine.Join(afterA.State, "b", false, NowTicks, NowUnixMs);
        var readyA = RoomStateMachine.SetLobbyReady(afterB.State, "a", true, NowTicks, NowUnixMs);
        var readyB = RoomStateMachine.SetLobbyReady(readyA.State, "b", true, NowTicks, NowUnixMs);

        var changed = RoomStateMachine.GameplayChanged(readyB.State, "a", NowTicks, NowUnixMs);

        Assert.True(changed.Applied);
        Assert.False(changed.State.Members.First(member => member.AccountId == "a").State.LobbyReady);
        Assert.True(changed.State.Members.First(member => member.AccountId == "b").State.LobbyReady);
    }

    [Fact]
    public void IsTransitionAllowed_AllowsExpectedPaths()
    {
        Assert.True(RoomStateMachine.IsTransitionAllowed(RoomPhase.Lobby, RoomPhase.Loading));
        Assert.True(RoomStateMachine.IsTransitionAllowed(RoomPhase.Loading, RoomPhase.Lobby));
        Assert.True(RoomStateMachine.IsTransitionAllowed(RoomPhase.Loading, RoomPhase.Starting));
        Assert.True(RoomStateMachine.IsTransitionAllowed(RoomPhase.Starting, RoomPhase.InBattle));
        Assert.True(RoomStateMachine.IsTransitionAllowed(RoomPhase.InBattle, RoomPhase.Closing));
        Assert.True(RoomStateMachine.IsTransitionAllowed(RoomPhase.Closing, RoomPhase.Closed));
    }

    [Fact]
    public void IsTransitionAllowed_RejectsIllegalPaths()
    {
        Assert.False(RoomStateMachine.IsTransitionAllowed(RoomPhase.Lobby, RoomPhase.InBattle));
        Assert.False(RoomStateMachine.IsTransitionAllowed(RoomPhase.Closed, RoomPhase.Lobby));
        Assert.False(RoomStateMachine.IsTransitionAllowed(RoomPhase.InBattle, RoomPhase.Lobby));
    }

    [Fact]
    public void PrepareCommit_RecordsCommitIdAndHash_WhenStarting()
    {
        var state = CreateStartingState();

        var transition = RoomStateMachine.PrepareCommit(state, "room-1:1", "hash-abc", NowUnixMs);

        Assert.True(transition.Applied);
        Assert.Equal("room-1:1", transition.State.BattleCommit.CommitId);
        Assert.Equal("hash-abc", transition.State.BattleCommit.InitSpecHash);
    }

    [Fact]
    public void PrepareCommit_IsIdempotent_WhenSameCommitIdAndHash()
    {
        var state = CreateStartingState() with
        {
            BattleCommit = new RoomBattleCommitPersistentState(
                1, "room-1:1", RoomBattleCommitStatus.Pending, "hash-abc", null, 0, null, 0, null)
        };

        var transition = RoomStateMachine.PrepareCommit(state, "room-1:1", "hash-abc", NowUnixMs);

        Assert.False(transition.Applied);
    }

    [Fact]
    public void PrepareCommit_RejectsDifferentCommitId()
    {
        var state = CreateStartingState() with
        {
            BattleCommit = new RoomBattleCommitPersistentState(
                1, "room-1:1", RoomBattleCommitStatus.Pending, "hash-abc", null, 0, null, 0, null)
        };

        var transition = RoomStateMachine.PrepareCommit(state, "room-1:2", "hash-abc", NowUnixMs);

        Assert.False(transition.Result.Success);
    }

    [Fact]
    public void CommitBattleStarted_TransitionsToInBattle_WhenGenerationMatches()
    {
        var state = CreateStartingState() with
        {
            Launch = new RoomLaunchPersistentState(1, 0, 0, null, new List<string>()),
            BattleCommit = new RoomBattleCommitPersistentState(
                1, "room-1:1", RoomBattleCommitStatus.Pending, "hash-abc", null, 0, null, 0, null)
        };
        var anchor = new WorldStartAnchor(1000, 60, 0, 0.0166);

        var transition = RoomStateMachine.CommitBattleStarted(
            state, "room-1:1", "battle-1", 42UL, anchor, "hash-abc", NowUnixMs);

        Assert.True(transition.Applied);
        Assert.Equal(RoomPhase.InBattle, transition.State.Phase);
        Assert.Equal(RoomBattleCommitStatus.Committed, transition.State.BattleCommit.Status);
        Assert.Equal("battle-1", transition.State.BattleCommit.BattleId);
        Assert.Equal(42UL, transition.State.BattleCommit.WorldId);
    }

    [Fact]
    public void CommitBattleStarted_RejectsStaleGeneration()
    {
        var state = CreateStartingState() with
        {
            Launch = new RoomLaunchPersistentState(2, 0, 0, null, new List<string>()),
            BattleCommit = new RoomBattleCommitPersistentState(
                1, "room-1:1", RoomBattleCommitStatus.Pending, "hash-abc", null, 0, null, 0, null)
        };

        var transition = RoomStateMachine.CommitBattleStarted(
            state, "room-1:1", "battle-1", 42UL, null, "hash-abc", NowUnixMs);

        Assert.False(transition.Result.Success);
        Assert.Equal(RoomPhase.Starting, transition.State.Phase);
    }

    [Fact]
    public void RollbackBattleCommit_Retries_WhenUnderMaxAttempts()
    {
        var state = CreateStartingState() with
        {
            BattleCommit = new RoomBattleCommitPersistentState(
                1, "room-1:1", RoomBattleCommitStatus.Pending, "hash-abc", null, 0, null, 0, null)
        };

        var transition = RoomStateMachine.RollbackBattleCommit(state, "transient-error", NowUnixMs, maxAttempts: 3);

        Assert.True(transition.Applied);
        Assert.Equal(RoomPhase.Starting, transition.State.Phase);
        Assert.Equal("CommitRetry", transition.State.PhaseReason);
        Assert.Equal(1, transition.State.BattleCommit.AttemptCount);
        Assert.Equal("transient-error", transition.State.BattleCommit.LastError);
    }

    [Fact]
    public void RollbackBattleCommit_RollsBackToLobby_WhenMaxAttemptsExceeded()
    {
        var state = CreateStartingState() with
        {
            BattleCommit = new RoomBattleCommitPersistentState(
                1, "room-1:1", RoomBattleCommitStatus.Pending, "hash-abc", null, 0, null, 2, null)
        };

        var transition = RoomStateMachine.RollbackBattleCommit(state, "fatal-error", NowUnixMs, maxAttempts: 3);

        Assert.True(transition.Applied);
        Assert.Equal(RoomPhase.Lobby, transition.State.Phase);
        Assert.Equal("CommitFailed", transition.State.PhaseReason);
        Assert.Equal(RoomBattleCommitStatus.Failed, transition.State.BattleCommit.Status);
        Assert.Null(transition.State.BattleCommit.BattleId);
        Assert.Null(transition.State.BattleCommit.CommitId);
        Assert.Null(transition.State.BattleCommit.InitSpecHash);
        Assert.Equal(3, transition.State.BattleCommit.AttemptCount);
        Assert.Equal("fatal-error", transition.State.BattleCommit.LastError);
    }

    [Fact]
    public void RollbackBattleCommit_DoesNotRegressGeneration()
    {
        var state = CreateStartingState() with
        {
            Launch = new RoomLaunchPersistentState(5, 0, 0, null, new List<string>()),
            BattleCommit = new RoomBattleCommitPersistentState(
                5, "room-1:5", RoomBattleCommitStatus.Pending, "hash-abc", null, 0, null, 2, null)
        };

        var transition = RoomStateMachine.RollbackBattleCommit(state, "fatal-error", NowUnixMs, maxAttempts: 3);

        Assert.Equal(5, transition.State.Launch.Generation);
    }

    private static RoomPersistentState CreateStartingState(string ownerAccountId = "owner")
    {
        return CreateState(phase: RoomPhase.Starting, ownerAccountId: ownerAccountId) with
        {
            Launch = new RoomLaunchPersistentState(1, 0, 0, null, new List<string>()),
            BattleCommit = new RoomBattleCommitPersistentState(
                1, null, RoomBattleCommitStatus.Pending, null, null, 0, null, 0, null)
        };
    }

    private static RoomPersistentState CreateState(
        int maxPlayers = 8,
        RoomPhase phase = RoomPhase.Lobby,
        string ownerAccountId = "owner")
    {
        var summary = new RoomSummary(
            Region: "local",
            ServerId: "server-a",
            RoomId: "room-1",
            RoomType: "moba",
            Title: "Room",
            IsPublic: true,
            MaxPlayers: maxPlayers,
            PlayerCount: 0,
            OwnerAccountId: ownerAccountId,
            CreatedAtUnixMs: 0,
            Tags: null);

        return new RoomPersistentState(
            SchemaVersion: RoomPersistentState.CurrentSchemaVersion,
            Summary: summary,
            DirectoryKey: "dir",
            Phase: phase,
            PhaseReason: string.Empty,
            Members: new List<RoomPersistentMember>(),
            NextJoinOrdinal: 1,
            GameplayState: new RoomGameplayPersistentState("empty", 1, Array.Empty<byte>()),
            Revision: 0,
            EventSequence: 0,
            Launch: new RoomLaunchPersistentState(0, 0, 0, null, new List<string>()),
            BattleCommit: new RoomBattleCommitPersistentState(0, null, RoomBattleCommitStatus.None, null, null, 0, null, 0, null),
            CommandDedupEntries: new List<RoomCommandDedupEntry>(),
            TerminalReason: null,
            UpdatedAtUnixMs: 0);
    }

    private static RoomPersistentState CreateLoadingState(params string[] accountIds)
    {
        var state = CreateState(phase: RoomPhase.Loading, ownerAccountId: accountIds.Length == 0 ? "owner" : accountIds[0]);
        var members = new List<RoomPersistentMember>();
        var ordinal = 1L;
        foreach (var accountId in accountIds)
        {
            members.Add(new RoomPersistentMember(
                accountId,
                new RoomMemberState(true, NowTicks, 0, false, ordinal)));
            ordinal++;
        }

        return state with
        {
            Members = members,
            NextJoinOrdinal = ordinal,
            Summary = state.Summary with { PlayerCount = members.Count },
            Launch = new RoomLaunchPersistentState(7, 0, 1, "hash", accountIds.ToList())
        };
    }
}
