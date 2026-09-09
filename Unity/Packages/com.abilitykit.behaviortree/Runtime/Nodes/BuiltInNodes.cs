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
                new[] { "无", "自身", "低优先级", "两者" },
                (long)AbortType.None,
                "条件中止类型");

            Composite(BuiltInNodeTypes.Sequence, "顺序", () => new SequenceNode());
            Composite(BuiltInNodeTypes.Selector, "选择", () => new SelectorNode());

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Parallel, "并行", "组合节点", NodeKind.Composite, 1, -1,
                () => new ParallelNode(),
                new[]
                {
                    abortField,
                    PropertyField.Enum(ParallelNode.SuccessPolicyProperty,
                        new[] { "全部成功", "任一成功" }, 0, "成功判定策略"),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.RandomSelector, "随机选择", "组合节点", NodeKind.Composite, 1, -1,
                () => new RandomSelectorNode(),
                new[] { abortField }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.RandomSequence, "随机顺序", "组合节点", NodeKind.Composite, 1, -1,
                () => new RandomSequenceNode(),
                new[] { abortField }));

            Decorator(BuiltInNodeTypes.Inverter, "结果取反", () => new InverterNode());
            Decorator(BuiltInNodeTypes.ForceSuccess, "强制成功", () => new ForceSuccessNode());
            Decorator(BuiltInNodeTypes.ForceFailure, "强制失败", () => new ForceFailureNode());

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Repeater, "重复执行", "装饰节点", NodeKind.Decorator, 1, 1,
                () => new RepeaterNode(),
                new[] { new PropertyField(RepeaterNode.CountProperty, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                    PropertyValue.Of(1L), "重复次数；-1 表示永久重复") }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Retry, "失败重试", "装饰节点", NodeKind.Decorator, 1, 1,
                () => new RetryNode(),
                new[] { new PropertyField(RetryNode.CountProperty, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                    PropertyValue.Of(1L), "失败后的最大重试次数；-1 表示无限重试") }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Timeout, "超时", "装饰节点", NodeKind.Decorator, 1, 1,
                () => new TimeoutNode(),
                new[] { new PropertyField(TimeoutNode.DurationSecondsProperty, AbilityKit.BehaviorTree.Definition.ValueType.Fixed64,
                    PropertyValue.Of(Fixed64.One), "超时时长，单位为定点数秒") }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Cooldown, "冷却", "装饰节点", NodeKind.Decorator, 1, 1,
                () => new CooldownNode(),
                new[]
                {
                    new PropertyField(CooldownNode.CooldownSecondsProperty, AbilityKit.BehaviorTree.Definition.ValueType.Fixed64,
                        PropertyValue.Of(Fixed64.One), "冷却时长，单位为定点数秒"),
                    PropertyField.Enum(CooldownNode.ResultOnCooldownProperty,
                        new[] { "失败", "成功" }, 0, "冷却期间返回的结果"),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Once, "仅执行一次", "装饰节点", NodeKind.Decorator, 1, 1,
                () => new OnceNode(),
                new[] { PropertyField.Enum(OnceNode.ResultAfterFirstProperty,
                    new[] { "失败", "成功" }, 0, "首次执行后返回的结果") }));

            Decorator(BuiltInNodeTypes.UntilSuccess, "直到成功", () => new UntilSuccessNode());
            Decorator(BuiltInNodeTypes.UntilFailure, "直到失败", () => new UntilFailureNode());

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.BlackboardCompare, "黑板值比较", "条件节点", NodeKind.Condition, 0, 0,
                () => new BlackboardCompareNode(),
                new[]
                {
                    PropertyField.KeyRef(BlackboardCompareNode.LeftKeyProperty, "左侧黑板键", order: 0),
                    PropertyField.Enum(BlackboardCompareNode.OpProperty,
                        new[] { "等于", "不等于", "小于", "小于等于", "大于", "大于等于" }, 0, "比较运算符", order: 1),
                    PropertyField.Enum(BlackboardCompareNode.RightKindProperty,
                        new[] { "常量", "黑板键" }, 0, "右操作数来源", order: 2),
                    PropertyField.KeyRef(BlackboardCompareNode.RightKeyProperty, "右操作数选择黑板键时使用的键", order: 3),
                    new PropertyField(BlackboardCompareNode.RightBoolProperty, AbilityKit.BehaviorTree.Definition.ValueType.Bool,
                        PropertyValue.Of(false), "右侧 Bool 常量", order: 4),
                    new PropertyField(BlackboardCompareNode.RightInt64Property, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                        PropertyValue.Of(0L), "右侧 Int64 常量", order: 5),
                    new PropertyField(BlackboardCompareNode.RightFixed64RawProperty, AbilityKit.BehaviorTree.Definition.ValueType.Fixed64,
                        PropertyValue.Of(Fixed64.Zero), "右侧 Fixed64 常量", order: 6),
                    new PropertyField(BlackboardCompareNode.RightStringProperty, AbilityKit.BehaviorTree.Definition.ValueType.String,
                        PropertyValue.Of(""), "右侧字符串常量", order: 7),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Probability, "概率判定", "条件节点", NodeKind.Condition, 0, 0,
                () => new ProbabilityNode(),
                new[] { new PropertyField(ProbabilityNode.PercentProperty, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                    PropertyValue.Of(50L), "通过概率 [0,100]", min: 0, max: 100) }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.BlackboardHasKey, "检查黑板键", "条件节点", NodeKind.Condition, 0, 0,
                () => new BlackboardHasKeyNode(),
                new[] { PropertyField.KeyRef(BlackboardHasKeyNode.KeyProperty, "要检查的黑板键") }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Wait, "等待", "动作节点", NodeKind.Action, 0, 0,
                () => new WaitNode(),
                new[]
                {
                    PropertyField.Enum(WaitNode.ModeProperty, new[] { "时间", "帧数" }, 0, "计时模式", order: 0),
                    new PropertyField(WaitNode.DurationSecondsProperty, AbilityKit.BehaviorTree.Definition.ValueType.Fixed64,
                        PropertyValue.Of(Fixed64.One), "时间模式下的等待时长，单位为定点数秒", order: 1),
                    new PropertyField(WaitNode.DurationFramesProperty, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                        PropertyValue.Of(30L), "帧数模式下的等待帧数", order: 2),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.SetBlackboard, "设置黑板值", "动作节点", NodeKind.Action, 0, 0,
                () => new SetBlackboardNode(),
                new[]
                {
                    PropertyField.KeyRef(SetBlackboardNode.KeyProperty, "目标黑板键", order: 0),
                    PropertyField.Enum(SetBlackboardNode.ValueKindProperty,
                        new[] { "常量", "从黑板键复制" }, 0, "值来源", order: 1),
                    PropertyField.KeyRef(SetBlackboardNode.FromKeyProperty, "复制时使用的来源黑板键", order: 2),
                    new PropertyField(SetBlackboardNode.ConstBoolProperty, AbilityKit.BehaviorTree.Definition.ValueType.Bool,
                        PropertyValue.Of(false), "Bool 常量", order: 3),
                    new PropertyField(SetBlackboardNode.ConstInt64Property, AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                        PropertyValue.Of(0L), "Int64 常量", order: 4),
                    new PropertyField(SetBlackboardNode.ConstFixed64Property, AbilityKit.BehaviorTree.Definition.ValueType.Fixed64,
                        PropertyValue.Of(Fixed64.Zero), "Fixed64 常量", order: 5),
                    new PropertyField(SetBlackboardNode.ConstStringProperty, AbilityKit.BehaviorTree.Definition.ValueType.String,
                        PropertyValue.Of(""), "字符串常量", order: 6),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Log, "输出日志", "动作节点", NodeKind.Action, 0, 0,
                () => new LogNode(),
                new[]
                {
                    new PropertyField(LogNode.MessageProperty, AbilityKit.BehaviorTree.Definition.ValueType.String,
                        PropertyValue.Of(""), "日志内容", order: 0),
                    PropertyField.Enum(LogNode.LevelProperty,
                        new[] { "跟踪", "信息", "警告", "错误" }, 1, "日志级别", order: 1),
                }));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Succeed, "返回成功", "动作节点", NodeKind.Action, 0, 0,
                () => new SucceedNode()));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Fail, "返回失败", "动作节点", NodeKind.Action, 0, 0,
                () => new FailNode()));

            registry.RegisterOrReplace(new NodeDescriptor(
                BuiltInNodeTypes.Subtree, "子树", "动作节点", NodeKind.Action, 0, 0,
                () => new SubtreeNode(),
                new[] { new PropertyField(SubtreeNode.TreeIdProperty, AbilityKit.BehaviorTree.Definition.ValueType.String,
                    PropertyValue.Of(""), "加载时展开的引用树 ID") },
                colorHint: "#6a5acd"));

            void Composite(string typeId, string displayName, Func<NodeBase> factory)
            {
                registry.RegisterOrReplace(new NodeDescriptor(
                    typeId, displayName, "组合节点", NodeKind.Composite, 1, -1,
                    factory, new[] { abortField }));
            }

            void Decorator(string typeId, string displayName, Func<NodeBase> factory)
            {
                registry.RegisterOrReplace(new NodeDescriptor(
                    typeId, displayName, "装饰节点", NodeKind.Decorator, 1, 1, factory));
            }
        }
    }
}
