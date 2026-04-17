using System;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// TriggerPlan 工厂类
    /// 负责创建 TriggerPlan 实例，将构造逻辑从数据结构中分离
    /// </summary>
    public static class TriggerPlanFactory
    {
        /// <summary>
        /// 创建无条件触发器
        /// </summary>
        public static TriggerPlan<TArgs> Create<TArgs>(
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
        public static TriggerPlan<TArgs> When<TArgs>(
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
        public static TriggerPlan<TArgs> When<TArgs>(
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
        public static TriggerPlan<TArgs> WhenExpr<TArgs>(
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
        public static TriggerPlan<TArgs> AddActions<TArgs>(this TriggerPlan<TArgs> plan, params ActionCallPlan[] actions)
        {
            var newActions = new ActionCallPlan[(plan.Actions?.Length ?? 0) + actions.Length];
            if (plan.Actions?.Length > 0)
                Array.Copy(plan.Actions, newActions, plan.Actions.Length);
            Array.Copy(actions, 0, newActions, plan.Actions?.Length ?? 0, actions.Length);
            return new TriggerPlan<TArgs>(
                plan.Phase, plan.Priority, plan.TriggerId, plan.InterruptPriority,
                plan.PredicateKind, plan.HasPredicate, plan.PredicateId,
                plan.PredicateArity, plan.PredicateArg0, plan.PredicateArg1,
                plan.PredicateExpr, newActions, plan.Cue, in plan.Schedule);
        }
    }
}
