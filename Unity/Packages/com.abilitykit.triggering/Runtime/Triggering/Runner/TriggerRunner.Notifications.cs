using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Timing;

namespace AbilityKit.Triggering.Runtime
{
    /// <remarks>
    /// 通知部分负责条件结果、短路与失败的对外反馈，以及 Trace 记录，不决定派发策略。
    /// </remarks>
    public sealed partial class TriggerRunner<TCtx>
    {
        private TraceScope _currentTraceScope;

        private void NotifyEvaluationException<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx,
            Exception ex)
        {
            Log.Error($"[TriggerRunner] Trigger evaluation exception. argsType={typeof(TArgs).Name} triggerId={GetDebugTriggerId(entry.Trigger)} triggerType={entry.Trigger.GetType().Name} phase={entry.Phase} priority={entry.Priority} order={entry.Order} exception={ex}");

            _lifecycle.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
            _observer.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);
            _lifecycle.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, "Evaluate", 0, 0, ex.Message);
            _observer.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, "Evaluate", 0, 0, ex.Message, in execCtx);

            var cue = entry.Trigger.Cue;
            if (ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                return;
            }

            var failCtx = TriggerRunnerCueDispatcher.BuildCueContext(
                key,
                in args,
                entry.Phase,
                entry.Priority,
                entry.Order,
                entry.Trigger,
                ShortCircuitReason.ConditionFailed,
                null,
                0,
                false,
                control,
                Config.ECueLevel.Trigger,
                Config.ECueLifecycleStage.ConditionFailed);
            cue.OnConditionFailed(in failCtx);
        }

        private void NotifyConditionPassed<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            _lifecycle.OnConditionPassed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
            _observer.OnConditionPassed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);

            var cue = entry.Trigger.Cue;
            if (ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                return;
            }

            var passCtx = TriggerRunnerCueDispatcher.BuildCueContext(
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
                Config.ECueLifecycleStage.ConditionPassed);
            cue.OnConditionPassed(in passCtx);
        }

        private void NotifyConditionFailed<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            _lifecycle.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
            _observer.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);

            var cue = entry.Trigger.Cue;
            if (ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                return;
            }

            var failCtx = TriggerRunnerCueDispatcher.BuildCueContext(
                key,
                in args,
                entry.Phase,
                entry.Priority,
                entry.Order,
                entry.Trigger,
                ShortCircuitReason.ConditionFailed,
                null,
                0,
                false,
                control,
                Config.ECueLevel.Trigger,
                Config.ECueLifecycleStage.ConditionFailed);
            cue.OnConditionFailed(in failCtx);
        }

        private void NotifyShortCircuit<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx,
            ShortCircuitReason reason,
            string interruptSourceName,
            int interruptTriggerId,
            bool interruptConditionPassed,
            ShortCircuitCueKind cueKind)
        {
            _lifecycle.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, reason);
            _observer.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, TriggerRunnerCueDispatcher.MapReason(reason), in execCtx);
            RecordTrace(key, in entry, TriggerRecordKind.ShortCircuited, null, reason, 0L);

            var cue = entry.Trigger.Cue;
            if (ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                return;
            }

            var cueContext = TriggerRunnerCueDispatcher.BuildCueContext(
                key,
                in args,
                entry.Phase,
                entry.Priority,
                entry.Order,
                entry.Trigger,
                reason,
                interruptSourceName,
                interruptTriggerId,
                interruptConditionPassed,
                control,
                Config.ECueLevel.Trigger,
                cueKind == ShortCircuitCueKind.Skipped ? Config.ECueLifecycleStage.Skipped : Config.ECueLifecycleStage.Interrupted);
            switch (cueKind)
            {
                case ShortCircuitCueKind.Skipped:
                    cue.OnSkipped(in cueContext);
                    break;
                case ShortCircuitCueKind.Interrupted:
                    cue.OnInterrupted(in cueContext);
                    break;
            }
        }

        private void RecordTrace<TArgs>(
            EventKey<TArgs> key,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            TriggerRecordKind kind,
            bool? predicateResult,
            ShortCircuitReason? reason,
            long elapsedTicks)
        {
            var record = new TriggerTraceRecord(
                (int)entry.Order,
                entry.Phase,
                entry.Priority,
                entry.Order,
                kind,
                predicateResult,
                reason,
                MonotonicTime.GetTimestamp(),
                elapsedTicks,
                string.Empty);
            _tracer.RecordTrigger<TArgs>(_currentTraceScope, record);
        }

        private void NotifyActionFailed<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            in ExecCtx<TCtx> execCtx,
            string actionName,
            int actionIndex,
            int actionCount,
            string message)
        {
            _lifecycle.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, actionName, actionIndex, actionCount, message);
            _observer.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, actionName, actionIndex, actionCount, message, in execCtx);
        }

        private static int GetDebugTriggerId<TArgs>(ITrigger<TArgs, TCtx> trigger)
        {
            return trigger is ITriggerWithId withId ? withId.TriggerId : 0;
        }

        /// <summary>
        /// 区分未进入当前触发器的跳过与执行后传播被中止的 Cue。
        /// </summary>
        private enum ShortCircuitCueKind
        {
            Skipped,
            Interrupted
        }
    }
}
