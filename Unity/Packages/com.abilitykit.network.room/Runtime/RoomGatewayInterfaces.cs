#nullable enable
#pragma warning disable CS1591

using System;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Network.Room
{
    public interface IRoomGatewaySnapshotFeed
    {
        RoomGatewaySnapshot? Current { get; }
        event Action<RoomGatewaySnapshot>? SnapshotChanged;
    }

    public interface IRoomGatewaySessionClientBase
    {
        Task<RoomGatewayCreateResult> CreateRoomAsync(RoomGatewayCreateRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        Task<RoomGatewayJoinResult> JoinRoomAsync(RoomGatewayJoinRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        Task<RoomGatewayLeaveResult> LeaveRoomAsync(RoomGatewayLeaveRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        Task<RoomGatewayReadyResult> SetReadyAsync(RoomGatewayReadyRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(RoomGatewayRestoreRoomRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(RoomGatewayGetSnapshotRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }

    public interface IRoomGatewayHeroPickCapability
    {
        Task<RoomGatewayPickHeroResult> PickHeroAsync(RoomGatewayPickHeroRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }

    public interface IRoomGatewayStagedLoadingCapability
    {
        Task<RoomGatewayBeginLoadingResult> BeginLoadingAsync(RoomGatewayBeginLoadingRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        Task<RoomGatewayReportLoadingProgressResult> ReportLoadingProgressAsync(RoomGatewayReportLoadingProgressRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        Task<RoomGatewayReportAssetsLoadedResult> ReportAssetsLoadedAsync(RoomGatewayReportAssetsLoadedRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        Task<RoomGatewayCancelLoadingResult> CancelLoadingAsync(RoomGatewayCancelLoadingRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }

    public interface IRoomGatewayDirectBattleStartCapability
    {
        Task<RoomGatewayStartBattleResult> StartBattleAsync(RoomGatewayStartBattleRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }

    public interface IRoomGatewayStateSyncSubscriptionCapability
    {
        Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(RoomGatewayStateSyncSubscriptionRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }

    public interface IRoomGatewaySessionClient :
        IRoomGatewaySessionClientBase,
        IRoomGatewayHeroPickCapability,
        IRoomGatewayStagedLoadingCapability,
        IRoomGatewayDirectBattleStartCapability,
        IRoomGatewayStateSyncSubscriptionCapability
    {
    }
}
