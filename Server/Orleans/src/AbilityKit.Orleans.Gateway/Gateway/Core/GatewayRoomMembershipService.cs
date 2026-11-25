using AbilityKit.Orleans.Contracts.Rooms;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Core;

public sealed record GatewayRoomLeaveResult(
    string? ActiveRoomId,
    RoomOperationResult Operation);

/// <summary>
/// 负责网关侧房间成员清理；阶段转换、房主转移及房间关闭仍以 RoomGrain 为权威。
/// </summary>
public sealed class GatewayRoomMembershipService
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<GatewayRoomMembershipService> _logger;

    public GatewayRoomMembershipService(
        IClusterClient clusterClient,
        ILogger<GatewayRoomMembershipService> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public async Task<GatewayRoomLeaveResult> LeaveMappedRoomAsync(
        string accountId,
        string requestedRoomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedRoomId);

        var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>(GatewayGrainKeys.Global);
        var activeRoomId = await mapping.TryGetAccountRoomAsync(accountId);
        if (string.IsNullOrWhiteSpace(activeRoomId))
        {
            return new GatewayRoomLeaveResult(
                null,
                RoomOperationResult.NoChange(0L, "Account already left the room."));
        }

        if (!string.Equals(activeRoomId, requestedRoomId, StringComparison.Ordinal))
        {
            return new GatewayRoomLeaveResult(
                activeRoomId,
                RoomOperationResult.Rejected(
                    RoomOperationErrorCode.NotMember,
                    $"Account is active in room '{activeRoomId}', not '{requestedRoomId}'.",
                    0L));
        }

        var room = _clusterClient.GetGrain<IRoomGrain>(activeRoomId);
        var operation = await room.LeaveWithResultAsync(accountId);
        if (operation.Success)
        {
            /*
             * 通常由 RoomGrain 清除此映射。成员已不存在但映射残留时，网关清理仍须幂等。
             */
            await mapping.ClearAccountRoomAsync(accountId, activeRoomId);
        }

        return new GatewayRoomLeaveResult(activeRoomId, operation);
    }

    public async ValueTask CleanupDisconnectedSessionAsync(
        string accountId,
        string roomId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>(GatewayGrainKeys.Global);
        var activeRoomId = await mapping.TryGetAccountRoomAsync(accountId);
        if (!string.Equals(activeRoomId, roomId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Disconnected room cleanup skipped because membership changed. AccountId={AccountId} RequestedRoomId={RequestedRoomId} ActiveRoomId={ActiveRoomId}",
                accountId,
                roomId,
                activeRoomId);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
        var offline = await room.MarkOfflineWithResultAsync(accountId);
        if (offline.Success)
        {
            _logger.LogInformation(
                "Disconnected room member marked offline. AccountId={AccountId} RoomId={RoomId} Applied={Applied}",
                accountId,
                roomId,
                offline.Applied);
            return;
        }

        if (offline.ErrorCode == RoomOperationErrorCode.NotMember)
        {
            await mapping.ClearAccountRoomAsync(accountId, roomId);
            return;
        }

        if (!offline.Success)
        {
            _logger.LogWarning(
                "Failed to mark disconnected room member offline. AccountId={AccountId} RoomId={RoomId} ErrorCode={ErrorCode}",
                accountId,
                roomId,
                offline.ErrorCode);
        }
    }
}
