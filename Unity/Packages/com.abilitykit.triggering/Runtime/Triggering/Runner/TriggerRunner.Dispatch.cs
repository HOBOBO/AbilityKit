using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Timing;

namespace AbilityKit.Triggering.Runtime
{
    /// <remarks>
    /// 派发部分维护一次事件的评估、执行和短路顺序。
    /// </remarks>
    public sealed partial class TriggerRunner<TCtx>
    {
        private void Dispatch<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control, Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>> list)
        {
            if (!list.TryGetValue(key, out var triggers) || triggers.Count == 0) return;

            _lifecycle.OnEventDispatching(key, in args);

            control = PrepareDispatchControl(control);
            var execCtx = CreateExecCtx(control);
            _currentTraceScope = _tracer.BeginTrace(key, in args);

            int executedCount = 0;
            int shortCircuitedCount = 0;

            try
            {
                for (int i = 0; i < triggers.Count; i++)
                {
                    control.ClearActionRejection();
                    var entry = triggers[i];

                    if (TryHandlePriorityBlock(key, in args, in entry, control, in execCtx))
                    {
                        shortCircuitedCount++;
                        continue;
                    }

                    var evaluation = EvaluateEntry(key, in args, in entry, control, in execCtx);
                    if (evaluation == DispatchEvaluationResult.FailedByException)
                    {
                        break;
                    }

                    if (evaluation == DispatchEvaluationResult.ConditionFailed)
                    {
                        shortCircuitedCount++;
                        if (HandleConditionRejected(key, in args, in entry, control, in execCtx))
                        {
                            break;
                        }

                        continue;
                    }

                    if (ExecuteEntry(key, in args, in entry, control, in execCtx, out var wasInterrupted))
                    {
                        executedCount++;
                    }

                    if (wasInterrupted)
                    {
                        shortCircuitedCount++;
                        break;
                    }
                }
            }
            finally
            {
                _tracer.EndTrace(_currentTraceScope);
                _currentTraceScope = default;
            }

            _lifecycle.OnEventDispatched(key, in args, executedCount, shortCircuitedCount);
        }

        private static ExecutionControl PrepareDispatchControl(ExecutionControl control)
        {
            if (control == null) control = new ExecutionControl();
            control.Reset();
            return control;
        }

        private ExecCtx<TCtx> CreateExecCtx(ExecutionControl control)
        {
            var ctx = _contextSource != null ? _contextSource.GetContext() : default;
            return _runtimeServices.CreateExecCtx(ctx, control, _actionSchedulerManager);
        }

        /// <summary>
        /// 处理因更高优先级或失败条件传播导致的触发器跳过。
        /// </summary>
        private bool TryHandlePriorityBlock<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            if (!control.ShouldBlock(entry.Phase, entry.Priority))
            {
                return false;
            }

            var reason = control.InterruptConditionPassed
                ? ShortCircuitReason.InterruptedByHigherPriority
                : ShortCircuitReason.InterruptedByFailedCondition;

            NotifyShortCircuit(
                key,
                in args,
                in entry,
                control,
                in execCtx,
                reason,
                control.InterruptSourceName,
                control.InterruptTriggerId,
                control.InterruptConditionPassed,
                ShortCircuitCueKind.Skipped);
            return true;
        }

        /// <summary>
        /// 执行触发条件评估，并统一派发生命周期、观察者与 Cue 回调。
        /// </summary>
        private DispatchEvaluationResult EvaluateEntry<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            _lifecycle.OnBeforeEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order);
            _observer.OnEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, false, in execCtx);

            bool ok;
            var startTicks = MonotonicTime.GetTimestamp();
            try
            {
                ok = entry.Trigger.Evaluate(in args, in execCtx);
            }
            catch (Exception ex)
            {
                RecordTrace(key, in entry, TriggerRecordKind.Evaluated, false, null, MonotonicTime.GetTimestamp() - startTicks);
                NotifyEvaluationException(key, in args, in entry, control, in execCtx, ex);
                return DispatchEvaluationResult.FailedByException;
            }

            var elapsedTicks = MonotonicTime.GetTimestamp() - startTicks;
            _lifecycle.OnAfterEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, ok);
            _observer.OnEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, ok, in execCtx);
            RecordTrace(key, in entry, TriggerRecordKind.Evaluated, ok, null, elapsedTicks);

            if (!ok)
            {
                NotifyConditionFailed(key, in args, in entry, control, in execCtx);
                return DispatchEvaluationResult.ConditionFailed;
            }

            NotifyConditionPassed(key, in args, in entry, control, in execCtx);
            return DispatchEvaluationResult.ConditionPassed;
        }

        /// <summary>
        /// 处理条件失败后的中断策略；返回 true 表示当前事件派发应立即结束。
        /// </summary>
        private bool HandleConditionRejected<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            if (_interruptPolicy != EInterruptPolicy.Strict)
            {
                return false;
            }

            NotifyShortCircuit(
                key,
                in args,
                in entry,
                control,
                in execCtx,
                ShortCircuitReason.ConditionFailed,
                null,
                0,
                false,
                ShortCircuitCueKind.Interrupted);
            return true;
        }

        /// <summary>
        /// 执行触发器动作；返回 true 表示动作完整执行并触发完成 Cue。
        /// </summary>
        private bool ExecuteEntry<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx,
            out bool wasInterrupted)
        {
            wasInterrupted = false;
            var cue = entry.Trigger.Cue;

            _lifecycle.OnBeforeExecute(key, in args, entry.Phase, entry.Priority, entry.Order);
            _lifecycle.OnActionExecuting(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1);
            _observer.OnActionExecuting(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, in execCtx);
            if (!ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                var executeCtx = TriggerRunnerCueDispatcher.BuildCueContext(
                    key,
                    in args,
                    entry.Phase,
                    entry.Priority,
                    entry.Order,
                    entry.Trigger,
                    ShortCircuitReason.None,
                    null,
                    0,
                    true,
                    control,
                    Config.ECueLevel.Trigger,
                    Config.ECueLifecycleStage.BeforeAction,
                    0);
                cue.OnBeforeAction(in executeCtx, 0);
            }

            var actionExecuted = TryExecuteTrigger(key, in args, in entry, in execCtx);
            if (TryHandleHardStop(key, in args, in entry, control, in execCtx))
            {
                wasInterrupted = true;
                _lifecycle.OnActionExecuted(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, true);
                _observer.OnActionExecuted(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, true, in execCtx);
                return false;
            }

            _lifecycle.OnAfterExecute(key, in args, entry.Phase, entry.Priority, entry.Order);
            _observer.OnExecute(key, in args, entry.Phase, entry.Priority, entry.Order, in execCtx);
            if (actionExecuted)
            {
                _lifecycle.OnActionExecuted(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, false);
                _observer.OnActionExecuted(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, false, in execCtx);
            }

            if (!actionExecuted)
            {
                return false;
            }

            if (!ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                var executedCtx = TriggerRunnerCueDispatcher.BuildCueContext(
                    key,
                    in args,
                    entry.Phase,
                    entry.Priority,
                    entry.Order,
                    entry.Trigger,
                    ShortCircuitReason.None,
                    null,
                    0,
                    true,
                    control,
                    Config.ECueLevel.Trigger,
                    Config.ECueLifecycleStage.Executed);
                cue.OnExecuted(in executedCtx);
            }
            return true;
        }

        private bool TryExecuteTrigger<TArgs>(EventKey<TArgs> key, in TArgs args, in TriggerRunnerEntry<TArgs, TCtx> entry, in ExecCtx<TCtx> execCtx)
        {
            var startTicks = MonotonicTime.GetTimestamp();
            try
            {
                entry.Trigger.Execute(in args, in execCtx);
                RecordTrace(key, in entry, TriggerRecordKind.Executed, null, null, MonotonicTime.GetTimestamp() - startTicks);
                return true;
            }
            catch (Exception ex)
            {
                RecordTrace(key, in entry, TriggerRecordKind.Executed, null, null, MonotonicTime.GetTimestamp() - startTicks);
                NotifyActionFailed(key, in args, in entry, in execCtx, entry.Trigger.GetType().Name, 0, 1, ex.Message);
                return false;
            }
        }

        private bool TryHandleHardStop<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            if (!control.IsHardStopped)
            {
                return false;
            }

            var reason = control.Cancel ? ShortCircuitReason.Cancel : ShortCircuitReason.StopPropagation;
            NotifyShortCircuit(
                key,
                in args,
                in entry,
                control,
                in execCtx,
                reason,
                control.InterruptSourceName ?? entry.Trigger.GetType().Name,
                control.InterruptTriggerId,
                true,
                ShortCircuitCueKind.Interrupted);
            return true;
        }

        /// <summary>
        /// 区分普通条件拒绝与评估异常，派发循环对两者采用不同的退出规则。
        /// </summary>
        private enum DispatchEvaluationResult
        {
            ConditionPassed,
            ConditionFailed,
            FailedByException
        }
    }
}
