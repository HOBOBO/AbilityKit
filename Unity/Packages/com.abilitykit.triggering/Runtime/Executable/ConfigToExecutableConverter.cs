using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 配置转行为实例的转换器
    /// </summary>
    public sealed class ConfigToExecutableConverter
    {
        private readonly FunctionRegistry _functions;
        private readonly ActionRegistry _actions;
        private readonly IIdNameRegistry _idNames;
        private readonly ScheduledExecutableFactoryRegistry _scheduledFactoryRegistry;

        public ConfigToExecutableConverter(
            FunctionRegistry functions,
            ActionRegistry actions,
            IIdNameRegistry idNames = null,
            ScheduledExecutableFactoryRegistry scheduledFactoryRegistry = null)
        {
            _functions = functions ?? throw new ArgumentNullException(nameof(functions));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _idNames = idNames;
            _scheduledFactoryRegistry = scheduledFactoryRegistry ?? ScheduledExecutableFactoryRegistry.Default;
        }

        /// <summary>
        /// 从配置创建行为实例
        /// </summary>
        public ISimpleExecutable Convert(ExecutableConfig config)
        {
            if (config.TypeId > 0)
                return ConvertByTypeId(config);
            if (!string.IsNullOrEmpty(config.TypeName))
                return ConvertByTypeName(config);
            return InferFromConfig(config);
        }

        /// <summary>
        /// 从配置列表创建 Sequence
        /// </summary>
        public SequenceExecutable ConvertToSequence(List<ExecutableConfig> configs)
        {
            var sequence = new SequenceExecutable();
            if (configs == null || configs.Count == 0)
                return sequence;
            foreach (var config in configs)
            {
                var executable = Convert(config);
                if (executable != null)
                    sequence.Add(executable);
            }
            return sequence;
        }

        private ISimpleExecutable ConvertByTypeId(ExecutableConfig config)
        {
            return config.TypeId switch
            {
                ExecutableTypeIds.Sequence => ConvertSequence(config),
                ExecutableTypeIds.Selector => ConvertSelector(config),
                ExecutableTypeIds.Parallel => ConvertParallel(config),
                ExecutableTypeIds.If => ConvertIf(config),
                ExecutableTypeIds.IfElse => ConvertIfElse(config),
                ExecutableTypeIds.Switch => ConvertSwitch(config),
                ExecutableTypeIds.RandomSelector => ConvertRandomSelector(config),
                ExecutableTypeIds.Repeat => ConvertRepeat(config),
                ExecutableTypeIds.ActionCall => ConvertActionCall(config),
                ExecutableTypeIds.Delay => ConvertDelay(config),
                ExecutableTypeIds.Schedule => ConvertSchedule(config),
                _ => throw new NotSupportedException($"Executable type id {config.TypeId} not supported")
            };
        }

        private ISimpleExecutable ConvertByTypeName(ExecutableConfig config)
        {
            return config.TypeName?.ToLowerInvariant() switch
            {
                "sequence" => ConvertSequence(config),
                "selector" => ConvertSelector(config),
                "parallel" => ConvertParallel(config),
                "if" => ConvertIf(config),
                "ifelse" => ConvertIfElse(config),
                "elseif" => ConvertIfElse(config),
                "switch" => ConvertSwitch(config),
                "randomselector" => ConvertRandomSelector(config),
                "repeat" => ConvertRepeat(config),
                "actioncall" => ConvertActionCall(config),
                "action" => ConvertActionCall(config),
                "delay" => ConvertDelay(config),
                "schedule" => ConvertSchedule(config),
                _ => throw new NotSupportedException($"Executable type name '{config.TypeName}' not supported")
            };
        }

        private ISimpleExecutable InferFromConfig(ExecutableConfig config)
        {
            if (config.Children != null && config.Children.Count > 0)
                return ConvertSequence(config);
            if (config.ActionCall.HasValue)
                return ConvertActionCall(config);
            if (config.Delay.HasValue)
                return ConvertDelay(config);
            if (config.Switch != null)
                return ConvertSwitch(config);
            if (!string.IsNullOrEmpty(config.Schedule.ScheduleMode))
                return ConvertSchedule(config);
            return new SequenceExecutable();
        }

        private SequenceExecutable ConvertSequence(ExecutableConfig config)
        {
            var sequence = new SequenceExecutable();
            if (config.Children != null)
            {
                foreach (var childConfig in config.Children)
                {
                    var child = Convert(childConfig);
                    if (child != null)
                        sequence.Add(child);
                }
            }
            return sequence;
        }

        private SelectorExecutable ConvertSelector(ExecutableConfig config)
        {
            var selector = new SelectorExecutable();
            if (config.Children != null)
            {
                foreach (var childConfig in config.Children)
                {
                    var child = Convert(childConfig);
                    if (child != null)
                        selector.Add(child);
                }
            }
            return selector;
        }

        private ParallelExecutable ConvertParallel(ExecutableConfig config)
        {
            var parallel = new ParallelExecutable();
            if (config.Children != null)
            {
                foreach (var childConfig in config.Children)
                {
                    var child = Convert(childConfig);
                    if (child != null)
                        parallel.Add(child);
                }
            }
            return parallel;
        }

        private RandomSelectorExecutable ConvertRandomSelector(ExecutableConfig config)
        {
            var random = new RandomSelectorExecutable();
            if (config.Children != null)
            {
                foreach (var childConfig in config.Children)
                {
                    var child = Convert(childConfig);
                    if (child != null)
                        random.Children.Add(child);
                }
            }
            return random;
        }

        private RepeatExecutable ConvertRepeat(ExecutableConfig config)
        {
            var repeat = new RepeatExecutable();
            if (config.Children != null && config.Children.Count > 0)
            {
                repeat.Child = ConvertToSequence(config.Children);
            }
            return repeat;
        }

        private IfExecutable ConvertIf(ExecutableConfig config)
        {
            ICondition condition = null;
            if (config.Condition != null)
                condition = ConvertCondition(config.Condition);
            ISimpleExecutable body = null;
            if (config.Children != null && config.Children.Count > 0)
                body = ConvertToSequence(config.Children);
            return new IfExecutable { Condition = condition, Body = body };
        }

        private IfElseExecutable ConvertIfElse(ExecutableConfig config)
        {
            var ifElse = new IfElseExecutable();
            if (config.Children != null)
            {
                foreach (var childConfig in config.Children)
                {
                    if (childConfig.TypeId == ExecutableTypeIds.If || childConfig.TypeName == "If")
                    {
                        ICondition condition = null;
                        if (childConfig.Condition != null)
                            condition = ConvertCondition(childConfig.Condition);
                        ISimpleExecutable body = null;
                        if (childConfig.Children != null && childConfig.Children.Count > 0)
                            body = ConvertToSequence(childConfig.Children);
                        ifElse.If(condition, body);
                    }
                    else if (childConfig.TypeName == "Else")
                    {
                        ISimpleExecutable body = null;
                        if (childConfig.Children != null && childConfig.Children.Count > 0)
                            body = ConvertToSequence(childConfig.Children);
                        ifElse.Else(body);
                    }
                }
            }
            return ifElse;
        }

        private SwitchExecutable ConvertSwitch(ExecutableConfig config)
        {
            var switchExec = new SwitchExecutable();
            if (config.Switch != null)
            {
                if (!string.IsNullOrEmpty(config.Switch.ValueSelector))
                    switchExec.ValueSelector = ctx => EvaluateValueSelector(config.Switch.ValueSelector, ctx);
                if (config.Switch.Cases != null)
                {
                    foreach (var caseConfig in config.Switch.Cases)
                    {
                        ISimpleExecutable body = null;
                        if (caseConfig.Body != null)
                            body = Convert(caseConfig.Body);
                        switchExec.Case(caseConfig.Value, body);
                    }
                }
                if (config.Switch.DefaultCase != null)
                    switchExec.Default(Convert(config.Switch.DefaultCase));
            }
            return switchExec;
        }

        private ActionCallExecutable ConvertActionCall(ExecutableConfig config)
        {
            var actionConfig = config.ActionCall.Value;
            return new ActionCallExecutable
            {
                ActionId = new ActionId(actionConfig.ActionId),
                Arity = actionConfig.Arity,
                Arg0 = ConvertNumericValueRef(actionConfig.Arg0),
                Arg1 = ConvertNumericValueRef(actionConfig.Arg1),
                Actions = _actions
            };
        }

        private DelayExecutable ConvertDelay(ExecutableConfig config)
        {
            var delayConfig = config.Delay.Value;
            return new DelayExecutable { DelayMs = delayConfig.DelayMs };
        }

        /// <summary>
        /// 转换调度行为（通过工厂注册表创建具体实现）
        /// </summary>
        private IScheduledExecutable ConvertSchedule(ExecutableConfig config)
        {
            var mode = config.Schedule.ScheduleMode ?? "external";

            // 转换 Body 子行为
            ISimpleExecutable body = null;
            if (config.Children != null && config.Children.Count > 0)
            {
                body = ConvertToSequence(config.Children);
            }

            // 创建调度配置
            var scheduleConfig = new ScheduleFactoryConfig
            {
                Mode = mode,
                DurationMs = config.Schedule.DurationMs,
                PeriodMs = config.Schedule.PeriodMs,
                MaxExecutions = config.Schedule.MaxExecutions,
                CanBeInterrupted = config.Schedule.CanBeInterrupted
            };

            // 使用工厂注册表创建
            var scheduledExec = _scheduledFactoryRegistry.Create(mode, scheduleConfig, _actions, null);
            if (scheduledExec == null)
                return null;

            // 设置 Inner
            if (scheduledExec is IScheduledExecutable schedExec)
            {
                SetInnerIfPossible(schedExec, body);
                SetModifiersIfPossible(schedExec, config.Schedule.Modifiers, config.TypeId);
            }

            return scheduledExec;
        }

        private static void SetModifiersIfPossible(IScheduledExecutable exec, List<ModifierDataConfig> configs, int sourceId)
        {
            if (exec is ModifierApplyingPeriodicExecutable modifierExec && configs != null)
            {
                modifierExec.Modifiers = ConvertModifiers(configs, sourceId);
                modifierExec.SourceId = sourceId;
            }
        }

        private static void SetInnerIfPossible(IScheduledExecutable exec, ISimpleExecutable body)
        {
            var innerProp = exec.GetType().GetProperty("Inner");
            innerProp?.SetValue(exec, body);
        }

        /// <summary>
        /// 将 ModifierDataConfig 列表转换为 ModifierData 数组
        /// </summary>
        private static ModifierData[] ConvertModifiers(List<ModifierDataConfig> configs, int sourceId)
        {
            if (configs == null || configs.Count == 0)
                return null;

            var modifiers = new ModifierData[configs.Count];
            for (int i = 0; i < configs.Count; i++)
            {
                modifiers[i] = ConvertModifierData(configs[i], sourceId);
            }
            return modifiers;
        }

        /// <summary>
        /// 将单个 ModifierDataConfig 转换为 ModifierData
        /// </summary>
        private static ModifierData ConvertModifierData(ModifierDataConfig config, int sourceId)
        {
            var op = ParseModifierOp(config.ModifierType);
            var key = ParseModifierKey(config.Key);

            return new ModifierData
            {
                Key = key,
                Op = op,
                Value = config.Value,
                MagnitudeSource = MagnitudeType.None,
                SourceId = sourceId
            };
        }

        private static ModifierOp ParseModifierOp(string modifierType)
        {
            if (string.IsNullOrEmpty(modifierType))
                return ModifierOp.Add;

            return modifierType.ToLowerInvariant() switch
            {
                "add" or "+" => ModifierOp.Add,
                "mul" or "*" or "multiply" => ModifierOp.Mul,
                "override" or "=" or "set" => ModifierOp.Override,
                "percentadd" or "%" or "percent" => ModifierOp.PercentAdd,
                _ => ModifierOp.Add
            };
        }

        private static ModifierKey ParseModifierKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return ModifierKey.None;

            // 支持预定义键的字符串映射
            return key.ToLowerInvariant() switch
            {
                "movespeed" or "movespeed%max" => ModifierKey.MoveSpeed,
                "shieldmax" or "shield" => ModifierKey.ShieldMax,
                "shieldregen" => ModifierKey.ShieldRegen,
                "dotdamage" or "dot" => ModifierKey.DOTDamage,
                "hotheal" or "hot" => ModifierKey.HOTHeal,
                "healtaken" => ModifierKey.HealTaken,
                _ => ModifierKey.Create(key.GetHashCode() & 0xFF)
            };
        }

        /// <summary>
        /// 转换条件
        /// </summary>
        public ICondition ConvertCondition(ConditionConfig config)
        {
            if (config.TypeId > 0)
                return ConvertConditionByTypeId(config);
            if (!string.IsNullOrEmpty(config.TypeName))
                return ConvertConditionByTypeName(config);
            return InferCondition(config);
        }

        private ICondition ConvertConditionByTypeId(ConditionConfig config)
        {
            return config.TypeId switch
            {
                ConditionTypeIds.Const => new ConstCondition { Value = true },
                ConditionTypeIds.And => ConvertAndCondition(config),
                ConditionTypeIds.Or => ConvertOrCondition(config),
                ConditionTypeIds.Not => ConvertNotCondition(config),
                ConditionTypeIds.NumericCompare => ConvertNumericCompare(config),
                ConditionTypeIds.PayloadCompare => ConvertPayloadCompare(config),
                ConditionTypeIds.HasTarget => ConvertHasTarget(config),
                ConditionTypeIds.Multi => ConvertMultiCondition(config),
                _ => throw new NotSupportedException($"Condition type id {config.TypeId} not supported")
            };
        }

        private ICondition ConvertConditionByTypeName(ConditionConfig config)
        {
            return config.TypeName.ToLowerInvariant() switch
            {
                "const" => new ConstCondition { Value = true },
                "and" => ConvertAndCondition(config),
                "or" => ConvertOrCondition(config),
                "not" => ConvertNotCondition(config),
                "numericcompare" => ConvertNumericCompare(config),
                "payloadcompare" => ConvertPayloadCompare(config),
                "hastarget" => ConvertHasTarget(config),
                "multi" => ConvertMultiCondition(config),
                _ => throw new NotSupportedException($"Condition type name '{config.TypeName}' not supported")
            };
        }

        private ICondition InferCondition(ConditionConfig config)
        {
            if (config.Children != null && config.Children.Count > 0)
            {
                var combinator = config.Combinator?.ToLowerInvariant() ?? "and";
                var multi = new MultiCondition
                {
                    Combinator = combinator == "or" ? EConditionCombinator.Or : EConditionCombinator.And
                };
                foreach (var childConfig in config.Children)
                    multi.Add(ConvertCondition(childConfig));
                return multi;
            }
            if (config.FieldId > 0)
                return ConvertPayloadCompare(config);
            if (config.Left.HasValue && config.Right.HasValue)
                return ConvertNumericCompare(config);
            return new ConstCondition { Value = true };
        }

        private AndCondition ConvertAndCondition(ConditionConfig config)
        {
            ICondition left = null, right = null;
            if (config.Children != null && config.Children.Count >= 2)
            {
                left = ConvertCondition(config.Children[0]);
                right = ConvertCondition(config.Children[1]);
            }
            return new AndCondition { Left = left, Right = right };
        }

        private OrCondition ConvertOrCondition(ConditionConfig config)
        {
            ICondition left = null, right = null;
            if (config.Children != null && config.Children.Count >= 2)
            {
                left = ConvertCondition(config.Children[0]);
                right = ConvertCondition(config.Children[1]);
            }
            return new OrCondition { Left = left, Right = right };
        }

        private NotCondition ConvertNotCondition(ConditionConfig config)
        {
            ICondition inner = null;
            if (config.Children != null && config.Children.Count > 0)
                inner = ConvertCondition(config.Children[0]);
            return new NotCondition { Inner = inner };
        }

        private NumericCompareCondition ConvertNumericCompare(ConditionConfig config)
        {
            var op = ParseCompareOp(config.CompareOp);
            return new NumericCompareCondition
            {
                Op = op,
                Left = ConvertNumericValueRef(config.Left),
                Right = ConvertNumericValueRef(config.Right)
            };
        }

        private PayloadCompareCondition ConvertPayloadCompare(ConditionConfig config)
        {
            var op = ParseCompareOp(config.CompareOp);
            return new PayloadCompareCondition
            {
                FieldId = config.FieldId,
                Op = op,
                CompareValue = ConvertNumericValueRef(config.CompareValue),
                Negate = config.Negate
            };
        }

        private HasTargetCondition ConvertHasTarget(ConditionConfig config)
            => new HasTargetCondition { Negate = config.Negate };

        private MultiCondition ConvertMultiCondition(ConditionConfig config)
        {
            var combinator = config.Combinator?.ToLowerInvariant() ?? "and";
            var multi = new MultiCondition
            {
                Combinator = combinator == "or" ? EConditionCombinator.Or : EConditionCombinator.And
            };
            if (config.Children != null)
            {
                foreach (var childConfig in config.Children)
                    multi.Add(ConvertCondition(childConfig));
            }
            return multi;
        }

        private NumericValueRef ConvertNumericValueRef(NumericValueRefDto dto)
        {
            if (!dto.HasValue)
                return default;
            return dto.Kind?.ToLowerInvariant() switch
            {
                "const" => NumericValueRef.Const(dto.ConstValue),
                "blackboard" => NumericValueRef.Blackboard(dto.BoardId, dto.KeyId),
                "payload" or "payloadfield" => NumericValueRef.PayloadField(dto.FieldId),
                "var" => NumericValueRef.Var(dto.DomainId, dto.Key),
                _ => NumericValueRef.Const(dto.ConstValue)
            };
        }

        private ECompareOp ParseCompareOp(string op)
        {
            if (string.IsNullOrEmpty(op))
                return ECompareOp.Equal;
            return op.ToLowerInvariant() switch
            {
                "eq" or "equal" or "==" => ECompareOp.Equal,
                "ne" or "notequal" or "!=" => ECompareOp.NotEqual,
                "gt" or "greaterthan" or ">" => ECompareOp.GreaterThan,
                "ge" or "greaterthanorequal" or ">=" => ECompareOp.GreaterThanOrEqual,
                "lt" or "lessthan" or "<" => ECompareOp.LessThan,
                "le" or "lessthanorequal" or "<=" => ECompareOp.LessThanOrEqual,
                _ => ECompareOp.Equal
            };
        }

        private int EvaluateValueSelector(string expression, object ctx)
        {
            return 0;
        }
    }
}
