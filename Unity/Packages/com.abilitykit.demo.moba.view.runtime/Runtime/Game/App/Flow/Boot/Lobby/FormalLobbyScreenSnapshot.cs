using System;
using System.Collections.Generic;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal enum FormalLobbyScreenContent
    {
        ConfigurationError = 0,
        Unavailable = 1,
        RoomBrowser = 2,
        Status = 3,
        LobbySynchronizing = 4,
        Lobby = 5,
        Loading = 6,
        Failed = 7,
        Transition = 8
    }

    internal readonly struct FormalLobbyRoomItem
    {
        public FormalLobbyRoomItem(
            string roomId,
            string displayName,
            string playerSummary,
            bool canJoin)
        {
            RoomId = roomId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PlayerSummary = playerSummary ?? string.Empty;
            CanJoin = canJoin;
        }

        public string RoomId { get; }
        public string DisplayName { get; }
        public string PlayerSummary { get; }
        public bool CanJoin { get; }
    }

    internal readonly struct FormalLobbyScreenInput
    {
        public FormalLobbyScreenInput(
            string configurationError,
            ConnectionState connectionState,
            bool reconnectExhausted,
            bool recoveryInProgress,
            string operationError,
            string controllerError,
            string operationLabel,
            string notice,
            bool operationBusy,
            bool controllerAvailable,
            MultiplayerRoomFlowState flowState,
            bool directoryLoaded,
            bool autoCreateWhenEmpty,
            IReadOnlyList<DemoRoomSummary> rooms,
            bool isLocalRoomOwner,
            bool canLeaveCurrentRoom,
            int loadingProgress,
            string loadingAssetKey,
            long loadingDeadlineUnixMs,
            long nowUnixMs,
            FormalLobbyPresentationSnapshot? lobby)
        {
            ConfigurationError = configurationError ?? string.Empty;
            ConnectionState = connectionState;
            ReconnectExhausted = reconnectExhausted;
            RecoveryInProgress = recoveryInProgress;
            OperationError = operationError ?? string.Empty;
            ControllerError = controllerError ?? string.Empty;
            OperationLabel = operationLabel ?? string.Empty;
            Notice = notice ?? string.Empty;
            OperationBusy = operationBusy;
            ControllerAvailable = controllerAvailable;
            FlowState = flowState;
            DirectoryLoaded = directoryLoaded;
            AutoCreateWhenEmpty = autoCreateWhenEmpty;
            Rooms = rooms ?? Array.Empty<DemoRoomSummary>();
            IsLocalRoomOwner = isLocalRoomOwner;
            CanLeaveCurrentRoom = canLeaveCurrentRoom;
            LoadingProgress = loadingProgress;
            LoadingAssetKey = loadingAssetKey ?? string.Empty;
            LoadingDeadlineUnixMs = loadingDeadlineUnixMs;
            NowUnixMs = nowUnixMs;
            Lobby = lobby;
        }

        public string ConfigurationError { get; }
        public ConnectionState ConnectionState { get; }
        public bool ReconnectExhausted { get; }
        public bool RecoveryInProgress { get; }
        public string OperationError { get; }
        public string ControllerError { get; }
        public string OperationLabel { get; }
        public string Notice { get; }
        public bool OperationBusy { get; }
        public bool ControllerAvailable { get; }
        public MultiplayerRoomFlowState FlowState { get; }
        public bool DirectoryLoaded { get; }
        public bool AutoCreateWhenEmpty { get; }
        public IReadOnlyList<DemoRoomSummary> Rooms { get; }
        public bool IsLocalRoomOwner { get; }
        public bool CanLeaveCurrentRoom { get; }
        public int LoadingProgress { get; }
        public string LoadingAssetKey { get; }
        public long LoadingDeadlineUnixMs { get; }
        public long NowUnixMs { get; }
        public FormalLobbyPresentationSnapshot? Lobby { get; }
    }

    internal readonly struct FormalLobbyScreenSnapshot
    {
        public FormalLobbyScreenSnapshot(
            FormalLobbyScreenContent content,
            string connectionLabel,
            bool canReconnect,
            string recoveryStatus,
            string configurationError,
            string error,
            string operationStatus,
            string notice,
            string statusLabel,
            IReadOnlyList<FormalLobbyRoomItem> rooms,
            bool directoryLoaded,
            bool autoCreateWhenEmpty,
            bool canExit,
            bool canCreateRoom,
            bool canRefreshRooms,
            bool canCancelLoading,
            bool canLeaveCurrentRoom,
            bool canReturnToRooms,
            int loadingProgress,
            string loadingAssetKey,
            bool hasLoadingDeadline,
            long loadingSecondsRemaining,
            FormalLobbyPresentationSnapshot? lobby)
        {
            Content = content;
            ConnectionLabel = connectionLabel ?? string.Empty;
            CanReconnect = canReconnect;
            RecoveryStatus = recoveryStatus ?? string.Empty;
            ConfigurationError = configurationError ?? string.Empty;
            Error = error ?? string.Empty;
            OperationStatus = operationStatus ?? string.Empty;
            Notice = notice ?? string.Empty;
            StatusLabel = statusLabel ?? string.Empty;
            Rooms = rooms ?? Array.Empty<FormalLobbyRoomItem>();
            DirectoryLoaded = directoryLoaded;
            AutoCreateWhenEmpty = autoCreateWhenEmpty;
            CanExit = canExit;
            CanCreateRoom = canCreateRoom;
            CanRefreshRooms = canRefreshRooms;
            CanCancelLoading = canCancelLoading;
            CanLeaveCurrentRoom = canLeaveCurrentRoom;
            CanReturnToRooms = canReturnToRooms;
            LoadingProgress = loadingProgress;
            LoadingAssetKey = loadingAssetKey ?? string.Empty;
            HasLoadingDeadline = hasLoadingDeadline;
            LoadingSecondsRemaining = loadingSecondsRemaining;
            Lobby = lobby;
        }

        public FormalLobbyScreenContent Content { get; }
        public string ConnectionLabel { get; }
        public bool CanReconnect { get; }
        public string RecoveryStatus { get; }
        public string ConfigurationError { get; }
        public string Error { get; }
        public string OperationStatus { get; }
        public string Notice { get; }
        public string StatusLabel { get; }
        public IReadOnlyList<FormalLobbyRoomItem> Rooms { get; }
        public bool DirectoryLoaded { get; }
        public bool AutoCreateWhenEmpty { get; }
        public bool CanExit { get; }
        public bool CanCreateRoom { get; }
        public bool CanRefreshRooms { get; }
        public bool CanCancelLoading { get; }
        public bool CanLeaveCurrentRoom { get; }
        public bool CanReturnToRooms { get; }
        public int LoadingProgress { get; }
        public string LoadingAssetKey { get; }
        public bool HasLoadingDeadline { get; }
        public long LoadingSecondsRemaining { get; }
        public FormalLobbyPresentationSnapshot? Lobby { get; }
    }
}
