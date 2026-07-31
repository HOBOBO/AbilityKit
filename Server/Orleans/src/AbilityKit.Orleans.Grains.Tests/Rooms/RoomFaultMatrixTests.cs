using System;
using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Grains.Rooms;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

/// <summary>
/// 阶段 8 故障矩阵测试。
/// 覆盖 RoomStateMachine 在各种异常输入与故障场景下的行为：幂等、重复操作、非法 phase、超时、commit 失败回滚等。
/// </summary>
public sealed class RoomFaultMatrixTests
{
    private const long NowTicks = 1_000L;
    private const long NowUnixMs = 1_700_000_000_000L;
    private const int ManifestVersion = 1;
    private const string ManifestHash = "manifest-hash-abc";
    private const long LoadingTimeoutMs = 60_000L;

    /// <summary>
    /// player1 ReportAssetsLoaded 两次，第二次返回幂等（NoChange），revision 不变。
    /// </summary>
    [Fact]
    public void DuplicateReportAssetsLoaded_IsIdempotent_RevisionDoesNotIncrease()
    {
        var state = CreateLoadingState("a");
        var generation = state.Launch.Generation;

        var first = RoomStateMachine.ReportAssetsLoaded(
            state, "a", generation, ManifestVersion, ManifestHash, NowTicks, NowUnixMs);
        Assert.True(first.Applied);
        var revisionAfterFirst = first.State.Revision;

        var second = RoomStateMachine.ReportAssetsLoaded(
            first.State, "a", generation, ManifestVersion, ManifestHash, NowTicks, NowUnixMs);

        // 第二次幂等：Success=true 但 Applied=false，revision 不变。
        Assert.True(second.Result.Success);
        Assert.False(second.Applied);
        Assert.Equal(revisionAfterFirst, second.State.Revision);
    }

    /// <summary>
    /// owner BeginLoading 两次，第二次返回错误（已在 Loading，InvalidPhase）。
    /// </summary>
    [Fact]
    public void BeginLoading_Twice_SecondReturnsAlreadyLoading()
    {
        var state = CreateLobbyState(ownerAccountId: "owner");
        var joined = RoomStateMachine.Join(state, "owner", false, NowTicks, NowUnixMs);

        var first = RoomStateMachine.BeginLoading(
            joined.State, "owner", null, ManifestVersion, ManifestHash, NowTicks, NowUnixMs, LoadingTimeoutMs);
        Assert.True(first.Applied);
        Assert.Equal(RoomPhase.Loading, first.State.Phase);

        var second = RoomStateMachine.BeginLoading(
            first.State, "owner", null, ManifestVersion, ManifestHash, NowTicks, NowUnixMs, LoadingTimeoutMs);

        Assert.False(second.Result.Success);
        Assert.False(second.Applied);
        Assert.Equal(RoomOperationErrorCode.InvalidPhase, second.Result.ErrorCode);
        Assert.Equal(RoomPhase.Loading, second.State.Phase);
    }

    /// <summary>
    /// 在 Lobby 状态 ReportAssetsLoaded -> 返回 InvalidPhase 错误。
    /// </summary>
    [Fact]
    public void ReportAssetsLoaded_BeforeBeginLoading_ReturnsInvalidPhase()
    {
        // Lobby 状态，但 generation 匹配（绕过 stale 检查），触发 InvalidPhase。
        var state = CreateLobbyState(ownerAccountId: "owner") with
        {
            Launch = new RoomLaunchPersistentState(1, 0, 0, null, new List<string> { "owner" })
        };
        var joined = RoomStateMachine.Join(state, "owner", false, NowTicks, NowUnixMs);

        var report = RoomStateMachine.ReportAssetsLoaded(
            joined.State, "owner", generation: 1, ManifestVersion, ManifestHash, NowTicks, NowUnixMs);

        Assert.False(report.Result.Success);
        Assert.False(report.Applied);
        Assert.Equal(RoomOperationErrorCode.InvalidPhase, report.Result.ErrorCode);
    }

    /// <summary>
    /// Loading 状态下一人 Leave -> phase 回 Lobby, generation 递增（不回退）, AssetsLoaded 清除。
    /// </summary>
    [Fact]
    public void LeaveDuringLoading_ClearsAssetsLoaded_AndReturnsToLobby()
    {
        var state = CreateLoadingState("a", "b");
        var generation = state.Launch.Generation;
        var loaded = RoomStateMachine.ReportAssetsLoaded(
            state, "a", generation, ManifestVersion, ManifestHash, NowTicks, NowUnixMs);
        Assert.True(loaded.State.Members.First(m => m.AccountId == "a").State.AssetsLoaded);

        var left = RoomStateMachine.Leave(loaded.State, "a", NowUnixMs);

        Assert.True(left.Applied);
        Assert.Equal(RoomPhase.Lobby, left.State.Phase);
        Assert.Equal("LockedMemberLeft", left.State.PhaseReason);
        // generation 不回退。
        Assert.Equal(generation, left.State.Launch.Generation);
        // AssetsLoaded 清除。
        Assert.All(left.State.Members, member => Assert.False(member.State.AssetsLoaded));
        Assert.Empty(left.State.Launch.LockedRoster);
        Assert.Equal(0, left.State.Launch.DeadlineUnixMs);
    }

    /// <summary>
    /// Loading 状态 owner CancelLoading -> phase=Lobby, generation 不回退。
    /// </summary>
    [Fact]
    public void CancelLoading_ReturnsToLobby_IncrementsGeneration()
    {
        var state = CreateLoadingState("a", "b");
        var generation = state.Launch.Generation;

        var cancel = RoomStateMachine.CancelLoading(state, "a", null, NowUnixMs);

        Assert.True(cancel.Applied);
        Assert.Equal(RoomPhase.Lobby, cancel.State.Phase);
        Assert.Equal("CancelLoading", cancel.State.PhaseReason);
        // generation 不回退（保留递增后的值）。
        Assert.Equal(generation, cancel.State.Launch.Generation);
        Assert.All(cancel.State.Members, member => Assert.False(member.State.AssetsLoaded));
        Assert.Empty(cancel.State.Launch.LockedRoster);
    }

    /// <summary>
    /// Loading 状态，时间超过 deadline -> Tick 返回 Lobby。
    /// </summary>
    [Fact]
    public void Tick_LoadingDeadlineExpired_ReturnsToLobby()
    {
        var state = CreateLoadingState("a") with
        {
            Launch = CreateLoadingState("a").Launch with { DeadlineUnixMs = 100 }
        };

        var tick = RoomStateMachine.Tick(state, NowTicks, nowUnixMs: 200, offlineGraceMs: 0);

        Assert.True(tick.Applied);
        Assert.Equal(RoomPhase.Lobby, tick.State.Phase);
        Assert.Equal("LoadingTimeout", tick.State.PhaseReason);
        Assert.All(tick.State.Members, member => Assert.False(member.State.AssetsLoaded));
    }

    /// <summary>
    /// Starting 状态，Battle 返回 hash mismatch -> rollback 到 Lobby。
    /// </summary>
    [Fact]
    public void CommitBattle_HashMismatch_RollsBackToLobby()
    {
        var state = CreateStartingState() with
        {
            BattleCommit = new RoomBattleCommitPersistentState(
                1, "room-1:1", RoomBattleCommitStatus.Pending, "hash-A", null, 0, null, 0, null)
        };

        // 模拟 BattleInitResult.FromHashMismatch -> forceMax rollback（maxAttempts=1）。
        var rollback = RoomStateMachine.RollbackBattleCommit(state, "InitSpecHashMismatch", NowUnixMs, maxAttempts: 1);

        Assert.True(rollback.Applied);
        Assert.Equal(RoomPhase.Lobby, rollback.State.Phase);
        Assert.Equal("CommitFailed", rollback.State.PhaseReason);
        Assert.Equal(RoomBattleCommitStatus.Failed, rollback.State.BattleCommit.Status);
        Assert.Null(rollback.State.BattleCommit.BattleId);
        Assert.Null(rollback.State.BattleCommit.CommitId);
        Assert.Null(rollback.State.BattleCommit.InitSpecHash);
    }

    /// <summary>
    /// Starting 状态，Battle 返回 AlreadyInitialized + 相同 hash -> 成功进入 InBattle（幂等）。
    /// </summary>
    [Fact]
    public void CommitBattle_AlreadyInitialized_SameHash_SucceedsIdempotently()
    {
        var initSpecHash = "hash-A";
        var state = CreateStartingState() with
        {
            BattleCommit = new RoomBattleCommitPersistentState(
                1, "room-1:1", RoomBattleCommitStatus.Pending, initSpecHash, null, 0, null, 0, null)
        };

        // 模拟 BattleInitResult.FromAlreadyInitialized（相同 hash）-> 视为成功，直接 CommitBattleStarted。
        var anchor = new WorldStartAnchor(1000, 60, 0, 0.0166);
        var committed = RoomStateMachine.CommitBattleStarted(
            state, "room-1:1", "battle-1", 42UL, anchor, initSpecHash, NowUnixMs);

        Assert.True(committed.Applied);
        Assert.Equal(RoomPhase.InBattle, committed.State.Phase);
        Assert.Equal(RoomBattleCommitStatus.Committed, committed.State.BattleCommit.Status);
        Assert.Equal("battle-1", committed.State.BattleCommit.BattleId);
        Assert.Equal(initSpecHash, committed.State.BattleCommit.InitSpecHash);
    }

    /// <summary>
    /// Ready=true 然后 Ready=false 都成功。
    /// </summary>
    [Fact]
    public void SetLobbyReady_ToggleOnOff_BothSucceed()
    {
        var state = CreateLobbyState(ownerAccountId: "owner");
        var joined = RoomStateMachine.Join(state, "owner", false, NowTicks, NowUnixMs);

        var readyOn = RoomStateMachine.SetLobbyReady(joined.State, "owner", ready: true, NowTicks, NowUnixMs);
        Assert.True(readyOn.Result.Success);
        Assert.True(readyOn.Applied);
        Assert.True(readyOn.State.Members[0].State.LobbyReady);

        var readyOff = RoomStateMachine.SetLobbyReady(readyOn.State, "owner", ready: false, NowTicks, NowUnixMs);
        Assert.True(readyOff.Result.Success);
        Assert.True(readyOff.Applied);
        Assert.False(readyOff.State.Members[0].State.LobbyReady);
    }

    /// <summary>
    /// 房间满员后 Join 返回错误。
    /// </summary>
    [Fact]
    public void JoinRoomFull_ReturnsError()
    {
        var state = CreateLobbyState(ownerAccountId: "owner", maxPlayers: 1);
        var first = RoomStateMachine.Join(state, "owner", false, NowTicks, NowUnixMs);
        Assert.True(first.Applied);

        var second = RoomStateMachine.Join(first.State, "player2", false, NowTicks, NowUnixMs);

        Assert.False(second.Result.Success);
        Assert.False(second.Applied);
        Assert.Equal(RoomOperationErrorCode.InvalidOperation, second.Result.ErrorCode);
        Assert.Single(second.State.Members);
    }

    private static RoomPersistentState CreateLobbyState(string ownerAccountId = "owner", int maxPlayers = 8)
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
            Phase: RoomPhase.Lobby,
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
        var state = CreateLobbyState(ownerAccountId: accountIds.Length == 0 ? "owner" : accountIds[0]) with
        {
            Phase = RoomPhase.Loading
        };
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
            Launch = new RoomLaunchPersistentState(7, 0, ManifestVersion, ManifestHash, new List<string>(accountIds))
        };
    }

    private static RoomPersistentState CreateStartingState(string ownerAccountId = "owner")
    {
        return CreateLobbyState(ownerAccountId: ownerAccountId) with
        {
            Phase = RoomPhase.Starting,
            PhaseReason = "AssetsLoaded",
            Launch = new RoomLaunchPersistentState(1, 0, 0, null, new List<string>()),
            BattleCommit = new RoomBattleCommitPersistentState(
                1, null, RoomBattleCommitStatus.Pending, null, null, 0, null, 0, null)
        };
    }
}
