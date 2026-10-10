using System;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Abstractions;

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

#if UNITY_5_3_OR_NEWER
    internal struct FormalLobbyAutomationContext
    {
        public FormalLobbyRuntime Runtime;
        public LobbyRoomDirectoryRuntime Directory;
        public MultiplayerRoomFlowController Controller;
        public FormalLobbyCommandCoordinator Commands;
        public ClientRoomPushSynchronizer PushSynchronizer;
        public IMultiplayerGatewayDiagnostics Gateway;
        public BattleGatewayConfigSO Config;
        public DemoMultiplayerLaunchRequest LaunchRequest;
        public Action<string, Func<LobbyOperationContext, Task>> StartOperation;
        public Func<LobbyOperationContext, Task> RefreshRooms;
        public Func<LobbyOperationContext, Task> CreateRoom;
    }

    internal sealed class FormalLobbyAutomationRuntime
    {
        private const long RoomListRefreshMilliseconds = 3000L;
        private const long SnapshotRecoveryMilliseconds = 5000L;
        private long _nextSnapshotRecoveryUnixMs;

        public void Reset() => _nextSnapshotRecoveryUnixMs = 0L;

        public void Tick(in FormalLobbyAutomationContext context)
        {
            TryPrepare(context);
            TryStartMatch(context);
            TryRecoverSnapshot(context);
            TryRefreshRooms(context);
            TryCreateRoom(context);
        }

        private static void TryPrepare(FormalLobbyAutomationContext context)
        {
            var controller = context.Controller;
            var runtime = context.Runtime;
            if (context.LaunchRequest?.SuppressAutomaticLobbyActions == true ||
                context.Config?.AutoReadyDefaultLoadout != true ||
                controller?.CurrentState != MultiplayerRoomFlowState.InLobby ||
                runtime?.IsOperationBusy == true || context.Commands == null)
                return;

            var roomId = controller.CurrentRoomId;
            if (string.IsNullOrWhiteSpace(roomId) ||
                string.Equals(runtime.PreparedRoomId, roomId, StringComparison.Ordinal))
                return;

            var localPlayer = FormalLobbyFeature.FindLocalPlayer(
                controller.CurrentSnapshot,
                controller.LocalPlayerId,
                context.LaunchRequest?.AccountId);
            if (localPlayer?.LobbyReady == true && localPlayer.HeroId > 0)
            {
                runtime.MarkPrepared(roomId);
                return;
            }

            runtime.MarkPrepared(roomId);
            context.StartOperation?.Invoke("Preparing player",
                operation => context.Commands.PrepareDefaultLoadoutAsync(
                    context.Config.BuildDefaultLoadout(),
                    context.Config.BuildSecondPlayerLoadout(), operation));
        }

        private static void TryStartMatch(FormalLobbyAutomationContext context)
        {
            var controller = context.Controller;
            var snapshot = controller?.CurrentSnapshot;
            if (!LobbyAutomationPolicy.ShouldStart(
                    context.LaunchRequest?.SuppressAutomaticLobbyActions != true &&
                    context.Config?.AutoStartWhenReady == true,
                    controller?.CurrentState ?? MultiplayerRoomFlowState.Idle,
                    controller?.IsLocalRoomOwner == true,
                    snapshot,
                    context.Config?.MinPlayers ?? 0,
                    context.Runtime?.AutomaticStartRoomId,
                    context.Runtime?.IsOperationBusy == true) || context.Commands == null)
                return;

            context.Runtime.MarkAutomaticStart(snapshot.RoomId);
            context.StartOperation?.Invoke("Starting match",
                context.Commands.BeginAutomaticLoadingAsync);
        }

        private void TryRecoverSnapshot(FormalLobbyAutomationContext context)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (context.PushSynchronizer == null ||
                context.Controller?.CurrentState != MultiplayerRoomFlowState.InLobby ||
                context.Gateway?.ConnectionState != ConnectionState.Connected ||
                context.Runtime?.IsOperationBusy == true ||
                now < _nextSnapshotRecoveryUnixMs)
                return;

            _nextSnapshotRecoveryUnixMs = now + SnapshotRecoveryMilliseconds;
            var synchronizer = context.PushSynchronizer;
            context.StartOperation?.Invoke("Synchronizing room",
                operation => synchronizer.TryRefreshAfterSilenceAsync(
                    TimeSpan.FromMilliseconds(SnapshotRecoveryMilliseconds),
                    operation.CancellationToken));
        }

        private static void TryRefreshRooms(FormalLobbyAutomationContext context)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (context.Directory == null || !LobbyAutomationPolicy.ShouldRefreshDirectory(
                    context.Controller?.CurrentState ?? MultiplayerRoomFlowState.Idle,
                    context.Gateway?.ConnectionState == ConnectionState.Connected,
                    context.Runtime?.IsOperationBusy == true,
                    context.Directory.IsBusy,
                    context.Directory.LastRefreshUnixMs,
                    now,
                    RoomListRefreshMilliseconds))
                return;

            context.StartOperation?.Invoke("Refreshing rooms", context.RefreshRooms);
        }

        private static void TryCreateRoom(FormalLobbyAutomationContext context)
        {
            if (context.Directory == null || !LobbyAutomationPolicy.ShouldCreateRoom(
                    context.Config?.AutoCreateWhenEmpty == true,
                    context.Controller?.CurrentState ?? MultiplayerRoomFlowState.Idle,
                    context.Gateway?.ConnectionState == ConnectionState.Connected,
                    context.Runtime?.IsOperationBusy == true,
                    context.Directory.IsLoaded,
                    context.Directory.Rooms.Count,
                    context.Runtime?.AutomaticCreateAttempted == true))
                return;

            context.Runtime.MarkAutomaticCreateAttempted();
            context.StartOperation?.Invoke("Creating room", context.CreateRoom);
        }
    }
#endif
}
