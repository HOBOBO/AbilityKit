using AbilityKit.Orleans.Contracts.Rooms;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Core;

public sealed record GatewayRoomLeaveResult(
    string? ActiveRoomId,
    RoomOperationResult Operation);

/// <summary>
/// Owns gateway-side room membership cleanup while RoomGrain remains the
/// authority for phase transitions, owner transfer, and room closure.
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
            // RoomGrain normally clears this mapping. Keep the gateway cleanup
            // idempotent for an already-missing member with a stale mapping.
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
