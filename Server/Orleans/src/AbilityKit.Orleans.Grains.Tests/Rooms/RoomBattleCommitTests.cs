using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Grains.Rooms;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

/// <summary>
/// 阶段 3 幂等 Battle commit 与故障恢复的集成测试。
/// 覆盖 BattleInitResult 契约语义、InitSpecHash 稳定性、以及状态机 commit/rollback 全链路。
/// </summary>
public sealed class RoomBattleCommitTests
{
    private const long NowUnixMs = 1_700_000_000_000L;

    [Fact]
    public void BattleInitResult_FromInitialized_SucceedsWithAnchor()
    {
        var anchor = new WorldStartAnchor(1000, 60, 0, 0.0166);

        var result = BattleInitResult.FromInitialized("hash-abc", anchor);

        Assert.True(result.Succeeded);
        Assert.True(result.Initialized);
        Assert.False(result.AlreadyInitialized);
        Assert.Equal("hash-abc", result.InitSpecHash);
        Assert.Same(anchor, result.WorldStartAnchor);
        Assert.Null(result.Error);
    }

    [Fact]
    public void BattleInitResult_FromAlreadyInitialized_SucceedsAsIdempotentHit()
    {
        var anchor = new WorldStartAnchor(1000, 60, 0, 0.0166);

        var result = BattleInitResult.FromAlreadyInitialized("hash-abc", anchor);

        Assert.True(result.Succeeded);
        Assert.False(result.Initialized);
        Assert.True(result.AlreadyInitialized);
        Assert.Equal("hash-abc", result.InitSpecHash);
    }

    [Fact]
    public void BattleInitResult_FromHashMismatch_DoesNotSucceed()
    {
        var result = BattleInitResult.FromHashMismatch("hash-stored");

        Assert.False(result.Succeeded);
        Assert.Equal("InitSpecHashMismatch", result.Error);
        Assert.True(result.AlreadyInitialized);
    }

    [Fact]
    public void BattleInitResult_FromError_DoesNotSucceed()
    {
        var result = BattleInitResult.FromError("boom");

        Assert.False(result.Succeeded);
        Assert.Equal("boom", result.Error);
    }

    /// <summary>
    /// 模拟完整 commit 成功链路：PrepareCommit -> CommitBattleStarted。
    /// 验证 InitSpecHash 在整个链路中保持一致，且最终进入 InBattle。
    /// </summary>
    [Fact]
    public void CommitFlow_PrepareThenCommit_ResultsInInBattleWithConsistentHash()
    {
        var state = CreateStartingState();
        var initSpecHash = RoomBattleInitSpecHasher.Compute(CreateInitParams());
        var commitId = "room-1:1";
        var anchor = new WorldStartAnchor(1000, 60, 0, 0.0166);

        var prepared = RoomStateMachine.PrepareCommit(state, commitId, initSpecHash, NowUnixMs);
        Assert.True(prepared.Applied);
        Assert.Equal(commitId, prepared.State.BattleCommit.CommitId);
        Assert.Equal(initSpecHash, prepared.State.BattleCommit.InitSpecHash);

        var committed = RoomStateMachine.CommitBattleStarted(
            prepared.State, commitId, "battle-1", 42UL, anchor, initSpecHash, NowUnixMs);

        Assert.True(committed.Applied);
        Assert.Equal(RoomPhase.InBattle, committed.State.Phase);
        Assert.Equal(RoomBattleCommitStatus.Committed, committed.State.BattleCommit.Status);
        Assert.Equal("battle-1", committed.State.BattleCommit.BattleId);
        Assert.Equal(initSpecHash, committed.State.BattleCommit.InitSpecHash);
        Assert.Equal(42UL, committed.State.BattleCommit.WorldId);
    }

    /// <summary>
    /// 模拟 hash 冲突场景：Battle 已用 hash-A 初始化，再次用 hash-B 初始化返回 mismatch，
    /// Room 侧应 forceMax rollback 到 Lobby。
    /// </summary>
    [Fact]
    public void CommitFlow_HashMismatch_ForcesImmediateRollbackToLobby()
    {
        var state = CreateStartingState() with
        {
            BattleCommit = new RoomBattleCommitPersistentState(
                1, "room-1:1", RoomBattleCommitStatus.Pending, "hash-A", null, 0, null, 0, null)
        };

        // 模拟 BattleInitResult.FromHashMismatch
        var initResult = BattleInitResult.FromHashMismatch("hash-A");
        Assert.False(initResult.Succeeded);

        // forceMax rollback（maxAttempts=1，立即超限）
        var rollback = RoomStateMachine.RollbackBattleCommit(state, initResult.Error!, NowUnixMs, maxAttempts: 1);

        Assert.True(rollback.Applied);
        Assert.Equal(RoomPhase.Lobby, rollback.State.Phase);
        Assert.Equal("CommitFailed", rollback.State.PhaseReason);
        Assert.Equal(RoomBattleCommitStatus.Failed, rollback.State.BattleCommit.Status);
        Assert.Null(rollback.State.BattleCommit.BattleId);
    }

    /// <summary>
    /// 模拟瞬时故障重试：第一次失败（attemptCount=1 < maxAttempts=3），保持 Starting 等待重试。
    /// </summary>
    [Fact]
    public void CommitFlow_TransientFailure_RetriesWithinMaxAttempts()
    {
        var state = CreateStartingState() with
        {
            BattleCommit = new RoomBattleCommitPersistentState(
                1, "room-1:1", RoomBattleCommitStatus.Pending, "hash-A", null, 0, null, 0, null)
        };

        var initResult = BattleInitResult.FromError("timeout");
        Assert.False(initResult.Succeeded);

        var rollback = RoomStateMachine.RollbackBattleCommit(state, initResult.Error!, NowUnixMs, maxAttempts: 3);

        Assert.True(rollback.Applied);
        Assert.Equal(RoomPhase.Starting, rollback.State.Phase);
        Assert.Equal("CommitRetry", rollback.State.PhaseReason);
        Assert.Equal(1, rollback.State.BattleCommit.AttemptCount);
    }

    /// <summary>
    /// 验证 CommitId 稳定性：同一 generation 多次 PrepareCommit 产生相同 CommitId。
    /// </summary>
    [Fact]
    public void CommitId_IsStable_ForSameGeneration()
    {
        var state = CreateStartingState();
        var roomId = state.Summary.RoomId;
        var generation = state.Launch.Generation;

        var commitId1 = roomId + ":" + generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var commitId2 = roomId + ":" + generation.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(commitId1, commitId2);

        var prepared1 = RoomStateMachine.PrepareCommit(state, commitId1, "hash", NowUnixMs);
        var prepared2 = RoomStateMachine.PrepareCommit(prepared1.State, commitId2, "hash", NowUnixMs);

        // 第二次应幂等（NoChange）
        Assert.False(prepared2.Applied);
        Assert.Equal(commitId1, prepared2.State.BattleCommit.CommitId);
    }

    /// <summary>
    /// 验证重试后成功 commit：rollback(retry) -> prepare(idempotent) -> commit。
    /// </summary>
    [Fact]
    public void CommitFlow_RetryThenSucceed_CommitsSuccessfully()
    {
        var state = CreateStartingState() with
        {
            BattleCommit = new RoomBattleCommitPersistentState(
                1, "room-1:1", RoomBattleCommitStatus.Pending, "hash-A", null, 0, null, 1, "timeout")
        };

        // 重试后 PrepareCommit 应幂等命中（CommitId/Hash 已记录）
        var prepared = RoomStateMachine.PrepareCommit(state, "room-1:1", "hash-A", NowUnixMs);
        Assert.False(prepared.Applied);

        var anchor = new WorldStartAnchor(2000, 60, 0, 0.0166);
        var committed = RoomStateMachine.CommitBattleStarted(
            state, "room-1:1", "battle-1", 42UL, anchor, "hash-A", NowUnixMs);

        Assert.Equal(RoomPhase.InBattle, committed.State.Phase);
        Assert.Equal(RoomBattleCommitStatus.Committed, committed.State.BattleCommit.Status);
    }

    [Fact]
    public void ReportAssetsLoadedCommitResult_Committed_ReturnsAppliedAtFinalRevision()
    {
        var state = CreateStartingState() with
        {
            Phase = RoomPhase.InBattle,
            Revision = 12,
            BattleCommit = new RoomBattleCommitPersistentState(
                1,
                "room-1:1",
                RoomBattleCommitStatus.Committed,
                "hash-A",
                "battle-1",
                42,
                new WorldStartAnchor(1000, 60, 0, 0.0166),
                0,
                null)
        };

        var result = RoomGrain.ResolveReportAssetsLoadedCommitResult(state);

        Assert.True(result.Success);
        Assert.True(result.Applied);
        Assert.Equal(12, result.RoomRevision);
    }

    [Fact]
    public void ReportAssetsLoadedCommitResult_RolledBack_ReturnsCommitFailure()
    {
        var state = CreateStartingState() with
        {
            Phase = RoomPhase.Lobby,
            Revision = 13,
            BattleCommit = new RoomBattleCommitPersistentState(
                1,
                null,
                RoomBattleCommitStatus.Failed,
                null,
                null,
                0,
                null,
                3,
                "BattleInitException:missing runtime")
        };

        var result = RoomGrain.ResolveReportAssetsLoadedCommitResult(state);

        Assert.False(result.Success);
        Assert.False(result.Applied);
        Assert.Equal(RoomOperationErrorCode.InvalidOperation, result.ErrorCode);
        Assert.Contains("BattleInitException:missing runtime", result.Message);
        Assert.Equal(13, result.RoomRevision);
    }

    private static RoomPersistentState CreateStartingState()
    {
        var summary = new RoomSummary(
            Region: "local",
            ServerId: "server-a",
            RoomId: "room-1",
            RoomType: "moba",
            Title: "Room",
            IsPublic: true,
            MaxPlayers: 8,
            PlayerCount: 0,
            OwnerAccountId: "owner",
            CreatedAtUnixMs: 0,
            Tags: null);

        return new RoomPersistentState(
            SchemaVersion: RoomPersistentState.CurrentSchemaVersion,
            Summary: summary,
            DirectoryKey: "dir",
            Phase: RoomPhase.Starting,
            PhaseReason: "AssetsLoaded",
            Members: new List<RoomPersistentMember>(),
            NextJoinOrdinal: 1,
            GameplayState: new RoomGameplayPersistentState("empty", 1, System.Array.Empty<byte>()),
            Revision: 0,
            EventSequence: 0,
            Launch: new RoomLaunchPersistentState(1, 0, 0, null, new List<string>()),
            BattleCommit: new RoomBattleCommitPersistentState(
                1, null, RoomBattleCommitStatus.Pending, null, null, 0, null, 0, null),
            CommandDedupEntries: new List<RoomCommandDedupEntry>(),
            TerminalReason: null,
            UpdatedAtUnixMs: 0);
    }

    private static BattleInitParams CreateInitParams()
    {
        return new BattleInitParams
        {
            WorldId = 42,
            TickRate = 20,
            MapId = 1,
            GameplayId = 0,
            RuleSetId = 0,
            ConfigVersion = 1,
            ProtocolVersion = 1,
            RandomSeed = 42,
            InputDelayFrames = 0,
            DurationFrames = 72000,
            WorldType = "moba",
            ClientId = "client-1",
            RoomType = "moba",
            Players = new List<PlayerInitInfo>
            {
                new()
                {
                    PlayerId = 1,
                    ActorId = 1,
                    HeroId = 101,
                    TeamId = 1,
                    Level = 1,
                    PosX = 1f,
                    PosY = 0f,
                    PosZ = 2f,
                    AccountId = "acc-1",
                    SkillIds = new List<int> { 1, 2, 3 }
                }
            }
        };
    }
}
