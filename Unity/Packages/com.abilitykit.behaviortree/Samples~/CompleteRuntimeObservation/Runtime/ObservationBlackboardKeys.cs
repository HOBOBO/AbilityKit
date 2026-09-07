namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation
{
    /// <summary>示例 authoring 文档与运行宿主共享的黑板契约。</summary>
    public static class ObservationBlackboardKeys
    {
        public const string Health = "self.health";
        public const string HasTarget = "self.hasTarget";
        public const string CanAct = "self.canAct";
        public const string TargetDistance = "self.targetDistance";
        public const string Stance = "self.stance";
        public const string Mode = "out.mode";
        public const string Busy = "out.busy";
    }
}
