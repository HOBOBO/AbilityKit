using System;
using System.Collections.Generic;
using AbilityKit.Core.Timing;

namespace AbilityKit.Ability.Flow
{
    /// <summary>
    /// Flow 节点同步执行选项，用于统一正式 API 的执行边界、异常处理和死循环保护。
    /// </summary>
    public sealed class FlowExecutionOptions
    {
        /// <summary>
        /// 创建默认执行选项。异常会被捕获到 FlowExecutionResult，最大步数为 1024。
        /// </summary>
        public static FlowExecutionOptions Default => new FlowExecutionOptions();

        /// <summary>
        /// 每次 Step 传入的 deltaTime。
        /// </summary>
        public float DeltaTime { get; set; }

        /// <summary>
        /// 最大执行步数。小于等于 0 表示不限制。
        /// </summary>
        public int MaxSteps { get; set; } = 1024;

        /// <summary>
        /// 节点执行异常回调。默认情况下异常会被 FlowRunner 捕获并使结果失败。
        /// </summary>
        public Action<Exception> ExceptionHandler { get; set; }

        /// <summary>
        /// 为 true 时，执行中捕获到的异常会在返回结果前重新抛出。
        /// </summary>
        public bool RethrowExceptions { get; set; }

        /// <summary>
        /// 外部观测器，用于收集节点生命周期、状态变化和异常。
        /// </summary>
        public IFlowObserver Observer { get; set; }

        /// <summary>
        /// Trace 记录器，用于对外暴露可快照的执行链路。
        /// </summary>
        public IFlowTraceRecorder TraceRecorder { get; set; }
    }

    public enum FlowTraceEventType
    {
        RunStarted = 0,
        RunFinished = 1,
        StatusChanged = 2,
        NodeEnter = 3,
        NodeTick = 4,
        NodeExit = 5,
        NodeInterrupt = 6,
        UnhandledException = 7,
        PumpLimitExceeded = 8
    }

    public readonly struct FlowTraceData
    {
        public readonly int Sequence;
        public readonly int RunId;
        public readonly FlowTraceEventType Type;
        public readonly string NodeName;
        public readonly FlowStatus Status;
        public readonly float DeltaTime;
        public readonly long Timestamp;
        public readonly long ElapsedTicks;
        public readonly string Message;
        public readonly Exception Exception;

        public FlowTraceData(
            int sequence,
            int runId,
            FlowTraceEventType type,
            string nodeName,
            FlowStatus status,
            float deltaTime,
            long timestamp,
            long elapsedTicks,
            string message,
            Exception exception)
        {
            Sequence = sequence;
            RunId = runId;
            Type = type;
            NodeName = nodeName ?? string.Empty;
            Status = status;
            DeltaTime = deltaTime;
            Timestamp = timestamp;
            ElapsedTicks = elapsedTicks;
            Message = message ?? string.Empty;
            Exception = exception;
        }
    }

    public interface IFlowObserver
    {
        void OnRunStarted(int runId, IFlowNode root, FlowContext context);
        void OnRunFinished(int runId, FlowStatus status, FlowContext context);
        void OnStatusChanged(int runId, FlowStatus previous, FlowStatus next, FlowContext context);
        void OnNodeEnter(int runId, IFlowNode node, FlowContext context);
        void OnNodeTick(int runId, IFlowNode node, FlowContext context, float deltaTime, FlowStatus result, long elapsedTicks);
        void OnNodeExit(int runId, IFlowNode node, FlowContext context, FlowStatus status);
        void OnNodeInterrupt(int runId, IFlowNode node, FlowContext context, FlowStatus status);
        void OnUnhandledException(int runId, Exception exception, FlowContext context);
    }

    public interface IFlowTraceRecorder
    {
        bool IsEnabled { get; }
        void Record(in FlowTraceData data);
        IReadOnlyList<FlowTraceData> GetSnapshot();
        void Clear();
    }

    public sealed class NullFlowObserver : IFlowObserver
    {
        public static readonly NullFlowObserver Instance = new NullFlowObserver();

        private NullFlowObserver() { }

        public void OnRunStarted(int runId, IFlowNode root, FlowContext context) { }
        public void OnRunFinished(int runId, FlowStatus status, FlowContext context) { }
        public void OnStatusChanged(int runId, FlowStatus previous, FlowStatus next, FlowContext context) { }
        public void OnNodeEnter(int runId, IFlowNode node, FlowContext context) { }
        public void OnNodeTick(int runId, IFlowNode node, FlowContext context, float deltaTime, FlowStatus result, long elapsedTicks) { }
        public void OnNodeExit(int runId, IFlowNode node, FlowContext context, FlowStatus status) { }
        public void OnNodeInterrupt(int runId, IFlowNode node, FlowContext context, FlowStatus status) { }
        public void OnUnhandledException(int runId, Exception exception, FlowContext context) { }
    }

    public sealed class InMemoryFlowTraceRecorder : IFlowTraceRecorder
    {
        private readonly List<FlowTraceData> _records = new List<FlowTraceData>();
        private readonly int _capacity;

        public InMemoryFlowTraceRecorder(int capacity = 1024)
        {
            _capacity = capacity <= 0 ? 1024 : capacity;
        }

        public bool IsEnabled => true;

        public void Record(in FlowTraceData data)
        {
            if (_records.Count >= _capacity)
            {
                _records.RemoveAt(0);
            }

            _records.Add(data);
        }

        public IReadOnlyList<FlowTraceData> GetSnapshot()
        {
            return _records.ToArray();
        }

        public void Clear()
        {
            _records.Clear();
        }
    }

    public sealed class FlowStatistics
    {
        public int RunsStarted { get; internal set; }
        public int RunsFinished { get; internal set; }
        public int NodesEntered { get; internal set; }
        public int NodesTicked { get; internal set; }
        public int NodesExited { get; internal set; }
        public int NodesInterrupted { get; internal set; }
        public int UnhandledExceptions { get; internal set; }
        public long TotalNodeTickTicks { get; internal set; }
        public FlowStatus LastStatus { get; internal set; }

        public double AverageNodeTickTicks => NodesTicked > 0 ? (double)TotalNodeTickTicks / NodesTicked : 0d;

        public FlowStatistics Snapshot()
        {
            return new FlowStatistics
            {
                RunsStarted = RunsStarted,
                RunsFinished = RunsFinished,
                NodesEntered = NodesEntered,
                NodesTicked = NodesTicked,
                NodesExited = NodesExited,
                NodesInterrupted = NodesInterrupted,
                UnhandledExceptions = UnhandledExceptions,
                TotalNodeTickTicks = TotalNodeTickTicks,
                LastStatus = LastStatus
            };
        }
    }

    public sealed class FlowRuntimeDiagnostics
    {
        private static int s_nextRunId;
        private int _sequence;

        public FlowRuntimeDiagnostics(IFlowObserver observer, IFlowTraceRecorder traceRecorder)
        {
            RunId = ++s_nextRunId;
            Observer = observer ?? NullFlowObserver.Instance;
            TraceRecorder = traceRecorder;
            Statistics = new FlowStatistics();
        }

        public int RunId { get; }
        public IFlowObserver Observer { get; }
        public IFlowTraceRecorder TraceRecorder { get; }
        public FlowStatistics Statistics { get; }

        public void Record(FlowTraceEventType type, IFlowNode node, FlowStatus status, float deltaTime, long elapsedTicks, string message = null, Exception exception = null)
        {
            if (TraceRecorder == null || !TraceRecorder.IsEnabled) return;

            var nodeName = node != null ? node.GetType().Name : string.Empty;
            var data = new FlowTraceData(
                _sequence++,
                RunId,
                type,
                nodeName,
                status,
                deltaTime,
                MonotonicTime.GetTimestamp(),
                elapsedTicks,
                message,
                exception);

            TraceRecorder.Record(in data);
        }
    }

    public static class FlowDiagnostics
    {
        public static FlowRuntimeDiagnostics Get(FlowContext context)
        {
            if (context != null && context.TryGet<FlowRuntimeDiagnostics>(out var diagnostics))
            {
                return diagnostics;
            }

            return null;
        }

        /// <summary>
        /// 先记录进入诊断，再调用节点的进入回调。
        /// </summary>
        public static void Enter(FlowContext context, IFlowNode node)
        {
            RecordEnter(context, node);
            node.Enter(context);
        }

        internal static void RecordEnter(FlowContext context, IFlowNode node)
        {
            var diagnostics = Get(context);
            diagnostics?.Observer.OnNodeEnter(diagnostics.RunId, node, context);
            diagnostics?.Record(FlowTraceEventType.NodeEnter, node, FlowStatus.Running, 0f, 0L);
            if (diagnostics != null) diagnostics.Statistics.NodesEntered++;
        }

        /// <summary>
        /// 仅在实际调用进入回调前登记活动状态，以便清理部分进入的节点。
        /// </summary>
        internal static void EnterTracked(FlowContext context, IFlowNode node, ref bool active)
        {
            RecordEnter(context, node);
            active = true;
            node.Enter(context);
        }

        /// <summary>
        /// 先清除活动状态，再收尾；收尾抛错后不再尝试清理同一节点。
        /// </summary>
        internal static void CloseTracked(FlowContext context, IFlowNode node, ref bool active, bool interrupt, FlowStatus status)
        {
            if (!active) return;
            active = false;
            if (interrupt) Interrupt(context, node, status);
            else Exit(context, node, status);
        }

        public static FlowStatus Tick(FlowContext context, IFlowNode node, float deltaTime)
        {
            var start = MonotonicTime.GetTimestamp();
            var result = node.Tick(context, deltaTime);
            var elapsed = MonotonicTime.GetTimestamp() - start;
            var diagnostics = Get(context);
            if (diagnostics != null)
            {
                diagnostics.Statistics.NodesTicked++;
                diagnostics.Statistics.TotalNodeTickTicks += elapsed;
                diagnostics.Observer.OnNodeTick(diagnostics.RunId, node, context, deltaTime, result, elapsed);
                diagnostics.Record(FlowTraceEventType.NodeTick, node, result, deltaTime, elapsed);
            }

            return result;
        }

        /// <summary>
        /// 记录退出并调用节点清理；诊断失败也不跳过清理。
        /// </summary>
        public static void Exit(FlowContext context, IFlowNode node, FlowStatus status = FlowStatus.Succeeded)
        {
            try
            {
                var diagnostics = Get(context);
                diagnostics?.Observer.OnNodeExit(diagnostics.RunId, node, context, status);
                diagnostics?.Record(FlowTraceEventType.NodeExit, node, status, 0f, 0L);
                if (diagnostics != null) diagnostics.Statistics.NodesExited++;
            }
            finally
            {
                node.Exit(context);
            }
        }

        /// <summary>
        /// 记录中断并调用节点清理；诊断失败也不跳过清理。
        /// </summary>
        public static void Interrupt(FlowContext context, IFlowNode node, FlowStatus status = FlowStatus.Canceled)
        {
            try
            {
                var diagnostics = Get(context);
                diagnostics?.Observer.OnNodeInterrupt(diagnostics.RunId, node, context, status);
                diagnostics?.Record(FlowTraceEventType.NodeInterrupt, node, status, 0f, 0L);
                if (diagnostics != null) diagnostics.Statistics.NodesInterrupted++;
            }
            finally
            {
                node.Interrupt(context);
            }
        }
    }
}
