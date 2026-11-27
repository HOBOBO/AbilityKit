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
        private void NotifyRegistered<TArgs>(EventKey<TArgs> key, TriggerRunnerEntry<TArgs, TCtx> entry)
        {
            try
            {
                _lifecycle.OnRegistered(key, entry.Trigger, entry.Phase, entry.Priority, entry.Order);
            }
            catch (Exception exception)
            {
                ReportDiagnosticFailure(key, entry, "Lifecycle.OnRegistered", exception);
            }
        }

        private void NotifyUnregistered<TArgs>(EventKey<TArgs> key, TriggerRunnerEntry<TArgs, TCtx> entry)
        {
            try
            {
                _lifecycle.OnUnregistered(key, entry.Trigger);
            }
            catch (Exception exception)
            {
                Log.Exception(exception,
                    $"[TriggerRunner] Lifecycle.OnUnregistered failed. eventId={key.IntId} phase={entry.Phase} priority={entry.Priority} order={entry.Order}");
            }
        }

        private void NotifyEventDispatched<TArgs>(EventKey<TArgs> key, in TArgs args, int executedCount, int shortCircuitedCount)
        {
            try
            {
                _lifecycle.OnEventDispatched(key, in args, executedCount, shortCircuitedCount);
            }
            catch (Exception exception)
            {
                Log.Exception(exception,
                    $"[TriggerRunner] Lifecycle.OnEventDispatched failed. eventId={key.IntId} executed={executedCount} shortCircuited={shortCircuitedCount}");
            }
        }

        private void NotifyEvaluationException<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx,
            in TraceScope? traceScope,
            Exception failure,
            long elapsedTicks)
        {
            TryRecordTrace(key, entry, in traceScope, TriggerRecordKind.Evaluated, false, null, elapsedTicks);

            var triggerType = entry.Trigger.GetType().Name;
            Log.Exception(failure,
                $"[TriggerRunner] Trigger evaluation exception. eventId={key.IntId} argsType={typeof(TArgs).Name} triggerType={triggerType} phase={entry.Phase} priority={entry.Priority} order={entry.Order}");

            try
            {
                _lifecycle.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, triggerType);
            }
            catch (Exception exception)
            {
                ReportDiagnosticFailure(key, entry, "Lifecycle.OnConditionFailed", exception);
            }

            try
            {
                _observer.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, triggerType, in execCtx);
            }
            catch (Exception exception)
            {
                ReportDiagnosticFailure(key, entry, "Observer.OnConditionFailed", exception);
            }

            NotifyActionFailed(key, in args, entry, in execCtx, "Evaluate", 0, 0, failure.Message);

            try
            {
                NotifyConditionFailedCue(key, in args, entry, control);
            }
            catch (Exception exception)
            {
                ReportDiagnosticFailure(key, entry, "Cue.OnConditionFailed", exception);
            }
        }

        private void NotifyConditionPassed<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            TriggerRunnerEntry<TArgs, TCtx> entry,
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
            TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            _lifecycle.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
            _observer.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);
            NotifyConditionFailedCue(key, in args, entry, control);
        }

        private static void NotifyConditionFailedCue<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control)
        {
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
            TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx,
            in TraceScope? traceScope,
            ShortCircuitReason reason,
            string interruptSourceName,
            int interruptTriggerId,
            bool interruptConditionPassed,
            ShortCircuitCueKind cueKind)
        {
            try
            {
                _lifecycle.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, reason);
            }
            catch (Exception exception)
            {
                ReportShortCircuitNotificationFailure(key, entry, reason, "Lifecycle", exception);
            }

            try
            {
                _observer.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, TriggerRunnerCueDispatcher.MapReason(reason), in execCtx);
            }
            catch (Exception exception)
            {
                ReportShortCircuitNotificationFailure(key, entry, reason, "Observer", exception);
            }

            TryRecordTrace(key, entry, in traceScope, TriggerRecordKind.ShortCircuited, null, reason, 0L);

            try
            {
                NotifyShortCircuitCue(key, in args, entry, control, reason, interruptSourceName, interruptTriggerId, interruptConditionPassed, cueKind);
            }
            catch (Exception exception)
            {
                ReportShortCircuitNotificationFailure(key, entry, reason, "Cue", exception);
            }
        }

        private static void ReportShortCircuitNotificationFailure<TArgs>(
            EventKey<TArgs> key,
            TriggerRunnerEntry<TArgs, TCtx> entry,
            ShortCircuitReason reason,
            string notification,
            Exception exception)
        {
            Log.Exception(exception,
                $"[TriggerRunner] Short-circuit notification failed. notification={notification} eventId={key.IntId} phase={entry.Phase} priority={entry.Priority} order={entry.Order} reason={reason}");
        }

        private static void NotifyShortCircuitCue<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            ShortCircuitReason reason,
            string interruptSourceName,
            int interruptTriggerId,
            bool interruptConditionPassed,
            ShortCircuitCueKind cueKind)
        {
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

        /// <summary>
        /// 为当前派发打开追踪；仅用空值表示开启失败，追踪器返回的默认结构仍有效。
        /// </summary>
        private TraceScope? TryBeginTrace<TArgs>(EventKey<TArgs> key, in TArgs args)
        {
            try
            {
                return _tracer.BeginTrace(key, in args);
            }
            catch (Exception exception)
            {
                Log.Exception(exception, $"[TriggerRunner] Trace begin failed. eventId={key.IntId}");
                return null;
            }
        }

        private void TryRecordTrace<TArgs>(
            EventKey<TArgs> key,
            TriggerRunnerEntry<TArgs, TCtx> entry,
            in TraceScope? traceScope,
            TriggerRecordKind kind,
            bool? predicateResult,
            ShortCircuitReason? reason,
            long elapsedTicks)
        {
            if (!(traceScope is TraceScope scope))
            {
                return;
            }

            try
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
                _tracer.RecordTrigger<TArgs>(scope, record);
            }
            catch (Exception exception)
            {
                if (reason.HasValue)
                {
                    ReportShortCircuitNotificationFailure(key, entry, reason.Value, "Trace", exception);
                }
                else
                {
                    ReportDiagnosticFailure(key, entry, $"Trace.{kind}", exception);
                }
            }
        }

        private void TryEndTrace<TArgs>(EventKey<TArgs> key, in TraceScope? traceScope)
        {
            if (!(traceScope is TraceScope scope))
            {
                return;
            }

            try
            {
                _tracer.EndTrace(scope);
            }
            catch (Exception exception)
            {
                Log.Exception(exception, $"[TriggerRunner] Trace end failed. eventId={key.IntId} scopeId={scope.ScopeId}");
            }
        }

        private void NotifyActionFailed<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            TriggerRunnerEntry<TArgs, TCtx> entry,
            in ExecCtx<TCtx> execCtx,
            string actionName,
            int actionIndex,
            int actionCount,
            string message)
        {
            try
            {
                _lifecycle.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, actionName, actionIndex, actionCount, message);
            }
            catch (Exception exception)
            {
                ReportDiagnosticFailure(key, entry, "Lifecycle.OnActionFailed", exception);
            }

            try
            {
                _observer.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, actionName, actionIndex, actionCount, message, in execCtx);
            }
            catch (Exception exception)
            {
                ReportDiagnosticFailure(key, entry, "Observer.OnActionFailed", exception);
            }
        }

        private static void ReportDiagnosticFailure<TArgs>(
            EventKey<TArgs> key,
            TriggerRunnerEntry<TArgs, TCtx> entry,
            string diagnostic,
            Exception exception)
        {
            Log.Exception(exception,
                $"[TriggerRunner] Diagnostic failed. diagnostic={diagnostic} eventId={key.IntId} phase={entry.Phase} priority={entry.Priority} order={entry.Order}");
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
