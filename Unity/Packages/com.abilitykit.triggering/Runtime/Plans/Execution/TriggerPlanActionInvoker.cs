#nullable enable
using System;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Context;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// 使用已绑定的委托执行单个动作，负责参数求值、动作 Scope 及行为 Cue。
    /// 不保存绑定或决定调度；执行树叶子沿用默认元数据，不派发行为 Cue。
    /// </summary>
    internal readonly struct TriggerPlanActionInvoker<TArgs, TCtx>
    {
        private readonly int _triggerId;
        private readonly int _phase;
        private readonly int _priority;
        private readonly ITriggerCue? _cue;

        internal TriggerPlanActionInvoker(int triggerId, int phase, int priority, ITriggerCue? cue)
        {
            _triggerId = triggerId;
            _phase = phase;
            _priority = priority;
            _cue = cue;
        }

        /// <summary>
        /// 按原有顺序报告 Scope 与 Cue；动作索引用于 Cue 和诊断，Scope 索引由调用方提供。
        /// </summary>
        internal void Execute(in TArgs args, in ActionCallPlan call, in ExecCtx<TCtx> ctx, in TriggerPlanActionBinding binding, int index, int scopeIndex)
        {
            var behaviorCue = !call.Cue.IsEmpty ? _cue : null;
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
                    Invoke(in args, in call, in ctx, in binding, index);
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

        private void Invoke(in TArgs args, in ActionCallPlan call, in ExecCtx<TCtx> ctx, in TriggerPlanActionBinding binding, int index)
        {
            var arguments = call.Arguments;
            var resolverArgs = binding.UseNamedArgs ? PlannedTriggerArgumentResolver<TArgs, TCtx>.ResolveNamedArgs(in args, in call, in ctx) : null;
            switch (arguments.Arity)
            {
                case 0:
                    RequireCallback<NamedAction0<TArgs, object, TCtx>>(in call, in ctx, in binding, index)(args, resolverArgs!, ctx);
                    break;
                case 1:
                {
                    var action = RequireCallback<NamedAction1<TArgs, object, TCtx>>(in call, in ctx, in binding, index);
                    if (!binding.UseNamedArgs)
                    {
                        var first = PlannedTriggerArgumentResolver<TArgs, TCtx>.ResolveNumeric(in args, in arguments.Arg0, in ctx);
                        resolverArgs = PlannedTriggerArgumentResolver<TArgs, TCtx>.CreatePositionalArgs(first);
                    }
                    action(args, resolverArgs!, ctx);
                    break;
                }
                case 2:
                {
                    var action = RequireCallback<NamedAction2<TArgs, object, TCtx>>(in call, in ctx, in binding, index);
                    if (!binding.UseNamedArgs)
                    {
                        var first = PlannedTriggerArgumentResolver<TArgs, TCtx>.ResolveNumeric(in args, in arguments.Arg0, in ctx);
                        var second = PlannedTriggerArgumentResolver<TArgs, TCtx>.ResolveNumeric(in args, in arguments.Arg1, in ctx);
                        resolverArgs = PlannedTriggerArgumentResolver<TArgs, TCtx>.CreatePositionalArgs(first, second);
                    }
                    action(args, resolverArgs!, ctx);
                    break;
                }
                default:
                    var mode = binding.UseNamedArgs ? "named action" : "action";
                    throw new InvalidOperationException($"Unsupported {mode} arity during execute. triggerId={_triggerId}, index={index}, id={PlannedTriggerArgumentResolver<TArgs, TCtx>.FormatActionId(in ctx, call.Id)}, arity={arguments.Arity}");
            }
        }

        private TDelegate RequireCallback<TDelegate>(in ActionCallPlan call, in ExecCtx<TCtx> ctx, in TriggerPlanActionBinding binding, int index)
            where TDelegate : Delegate
        {
            return binding.Callback as TDelegate
                ?? throw new InvalidOperationException($"Action slot missing. triggerId={_triggerId}, index={index}, id={PlannedTriggerArgumentResolver<TArgs, TCtx>.FormatActionId(in ctx, call.Id)}, arity={call.Arguments.Arity}");
        }

        private TriggerCueContext BuildBehaviorCueContext(
            in TArgs args,
            in ActionCallPlan call,
            in ExecCtx<TCtx> ctx,
            int index,
            ECueLifecycleStage stage)
        {
            object cueData = null!;
            string cuePayload = null!;
            TriggerRunnerCueDispatcher.ResolveCueData(in args, ECueLevel.Behavior, stage, index, in call.Cue, ref cueData, ref cuePayload);

            return new TriggerCueContext(
                0,
                null!,
                args!,
                _phase,
                _priority,
                0,
                _triggerId,
                nameof(PlannedTrigger<TArgs, TCtx>),
                ETriggerShortCircuitReason.None,
                ctx.Control?.InterruptSourceName!,
                ctx.Control?.InterruptTriggerId ?? 0,
                true,
                ctx.Control!,
                ECueLevel.Behavior,
                stage,
                index,
                in call.Cue,
                cueData,
                cuePayload);
        }
    }
}
