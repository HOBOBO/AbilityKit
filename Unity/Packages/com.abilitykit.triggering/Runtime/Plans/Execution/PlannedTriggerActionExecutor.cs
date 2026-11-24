using System;
using AbilityKit.Triggering.Runtime.Dispatcher;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// 管理扁平计划的动作绑定，供立即执行和调度回调按原计划索引复用。
    /// </summary>
    /// <remarks>
    /// 首次成功绑定后不跟踪注册表代次，也不跟踪动作描述的原地变更；调用方负责装配生命周期。
    /// </remarks>
    internal sealed class PlannedTriggerActionExecutor<TArgs, TCtx>
    {
        private readonly TriggerPlan<TArgs> _plan;
        private readonly TriggerPlanActionInvoker<TArgs, TCtx> _invoker;
        private TriggerPlanActionBinding[] _bindings = Array.Empty<TriggerPlanActionBinding>();
        private bool _resolved;

        internal PlannedTriggerActionExecutor(in TriggerPlan<TArgs> plan)
        {
            _plan = plan;
            _invoker = new TriggerPlanActionInvoker<TArgs, TCtx>(plan.TriggerId, plan.Phase, plan.Priority, plan.Cue);
        }

        /// <summary>
        /// 首次执行前按计划顺序绑定全部动作；任一绑定失败时不发布缓存。
        /// </summary>
        internal void Resolve(in ExecCtx<TCtx> ctx)
        {
            if (_resolved) return;

            var actions = _plan.Actions;
            if (actions != null && actions.Length > 0)
            {
                var bindings = new TriggerPlanActionBinding[actions.Length];
                for (int index = 0; index < actions.Length; index++)
                {
                    bindings[index] = PlannedTriggerActionBindingResolver<TArgs, TCtx>.Resolve(in actions[index], in ctx);
                }
                _bindings = bindings;
            }
            _resolved = true;
        }

        /// <summary>
        /// 使用给定的上下文调用已绑定的动作；计划索引同时用于行为 Cue 和动作 Scope。
        /// </summary>
        internal void Execute(in TArgs args, in ExecCtx<TCtx> ctx, int index)
        {
            var call = _plan.Actions[index];
            _invoker.Execute(in args, in call, in ctx, in _bindings[index], index, index);
        }

        /// <summary>
        /// 固定本次注册的动作描述；回调执行时仍通过调用方提供的读取器获取上下文。
        /// </summary>
        internal Action<object, ITriggerDispatcherContext> CreateActionDelegate(int index, Func<ExecCtx<TCtx>> execCtxAccessor)
        {
            if (execCtxAccessor == null) throw new ArgumentNullException(nameof(execCtxAccessor));
            var call = _plan.Actions[index];

            return (argsObj, _) =>
            {
                var args = (TArgs)argsObj;
                var ctx = execCtxAccessor();
                _invoker.Execute(in args, in call, in ctx, in _bindings[index], index, index);
            };
        }
    }
}
