using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;

namespace AbilityKit.Flow.Tests;

/// <summary>
/// 记录生命周期调用的测试节点：可配置每次 Tick 返回的状态序列与副作用。
/// </summary>
internal sealed class RecordingNode : IFlowNode
{
    public int EnterCount;
    public int TickCount;
    public int ExitCount;
    public int InterruptCount;

    public readonly List<FlowStatus> TickResults = new List<FlowStatus>();
    public readonly List<float> TickDeltaTimes = new List<float>();

    private readonly Queue<FlowStatus> _pending = new Queue<FlowStatus>();

    public Action<FlowContext> OnEnter { get; set; }
    public Action<FlowContext, float> BeforeTick { get; set; }
    public Action<FlowContext> OnExit { get; set; }
    public Action<FlowContext> OnInterrupt { get; set; }

    public FlowContext LastEnterContext { get; private set; }
    public FlowContext LastExitContext { get; private set; }
    public FlowContext LastInterruptContext { get; private set; }

    /// <summary>本次 Tick 返回的状态；未配置时直接返回 Succeeded。</summary>
    public FlowStatus Result { get; set; } = FlowStatus.Succeeded;

    public RecordingNode()
    {
    }

    public RecordingNode(FlowStatus result)
    {
        Result = result;
    }

    public RecordingNode(params FlowStatus[] tickSequence)
    {
        foreach (var s in tickSequence) _pending.Enqueue(s);
    }

    public void Enter(FlowContext ctx)
    {
        EnterCount++;
        LastEnterContext = ctx;
        OnEnter?.Invoke(ctx);
    }

    public FlowStatus Tick(FlowContext ctx, float deltaTime)
    {
        TickCount++;
        TickDeltaTimes.Add(deltaTime);
        BeforeTick?.Invoke(ctx, deltaTime);

        if (_pending.Count > 0)
        {
            var s = _pending.Dequeue();
            TickResults.Add(s);
            return s;
        }

        TickResults.Add(Result);
        return Result;
    }

    public void Exit(FlowContext ctx)
    {
        ExitCount++;
        LastExitContext = ctx;
        OnExit?.Invoke(ctx);
    }

    public void Interrupt(FlowContext ctx)
    {
        InterruptCount++;
        LastInterruptContext = ctx;
        OnInterrupt?.Invoke(ctx);
    }
}

/// <summary>
/// 记录所有观测回调的 IFlowObserver，用于钉住生命周期回调顺序。
/// RunId 是进程级静态递增值，Calls 中不记录 RunId（单独放在 RunStartedIds）。
/// </summary>
internal sealed class RecordingObserver : IFlowObserver
{
    public readonly List<string> Calls = new List<string>();
    public readonly List<int> RunStartedIds = new List<int>();

    public void OnRunStarted(int runId, IFlowNode root, FlowContext context)
    {
        RunStartedIds.Add(runId);
        Calls.Add("RunStarted");
    }

    public void OnRunFinished(int runId, FlowStatus status, FlowContext context) => Calls.Add($"RunFinished:{status}");
    public void OnStatusChanged(int runId, FlowStatus previous, FlowStatus next, FlowContext context) => Calls.Add($"StatusChanged:{previous}->{next}");
    public void OnNodeEnter(int runId, IFlowNode node, FlowContext context) => Calls.Add($"Enter:{node.GetType().Name}");
    public void OnNodeTick(int runId, IFlowNode node, FlowContext context, float deltaTime, FlowStatus result, long elapsedTicks) => Calls.Add($"Tick:{node.GetType().Name}:{result}");
    public void OnNodeExit(int runId, IFlowNode node, FlowContext context, FlowStatus status) => Calls.Add($"Exit:{node.GetType().Name}:{status}");
    public void OnNodeInterrupt(int runId, IFlowNode node, FlowContext context, FlowStatus status) => Calls.Add($"Interrupt:{node.GetType().Name}:{status}");
    public void OnUnhandledException(int runId, Exception exception, FlowContext context) => Calls.Add($"Unhandled:{exception.GetType().Name}:{exception.Message}");
}

/// <summary>
/// 简单委托版根节点提供者，用于 FlowHost 测试。
/// </summary>
internal sealed class DelegateRootProvider<TArgs> : IFlowRootProvider<TArgs>
{
    private readonly Func<TArgs, IFlowNode> _factory;
    public int CreateCount;

    public DelegateRootProvider(Func<TArgs, IFlowNode> factory)
    {
        _factory = factory;
    }

    public IFlowNode CreateRoot(TArgs args)
    {
        CreateCount++;
        return _factory(args);
    }
}
