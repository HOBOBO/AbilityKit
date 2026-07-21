using AbilityKit.Core.Snapshots.Routing;

namespace AbilityKit.Game.Flow
{
    internal readonly struct RemoteDrivenWorldTickOptions
    {
        public readonly BattleStartPlan Plan;
        public readonly BattleSessionHandles.RemoteDrivenHandles Handles;
        public readonly SessionWorldCatchUpController WorldCatchUp;
        public readonly FrameSnapshotDispatcher Snapshots;
        public readonly int LastTickedFrame;
        public readonly float FixedDeltaSeconds;
        public readonly int StepsBudget;

        public RemoteDrivenWorldTickOptions(
            BattleStartPlan plan,
            BattleSessionHandles.RemoteDrivenHandles handles,
            SessionWorldCatchUpController worldCatchUp,
            FrameSnapshotDispatcher snapshots,
            int lastTickedFrame,
            float fixedDeltaSeconds,
            int stepsBudget)
        {
            Plan = plan;
            Handles = handles;
            WorldCatchUp = worldCatchUp;
            Snapshots = snapshots;
            LastTickedFrame = lastTickedFrame;
            FixedDeltaSeconds = fixedDeltaSeconds;
            StepsBudget = stepsBudget;
        }
    }

    internal static class RemoteDrivenWorldTickDriver
    {
        public static int Tick(RemoteDrivenWorldTickOptions options)
        {
            var handles = options.Handles;
            var lastTickedFrame = options.LastTickedFrame;
            if (handles.World == null || handles.Runtime == null) return lastTickedFrame;
            if (handles.InputSource == null) return lastTickedFrame;

            var inputSource = handles.InputSource;
            var inputTargetFrame = inputSource.TargetFrame;
            if (inputTargetFrame <= 0) return lastTickedFrame;

            var driveTargetFrame = inputTargetFrame;
            inputSource.DelayFrames = SessionSimRuntimeTuning.NormalizeInputDelayFrames(options.Plan.World.InputDelayFrames);

            if (driveTargetFrame <= 0 || options.StepsBudget <= 0) return lastTickedFrame;

            var nextTickedFrame = options.WorldCatchUp.CatchUpAndFeedSnapshots(
                runtime: handles.Runtime,
                world: handles.World,
                lastTickedFrame: lastTickedFrame,
                driveTargetFrame: driveTargetFrame,
                fixedDelta: options.FixedDeltaSeconds,
                stepsBudget: options.StepsBudget,
                feed: packet => options.Snapshots?.Feed(packet));

            inputSource.TrimBefore(SessionSimRuntimeTuning.ResolveInputTrimBeforeFrame(
                nextTickedFrame,
                handles.Consumable.LastConsumedFrame));
            return nextTickedFrame;
        }
    }
}
