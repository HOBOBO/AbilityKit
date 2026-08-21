using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>内置节点类型 id 常量（导出 JSON 中以字符串出现，保持稳定）。</summary>
    public static class BtBuiltInNodeTypes
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
    }

    /// <summary>内置节点目录注册入口。</summary>
    public static class BtBuiltInNodes
    {
        /// <summary>把全部内置节点描述符注册进注册中心（幂等，覆盖语义）。</summary>
        public static void RegisterAll(BtNodeRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var abortField = BtPropertyField.Enum(
                BtCompositeNode.AbortTypeProperty,
                new[] { "None", "Self", "LowerPriority", "Both" },
                (long)BtAbortType.None,
                "条件中断类型");

            Composite(BtBuiltInNodeTypes.Sequence, "顺序 Sequence", () => new BtSequenceNode());
            Composite(BtBuiltInNodeTypes.Selector, "选择 Selector", () => new BtSelectorNode());

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.Parallel, "并行 Parallel", "组合节点", BtNodeKind.Composite, 1, -1,
                () => new BtParallelNode(),
                new[]
                {
                    abortField,
                    BtPropertyField.Enum(BtParallelNode.SuccessPolicyProperty,
                        new[] { "全部成功", "任一成功" }, 0, "成功策略"),
                }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.RandomSelector, "随机选择 RandomSelector", "组合节点", BtNodeKind.Composite, 1, -1,
                () => new BtRandomSelectorNode(),
                new[] { abortField }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.RandomSequence, "随机顺序 RandomSequence", "组合节点", BtNodeKind.Composite, 1, -1,
                () => new BtRandomSequenceNode(),
                new[] { abortField }));

            Decorator(BtBuiltInNodeTypes.Inverter, "反转 Inverter", () => new BtInverterNode());
            Decorator(BtBuiltInNodeTypes.ForceSuccess, "强制成功 ForceSuccess", () => new BtForceSuccessNode());
            Decorator(BtBuiltInNodeTypes.ForceFailure, "强制失败 ForceFailure", () => new BtForceFailureNode());

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.Repeater, "重复 Repeater", "装饰节点", BtNodeKind.Decorator, 1, 1,
                () => new BtRepeaterNode(),
                new[] { new BtPropertyField(BtRepeaterNode.CountProperty, BtValueType.Int64,
                    BtPropertyValue.Of(1L), "重复次数；-1 表示永久") }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.Retry, "重试 Retry", "装饰节点", BtNodeKind.Decorator, 1, 1,
                () => new BtRetryNode(),
                new[] { new BtPropertyField(BtRetryNode.CountProperty, BtValueType.Int64,
                    BtPropertyValue.Of(1L), "失败后的最大重试次数；-1 表示无限") }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.Timeout, "超时 Timeout", "装饰节点", BtNodeKind.Decorator, 1, 1,
                () => new BtTimeoutNode(),
                new[] { new BtPropertyField(BtTimeoutNode.DurationSecondsProperty, BtValueType.Fixed64,
                    BtPropertyValue.Of(Fixed64.One), "超时时长（定点秒）") }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.Cooldown, "冷却 Cooldown", "装饰节点", BtNodeKind.Decorator, 1, 1,
                () => new BtCooldownNode(),
                new[]
                {
                    new BtPropertyField(BtCooldownNode.CooldownSecondsProperty, BtValueType.Fixed64,
                        BtPropertyValue.Of(Fixed64.One), "冷却时长（定点秒）"),
                    BtPropertyField.Enum(BtCooldownNode.ResultOnCooldownProperty,
                        new[] { "Failure", "Success" }, 0, "冷却期内完成结果"),
                }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.Once, "一次 Once", "装饰节点", BtNodeKind.Decorator, 1, 1,
                () => new BtOnceNode(),
                new[] { BtPropertyField.Enum(BtOnceNode.ResultAfterFirstProperty,
                    new[] { "Failure", "Success" }, 0, "首次之后的完成结果") }));

            Decorator(BtBuiltInNodeTypes.UntilSuccess, "直到成功 UntilSuccess", () => new BtUntilSuccessNode());
            Decorator(BtBuiltInNodeTypes.UntilFailure, "直到失败 UntilFailure", () => new BtUntilFailureNode());

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.BlackboardCompare, "黑板比较 Compare", "条件节点", BtNodeKind.Condition, 0, 0,
                () => new BtBlackboardCompareNode(),
                new[]
                {
                    BtPropertyField.KeyRef(BtBlackboardCompareNode.LeftKeyProperty, "左侧黑板 key", order: 0),
                    BtPropertyField.Enum(BtBlackboardCompareNode.OpProperty,
                        new[] { "等于", "不等于", "小于", "小于等于", "大于", "大于等于" }, 0, "比较运算符", order: 1),
                    BtPropertyField.Enum(BtBlackboardCompareNode.RightKindProperty,
                        new[] { "常量", "key" }, 0, "右侧来源", order: 2),
                    BtPropertyField.KeyRef(BtBlackboardCompareNode.RightKeyProperty, "右侧黑板 key（来源=key 时有效）", order: 3),
                    new BtPropertyField(BtBlackboardCompareNode.RightBoolProperty, BtValueType.Bool,
                        BtPropertyValue.Of(false), "右侧常量（Bool）", order: 4),
                    new BtPropertyField(BtBlackboardCompareNode.RightInt64Property, BtValueType.Int64,
                        BtPropertyValue.Of(0L), "右侧常量（Int64）", order: 5),
                    new BtPropertyField(BtBlackboardCompareNode.RightFixed64RawProperty, BtValueType.Fixed64,
                        BtPropertyValue.Of(Fixed64.Zero), "右侧常量（Fixed64）", order: 6),
                    new BtPropertyField(BtBlackboardCompareNode.RightStringProperty, BtValueType.String,
                        BtPropertyValue.Of(""), "右侧常量（String）", order: 7),
                }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.Probability, "概率 Probability", "条件节点", BtNodeKind.Condition, 0, 0,
                () => new BtProbabilityNode(),
                new[] { new BtPropertyField(BtProbabilityNode.PercentProperty, BtValueType.Int64,
                    BtPropertyValue.Of(50L), "通过百分比 [0,100]", min: 0, max: 100) }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.BlackboardHasKey, "黑板有值 HasKey", "条件节点", BtNodeKind.Condition, 0, 0,
                () => new BtBlackboardHasKeyNode(),
                new[] { BtPropertyField.KeyRef(BtBlackboardHasKeyNode.KeyProperty, "黑板 key") }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.Wait, "等待 Wait", "动作节点", BtNodeKind.Action, 0, 0,
                () => new BtWaitNode(),
                new[]
                {
                    BtPropertyField.Enum(BtWaitNode.ModeProperty, new[] { "按时间", "按帧数" }, 0, "计时方式", order: 0),
                    new BtPropertyField(BtWaitNode.DurationSecondsProperty, BtValueType.Fixed64,
                        BtPropertyValue.Of(Fixed64.One), "等待时长（定点秒，mode=时间）", order: 1),
                    new BtPropertyField(BtWaitNode.DurationFramesProperty, BtValueType.Int64,
                        BtPropertyValue.Of(30L), "等待帧数（mode=帧数）", order: 2),
                }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.SetBlackboard, "写黑板 SetBlackboard", "动作节点", BtNodeKind.Action, 0, 0,
                () => new BtSetBlackboardNode(),
                new[]
                {
                    BtPropertyField.KeyRef(BtSetBlackboardNode.KeyProperty, "目标黑板 key", order: 0),
                    BtPropertyField.Enum(BtSetBlackboardNode.ValueKindProperty,
                        new[] { "常量", "复制自 key" }, 0, "取值方式", order: 1),
                    BtPropertyField.KeyRef(BtSetBlackboardNode.FromKeyProperty, "来源黑板 key（复制模式）", order: 2),
                    new BtPropertyField(BtSetBlackboardNode.ConstBoolProperty, BtValueType.Bool,
                        BtPropertyValue.Of(false), "常量（Bool）", order: 3),
                    new BtPropertyField(BtSetBlackboardNode.ConstInt64Property, BtValueType.Int64,
                        BtPropertyValue.Of(0L), "常量（Int64）", order: 4),
                    new BtPropertyField(BtSetBlackboardNode.ConstFixed64Property, BtValueType.Fixed64,
                        BtPropertyValue.Of(Fixed64.Zero), "常量（Fixed64）", order: 5),
                    new BtPropertyField(BtSetBlackboardNode.ConstStringProperty, BtValueType.String,
                        BtPropertyValue.Of(""), "常量（String）", order: 6),
                }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.Log, "日志 Log", "动作节点", BtNodeKind.Action, 0, 0,
                () => new BtLogNode(),
                new[]
                {
                    new BtPropertyField(BtLogNode.MessageProperty, BtValueType.String,
                        BtPropertyValue.Of(""), "日志内容", order: 0),
                    BtPropertyField.Enum(BtLogNode.LevelProperty,
                        new[] { "Trace", "Info", "Warning", "Error" }, 1, "日志级别", order: 1),
                }));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.Succeed, "恒成功 Succeed", "动作节点", BtNodeKind.Action, 0, 0,
                () => new BtSucceedNode()));

            registry.RegisterOrReplace(new BtNodeDescriptor(
                BtBuiltInNodeTypes.Fail, "恒失败 Fail", "动作节点", BtNodeKind.Action, 0, 0,
                () => new BtFailNode()));

            void Composite(string typeId, string displayName, Func<BtNodeBase> factory)
            {
                registry.RegisterOrReplace(new BtNodeDescriptor(
                    typeId, displayName, "组合节点", BtNodeKind.Composite, 1, -1,
                    factory, new[] { abortField }));
            }

            void Decorator(string typeId, string displayName, Func<BtNodeBase> factory)
            {
                registry.RegisterOrReplace(new BtNodeDescriptor(
                    typeId, displayName, "装饰节点", BtNodeKind.Decorator, 1, 1, factory));
            }
        }
    }
}
