using System;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal readonly struct ConfirmedAuthorityWorldTickOptions
    {
        public readonly BattleStartPlan Plan;
        public readonly BattleContext Context;
        public readonly BattleSessionHandles.ConfirmedHandles Handles;
        public readonly SessionWorldCatchUpController WorldCatchUp;
        public readonly int LastTickedFrame;
        public readonly float FixedDeltaSeconds;
        public readonly int StepsBudget;

        public ConfirmedAuthorityWorldTickOptions(
            BattleStartPlan plan,
            BattleContext context,
            BattleSessionHandles.ConfirmedHandles handles,
            SessionWorldCatchUpController worldCatchUp,
            int lastTickedFrame,
            float fixedDeltaSeconds,
            int stepsBudget)
        {
            Plan = plan;
            Context = context;
            Handles = handles;
            WorldCatchUp = worldCatchUp;
            LastTickedFrame = lastTickedFrame;
            FixedDeltaSeconds = fixedDeltaSeconds;
            StepsBudget = stepsBudget;
        }
    }

    internal static class ConfirmedAuthorityWorldTickDriver
    {
        public static int Tick(ConfirmedAuthorityWorldTickOptions options)
        {
            var handles = options.Handles;
            var lastTickedFrame = options.LastTickedFrame;
            if (handles.World == null || handles.Runtime == null) return lastTickedFrame;
            if (handles.InputSource == null) return lastTickedFrame;

            var inputSource = handles.InputSource;
            var inputTargetFrame = inputSource.TargetFrame;
            if (inputTargetFrame <= 0) return lastTickedFrame;

            var frameState = ResolveDriveFrameState(options.Plan, options.Context, inputTargetFrame);
            if (frameState.DriveTargetFrame <= 0 || options.StepsBudget <= 0) return lastTickedFrame;

            var nextTickedFrame = options.WorldCatchUp.CatchUpAndFeedSnapshots(
                runtime: handles.Runtime,
                world: handles.World,
                lastTickedFrame: lastTickedFrame,
                driveTargetFrame: frameState.DriveTargetFrame,
                fixedDelta: options.FixedDeltaSeconds,
                stepsBudget: options.StepsBudget,
                feed: packet =>
                {
                    handles.Snapshots?.Feed(packet);
                    handles.ViewSnapshotRuntime?.Snapshots?.Feed(packet);
                });

            inputSource.TrimBefore(SessionSimRuntimeTuning.ResolveInputTrimBeforeFrame(
                nextTickedFrame,
                handles.Consumable.LastConsumedFrame));

            ConfirmedAuthorityDebugStatsPublisher.Update(
                frameState.ConfirmedFrame,
                frameState.PredictedFrame,
                inputTargetFrame,
                frameState.DriveTargetFrame,
                nextTickedFrame,
                handles.ViewEventSink);

            return nextTickedFrame;
        }

        private static ConfirmedDriveFrameState ResolveDriveFrameState(
            BattleStartPlan plan,
            BattleContext ctx,
            int inputTargetFrame)
        {
            var state = new ConfirmedDriveFrameState(inputTargetFrame, 0, 0);
            var stats = ctx != null ? ctx.PredictionStats : null;
            if (stats == null) return state;

            var wid = new WorldId(plan.World.WorldId);
            if (!stats.TryGetFrames(wid, out var confirmed, out var predicted)) return state;

            var confirmedFrame = confirmed.Value;
            var predictedFrame = predicted.Value;
            var driveTargetFrame = confirmedFrame > 0
                ? Math.Min(inputTargetFrame, confirmedFrame)
                : inputTargetFrame;

            return new ConfirmedDriveFrameState(driveTargetFrame, confirmedFrame, predictedFrame);
        }

        private readonly struct ConfirmedDriveFrameState
        {
            public readonly int DriveTargetFrame;
            public readonly int ConfirmedFrame;
            public readonly int PredictedFrame;

            public ConfirmedDriveFrameState(
                int driveTargetFrame,
                int confirmedFrame,
                int predictedFrame)
            {
                DriveTargetFrame = driveTargetFrame;
                ConfirmedFrame = confirmedFrame;
                PredictedFrame = predictedFrame;
            }
        }
    }
}
