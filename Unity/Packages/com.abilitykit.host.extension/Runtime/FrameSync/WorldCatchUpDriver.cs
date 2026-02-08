using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    internal static class WorldCatchUpDriverInternal
    {
        public static int CatchUpAndFeedSnapshots(
            HostRuntime runtime,
            IWorld world,
            int lastTickedFrame,
            int driveTargetFrame,
            float fixedDelta,
            int stepsBudget,
            AbilityKit.Ability.Host.IWorldStateSnapshotProvider provider,
            int maxSnapshotsPerStep,
            Action<FramePacket> feed)
        {
            if (runtime == null) return lastTickedFrame;
            if (world == null) return lastTickedFrame;
            if (driveTargetFrame <= 0) return lastTickedFrame;
            if (stepsBudget <= 0) return lastTickedFrame;

            if (maxSnapshotsPerStep <= 0) maxSnapshotsPerStep = 0;

            var worldId = world.Id;

            var steps = 0;
            while (steps < stepsBudget && lastTickedFrame < driveTargetFrame)
            {
                var nextFrame = lastTickedFrame + 1;
                var frameIndex = new FrameIndex(nextFrame);

                runtime.Tick(fixedDelta);

                if (provider != null && feed != null && maxSnapshotsPerStep > 0)
                {
                    for (int i = 0; i < maxSnapshotsPerStep; i++)
                    {
                        if (!provider.TryGetSnapshot(frameIndex, out var s)) break;
                        var packet = new FramePacket(worldId, frameIndex, Array.Empty<PlayerInputCommand>(), s);
                        feed(packet);
                    }
                }

                lastTickedFrame = nextFrame;
                steps++;
            }

            return lastTickedFrame;
        }
    }
}
