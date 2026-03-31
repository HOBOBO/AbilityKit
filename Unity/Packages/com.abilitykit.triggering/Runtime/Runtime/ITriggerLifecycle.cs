using System;
using System.Diagnostics;
using AbilityKit.Core.Eventing;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 触发器生命周期钩子接口
    /// 允许在触发器执行的关键节点注入逻辑（日志，性能监控、AOP等）
    /// </summary>
    public interface ITriggerLifecycle<TCtx>
    {
        /// <summary>
        /// 触发器被注册时调用
        /// </summary>
        void OnRegistered<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase, int priority, long order);

        /// <summary>
        /// 触发器被注销时调用
        /// </summary>
        void OnUnregistered<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger);

        /// <summary>
        /// 事件被派发前调用
        /// </summary>
        void OnEventDispatching<TArgs>(EventKey<TArgs> key, in TArgs args);

        /// <summary>
        /// 事件派发完成后调用
        /// </summary>
        void OnEventDispatched<TArgs>(EventKey<TArgs> key, in TArgs args, int executedCount, int shortCircuitedCount);

        /// <summary>
        /// 触发器 Evaluate 前调用
        /// </summary>
        void OnBeforeEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order);

        /// <summary>
        /// 触发器 Evaluate 后调用
        /// </summary>
        void OnAfterEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool result);

        /// <summary>
        /// 触发器 Execute 前调用
        /// </summary>
        void OnBeforeExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order);

        /// <summary>
        /// 触发器 Execute 后调用
        /// </summary>
        void OnAfterExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order);

        /// <summary>
        /// 触发器短路时调用
        /// </summary>
        void OnShortCircuit<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ShortCircuitReason reason);

        /// <summary>
        /// 层级切换时调用（从父级进入子级或反之）
        /// </summary>
        void OnScopeTransition(string fromScope, string toScope);
    }

    /// <summary>
    /// 短路原因枚举
    /// </summary>
    public enum ShortCircuitReason
    {
        None = 0,
        StopPropagation = 1,
        Cancel = 2,
        ParentBlocked = 3,
        ConditionFailed = 4,
    }

    /// <summary>
    /// 触发器执行追踪接口
    /// 用于记录和分析触发器执行链路
    /// </summary>
    public interface ITriggerTracer<TCtx>
    {
        /// <summary>
        /// 开始追踪一个事件派发
        /// </summary>
        TraceScope BeginTrace<TArgs>(EventKey<TArgs> key, in TArgs args);

        /// <summary>
        /// 记录触发器执行
        /// </summary>
        void RecordTrigger<TArgs>(TraceScope scope, TriggerTraceRecord record);

        /// <summary>
        /// 结束追踪
        /// </summary>
        void EndTrace(TraceScope scope);
    }

    /// <summary>
    /// 追踪范围标记
    /// </summary>
    public readonly struct TraceScope
    {
        public readonly long ScopeId;
        public readonly long StartTimestamp;
        public readonly string EventName;
        public readonly int EventHash;

        public TraceScope(long scopeId, long startTimestamp, string eventName, int eventHash)
        {
            ScopeId = scopeId;
            StartTimestamp = startTimestamp;
            EventName = eventName;
            EventHash = eventHash;
        }
    }

    /// <summary>
    /// 触发器执行记录
    /// </summary>
    public readonly struct TriggerTraceRecord
    {
        public readonly int TriggerId;
        public readonly int Phase;
        public readonly int Priority;
        public readonly long Order;
        public readonly TriggerRecordKind Kind;
        public readonly bool? PredicateResult;
        public readonly ShortCircuitReason? ShortCircuitReason;
        public readonly long Timestamp;
        public readonly long ElapsedTicks;
        public readonly string ScopePath;

        public TriggerTraceRecord(
            int triggerId,
            int phase,
            int priority,
            long order,
            TriggerRecordKind kind,
            bool? predicateResult,
            ShortCircuitReason? shortCircuitReason,
            long timestamp,
            long elapsedTicks,
            string scopePath)
        {
            TriggerId = triggerId;
            Phase = phase;
            Priority = priority;
            Order = order;
            Kind = kind;
            PredicateResult = predicateResult;
            ShortCircuitReason = shortCircuitReason;
            Timestamp = timestamp;
            ElapsedTicks = elapsedTicks;
            ScopePath = scopePath;
        }
    }

    /// <summary>
    /// 触发器记录类型
    /// </summary>
    public enum TriggerRecordKind
    {
        Evaluated = 0,
        Executed = 1,
        ShortCircuited = 2,
    }

    /// <summary>
    /// 触发器溯源信息
    /// 用于追踪事件来源和执行链路
    /// 注：业务来源类型（如 Skill/Buff/Item）应由业务层自行定义，核心包保持通用
    /// </summary>
    public readonly struct TriggerSourceInfo
    {
        /// <summary>
        /// 来源标识类型（由业务层定义，如 1=Skill, 2=Buff, 3=Item）
        /// </summary>
        public readonly int SourceTypeId;

        /// <summary>
        /// 来源标识（如技能ID、BuffID、系统名称等）
        /// </summary>
        public readonly int SourceId;

        /// <summary>
        /// 来源名称（用于调试显示）
        /// </summary>
        public readonly string SourceName;

        /// <summary>
        /// 触发来源的实体ID
        /// </summary>
        public readonly int EntityId;

        /// <summary>
        /// 父级溯源信息的深度（用于追踪嵌套事件）
        /// </summary>
        public readonly int ParentDepth;

        /// <summary>
        /// 递归深度
        /// </summary>
        public readonly int Depth;

        public TriggerSourceInfo(int sourceTypeId, int sourceId, string sourceName, int entityId, int parentDepth, int depth)
        {
            SourceTypeId = sourceTypeId;
            SourceId = sourceId;
            SourceName = sourceName;
            EntityId = entityId;
            ParentDepth = parentDepth;
            Depth = depth;
        }

        /// <summary>
        /// 创建一个新的溯源信息，增加深度
        /// </summary>
        public TriggerSourceInfo WithDepth(int additionalDepth = 1)
        {
            return new TriggerSourceInfo(SourceTypeId, SourceId, SourceName, EntityId, Depth, Depth + additionalDepth);
        }

        /// <summary>
        /// 创建顶级溯源信息
        /// </summary>
        public static TriggerSourceInfo CreateRoot(int sourceTypeId, int sourceId, string sourceName, int entityId)
        {
            return new TriggerSourceInfo(sourceTypeId, sourceId, sourceName, entityId, -1, 0);
        }

        /// <summary>
        /// 创建未知来源的溯源信息
        /// </summary>
        public static TriggerSourceInfo CreateUnknown()
        {
            return new TriggerSourceInfo(0, 0, "Unknown", 0, -1, 0);
        }
    }

   

    /// <summary>
    /// 触发器统计信息
    /// </summary>
    public struct TriggerStatistics
    {
        public int TotalTriggered;
        public int TotalEvaluated;
        public int TotalExecuted;
        public int TotalShortCircuited;
        public long TotalEvaluateTicks;
        public long TotalExecuteTicks;

        public double AverageEvaluateTicks => TotalEvaluated > 0 ? (double)TotalEvaluateTicks / TotalEvaluated : 0;
        public double AverageExecuteTicks => TotalExecuted > 0 ? (double)TotalExecuteTicks / TotalExecuted : 0;

        public void Reset()
        {
            TotalTriggered = 0;
            TotalEvaluated = 0;
            TotalExecuted = 0;
            TotalShortCircuited = 0;
            TotalEvaluateTicks = 0;
            TotalExecuteTicks = 0;
        }
    }
}
