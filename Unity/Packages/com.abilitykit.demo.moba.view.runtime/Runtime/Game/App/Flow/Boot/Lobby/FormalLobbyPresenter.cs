using System;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal static class FormalLobbyPresenter
    {
        public static FormalLobbyScreenSnapshot BuildScreen(
            in FormalLobbyScreenInput input)
        {
            var connected = input.ConnectionState == ConnectionState.Connected;
            var content = ResolveScreenContent(in input);
            var rooms = BuildRoomItems(
                input.Rooms,
                content == FormalLobbyScreenContent.RoomBrowser &&
                connected &&
                !input.OperationBusy);
            var hasDeadline = input.LoadingDeadlineUnixMs > 0L;
            var secondsRemaining = hasDeadline
                ? Math.Max(0L, input.LoadingDeadlineUnixMs - input.NowUnixMs) / 1000L
                : 0L;
            var error = !string.IsNullOrWhiteSpace(input.OperationError)
                ? input.OperationError
                : input.ControllerError;

            return new FormalLobbyScreenSnapshot(
                content,
                FormatConnection(input.ConnectionState),
                input.ReconnectExhausted,
                input.RecoveryInProgress
                    ? "Restoring multiplayer session..."
                    : string.Empty,
                input.ConfigurationError,
                error,
                input.OperationBusy && !string.IsNullOrWhiteSpace(input.OperationLabel)
                    ? input.OperationLabel + "..."
                    : string.Empty,
                input.Notice,
                FormatStatus(content, input.FlowState),
                rooms,
                input.DirectoryLoaded,
                input.AutoCreateWhenEmpty,
                canExit: !input.OperationBusy,
                canCreateRoom:
                    content == FormalLobbyScreenContent.RoomBrowser &&
                    connected &&
                    !input.OperationBusy,
                canRefreshRooms:
                    content == FormalLobbyScreenContent.RoomBrowser &&
                    connected &&
                    !input.OperationBusy,
                canCancelLoading:
                    content == FormalLobbyScreenContent.Loading &&
                    input.IsLocalRoomOwner &&
                    !input.OperationBusy,
                canLeaveCurrentRoom:
                    content == FormalLobbyScreenContent.Loading &&
                    input.CanLeaveCurrentRoom &&
                    !input.OperationBusy,
                canReturnToRooms:
                    content == FormalLobbyScreenContent.Failed &&
                    !input.OperationBusy,
                loadingProgress: Math.Max(0, Math.Min(100, input.LoadingProgress)),
                input.LoadingAssetKey,
                hasDeadline,
                secondsRemaining,
                input.Lobby);
        }

        public static FormalLobbyPresentationState Build(
            MultiplayerRoomSnapshot snapshot,
            MultiplayerRoomPlayerSnapshot localPlayer,
            bool isLocalRoomOwner,
            int maxPlayers,
            int minPlayers,
            ConnectionState connectionState,
            bool snapshotIsStale,
            long lastSnapshotReceivedAtUnixMs,
            long nowUnixMs)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var players = snapshot.Players ?? Array.Empty<MultiplayerRoomPlayerSnapshot>();
            var onlinePlayerCount = 0;
            var readyPlayerCount = 0;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!player.IsOnline) continue;
                onlinePlayerCount++;
                if (player.LobbyReady && player.HeroId > 0) readyPlayerCount++;
            }

            maxPlayers = Math.Max(Math.Max(1, maxPlayers), players.Count);
            minPlayers = Math.Max(1, Math.Min(minPlayers, maxPlayers));
            var localReady = localPlayer?.LobbyReady == true && localPlayer.HeroId > 0;
            var updatesCurrent = connectionState == ConnectionState.Connected && !snapshotIsStale;
            var canReady = localPlayer != null && !localReady && updatesCurrent;
            var canNotReady = localPlayer != null && localReady && updatesCurrent;
            var canStart = isLocalRoomOwner &&
                           localReady &&
                           updatesCurrent &&
                           onlinePlayerCount >= minPlayers &&
                           readyPlayerCount == onlinePlayerCount &&
                           snapshot.CanStart;

            string actionStatus;
            if (connectionState != ConnectionState.Connected)
            {
                actionStatus = "Waiting for the room connection.";
            }
            else if (snapshotIsStale)
            {
                actionStatus = "Synchronizing the latest room state.";
            }
            else if (localPlayer == null)
            {
                actionStatus = "Waiting for local player assignment.";
            }
            else if (!localReady)
            {
                actionStatus = "Ready up to join the match.";
            }
            else if (isLocalRoomOwner)
            {
                if (onlinePlayerCount < minPlayers)
                {
                    actionStatus = $"Waiting for players ({onlinePlayerCount}/{minPlayers}).";
                }
                else if (readyPlayerCount < onlinePlayerCount)
                {
                    var remaining = onlinePlayerCount - readyPlayerCount;
                    actionStatus = remaining == 1
                        ? "Waiting for 1 player to be ready."
                        : $"Waiting for {remaining} players to be ready.";
                }
                else
                {
                    actionStatus = snapshot.CanStart
                        ? "All players are ready."
                        : "Waiting for server start confirmation.";
                }
            }
            else
            {
                actionStatus = FormalLobbyDecision.IsOwnerAbsent(snapshot)
                    ? "Room owner is offline or absent."
                    : "Waiting for room owner to start.";
            }

            return new FormalLobbyPresentationState(
                FormatPhase(snapshot.Phase),
                isLocalRoomOwner ? "Owner" : "Member",
                FormatRoomSyncStatus(
                    snapshot.RoomRevision,
                    connectionState,
                    snapshotIsStale,
                    lastSnapshotReceivedAtUnixMs,
                    nowUnixMs),
                actionStatus,
                players.Count,
                onlinePlayerCount,
                readyPlayerCount,
                maxPlayers,
                minPlayers,
                localReady,
                canReady,
                canNotReady,
                canStart);
        }

        public static string[] BuildPlayerLabels(
            MultiplayerRoomSnapshot snapshot,
            MultiplayerRoomPlayerSnapshot localPlayer)
        {
            var players = snapshot?.Players;
            if (players == null || players.Count == 0)
            {
                return Array.Empty<string>();
            }

            var labels = new string[players.Count];
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var owner = string.Equals(player.AccountId, snapshot.OwnerAccountId, StringComparison.Ordinal)
                    ? " | Owner"
                    : string.Empty;
                var local = ReferenceEquals(player, localPlayer) ? " | You" : string.Empty;
                var status = !player.IsOnline
                    ? "Offline"
                    : player.LobbyReady && player.HeroId > 0
                        ? "Ready"
                        : "Preparing";
                labels[i] = $"{player.AccountId}{local}{owner}   Hero {player.HeroId}   {status}";
            }

            return labels;
        }

        private static FormalLobbyScreenContent ResolveScreenContent(
            in FormalLobbyScreenInput input)
        {
            if (!string.IsNullOrWhiteSpace(input.ConfigurationError))
            {
                return FormalLobbyScreenContent.ConfigurationError;
            }
            if (!input.ControllerAvailable)
            {
                return FormalLobbyScreenContent.Unavailable;
            }

            return input.FlowState switch
            {
                MultiplayerRoomFlowState.Idle => FormalLobbyScreenContent.RoomBrowser,
                MultiplayerRoomFlowState.LoggingIn => FormalLobbyScreenContent.Status,
                MultiplayerRoomFlowState.CreatingRoom => FormalLobbyScreenContent.Status,
                MultiplayerRoomFlowState.JoiningRoom => FormalLobbyScreenContent.Status,
                MultiplayerRoomFlowState.LeavingRoom => FormalLobbyScreenContent.Status,
                MultiplayerRoomFlowState.InLobby => input.Lobby.HasValue
                    ? FormalLobbyScreenContent.Lobby
                    : FormalLobbyScreenContent.LobbySynchronizing,
                MultiplayerRoomFlowState.LoadingAssets => FormalLobbyScreenContent.Loading,
                MultiplayerRoomFlowState.WaitingForBattle => FormalLobbyScreenContent.Loading,
                MultiplayerRoomFlowState.Failed => FormalLobbyScreenContent.Failed,
                _ => FormalLobbyScreenContent.Transition
            };
        }

        private static string FormatConnection(ConnectionState state)
        {
            return state switch
            {
                ConnectionState.Connected => "Online",
                ConnectionState.Connecting => "Connecting",
                _ => "Offline"
            };
        }

        private static string FormatStatus(
            FormalLobbyScreenContent content,
            MultiplayerRoomFlowState state)
        {
            if (content == FormalLobbyScreenContent.Unavailable)
            {
                return "Room flow is unavailable.";
            }
            if (content == FormalLobbyScreenContent.LobbySynchronizing)
            {
                return "Synchronizing room...";
            }
            if (content == FormalLobbyScreenContent.Failed)
            {
                return "The multiplayer flow could not continue.";
            }
            if (content == FormalLobbyScreenContent.Transition)
            {
                return "Entering battle...";
            }

            return state switch
            {
                MultiplayerRoomFlowState.LoggingIn => "Authenticating session...",
                MultiplayerRoomFlowState.CreatingRoom => "Creating room...",
                MultiplayerRoomFlowState.JoiningRoom => "Joining room...",
                MultiplayerRoomFlowState.LeavingRoom => "Leaving room...",
                MultiplayerRoomFlowState.LoadingAssets => "Loading battle assets",
                MultiplayerRoomFlowState.WaitingForBattle => "Waiting for battle server",
                _ => string.Empty
            };
        }

        private static FormalLobbyRoomItem[] BuildRoomItems(
            System.Collections.Generic.IReadOnlyList<DemoRoomSummary> rooms,
            bool commandsEnabled)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return Array.Empty<FormalLobbyRoomItem>();
            }

            var items = new FormalLobbyRoomItem[rooms.Count];
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                items[i] = new FormalLobbyRoomItem(
                    room.RoomId,
                    room.DisplayName,
                    $"{room.PlayerCount}/{room.MaxPlayers} players",
                    commandsEnabled && room.HasOpenSlot);
            }

            return items;
        }

        private static string FormatPhase(MultiplayerRoomPhase phase)
        {
            return phase switch
            {
                MultiplayerRoomPhase.Lobby => "Lobby",
                MultiplayerRoomPhase.Loading => "Loading",
                MultiplayerRoomPhase.Starting => "Starting",
                MultiplayerRoomPhase.InBattle => "In Battle",
                MultiplayerRoomPhase.Closing => "Closing",
                MultiplayerRoomPhase.Closed => "Closed",
                MultiplayerRoomPhase.Expired => "Expired",
                _ => phase.ToString()
            };
        }

        private static string FormatRoomSyncStatus(
            long revision,
            ConnectionState connectionState,
            bool snapshotIsStale,
            long receivedAtUnixMs,
            long nowUnixMs)
        {
            if (connectionState != ConnectionState.Connected)
            {
                return $"Room updates: Paused | Revision {revision}";
            }

            if (snapshotIsStale)
            {
                return $"Room updates: Catching up | Revision {revision}";
            }

            if (receivedAtUnixMs <= 0L)
            {
                return $"Room updates: Synchronizing | Revision {revision}";
            }

            var ageSeconds = Math.Max(0L, nowUnixMs - receivedAtUnixMs) / 1000L;
            var age = ageSeconds <= 1L
                ? "just now"
                : ageSeconds < 60L
                    ? $"{ageSeconds}s ago"
                    : $"{ageSeconds / 60L}m ago";
            return $"Room updates: Live | Revision {revision} | {age}";
        }
    }
}
