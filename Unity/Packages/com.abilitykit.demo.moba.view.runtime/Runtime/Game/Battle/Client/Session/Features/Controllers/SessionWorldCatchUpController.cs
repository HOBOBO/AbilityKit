using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;

using HostWorldStateSnapshotProvider = AbilityKit.Ability.Host.IWorldStateSnapshotProvider;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionWorldCatchUpController
    {
        private const int MaxSnapshotsPerStep = 16;

        public int CatchUpAndFeedSnapshots(
            HostRuntime runtime,
            IWorld world,
            HostWorldStateSnapshotProvider snapshotProvider,
            int lastTickedFrame,
            int driveTargetFrame,
            float fixedDelta,
            int stepsBudget,
            Action<FramePacket> feed)
        {
            return WorldCatchUpDriver.CatchUpAndFeedSnapshots(
                runtime: runtime,
                world: world,
                lastTickedFrame: lastTickedFrame,
                driveTargetFrame: driveTargetFrame,
                fixedDelta: fixedDelta,
                stepsBudget: stepsBudget,
                provider: snapshotProvider,
                maxSnapshotsPerStep: MaxSnapshotsPerStep,
                feed: feed);
        }
    }
}
