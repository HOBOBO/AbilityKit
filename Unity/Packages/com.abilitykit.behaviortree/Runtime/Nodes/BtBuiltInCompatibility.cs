namespace AbilityKit.BehaviorTree
{
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.", false)]
    public static class BtBuiltInNodeTypes
    {
        public const string Sequence = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Sequence;
        public const string Selector = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Selector;
        public const string Parallel = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Parallel;
        public const string RandomSelector = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.RandomSelector;
        public const string RandomSequence = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.RandomSequence;
        public const string Inverter = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Inverter;
        public const string ForceSuccess = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.ForceSuccess;
        public const string ForceFailure = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.ForceFailure;
        public const string Repeater = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Repeater;
        public const string Retry = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Retry;
        public const string Timeout = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Timeout;
        public const string Cooldown = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Cooldown;
        public const string Once = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Once;
        public const string UntilSuccess = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.UntilSuccess;
        public const string UntilFailure = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.UntilFailure;
        public const string BlackboardCompare = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.BlackboardCompare;
        public const string Probability = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Probability;
        public const string BlackboardHasKey = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.BlackboardHasKey;
        public const string Wait = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Wait;
        public const string SetBlackboard = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.SetBlackboard;
        public const string Log = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Log;
        public const string Succeed = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Succeed;
        public const string Fail = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Fail;
        public const string Subtree = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes.Subtree;
    }

    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.BuiltInNodes.", false)]
    public static class BtBuiltInNodes
    {
        public static void RegisterAll(BtNodeRegistry registry)
        {
            if (registry == null) throw new System.ArgumentNullException(nameof(registry));

            var canonical = new AbilityKit.BehaviorTree.Registry.NodeRegistry();
            AbilityKit.BehaviorTree.Nodes.BuiltInNodes.RegisterAll(canonical);
            foreach (var descriptor in canonical.Descriptors)
            {
                registry.RegisterOrReplace(descriptor.ToLegacy());
            }
        }
    }

    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.SequenceNode.", false)]
    public sealed class BtSequenceNode : AbilityKit.BehaviorTree.Nodes.SequenceNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.SelectorNode.", false)]
    public sealed class BtSelectorNode : AbilityKit.BehaviorTree.Nodes.SelectorNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.ParallelNode.", false)]
    public sealed class BtParallelNode : AbilityKit.BehaviorTree.Nodes.ParallelNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.RandomSelectorNode.", false)]
    public sealed class BtRandomSelectorNode : AbilityKit.BehaviorTree.Nodes.RandomSelectorNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.RandomSequenceNode.", false)]
    public sealed class BtRandomSequenceNode : AbilityKit.BehaviorTree.Nodes.RandomSequenceNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.InverterNode.", false)]
    public sealed class BtInverterNode : AbilityKit.BehaviorTree.Nodes.InverterNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.ForceSuccessNode.", false)]
    public sealed class BtForceSuccessNode : AbilityKit.BehaviorTree.Nodes.ForceSuccessNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.ForceFailureNode.", false)]
    public sealed class BtForceFailureNode : AbilityKit.BehaviorTree.Nodes.ForceFailureNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.RepeaterNode.", false)]
    public sealed class BtRepeaterNode : AbilityKit.BehaviorTree.Nodes.RepeaterNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.RetryNode.", false)]
    public sealed class BtRetryNode : AbilityKit.BehaviorTree.Nodes.RetryNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.TimeoutNode.", false)]
    public sealed class BtTimeoutNode : AbilityKit.BehaviorTree.Nodes.TimeoutNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.CooldownNode.", false)]
    public sealed class BtCooldownNode : AbilityKit.BehaviorTree.Nodes.CooldownNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.OnceNode.", false)]
    public sealed class BtOnceNode : AbilityKit.BehaviorTree.Nodes.OnceNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.UntilSuccessNode.", false)]
    public sealed class BtUntilSuccessNode : AbilityKit.BehaviorTree.Nodes.UntilSuccessNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.UntilFailureNode.", false)]
    public sealed class BtUntilFailureNode : AbilityKit.BehaviorTree.Nodes.UntilFailureNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.BlackboardCompareNode.", false)]
    public sealed class BtBlackboardCompareNode : AbilityKit.BehaviorTree.Nodes.BlackboardCompareNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.ProbabilityNode.", false)]
    public sealed class BtProbabilityNode : AbilityKit.BehaviorTree.Nodes.ProbabilityNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.BlackboardHasKeyNode.", false)]
    public sealed class BtBlackboardHasKeyNode : AbilityKit.BehaviorTree.Nodes.BlackboardHasKeyNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.WaitNode.", false)]
    public sealed class BtWaitNode : AbilityKit.BehaviorTree.Nodes.WaitNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.SetBlackboardNode.", false)]
    public sealed class BtSetBlackboardNode : AbilityKit.BehaviorTree.Nodes.SetBlackboardNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.LogNode.", false)]
    public sealed class BtLogNode : AbilityKit.BehaviorTree.Nodes.LogNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.SucceedNode.", false)]
    public sealed class BtSucceedNode : AbilityKit.BehaviorTree.Nodes.SucceedNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.FailNode.", false)]
    public sealed class BtFailNode : AbilityKit.BehaviorTree.Nodes.FailNode { }
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.SubtreeNode.", false)]
    public sealed class BtSubtreeNode : AbilityKit.BehaviorTree.Nodes.SubtreeNode { }
}
