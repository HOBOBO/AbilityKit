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
        public readonly int LastServerAckFrame;

        public RemoteDrivenWorldTickOptions(
            BattleStartPlan plan,
            BattleSessionHandles.RemoteDrivenHandles handles,
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

    internal static class RemoteDrivenWorldTickDriver
    {
        // P0-2 FIX: 预测超前帧数。与 RemoteDrivenRuntimeModuleFactory.CreateClientPredictionModule
        // 的 maxPredictionAheadFrames 参数保持一致。当 EnableClientPrediction=true 时，
        // driveTargetFrame 要加上这个窗口，让 CatchUpAndFeedSnapshots 的 while 循环
        // 给 ClientPredictionDriverModule.OnPreTick 的预测分支留执行步数。
        // 否则 world 只追到 jitter buffer 的 TargetFrame 就停，预测永远不执行。
        private const int DefaultPredictionAheadFrames = 30;

        public static int Tick(RemoteDrivenWorldTickOptions options)
        {
            var handles = options.Handles;
            var lastTickedFrame = options.LastTickedFrame;
            if (handles.World == null || handles.Runtime == null) return lastTickedFrame;
            if (handles.InputSource == null) return lastTickedFrame;

            var inputSource = handles.InputSource;
            var inputTargetFrame = inputSource.TargetFrame;
            if (inputTargetFrame <= 0) return lastTickedFrame;

            // P0-2 FIX: 当客户端预测开启时，driveTargetFrame 要加上预测窗口，
            // 让 world 超前推进于服务端已收到的帧。ClientPredictionDriverModule.OnPreTick
            // 的步骤 2（预测分支）只在 runtime.Tick 被调用时执行——如果 while 循环
            // 在 TargetFrame 就停了，预测分支永远不执行，等于没有客户端预测。
            var predictionWindow = options.Plan.Authority.EnableClientPrediction
                ? DefaultPredictionAheadFrames
                : 0;
            var driveTargetFrame = inputTargetFrame + predictionWindow;

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

            // 诊断：对比服务器 ACK 帧与驱动目标帧的偏差。
            // 偏差过大说明客户端时钟与服务器时钟未对齐，预测窗口需要调整。
            var ackFrame = options.LastServerAckFrame;
            if (ackFrame > 0)
            {
                var drift = driveTargetFrame - ackFrame;
                if (drift > DefaultPredictionAheadFrames * 2 || drift < -DefaultPredictionAheadFrames)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[RemoteDrivenTick] ServerAck drift detected. " +
                        $"serverAckFrame={ackFrame} driveTargetFrame={driveTargetFrame} drift={drift}");
                }
            }

            return nextTickedFrame;
        }
    }
}
