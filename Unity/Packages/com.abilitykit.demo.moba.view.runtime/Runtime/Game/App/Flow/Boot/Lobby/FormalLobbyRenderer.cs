using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal readonly struct FormalLobbyRenderCommands
    {
        public FormalLobbyRenderCommands(
            Action exit,
            Action reconnect,
            Action createRoom,
            Action refreshRooms,
            Action<string> joinRoom,
            Action ready,
            Action notReady,
            Action start,
            Action leaveAndCreate,
            Action leave,
            Action cancelLoading,
            Action returnToRooms)
        {
            Exit = exit;
            Reconnect = reconnect;
            CreateRoom = createRoom;
            RefreshRooms = refreshRooms;
            JoinRoom = joinRoom;
            Ready = ready;
            NotReady = notReady;
            Start = start;
            LeaveAndCreate = leaveAndCreate;
            Leave = leave;
            CancelLoading = cancelLoading;
            ReturnToRooms = returnToRooms;
        }

        public Action Exit { get; }
        public Action Reconnect { get; }
        public Action CreateRoom { get; }
        public Action RefreshRooms { get; }
        public Action<string> JoinRoom { get; }
        public Action Ready { get; }
        public Action NotReady { get; }
        public Action Start { get; }
        public Action LeaveAndCreate { get; }
        public Action Leave { get; }
        public Action CancelLoading { get; }
        public Action ReturnToRooms { get; }
    }

    internal static class FormalLobbyRenderer
    {
        private const float WindowWidth = 460f;
        private const float WindowHeight = 570f;

        public static void Draw(
            in FormalLobbyScreenSnapshot snapshot,
            in FormalLobbyRenderCommands commands,
            ref Vector2 roomScroll)
        {
            var width = Mathf.Min(WindowWidth, Mathf.Max(300f, Screen.width - 24f));
            var height = Mathf.Min(WindowHeight, Mathf.Max(360f, Screen.height - 24f));
            var x = Mathf.Max(12f, (Screen.width - width) * 0.5f);
            var y = Mathf.Max(12f, (Screen.height - height) * 0.5f);

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.window);
            try
            {
                DrawHeader(snapshot.CanExit, commands.Exit);
                DrawConnection(snapshot, commands.Reconnect);

                if (snapshot.Content == FormalLobbyScreenContent.ConfigurationError)
                {
                    GUILayout.Space(10f);
                    GUILayout.Label("Multiplayer is not configured correctly.");
                    GUILayout.Label(snapshot.ConfigurationError);
                    return;
                }

                DrawTransientStatus(snapshot);
                GUILayout.Space(8f);
                DrawContent(snapshot, commands, ref roomScroll);
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static void DrawHeader(bool canExit, Action exit)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("MOBA MULTIPLAYER");
            GUILayout.FlexibleSpace();
            DrawButton("Back", canExit, exit, GUILayout.Width(64f), GUILayout.Height(24f));
            GUILayout.EndHorizontal();
        }

        private static void DrawConnection(
            in FormalLobbyScreenSnapshot snapshot,
            Action reconnect)
        {
            GUILayout.Label($"Gateway: {snapshot.ConnectionLabel}");
            if (snapshot.CanReconnect)
            {
                GUILayout.Label("Connection recovery stopped.");
                if (GUILayout.Button("Reconnect", GUILayout.Height(28f)))
                {
                    reconnect?.Invoke();
                }
            }
            else if (!string.IsNullOrWhiteSpace(snapshot.RecoveryStatus))
            {
                GUILayout.Label(snapshot.RecoveryStatus);
            }
        }

        private static void DrawTransientStatus(in FormalLobbyScreenSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.Error))
            {
                GUILayout.Space(6f);
                GUILayout.Label(snapshot.Error);
            }
            if (!string.IsNullOrWhiteSpace(snapshot.OperationStatus))
            {
                GUILayout.Space(6f);
                GUILayout.Label(snapshot.OperationStatus);
            }
            if (!string.IsNullOrWhiteSpace(snapshot.Notice))
            {
                GUILayout.Space(6f);
                GUILayout.Label(snapshot.Notice);
            }
        }

        private static void DrawContent(
            in FormalLobbyScreenSnapshot snapshot,
            in FormalLobbyRenderCommands commands,
            ref Vector2 roomScroll)
        {
            switch (snapshot.Content)
            {
                case FormalLobbyScreenContent.RoomBrowser:
                    DrawRoomBrowser(snapshot, commands, ref roomScroll);
                    break;
                case FormalLobbyScreenContent.Lobby:
                    if (snapshot.Lobby.HasValue)
                    {
                        DrawLobby(snapshot.Lobby.Value, commands);
                    }
                    break;
                case FormalLobbyScreenContent.Loading:
                    DrawLoading(snapshot, commands);
                    break;
                case FormalLobbyScreenContent.Failed:
                    GUILayout.Label(snapshot.StatusLabel);
                    DrawButton(
                        "Return to Rooms",
                        snapshot.CanReturnToRooms,
                        commands.ReturnToRooms,
                        GUILayout.Height(32f));
                    break;
                default:
                    GUILayout.Label(snapshot.StatusLabel);
                    break;
            }
        }

        private static void DrawRoomBrowser(
            in FormalLobbyScreenSnapshot snapshot,
            in FormalLobbyRenderCommands commands,
            ref Vector2 roomScroll)
        {
            DrawButton(
                "Create Room",
                snapshot.CanCreateRoom,
                commands.CreateRoom,
                GUILayout.Height(38f));

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Open Rooms");
            GUILayout.FlexibleSpace();
            DrawButton(
                "Refresh",
                snapshot.CanRefreshRooms,
                commands.RefreshRooms,
                GUILayout.Width(72f),
                GUILayout.Height(24f));
            GUILayout.EndHorizontal();

            if (snapshot.DirectoryLoaded && snapshot.Rooms.Count == 0)
            {
                GUILayout.Label(snapshot.AutoCreateWhenEmpty
                    ? "No open rooms. Creating a new room to host..."
                    : "No open rooms. Click \"Create Room\" to host.");
            }

            roomScroll = GUILayout.BeginScrollView(roomScroll, GUILayout.Height(300f));
            var joinRoom = commands.JoinRoom;
            for (var i = 0; i < snapshot.Rooms.Count; i++)
            {
                var room = snapshot.Rooms[i];
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical();
                GUILayout.Label(room.DisplayName);
                GUILayout.Label(room.PlayerSummary);
                GUILayout.EndVertical();
                var roomId = room.RoomId;
                DrawButton(
                    "Join",
                    room.CanJoin,
                    () => joinRoom?.Invoke(roomId),
                    GUILayout.Width(64f),
                    GUILayout.Height(34f));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private static void DrawLobby(
            in FormalLobbyPresentationSnapshot snapshot,
            in FormalLobbyRenderCommands commands)
        {
            var state = snapshot.State;
            GUILayout.Label(string.IsNullOrWhiteSpace(snapshot.RoomId)
                ? "Room"
                : $"Room {snapshot.RoomId}");
            GUILayout.Label($"Status: {state.PhaseLabel}   You: {state.RoleLabel}");
            GUILayout.Label(
                $"Players: {state.PlayerCount}/{state.MaxPlayers}   " +
                $"Ready: {state.ReadyPlayerCount}/{state.OnlinePlayerCount}");
            GUILayout.Label(state.SyncStatus);
            DrawPlayers(snapshot.PlayerLabels);

            GUILayout.Space(8f);
            if (!state.LocalReady)
            {
                DrawButton(
                    "Ready",
                    state.CanReady && !snapshot.OperationBusy,
                    commands.Ready,
                    GUILayout.Height(34f));
            }
            else
            {
                DrawButton(
                    "Not Ready",
                    state.CanNotReady && !snapshot.OperationBusy,
                    commands.NotReady,
                    GUILayout.Height(34f));
            }

            if (snapshot.IsLocalRoomOwner)
            {
                DrawButton(
                    "Start Match",
                    state.CanStart && !snapshot.OperationBusy,
                    commands.Start,
                    GUILayout.Height(38f));
                GUILayout.Label(state.ActionStatus);
            }
            else if (snapshot.OwnerAbsent)
            {
                GUILayout.Label(state.ActionStatus);
                GUILayout.Label("This room cannot be started.");
                DrawButton(
                    "Leave & Create Room",
                    snapshot.CanLeave && !snapshot.OperationBusy,
                    commands.LeaveAndCreate,
                    GUILayout.Height(34f));
            }
            else
            {
                GUILayout.Label(state.ActionStatus);
            }

            DrawButton(
                "Leave Room",
                snapshot.CanLeave && !snapshot.OperationBusy,
                commands.Leave,
                GUILayout.Height(30f));
        }

        private static void DrawLoading(
            in FormalLobbyScreenSnapshot snapshot,
            in FormalLobbyRenderCommands commands)
        {
            GUILayout.Label(snapshot.StatusLabel);
            GUILayout.Label(string.IsNullOrWhiteSpace(snapshot.LoadingAssetKey)
                ? $"Local progress: {snapshot.LoadingProgress}%"
                : $"Local progress: {snapshot.LoadingProgress}%  {snapshot.LoadingAssetKey}");
            DrawProgressBar(snapshot.LoadingProgress);
            if (snapshot.HasLoadingDeadline)
            {
                GUILayout.Label($"Time remaining: {snapshot.LoadingSecondsRemaining}s");
            }

            DrawButton(
                "Cancel Match Start",
                snapshot.CanCancelLoading,
                commands.CancelLoading,
                GUILayout.Height(30f));
            DrawButton(
                "Leave Room",
                snapshot.CanLeaveCurrentRoom,
                commands.Leave,
                GUILayout.Height(30f));
        }

        private static void DrawPlayers(IReadOnlyList<string> playerLabels)
        {
            if (playerLabels == null || playerLabels.Count == 0)
            {
                GUILayout.Label("Waiting for room members...");
                return;
            }

            GUILayout.Space(8f);
            for (var i = 0; i < playerLabels.Count; i++)
            {
                GUILayout.Label(playerLabels[i]);
            }
        }

        private static void DrawProgressBar(int progress)
        {
            var value = Mathf.Clamp(progress, 0, 100);
            var rect = GUILayoutUtility.GetRect(1f, 20f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, string.Empty);
            var innerWidth = Mathf.Max(0f, rect.width - 4f);
            var fill = new Rect(
                rect.x + 2f,
                rect.y + 2f,
                innerWidth * value / 100f,
                rect.height - 4f);
            if (fill.width > 0f) GUI.Box(fill, string.Empty);
            GUI.Label(rect, value + "%");
        }

        private static void DrawButton(
            string label,
            bool enabled,
            Action command,
            params GUILayoutOption[] options)
        {
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && enabled;
            if (GUILayout.Button(label, options)) command?.Invoke();
            GUI.enabled = previousEnabled;
        }
    }
}
