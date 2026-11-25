using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Ability.Host.Extensions.FrameSync;

namespace AbilityKit.Game.Flow
{
    internal readonly struct RemoteDrivenWorldTickOptions
    {
        public readonly BattleStartPlan Plan;
        public readonly BattleSessionRemoteDrivenWorldRuntime Handles;
        public readonly SessionWorldCatchUpController WorldCatchUp;
        public readonly FrameSnapshotDispatcher Snapshots;
        public readonly int LastTickedFrame;
        public readonly float FixedDeltaSeconds;
        public readonly int StepsBudget;
        public readonly int LastServerAckFrame;

        public RemoteDrivenWorldTickOptions(
            BattleStartPlan plan,
            BattleSessionRemoteDrivenWorldRuntime handles,
            SessionWorldCatchUpController worldCatchUp,
            FrameSnapshotDispatcher snapshots,
            int lastTickedFrame,
            float fixedDeltaSeconds,
            int stepsBudget,
            int lastServerAckFrame = 0)
        {
            Plan = plan;
            Handles = handles;
            WorldCatchUp = worldCatchUp;
            Snapshots = snapshots;
            LastTickedFrame = lastTickedFrame;
            FixedDeltaSeconds = fixedDeltaSeconds;
            StepsBudget = stepsBudget;
            LastServerAckFrame = lastServerAckFrame;
        }
    }

    /// <summary>
    /// 依据权威输入帧及帧预测窗口推进世界，不消费实体状态快照作为步进依据。
    /// </summary>
    internal static class RemoteDrivenWorldTickDriver
    {
        internal static int TickSettled(RemoteDrivenWorldTickOptions options, MobaSettledFrameSession settled)
        {
            settled?.Tick();
            var handles = options.Handles;
            if (settled?.CanAdvance != true || handles.Runtime == null || handles.World == null ||
                !handles.Runtime.Features.TryGetFeature<IClientPredictionDriverStats>(out var stats))
                return options.LastTickedFrame;

            var worldId = handles.World.Id;
            var lastFrame = options.LastTickedFrame;
            for (var step = 0; step < options.StepsBudget; step++)
            {
                if (!stats.TryGetFrames(worldId, out var confirmed, out var predicted)) break;
                lastFrame = predicted.Value;
                var hasAuthority = confirmed.Value < settled.Receiver.TargetFrame;
                if (!hasAuthority && (!settled.CanSubmit || !options.Plan.Authority.EnableClientPrediction)) break;
                options.WorldCatchUp.CatchUpAndFeedSnapshots(handles.Runtime, handles.World,
                    handles.Capabilities.SnapshotProvider, predicted.Value, predicted.Value + 1,
                    options.FixedDeltaSeconds, 1, packet => options.Snapshots?.Feed(packet));
                if (!stats.TryGetFrames(worldId, out var nextConfirmed, out var nextPredicted)) break;
                lastFrame = nextPredicted.Value;
                if (nextConfirmed.Value == confirmed.Value && nextPredicted.Value == predicted.Value) break;
            }
            return lastFrame;
        }

        public static int Tick(RemoteDrivenWorldTickOptions options)
        {
            var handles = options.Handles;
            var hasPredictionFrame = TryGetPredictionState(
                handles,
                out var predictionFrame,
                out var predictionWindow);
            var lastTickedFrame = ResolveCatchUpFrame(
                options.LastTickedFrame,
                hasPredictionFrame,
                predictionFrame);
            if (handles.World == null || handles.Runtime == null) return lastTickedFrame;
            if (handles.InputSource == null) return lastTickedFrame;

            var inputSource = handles.InputSource;
            var inputTargetFrame = inputSource.TargetFrame;
            if (inputTargetFrame <= 0) return lastTickedFrame;

            inputSource.DelayFrames = SessionSimRuntimeTuning.NormalizeInputDelayFrames(options.Plan.World.InputDelayFrames);

            var nextTickedFrame = lastTickedFrame;
            var driveTargetFrame = ResolveDriveTargetFrame(
                inputTargetFrame,
                options.Plan.Authority.EnableClientPrediction,
                hasPredictionFrame,
                predictionWindow);

            // Re-read the dynamic window after every runtime step. The prediction module can
            // shrink its attainable window while catching up; continuing with a stale target
            // would tick gameplay systems without advancing the simulation frame.
            var remainingSteps = options.StepsBudget;
            while (ShouldDriveRuntime(nextTickedFrame, driveTargetFrame, remainingSteps))
            {
                var predictionFrameBeforeStep = nextTickedFrame;
                nextTickedFrame = options.WorldCatchUp.CatchUpAndFeedSnapshots(
                    runtime: handles.Runtime,
                    world: handles.World,
                    snapshotProvider: handles.Capabilities.SnapshotProvider,
                    lastTickedFrame: nextTickedFrame,
                    driveTargetFrame: driveTargetFrame,
                    fixedDelta: options.FixedDeltaSeconds,
                    stepsBudget: 1,
                    feed: packet => options.Snapshots?.Feed(packet));
                remainingSteps--;

                if (!TryGetPredictionState(
                        handles,
                        out predictionFrame,
                        out predictionWindow))
                {
                    continue;
                }

                // Runtime.Tick may consume, predict, replay, or stall. Its prediction frame is
                // the authoritative progress marker; a synthetic loop counter can otherwise run
                // ahead and permanently suppress catch-up while inputs remain queued.
                nextTickedFrame = ResolveCatchUpFrame(nextTickedFrame, true, predictionFrame);
                driveTargetFrame = ResolveDriveTargetFrame(
                    inputTargetFrame,
                    options.Plan.Authority.EnableClientPrediction,
                    true,
                    predictionWindow);
                if (!DidAdvancePredictionFrame(predictionFrameBeforeStep, nextTickedFrame))
                {
                    break;
                }
            }

            inputSource.TrimBefore(SessionSimRuntimeTuning.ResolveInputTrimBeforeFrame(
                nextTickedFrame,
                handles.Consumable.LastConsumedFrame));

            // 诊断：对比服务器 ACK 帧与驱动目标帧的偏差。
            // 偏差过大说明客户端时钟与服务器时钟未对齐，预测窗口需要调整。
            var ackFrame = options.LastServerAckFrame;
            if (ackFrame > 0)
            {
                var drift = driveTargetFrame - ackFrame;
                var driftTolerance = predictionWindow > 0 ? predictionWindow : 1;
                if (drift > driftTolerance * 2 || drift < -driftTolerance)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[RemoteDrivenTick] ServerAck drift detected. " +
                        $"serverAckFrame={ackFrame} driveTargetFrame={driveTargetFrame} drift={drift}");
                }
            }

            return nextTickedFrame;
        }

        internal static int ResolveDriveTargetFrame(
            int inputTargetFrame,
            bool predictionEnabled,
            bool hasPredictionWindow,
            int predictionWindow)
        {
            if (inputTargetFrame <= 0) return inputTargetFrame;
            if (!predictionEnabled || !hasPredictionWindow || predictionWindow <= 0)
                return inputTargetFrame;

            var target = (long)inputTargetFrame + predictionWindow;
            return target > int.MaxValue ? int.MaxValue : (int)target;
        }

        internal static bool ShouldDriveRuntime(
            int currentFrame,
            int driveTargetFrame,
            int remainingSteps)
        {
            return remainingSteps > 0 &&
                   driveTargetFrame > 0 &&
                   currentFrame < driveTargetFrame;
        }

        internal static int ResolveCatchUpFrame(
            int fallbackFrame,
            bool hasPredictionFrame,
            int predictionFrame)
        {
            return hasPredictionFrame && predictionFrame >= 0
                ? predictionFrame
                : fallbackFrame;
        }

        internal static bool DidAdvancePredictionFrame(int beforeStep, int afterStep)
        {
            return afterStep > beforeStep;
        }

        private static bool TryGetPredictionState(
            BattleSessionRemoteDrivenWorldRuntime handles,
            out int predictionFrame,
            out int predictionWindow)
        {
            predictionFrame = 0;
            predictionWindow = 0;
            if (handles?.Runtime == null || handles.World == null) return false;
            if (!handles.Runtime.Features.TryGetFeature<IClientPredictionDriverStats>(out var stats) ||
                stats == null ||
                !stats.TryGetFrames(handles.World.Id, out _, out var predicted))
            {
                return false;
            }

            predictionFrame = predicted.Value;
            stats.TryGetPredictionWindowStats(
                handles.World.Id,
                out _,
                out _,
                out predictionWindow,
                out _);
            if (predictionWindow < 0) predictionWindow = 0;
            return true;
        }
    }
}
