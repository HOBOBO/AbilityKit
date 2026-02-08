using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;

using HostWorldStateSnapshotProvider = AbilityKit.Ability.Host.IWorldStateSnapshotProvider;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void TickRemoteDrivenLocalSim(float deltaTime)
        {
            if (_remoteDrivenWorld == null || _remoteDrivenRuntime == null) return;
            if (_remoteDrivenInputSource == null) return;

            var inputTargetFrame = _remoteDrivenInputSource.TargetFrame;
            if (inputTargetFrame <= 0) return;

            var driveTargetFrame = inputTargetFrame;

            _remoteDrivenInputSource.DelayFrames = _plan.InputDelayFrames < 0 ? 0 : _plan.InputDelayFrames;

            if (driveTargetFrame <= 0) return;

            var fixedDelta = GetFixedDeltaSeconds();
            var stepsBudget = MaxRemoteDrivenCatchUpStepsPerUpdate;
            if (stepsBudget <= 0) return;
            HostWorldStateSnapshotProvider provider = null;

            try
            {
                if (_remoteDrivenWorld.Services != null)
                {
                    _remoteDrivenWorld.Services.TryResolve(out provider);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                provider = null;
            }


            _remoteDrivenLastTickedFrame = WorldCatchUpDriver.CatchUpAndFeedSnapshots(
                runtime: _remoteDrivenRuntime,
                world: _remoteDrivenWorld,
                lastTickedFrame: _remoteDrivenLastTickedFrame,
                driveTargetFrame: driveTargetFrame,
                fixedDelta: fixedDelta,
                stepsBudget: stepsBudget,
                provider: provider,
                maxSnapshotsPerStep: 16,
                feed: packet => _snapshots?.Feed(packet));

            _remoteDrivenInputSource.TrimBefore(_remoteDrivenLastTickedFrame - 120);
        }
    }
}
