using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;

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

            var worldId = _remoteDrivenWorld.Id;
            IWorldStateSnapshotProvider provider = null;

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

            var steps = 0;
            while (steps < stepsBudget && _remoteDrivenLastTickedFrame < driveTargetFrame)
            {
                var nextFrame = _remoteDrivenLastTickedFrame + 1;
                var frameIndex = new FrameIndex(nextFrame);

                _remoteDrivenRuntime.Tick(fixedDelta);

                if (provider != null)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        if (!provider.TryGetSnapshot(frameIndex, out var s))
                        {
                            break;
                        }

                        var synthesized = new FramePacket(worldId, frameIndex, Array.Empty<PlayerInputCommand>(), s);
                        _snapshots?.Feed(synthesized);
                    }
                }

                _remoteDrivenLastTickedFrame = nextFrame;
                steps++;
            }

            _remoteDrivenInputSource.TrimBefore(_remoteDrivenLastTickedFrame - 120);
        }
    }
}
