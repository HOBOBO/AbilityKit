using System;
using System.Collections.Generic;
using System.Diagnostics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Eventing;
using AbilityKit.Diagnostics;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaTriggerDiagnosticsAdapter : ITriggerLifecycle<IWorldResolver>, ITriggerTracer<IWorldResolver>
    {
        private readonly IWorldResolver _services;
        private Stack<DispatchPerformanceScope> _dispatchScopes;
        private Stack<ProbeScope> _evaluateScopes;
        private Stack<ProbeScope> _executeScopes;
        private long _nextScopeId = 1L;

        public MobaTriggerDiagnosticsAdapter(IWorldResolver services)
        {
            _services = services;
        }

        public void OnRegistered<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, IWorldResolver> trigger, int phase, int priority, long order)
        {
            var diagnostics = Diagnostics;
            if (ShouldSampleHook(diagnostics)) diagnostics.Counter(MobaBattleDiagnosticMetric.TriggerRegistered);
        }

        public void OnUnregistered<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, IWorldResolver> trigger)
        {
            var diagnostics = Diagnostics;
            if (ShouldSampleHook(diagnostics)) diagnostics.Counter(MobaBattleDiagnosticMetric.TriggerUnregistered);
        }

        public void OnEventDispatching<TArgs>(EventKey<TArgs> key, in TArgs args)
        {
            var diagnostics = Diagnostics;
            if (ShouldSampleHook(diagnostics)) diagnostics.Counter(MobaBattleDiagnosticMetric.TriggerDispatchStarted);
        }

        public void OnEventDispatched<TArgs>(EventKey<TArgs> key, in TArgs args, int executedCount, int shortCircuitedCount)
        {
            var diagnostics = Diagnostics;
            if (!ShouldSampleHook(diagnostics)) return;

            diagnostics.Counter(MobaBattleDiagnosticMetric.TriggerDispatchCompleted);
            diagnostics.Sample(MobaBattleDiagnosticMetric.TriggerDispatchExecuted, executedCount);
            diagnostics.Sample(MobaBattleDiagnosticMetric.TriggerDispatchShortCircuited, shortCircuitedCount);
        }

        public void OnBeforeEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
            BeginNestedPerformanceScope(
                ref _evaluateScopes,
                MobaBattleDiagnosticMetric.TriggerEvaluateScope);
        }

        public void OnAfterEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool result)
        {
            MobaPerformanceProfiling.End(_evaluateScopes);
        }

        public void OnBeforeExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
        }

        public void OnAfterExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
        }

        public void OnShortCircuit<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ShortCircuitReason reason)
        {
            var diagnostics = Diagnostics;
            if (ShouldSampleHook(diagnostics)) diagnostics.Counter(MobaBattleDiagnosticMetric.TriggerShortCircuit);
        }

        public void OnScopeTransition(string fromScope, string toScope)
        {
        }

        public void OnConditionPassed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName)
        {
        }

        public void OnConditionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName)
        {
        }

        public void OnActionExecuting<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions)
        {
            BeginNestedPerformanceScope(
                ref _executeScopes,
                MobaBattleDiagnosticMetric.TriggerExecuteScope);
        }

        public void OnActionExecuted<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted)
        {
            MobaPerformanceProfiling.End(_executeScopes);
            if (wasInterrupted)
            {
                var diagnostics = Diagnostics;
                if (ShouldSampleHook(diagnostics)) diagnostics.Counter(MobaBattleDiagnosticMetric.TriggerActionInterrupted);
            }
        }

        public void OnActionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage)
        {
            if (string.Equals(actionName, "Evaluate", StringComparison.Ordinal))
            {
                MobaPerformanceProfiling.End(_evaluateScopes);
            }
            else
            {
                MobaPerformanceProfiling.End(_executeScopes);
            }

            var diagnostics = Diagnostics;
            if (diagnostics == null) return;

            diagnostics.Counter(MobaBattleDiagnosticMetric.TriggerActionFailed);
            diagnostics.Warning(
                MobaBattleDiagnosticMetric.TriggerActionFailed,
                () => $"[MobaTriggerDiagnosticsAdapter] Trigger action failed. event={GetEventName(key)} phase={phase} priority={priority} order={order} actionId={actionId} action={actionName} index={actionIndex}/{totalActions} error={errorMessage}");
        }

        public TraceScope BeginTrace<TArgs>(EventKey<TArgs> key, in TArgs args)
        {
            var hasOuterDispatch = _dispatchScopes != null && _dispatchScopes.Count > 0;
            var started = TryBeginPerformanceScope(
                MobaBattleDiagnosticMetric.TriggerDispatchScope,
                out var scope);
            if (started || hasOuterDispatch)
            {
                if (_dispatchScopes == null)
                {
                    _dispatchScopes = new Stack<DispatchPerformanceScope>();
                }

                _dispatchScopes.Push(new DispatchPerformanceScope(
                    scope,
                    _evaluateScopes?.Count ?? 0,
                    _executeScopes?.Count ?? 0));
            }

            return new TraceScope(_nextScopeId++, Stopwatch.GetTimestamp(), GetEventName(key), key.GetHashCode());
        }

        public void RecordTrigger<TArgs>(TraceScope scope, TriggerTraceRecord record)
        {
            var diagnostics = Diagnostics;
            if (!ShouldSampleHook(diagnostics)) return;

            switch (record.Kind)
            {
                case TriggerRecordKind.Evaluated:
                    diagnostics.Counter(MobaBattleDiagnosticMetric.TriggerEvaluated);
                    diagnostics.Counter(record.PredicateResult == false ? MobaBattleDiagnosticMetric.TriggerEvaluateFailed : MobaBattleDiagnosticMetric.TriggerEvaluatePassed);
                    RecordElapsedSample(diagnostics, MobaBattleDiagnosticMetric.TriggerEvaluateDuration, record.ElapsedTicks);
                    break;
                case TriggerRecordKind.Executed:
                    diagnostics.Counter(MobaBattleDiagnosticMetric.TriggerExecuted);
                    RecordElapsedSample(diagnostics, MobaBattleDiagnosticMetric.TriggerExecuteDuration, record.ElapsedTicks);
                    break;
                case TriggerRecordKind.ShortCircuited:
                    diagnostics.Counter(MobaBattleDiagnosticMetric.TriggerShortCircuit);
                    break;
            }
        }

        public void EndTrace(TraceScope scope)
        {
            try
            {
                var diagnostics = Diagnostics;
                if (ShouldSampleHook(diagnostics)) RecordElapsedSample(diagnostics, MobaBattleDiagnosticMetric.TriggerDispatchDuration, Stopwatch.GetTimestamp() - scope.StartTimestamp);
            }
            finally
            {
                EndDispatchPerformanceScope();
            }
        }

        private void BeginNestedPerformanceScope(
            ref Stack<ProbeScope> scopes,
            string marker)
        {
            var hasOuterScope = scopes != null && scopes.Count > 0;
            var started = TryBeginPerformanceScope(marker, out var scope);
            if (!started && !hasOuterScope) return;

            if (scopes == null)
            {
                scopes = new Stack<ProbeScope>();
            }

            // A default sentinel preserves stack pairing if profiling is toggled during reentrant dispatch.
            scopes.Push(scope);
        }

        private bool TryBeginPerformanceScope(string marker, out ProbeScope scope)
        {
            scope = default;
            if (!ProfilerHub.IsEnabled) return false;

            return MobaPerformanceProfiling.TryBegin(
                Diagnostics,
                MobaBattleDiagnosticChannel.TriggerHook,
                marker,
                out scope);
        }

        private void EndDispatchPerformanceScope()
        {
            if (_dispatchScopes == null || _dispatchScopes.Count == 0) return;

            var dispatch = _dispatchScopes.Pop();
            EndToDepth(_evaluateScopes, dispatch.EvaluateDepth);
            EndToDepth(_executeScopes, dispatch.ExecuteDepth);
            dispatch.Scope.Dispose();
        }

        private static void EndToDepth(Stack<ProbeScope> scopes, int depth)
        {
            if (scopes == null) return;

            while (scopes.Count > depth)
            {
                scopes.Pop().Dispose();
            }
        }

        private readonly struct DispatchPerformanceScope
        {
            public DispatchPerformanceScope(ProbeScope scope, int evaluateDepth, int executeDepth)
            {
                Scope = scope;
                EvaluateDepth = evaluateDepth;
                ExecuteDepth = executeDepth;
            }

            public ProbeScope Scope { get; }
            public int EvaluateDepth { get; }
            public int ExecuteDepth { get; }
        }

        private IMobaBattleDiagnosticsService Diagnostics
        {
            get
            {
                if (_services == null) return null;
                return _services.TryResolve<IMobaBattleDiagnosticsService>(out var diagnostics) ? diagnostics : null;
            }
        }

        private static bool ShouldSampleHook(IMobaBattleDiagnosticsService diagnostics)
        {
            return diagnostics != null && diagnostics.ShouldSample(MobaBattleDiagnosticChannel.TriggerHook);
        }

        private static void RecordElapsedSample(IMobaBattleDiagnosticsService diagnostics, string metricName, long elapsedTicks)
        {
            if (diagnostics == null || elapsedTicks <= 0L) return;
            diagnostics.Sample(metricName, elapsedTicks * 1000.0d / Stopwatch.Frequency);
        }

        private static string GetEventName<TArgs>(EventKey<TArgs> key)
        {
            return key.StringId ?? key.IntId.ToString();
        }
    }
}
