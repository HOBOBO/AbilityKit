using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Grains.Rooms;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

/// <summary>
/// 阶段 8 双客户端端到端流程测试。
/// 通过 RoomStateMachine 纯函数 API 模拟完整多人流程（加入 -> 选英雄 -> 准备 -> 加载 -> 资源上报 -> 进入战斗），
/// 以及各种多人故障场景（超时、owner 迁移）。
/// </summary>
public sealed class RoomMultiplayerE2EFlowTests
{
    private const long NowTicks = 1_000L;
    private const long NowUnixMs = 1_700_000_000_000L;
    private const int ManifestVersion = 1;
    private const string ManifestHash = "manifest-hash-abc";
    private const long LoadingTimeoutMs = 60_000L;

    [Fact]
    public async Task InitializeAsync_AddsOwnerToMembersAndMobaRoster()
    {
        var store = new InMemoryRoomStateStore();
        var grain = new RoomGrain(store);
        var summary = new RoomSummary(
            Region: "local",
            ServerId: "server-a",
            RoomId: "room-created",
            RoomType: "moba",
            Title: "Room",
            IsPublic: true,
            MaxPlayers: 2,
            PlayerCount: 0,
            OwnerAccountId: "owner",
            CreatedAtUnixMs: NowUnixMs,
            Tags: null);

        await grain.InitializeAsync(summary, "local:server-a");

        var snapshot = await grain.GetSnapshotAsync();
        Assert.Equal(1, snapshot.Summary.PlayerCount);
        Assert.Equal(new[] { "owner" }, snapshot.Members);
        var owner = Assert.Single(snapshot.Players);
        Assert.Equal("owner", owner.AccountId);
        Assert.Equal(1, owner.JoinOrdinal);
        Assert.True(owner.IsOnline);

        var persisted = await store.TryGetRuntimeStateAsync(summary.RoomId);
        Assert.NotNull(persisted);
        var persistedOwner = Assert.Single(persisted.Members);
        Assert.Equal("owner", persistedOwner.AccountId);
        Assert.Equal(1, persistedOwner.State.JoinOrdinal);
        Assert.Equal(2, persisted.NextJoinOrdinal);
        Assert.Equal(1, persisted.Summary.PlayerCount);
    }

    [Fact]
    public void PickHero_SameTeamConflict_DoesNotAdvanceRevisionOrMutateRejectedPlayer()
    {
        var summary = new RoomSummary(
            Region: "local",
            ServerId: "server-a",
            RoomId: "room-hero-conflict",
            RoomType: "moba",
            Title: "Room",
            IsPublic: true,
            MaxPlayers: 2,
            PlayerCount: 0,
            OwnerAccountId: "owner",
            CreatedAtUnixMs: NowUnixMs,
            Tags: null);

        var adapter = new MobaRoomGameplayAdapter();
        var gameplayState = adapter.CreateState(summary);
        adapter.Join(gameplayState, summary, Array.Empty<string>(), "owner");
        adapter.Join(gameplayState, summary, new[] { "owner" }, "player2");
        var ownerResult = adapter.SubmitCommand(
            gameplayState,
            CreateLoadout("owner", heroId: 1001, teamId: 1));
        Assert.True(ownerResult.Success);
        var beforeConflict = adapter.ExportPersistentState(gameplayState);

        var state = CreateLobbyState();
        state = RoomStateMachine.Join(state, "owner", false, NowTicks, NowUnixMs).State;
        state = RoomStateMachine.Join(state, "player2", false, NowTicks + 1, NowUnixMs + 1).State;
        state = RoomStateMachine.GameplayChanged(state, "owner", NowTicks + 2, NowUnixMs + 2).State;
        var revisionBeforeConflict = state.Revision;
        var candidateTransition = RoomStateMachine.GameplayChanged(
            state,
            "player2",
            NowTicks + 3,
            NowUnixMs + 3);
        var result = adapter.SubmitCommand(
            gameplayState,
            CreateLoadout("player2", heroId: 1001, teamId: 1));
        var afterConflict = adapter.ExportPersistentState(gameplayState);

        Assert.False(result.Success);
        Assert.Equal(RoomOperationErrorCode.HeroConflict, result.ErrorCode);
        Assert.True(candidateTransition.Applied);
        Assert.Equal(revisionBeforeConflict, state.Revision);
        Assert.Equal(beforeConflict.Payload, afterConflict.Payload);

        var invalidResult = adapter.SubmitCommand(
            gameplayState,
            RoomGameplayCommandRequest.CreateMobaLoadout(
                "player2",
                heroId: 1002,
                teamId: 1,
                spawnPointId: 9,
                level: 1,
                attributeTemplateId: 2002,
                basicAttackSkillId: 3004,
                skillIds: Array.Empty<int>()));
        Assert.False(invalidResult.Success);
        Assert.Equal(RoomOperationErrorCode.InvalidGameplayCommand, invalidResult.ErrorCode);
        Assert.Equal(beforeConflict.Payload, adapter.ExportPersistentState(gameplayState).Payload);
    }

    [Fact]
    public void Restore_InBattleMemberReconnectsWithoutLosingBattleIdentity()
    {
        var state = CreateLobbyState();
        state = RoomStateMachine.Join(state, "owner", false, NowTicks, NowUnixMs).State;
        state = RoomStateMachine.Join(state, "player2", false, NowTicks + 1, NowUnixMs + 1).State;
        state = state with
        {
            Phase = RoomPhase.InBattle,
            BattleCommit = new RoomBattleCommitPersistentState(
                Generation: 3,
                CommitId: "room-1:3",
                Status: RoomBattleCommitStatus.Committed,
                InitSpecHash: "init-hash",
                BattleId: "battle-1",
                WorldId: 42UL,
                WorldStartAnchor: new WorldStartAnchor(1000, 60, 0, 1d / 60d),
                AttemptCount: 1,
                LastError: null)
        };
        var offline = RoomStateMachine.MarkOffline(
            state,
            "player2",
            NowTicks + 2,
            NowUnixMs + 2);
        var restored = RoomStateMachine.Reconnect(
            offline.State,
            "player2",
            false,
            NowTicks + 3,
            NowUnixMs + 3);

        Assert.True(restored.Applied);
        Assert.Equal(RoomPhase.InBattle, restored.State.Phase);
        Assert.True(restored.State.Members.Single(member => member.AccountId == "player2").State.IsOnline);
        Assert.Equal("battle-1", restored.State.BattleCommit.BattleId);
        Assert.Equal(42UL, restored.State.BattleCommit.WorldId);
        Assert.Equal(3, restored.State.BattleCommit.Generation);
    }

    /// <summary>
    /// 完整双客户端流程：两人加入 -> 各自选英雄 -> 各自准备 -> owner BeginLoading -> 两人 ReportAssetsLoaded
    /// -> Tick 进入 Starting -> PrepareCommit + CommitBattleStarted -> InBattle。
    /// 验证两人最终 phase=InBattle、revision 一致、battle identity 一致。
    /// </summary>
    [Fact]
    public void FullFlow_TwoPlayersJoin_PickDifferentHeroes_Ready_BeginLoading_ReportAssets_AutoCommit_EntersInBattle()
    {
        var state = CreateLobbyState(ownerAccountId: "owner");

        // owner 已在房间（owner 是房间创建者，第一个加入）。
        var afterOwnerJoin = RoomStateMachine.Join(state, "owner", isBot: false, NowTicks, NowUnixMs);
        Assert.True(afterOwnerJoin.Applied);

        // player2 加入。
        var afterPlayer2Join = RoomStateMachine.Join(afterOwnerJoin.State, "player2", isBot: false, NowTicks + 1, NowUnixMs + 1);
        Assert.True(afterPlayer2Join.Applied);
        Assert.Equal(2, afterPlayer2Join.State.Members.Count);
        Assert.All(afterPlayer2Join.State.Members, member => Assert.Equal(RoomPhase.Lobby, afterPlayer2Join.State.Phase));

        // 两人各自选英雄（GameplayChanged 会重置该成员的 LobbyReady）。
        var afterOwnerPick = RoomStateMachine.GameplayChanged(afterPlayer2Join.State, "owner", NowTicks + 2, NowUnixMs + 2);
        Assert.True(afterOwnerPick.Applied);
        var afterPlayer2Pick = RoomStateMachine.GameplayChanged(afterOwnerPick.State, "player2", NowTicks + 3, NowUnixMs + 3);
        Assert.True(afterPlayer2Pick.Applied);

        // 两人各自准备。
        var afterOwnerReady = RoomStateMachine.SetLobbyReady(afterPlayer2Pick.State, "owner", ready: true, NowTicks + 4, NowUnixMs + 4);
        Assert.True(afterOwnerReady.Applied);
        var afterPlayer2Ready = RoomStateMachine.SetLobbyReady(afterOwnerReady.State, "player2", ready: true, NowTicks + 5, NowUnixMs + 5);
        Assert.True(afterPlayer2Ready.Applied);
        Assert.All(afterPlayer2Ready.State.Members, member => Assert.True(member.State.LobbyReady));

        // owner BeginLoading -> phase=Loading, generation=1。
        var beginLoading = RoomStateMachine.BeginLoading(
            afterPlayer2Ready.State,
            accountId: "owner",
            expectedRevision: null,
            manifestVersion: ManifestVersion,
            manifestHash: ManifestHash,
            nowTicks: NowTicks + 6,
            nowUnixMs: NowUnixMs + 6,
            loadingTimeoutMs: LoadingTimeoutMs);
        Assert.True(beginLoading.Applied);
        Assert.Equal(RoomPhase.Loading, beginLoading.State.Phase);
        Assert.Equal(1, beginLoading.State.Launch.Generation);
        Assert.Equal(2, beginLoading.State.Launch.LockedRoster.Count);

        var generation = beginLoading.State.Launch.Generation;

        // 两人各自 ReportAssetsLoaded。
        var afterOwnerReport = RoomStateMachine.ReportAssetsLoaded(
            beginLoading.State, "owner", generation, ManifestVersion, ManifestHash, NowTicks + 7, NowUnixMs + 7);
        Assert.True(afterOwnerReport.Applied);
        Assert.Equal(RoomPhase.Loading, afterOwnerReport.State.Phase);

        var afterPlayer2Report = RoomStateMachine.ReportAssetsLoaded(
            afterOwnerReport.State, "player2", generation, ManifestVersion, ManifestHash, NowTicks + 8, NowUnixMs + 8);
        Assert.True(afterPlayer2Report.Applied);
        // 最后一人 report 后自动进入 Starting。
        Assert.Equal(RoomPhase.Starting, afterPlayer2Report.State.Phase);
        Assert.Equal("AllAssetsLoaded", afterPlayer2Report.State.PhaseReason);

        // Tick（所有成员 AssetsLoaded，应无变化，仍 Starting）。
        var tick = RoomStateMachine.Tick(afterPlayer2Report.State, NowTicks + 9, NowUnixMs + 9, offlineGraceMs: 0);
        Assert.False(tick.Applied);
        Assert.Equal(RoomPhase.Starting, tick.State.Phase);

        // PrepareCommit + CommitBattleStarted -> InBattle。
        var commitId = tick.State.Summary.RoomId + ":" + generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var initSpecHash = "init-spec-hash";
        var anchor = new WorldStartAnchor(1000, 60, 0, 0.0166);

        var prepared = RoomStateMachine.PrepareCommit(tick.State, commitId, initSpecHash, NowUnixMs + 10);
        Assert.True(prepared.Applied);

        var committed = RoomStateMachine.CommitBattleStarted(
            prepared.State, commitId, "battle-1", 42UL, anchor, initSpecHash, NowUnixMs + 11);
        Assert.True(committed.Applied);
        Assert.Equal(RoomPhase.InBattle, committed.State.Phase);

        // 验证：两人最终 phase=InBattle、revision 一致、battle identity 一致。
        Assert.Equal(2, committed.State.Members.Count);
        Assert.All(committed.State.Members, member =>
        {
            // 状态机层面 phase 是房间级，所有成员共享同一 phase。
            Assert.Equal(RoomPhase.InBattle, committed.State.Phase);
        });
        Assert.Equal(committed.State.Revision, committed.Result.RoomRevision);
        Assert.Equal("battle-1", committed.State.BattleCommit.BattleId);
        Assert.Equal(42UL, committed.State.BattleCommit.WorldId);
        Assert.Equal(initSpecHash, committed.State.BattleCommit.InitSpecHash);
        Assert.Equal(generation, committed.State.BattleCommit.Generation);
        Assert.Equal(RoomBattleCommitStatus.Committed, committed.State.BattleCommit.Status);
    }

    /// <summary>
    /// 3 人加入，2 人 report，第 3 人超时未 report。
    /// Tick 超过 loading deadline -> phase=Lobby, generation 递增（不回退）, AssetsLoaded 清除。
    /// </summary>
    [Fact]
    public void FullFlow_ThreePlayers_OnlyTwoReport_ThirdTimesOut_ReturnsToLobby()
    {
        var state = CreateLobbyState(ownerAccountId: "owner");

        var afterOwner = RoomStateMachine.Join(state, "owner", false, NowTicks, NowUnixMs);
        var afterP2 = RoomStateMachine.Join(afterOwner.State, "player2", false, NowTicks + 1, NowUnixMs + 1);
        var afterP3 = RoomStateMachine.Join(afterP2.State, "player3", false, NowTicks + 2, NowUnixMs + 2);
        Assert.Equal(3, afterP3.State.Members.Count);

        var beginLoading = RoomStateMachine.BeginLoading(
            afterP3.State, "owner", null, ManifestVersion, ManifestHash, NowTicks + 3, NowUnixMs + 3, LoadingTimeoutMs);
        Assert.True(beginLoading.Applied);
        Assert.Equal(RoomPhase.Loading, beginLoading.State.Phase);
        var generation = beginLoading.State.Launch.Generation;
        Assert.Equal(1, generation);

        // 仅 owner + player2 report。
        var afterOwnerReport = RoomStateMachine.ReportAssetsLoaded(
            beginLoading.State, "owner", generation, ManifestVersion, ManifestHash, NowTicks + 4, NowUnixMs + 4);
        var afterP2Report = RoomStateMachine.ReportAssetsLoaded(
            afterOwnerReport.State, "player2", generation, ManifestVersion, ManifestHash, NowTicks + 5, NowUnixMs + 5);
        Assert.Equal(RoomPhase.Loading, afterP2Report.State.Phase);

        // player3 未 report，时间超过 deadline。
        var deadline = beginLoading.State.Launch.DeadlineUnixMs;
        var tick = RoomStateMachine.Tick(afterP2Report.State, NowTicks + 6, nowUnixMs: deadline + 1, offlineGraceMs: 0);

        Assert.True(tick.Applied);
        Assert.Equal(RoomPhase.Lobby, tick.State.Phase);
        Assert.Equal("LoadingTimeout", tick.State.PhaseReason);
        // generation 不回退（保留递增值）。
        Assert.Equal(generation, tick.State.Launch.Generation);
        // AssetsLoaded 清除。
        Assert.All(tick.State.Members, member => Assert.False(member.State.AssetsLoaded));
        // roster 清空。
        Assert.Empty(tick.State.Launch.LockedRoster);
    }

    /// <summary>
    /// owner + player2 在 Lobby，owner Leave -> player2 成为新 owner（按 JoinOrdinal）。
    /// 验证新 owner 可以 BeginLoading。
    /// </summary>
    [Fact]
    public void FullFlow_OwnerLeavesInLobby_OwnershipMigratesToNextJoiner()
    {
        var state = CreateLobbyState(ownerAccountId: "owner");

        var afterOwner = RoomStateMachine.Join(state, "owner", false, NowTicks, NowUnixMs);
        var afterP2 = RoomStateMachine.Join(afterOwner.State, "player2", false, NowTicks + 1, NowUnixMs + 1);
        Assert.Equal("owner", afterP2.State.Summary.OwnerAccountId);

        // owner Leave。
        var ownerLeft = RoomStateMachine.Leave(afterP2.State, "owner", NowUnixMs + 2);
        Assert.True(ownerLeft.Applied);
        Assert.Equal(RoomPhase.Lobby, ownerLeft.State.Phase);
        // player2 成为新 owner（JoinOrdinal 最小的在线成员）。
        Assert.Equal("player2", ownerLeft.State.Summary.OwnerAccountId);
        Assert.Single(ownerLeft.State.Members);

        // 新 owner 可以 BeginLoading。
        var beginLoading = RoomStateMachine.BeginLoading(
            ownerLeft.State, "player2", null, ManifestVersion, ManifestHash, NowTicks + 3, NowUnixMs + 3, LoadingTimeoutMs);
        Assert.True(beginLoading.Applied);
        Assert.Equal(RoomPhase.Loading, beginLoading.State.Phase);
        Assert.Equal("player2", beginLoading.State.Launch.LockedRoster[0]);
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

    private static RoomGameplayCommandRequest CreateLoadout(
        string accountId,
        int heroId,
        int teamId)
    {
        return RoomGameplayCommandRequest.CreateMobaLoadout(
            accountId,
            heroId,
            teamId,
            spawnPointId: 1,
            level: 1,
            attributeTemplateId: 2001,
            basicAttackSkillId: 3001,
            skillIds: new[] { 3002, 3003 });
    }
}
