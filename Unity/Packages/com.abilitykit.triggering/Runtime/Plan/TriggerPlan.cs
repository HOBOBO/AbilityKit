using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// Action 调用计划（参数化动作描述）
    /// </summary>
    public readonly struct ActionCallPlan
    {
        public readonly ActionId Id;
        public readonly byte Arity;
        public readonly NumericValueRef Arg0;
        public readonly NumericValueRef Arg1;

        /// <summary>
        /// 具名参数字典（key=参数名，value=参数值引用）
        /// 为 null 时表示向后兼容的位置参数模式（使用 Arg0/Arg1）
        /// </summary>
        public readonly Dictionary<string, ActionArgValue> Args;

        /// <summary>
        /// 调度模式（Action 自身如何运行）
        /// Immediate: 立即执行一次
        /// Delayed: 延迟执行（等待 ScheduleParam 毫秒）
        /// Periodic: 周期执行（每 ScheduleParam 毫秒）
        /// Continuous: 持续帧执行（每帧，直到外部终止）
        /// Timeline: 时间线执行（按时间轴序列）
        /// </summary>
        public readonly Config.EActionScheduleMode ScheduleMode;

        /// <summary>
        /// 调度参数
        /// Delayed: 延迟时间（毫秒）
        /// Periodic: 周期间隔（毫秒）
        /// Timeline: 时间线总时长（毫秒）
        /// </summary>
        public readonly float ScheduleParam;

        /// <summary>
        /// 最大执行次数（-1=无限，仅对 Periodic/Delayed 有效）
        /// </summary>
        public readonly int MaxExecutions;

        /// <summary>
        /// 是否可被中断（持续行为有效）
        /// </summary>
        public readonly bool CanBeInterrupted;

        /// <summary>
        /// 执行策略（单次执行的约束）
        /// </summary>
        public readonly Config.EActionExecutionPolicy ExecutionPolicy;

        /// <summary>
        /// 创建无参数的动作调用（默认 Immediate）
        /// </summary>
        public ActionCallPlan(ActionId id)
        {
            Id = id;
            Arity = 0;
            Arg0 = default;
            Arg1 = default;
            Args = null;
            ScheduleMode = Config.EActionScheduleMode.Immediate;
            ScheduleParam = 0;
            MaxExecutions = -1;
            CanBeInterrupted = true;
            ExecutionPolicy = Config.EActionExecutionPolicy.Immediate;
        }

        /// <summary>
        /// 创建带参数的动作调用（默认 Immediate）
        /// </summary>
        public ActionCallPlan(ActionId id, NumericValueRef arg0, NumericValueRef arg1 = default, NumericValueRef arg2 = default)
        {
            Id = id;
            if (arg2.Kind != ENumericValueRefKind.Const || arg2.ConstValue != 0 || !string.IsNullOrEmpty(arg2.Key) || !string.IsNullOrEmpty(arg2.ExprText))
            {
                Arity = 3;
                Arg0 = arg0;
                Arg1 = arg1;
                Args = new Dictionary<string, ActionArgValue> { ["_2"] = ActionArgValue.Of(arg2, "_2") };
            }
            else if (arg1.Kind != ENumericValueRefKind.Const || arg1.ConstValue != 0 || !string.IsNullOrEmpty(arg1.Key) || !string.IsNullOrEmpty(arg1.ExprText))
            {
                Arity = 2;
                Arg0 = arg0;
                Arg1 = arg1;
                Args = null;
            }
            else if (arg0.Kind != ENumericValueRefKind.Const || arg0.ConstValue != 0 || !string.IsNullOrEmpty(arg0.Key) || !string.IsNullOrEmpty(arg0.ExprText))
            {
                Arity = 1;
                Arg0 = arg0;
                Arg1 = default;
                Args = null;
            }
            else
            {
                Arity = 0;
                Arg0 = default;
                Arg1 = default;
                Args = null;
            }
            ScheduleMode = Config.EActionScheduleMode.Immediate;
            ScheduleParam = 0;
            MaxExecutions = -1;
            CanBeInterrupted = true;
            ExecutionPolicy = Config.EActionExecutionPolicy.Immediate;
        }

        /// <summary>
        /// 创建带常量参数的动作调用（默认 Immediate）
        /// </summary>
        public ActionCallPlan(ActionId id, params double[] constArgs)
        {
            Id = id;
            switch (constArgs.Length)
            {
                case 0:
                    Arity = 0;
                    Arg0 = default;
                    Arg1 = default;
                    Args = null;
                    break;
                case 1:
                    Arity = 1;
                    Arg0 = NumericValueRef.Const(constArgs[0]);
                    Arg1 = default;
                    Args = null;
                    break;
                case 2:
                    Arity = 2;
                    Arg0 = NumericValueRef.Const(constArgs[0]);
                    Arg1 = NumericValueRef.Const(constArgs[1]);
                    Args = null;
                    break;
                default:
                    Arity = (byte)constArgs.Length;
                    Arg0 = NumericValueRef.Const(constArgs[0]);
                    Arg1 = NumericValueRef.Const(constArgs[1]);
                    Args = new Dictionary<string, ActionArgValue>();
                    for (int i = 2; i < constArgs.Length; i++)
                        Args[$"__{i}"] = ActionArgValue.OfConst(constArgs[i], $"__{i}");
                    break;
            }
            ScheduleMode = Config.EActionScheduleMode.Immediate;
            ScheduleParam = 0;
            MaxExecutions = -1;
            CanBeInterrupted = true;
            ExecutionPolicy = Config.EActionExecutionPolicy.Immediate;
        }

        /// <summary>
        /// 创建带有具名参数的 ActionCallPlan（默认 Immediate）
        /// </summary>
        public static ActionCallPlan WithArgs(ActionId id, Dictionary<string, ActionArgValue> args)
        {
            return new ActionCallPlan(id, args);
        }

        private ActionCallPlan(ActionId id, Dictionary<string, ActionArgValue> args)
        {
            Id = id;
            Arity = (byte)(args != null ? args.Count : 0);
            Arg0 = default;
            Arg1 = default;
            Args = args;
            ScheduleMode = Config.EActionScheduleMode.Immediate;
            ScheduleParam = 0;
            MaxExecutions = -1;
            CanBeInterrupted = true;
            ExecutionPolicy = Config.EActionExecutionPolicy.Immediate;
        }

        /// <summary>
        /// 是否使用了具名参数模式
        /// </summary>
        public bool HasNamedArgs => Args != null && Args.Count > 0;

        /// <summary>
        /// 完整构造函数（用于扩展方法创建修改后的副本）
        /// </summary>
        public ActionCallPlan(
            ActionId id,
            byte arity,
            NumericValueRef arg0,
            NumericValueRef arg1,
            Dictionary<string, ActionArgValue> args,
            Config.EActionScheduleMode scheduleMode,
            float scheduleParam,
            int maxExecutions,
            bool canBeInterrupted,
            Config.EActionExecutionPolicy executionPolicy)
        {
            Id = id;
            Arity = arity;
            Arg0 = arg0;
            Arg1 = arg1;
            Args = args;
            ScheduleMode = scheduleMode;
            ScheduleParam = scheduleParam;
            MaxExecutions = maxExecutions;
            CanBeInterrupted = canBeInterrupted;
            ExecutionPolicy = executionPolicy;
        }
    }

    /// <summary>
    /// 触发器计划（不可变数据结构）
    /// </summary>
    public readonly struct TriggerPlan<TArgs>
    {
        public readonly int Phase;
        public readonly int Priority;

        /// <summary>
        /// 触发器 ID
        /// </summary>
        public readonly int TriggerId;

        /// <summary>
        /// 优先级打断阈值。Execute 成功后自动调用 StopBelowPriority。
        /// 0 = 不自动打断；>0 = 以此值为阈值打断更低优先级的触发器。
        /// </summary>
        public readonly int InterruptPriority;

        public readonly EPredicateKind PredicateKind;
        public readonly bool HasPredicate;
        public readonly FunctionId PredicateId;

        public readonly byte PredicateArity;
        public readonly NumericValueRef PredicateArg0;
        public readonly NumericValueRef PredicateArg1;

        public readonly PredicateExprPlan PredicateExpr;

        public readonly ActionCallPlan[] Actions;

        /// <summary>
        /// 表现层 Cue（VFX / SFX / UI 反馈）
        /// </summary>
        public readonly ITriggerCue Cue;

        /// <summary>
        /// 调度配置（持续行为相关）
        /// </summary>
        public readonly ScheduleModePlan Schedule;

        // ========== 核心构造器（保留 3 个）==========

        /// <summary>
        /// 无条件触发器构造器
        /// </summary>
        public TriggerPlan(
            int phase,
            int priority,
            int triggerId = 0,
            ActionCallPlan[] actions = null,
            int interruptPriority = 0,
            ITriggerCue cue = null,
            in ScheduleModePlan schedule = default)
        {
            Phase = phase;
            Priority = priority;
            TriggerId = triggerId;
            InterruptPriority = interruptPriority;
            PredicateKind = EPredicateKind.None;
            HasPredicate = false;
            PredicateId = default;
            PredicateArity = 0;
            PredicateArg0 = default;
            PredicateArg1 = default;
            PredicateExpr = default;
            Actions = actions ?? Array.Empty<ActionCallPlan>();
            Cue = cue ?? NullTriggerCue.Instance;
            Schedule = schedule;
        }

        /// <summary>
        /// 函数条件触发器构造器
        /// </summary>
        public TriggerPlan(
            int phase,
            int priority,
            int triggerId,
            FunctionId predicateId,
            NumericValueRef[] predicateArgs,
            ActionCallPlan[] actions = null,
            int interruptPriority = 0,
            ITriggerCue cue = null,
            in ScheduleModePlan schedule = default)
        {
            Phase = phase;
            Priority = priority;
            TriggerId = triggerId;
            InterruptPriority = interruptPriority;
            PredicateKind = EPredicateKind.Function;
            HasPredicate = true;
            PredicateId = predicateId;
            PredicateArity = (byte)(predicateArgs?.Length ?? 0);
            PredicateArg0 = predicateArgs?.Length > 0 ? predicateArgs[0] : default;
            PredicateArg1 = predicateArgs?.Length > 1 ? predicateArgs[1] : default;
            PredicateExpr = default;
            Actions = actions ?? Array.Empty<ActionCallPlan>();
            Cue = cue ?? NullTriggerCue.Instance;
            Schedule = schedule;
        }

        /// <summary>
        /// 表达式条件触发器构造器
        /// </summary>
        public TriggerPlan(
            int phase,
            int priority,
            int triggerId = 0,
            PredicateExprPlan predicateExpr = default,
            ActionCallPlan[] actions = null,
            int interruptPriority = 0,
            ITriggerCue cue = null,
            in ScheduleModePlan schedule = default)
        {
            Phase = phase;
            Priority = priority;
            TriggerId = triggerId;
            InterruptPriority = interruptPriority;
            PredicateKind = EPredicateKind.Expr;
            HasPredicate = predicateExpr.Nodes != null && predicateExpr.Nodes.Length > 0;
            PredicateId = default;
            PredicateArity = 0;
            PredicateArg0 = default;
            PredicateArg1 = default;
            PredicateExpr = predicateExpr;
            Actions = actions ?? Array.Empty<ActionCallPlan>();
            Cue = cue ?? NullTriggerCue.Instance;
            Schedule = schedule;
        }

        // ========== 便捷工厂方法==========

        /// <summary>
        /// 创建无条件触发器
        /// </summary>
        public static TriggerPlan<TArgs> Create(
            int phase = 0,
            int priority = 0,
            int interruptPriority = 0,
            params ActionCallPlan[] actions)
        {
            return new TriggerPlan<TArgs>(phase, priority, 0, actions, interruptPriority);
        }

        /// <summary>
        /// 创建带函数条件的触发器（无参数）
        /// </summary>
        public static TriggerPlan<TArgs> When(
            int phase,
            int priority,
            FunctionId predicateId,
            int interruptPriority = 0,
            params ActionCallPlan[] actions)
        {
            return new TriggerPlan<TArgs>(phase, priority, 0, predicateId, null, actions, interruptPriority, null, default);
        }

        /// <summary>
        /// 创建带函数条件的触发器（带参数）
        /// </summary>
        public static TriggerPlan<TArgs> When(
            int phase,
            int priority,
            FunctionId predicateId,
            NumericValueRef[] predicateArgs,
            int interruptPriority = 0,
            params ActionCallPlan[] actions)
        {
            return new TriggerPlan<TArgs>(phase, priority, 0, predicateId, predicateArgs, actions, interruptPriority, null, default);
        }

        /// <summary>
        /// 创建带表达式条件的触发器
        /// </summary>
        public static TriggerPlan<TArgs> WhenExpr(
            int phase,
            int priority,
            PredicateExprPlan predicateExpr,
            int interruptPriority = 0,
            params ActionCallPlan[] actions)
        {
            return new TriggerPlan<TArgs>(phase, priority, 0, predicateExpr, actions, interruptPriority, null, default);
        }

        /// <summary>
        /// 添加动作，返回新的 TriggerPlan
        /// </summary>
        public TriggerPlan<TArgs> AddActions(params ActionCallPlan[] actions)
        {
            var newActions = new ActionCallPlan[(Actions?.Length ?? 0) + actions.Length];
            if (Actions?.Length > 0)
                Array.Copy(Actions, newActions, Actions.Length);
            Array.Copy(actions, 0, newActions, Actions?.Length ?? 0, actions.Length);
            return new TriggerPlan<TArgs>(Phase, Priority, TriggerId, InterruptPriority, PredicateKind, HasPredicate, PredicateId,
                PredicateArity, PredicateArg0, PredicateArg1, PredicateExpr, newActions, Cue, in Schedule);
        }

        internal TriggerPlan(
            int phase, int priority, int triggerId, int interruptPriority,
            EPredicateKind predicateKind, bool hasPredicate, FunctionId predicateId,
            byte predicateArity, NumericValueRef predicateArg0, NumericValueRef predicateArg1,
            PredicateExprPlan predicateExpr,
            ActionCallPlan[] actions, ITriggerCue cue, in ScheduleModePlan schedule)
        {
            Phase = phase;
            Priority = priority;
            TriggerId = triggerId;
            InterruptPriority = interruptPriority;
            PredicateKind = predicateKind;
            HasPredicate = hasPredicate;
            PredicateId = predicateId;
            PredicateArity = predicateArity;
            PredicateArg0 = predicateArg0;
            PredicateArg1 = predicateArg1;
            PredicateExpr = predicateExpr;
            Actions = actions;
            Cue = cue;
            Schedule = schedule;
        }

        private TriggerPlan(
            int phase, int priority, int interruptPriority,
            EPredicateKind predicateKind, bool hasPredicate, FunctionId predicateId,
            byte predicateArity, NumericValueRef predicateArg0, NumericValueRef predicateArg1,
            PredicateExprPlan predicateExpr,
            ActionCallPlan[] actions, ITriggerCue cue, in ScheduleModePlan schedule)
        {
            Phase = phase;
            Priority = priority;
            TriggerId = 0;
            InterruptPriority = interruptPriority;
            PredicateKind = predicateKind;
            HasPredicate = hasPredicate;
            PredicateId = predicateId;
            PredicateArity = predicateArity;
            PredicateArg0 = predicateArg0;
            PredicateArg1 = predicateArg1;
            PredicateExpr = predicateExpr;
            Actions = actions;
            Cue = cue;
            Schedule = schedule;
        }
    }

    /// <summary>
    /// 调度模式运行时数据
    /// </summary>
    public readonly struct ScheduleModePlan
    {
        public static ScheduleModePlan None => default;

        /// <summary>
        /// 调度模式
        /// </summary>
        public readonly EScheduleMode Mode;

        /// <summary>
        /// 执行间隔（毫秒），0=每帧
        /// </summary>
        public readonly float IntervalMs;

        /// <summary>
        /// 最大执行次数，-1=无限
        /// </summary>
        public readonly int MaxExecutions;

        /// <summary>
        /// 是否可中断
        /// </summary>
        public readonly bool CanBeInterrupted;

        public ScheduleModePlan(EScheduleMode mode, float intervalMs = 0, int maxExecutions = -1, bool canBeInterrupted = true)
        {
            Mode = mode;
            IntervalMs = intervalMs;
            MaxExecutions = maxExecutions;
            CanBeInterrupted = canBeInterrupted;
        }

        public static ScheduleModePlan Continuous(float intervalMs = 0, int maxExecutions = -1, bool canBeInterrupted = true)
            => new ScheduleModePlan(EScheduleMode.Continuous, intervalMs, maxExecutions, canBeInterrupted);

        public static ScheduleModePlan Periodic(float intervalMs, int maxExecutions = -1)
            => new ScheduleModePlan(EScheduleMode.Periodic, intervalMs, maxExecutions, canBeInterrupted: true);

        public static ScheduleModePlan Timed(float delayMs)
            => new ScheduleModePlan(EScheduleMode.Timed, delayMs, 1, canBeInterrupted: true);
    }

    /// <summary>
    /// ActionCallPlan 的扩展方法
    /// </summary>
    public static class ActionCallPlanExtensions
    {
        /// <summary>
        /// 创建无参数的动作调用（默认 Immediate）
        /// </summary>
        public static ActionCallPlan Call(ActionId id)
        {
            return new ActionCallPlan(id);
        }

        /// <summary>
        /// 创建带一个参数的动作调用（默认 Immediate）
        /// </summary>
        public static ActionCallPlan Call(this ActionId id, NumericValueRef arg0)
        {
            return new ActionCallPlan(id, arg0);
        }

        /// <summary>
        /// 创建带两个参数的动作调用（默认 Immediate）
        /// </summary>
        public static ActionCallPlan Call(this ActionId id, NumericValueRef arg0, NumericValueRef arg1)
        {
            return new ActionCallPlan(id, arg0, arg1);
        }

        /// <summary>
        /// 创建带有具名参数的动作调用（默认 Immediate）
        /// </summary>
        public static ActionCallPlan CallArgs(this ActionId id, Dictionary<string, ActionArgValue> args)
        {
            return ActionCallPlan.WithArgs(id, args);
        }

        /// <summary>
        /// 创建带有两个具名参数的动作调用（默认 Immediate）
        /// </summary>
        public static ActionCallPlan CallArgs(this ActionId id, string name0, double value0, string name1, double value1)
        {
            return ActionCallPlan.WithArgs(id, new Dictionary<string, ActionArgValue>
            {
                [name0] = ActionArgValue.OfConst(value0, name0),
                [name1] = ActionArgValue.OfConst(value1, name1)
            });
        }

        /// <summary>
        /// 创建带有三个具名参数的动作调用（默认 Immediate）
        /// </summary>
        public static ActionCallPlan CallArgs(this ActionId id, string name0, double value0, string name1, double value1, string name2, double value2)
        {
            return ActionCallPlan.WithArgs(id, new Dictionary<string, ActionArgValue>
            {
                [name0] = ActionArgValue.OfConst(value0, name0),
                [name1] = ActionArgValue.OfConst(value1, name1),
                [name2] = ActionArgValue.OfConst(value2, name2)
            });
        }

        /// <summary>
        /// 创建带具名参数的动作调用（扩展方法版本）
        /// </summary>
        public static ActionCallPlan WithArgs(this ActionId id, Dictionary<string, ActionArgValue> args)
        {
            return ActionCallPlan.WithArgs(id, args);
        }

        /// <summary>
        /// 创建带两个具名参数的动作调用（扩展方法版本）
        /// </summary>
        public static ActionCallPlan WithArgs(this ActionId id, string name0, double value0, string name1, double value1)
        {
            return ActionCallPlan.WithArgs(id, new Dictionary<string, ActionArgValue>
            {
                [name0] = ActionArgValue.OfConst(value0, name0),
                [name1] = ActionArgValue.OfConst(value1, name1)
            });
        }

        /// <summary>
        /// 创建带三个具名参数的动作调用（扩展方法版本）
        /// </summary>
        public static ActionCallPlan WithArgs(this ActionId id, string name0, double value0, string name1, double value1, string name2, double value2)
        {
            return ActionCallPlan.WithArgs(id, new Dictionary<string, ActionArgValue>
            {
                [name0] = ActionArgValue.OfConst(value0, name0),
                [name1] = ActionArgValue.OfConst(value1, name1),
                [name2] = ActionArgValue.OfConst(value2, name2)
            });
        }

        // ========== 调度模式工厂方法 ==========

        /// <summary>
        /// 创建立即执行的动作
        /// </summary>
        public static ActionCallPlan Immediate(this ActionId id)
        {
            var plan = new ActionCallPlan(id);
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                Config.EActionScheduleMode.Immediate, 0, -1, true,
                Config.EActionExecutionPolicy.Immediate);
        }

        /// <summary>
        /// 创建延迟执行的动作
        /// </summary>
        /// <param name="delayMs">延迟时间（毫秒）</param>
        /// <param name="maxExecutions">最大执行次数，-1=无限</param>
        public static ActionCallPlan Delayed(this ActionId id, float delayMs, int maxExecutions = 1)
        {
            var plan = new ActionCallPlan(id);
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                Config.EActionScheduleMode.Delayed, delayMs, maxExecutions, true,
                Config.EActionExecutionPolicy.Immediate);
        }

        /// <summary>
        /// 创建周期执行的动作
        /// </summary>
        /// <param name="intervalMs">周期间隔（毫秒）</param>
        /// <param name="maxExecutions">最大执行次数，-1=无限</param>
        /// <param name="canBeInterrupted">是否可中断</param>
        public static ActionCallPlan Periodic(this ActionId id, float intervalMs, int maxExecutions = -1, bool canBeInterrupted = true)
        {
            var plan = new ActionCallPlan(id);
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                Config.EActionScheduleMode.Periodic, intervalMs, maxExecutions, canBeInterrupted,
                Config.EActionExecutionPolicy.Immediate);
        }

        /// <summary>
        /// 创建持续帧执行的动作（每帧执行直到外部终止）
        /// </summary>
        /// <param name="canBeInterrupted">是否可中断</param>
        public static ActionCallPlan Continuous(this ActionId id, bool canBeInterrupted = true)
        {
            var plan = new ActionCallPlan(id);
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                Config.EActionScheduleMode.Continuous, 0, -1, canBeInterrupted,
                Config.EActionExecutionPolicy.Immediate);
        }

        /// <summary>
        /// 创建带执行策略的动作
        /// </summary>
        public static ActionCallPlan WithExecutionPolicy(this ActionCallPlan plan, Config.EActionExecutionPolicy policy)
        {
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                plan.ScheduleMode, plan.ScheduleParam, plan.MaxExecutions, plan.CanBeInterrupted,
                policy);
        }

        /// <summary>
        /// 创建带调度参数的动作
        /// </summary>
        public static ActionCallPlan WithSchedule(this ActionCallPlan plan, Config.EActionScheduleMode mode, float param = 0, int maxExecutions = -1, bool canBeInterrupted = true)
        {
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                mode, param, maxExecutions, canBeInterrupted,
                plan.ExecutionPolicy);
        }
    }
}
