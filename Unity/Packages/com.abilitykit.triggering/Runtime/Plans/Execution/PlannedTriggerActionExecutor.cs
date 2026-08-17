using System;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Runtime.Dispatcher;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// PlannedTrigger 的 Action 执行边界。
    /// 负责 Action 委托缓存、具名/位置参数解析与单 Action 调用，让 PlannedTrigger 聚焦触发器编排。
    /// </summary>
    internal sealed class PlannedTriggerActionExecutor<TArgs, TCtx>
        where TArgs : class
    {
        private readonly TriggerPlan<TArgs> _plan;
        private NamedAction0<TArgs, object, TCtx>[] _actions0;
        private NamedAction1<TArgs, object, TCtx>[] _actions1;
        private NamedAction2<TArgs, object, TCtx>[] _actions2;
        private bool[] _useNamedArgs;
        private bool _resolved;

        public PlannedTriggerActionExecutor(in TriggerPlan<TArgs> plan)
        {
            _plan = plan;
        }

        public void Resolve(in ExecCtx<TCtx> ctx)
        {
            if (_resolved) return;

            InitializeActionBindings();
            PlannedTriggerActionBindingResolver<TArgs, TCtx>.ResolveAll(
                _plan.Actions,
                in ctx,
                _actions0,
                _actions1,
                _actions2,
                _useNamedArgs);
            _resolved = true;
        }

        public void Execute(in TArgs args, in ExecCtx<TCtx> ctx, int index)
        {
            var call = _plan.Actions[index];
            Execute(in args, in call, in ctx, index, index);
        }

        public void ExecuteWithScopeIndex(in TArgs args, in ExecCtx<TCtx> ctx, int index, int scopeIndex)
        {
            var call = _plan.Actions[index];
            Execute(in args, in call, in ctx, index, scopeIndex);
        }

        public Action<object, ITriggerDispatcherContext> CreateActionDelegate(int index, Func<ExecCtx<TCtx>> execCtxAccessor)
        {
            if (execCtxAccessor == null) throw new ArgumentNullException(nameof(execCtxAccessor));
            var call = _plan.Actions[index];

            return (argsObj, _) =>
            {
                var args = (TArgs)argsObj;
                var ctx = execCtxAccessor();
                Execute(in args, in call, in ctx, index, index);
            };
        }

        private void Execute(in TArgs args, in ActionCallPlan call, in ExecCtx<TCtx> ctx, int index, int scopeIndex)
        {
            var behaviorCue = !call.Cue.IsEmpty && _plan.Cue != null ? _plan.Cue : null;
            ExecCtxContextAccessorExtensions.TryGetService<TCtx, ITriggerActionExecutionScopeObserver>(in ctx, out var actionScopeObserver);
            var succeeded = false;
            actionScopeObserver?.EnterActionExecution(scopeIndex, call.Id.Value);
            try
            {
                if (behaviorCue != null)
                {
                    var beforeContext = BuildBehaviorCueContext(in args, in call, in ctx, index, ECueLifecycleStage.BeforeAction);
                    behaviorCue.OnBeforeAction(in beforeContext, index);
                }

                try
                {
                    if (_useNamedArgs[index])
                    {
                        ExecuteNamed(in args, in call, in ctx, index);
                    }
                    else
                    {
                        ExecutePositional(in args, in call, in ctx, index);
                    }

                }
                catch
                {
                    if (behaviorCue != null)
                    {
                        var interruptedContext = BuildBehaviorCueContext(in args, in call, in ctx, index, ECueLifecycleStage.Interrupted);
                        behaviorCue.OnInterrupted(in interruptedContext);
                    }

                    throw;
                }

                if (behaviorCue != null)
                {
                    var executedContext = BuildBehaviorCueContext(in args, in call, in ctx, index, ECueLifecycleStage.Executed);
                    behaviorCue.OnExecuted(in executedContext);
                }

                succeeded = true;
            }
            finally
            {
                actionScopeObserver?.ExitActionExecution(scopeIndex, call.Id.Value, succeeded);
            }
        }

        private void ExecuteNamed(in TArgs args, in ActionCallPlan call, in ExecCtx<TCtx> ctx, int index)
        {
            var arguments = call.Arguments;
            var rawArgs = PlannedTriggerArgumentResolver<TArgs, TCtx>.ResolveNamedArgs(in args, in call, in ctx);
            switch (arguments.Arity)
            {
                case 0:
                    if (_actions0[index] == null) ThrowActionSlotMissing(in call, in ctx, index);
                    _actions0[index].Invoke(args, rawArgs, ctx);
                    break;
                case 1:
                    if (_actions1[index] == null) ThrowActionSlotMissing(in call, in ctx, index);
                    _actions1[index].Invoke(args, rawArgs, ctx);
                    break;
                case 2:
                    if (_actions2[index] == null) ThrowActionSlotMissing(in call, in ctx, index);
                    _actions2[index].Invoke(args, rawArgs, ctx);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported named action arity during execute. triggerId={_plan.TriggerId}, index={index}, id={FormatActionId(in ctx, call.Id)}, arity={arguments.Arity}");
            }
        }

        private void ExecutePositional(in TArgs args, in ActionCallPlan call, in ExecCtx<TCtx> ctx, int index)
        {
            var arguments = call.Arguments;
            switch (arguments.Arity)
            {
                case 0:
                    if (_actions0[index] == null) ThrowActionSlotMissing(in call, in ctx, index);
                    _actions0[index].Invoke(args, null, ctx);
                    break;
                case 1:
                    {
                        if (_actions1[index] == null) ThrowActionSlotMissing(in call, in ctx, index);
                        var v0 = PlannedTriggerArgumentResolver<TArgs, TCtx>.ResolveNumeric(in args, in arguments.Arg0, in ctx);
                        var argsDict = PlannedTriggerArgumentResolver<TArgs, TCtx>.CreatePositionalArgs(v0);
                        _actions1[index].Invoke(args, argsDict, ctx);
                        break;
                    }
                case 2:
                    {
                        if (_actions2[index] == null) ThrowActionSlotMissing(in call, in ctx, index);
                        var v0 = PlannedTriggerArgumentResolver<TArgs, TCtx>.ResolveNumeric(in args, in arguments.Arg0, in ctx);
                        var v1 = PlannedTriggerArgumentResolver<TArgs, TCtx>.ResolveNumeric(in args, in arguments.Arg1, in ctx);
                        var argsDict = PlannedTriggerArgumentResolver<TArgs, TCtx>.CreatePositionalArgs(v0, v1);
                        _actions2[index].Invoke(args, argsDict, ctx);
                        break;
                    }
                default:
                    throw new InvalidOperationException($"Unsupported action arity during execute. triggerId={_plan.TriggerId}, index={index}, id={FormatActionId(in ctx, call.Id)}, arity={arguments.Arity}");
            }
        }

        private void InitializeActionBindings()
        {
            var len = _plan.Actions?.Length ?? 0;
            _actions0 = len > 0 ? new NamedAction0<TArgs, object, TCtx>[len] : null;
            _actions1 = len > 0 ? new NamedAction1<TArgs, object, TCtx>[len] : null;
            _actions2 = len > 0 ? new NamedAction2<TArgs, object, TCtx>[len] : null;
            _useNamedArgs = len > 0 ? new bool[len] : null;
        }

        private TriggerCueContext BuildBehaviorCueContext(
            in TArgs args,
            in ActionCallPlan call,
            in ExecCtx<TCtx> ctx,
            int index,
            ECueLifecycleStage stage)
        {
            object cueData = null;
            string cuePayload = null;
            TriggerRunnerCueDispatcher.ResolveCueData(in args, ECueLevel.Behavior, stage, index, in call.Cue, ref cueData, ref cuePayload);

            return new TriggerCueContext(
                0,
                null,
                args,
                _plan.Phase,
                _plan.Priority,
                0,
                _plan.TriggerId,
                nameof(PlannedTrigger<TArgs, TCtx>),
                ETriggerShortCircuitReason.None,
                ctx.Control?.InterruptSourceName,
                ctx.Control?.InterruptTriggerId ?? 0,
                true,
                ctx.Control,
                ECueLevel.Behavior,
                stage,
                index,
                in call.Cue,
                cueData,
                cuePayload);
        }

        private void ThrowActionSlotMissing(in ActionCallPlan call, in ExecCtx<TCtx> ctx, int index)
        {
            throw new InvalidOperationException($"Action slot missing. triggerId={_plan.TriggerId}, index={index}, id={FormatActionId(in ctx, call.Id)}, arity={call.Arguments.Arity}");
        }

        private static string FormatActionId(in ExecCtx<TCtx> ctx, ActionId id)
        {
            return PlannedTriggerArgumentResolver<TArgs, TCtx>.FormatActionId(in ctx, id);
        }
    }
}
