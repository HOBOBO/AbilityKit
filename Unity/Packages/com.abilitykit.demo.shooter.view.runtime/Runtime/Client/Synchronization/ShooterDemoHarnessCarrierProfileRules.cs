#nullable enable

using System;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    internal static class ShooterDemoHarnessCarrierProfileRules
    {
        public static SyncDemoCapabilityResult SupportsPredictRollback(
            in NetworkSyncProfile profile,
            in NetworkConditionProfile networkProfile)
        {
            if (profile.ClientPlayback != ClientPlaybackPolicy.PredictRollback)
            {
                return SyncDemoCapabilityResult.Unsupported("Shooter rollback carrier currently supports predict rollback playback only.");
            }

            if (!profile.Snapshot.HasFlag(SnapshotPolicy.FullSnapshot) && !profile.Snapshot.HasFlag(SnapshotPolicy.AuthorityOverride))
            {
                return SyncDemoCapabilityResult.Unsupported("Shooter rollback carrier requires full or authority override snapshots.");
            }

            return SyncDemoCapabilityResult.Supported;
        }

        public static SyncDemoCapabilityResult SupportsAuthoritativeInterpolation(
            in NetworkSyncProfile profile,
            in NetworkConditionProfile networkProfile)
        {
            if (profile.ClientPlayback != ClientPlaybackPolicy.AuthoritativeInterpolation)
            {
                return SyncDemoCapabilityResult.Unsupported("Shooter interpolation carrier supports authoritative interpolation playback only.");
            }

            if (!profile.Snapshot.HasFlag(SnapshotPolicy.FixedRateStateStream)
                && !profile.Snapshot.HasFlag(SnapshotPolicy.BatchSnapshot))
            {
                return SyncDemoCapabilityResult.Unsupported("Shooter interpolation carrier requires fixed-rate or batched state snapshots.");
            }

            return SyncDemoCapabilityResult.Supported;
        }

        public static SyncDemoCapabilityResult SupportsHybrid(
            in NetworkSyncProfile profile,
            in NetworkConditionProfile networkProfile,
            NetworkSyncModel controllerModel)
        {
            if (profile.ClientPlayback == ClientPlaybackPolicy.HybridLocalPredictRemoteInterpolate)
            {
                return controllerModel == NetworkSyncModel.HybridHeroPrediction
                    ? SyncDemoCapabilityResult.Supported
                    : SyncDemoCapabilityResult.Unsupported("Shooter hybrid carrier requires a HybridHeroPrediction controller.");
            }

            if (profile.ClientPlayback == ClientPlaybackPolicy.PredictRollback)
            {
                if (!profile.Snapshot.HasFlag(SnapshotPolicy.FullSnapshot) && !profile.Snapshot.HasFlag(SnapshotPolicy.AuthorityOverride))
                {
                    return SyncDemoCapabilityResult.Unsupported("Shooter hybrid carrier requires full or authority override snapshots for rollback entities.");
                }

                return SyncDemoCapabilityResult.Supported;
            }

            return SyncDemoCapabilityResult.Unsupported("Shooter hybrid carrier supports hybrid or predict-rollback playback only.");
        }

        public static SyncHealthEvent[]? CollectFastReconnectHealthEvents(
            IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> strategy)
        {
            if (strategy is not IShooterClientSyncController controller)
            {
                return null;
            }

            var events = controller.LastFastReconnectHealthEvents;
            if (events == null || events.Count == 0)
            {
                return null;
            }

            var buffer = new SyncHealthEvent[events.Count];
            for (var i = 0; i < events.Count; i++)
            {
                buffer[i] = events[i];
            }

            return buffer;
        }
    }
}
