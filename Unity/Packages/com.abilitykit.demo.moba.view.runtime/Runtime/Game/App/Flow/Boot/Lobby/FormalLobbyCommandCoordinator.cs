using System;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    internal sealed class FormalLobbyCommandCoordinator
    {
        private readonly FormalLobbyRuntime _runtime;
        private readonly ILobbyRoomCommandPort _room;

        public FormalLobbyCommandCoordinator(
            FormalLobbyRuntime runtime,
            ILobbyRoomCommandPort room)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public async Task InitializeAsync(
            MultiplayerRoomLaunchSpec spec,
            bool restoreRoomOnEntry,
            uint restoreFallbackPlayerId,
            Func<LobbyOperationContext, Task> refreshRooms,
            LobbyOperationContext operationContext)
        {
            if (!IsCurrent(operationContext)) return;
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            if (restoreRoomOnEntry)
            {
                var restored = await _room.RestoreAsync(
                    spec,
                    restoreFallbackPlayerId,
                    operationContext.CancellationToken);
                if (!IsCurrent(operationContext)) return;
                if (restored.HasActiveRoom) return;
                if (_room.CurrentState == MultiplayerRoomFlowState.Failed)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(restored.Message)
                            ? $"Room restore failed: {restored.Status}."
                            : restored.Message);
                }
            }

            await RefreshAsync(refreshRooms, operationContext);
        }

        public async Task PrepareDefaultLoadoutAsync(
            MultiplayerLoadoutSpec configuredLoadout,
            LobbyOperationContext operationContext)
        {
            try
            {
                await _room.PickHeroAsync(
                    FormalLobbyDecision.ResolveAvailableDefaultLoadout(
                        configuredLoadout,
                        _room.CurrentSnapshot,
                        _room.LocalPlayerId),
                    operationContext.CancellationToken);
                if (!IsCurrent(operationContext)) return;
                await _room.SetReadyAsync(true, operationContext.CancellationToken);
            }
            catch
            {
                if (IsCurrent(operationContext))
                {
                    _runtime.ClearPrepared();
                }
                throw;
            }
        }

        public async Task BeginAutomaticLoadingAsync(LobbyOperationContext operationContext)
        {
            try
            {
                await _room.BeginLoadingAsync(operationContext.CancellationToken);
            }
            catch
            {
                if (IsCurrent(operationContext))
                {
                    _runtime.ClearAutomaticStart();
                }
                throw;
            }
        }

        public Task SetReadyAsync(bool ready, LobbyOperationContext operationContext)
        {
            return _room.SetReadyAsync(ready, operationContext.CancellationToken);
        }

        public Task BeginLoadingAsync(LobbyOperationContext operationContext)
        {
            return _room.BeginLoadingAsync(operationContext.CancellationToken);
        }

        public Task CancelLoadingAsync(LobbyOperationContext operationContext)
        {
            return _room.CancelLoadingAsync(operationContext.CancellationToken);
        }

        public async Task CreateRoomAsync(
            MultiplayerRoomLaunchSpec spec,
            LobbyOperationContext operationContext)
        {
            if (!IsCurrent(operationContext)) return;
            ResetRoomAutomation();
            await _room.StartCreateRoomAsync(spec, operationContext.CancellationToken);
        }

        public async Task JoinRoomAsync(
            MultiplayerRoomLaunchSpec spec,
            string roomId,
            LobbyOperationContext operationContext)
        {
            if (!IsCurrent(operationContext)) return;
            ResetRoomAutomation();
            await _room.StartJoinRoomAsync(spec, roomId, operationContext.CancellationToken);
        }

        public async Task LeaveRoomAndRefreshAsync(
            Func<LobbyOperationContext, Task> refreshRooms,
            LobbyOperationContext operationContext)
        {
            await _room.LeaveRoomAsync(operationContext.CancellationToken);
            if (!IsCurrent(operationContext)) return;
            ResetRoomAutomation();
            await RefreshAsync(refreshRooms, operationContext);
        }

        public async Task LeaveAndCreateRoomAsync(
            MultiplayerRoomLaunchSpec spec,
            LobbyOperationContext operationContext)
        {
            if (_room.CanLeaveCurrentRoom)
            {
                await _room.LeaveRoomAsync(operationContext.CancellationToken);
                if (!IsCurrent(operationContext)) return;
            }

            await CreateRoomAsync(spec, operationContext);
        }

        public async Task ReturnToRoomsAsync(
            Func<LobbyOperationContext, Task> refreshRooms,
            LobbyOperationContext operationContext)
        {
            if (!IsCurrent(operationContext)) return;
            if (!string.IsNullOrWhiteSpace(_room.CurrentRoomId))
            {
                EnsureRoomCanBeLeft();
                await _room.LeaveRoomAsync(operationContext.CancellationToken);
                if (!IsCurrent(operationContext)) return;
            }
            else
            {
                _room.Cancel();
            }

            ResetRoomAutomation();
            await RefreshAsync(refreshRooms, operationContext);
        }

        public async Task LeaveBeforeExitAsync(LobbyOperationContext operationContext)
        {
            if (!IsCurrent(operationContext) || string.IsNullOrWhiteSpace(_room.CurrentRoomId)) return;

            EnsureRoomCanBeLeft();
            await _room.LeaveRoomAsync(operationContext.CancellationToken);
        }

        private bool IsCurrent(in LobbyOperationContext operationContext)
        {
            return _runtime.IsCurrent(operationContext);
        }

        private void EnsureRoomCanBeLeft()
        {
            if (_room.CanLeaveCurrentRoom) return;

            throw new InvalidOperationException(
                $"The room cannot be left during phase {_room.CurrentSnapshot?.Phase}.");
        }

        private void ResetRoomAutomation()
        {
            _runtime.ClearPrepared();
            _runtime.ClearAutomaticStart();
        }

        private static Task RefreshAsync(
            Func<LobbyOperationContext, Task> refreshRooms,
            LobbyOperationContext operationContext)
        {
            return refreshRooms != null
                ? refreshRooms(operationContext)
                : Task.CompletedTask;
        }
    }
}
