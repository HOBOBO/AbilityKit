using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.Samples
{
    public static class TinyStateExample
    {
        public static void RequireStateCapability(RoomGatewaySnapshot battle)
        {
            if (battle == null) throw new ArgumentNullException(nameof(battle));
            var binding = RoomGatewayNetworkSyncSessionBinding.Create(
                battle.SyncCapabilities, nameof(NetworkSyncModel.AuthoritativeInterpolation),
                NetworkSyncRemoteCapabilityPolicy.Require);
            var profile = NetworkSyncProfiles.AuthoritativeInterpolation;
            var descriptor = binding.Negotiate(new NetworkSyncSessionOptions
            {
                RequiredProfile = profile,
                RequiredMinimumSchemaVersion = 1,
                RequiredMaximumSchemaVersion = 1,
                AvailableCapabilities = NetworkSyncCapabilities.FromProfile(in profile, 1, 1)
            });
            if (!descriptor.IsRemoteNegotiated)
                throw new InvalidOperationException("Remote State capability negotiation was skipped.");
        }

        public static async Task SubscribeAsync(
            IRoomGatewaySessionClient rooms, string sessionToken, string roomId,
            string battleId, CancellationToken cancellationToken)
        {
            var result = await rooms.SubscribeStateSyncAsync(
                new RoomGatewayStateSyncSubscriptionRequest(sessionToken, battleId, roomId),
                cancellationToken: cancellationToken);
            if (!result.Success)
                throw new InvalidOperationException("Subscribe State: " + result.Message);
        }

        public static WireStateSyncActorSnapshot ReadActor(
            in WireStateSyncSnapshotPush snapshot, uint playerId)
        {
            if (snapshot.Actors == null)
                throw new InvalidOperationException("Tiny snapshot has no actors.");
            foreach (var actor in snapshot.Actors)
                if (actor.ActorId == playerId) return actor;
            throw new InvalidOperationException("Tiny player is missing from the snapshot.");
        }
    }
}
