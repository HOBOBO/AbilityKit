#nullable enable annotations
using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /// <summary>
    /// 按预算追赶目标帧，并在已提交的步进后转发快照。
    /// </summary>
    public static class WorldCatchUpDriver
    {
        /// <summary>
        /// 以给定的固定步长（秒）推进宿主，返回最后一帧成功提交的帧号。只检查指定世界的帧守卫，不是端到端重连入口。
        /// </summary>
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
                IWorldFrameGuard frameGuard = null;
                world.Services?.TryResolve(out frameGuard);
                if (frameGuard != null && !frameGuard.CanAdvance) break;
                var nextFrame = lastTickedFrame + 1;
                var frameIndex = new FrameIndex(nextFrame);

                runtime.Tick(fixedDelta);
                if (frameGuard != null && !frameGuard.CanCommit) break;

                if (provider != null && feed != null && maxSnapshotsPerStep > 0)
                {
                    SnapshotProviderDrain.DrainSnapshots(provider, worldId, frameIndex, maxSnapshotsPerStep, feed);
                }

                lastTickedFrame = nextFrame;
                steps++;
            }

            return lastTickedFrame;
        }
    }
}
