using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;
using static AbilityKit.Triggering.Runtime.Plan.ActionCallPlanFactory;

namespace AbilityKit.Triggering.Runtime.Dsl
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
        private int _triggerId;
        private int _interruptPriority;
        private ITriggerCue _cue;
        private ScheduleModePlan _schedule;

        internal TriggerPlanBuilder(int phase, int priority)
        {
            _phase = phase;
            _priority = priority;
        }

        /// <summary>
        /// 设置触发器标识（用于打断溯源）
        /// </summary>
        public TriggerPlanBuilder<TArgs> WithTriggerId(int id)
        {
            _triggerId = id;
            return this;
        }

        /// <summary>
        /// 设置执行成功后打断更低优先级的触发器（自身优先级作为阈值）
        /// 等价于 Execute 成功后调用 control.StopBelowPriority(_priority, ...)
        /// </summary>
        public TriggerPlanBuilder<TArgs> WithPriorityInterrupt()
        {
            _interruptPriority = _priority;
            return this;
        }

        /// <summary>
        /// 设置打断优先级阈值。Execute 成功后以此值为阈值打断所有 Priority 更低的触发器。
        /// </summary>
        /// <param name="threshold">打断阈值（通常设为自身 Priority）</param>
        public TriggerPlanBuilder<TArgs> WithInterruptThreshold(int threshold)
        {
            _interruptPriority = threshold;
            return this;
        }

        /// <summary>
        /// 设置表现层 Cue（VFX / SFX / UI 反馈）
        /// Cue 与触发器生命周期绑定，在条件通过/失败/执行/打断/跳过时触发对应回调
        /// </summary>
        /// <param name="cue">表现层实现，不传则使用 NullTriggerCue（无任何表现）</param>
        public TriggerPlanBuilder<TArgs> WithCue(ITriggerCue cue)
        {
            _cue = cue;
            return this;
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
        /// 设置持续行为调度
        /// 每帧驱动 Action 执行直到外部终止
        /// </summary>
        /// <param name="intervalMs">执行间隔（毫秒），0=每帧</param>
        /// <param name="maxExecutions">最大执行次数，-1=无限</param>
        /// <param name="canBeInterrupted">是否可中断</param>
        public TriggerPlanBuilder<TArgs> WithContinuous(float intervalMs = 0, int maxExecutions = -1, bool canBeInterrupted = true)
        {
            _schedule = ScheduleModePlan.Continuous(intervalMs, maxExecutions, canBeInterrupted);
            return this;
        }

        /// <summary>
        /// 设置周期调度
        /// </summary>
        /// <param name="intervalMs">执行间隔（毫秒）</param>
        /// <param name="maxExecutions">最大执行次数，-1=无限</param>
        public TriggerPlanBuilder<TArgs> WithPeriodic(float intervalMs, int maxExecutions = -1)
        {
            _schedule = ScheduleModePlan.Periodic(intervalMs, maxExecutions);
            return this;
        }

        /// <summary>
        /// 设置自定义调度模式
        /// </summary>
        public TriggerPlanBuilder<TArgs> WithSchedule(in ScheduleModePlan schedule)
        {
            _schedule = schedule;
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
        /// 添加一个带具名参数的动作
        /// </summary>
        public TriggerPlanBuilder<TArgs> DoArgs(ActionId actionId, Dictionary<string, ActionArgValue> args)
        {
            _actions.Add(ActionCallPlan.WithArgs(actionId, args));
            return this;
        }

        /// <summary>
        /// 添加一个带两个具名参数的动作（便捷重载）
        /// </summary>
        public TriggerPlanBuilder<TArgs> DoArgs(ActionId actionId, string name0, double value0, string name1, double value1)
        {
            _actions.Add(ActionCallPlan.WithArgs(actionId, new Dictionary<string, ActionArgValue>
            {
                [name0] = ActionArgValue.OfConst(value0, name0),
                [name1] = ActionArgValue.OfConst(value1, name1)
            }));
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
                // 使用表达式条件触发器构造函数
                return new TriggerPlan<TArgs>(
                    phase: _phase,
                    priority: _priority,
                    triggerId: _triggerId,
                    predicateExpr: _predicate,
                    actions: actions,
                    interruptPriority: _interruptPriority,
                    cue: _cue,
                    schedule: _schedule);
            }

            // 使用无条件触发器构造函数
            return new TriggerPlan<TArgs>(
                phase: _phase,
                priority: _priority,
                triggerId: _triggerId,
                actions: actions,
                interruptPriority: _interruptPriority,
                cue: _cue,
                schedule: _schedule);
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

        /// <summary>
        /// 创建一个带具名参数的动作调用
        /// </summary>
        public static ActionCallPlan CallArgs(ActionId actionId, Dictionary<string, ActionArgValue> args)
        {
            return ActionCallPlan.WithArgs(actionId, args);
        }

        /// <summary>
        /// 创建一个带两个具名参数的动作调用（便捷重载）
        /// </summary>
        public static ActionCallPlan CallArgs(ActionId actionId, string name0, double value0, string name1, double value1)
        {
            return ActionCallPlan.WithArgs(actionId, new Dictionary<string, ActionArgValue>
            {
                [name0] = ActionArgValue.OfConst(value0, name0),
                [name1] = ActionArgValue.OfConst(value1, name1)
            });
        }

        /// <summary>
        /// 创建一个带三个具名参数的动作调用（便捷重载）
        /// </summary>
        public static ActionCallPlan CallArgs(ActionId actionId, string name0, double value0, string name1, double value1, string name2, double value2)
        {
            return ActionCallPlan.WithArgs(actionId, new Dictionary<string, ActionArgValue>
            {
                [name0] = ActionArgValue.OfConst(value0, name0),
                [name1] = ActionArgValue.OfConst(value1, name1),
                [name2] = ActionArgValue.OfConst(value2, name2)
            });
        }
    }
}
