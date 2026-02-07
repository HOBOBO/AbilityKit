using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void TickConfirmedAuthorityWorldSim(float deltaTime)
        {
            if (_confirmedWorld == null || _confirmedRuntime == null) return;
            if (_confirmedInputSource == null) return;

            var inputTargetFrame = _confirmedInputSource.TargetFrame;
            if (inputTargetFrame <= 0) return;

            var driveTargetFrame = inputTargetFrame;
            var confirmedFrame = 0;
            var predictedFrame = 0;

            var stats = _ctx != null ? _ctx.PredictionStats : null;
            if (stats != null)
            {
                var wid = new WorldId(_plan.WorldId);
                if (stats.TryGetFrames(wid, out var confirmed, out var predicted))
                {
                    confirmedFrame = confirmed.Value;
                    predictedFrame = predicted.Value;

                    if (confirmedFrame > 0)
                    {
                        driveTargetFrame = Math.Min(inputTargetFrame, confirmedFrame);
                    }
                }
            }

            if (driveTargetFrame <= 0) return;

            var fixedDelta = GetFixedDeltaSeconds();
            var stepsBudget = MaxRemoteDrivenCatchUpStepsPerUpdate;
            if (stepsBudget <= 0) return;

            var worldId = _confirmedWorld.Id;
            IWorldStateSnapshotProvider provider = null;

            try
            {
                if (_confirmedWorld.Services != null)
                {
                    _confirmedWorld.Services.TryResolve(out provider);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                provider = null;
            }

            var steps = 0;
            while (steps < stepsBudget && _confirmedLastTickedFrame < driveTargetFrame)
            {
                var nextFrame = _confirmedLastTickedFrame + 1;
                var frameIndex = new FrameIndex(nextFrame);

                _confirmedRuntime.Tick(fixedDelta);

                if (provider != null)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        if (!provider.TryGetSnapshot(frameIndex, out var s))
                        {
                            break;
                        }

                        var synthesized = new FramePacket(worldId, frameIndex, Array.Empty<PlayerInputCommand>(), s);
                        _confirmedSnapshots?.Feed(synthesized);
                        _confirmedViewSnapshots?.Feed(synthesized);
                    }
                }

                _confirmedLastTickedFrame = nextFrame;
                steps++;
            }

            _confirmedInputSource.TrimBefore(_confirmedLastTickedFrame - 120);

            if (BattleFlowDebugProvider.ConfirmedAuthorityWorldStats != null)
            {
                var s = BattleFlowDebugProvider.ConfirmedAuthorityWorldStats;
                s.ConfirmedFrame = confirmedFrame;
                s.PredictedFrame = predictedFrame;
                s.AuthorityInputTargetFrame = inputTargetFrame;
                s.AuthorityDriveTargetFrame = driveTargetFrame;
                s.AuthorityLastTickedFrame = _confirmedLastTickedFrame;

                if (_confirmedViewEventSink != null)
                {
                    s.ViewEventTotal = _confirmedViewEventSink.Total;
                    s.RecentViewEvents = _confirmedViewEventSink.GetRecentLines();
                }
            }
        }
    }
}
