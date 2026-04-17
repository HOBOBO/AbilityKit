using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Executable;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// Executable 转换策略接口
    /// 定义将 Config 转换为 Executable 的抽象
    /// </summary>
    public interface IExecutableConverterStrategy
    {
        /// <summary>
        /// 此策略处理的类型 ID（0 表示不通过 ID 处理）
        /// </summary>
        int TypeId { get; }

        /// <summary>
        /// 此策略处理的类型名称（null 表示不通过名称处理）
        /// </summary>
        string TypeName { get; }

        /// <summary>
        /// 是否可以处理此配置
        /// </summary>
        bool CanHandle(ExecutableConfig config);

        /// <summary>
        /// 将配置转换为 Executable
        /// </summary>
        ISimpleExecutable Convert(ExecutableConfig config, ConfigToExecutableConverter converter);
    }

    /// <summary>
    /// Executable 转换策略基类
    /// 提供通用功能
    /// </summary>
    public abstract class ExecutableConverterStrategyBase : IExecutableConverterStrategy
    {
        public virtual int TypeId => 0;
        public virtual string TypeName => null;

        public virtual bool CanHandle(ExecutableConfig config)
        {
            if (TypeId > 0 && config.TypeId == TypeId)
                return true;
            if (!string.IsNullOrEmpty(TypeName) && TypeName.Equals(config.TypeName, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        public abstract ISimpleExecutable Convert(ExecutableConfig config, ConfigToExecutableConverter converter);

        protected static SequenceExecutable ConvertChildrenToSequence(
            ConfigToExecutableConverter converter,
            ExecutableConfig config)
        {
            var sequence = new SequenceExecutable();
            if (config.Children != null)
            {
                foreach (var childConfig in config.Children)
                {
                    var child = converter.Convert(childConfig);
                    if (child != null)
                        sequence.Add(child);
                }
            }
            return sequence;
        }

        protected static ISimpleExecutable ConvertChildToExecutable(
            ConfigToExecutableConverter converter,
            ExecutableConfig config)
        {
            return converter.Convert(config);
        }
    }

    /// <summary>
    /// 复合行为转换策略基类（用于 Sequence, Selector, Parallel 等）
    /// </summary>
    public abstract class CompositeExecutableStrategyBase : ExecutableConverterStrategyBase
    {
        protected abstract ISimpleExecutable CreateExecutable();

        protected virtual void ApplyConfig(ISimpleExecutable executable, ExecutableConfig config, ConfigToExecutableConverter converter)
        {
            // 子类可重写以添加额外的配置应用逻辑
        }

        public override sealed ISimpleExecutable Convert(ExecutableConfig config, ConfigToExecutableConverter converter)
        {
            var executable = CreateExecutable();
            if (config.Children != null)
            {
                foreach (var childConfig in config.Children)
                {
                    var child = converter.Convert(childConfig);
                    if (child != null)
                        AddChild(executable, child);
                }
            }
            ApplyConfig(executable, config, converter);
            return executable;
        }

        protected virtual void AddChild(ISimpleExecutable parent, ISimpleExecutable child) { }
    }

    /// <summary>
    /// Sequence 转换策略
    /// </summary>
    public sealed class SequenceExecutableStrategy : CompositeExecutableStrategyBase
    {
        public override int TypeId => ExecutableModule.ExecutableTypeIds.Sequence;
        public override string TypeName => "sequence";

        protected override ISimpleExecutable CreateExecutable() => new SequenceExecutable();

        protected override void AddChild(ISimpleExecutable parent, ISimpleExecutable child)
        {
            if (parent is SequenceExecutable seq)
                seq.Add(child);
        }
    }

    /// <summary>
    /// Selector 转换策略
    /// </summary>
    public sealed class SelectorExecutableStrategy : CompositeExecutableStrategyBase
    {
        public override int TypeId => ExecutableModule.ExecutableTypeIds.Selector;
        public override string TypeName => "selector";

        protected override ISimpleExecutable CreateExecutable() => new SelectorExecutable();

        protected override void AddChild(ISimpleExecutable parent, ISimpleExecutable child)
        {
            if (parent is SelectorExecutable sel)
                sel.Add(child);
        }
    }

    /// <summary>
    /// Parallel 转换策略
    /// </summary>
    public sealed class ParallelExecutableStrategy : CompositeExecutableStrategyBase
    {
        public override int TypeId => ExecutableModule.ExecutableTypeIds.Parallel;
        public override string TypeName => "parallel";

        protected override ISimpleExecutable CreateExecutable() => new ParallelExecutable();

        protected override void AddChild(ISimpleExecutable parent, ISimpleExecutable child)
        {
            if (parent is ParallelExecutable parallel)
                parallel.Add(child);
        }
    }

    /// <summary>
    /// RandomSelector 转换策略
    /// </summary>
    public sealed class RandomSelectorExecutableStrategy : CompositeExecutableStrategyBase
    {
        public override int TypeId => ExecutableModule.ExecutableTypeIds.RandomSelector;
        public override string TypeName => "randomselector";

        protected override ISimpleExecutable CreateExecutable() => new RandomSelectorExecutable();

        protected override void AddChild(ISimpleExecutable parent, ISimpleExecutable child)
        {
            if (parent is RandomSelectorExecutable random)
                random.Children.Add(child);
        }
    }

    /// <summary>
    /// Repeat 转换策略
    /// </summary>
    public sealed class RepeatExecutableStrategy : ExecutableConverterStrategyBase
    {
        public override int TypeId => ExecutableModule.ExecutableTypeIds.Repeat;
        public override string TypeName => "repeat";

        public override ISimpleExecutable Convert(ExecutableConfig config, ConfigToExecutableConverter converter)
        {
            var repeat = new RepeatExecutable();
            if (config.Children != null && config.Children.Count > 0)
            {
                repeat.Child = converter.ConvertToSequence(config.Children);
            }
            return repeat;
        }
    }

    /// <summary>
    /// If 转换策略
    /// </summary>
    public sealed class IfExecutableStrategy : ExecutableConverterStrategyBase
    {
        public override int TypeId => ExecutableModule.ExecutableTypeIds.If;
        public override string TypeName => "if";

        public override ISimpleExecutable Convert(ExecutableConfig config, ConfigToExecutableConverter converter)
        {
            ICondition condition = null;
            if (config.Condition != null)
                condition = converter.ConvertCondition(config.Condition);
            ISimpleExecutable body = null;
            if (config.Children != null && config.Children.Count > 0)
                body = converter.ConvertToSequence(config.Children);
            return new IfExecutable { Condition = condition, Body = body };
        }
    }

    /// <summary>
    /// IfElse 转换策略
    /// </summary>
    public sealed class IfElseExecutableStrategy : ExecutableConverterStrategyBase
    {
        public override int TypeId => ExecutableModule.ExecutableTypeIds.IfElse;
        public override string TypeName => "ifelse";

        public override bool CanHandle(ExecutableConfig config)
        {
            return base.CanHandle(config) ||
                   config.TypeName?.Equals("elseif", StringComparison.OrdinalIgnoreCase) == true;
        }

        public override ISimpleExecutable Convert(ExecutableConfig config, ConfigToExecutableConverter converter)
        {
            var ifElse = new IfElseExecutable();
            if (config.Children != null)
            {
                foreach (var childConfig in config.Children)
                {
                    if (childConfig.TypeId == ExecutableModule.ExecutableTypeIds.If ||
                        childConfig.TypeName?.Equals("if", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        ICondition condition = null;
                        if (childConfig.Condition != null)
                            condition = converter.ConvertCondition(childConfig.Condition);
                        ISimpleExecutable body = null;
                        if (childConfig.Children != null && childConfig.Children.Count > 0)
                            body = converter.ConvertToSequence(childConfig.Children);
                        ifElse.If(condition, body);
                    }
                    else if (childConfig.TypeName?.Equals("else", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        ISimpleExecutable body = null;
                        if (childConfig.Children != null && childConfig.Children.Count > 0)
                            body = converter.ConvertToSequence(childConfig.Children);
                        ifElse.Else(body);
                    }
                }
            }
            return ifElse;
        }
    }

    /// <summary>
    /// Switch 转换策略
    /// </summary>
    public sealed class SwitchExecutableStrategy : ExecutableConverterStrategyBase
    {
        public override int TypeId => ExecutableModule.ExecutableTypeIds.Switch;
        public override string TypeName => "switch";

        public override bool CanHandle(ExecutableConfig config)
        {
            return base.CanHandle(config) || config.Switch != null;
        }

        public override ISimpleExecutable Convert(ExecutableConfig config, ConfigToExecutableConverter converter)
        {
            var switchExec = new SwitchExecutable();
            if (config.Switch != null)
            {
                if (!string.IsNullOrEmpty(config.Switch.ValueSelector))
                    switchExec.ValueSelector = ctx => converter.EvaluateValueSelector(config.Switch.ValueSelector, ctx);
                if (config.Switch.Cases != null)
                {
                    foreach (var caseConfig in config.Switch.Cases)
                    {
                        ISimpleExecutable body = null;
                        if (caseConfig.Body != null)
                            body = converter.Convert(caseConfig.Body);
                        switchExec.Case(caseConfig.Value, body);
                    }
                }
                if (config.Switch.DefaultCase != null)
                    switchExec.Default(converter.Convert(config.Switch.DefaultCase));
            }
            return switchExec;
        }
    }

    /// <summary>
    /// ActionCall 转换策略
    /// </summary>
    public sealed class ActionCallExecutableStrategy : ExecutableConverterStrategyBase
    {
        public override int TypeId => ExecutableModule.ExecutableTypeIds.ActionCall;
        public override string TypeName => "actioncall";

        public override bool CanHandle(ExecutableConfig config)
        {
            return base.CanHandle(config) ||
                   config.TypeName?.Equals("action", StringComparison.OrdinalIgnoreCase) == true ||
                   config.ActionCall.HasValue;
        }

        public override ISimpleExecutable Convert(ExecutableConfig config, ConfigToExecutableConverter converter)
        {
            var actionConfig = config.ActionCall.Value;
            var actionId = new ActionId(actionConfig.ActionId);

            var arg0 = converter.ConvertNumericValueRef(actionConfig.Arg0);
            var arg1 = converter.ConvertNumericValueRef(actionConfig.Arg1);

            return new ActionCallExecutable
            {
                ActionId = actionId,
                Arity = actionConfig.Arity,
                Arg0 = arg0,
                Arg1 = arg1
            };
        }
    }

    /// <summary>
    /// Delay 转换策略
    /// </summary>
    public sealed class DelayExecutableStrategy : ExecutableConverterStrategyBase
    {
        public override int TypeId => ExecutableModule.ExecutableTypeIds.Delay;
        public override string TypeName => "delay";

        public override bool CanHandle(ExecutableConfig config)
        {
            return base.CanHandle(config) || config.Delay.HasValue;
        }

        public override ISimpleExecutable Convert(ExecutableConfig config, ConfigToExecutableConverter converter)
        {
            var delayConfig = config.Delay.Value;
            return new DelayExecutable { DelayMs = delayConfig.DelayMs };
        }
    }

    /// <summary>
    /// Schedule 转换策略
    /// </summary>
    public sealed class ScheduleExecutableStrategy : ExecutableConverterStrategyBase
    {
        public override int TypeId => ExecutableModule.ExecutableTypeIds.Schedule;
        public override string TypeName => "schedule";

        public override bool CanHandle(ExecutableConfig config)
        {
            return base.CanHandle(config) ||
                   !string.IsNullOrEmpty(config.Schedule.ScheduleMode);
        }

        public override ISimpleExecutable Convert(ExecutableConfig config, ConfigToExecutableConverter converter)
        {
            return converter.ConvertSchedule(config);
        }
    }

    /// <summary>
    /// 推断型转换策略（当没有指定类型时，根据配置内容推断）
    /// </summary>
    public sealed class InferenceExecutableStrategy : ExecutableConverterStrategyBase
    {
        public override bool CanHandle(ExecutableConfig config)
        {
            return config.TypeId == 0 && string.IsNullOrEmpty(config.TypeName);
        }

        public override ISimpleExecutable Convert(ExecutableConfig config, ConfigToExecutableConverter converter)
        {
            if (config.Children != null && config.Children.Count > 0)
                return ConvertChildrenToSequence(converter, config);
            if (config.ActionCall.HasValue)
                return new ActionCallExecutableStrategy().Convert(config, converter);
            if (config.Delay.HasValue)
                return new DelayExecutableStrategy().Convert(config, converter);
            if (config.Switch != null)
                return new SwitchExecutableStrategy().Convert(config, converter);
            if (!string.IsNullOrEmpty(config.Schedule.ScheduleMode))
                return new ScheduleExecutableStrategy().Convert(config, converter);
            return new SequenceExecutable();
        }
    }
}
