using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal static class FormalLobbyRenderer
    {
        public static void Draw(
            in FormalLobbyPresentationSnapshot snapshot,
            Action ready,
            Action start,
            Action leaveAndCreate,
            Action leave)
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

            var previousEnabled = GUI.enabled;
            GUILayout.Space(8f);
            if (!state.LocalReady)
            {
                GUI.enabled = previousEnabled && state.CanReady && !snapshot.OperationBusy;
                if (GUILayout.Button("Ready", GUILayout.Height(34f))) ready?.Invoke();
                GUI.enabled = previousEnabled;
            }
            else
            {
                GUILayout.Label("Ready");
            }

            if (snapshot.IsLocalRoomOwner)
            {
                GUI.enabled = previousEnabled && state.CanStart && !snapshot.OperationBusy;
                if (GUILayout.Button("Start Match", GUILayout.Height(38f))) start?.Invoke();
                GUI.enabled = previousEnabled;
                GUILayout.Label(state.ActionStatus);
            }
            else if (snapshot.OwnerAbsent)
            {
                GUILayout.Label(state.ActionStatus);
                GUILayout.Label("This room cannot be started.");
                GUI.enabled = previousEnabled && snapshot.CanLeave && !snapshot.OperationBusy;
                if (GUILayout.Button("Leave & Create Room", GUILayout.Height(34f)))
                {
                    leaveAndCreate?.Invoke();
                }
                GUI.enabled = previousEnabled;
            }
            else
            {
                GUILayout.Label(state.ActionStatus);
            }

            GUI.enabled = previousEnabled && snapshot.CanLeave && !snapshot.OperationBusy;
            if (GUILayout.Button("Leave Room", GUILayout.Height(30f))) leave?.Invoke();
            GUI.enabled = previousEnabled;
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
    }
}
