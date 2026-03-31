namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 无操作的 TriggerCue 实现（单例）
    /// 当 TriggerPlan 未关联 Cue 时使用此实例
    /// </summary>
    public sealed class NullTriggerCue : ITriggerCue
    {
        public static readonly NullTriggerCue Instance = new NullTriggerCue();

        private NullTriggerCue() { }

        public void OnConditionPassed(in TriggerCueContext context) { }
        public void OnConditionFailed(in TriggerCueContext context) { }
        public void OnBeforeAction(in TriggerCueContext context, int actionIndex) { }
        public void OnExecuted(in TriggerCueContext context) { }
        public void OnInterrupted(in TriggerCueContext context) { }
        public void OnSkipped(in TriggerCueContext context) { }
    }
}
