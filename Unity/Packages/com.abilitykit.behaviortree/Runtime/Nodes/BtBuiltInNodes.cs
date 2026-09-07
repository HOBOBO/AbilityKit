using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>Stable built-in node type ids used in runtime JSON.</summary>
    public static class BuiltInNodeTypes
    {
        public const string Sequence = "builtin.sequence";
        public const string Selector = "builtin.selector";
        public const string Parallel = "builtin.parallel";
        public const string RandomSelector = "builtin.randomSelector";
        public const string RandomSequence = "builtin.randomSequence";

        public const string Inverter = "builtin.inverter";
        public const string ForceSuccess = "builtin.forceSuccess";
        public const string ForceFailure = "builtin.forceFailure";
        public const string Repeater = "builtin.repeater";
        public const string Retry = "builtin.retry";
        public const string Timeout = "builtin.timeout";
        public const string Cooldown = "builtin.cooldown";
        public const string Once = "builtin.once";
        public const string UntilSuccess = "builtin.untilSuccess";
        public const string UntilFailure = "builtin.untilFailure";

        public const string BlackboardCompare = "builtin.blackboardCompare";
        public const string Probability = "builtin.probability";
        public const string BlackboardHasKey = "builtin.blackboardHasKey";

        public const string Wait = "builtin.wait";
        public const string SetBlackboard = "builtin.setBlackboard";
        public const string Log = "builtin.log";
        public const string Succeed = "builtin.succeed";
        public const string Fail = "builtin.fail";
        public const string Subtree = "builtin.subtree";
    }

    /// <summary>Registers built-in behavior tree node descriptors.</summary>
    public static class BuiltInNodes
    {
        /// <summary>Registers all built-in descriptors into the supplied registry.</summary>
        public static void RegisterAll(NodeRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var abortField = PropertyField.Enum(
                CompositeNode.AbortTypeProperty,
                new[] { "None", "Self", "LowerPriority", "Both" },
                (long)AbortType.None,
                "Conditional abort type");

            Composite(BuiltInNodeTypes.Sequence, "Sequence", () => new SequenceNode());
            Composite(BuiltInNodeTypes.Selector, "Selector", () => new SelectorNode());

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Parallel, "Parallel", "Composite", NodeKind.Composite, 1, -1,
                () => new ParallelNode(),
                new[]
                {
                    abortField,
                    PropertyField.Enum(ParallelNode.SuccessPolicyProperty,
                        new[] { "RequireAll", "FirstSuccess" }, 0, "Success policy"),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.RandomSelector, "Random Selector", "Composite", NodeKind.Composite, 1, -1,
                () => new RandomSelectorNode(),
                new[] { abortField }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.RandomSequence, "Random Sequence", "Composite", NodeKind.Composite, 1, -1,
                () => new RandomSequenceNode(),
                new[] { abortField }));

            Decorator(BuiltInNodeTypes.Inverter, "Inverter", () => new InverterNode());
            Decorator(BuiltInNodeTypes.ForceSuccess, "Force Success", () => new ForceSuccessNode());
            Decorator(BuiltInNodeTypes.ForceFailure, "Force Failure", () => new ForceFailureNode());

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Repeater, "Repeater", "Decorator", NodeKind.Decorator, 1, 1,
                () => new RepeaterNode(),
                new[] { new PropertyField(RepeaterNode.CountProperty, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                    PropertyValue.Of(1L), "Repeat count; -1 means forever") }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Retry, "Retry", "Decorator", NodeKind.Decorator, 1, 1,
                () => new RetryNode(),
                new[] { new PropertyField(RetryNode.CountProperty, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                    PropertyValue.Of(1L), "Maximum retries after failure; -1 means unlimited") }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Timeout, "Timeout", "Decorator", NodeKind.Decorator, 1, 1,
                () => new TimeoutNode(),
                new[] { new PropertyField(TimeoutNode.DurationSecondsProperty, AbilityKit.BehaviorTree.Definition.ValueType.Fixed64,
                    PropertyValue.Of(Fixed64.One), "Timeout duration in fixed-point seconds") }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Cooldown, "Cooldown", "Decorator", NodeKind.Decorator, 1, 1,
                () => new CooldownNode(),
                new[]
                {
                    new PropertyField(CooldownNode.CooldownSecondsProperty, AbilityKit.BehaviorTree.Definition.ValueType.Fixed64,
                        PropertyValue.Of(Fixed64.One), "Cooldown duration in fixed-point seconds"),
                    PropertyField.Enum(CooldownNode.ResultOnCooldownProperty,
                        new[] { "Failure", "Success" }, 0, "Result while on cooldown"),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Once, "Once", "Decorator", NodeKind.Decorator, 1, 1,
                () => new OnceNode(),
                new[] { PropertyField.Enum(OnceNode.ResultAfterFirstProperty,
                    new[] { "Failure", "Success" }, 0, "Result after the first execution") }));

            Decorator(BuiltInNodeTypes.UntilSuccess, "Until Success", () => new UntilSuccessNode());
            Decorator(BuiltInNodeTypes.UntilFailure, "Until Failure", () => new UntilFailureNode());

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.BlackboardCompare, "Blackboard Compare", "Condition", NodeKind.Condition, 0, 0,
                () => new BlackboardCompareNode(),
                new[]
                {
                    PropertyField.KeyRef(BlackboardCompareNode.LeftKeyProperty, "Left blackboard key", order: 0),
                    PropertyField.Enum(BlackboardCompareNode.OpProperty,
                        new[] { "Equal", "NotEqual", "LessThan", "LessOrEqual", "GreaterThan", "GreaterOrEqual" }, 0, "Comparison operator", order: 1),
                    PropertyField.Enum(BlackboardCompareNode.RightKindProperty,
                        new[] { "Constant", "Key" }, 0, "Right operand source", order: 2),
                    PropertyField.KeyRef(BlackboardCompareNode.RightKeyProperty, "Right blackboard key used when source is Key", order: 3),
                    new PropertyField(BlackboardCompareNode.RightBoolProperty, AbilityKit.BehaviorTree.Definition.ValueType.Bool,
                        PropertyValue.Of(false), "Right constant (Bool)", order: 4),
                    new PropertyField(BlackboardCompareNode.RightInt64Property, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                        PropertyValue.Of(0L), "Right constant (Int64)", order: 5),
                    new PropertyField(BlackboardCompareNode.RightFixed64RawProperty, AbilityKit.BehaviorTree.Definition.ValueType.Fixed64,
                        PropertyValue.Of(Fixed64.Zero), "Right constant (Fixed64)", order: 6),
                    new PropertyField(BlackboardCompareNode.RightStringProperty, AbilityKit.BehaviorTree.Definition.ValueType.String,
                        PropertyValue.Of(""), "Right constant (String)", order: 7),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Probability, "Probability", "Condition", NodeKind.Condition, 0, 0,
                () => new ProbabilityNode(),
                new[] { new PropertyField(ProbabilityNode.PercentProperty, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                    PropertyValue.Of(50L), "Pass percentage [0,100]", min: 0, max: 100) }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.BlackboardHasKey, "Blackboard Has Key", "Condition", NodeKind.Condition, 0, 0,
                () => new BlackboardHasKeyNode(),
                new[] { PropertyField.KeyRef(BlackboardHasKeyNode.KeyProperty, "Blackboard key") }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Wait, "Wait", "Action", NodeKind.Action, 0, 0,
                () => new WaitNode(),
                new[]
                {
                    PropertyField.Enum(WaitNode.ModeProperty, new[] { "Time", "Frames" }, 0, "Timing mode", order: 0),
                    new PropertyField(WaitNode.DurationSecondsProperty, AbilityKit.BehaviorTree.Definition.ValueType.Fixed64,
                        PropertyValue.Of(Fixed64.One), "Wait duration in fixed-point seconds when mode is Time", order: 1),
                    new PropertyField(WaitNode.DurationFramesProperty, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                        PropertyValue.Of(30L), "Wait frame count when mode is Frames", order: 2),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.SetBlackboard, "Set Blackboard", "Action", NodeKind.Action, 0, 0,
                () => new SetBlackboardNode(),
                new[]
                {
                    PropertyField.KeyRef(SetBlackboardNode.KeyProperty, "Target blackboard key", order: 0),
                    PropertyField.Enum(SetBlackboardNode.ValueKindProperty,
                        new[] { "Constant", "CopyFromKey" }, 0, "Value source", order: 1),
                    PropertyField.KeyRef(SetBlackboardNode.FromKeyProperty, "Source blackboard key when copying", order: 2),
                    new PropertyField(SetBlackboardNode.ConstBoolProperty, AbilityKit.BehaviorTree.Definition.ValueType.Bool,
                        PropertyValue.Of(false), "Constant (Bool)", order: 3),
                    new PropertyField(SetBlackboardNode.ConstInt64Property, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                        PropertyValue.Of(0L), "Constant (Int64)", order: 4),
                    new PropertyField(SetBlackboardNode.ConstFixed64Property, AbilityKit.BehaviorTree.Definition.ValueType.Fixed64,
                        PropertyValue.Of(Fixed64.Zero), "Constant (Fixed64)", order: 5),
                    new PropertyField(SetBlackboardNode.ConstStringProperty, AbilityKit.BehaviorTree.Definition.ValueType.String,
                        PropertyValue.Of(""), "Constant (String)", order: 6),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Log, "Log", "Action", NodeKind.Action, 0, 0,
                () => new LogNode(),
                new[]
                {
                    new PropertyField(LogNode.MessageProperty, AbilityKit.BehaviorTree.Definition.ValueType.String,
                        PropertyValue.Of(""), "Log message", order: 0),
                    PropertyField.Enum(LogNode.LevelProperty,
                        new[] { "Trace", "Info", "Warning", "Error" }, 1, "Log level", order: 1),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Succeed, "Succeed", "Action", NodeKind.Action, 0, 0,
                () => new SucceedNode()));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Fail, "Fail", "Action", NodeKind.Action, 0, 0,
                () => new FailNode()));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Subtree, "Subtree", "Action", NodeKind.Action, 0, 0,
                () => new SubtreeNode(),
                new[] { new PropertyField(SubtreeNode.TreeIdProperty, AbilityKit.BehaviorTree.Definition.ValueType.String,
                    PropertyValue.Of(""), "Referenced tree id expanded during load") },
                colorHint: "#6a5acd"));

            void Composite(string typeId, string displayName, Func<NodeBase> factory)
            {
                registry.RegisterOrReplace(new NodeDescriptor(
                    typeId, displayName, "Composite", NodeKind.Composite, 1, -1,
                    factory, new[] { abortField }));
            }

            void Decorator(string typeId, string displayName, Func<NodeBase> factory)
            {
                registry.RegisterOrReplace(new NodeDescriptor(
                    typeId, displayName, "Decorator", NodeKind.Decorator, 1, 1, factory));
            }
        }
    }
}
