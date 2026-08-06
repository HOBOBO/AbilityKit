using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class GatewayMultiplayerRoomSessionTests
{
    [Fact]
    public async Task CreateRoom_ForwardsFormalBattleMetadataAsRoomTags()
    {
        var client = new StubGatewayRoomClient();
        var session = NewSession(client, new ClientRoomStore());
        var spec = NewSpec();
        spec.GameplayId = 11;
        spec.RuleSetId = 12;
        spec.ConfigVersion = 13;
        spec.ProtocolVersion = 14;
        spec.WorldType = "moba-test";
        spec.ClientId = "client-test";

        await session.CreateRoomAsync(spec, CancellationToken.None);

        Assert.Equal("11", client.CreateTags[RoomTagKeys.GameplayId]);
        Assert.Equal("12", client.CreateTags[RoomTagKeys.RuleSetId]);
        Assert.Equal("13", client.CreateTags[RoomTagKeys.ConfigVersion]);
        Assert.Equal("14", client.CreateTags[RoomTagKeys.ProtocolVersion]);
        Assert.Equal("moba-test", client.CreateTags[RoomTagKeys.WorldType]);
        Assert.Equal("client-test", client.CreateTags[RoomTagKeys.ClientId]);
    }

    [Fact]
    public void SnapshotProvider_FirstStoreApply_PublishesProjectedSnapshot()
    {
        var store = new ClientRoomStore();
        using var provider = new ClientRoomSnapshotProvider(store);
        MultiplayerRoomSnapshot? published = null;
        provider.OnSnapshotChanged += snapshot => published = snapshot;

        var result = store.ApplySnapshot(Snapshot(ClientRoomPhase.Loading, revision: 3, sequence: 1));

        Assert.Equal(ClientRoomSnapshotApplyResult.Applied, result);
        Assert.NotNull(published);
        Assert.Equal("room-1", published!.RoomId);
        Assert.Equal(MultiplayerRoomPhase.Loading, published.Phase);
        Assert.Equal(3, published.RoomRevision);
    }

    [Fact]
    public void SnapshotProvider_ProjectsAuthoritativeMemberPresenceAndLoadout()
    {
        var store = new ClientRoomStore();
        using var provider = new ClientRoomSnapshotProvider(store);
        var snapshot = Snapshot(ClientRoomPhase.Loading, revision: 3, sequence: 1);
        snapshot.OwnerAccountId = "owner-1";
        snapshot.Members = new[] { "owner-1" };
        snapshot.LoadingDeadlineUnixMs = 123456L;
        snapshot.Players = new[]
        {
            new ClientRoomPlayer
            {
                AccountId = "owner-1",
                PlayerId = 9,
                HeroId = 10001,
                LobbyReady = true,
                AssetsLoaded = true,
                IsOnline = false,
                OfflineSinceTicks = 77
            }
        };

        store.ApplySnapshot(snapshot);

        var projected = provider.Current!;
        Assert.Equal("owner-1", projected.OwnerAccountId);
        Assert.Equal(123456L, projected.LoadingDeadlineUnixMs);
        Assert.Single(projected.Players);
        Assert.Equal(10001, projected.Players[0].HeroId);
        Assert.True(projected.Players[0].AssetsLoaded);
        Assert.False(projected.Players[0].IsOnline);
        Assert.Equal(77, projected.Players[0].OfflineSinceTicks);
    }

    [Fact]
    public void ReliableEventCheckpointStore_IsScopedByBattleIdentity()
    {
        var session = NewSession(
            new StubGatewayRoomClient(),
            new ClientRoomStore());
        var checkpoint = new MobaReliableBattleEventCheckpoint(
            "battle-1",
            "epoch-7",
            19);

        session.Save(in checkpoint);

        Assert.True(session.TryLoad("battle-1", out var restored));
        Assert.Equal("epoch-7", restored.Epoch);
        Assert.Equal(19, restored.LastAcknowledgedSequence);
        Assert.False(session.TryLoad("battle-2", out _));
    }

    [Fact]
    public async Task PickHero_HeroConflictFailsControllerWithoutRefreshingAsSuccess()
    {
        var client = new StubGatewayRoomClient
        {
            PickHeroResult = new GatewayRoomSnapshotResult(
                success: false,
                applied: false,
                errorCode: 8,
                message: "Hero 1001 is already selected by another player on team 1.",
                roomId: "room-1",
                numericRoomId: 1UL)
        };
        client.Snapshots.Enqueue(Snapshot(ClientRoomPhase.Lobby, revision: 4, sequence: 4));
        var store = new ClientRoomStore();
        var session = NewSession(client, store);
        await session.JoinRoomAsync(NewSpec(), "room-1", CancellationToken.None);
        using var provider = new ClientRoomSnapshotProvider(store);
        using var controller = new MultiplayerRoomFlowController(session, provider);
        controller.RestoreFromSnapshot();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.PickHeroAsync(new MultiplayerLoadoutSpec(
                heroId: 1001,
                teamId: 1,
                spawnPointId: 1,
                level: 1,
                attributeTemplateId: 2001,
                basicAttackSkillId: 3001,
                skillIds: new[] { 3002, 3003 })));

        Assert.Contains("failed (8)", exception.Message);
        Assert.Contains("already selected", exception.Message);
        Assert.Equal(MultiplayerRoomFlowState.Failed, controller.CurrentState);
        Assert.Equal(exception.Message, controller.LastError);
        Assert.Equal(1, client.GetSnapshotCalls);
        Assert.Equal(4, store.Current.RoomRevision);
    }

    [Fact]
    public async Task ReportAssetsLoaded_UsesAuthoritativeManifestIdentity()
    {
        var client = new StubGatewayRoomClient();
        client.Snapshots.Enqueue(Snapshot(ClientRoomPhase.Lobby, revision: 1, sequence: 1));
        var store = new ClientRoomStore();
        var session = NewSession(client, store);
        var spec = NewSpec();

        await session.JoinRoomAsync(spec, "room-1", CancellationToken.None);
        store.ApplySnapshot(Snapshot(ClientRoomPhase.Loading, revision: 2, sequence: 2));
        await session.ReportAssetsLoadedAsync("room-1", CancellationToken.None);

        Assert.Equal(7, client.ReportGeneration);
        Assert.Equal(4, client.ReportManifestVersion);
        Assert.Equal("manifest-hash", client.ReportManifestHash);
        Assert.StartsWith("assets-loaded:", client.ReportCommandId);
    }

    [Fact]
    public async Task CancelLoading_UsesAuthoritativeRevisionAndAppliesLobbySnapshot()
    {
        var client = new StubGatewayRoomClient();
        client.Snapshots.Enqueue(Snapshot(ClientRoomPhase.Lobby, revision: 1, sequence: 1));
        var store = new ClientRoomStore();
        var session = NewSession(client, store);
        await session.JoinRoomAsync(NewSpec(), "room-1", CancellationToken.None);
        store.ApplySnapshot(Snapshot(ClientRoomPhase.Loading, revision: 9, sequence: 2));

        await session.CancelLoadingAsync("room-1", CancellationToken.None);

        Assert.Equal(9, client.CancelExpectedRevision);
        Assert.StartsWith("cancel-loading:", client.CancelCommandId);
        Assert.Equal(ClientRoomPhase.Lobby, store.Current.Phase);
    }

    [Fact]
    public async Task LeaveRoom_SuccessClearsAuthoritativeMembershipAndStore()
    {
        var client = new StubGatewayRoomClient();
        client.Snapshots.Enqueue(Snapshot(ClientRoomPhase.Lobby, revision: 6, sequence: 1));
        var store = new ClientRoomStore();
        var session = NewSession(client, store);
        await session.JoinRoomAsync(NewSpec(), "room-1", CancellationToken.None);

        await session.LeaveRoomAsync("room-1", CancellationToken.None);

        Assert.Equal(6, client.LeaveExpectedRevision);
        Assert.StartsWith("leave-room:", client.LeaveCommandId);
        Assert.Equal(string.Empty, session.CurrentRoomId);
        Assert.Equal(0UL, session.CurrentNumericRoomId);
        Assert.Equal(0U, session.CurrentPlayerId);
        Assert.Null(store.Current);
    }

    [Fact]
    public async Task LeaveRoom_FailurePreservesAuthoritativeMembershipAndStore()
    {
        var client = new StubGatewayRoomClient
        {
            LeaveResult = new GatewayRoomOperationResult(
                false, false, 3, "leave rejected", 6, null!)
        };
        client.Snapshots.Enqueue(Snapshot(ClientRoomPhase.Lobby, revision: 6, sequence: 1));
        var store = new ClientRoomStore();
        var session = NewSession(client, store);
        await session.JoinRoomAsync(NewSpec(), "room-1", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.LeaveRoomAsync("room-1", CancellationToken.None));

        Assert.Equal("room-1", session.CurrentRoomId);
        Assert.Equal(1UL, session.CurrentNumericRoomId);
        Assert.Equal(7U, session.CurrentPlayerId);
        Assert.NotNull(store.Current);
    }

    [Fact]
    public async Task WaitForBattleStart_WaitsPastStartingUntilCommittedIdentityExists()
    {
        var client = new StubGatewayRoomClient();
        client.Snapshots.Enqueue(Snapshot(ClientRoomPhase.Lobby, revision: 1, sequence: 1));
        client.Snapshots.Enqueue(Snapshot(ClientRoomPhase.Starting, revision: 2, sequence: 2));
        var inBattle = Snapshot(
            ClientRoomPhase.InBattle,
            revision: 3,
            sequence: 3,
            battleId: "battle-1",
            worldId: 42);
        client.Snapshots.Enqueue(inBattle);
        var store = new ClientRoomStore();
        var session = NewSession(client, store);

        await session.JoinRoomAsync(NewSpec(), "room-1", CancellationToken.None);
        var waiting = session.WaitForBattleStartAsync("room-1", CancellationToken.None);
        await client.StartingSnapshotRead.Task.WaitAsync(TimeSpan.FromSeconds(1));
        store.ApplySnapshot(inBattle);
        await waiting;

        Assert.Equal(3, client.GetSnapshotCalls);
        Assert.Equal(ClientRoomPhase.InBattle, store.Current.Phase);
        Assert.Equal("battle-1", store.Current.BattleId);
        Assert.Equal(42UL, store.Current.WorldId);
    }

    [Theory]
    [InlineData(ClientRoomPhase.Lobby, MultiplayerRoomRestoreNextStep.SetReadyAndBeginLoading)]
    [InlineData(ClientRoomPhase.Loading, MultiplayerRoomRestoreNextStep.ReportAssetsLoaded)]
    [InlineData(ClientRoomPhase.Starting, MultiplayerRoomRestoreNextStep.WaitForBattleStart)]
    [InlineData(ClientRoomPhase.InBattle, MultiplayerRoomRestoreNextStep.EnterBattle)]
    public async Task Restore_MapsFrameworkNextStep_WithoutExecutingPendingStage(
        ClientRoomPhase phase,
        MultiplayerRoomRestoreNextStep expectedNextStep)
    {
        var client = new StubGatewayRoomClient();
        var battleId = phase == ClientRoomPhase.InBattle ? "battle-1" : string.Empty;
        var worldId = phase == ClientRoomPhase.InBattle ? 42UL : 0UL;
        var restoredSnapshot = Snapshot(phase, revision: 5, sequence: 8, battleId, worldId);
        restoredSnapshot.OwnerAccountId = "owner-1";
        restoredSnapshot.Members = new[] { "owner-1" };
        restoredSnapshot.Players = new[]
        {
            new ClientRoomPlayer
            {
                AccountId = "owner-1",
                PlayerId = 15u,
                HeroId = 10001,
                LobbyReady = true,
                IsOnline = true,
                LastSeenTicks = 1234L
            }
        };
        client.RestoreResult = new GatewayRestoreRoomResult(
            true,
            true,
            phase == ClientRoomPhase.InBattle,
            "room-1",
            77UL,
            restoredSnapshot,
            default,
            string.Empty,
            phase == ClientRoomPhase.InBattle ? RoomGatewayJoinKind.Reconnect : RoomGatewayJoinKind.TeamLobby,
            123L,
            15u);
        client.Snapshots.Enqueue(restoredSnapshot);
        var store = new ClientRoomStore();
        var session = NewSession(client, store);

        var result = await session.RestoreAsync(NewSpec(), 9u, CancellationToken.None);

        Assert.True(result.HasActiveRoom);
        Assert.Equal(expectedNextStep, result.NextStep);
        Assert.Equal(15u, result.PlayerId);
        Assert.Equal(77UL, store.Current.NumericRoomId);
        Assert.Equal(phase, store.Current.Phase);
        Assert.Equal("owner-1", store.Current.OwnerAccountId);
        Assert.Single(store.Current.Members);
        Assert.Single(store.Current.Players);
        Assert.Equal(10001, store.Current.Players[0].HeroId);
        Assert.Equal(1234L, store.Current.Players[0].LastSeenTicks);
        Assert.Equal(1, client.RestoreCalls);
        Assert.Equal(1, client.GetSnapshotCalls);
        Assert.Equal(0, client.SetReadyCalls);
        Assert.Equal(0, client.BeginLoadingCalls);
        Assert.Equal(0, client.ReportAssetsLoadedCalls);
    }

    [Fact]
    public async Task Restore_NoActiveRoom_ResetsStaleRoomAndDoesNotPullSnapshot()
    {
        var client = new StubGatewayRoomClient
        {
            RestoreResult = new GatewayRestoreRoomResult(
                true,
                false,
                false,
                string.Empty,
                0UL,
                null!,
                default,
                "no active room",
                RoomGatewayJoinKind.TeamLobby,
                0L,
                9u)
        };
        var store = new ClientRoomStore();
        store.ApplySnapshot(Snapshot(ClientRoomPhase.Lobby, 99, 99));
        var session = NewSession(client, store);

        var result = await session.RestoreAsync(NewSpec(), 9u, CancellationToken.None);

        Assert.Equal(MultiplayerRoomRestoreStatus.NoActiveRoom, result.Status);
        Assert.False(result.CanRetry);
        Assert.Null(store.Current);
        Assert.Equal(0, client.GetSnapshotCalls);
    }

    [Fact]
    public async Task Restore_Timeout_ReturnsRetryableDiagnostic()
    {
        var client = new StubGatewayRoomClient
        {
            RestoreException = new TimeoutException("restore timeout")
        };
        var session = NewSession(client, new ClientRoomStore());

        var result = await session.RestoreAsync(NewSpec(), 9u, CancellationToken.None);

        Assert.Equal(MultiplayerRoomRestoreStatus.Timeout, result.Status);
        Assert.Equal(MultiplayerRoomRestoreErrorCode.Timeout, result.ErrorCode);
        Assert.True(result.CanRetry);
        Assert.Contains("restore timeout", result.Message);
    }

    [Fact]
    public async Task Restore_ProtocolFailure_PreservesRetryableInternalDiagnostic()
    {
        var client = new StubGatewayRoomClient
        {
            RestoreResult = new GatewayRestoreRoomResult(
                false,
                false,
                false,
                string.Empty,
                0UL,
                null!,
                default,
                "restore service unavailable",
                RoomGatewayJoinKind.TeamLobby,
                0L,
                9u)
        };
        var session = NewSession(client, new ClientRoomStore());

        var result = await session.RestoreAsync(NewSpec(), 9u, CancellationToken.None);

        Assert.Equal(MultiplayerRoomRestoreStatus.Failed, result.Status);
        Assert.Equal(MultiplayerRoomRestoreErrorCode.InternalError, result.ErrorCode);
        Assert.True(result.CanRetry);
        Assert.Equal(0, client.GetSnapshotCalls);
    }

    [Fact]
    public async Task PushSynchronizer_IgnoresNonRoomPush()
    {
        var client = new StubGatewayRoomClient();
        var store = new ClientRoomStore();
        var refreshCalls = 0;
        var synchronizer = new ClientRoomPushSynchronizer(
            client,
            store,
            _ =>
            {
                refreshCalls++;
                return Task.CompletedTask;
            });

        var handled = await synchronizer.HandleServerPushAsync(123u, default);

        Assert.False(handled);
        Assert.Null(store.Current);
        Assert.Equal(0, refreshCalls);
    }

    [Fact]
    public async Task PushSynchronizer_RevisionGapsUseCompletePushWithoutRefresh()
    {
        var client = new StubGatewayRoomClient();
        client.PushSnapshots.Enqueue(Snapshot(ClientRoomPhase.Lobby, 3, 3));
        client.PushSnapshots.Enqueue(Snapshot(ClientRoomPhase.Lobby, 5, 5));
        var store = new ClientRoomStore();
        store.ApplySnapshot(Snapshot(ClientRoomPhase.Lobby, 1, 1));
        var refreshCalls = 0;
        var synchronizer = new ClientRoomPushSynchronizer(
            client,
            store,
            _ =>
            {
                refreshCalls++;
                return Task.CompletedTask;
            });

        var first = synchronizer.HandleServerPushAsync(StubGatewayRoomClient.RoomPushOpCode, default);
        var second = synchronizer.HandleServerPushAsync(StubGatewayRoomClient.RoomPushOpCode, default);

        Assert.Equal(0, refreshCalls);
        Assert.Equal(5, store.Current.RoomRevision);
        Assert.False(store.IsStale);
        Assert.Equal(0, synchronizer.RefreshFallbackCount);
        Assert.True(await first);
        Assert.True(await second);
    }

    private static GatewayMultiplayerRoomSession NewSession(
        StubGatewayRoomClient client,
        ClientRoomStore store)
    {
        return new GatewayMultiplayerRoomSession(
            client,
            store,
            requestTimeout: TimeSpan.FromSeconds(1),
            pollInterval: TimeSpan.FromMilliseconds(1),
            battleStartTimeout: TimeSpan.FromSeconds(1));
    }

    private static MultiplayerRoomLaunchSpec NewSpec()
    {
        return new MultiplayerRoomLaunchSpec
        {
            SessionToken = "token-1",
            Region = "dev",
            ServerId = "server-1",
            RoomType = "moba",
            RoomTitle = "test",
            MaxPlayers = 2
        };
    }

    private static ClientRoomSnapshot Snapshot(
        ClientRoomPhase phase,
        long revision,
        long sequence,
        string battleId = "",
        ulong worldId = 0)
    {
        return new ClientRoomSnapshot
        {
            RoomId = "room-1",
            Phase = phase,
            LaunchGeneration = 7,
            LaunchManifestVersion = 4,
            LaunchManifestHash = "manifest-hash",
            RoomRevision = revision,
            LastEventSequence = sequence,
            BattleId = battleId,
            WorldId = worldId
        };
    }

    private sealed class StubGatewayRoomClient : IGatewayRoomClient
    {
        public const uint RoomPushOpCode = 777u;

        public readonly Queue<ClientRoomSnapshot> Snapshots = new();
        public readonly Queue<ClientRoomSnapshot> PushSnapshots = new();
        public int GetSnapshotCalls { get; private set; }
        public IReadOnlyDictionary<string, string> CreateTags { get; private set; } =
            new Dictionary<string, string>();
        public TaskCompletionSource<bool> StartingSnapshotRead { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RestoreCalls { get; private set; }
        public int SetReadyCalls { get; private set; }
        public int BeginLoadingCalls { get; private set; }
        public int ReportAssetsLoadedCalls { get; private set; }
        public long ReportGeneration { get; private set; }
        public int ReportManifestVersion { get; private set; }
        public string ReportManifestHash { get; private set; } = string.Empty;
        public string ReportCommandId { get; private set; } = string.Empty;
        public long? CancelExpectedRevision { get; private set; }
        public string CancelCommandId { get; private set; } = string.Empty;
        public long? LeaveExpectedRevision { get; private set; }
        public string LeaveCommandId { get; private set; } = string.Empty;
        public GatewayRoomOperationResult LeaveResult { get; set; } =
            new GatewayRoomOperationResult(true, true, 0, string.Empty, 7, null!);
        public Exception? RestoreException { get; set; }
        public GatewayRestoreRoomResult RestoreResult { get; set; }
        public GatewayRoomSnapshotResult PickHeroResult { get; set; } =
            new GatewayRoomSnapshotResult("room-1", 1UL);

        public Task<GatewayTimeSyncResult> TimeSyncAsync(uint timeSyncOpCode, long clientSendTicks, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string> GuestLoginAsync(uint guestLoginOpCode, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => Task.FromResult("guest-token");

        public Task<GatewayCreateRoomResult> CreateRoomAsync(string sessionToken, string region, string serverId, string roomType, string title, bool isPublic, int maxPlayers, IReadOnlyDictionary<string, string> tags, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            CreateTags = tags;
            return Task.FromResult(new GatewayCreateRoomResult("room-1", 1));
        }

        public Task<GatewayJoinRoomResult> JoinRoomAsync(string sessionToken, string region, string serverId, string roomId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var anchor = default(GatewayWorldStartAnchor);
            return Task.FromResult(new GatewayJoinRoomResult(
                true, roomId, 1, string.Empty, in anchor, string.Empty,
                string.Empty, false, 0L, 0UL, 7U));
        }

        public Task<GatewayRoomOperationResult> LeaveRoomAsync(string sessionToken, string roomId, long? expectedRevision, string commandId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            LeaveExpectedRevision = expectedRevision;
            LeaveCommandId = commandId;
            return Task.FromResult(LeaveResult);
        }

        public Task<GatewayRoomSnapshotResult> SetReadyAsync(string sessionToken, string roomId, bool ready, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            SetReadyCalls++;
            return Task.FromResult(new GatewayRoomSnapshotResult(roomId, 1));
        }

        public Task<GatewayRoomSnapshotResult> PickHeroAsync(string sessionToken, string roomId, int heroId, int teamId, int spawnPointId, int level, int attributeTemplateId, int basicAttackSkillId, IReadOnlyList<int> skillIds, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => Task.FromResult(PickHeroResult);

        public Task<GatewayRoomOperationResult> BeginLoadingAsync(string sessionToken, string roomId, long? expectedRevision, string commandId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            BeginLoadingCalls++;
            return Task.FromResult(new GatewayRoomOperationResult(true, true, 0, string.Empty, expectedRevision ?? 0, null!));
        }

        public Task<GatewayRoomOperationResult> ReportAssetsLoadedAsync(string sessionToken, string roomId, long launchGeneration, int manifestVersion, string manifestHash, string commandId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ReportAssetsLoadedCalls++;
            ReportGeneration = launchGeneration;
            ReportManifestVersion = manifestVersion;
            ReportManifestHash = manifestHash;
            ReportCommandId = commandId;
            return Task.FromResult(new GatewayRoomOperationResult(true, true, 0, string.Empty, 2, Snapshot(ClientRoomPhase.Starting, 3, 3)));
        }

        public Task<GatewayRoomOperationResult> CancelLoadingAsync(string sessionToken, string roomId, long? expectedRevision, string commandId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            CancelExpectedRevision = expectedRevision;
            CancelCommandId = commandId;
            return Task.FromResult(new GatewayRoomOperationResult(
                true,
                true,
                0,
                string.Empty,
                (expectedRevision ?? 0) + 1,
                Snapshot(ClientRoomPhase.Lobby, (expectedRevision ?? 0) + 1, 3)));
        }

        public Task<GatewayRoomOperationResult> ReportLoadingProgressAsync(string sessionToken, string roomId, long launchGeneration, int manifestVersion, string manifestHash, int progress, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GatewayRoomOperationResult(
                true,
                true,
                0,
                string.Empty,
                2,
                Snapshot(ClientRoomPhase.Loading, 2, 2)));
        }

        public Task<GatewayGetSnapshotResult> GetSnapshotAsync(string sessionToken, string roomId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            GetSnapshotCalls++;
            var snapshot = Snapshots.Dequeue();
            if (snapshot.Phase == ClientRoomPhase.Starting)
            {
                StartingSnapshotRead.TrySetResult(true);
            }
            return Task.FromResult(new GatewayGetSnapshotResult(true, roomId, 1, snapshot, string.Empty));
        }

        public Task<GatewayRestoreRoomResult> RestoreRoomAsync(string sessionToken, string region, string serverId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            RestoreCalls++;
            if (RestoreException != null) throw RestoreException;
            return Task.FromResult(RestoreResult);
        }

        public ClientRoomSnapshot DeserializeRoomStateChangedPush(ArraySegment<byte> payload)
            => PushSnapshots.Dequeue();

        public bool IsRoomStateChangedPush(uint opCode) => opCode == RoomPushOpCode;
    }
}
