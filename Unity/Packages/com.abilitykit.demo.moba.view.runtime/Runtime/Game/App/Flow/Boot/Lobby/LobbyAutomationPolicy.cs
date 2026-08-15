using System;

namespace AbilityKit.Game.Flow
{
    internal static class LobbyAutomationPolicy
    {
        public static bool ShouldStart(
            bool enabled,
            MultiplayerRoomFlowState state,
            bool isLocalRoomOwner,
            MultiplayerRoomSnapshot snapshot,
            int minPlayers,
            string attemptedRoomId,
            bool operationBusy)
        {
            return enabled &&
                   state == MultiplayerRoomFlowState.InLobby &&
                   isLocalRoomOwner &&
                   snapshot?.CanStart == true &&
                   minPlayers > 0 &&
                   snapshot.Players?.Count >= minPlayers &&
                   !string.IsNullOrWhiteSpace(snapshot.RoomId) &&
                   !string.Equals(snapshot.RoomId, attemptedRoomId, StringComparison.Ordinal) &&
                   !operationBusy;
        }

        public static bool ShouldRefreshDirectory(
            MultiplayerRoomFlowState state,
            bool connected,
            bool operationBusy,
            bool directoryBusy,
            long lastRefreshUnixMs,
            long nowUnixMs,
            long intervalMilliseconds)
        {
            return state == MultiplayerRoomFlowState.Idle &&
                   connected &&
                   !operationBusy &&
                   !directoryBusy &&
                   lastRefreshUnixMs > 0L &&
                   nowUnixMs - lastRefreshUnixMs >= intervalMilliseconds;
        }

        public static bool ShouldCreateRoom(
            bool enabled,
            MultiplayerRoomFlowState state,
            bool connected,
            bool operationBusy,
            bool directoryLoaded,
            int openRoomCount,
            bool attempted)
        {
            return enabled &&
                   state == MultiplayerRoomFlowState.Idle &&
                   connected &&
                   !operationBusy &&
                   directoryLoaded &&
                   openRoomCount == 0 &&
                   !attempted;
        }
    }
}
