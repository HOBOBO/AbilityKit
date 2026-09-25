using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Room;

namespace AbilityKit.Demo.Tiny.Samples
{
    public static class TinyRoomExample
    {
        public static async Task<string> CreateRoomAsync(
            IRoomGatewaySessionClient rooms, string sessionToken, string region,
            string serverId, string commandId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Create command ID is required.", nameof(commandId));
            var result = await rooms.CreateRoomAsync(new RoomGatewayCreateRequest(
                sessionToken, region, serverId, "tiny", "Tiny Battle", false, 2,
                commandId: commandId),
                cancellationToken: cancellationToken);
            if (!result.Success || string.IsNullOrEmpty(result.RoomId))
                throw new InvalidOperationException("Create room: " + result.Message);
            return result.RoomId;
        }

        public static async Task JoinAndReadyAsync(
            IRoomGatewaySessionClient rooms, string sessionToken, string region,
            string serverId, string roomId, CancellationToken cancellationToken)
        {
            var joined = await rooms.JoinRoomAsync(new RoomGatewayJoinRequest(
                sessionToken, region, serverId, roomId), cancellationToken: cancellationToken);
            if (!joined.Success)
                throw new InvalidOperationException("Join room: " + joined.Message);
            var ready = await rooms.SetReadyAsync(new RoomGatewayReadyRequest(
                sessionToken, roomId, true), cancellationToken: cancellationToken);
            if (!ready.Success)
                throw new InvalidOperationException("Ready: " + ready.Message);
        }

        public static async Task<RoomGatewaySnapshot> StartBattleAsync(
            IRoomGatewaySessionClient ownerRooms, string ownerToken,
            IRoomGatewaySessionClient guestRooms, string guestToken,
            string roomId, CancellationToken cancellationToken)
        {
            var flow = new RoomGatewaySessionFlow(ownerRooms);
            var loading = await flow.BeginLoadingAsync(new RoomGatewayBeginLoadingRequest(
                ownerToken, roomId, null, Guid.NewGuid().ToString("N")),
                cancellationToken: cancellationToken);
            if (!loading.Success || loading.Snapshot == null)
                throw new InvalidOperationException("Begin loading: " + loading.Message);

            var manifest = loading.Snapshot;
            var ownerLoaded = await flow.ReportAssetsLoadedAsync(new RoomGatewayReportAssetsLoadedRequest(
                ownerToken, roomId, manifest.LaunchGeneration, manifest.LaunchManifestVersion,
                manifest.LaunchManifestHash, Guid.NewGuid().ToString("N")),
                cancellationToken: cancellationToken);
            var guestLoaded = await guestRooms.ReportAssetsLoadedAsync(new RoomGatewayReportAssetsLoadedRequest(
                guestToken, roomId, manifest.LaunchGeneration, manifest.LaunchManifestVersion,
                manifest.LaunchManifestHash, Guid.NewGuid().ToString("N")),
                cancellationToken: cancellationToken);
            if (!ownerLoaded.Success || !guestLoaded.Success)
                throw new InvalidOperationException("Report assets loaded: " +
                    ownerLoaded.Message + " / " + guestLoaded.Message);

            var started = await flow.WaitForBattleStartAsync(
                ownerToken, roomId, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(15),
                cancellationToken);
            if (!started.Success || started.Snapshot == null)
                throw new InvalidOperationException("Start battle: " + started.Message);
            return started.Snapshot;
        }
    }
}
