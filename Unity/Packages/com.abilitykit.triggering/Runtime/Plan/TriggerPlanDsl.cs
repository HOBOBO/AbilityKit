using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// TriggerPlan 的流畅 API 扩展
    /// 提供更易读的条件和动作构建方式
    /// </summary>
    public static class TriggerPlanDsl
    {
        /// <summary>
        /// 开始构建一个触发器计划
        /// </summary>
        public static TriggerPlanBuilder<TArgs> Create<TArgs>(int phase = 0, int priority = 0)
        {
            return new TriggerPlanBuilder<TArgs>(phase, priority);
        }
    }

    /// <summary>
    /// 触发器计划构建器
    /// </summary>
    public sealed class TriggerPlanBuilder<TArgs>
    {
        private readonly int _phase;
        private readonly int _priority;
        private PredicateExprPlan _predicate;
        private readonly List<ActionCallPlan> _actions = new List<ActionCallPlan>();

        internal TriggerPlanBuilder(int phase, int priority)
        {
            _phase = phase;
            _priority = priority;
        }

        /// <summary>
        /// 设置无条件触发器
        /// </summary>
        public TriggerPlanBuilder<TArgs> WithNoCondition()
        {
            return this;
        }

        /// <summary>
        /// 设置布尔表达式条件
        /// </summary>
        public TriggerPlanBuilder<TArgs> When(PredicateExprPlan predicate)
        {
            _predicate = predicate;
            return this;
        }

        /// <summary>
        /// 添加一个无参数的动作
        /// </summary>
        public TriggerPlanBuilder<TArgs> Do(ActionId actionId)
        {
            _actions.Add(new ActionCallPlan(actionId));
            return this;
        }

        /// <summary>
        /// 添加一个带一个参数的动作
        /// </summary>
        public TriggerPlanBuilder<TArgs> Do(ActionId actionId, NumericValueRef arg0)
        {
            _actions.Add(new ActionCallPlan(actionId, arg0));
            return this;
        }

        /// <summary>
        /// 添加一个带两个参数的动作
        /// </summary>
        public TriggerPlanBuilder<TArgs> Do(ActionId actionId, NumericValueRef arg0, NumericValueRef arg1)
        {
            _actions.Add(new ActionCallPlan(actionId, arg0, arg1));
            return this;
        }

        /// <summary>
        /// 添加一个带常量参数的动作
        /// </summary>
        public TriggerPlanBuilder<TArgs> DoConst(ActionId actionId, double arg0)
        {
            _actions.Add(new ActionCallPlan(actionId, arg0));
            return this;
        }

        /// <summary>
        /// 添加一个带两个常量参数的动作
        /// </summary>
        public TriggerPlanBuilder<TArgs> DoConst(ActionId actionId, double arg0, double arg1)
        {
            _actions.Add(new ActionCallPlan(actionId, arg0, arg1));
            return this;
        }

        /// <summary>
        /// 添加多个动作
        /// </summary>
        public TriggerPlanBuilder<TArgs> DoAll(params ActionCallPlan[] actions)
        {
            _actions.AddRange(actions);
            return this;
        }

        /// <summary>
        /// 构建 TriggerPlan
        /// </summary>
        public TriggerPlan<TArgs> Build()
        {
            var actions = _actions.Count > 0 ? _actions.ToArray() : Array.Empty<ActionCallPlan>();

            if (_predicate.Nodes != null && _predicate.Nodes.Length > 0)
            {
                return new TriggerPlan<TArgs>(_phase, _priority, _predicate, actions);
            }

            return new TriggerPlan<TArgs>(_phase, _priority, actions);
        }
    }

    /// <summary>
    /// ActionCallPlan 的流畅 API 扩展
    /// </summary>
    public static class ActionCallPlanDsl
    {
        /// <summary>
        /// 创建一个无参数的动作调用
        /// </summary>
        public static ActionCallPlan Call(ActionId actionId)
        {
            return new ActionCallPlan(actionId);
        }

        /// <summary>
        /// 创建一个带一个参数的动作调用
        /// </summary>
        public static ActionCallPlan Call(ActionId actionId, NumericValueRef arg0)
        {
            return new ActionCallPlan(actionId, arg0);
        }

        /// <summary>
        /// 创建一个带两个参数的动作调用
        /// </summary>
        public static ActionCallPlan Call(ActionId actionId, NumericValueRef arg0, NumericValueRef arg1)
        {
            return new ActionCallPlan(actionId, arg0, arg1);
        }

        /// <summary>
        /// 创建一个带常量参数的动作调用
        /// </summary>
        public static ActionCallPlan CallConst(ActionId actionId, double arg0)
        {
            return new ActionCallPlan(actionId, arg0);
        }

        /// <summary>
        /// 创建一个带两个常量参数的动作调用
        /// </summary>
        public static ActionCallPlan CallConst(ActionId actionId, double arg0, double arg1)
        {
            return new ActionCallPlan(actionId, arg0, arg1);
        }
    }
}
