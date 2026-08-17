using System;
using System.Collections.Generic;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class FormalLobbyPureLogicTests
{
    [Fact]
    public void Presenter_LiveReadyOwnerCanStart()
    {
        var owner = Player("owner", 7u, 1001, ready: true, online: true);
        var snapshot = new MultiplayerRoomSnapshot
        {
            RoomId = "room-a",
            OwnerAccountId = owner.AccountId,
            Phase = MultiplayerRoomPhase.Lobby,
            RoomRevision = 12,
            CanStart = true,
            Players = new[]
            {
                owner,
                Player("member", 8u, 1002, ready: true, online: true)
            }
        };

        var state = FormalLobbyPresenter.Build(
            snapshot,
            owner,
            isLocalRoomOwner: true,
            maxPlayers: 2,
            minPlayers: 2,
            ConnectionState.Connected,
            snapshotIsStale: false,
            lastSnapshotReceivedAtUnixMs: 1000L,
            nowUnixMs: 1500L);

        Assert.Equal("Owner", state.RoleLabel);
        Assert.Equal(2, state.ReadyPlayerCount);
        Assert.True(state.CanStart);
        Assert.True(state.CanNotReady);
        Assert.Equal("All players are ready.", state.ActionStatus);
        Assert.Contains("Live | Revision 12 | just now", state.SyncStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void Presenter_StaleSnapshotDisablesCommands()
    {
        var owner = Player("owner", 7u, 1001, ready: true, online: true);
        var snapshot = new MultiplayerRoomSnapshot
        {
            OwnerAccountId = owner.AccountId,
            RoomRevision = 20,
            CanStart = true,
            Players = new[] { owner }
        };

        var state = FormalLobbyPresenter.Build(
            snapshot,
            owner,
            isLocalRoomOwner: true,
            maxPlayers: 2,
            minPlayers: 1,
            ConnectionState.Connected,
            snapshotIsStale: true,
            lastSnapshotReceivedAtUnixMs: 1000L,
            nowUnixMs: 2000L);

        Assert.False(state.CanStart);
        Assert.False(state.CanNotReady);
        Assert.Equal("Synchronizing the latest room state.", state.ActionStatus);
        Assert.Equal("Room updates: Catching up | Revision 20", state.SyncStatus);
    }

    [Fact]
    public void Decision_PrefersAuthenticatedAccountAndDetectsAbsentOwner()
    {
        var staleIdMatch = Player("member", 7u, 1002, ready: true, online: true);
        var expected = Player("owner", 17u, 1001, ready: true, online: false);
        var snapshot = new MultiplayerRoomSnapshot
        {
            OwnerAccountId = "owner",
            Players = new[] { staleIdMatch, expected }
        };

        var resolved = FormalLobbyDecision.FindLocalPlayer(snapshot, 7u, "owner");

        Assert.Same(expected, resolved);
        Assert.True(FormalLobbyDecision.IsOwnerAbsent(snapshot));
    }

    [Fact]
    public void NoticeFormatter_FormatsMembershipAndPlayerStateChanges()
    {
        var membership = new ClientRoomMembershipChange(
            "room-a",
            previousRevision: 10,
            currentRevision: 11,
            joinedAccountIds: new[] { "joined" },
            leftAccountIds: new[] { "left" },
            previousOwnerAccountId: "left",
            currentOwnerAccountId: "joined");
        var states = new ClientRoomPlayerStateChanges(
            "room-a",
            previousRevision: 11,
            currentRevision: 12,
            new[]
            {
                new ClientRoomPlayerStateChange(
                    "joined",
                    previousOnline: false,
                    currentOnline: true,
                    previousReady: false,
                    currentReady: true,
                    previousHeroId: 0,
                    currentHeroId: 1001)
            });

        Assert.Equal(
            "left left the room. joined joined the room. joined is now room owner.",
            LobbyNoticeFormatter.FormatMembership(membership));
        Assert.Equal(
            "joined reconnected. joined is ready.",
            LobbyNoticeFormatter.FormatPlayerState(states));
    }

    [Fact]
    public void AutomationPolicy_StartRequiresOwnerCapacityAndOneAttemptPerRoom()
    {
        var snapshot = new MultiplayerRoomSnapshot
        {
            RoomId = "room-a",
            Phase = MultiplayerRoomPhase.Lobby,
            CanStart = true,
            Players = new[]
            {
                Player("owner", 1u, 1001, ready: true, online: true),
                Player("member", 2u, 1002, ready: true, online: true)
            }
        };

        Assert.True(LobbyAutomationPolicy.ShouldStart(
            enabled: true,
            MultiplayerRoomFlowState.InLobby,
            isLocalRoomOwner: true,
            snapshot,
            minPlayers: 2,
            attemptedRoomId: string.Empty,
            operationBusy: false));
        Assert.False(LobbyAutomationPolicy.ShouldStart(
            enabled: true,
            MultiplayerRoomFlowState.InLobby,
            isLocalRoomOwner: true,
            snapshot,
            minPlayers: 2,
            attemptedRoomId: "room-a",
            operationBusy: false));
    }

    [Fact]
    public void ScreenPresenter_RoomBrowserMapsDirectoryAndEnablesConnectedCommands()
    {
        var rooms = new[]
        {
            new DemoRoomSummary(
                "cn",
                "gateway-a",
                "room-a",
                "moba",
                "Ranked Room",
                isPublic: true,
                maxPlayers: 4,
                playerCount: 2,
                ownerAccountId: "owner",
                createdAtUnixMs: 100L)
        };

        var input = ScreenInput(rooms: rooms);
        var snapshot = FormalLobbyPresenter.BuildScreen(in input);

        Assert.Equal(FormalLobbyScreenContent.RoomBrowser, snapshot.Content);
        Assert.Equal("Online", snapshot.ConnectionLabel);
        Assert.True(snapshot.CanCreateRoom);
        Assert.True(snapshot.CanRefreshRooms);
        var room = Assert.Single(snapshot.Rooms);
        Assert.Equal("room-a", room.RoomId);
        Assert.Equal("Ranked Room", room.DisplayName);
        Assert.Equal("2/4 players", room.PlayerSummary);
        Assert.True(room.CanJoin);
    }

    [Fact]
    public void ScreenPresenter_BusyStateDisablesEveryRoomBrowserCommand()
    {
        var rooms = new[]
        {
            new DemoRoomSummary(
                "cn",
                "gateway-a",
                "room-a",
                "moba",
                string.Empty,
                isPublic: true,
                maxPlayers: 2,
                playerCount: 1,
                ownerAccountId: "owner",
                createdAtUnixMs: 100L)
        };

        var input = ScreenInput(operationBusy: true, rooms: rooms);
        var snapshot = FormalLobbyPresenter.BuildScreen(in input);

        Assert.False(snapshot.CanExit);
        Assert.False(snapshot.CanCreateRoom);
        Assert.False(snapshot.CanRefreshRooms);
        Assert.False(Assert.Single(snapshot.Rooms).CanJoin);
        Assert.Equal("Working...", snapshot.OperationStatus);
    }

    [Fact]
    public void ScreenPresenter_LoadingClampsProgressAndComputesDeadline()
    {
        var input = ScreenInput(
            flowState: MultiplayerRoomFlowState.LoadingAssets,
            isLocalRoomOwner: true,
            loadingProgress: 140,
            loadingDeadlineUnixMs: 4500L,
            nowUnixMs: 1000L);

        var snapshot = FormalLobbyPresenter.BuildScreen(in input);

        Assert.Equal(FormalLobbyScreenContent.Loading, snapshot.Content);
        Assert.Equal("Loading battle assets", snapshot.StatusLabel);
        Assert.Equal(100, snapshot.LoadingProgress);
        Assert.True(snapshot.HasLoadingDeadline);
        Assert.Equal(3L, snapshot.LoadingSecondsRemaining);
        Assert.True(snapshot.CanCancelLoading);
        Assert.True(snapshot.CanLeaveCurrentRoom);
    }

    [Fact]
    public void ScreenPresenter_ConfigurationErrorTakesPriorityOverFlowState()
    {
        var input = ScreenInput(
            configurationError: "Session token is missing.",
            flowState: MultiplayerRoomFlowState.InLobby,
            controllerAvailable: false);

        var snapshot = FormalLobbyPresenter.BuildScreen(in input);

        Assert.Equal(FormalLobbyScreenContent.ConfigurationError, snapshot.Content);
        Assert.Equal("Session token is missing.", snapshot.ConfigurationError);
        Assert.False(snapshot.CanCreateRoom);
        Assert.False(snapshot.CanRefreshRooms);
        Assert.False(snapshot.CanCancelLoading);
        Assert.False(snapshot.CanReturnToRooms);
    }

    private static FormalLobbyScreenInput ScreenInput(
        string configurationError = "",
        ConnectionState connectionState = ConnectionState.Connected,
        bool operationBusy = false,
        bool controllerAvailable = true,
        MultiplayerRoomFlowState flowState = MultiplayerRoomFlowState.Idle,
        IReadOnlyList<DemoRoomSummary>? rooms = null,
        bool isLocalRoomOwner = false,
        int loadingProgress = 0,
        long loadingDeadlineUnixMs = 0L,
        long nowUnixMs = 0L)
    {
        return new FormalLobbyScreenInput(
            configurationError,
            connectionState,
            reconnectExhausted: false,
            recoveryInProgress: false,
            operationError: string.Empty,
            controllerError: string.Empty,
            operationLabel: "Working",
            notice: string.Empty,
            operationBusy,
            controllerAvailable,
            flowState,
            directoryLoaded: true,
            autoCreateWhenEmpty: false,
            rooms ?? Array.Empty<DemoRoomSummary>(),
            isLocalRoomOwner,
            canLeaveCurrentRoom: true,
            loadingProgress,
            loadingAssetKey: string.Empty,
            loadingDeadlineUnixMs,
            nowUnixMs,
            lobby: null);
    }

    private static MultiplayerRoomPlayerSnapshot Player(
        string accountId,
        uint playerId,
        int heroId,
        bool ready,
        bool online)
    {
        return new MultiplayerRoomPlayerSnapshot
        {
            AccountId = accountId,
            PlayerId = playerId,
            HeroId = heroId,
            LobbyReady = ready,
            IsOnline = online
        };
    }
}
