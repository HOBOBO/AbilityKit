using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;

using HostWorldStateSnapshotProvider = AbilityKit.Ability.Host.IWorldStateSnapshotProvider;

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
            HostWorldStateSnapshotProvider provider = null;

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


            _confirmedLastTickedFrame = WorldCatchUpDriver.CatchUpAndFeedSnapshots(
                runtime: _confirmedRuntime,
                world: _confirmedWorld,
                lastTickedFrame: _confirmedLastTickedFrame,
                driveTargetFrame: driveTargetFrame,
                fixedDelta: fixedDelta,
                stepsBudget: stepsBudget,
                provider: provider,
                maxSnapshotsPerStep: 16,
                feed: packet =>
                {
                    _confirmedSnapshots?.Feed(packet);
                    _confirmedViewSnapshots?.Feed(packet);
                });

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
