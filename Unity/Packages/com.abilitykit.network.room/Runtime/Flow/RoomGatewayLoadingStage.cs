#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Network.Room
{
    /// <summary>Runs project asset preparation before reporting a server loading generation.</summary>
    public sealed class RoomGatewayLoadingStage
    {
        private readonly RoomGatewaySessionFlow _flow;
        private readonly RoomGatewayCommandIdLedger _commands;
        private readonly Func<RoomGatewaySnapshot, CancellationToken, Task> _prepareAssets;
        private long _completedGeneration = -1;

        public RoomGatewayLoadingStage(RoomGatewaySessionFlow flow,
            RoomGatewayCommandIdLedger commands,
            Func<RoomGatewaySnapshot, CancellationToken, Task> prepareAssets)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _prepareAssets = prepareAssets ?? throw new ArgumentNullException(nameof(prepareAssets));
        }

        public void Reset() => _completedGeneration = -1;

        public async Task<bool> AdvanceAsync(string sessionToken, RoomGatewaySnapshot room,
            CancellationToken cancellationToken, TimeSpan? timeout = null)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (room.Phase != RoomGatewaySessionPhase.Loading || room.LaunchManifestVersion <= 0 ||
                room.LaunchGeneration == _completedGeneration)
                return false;

            var operationKey = "assets-loaded:" + room.RoomId + ":" + room.LaunchGeneration;
            await _prepareAssets(room, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var loaded = await _flow.ReportAssetsLoadedAsync(new RoomGatewayReportAssetsLoadedRequest(
                sessionToken, room.RoomId, room.LaunchGeneration, room.LaunchManifestVersion,
                room.LaunchManifestHash, _commands.GetOrCreate(operationKey)),
                timeout: timeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!loaded.Success) throw new InvalidOperationException(loaded.Message);
            _commands.Complete(operationKey);
            _completedGeneration = room.LaunchGeneration;
            return true;
        }
    }
}
