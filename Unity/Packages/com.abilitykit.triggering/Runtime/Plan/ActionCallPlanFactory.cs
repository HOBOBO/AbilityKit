using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// ActionCallPlan 工厂类
    /// 负责创建 ActionCallPlan 实例，将构造逻辑从数据结构中分离
    /// </summary>
    public static class ActionCallPlanFactory
    {
        // ========== 基础创建方法 ==========

        /// <summary>
        /// 创建无参数的动作调用（默认 Immediate）
        /// </summary>
        public static ActionCallPlan Create(ActionId id)
        {
            return new ActionCallPlan(id);
        }

        /// <summary>
        /// 创建带参数的动作调用（默认 Immediate）
        /// </summary>
        public static ActionCallPlan Create(ActionId id, NumericValueRef arg0, NumericValueRef arg1 = default, NumericValueRef arg2 = default)
        {
            return new ActionCallPlan(id, arg0, arg1, arg2);
        }

        /// <summary>
        /// 创建带常量参数的动作调用（默认 Immediate）
        /// </summary>
        public static ActionCallPlan Create(ActionId id, params double[] constArgs)
        {
            return new ActionCallPlan(id, constArgs);
        }

        /// <summary>
        /// 创建带有具名参数的 ActionCallPlan
        /// </summary>
        public static ActionCallPlan CreateWithArgs(ActionId id, Dictionary<string, ActionArgValue> args)
        {
            return ActionCallPlan.WithArgs(id, args);
        }

        // ========== 扩展方法版本（更流畅的 API）==========

        /// <summary>
        /// 创建无参数的动作调用
        /// </summary>
        public static ActionCallPlan Call(ActionId id)
        {
            return new ActionCallPlan(id);
        }

        /// <summary>
        /// 创建带一个参数的动作调用
        /// </summary>
        public static ActionCallPlan Call(this ActionId id, NumericValueRef arg0)
        {
            return new ActionCallPlan(id, arg0);
        }

        /// <summary>
        /// 创建带两个参数的动作调用
        /// </summary>
        public static ActionCallPlan Call(this ActionId id, NumericValueRef arg0, NumericValueRef arg1)
        {
            return new ActionCallPlan(id, arg0, arg1);
        }

        /// <summary>
        /// 创建带有具名参数的动作调用
        /// </summary>
        public static ActionCallPlan CallArgs(this ActionId id, Dictionary<string, ActionArgValue> args)
        {
            return ActionCallPlan.WithArgs(id, args);
        }

        /// <summary>
        /// 创建带有两个具名参数的动作调用
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
        /// 创建带有三个具名参数的动作调用
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

        // ========== 调度模式工厂方法 ==========

        /// <summary>
        /// 创建立即执行的动作
        /// </summary>
        public static ActionCallPlan Immediate(ActionId id)
        {
            var plan = new ActionCallPlan(id);
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                EActionScheduleMode.Immediate, 0, -1, true,
                EActionExecutionPolicy.Immediate);
        }

        /// <summary>
        /// 创建延迟执行的动作
        /// </summary>
        /// <param name="delayMs">延迟时间（毫秒）</param>
        /// <param name="maxExecutions">最大执行次数，-1=无限</param>
        public static ActionCallPlan Delayed(ActionId id, float delayMs, int maxExecutions = 1)
        {
            var plan = new ActionCallPlan(id);
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                EActionScheduleMode.Delayed, delayMs, maxExecutions, true,
                EActionExecutionPolicy.Immediate);
        }

        /// <summary>
        /// 创建周期执行的动作
        /// </summary>
        /// <param name="intervalMs">周期间隔（毫秒）</param>
        /// <param name="maxExecutions">最大执行次数，-1=无限</param>
        /// <param name="canBeInterrupted">是否可中断</param>
        public static ActionCallPlan Periodic(ActionId id, float intervalMs, int maxExecutions = -1, bool canBeInterrupted = true)
        {
            var plan = new ActionCallPlan(id);
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                EActionScheduleMode.Periodic, intervalMs, maxExecutions, canBeInterrupted,
                EActionExecutionPolicy.Immediate);
        }

        /// <summary>
        /// 创建持续帧执行的动作（每帧执行直到外部终止）
        /// </summary>
        /// <param name="canBeInterrupted">是否可中断</param>
        public static ActionCallPlan Continuous(ActionId id, bool canBeInterrupted = true)
        {
            var plan = new ActionCallPlan(id);
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                EActionScheduleMode.Continuous, 0, -1, canBeInterrupted,
                EActionExecutionPolicy.Immediate);
        }

        // ========== 修改器工厂方法 ==========

        /// <summary>
        /// 创建带执行策略的动作
        /// </summary>
        public static ActionCallPlan WithExecutionPolicy(ActionCallPlan plan, EActionExecutionPolicy policy)
        {
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                plan.ScheduleMode, plan.ScheduleParam, plan.MaxExecutions, plan.CanBeInterrupted,
                policy);
        }

        /// <summary>
        /// 创建带调度参数的动作
        /// </summary>
        public static ActionCallPlan WithSchedule(ActionCallPlan plan, EActionScheduleMode mode, float param = 0, int maxExecutions = -1, bool canBeInterrupted = true)
        {
            return new ActionCallPlan(
                plan.Id, plan.Arity, plan.Arg0, plan.Arg1, plan.Args,
                mode, param, maxExecutions, canBeInterrupted,
                plan.ExecutionPolicy);
        }
    }
}
