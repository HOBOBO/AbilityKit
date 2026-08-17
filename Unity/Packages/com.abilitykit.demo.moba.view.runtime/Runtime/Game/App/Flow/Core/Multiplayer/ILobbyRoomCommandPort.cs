using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    internal interface ILobbyRoomCommandPort
    {
        MultiplayerRoomFlowState CurrentState { get; }
        MultiplayerRoomSnapshot CurrentSnapshot { get; }
        string CurrentRoomId { get; }
        uint LocalPlayerId { get; }
        bool CanLeaveCurrentRoom { get; }

        Task<MultiplayerRoomRestoreResult> RestoreAsync(
            MultiplayerRoomLaunchSpec spec,
            uint fallbackPlayerId,
            CancellationToken cancellationToken = default);

        Task StartCreateRoomAsync(
            MultiplayerRoomLaunchSpec spec,
            CancellationToken cancellationToken = default);

        Task StartJoinRoomAsync(
            MultiplayerRoomLaunchSpec spec,
            string roomId,
            CancellationToken cancellationToken = default);

        Task PickHeroAsync(
            MultiplayerLoadoutSpec loadout,
            CancellationToken cancellationToken = default);

        Task SetReadyAsync(
            bool ready,
            CancellationToken cancellationToken = default);

        Task BeginLoadingAsync(CancellationToken cancellationToken = default);
        Task CancelLoadingAsync(CancellationToken cancellationToken = default);
        Task LeaveRoomAsync(CancellationToken cancellationToken = default);
        void Cancel();
    }
}
