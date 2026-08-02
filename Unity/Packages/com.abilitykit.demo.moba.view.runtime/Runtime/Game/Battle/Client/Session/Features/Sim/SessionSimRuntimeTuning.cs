namespace AbilityKit.Game.Flow
{
    internal static class SessionSimRuntimeTuning
    {
        public const int MaxCatchUpStepsPerUpdate = 5;
        public const int RetainedInputFrames = 120;
        private const int MinimumGatewayInputLeadFrames = 2;

        public static int NormalizeInputDelayFrames(int inputDelayFrames)
        {
            return inputDelayFrames < 0 ? 0 : inputDelayFrames;
        }

        public static int ResolveInputTrimBeforeFrame(int lastTickedFrame, int lastConsumedFrame)
        {
            var retainedWindowFloor = lastTickedFrame - RetainedInputFrames;
            if (lastConsumedFrame < 0) return retainedWindowFloor < 0 ? retainedWindowFloor : 0;
            return System.Math.Min(retainedWindowFloor, lastConsumedFrame + 1);
        }

        public static int ResolveInputSubmitFrame(int lastObservedFrame, in BattleStartPlan plan)
        {
            if (plan.HostMode != BattleStartConfig.BattleHostMode.GatewayRemote ||
                !plan.Gateway.UseGatewayTransport)
            {
                return lastObservedFrame + 1;
            }

            var configuredDelay = NormalizeInputDelayFrames(plan.World.InputDelayFrames);
            var leadFrames = configuredDelay < MinimumGatewayInputLeadFrames
                ? MinimumGatewayInputLeadFrames
                : configuredDelay;
            return lastObservedFrame + 1 + leadFrames;
        }

        public static bool ShouldUseFrameSyncInput(BattleSyncMode syncMode)
        {
            return syncMode != BattleSyncMode.SnapshotAuthority;
        }
    }
}
