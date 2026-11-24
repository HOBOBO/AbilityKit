using System;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// 描述触发器的条件、动作及执行策略；只读结构不保证引用成员深度不可变。
    /// </summary>
    /// <remarks>
    /// 计划副本可共享数组及参数对象。交给运行时后，调用方须将这些数据视为只读，
    /// 不在已有注册或调度任务仍使用时原地修改；变更应创建新计划并重新装配。
    /// </remarks>
    public readonly struct TriggerPlan<TArgs>
    {
        public readonly int Phase;
        public readonly int Priority;

        /// <summary>
        /// 触发器 ID
        /// </summary>
        public readonly int TriggerId;

        /// <summary>
        /// 自动优先级中断阈值。正值仅在实际执行路径应用此配置时拦截更低优先级的触发器。
        /// 执行根正常返回不代表业务成功；即时扁平路径不应用此配置。
        /// </summary>
        public readonly int InterruptPriority;

        public readonly EPredicateKind PredicateKind;
        public readonly bool HasPredicate;
        public readonly FunctionId PredicateId;

        public readonly byte PredicateArity;
        public readonly NumericValueRef PredicateArg0;
        public readonly NumericValueRef PredicateArg1;

        public readonly PredicateExprPlan PredicateExpr;

        /// <summary>
        /// 按执行顺序存放动作描述；数组可与调用方或其他计划共享，装配后不得原地修改。
        /// </summary>
        public readonly ActionCallPlan[] Actions;

        /// <summary>
        /// Cue 回调处理器。
        /// </summary>
        public readonly ITriggerCue Cue;

        /// <summary>
        /// 调度配置（持续行为相关）
        /// </summary>
        public readonly ScheduleModePlan Schedule;

        public readonly TriggerExecutionControlPlan ExecutionControl;

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
            in ScheduleModePlan schedule = default,
            in TriggerExecutionControlPlan executionControl = default)
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
            ExecutionControl = executionControl;
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
            in ScheduleModePlan schedule = default,
            in TriggerExecutionControlPlan executionControl = default)
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
            ExecutionControl = executionControl;
        }

        /// <summary>
        /// 表达式条件触发器构造器
        /// </summary>
        public TriggerPlan(
            int phase,
            int priority,
            int triggerId,
            PredicateExprPlan predicateExpr,
            ActionCallPlan[] actions = null,
            int interruptPriority = 0,
            ITriggerCue cue = null,
            in ScheduleModePlan schedule = default,
            in TriggerExecutionControlPlan executionControl = default)
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
            ExecutionControl = executionControl;
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
                PredicateArity, PredicateArg0, PredicateArg1, PredicateExpr, newActions, Cue, in Schedule, in ExecutionControl);
        }

        /// <summary>
        /// 返回另一事件参数类型的计划视图；复用动作数组及其他引用数据，不做深拷贝。
        /// </summary>
        public TriggerPlan<TNextArgs> AsArgs<TNextArgs>()
        {
            return new TriggerPlan<TNextArgs>(Phase, Priority, TriggerId, InterruptPriority, PredicateKind, HasPredicate, PredicateId,
                PredicateArity, PredicateArg0, PredicateArg1, PredicateExpr, Actions, Cue, in Schedule, in ExecutionControl);
        }

        internal TriggerPlan(
            int phase, int priority, int triggerId, int interruptPriority,
            EPredicateKind predicateKind, bool hasPredicate, FunctionId predicateId,
            byte predicateArity, NumericValueRef predicateArg0, NumericValueRef predicateArg1,
            PredicateExprPlan predicateExpr,
            ActionCallPlan[] actions, ITriggerCue cue, in ScheduleModePlan schedule, in TriggerExecutionControlPlan executionControl = default)
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
            ExecutionControl = executionControl;
        }

        private TriggerPlan(
            int phase, int priority, int interruptPriority,
            EPredicateKind predicateKind, bool hasPredicate, FunctionId predicateId,
            byte predicateArity, NumericValueRef predicateArg0, NumericValueRef predicateArg1,
            PredicateExprPlan predicateExpr,
            ActionCallPlan[] actions, ITriggerCue cue, in ScheduleModePlan schedule, in TriggerExecutionControlPlan executionControl = default)
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
            ExecutionControl = executionControl;
        }
    }
}
