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

    internal readonly struct FormalLobbyConnectionInput
    {
        public FormalLobbyConnectionInput(string configurationError, ConnectionState connectionState,
            bool reconnectExhausted, bool recoveryInProgress)
        {
            ConfigurationError = configurationError ?? string.Empty;
            ConnectionState = connectionState;
            ReconnectExhausted = reconnectExhausted;
            RecoveryInProgress = recoveryInProgress;
        }

        public string ConfigurationError { get; }
        public ConnectionState ConnectionState { get; }
        public bool ReconnectExhausted { get; }
        public bool RecoveryInProgress { get; }
    }

    internal readonly struct FormalLobbyOperationInput
    {
        public FormalLobbyOperationInput(string operationError, string controllerError,
            string operationLabel, string notice, bool operationBusy, bool controllerAvailable)
        {
            OperationError = operationError ?? string.Empty;
            ControllerError = controllerError ?? string.Empty;
            OperationLabel = operationLabel ?? string.Empty;
            Notice = notice ?? string.Empty;
            OperationBusy = operationBusy;
            ControllerAvailable = controllerAvailable;
        }

        public string OperationError { get; }
        public string ControllerError { get; }
        public string OperationLabel { get; }
        public string Notice { get; }
        public bool OperationBusy { get; }
        public bool ControllerAvailable { get; }
    }

    internal readonly struct FormalLobbyRoomInput
    {
        public FormalLobbyRoomInput(MultiplayerRoomFlowState flowState, bool directoryLoaded,
            bool autoCreateWhenEmpty, IReadOnlyList<DemoRoomSummary> rooms,
            bool isLocalRoomOwner, bool canLeaveCurrentRoom, FormalLobbyPresentationSnapshot? lobby)
        {
            FlowState = flowState;
            DirectoryLoaded = directoryLoaded;
            AutoCreateWhenEmpty = autoCreateWhenEmpty;
            Rooms = rooms ?? Array.Empty<DemoRoomSummary>();
            IsLocalRoomOwner = isLocalRoomOwner;
            CanLeaveCurrentRoom = canLeaveCurrentRoom;
            Lobby = lobby;
        }

        public MultiplayerRoomFlowState FlowState { get; }
        public bool DirectoryLoaded { get; }
        public bool AutoCreateWhenEmpty { get; }
        public IReadOnlyList<DemoRoomSummary> Rooms { get; }
        public bool IsLocalRoomOwner { get; }
        public bool CanLeaveCurrentRoom { get; }
        public FormalLobbyPresentationSnapshot? Lobby { get; }
    }

    internal readonly struct FormalLobbyLoadingInput
    {
        public FormalLobbyLoadingInput(int progress, string assetKey, long deadlineUnixMs, long nowUnixMs)
        {
            Progress = progress;
            AssetKey = assetKey ?? string.Empty;
            DeadlineUnixMs = deadlineUnixMs;
            NowUnixMs = nowUnixMs;
        }

        public int Progress { get; }
        public string AssetKey { get; }
        public long DeadlineUnixMs { get; }
        public long NowUnixMs { get; }
    }

    internal readonly struct FormalLobbyScreenInput
    {
        public FormalLobbyScreenInput(
            in FormalLobbyConnectionInput connection,
            in FormalLobbyOperationInput operation,
            in FormalLobbyRoomInput room,
            in FormalLobbyLoadingInput loading)
        {
            ConfigurationError = connection.ConfigurationError;
            ConnectionState = connection.ConnectionState;
            ReconnectExhausted = connection.ReconnectExhausted;
            RecoveryInProgress = connection.RecoveryInProgress;
            OperationError = operation.OperationError;
            ControllerError = operation.ControllerError;
            OperationLabel = operation.OperationLabel;
            Notice = operation.Notice;
            OperationBusy = operation.OperationBusy;
            ControllerAvailable = operation.ControllerAvailable;
            FlowState = room.FlowState;
            DirectoryLoaded = room.DirectoryLoaded;
            AutoCreateWhenEmpty = room.AutoCreateWhenEmpty;
            Rooms = room.Rooms;
            IsLocalRoomOwner = room.IsLocalRoomOwner;
            CanLeaveCurrentRoom = room.CanLeaveCurrentRoom;
            Lobby = room.Lobby;
            LoadingProgress = loading.Progress;
            LoadingAssetKey = loading.AssetKey;
            LoadingDeadlineUnixMs = loading.DeadlineUnixMs;
            NowUnixMs = loading.NowUnixMs;
        }

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
            : this(
                new FormalLobbyConnectionInput(configurationError, connectionState, reconnectExhausted, recoveryInProgress),
                new FormalLobbyOperationInput(operationError, controllerError, operationLabel, notice, operationBusy, controllerAvailable),
                new FormalLobbyRoomInput(flowState, directoryLoaded, autoCreateWhenEmpty, rooms, isLocalRoomOwner, canLeaveCurrentRoom, lobby),
                new FormalLobbyLoadingInput(loadingProgress, loadingAssetKey, loadingDeadlineUnixMs, nowUnixMs))
        {
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

    internal readonly struct FormalLobbyScreenStatus
    {
        public FormalLobbyScreenStatus(FormalLobbyScreenContent content, string connectionLabel,
            bool canReconnect, string recoveryStatus, string configurationError,
            string error, string operationStatus, string notice, string statusLabel)
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
    }

    internal readonly struct FormalLobbyScreenDirectory
    {
        public FormalLobbyScreenDirectory(IReadOnlyList<FormalLobbyRoomItem> rooms,
            bool directoryLoaded, bool autoCreateWhenEmpty)
        {
            Rooms = rooms ?? Array.Empty<FormalLobbyRoomItem>();
            DirectoryLoaded = directoryLoaded;
            AutoCreateWhenEmpty = autoCreateWhenEmpty;
        }

        public IReadOnlyList<FormalLobbyRoomItem> Rooms { get; }
        public bool DirectoryLoaded { get; }
        public bool AutoCreateWhenEmpty { get; }
    }

    internal readonly struct FormalLobbyScreenActions
    {
        public FormalLobbyScreenActions(bool canExit, bool canCreateRoom, bool canRefreshRooms,
            bool canCancelLoading, bool canLeaveCurrentRoom, bool canReturnToRooms)
        {
            CanExit = canExit;
            CanCreateRoom = canCreateRoom;
            CanRefreshRooms = canRefreshRooms;
            CanCancelLoading = canCancelLoading;
            CanLeaveCurrentRoom = canLeaveCurrentRoom;
            CanReturnToRooms = canReturnToRooms;
        }

        public bool CanExit { get; }
        public bool CanCreateRoom { get; }
        public bool CanRefreshRooms { get; }
        public bool CanCancelLoading { get; }
        public bool CanLeaveCurrentRoom { get; }
        public bool CanReturnToRooms { get; }
    }

    internal readonly struct FormalLobbyScreenLoading
    {
        public FormalLobbyScreenLoading(int progress, string assetKey,
            bool hasDeadline, long secondsRemaining)
        {
            Progress = progress;
            AssetKey = assetKey ?? string.Empty;
            HasDeadline = hasDeadline;
            SecondsRemaining = secondsRemaining;
        }

        public int Progress { get; }
        public string AssetKey { get; }
        public bool HasDeadline { get; }
        public long SecondsRemaining { get; }
    }

    internal readonly struct FormalLobbyScreenSnapshot
    {
        public FormalLobbyScreenSnapshot(
            in FormalLobbyScreenStatus status,
            in FormalLobbyScreenDirectory directory,
            in FormalLobbyScreenActions actions,
            in FormalLobbyScreenLoading loading,
            FormalLobbyPresentationSnapshot? lobby)
        {
            Content = status.Content;
            ConnectionLabel = status.ConnectionLabel;
            CanReconnect = status.CanReconnect;
            RecoveryStatus = status.RecoveryStatus;
            ConfigurationError = status.ConfigurationError;
            Error = status.Error;
            OperationStatus = status.OperationStatus;
            Notice = status.Notice;
            StatusLabel = status.StatusLabel;
            Rooms = directory.Rooms;
            DirectoryLoaded = directory.DirectoryLoaded;
            AutoCreateWhenEmpty = directory.AutoCreateWhenEmpty;
            CanExit = actions.CanExit;
            CanCreateRoom = actions.CanCreateRoom;
            CanRefreshRooms = actions.CanRefreshRooms;
            CanCancelLoading = actions.CanCancelLoading;
            CanLeaveCurrentRoom = actions.CanLeaveCurrentRoom;
            CanReturnToRooms = actions.CanReturnToRooms;
            LoadingProgress = loading.Progress;
            LoadingAssetKey = loading.AssetKey;
            HasLoadingDeadline = loading.HasDeadline;
            LoadingSecondsRemaining = loading.SecondsRemaining;
            Lobby = lobby;
        }

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
            : this(
                new FormalLobbyScreenStatus(content, connectionLabel, canReconnect, recoveryStatus,
                    configurationError, error, operationStatus, notice, statusLabel),
                new FormalLobbyScreenDirectory(rooms, directoryLoaded, autoCreateWhenEmpty),
                new FormalLobbyScreenActions(canExit, canCreateRoom, canRefreshRooms, canCancelLoading,
                    canLeaveCurrentRoom, canReturnToRooms),
                new FormalLobbyScreenLoading(loadingProgress, loadingAssetKey,
                    hasLoadingDeadline, loadingSecondsRemaining),
                lobby)
        {
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
