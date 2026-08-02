using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.View;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Gateway;

public sealed class ShooterRoomSessionControllerTests
{
    [Fact]
    public async Task Create_only_enters_lobby_without_starting_loading()
    {
        var store = new ShooterRoomSessionStore();
        var session = new FakeRoomSession(store);
        using var controller = new ShooterRoomSessionController(session, store);

        await controller.StartCreateRoomAsync(CreateLaunchSpec());

        Assert.Equal(ShooterRoomSessionState.InLobby, controller.CurrentState);
        Assert.Equal("room-1", controller.CurrentRoomId);
        Assert.Equal(17u, controller.LocalPlayerId);
        Assert.True(controller.IsLocalRoomOwner);
        Assert.Equal(1, session.CreateCalls);
        Assert.Equal(0, session.BeginLoadingCalls);
        Assert.Equal(0, session.PrepareAssetsCalls);
    }

    [Fact]
    public async Task Join_only_enters_lobby()
    {
        var store = new ShooterRoomSessionStore();
        var session = new FakeRoomSession(store);
        using var controller = new ShooterRoomSessionController(session, store);

        await controller.StartJoinRoomAsync(CreateLaunchSpec(), "room-2");

        Assert.Equal(ShooterRoomSessionState.InLobby, controller.CurrentState);
        Assert.Equal("room-2", controller.CurrentRoomId);
        Assert.Equal(1, session.JoinCalls);
        Assert.Equal(0, session.BeginLoadingCalls);
    }

    [Fact]
    public async Task Snapshot_push_drives_loading_waiting_and_battle_states()
    {
        var store = new ShooterRoomSessionStore();
        var session = new FakeRoomSession(store);
        using var controller = new ShooterRoomSessionController(session, store);
        await controller.StartCreateRoomAsync(CreateLaunchSpec());

        store.TryApply(CreateSnapshot("room-1", 2, ShooterRoomSessionPhase.Loading, assetsLoaded: false));
        Assert.Equal(ShooterRoomSessionState.LoadingAssets, controller.CurrentState);

        store.TryApply(CreateSnapshot("room-1", 3, ShooterRoomSessionPhase.Loading, assetsLoaded: true));
        Assert.Equal(ShooterRoomSessionState.WaitingForBattle, controller.CurrentState);

        store.TryApply(CreateSnapshot("room-1", 4, ShooterRoomSessionPhase.InBattle, assetsLoaded: true));
        Assert.Equal(ShooterRoomSessionState.InBattle, controller.CurrentState);
    }

    [Fact]
    public void Store_rejects_stale_revision_for_same_room()
    {
        using var store = new ShooterRoomSessionStore();
        Assert.True(store.TryApply(CreateSnapshot("room-1", 8, ShooterRoomSessionPhase.Loading, assetsLoaded: false)));
        Assert.False(store.TryApply(CreateSnapshot("room-1", 7, ShooterRoomSessionPhase.Lobby, assetsLoaded: false)));

        Assert.NotNull(store.Current);
        Assert.Equal(8, store.Current!.RoomRevision);
        Assert.Equal(ShooterRoomSessionPhase.Loading, store.Current.Phase);
    }

    [Fact]
    public async Task Owner_can_cancel_loading_and_leave_authoritatively()
    {
        var store = new ShooterRoomSessionStore();
        var session = new FakeRoomSession(store);
        using var controller = new ShooterRoomSessionController(session, store);
        await controller.StartCreateRoomAsync(CreateLaunchSpec());
        await controller.SetReadyAsync(true);
        await controller.BeginLoadingAsync();

        Assert.Equal(ShooterRoomSessionState.LoadingAssets, controller.CurrentState);
        await controller.CancelLoadingAsync();
        Assert.Equal(ShooterRoomSessionState.InLobby, controller.CurrentState);
        Assert.Equal(1, session.CancelLoadingCalls);

        await controller.LeaveRoomAsync();
        Assert.Equal(ShooterRoomSessionState.Idle, controller.CurrentState);
        Assert.False(controller.HasActiveRoom);
        Assert.Null(controller.CurrentSnapshot);
        Assert.Equal(1, session.LeaveCalls);

        store.TryApply(CreateSnapshot("room-1", 99, ShooterRoomSessionPhase.Lobby, assetsLoaded: false));
        Assert.Equal(ShooterRoomSessionState.Idle, controller.CurrentState);
        Assert.Null(controller.CurrentSnapshot);
        Assert.Null(store.Current);
    }

    [Fact]
    public async Task Leave_failure_preserves_active_room_and_previous_state()
    {
        var store = new ShooterRoomSessionStore();
        var session = new FakeRoomSession(store) { ThrowOnLeave = true };
        using var controller = new ShooterRoomSessionController(session, store);
        await controller.StartCreateRoomAsync(CreateLaunchSpec());

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.LeaveRoomAsync());

        Assert.Equal(ShooterRoomSessionState.InLobby, controller.CurrentState);
        Assert.Equal("room-1", controller.CurrentRoomId);
        Assert.NotNull(controller.CurrentSnapshot);
    }

    private static ShooterRoomSessionLaunchSpec CreateLaunchSpec()
    {
        var launchSpec = ShooterRoomLaunchSpec.CreateDefault("test-client");
        return new ShooterRoomSessionLaunchSpec("session-token", in launchSpec, 17u, TimeSpan.FromSeconds(2));
    }

    private static ShooterGatewayStagedRoomSnapshot CreateSnapshot(
        string roomId,
        long revision,
        ShooterRoomSessionPhase phase,
        bool assetsLoaded,
        bool canStart = true)
    {
        var anchor = new ShooterGatewayWorldStartAnchor(10L, 1000L, 1, 1d / 30d);
        var members = new[]
        {
            new ShooterGatewayStagedRoomPlayerSnapshot(
                "owner-account",
                17u,
                isOnline: true,
                lobbyReady: canStart,
                assetsLoaded,
                assetsLoaded ? 100 : 0)
        };
        return new ShooterGatewayStagedRoomSnapshot(
            roomId,
            (int)phase,
            string.Empty,
            phase == ShooterRoomSessionPhase.Lobby ? 0L : 1L,
            0L,
            "manifest",
            1,
            string.Empty,
            revision,
            revision,
            canStart,
            phase == ShooterRoomSessionPhase.InBattle ? "battle-1" : string.Empty,
            phase == ShooterRoomSessionPhase.InBattle ? 91ul : 0ul,
            in anchor,
            "owner-account",
            members);
    }

    private sealed class FakeRoomSession : IShooterRoomSession
    {
        private readonly ShooterRoomSessionStore _store;
        private string _roomId = "room-1";
        private long _revision;

        public FakeRoomSession(ShooterRoomSessionStore store)
        {
            _store = store;
        }

        public int CreateCalls { get; private set; }
        public int JoinCalls { get; private set; }
        public int BeginLoadingCalls { get; private set; }
        public int PrepareAssetsCalls { get; private set; }
        public int CancelLoadingCalls { get; private set; }
        public int LeaveCalls { get; private set; }
        public bool ThrowOnLeave { get; set; }

        public Task<ShooterRoomSessionJoinResult> CreateAndJoinAsync(
            ShooterRoomSessionLaunchSpec spec,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            _roomId = "room-1";
            Apply(ShooterRoomSessionPhase.Lobby, assetsLoaded: false, canStart: false);
            return Task.FromResult(new ShooterRoomSessionJoinResult(
                _roomId,
                1ul,
                17u,
                ShooterRoomGatewayEntryKind.TeamLobby,
                string.Empty,
                "created"));
        }

        public Task<ShooterRoomSessionJoinResult> JoinAsync(
            ShooterRoomSessionLaunchSpec spec,
            string roomId,
            CancellationToken cancellationToken = default)
        {
            JoinCalls++;
            _roomId = roomId;
            Apply(ShooterRoomSessionPhase.Lobby, assetsLoaded: false, canStart: false);
            return Task.FromResult(new ShooterRoomSessionJoinResult(
                roomId,
                2ul,
                17u,
                ShooterRoomGatewayEntryKind.TeamLobby,
                string.Empty,
                "joined"));
        }

        public Task SetReadyAsync(string roomId, bool ready, CancellationToken cancellationToken = default)
        {
            Apply(ShooterRoomSessionPhase.Lobby, assetsLoaded: false, canStart: ready);
            return Task.CompletedTask;
        }

        public Task BeginLoadingAsync(string roomId, long? expectedRevision, CancellationToken cancellationToken = default)
        {
            BeginLoadingCalls++;
            Apply(ShooterRoomSessionPhase.Loading, assetsLoaded: false, canStart: true);
            return Task.CompletedTask;
        }

        public Task PrepareAndReportAssetsLoadedAsync(
            ShooterRoomSessionLaunchSpec spec,
            ShooterRoomSessionSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            PrepareAssetsCalls++;
            Apply(ShooterRoomSessionPhase.Loading, assetsLoaded: true, canStart: true);
            return Task.CompletedTask;
        }

        public Task CancelLoadingAsync(string roomId, long? expectedRevision, CancellationToken cancellationToken = default)
        {
            CancelLoadingCalls++;
            Apply(ShooterRoomSessionPhase.Lobby, assetsLoaded: false, canStart: true);
            return Task.CompletedTask;
        }

        public Task LeaveRoomAsync(string roomId, long? expectedRevision, CancellationToken cancellationToken = default)
        {
            LeaveCalls++;
            if (ThrowOnLeave) throw new InvalidOperationException("leave rejected");
            return Task.CompletedTask;
        }

        public Task<ShooterRoomSessionSnapshot> RefreshAsync(string roomId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.Current ?? throw new InvalidOperationException("missing snapshot"));
        }

        public Task<ShooterRoomSessionSnapshot> WaitForBattleStartAsync(string roomId, CancellationToken cancellationToken = default)
        {
            Apply(ShooterRoomSessionPhase.InBattle, assetsLoaded: true, canStart: true);
            return Task.FromResult(_store.Current ?? throw new InvalidOperationException("missing snapshot"));
        }

        private void Apply(ShooterRoomSessionPhase phase, bool assetsLoaded, bool canStart)
        {
            _revision++;
            _store.TryApply(CreateSnapshot(_roomId, _revision, phase, assetsLoaded, canStart));
        }

        public void Dispose()
        {
        }
    }
}
