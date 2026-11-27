using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 配置到可执行节点的正式转换器。
    /// 仅接受 TypeName 驱动的正式配置；TypeId 兼容路径已移除。
    /// </summary>
    public sealed class ConfigToExecutableConverter
    {
        private readonly ExecutableConverterStrategyRegistry _strategyRegistry;
        private readonly IExecutableConverterStrategy _defaultStrategy;

        public ActionRegistry Actions { get; }

        public ConfigToExecutableConverter()
            : this(ExecutableConverterStrategyRegistry.Default, null, null)
        {
        }

        public ConfigToExecutableConverter(ExecutableConverterStrategyRegistry strategyRegistry = null, IExecutableConverterStrategy defaultStrategy = null, ActionRegistry actions = null)
        {
            _strategyRegistry = strategyRegistry ?? ExecutableConverterStrategyRegistry.Default;
            _defaultStrategy = defaultStrategy;
            Actions = actions;
        }

        public ISimpleExecutable Convert(ExecutableConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (!string.IsNullOrWhiteSpace(config.TypeName))
                return ConvertByTypeName(config);

            if (config.TypeId > 0)
                throw new NotSupportedException($"Executable config typeId={config.TypeId} is not supported by the formal triggering runtime. Use a formal TypeName instead.");

            throw new InvalidOperationException("Executable config must specify a formal executable TypeName.");
        }

        public ISimpleExecutable ConvertToSequence(List<ExecutableConfig> children)
        {
            var sequence = new SequenceExecutable();
            if (children == null)
                return sequence;

            foreach (var childConfig in children)
            {
                var child = Convert(childConfig);
                if (child != null)
                    sequence.Add(child);
            }

            return sequence;
        }

        public ICondition ConvertCondition(ConditionConfig condition)
        {
            if (condition == null)
                return null;

            var normalized = condition.TypeName?.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "payloadcompare":
                    return new PayloadCompareCondition();
                case "not":
                    return new NotCondition();
                case "and":
                    return new AndCondition();
                case "or":
                    return new OrCondition();
                case "numericcompare":
                    return new NumericCompareCondition();
                case "const":
                    return new ConstCondition();
                default:
                    throw new NotSupportedException($"Condition TypeName '{condition.TypeName}' is not supported by the formal triggering runtime.");
            }
        }

        public NumericValueRef ConvertNumericValueRef(NumericValueRefDto valueRef)
        {
            switch (valueRef.Kind)
            {
                case "Const":
                    return NumericValueRef.Const(valueRef.ConstValue);
                case "Var":
                    return NumericValueRef.Var(valueRef.DomainId, valueRef.Key);
                case "Blackboard":
                    return NumericValueRef.Blackboard(valueRef.BoardId, valueRef.KeyId);
                case "PayloadField":
                    return NumericValueRef.PayloadField(valueRef.FieldId);
                case "Expr":
                    return NumericValueRef.Expr(valueRef.ExprText);
                default:
                    return default;
            }
        }

        public ScheduleConfig ConvertSchedule(ScheduleConfig schedule)
        {
            return schedule;
        }

        public object EvaluateValueSelector(object selector, object ctx)
        {
            return selector;
        }

        public static ECompareOp ParseCompareOp(string compareOp)
        {
            if (string.IsNullOrWhiteSpace(compareOp))
                return ECompareOp.Equal;

            switch (compareOp.Trim().ToLowerInvariant())
            {
                case "eq":
                case "equal":
                    return ECompareOp.Equal;
                case "ne":
                case "notequal":
                    return ECompareOp.NotEqual;
                case "gt":
                case "greaterthan":
                    return ECompareOp.GreaterThan;
                case "ge":
                case "greaterthanorequal":
                    return ECompareOp.GreaterThanOrEqual;
                case "lt":
                case "lessthan":
                    return ECompareOp.LessThan;
                case "le":
                case "lessthanorequal":
                    return ECompareOp.LessThanOrEqual;
                default:
                    return ECompareOp.Equal;
            }
        }

        private ISimpleExecutable ConvertByTypeName(ExecutableConfig config)
        {
            var normalized = config.TypeName.Trim().ToLowerInvariant();
            var strategy = _strategyRegistry != null && _strategyRegistry.TryGetByTypeName(normalized, out var matched)
                ? matched
                : _defaultStrategy;

            if (strategy == null)
                throw new NotSupportedException($"Executable TypeName '{config.TypeName}' is not supported by the formal triggering runtime. Use Runtime.Plan executables or registered action logic instead.");

            return strategy.Convert(config, this);
        }

    }
}
