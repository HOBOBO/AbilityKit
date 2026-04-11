using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 空生命周期实现（不执行任何操作）
    /// </summary>
    public sealed class NullTriggerLifecycle<TCtx> : ITriggerLifecycle<TCtx>
    {
        public static readonly NullTriggerLifecycle<TCtx> Instance = new NullTriggerLifecycle<TCtx>();

        private NullTriggerLifecycle() { }

        public void OnRegistered<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase, int priority, long order) { }
        public void OnUnregistered<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger) { }
        public void OnEventDispatching<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args) { }
        public void OnEventDispatched<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int executedCount, int shortCircuitedCount) { }
        public void OnBeforeEvaluate<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order) { }
        public void OnAfterEvaluate<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool result) { }
        public void OnBeforeExecute<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order) { }
        public void OnAfterExecute<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order) { }
        public void OnShortCircuit<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ShortCircuitReason reason) { }
        public void OnScopeTransition(string fromScope, string toScope) { }
        public void OnConditionPassed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName) { }
        public void OnConditionFailed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName) { }
        public void OnActionExecuting<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions) { }
        public void OnActionExecuted<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted) { }
        public void OnActionFailed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage) { }
    }

    /// <summary>
    /// 空观察者实现（不执行任何操作）
    /// </summary>
    public sealed class NullTriggerObserver<TCtx> : ITriggerObserver<TCtx>
    {
        public static readonly NullTriggerObserver<TCtx> Instance = new NullTriggerObserver<TCtx>();

        private NullTriggerObserver() { }

        public void OnEvaluate<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool passed, in ExecCtx<TCtx> ctx) { }
        public void OnExecute<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, in ExecCtx<TCtx> ctx) { }
        public void OnShortCircuit<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, AbilityKit.Triggering.Runtime.ETriggerShortCircuitReason reason, in ExecCtx<TCtx> ctx) { }
        public void OnConditionPassed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName, in ExecCtx<TCtx> ctx) { }
        public void OnConditionFailed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName, in ExecCtx<TCtx> ctx) { }
        public void OnActionExecuting<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, in ExecCtx<TCtx> ctx) { }
        public void OnActionExecuted<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted, in ExecCtx<TCtx> ctx) { }
        public void OnActionFailed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage, in ExecCtx<TCtx> ctx) { }
    }

    /// <summary>
    /// 空追踪器实现（不执行任何操作）
    /// </summary>
    public sealed class NullTriggerTracer<TCtx> : ITriggerTracer<TCtx>
    {
        public static readonly NullTriggerTracer<TCtx> Instance = new NullTriggerTracer<TCtx>();

        private NullTriggerTracer() { }

        public TraceScope BeginTrace<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args) => default;
        public void RecordTrigger<TArgs>(TraceScope scope, TriggerTraceRecord record) { }
        public void EndTrace(TraceScope scope) { }
    }

    /// <summary>
    /// 调试用生命周期实现
    /// 输出详细的触发器执行日志
    /// </summary>
    public sealed class DebugTriggerLifecycle<TCtx> : ITriggerLifecycle<TCtx>
    {
        private readonly string _scopeName;
        private readonly bool _logEvaluate;
        private readonly bool _logExecute;
        private readonly bool _logShortCircuit;

        public DebugTriggerLifecycle(string scopeName = null, bool logEvaluate = true, bool logExecute = true, bool logShortCircuit = true)
        {
            _scopeName = scopeName ?? "Trigger";
            _logEvaluate = logEvaluate;
            _logExecute = logExecute;
            _logShortCircuit = logShortCircuit;
        }

        public void OnRegistered<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase, int priority, long order)
        {
            Log.Info($"[{_scopeName}] Registered: {key.StringId ?? key.IntId.ToString()} Phase={phase} Priority={priority}");
        }

        public void OnUnregistered<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger)
        {
            Log.Info($"[{_scopeName}] Unregistered: {key.StringId ?? key.IntId.ToString()}");
        }

        public void OnEventDispatching<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args)
        {
            Log.Info($"[{_scopeName}] >>> Dispatching: {key.StringId ?? key.IntId.ToString()}");
        }

        public void OnEventDispatched<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int executedCount, int shortCircuitedCount)
        {
            Log.Info($"[{_scopeName}] <<< Dispatched: {key.StringId ?? key.IntId.ToString()} Executed={executedCount} ShortCircuited={shortCircuitedCount}");
        }

        public void OnBeforeEvaluate<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
            if (_logEvaluate)
                Log.Info($"[{_scopeName}] Evaluate [{phase},{priority},{order}]: {key.StringId ?? key.IntId.ToString()}");
        }

        public void OnAfterEvaluate<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool result)
        {
            if (_logEvaluate)
                Log.Info($"[{_scopeName}] Evaluate [{phase},{priority},{order}]: {key.StringId ?? key.IntId.ToString()} = {result}");
        }

        public void OnBeforeExecute<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
            if (_logExecute)
                Log.Info($"[{_scopeName}] Execute [{phase},{priority},{order}]: {key.StringId ?? key.IntId.ToString()}");
        }

        public void OnAfterExecute<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
            if (_logExecute)
                Log.Info($"[{_scopeName}] Executed [{phase},{priority},{order}]: {key.StringId ?? key.IntId.ToString()}");
        }

        public void OnShortCircuit<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ShortCircuitReason reason)
        {
            if (_logShortCircuit)
                Log.Warning($"[{_scopeName}] ShortCircuit [{phase},{priority},{order}]: {key.StringId ?? key.IntId.ToString()} Reason={reason}");
        }

        public void OnScopeTransition(string fromScope, string toScope)
        {
            Log.Info($"[{_scopeName}] Scope: {fromScope} -> {toScope}");
        }

        public void OnConditionPassed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName)
        {
            if (_logEvaluate)
                Log.Info($"[{_scopeName}] Condition Passed [{phase},{priority},{order}] {conditionName}(Id={conditionId})");
        }

        public void OnConditionFailed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName)
        {
            if (_logEvaluate)
                Log.Warning($"[{_scopeName}] Condition Failed [{phase},{priority},{order}] {conditionName}(Id={conditionId})");
        }

        public void OnActionExecuting<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions)
        {
            if (_logExecute)
                Log.Info($"[{_scopeName}] Action [{phase},{priority},{order}] [{actionIndex}/{totalActions}] {actionName}(Id={actionId})");
        }

        public void OnActionExecuted<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted)
        {
            if (_logExecute)
                {
                    if (wasInterrupted)
                        Log.Warning($"[{_scopeName}] Action Interrupted [{actionIndex}/{totalActions}] {actionName}(Id={actionId})");
                    else
                        Log.Info($"[{_scopeName}] Action Done [{actionIndex}/{totalActions}] {actionName}(Id={actionId})");
                }
        }

        public void OnActionFailed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage)
        {
            Log.Error($"[{_scopeName}] Action Error [{actionIndex}/{totalActions}] {actionName}(Id={actionId}): {errorMessage}");
        }
    }

    /// <summary>
    /// 性能监控生命周期实现
    /// </summary>
    public sealed class PerformanceTriggerLifecycle<TCtx> : ITriggerLifecycle<TCtx>
    {
        private readonly Dictionary<int, TriggerStatistics> _statsByTriggerId = new Dictionary<int, TriggerStatistics>();
        private readonly Dictionary<string, TriggerStatistics> _statsByEventName = new Dictionary<string, TriggerStatistics>();
        private readonly Dictionary<string, TriggerStatistics> _statsByScope = new Dictionary<string, TriggerStatistics>();

        private int _currentTriggerId = -1;
        private long _currentEvaluateStart;
        private long _currentExecuteStart;

        public IReadOnlyDictionary<int, TriggerStatistics> StatsByTriggerId => _statsByTriggerId;
        public IReadOnlyDictionary<string, TriggerStatistics> StatsByEventName => _statsByEventName;
        public IReadOnlyDictionary<string, TriggerStatistics> StatsByScope => _statsByScope;

        public void Reset()
        {
            _statsByTriggerId.Clear();
            _statsByEventName.Clear();
            _statsByScope.Clear();
        }

        private void IncrementTriggered(string eventName)
        {
            if (!_statsByEventName.TryGetValue(eventName, out var stats))
            {
                stats = new TriggerStatistics();
                _statsByEventName[eventName] = stats;
            }
            else
            {
                stats.TotalTriggered++;
                _statsByEventName[eventName] = stats;
            }
        }

        private void IncrementEvaluated(int triggerId, string eventName, long elapsedTicks)
        {
            if (!_statsByTriggerId.TryGetValue(triggerId, out var byId))
            {
                byId = new TriggerStatistics { TotalEvaluated = 1, TotalEvaluateTicks = elapsedTicks };
                _statsByTriggerId[triggerId] = byId;
            }
            else
            {
                byId.TotalEvaluated++;
                byId.TotalEvaluateTicks += elapsedTicks;
                _statsByTriggerId[triggerId] = byId;
            }

            if (!_statsByEventName.TryGetValue(eventName, out var byEvent))
            {
                byEvent = new TriggerStatistics { TotalEvaluated = 1, TotalEvaluateTicks = elapsedTicks };
                _statsByEventName[eventName] = byEvent;
            }
            else
            {
                byEvent.TotalEvaluated++;
                byEvent.TotalEvaluateTicks += elapsedTicks;
                _statsByEventName[eventName] = byEvent;
            }

            if (!_statsByScope.TryGetValue("Default", out var byScope))
            {
                byScope = new TriggerStatistics { TotalEvaluated = 1, TotalEvaluateTicks = elapsedTicks };
                _statsByScope["Default"] = byScope;
            }
            else
            {
                byScope.TotalEvaluated++;
                byScope.TotalEvaluateTicks += elapsedTicks;
                _statsByScope["Default"] = byScope;
            }
        }

        private void IncrementExecuted(int triggerId, string eventName, long elapsedTicks)
        {
            if (!_statsByTriggerId.TryGetValue(triggerId, out var byId))
            {
                byId = new TriggerStatistics { TotalExecuted = 1, TotalExecuteTicks = elapsedTicks };
                _statsByTriggerId[triggerId] = byId;
            }
            else
            {
                byId.TotalExecuted++;
                byId.TotalExecuteTicks += elapsedTicks;
                _statsByTriggerId[triggerId] = byId;
            }

            if (!_statsByEventName.TryGetValue(eventName, out var byEvent))
            {
                byEvent = new TriggerStatistics { TotalExecuted = 1, TotalExecuteTicks = elapsedTicks };
                _statsByEventName[eventName] = byEvent;
            }
            else
            {
                byEvent.TotalExecuted++;
                byEvent.TotalExecuteTicks += elapsedTicks;
                _statsByEventName[eventName] = byEvent;
            }

            if (!_statsByScope.TryGetValue("Default", out var byScope))
            {
                byScope = new TriggerStatistics { TotalExecuted = 1, TotalExecuteTicks = elapsedTicks };
                _statsByScope["Default"] = byScope;
            }
            else
            {
                byScope.TotalExecuted++;
                byScope.TotalExecuteTicks += elapsedTicks;
                _statsByScope["Default"] = byScope;
            }
        }

        private void IncrementShortCircuited(int triggerId, string eventName)
        {
            if (!_statsByTriggerId.TryGetValue(triggerId, out var byId))
            {
                byId = new TriggerStatistics { TotalShortCircuited = 1 };
                _statsByTriggerId[triggerId] = byId;
            }
            else
            {
                byId.TotalShortCircuited++;
                _statsByTriggerId[triggerId] = byId;
            }

            if (!_statsByEventName.TryGetValue(eventName, out var byEvent))
            {
                byEvent = new TriggerStatistics { TotalShortCircuited = 1 };
                _statsByEventName[eventName] = byEvent;
            }
            else
            {
                byEvent.TotalShortCircuited++;
                _statsByEventName[eventName] = byEvent;
            }

            if (!_statsByScope.TryGetValue("Default", out var byScope))
            {
                byScope = new TriggerStatistics { TotalShortCircuited = 1 };
                _statsByScope["Default"] = byScope;
            }
            else
            {
                byScope.TotalShortCircuited++;
                _statsByScope["Default"] = byScope;
            }
        }

        public void OnRegistered<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase, int priority, long order)
        {
            // 可以在这里记录触发器注册信息
        }

        public void OnUnregistered<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger)
        {
            // 可以在这里清理统计信息
        }

        public void OnEventDispatching<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args)
        {
            var eventName = key.StringId ?? key.IntId.ToString();
            IncrementTriggered(eventName);
        }

        public void OnEventDispatched<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int executedCount, int shortCircuitedCount)
        {
            // 事件派发完成
        }

        public void OnBeforeEvaluate<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
            _currentTriggerId = (int)order;
            _currentEvaluateStart = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        public void OnAfterEvaluate<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool result)
        {
            var elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - _currentEvaluateStart;
            var eventName = key.StringId ?? key.IntId.ToString();
            IncrementEvaluated((int)order, eventName, elapsed);
        }

        public void OnBeforeExecute<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
            _currentExecuteStart = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        public void OnAfterExecute<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
            var elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - _currentExecuteStart;
            var eventName = key.StringId ?? key.IntId.ToString();
            IncrementExecuted((int)order, eventName, elapsed);
        }

        public void OnShortCircuit<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ShortCircuitReason reason)
        {
            var eventName = key.StringId ?? key.IntId.ToString();
            IncrementShortCircuited((int)order, eventName);
        }

        public void OnScopeTransition(string fromScope, string toScope)
        {
            // 可以在这里记录层级切换
        }

        public void OnConditionPassed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName)
        {
            // 可扩展：按 conditionId 统计
        }

        public void OnConditionFailed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName)
        {
            // 可扩展：按 conditionId 统计
        }

        public void OnActionExecuting<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions)
        {
            // 可扩展：记录每个 Action 执行耗时
        }

        public void OnActionExecuted<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted)
        {
            // 可扩展：按 actionId 统计
        }

        public void OnActionFailed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage)
        {
            // 可扩展：记录错误
        }

        /// <summary>
        /// 打印统计报告
        /// </summary>
        public void PrintReport()
        {
            Log.Info("=== Trigger Performance Report ===");
            foreach (var kvp in _statsByEventName)
            {
                var s = kvp.Value;
                Log.Info($"[{kvp.Key}] Triggered={s.TotalTriggered} Evaluated={s.TotalEvaluated} Executed={s.TotalExecuted} ShortCircuited={s.TotalShortCircuited}");
                Log.Info($"  AvgEvaluate={s.AverageEvaluateTicks:F2}ticks AvgExecute={s.AverageExecuteTicks:F2}ticks");
            }
        }
    }

    /// <summary>
    /// 性能监控追踪器实现
    /// </summary>
    public sealed class PerformanceTriggerTracer<TCtx> : ITriggerTracer<TCtx>
    {
        private readonly List<TriggerTraceRecord> _records = new List<TriggerTraceRecord>();
        private long _nextScopeId = 1;

        public IReadOnlyList<TriggerTraceRecord> Records => _records;

        public TraceScope BeginTrace<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args)
        {
            var scopeId = _nextScopeId++;
            var timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            var eventName = key.StringId ?? key.IntId.ToString();
            return new TraceScope(scopeId, timestamp, eventName, key.GetHashCode());
        }

        public void RecordTrigger<TArgs>(TraceScope scope, TriggerTraceRecord record)
        {
            _records.Add(record);
        }

        public void EndTrace(TraceScope scope)
        {
            // 可以在这里清理或处理记录的追踪数据
        }

        public void Clear()
        {
            _records.Clear();
        }
    }
}
