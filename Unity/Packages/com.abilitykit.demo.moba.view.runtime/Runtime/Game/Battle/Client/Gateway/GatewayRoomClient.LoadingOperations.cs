using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Room;

namespace AbilityKit.Game.Battle.Agent
{
    public sealed partial class GatewayRoomClient
    {
        public async Task<GatewayRoomOperationResult> BeginLoadingAsync(
            string sessionToken,
            string roomId,
            long? expectedRevision,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.BeginLoadingAsync(
                new RoomGatewayBeginLoadingRequest(
                    sessionToken,
                    roomId,
                    expectedRevision,
                    commandId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToOperationResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomRevision,
                result.Snapshot);
        }

        public async Task<GatewayRoomOperationResult> ReportAssetsLoadedAsync(
            string sessionToken,
            string roomId,
            long launchGeneration,
            int manifestVersion,
            string manifestHash,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.ReportAssetsLoadedAsync(
                new RoomGatewayReportAssetsLoadedRequest(
                    sessionToken,
                    roomId,
                    launchGeneration,
                    manifestVersion,
                    manifestHash,
                    commandId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToOperationResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomRevision,
                result.Snapshot);
        }

        public async Task<GatewayRoomOperationResult> ReportLoadingProgressAsync(
            string sessionToken,
            string roomId,
            long launchGeneration,
            int manifestVersion,
            string manifestHash,
            int progress,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            if (progress < 0 || progress > 100) throw new ArgumentOutOfRangeException(nameof(progress));

            var result = await _roomSessionClient.ReportLoadingProgressAsync(
                new RoomGatewayReportLoadingProgressRequest(
                    sessionToken,
                    roomId,
                    launchGeneration,
                    manifestVersion,
                    manifestHash,
                    progress),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToOperationResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomRevision,
                result.Snapshot);
        }

        public async Task<GatewayRoomOperationResult> CancelLoadingAsync(
            string sessionToken,
            string roomId,
            long? expectedRevision,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.CancelLoadingAsync(
                new RoomGatewayCancelLoadingRequest(
                    sessionToken,
                    roomId,
                    expectedRevision,
                    commandId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToOperationResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomRevision,
                result.Snapshot);
        }

        public async Task<GatewayRoomOperationResult> LeaveRoomAsync(
            string sessionToken,
            string roomId,
            long? expectedRevision,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.LeaveRoomAsync(
                new RoomGatewayLeaveRequest(
                    sessionToken,
                    roomId,
                    expectedRevision,
                    commandId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToOperationResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomRevision,
                result.Snapshot);
        }

        public async Task<GatewayGetSnapshotResult> GetSnapshotAsync(
            string sessionToken,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.GetSnapshotAsync(
                new RoomGatewayGetSnapshotRequest(sessionToken, roomId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            var snapshot = ToClientSnapshot(result.Snapshot);
            return new GatewayGetSnapshotResult(
                result.Success,
                result.RoomId,
                result.NumericRoomId,
                snapshot,
                result.Message);
        }

        public async Task<GatewayRestoreRoomResult> RestoreRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(region)) throw new ArgumentException("region is required.", nameof(region));
            if (string.IsNullOrWhiteSpace(serverId)) throw new ArgumentException("serverId is required.", nameof(serverId));

            var result = await _roomSessionClient.RestoreRoomAsync(
                new RoomGatewayRestoreRoomRequest(sessionToken, region, serverId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            var snapshot = ToClientSnapshot(result.Snapshot);
            var anchor = ToGatewayAnchor(in result.WorldStartAnchor);
            return new GatewayRestoreRoomResult(
                result.Success,
                result.HasActiveRoom,
                result.IsInBattle,
                result.RoomId,
                result.NumericRoomId,
                snapshot,
                in anchor,
                result.Message,
                ToJoinKind(result.JoinKind),
                result.ServerNowTicks,
                result.CurrentPlayerId);
        }

        public ClientRoomSnapshot DeserializeRoomStateChangedPush(ArraySegment<byte> payload)
        {
            return _wireClient.DeserializeRoomStateChangedPush(payload);
        }

        public bool IsRoomStateChangedPush(uint opCode)
        {
            return opCode == _opCodes.RoomStateChanged;
        }
    }
}
