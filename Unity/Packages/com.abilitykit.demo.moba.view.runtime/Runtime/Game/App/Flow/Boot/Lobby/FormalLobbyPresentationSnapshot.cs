using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    internal readonly struct FormalLobbyPresentationSnapshot
    {
        public FormalLobbyPresentationSnapshot(
            string roomId,
            FormalLobbyPresentationState state,
            string[] playerLabels,
            bool isLocalRoomOwner,
            bool ownerAbsent,
            bool canLeave,
            bool operationBusy)
        {
            RoomId = roomId ?? string.Empty;
            State = state;
            PlayerLabels = Array.AsReadOnly(
                playerLabels != null
                    ? (string[])playerLabels.Clone()
                    : Array.Empty<string>());
            IsLocalRoomOwner = isLocalRoomOwner;
            OwnerAbsent = ownerAbsent;
            CanLeave = canLeave;
            OperationBusy = operationBusy;
        }

        public string RoomId { get; }
        public FormalLobbyPresentationState State { get; }
        public IReadOnlyList<string> PlayerLabels { get; }
        public bool IsLocalRoomOwner { get; }
        public bool OwnerAbsent { get; }
        public bool CanLeave { get; }
        public bool OperationBusy { get; }
    }

    internal readonly struct FormalLobbyPresentationState
    {
        public FormalLobbyPresentationState(
            string phaseLabel,
            string roleLabel,
            string syncStatus,
            string actionStatus,
            int playerCount,
            int onlinePlayerCount,
            int readyPlayerCount,
            int maxPlayers,
            int minPlayers,
            bool localReady,
            bool canReady,
            bool canStart)
        {
            PhaseLabel = phaseLabel;
            RoleLabel = roleLabel;
            SyncStatus = syncStatus;
            ActionStatus = actionStatus;
            PlayerCount = playerCount;
            OnlinePlayerCount = onlinePlayerCount;
            ReadyPlayerCount = readyPlayerCount;
            MaxPlayers = maxPlayers;
            MinPlayers = minPlayers;
            LocalReady = localReady;
            CanReady = canReady;
            CanStart = canStart;
        }

        public string PhaseLabel { get; }
        public string RoleLabel { get; }
        public string SyncStatus { get; }
        public string ActionStatus { get; }
        public int PlayerCount { get; }
        public int OnlinePlayerCount { get; }
        public int ReadyPlayerCount { get; }
        public int MaxPlayers { get; }
        public int MinPlayers { get; }
        public bool LocalReady { get; }
        public bool CanReady { get; }
        public bool CanStart { get; }
    }
}
