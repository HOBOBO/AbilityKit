using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 诊断图中的节点语义。该枚举只描述展示结构，不参与阶段调度。
    /// </summary>
    public enum EPipelineDebugNodeKind
    {
        Phase,
        Sequence,
        Parallel,
        Conditional,
        Gate,
        Composite
    }

    /// <summary>
    /// 诊断图中边的语义。
    /// </summary>
    public enum EPipelineDebugEdgeKind
    {
        Flow,
        Sequence,
        Parallel,
        Condition,
        Child
    }

    /// <summary>
    /// 单个阶段节点的运行状态。
    /// </summary>
    public enum EPipelineDebugExecutionState
    {
        Pending,
        Active,
        Completed,
        Skipped,
        Failed
    }

    /// <summary>
    /// 条件分支最近一次可观测的判断结果。
    /// </summary>
    public enum EPipelineDebugConditionResult
    {
        Unknown,
        Matched,
        Rejected
    }

    /// <summary>
    /// 管线启动时发送给可选诊断观察者的瞬时数据。
    /// </summary>
    public readonly struct PipelineRunStartedData
    {
        public PipelineRunStartedData(
            IPipelineLifeOwner owner,
            object pipeline,
            IAbilityPipelineConfig config,
            IPipelineRunControl run,
            IAbilityPipelineContext context)
        {
            Owner = owner;
            Pipeline = pipeline;
            Config = config;
            Run = run;
            Context = context;
            UtcTime = DateTime.UtcNow;
        }

        public IPipelineLifeOwner Owner { get; }
        public object Pipeline { get; }
        public IAbilityPipelineConfig Config { get; }
        public IPipelineRunControl Run { get; }
        public IAbilityPipelineContext Context { get; }
        public DateTime UtcTime { get; }
    }

    /// <summary>
    /// 管线释放上下文前发送的最终轻量快照。
    /// </summary>
    public readonly struct PipelineRunEndedData
    {
        public PipelineRunEndedData(IPipelineLifeOwner owner)
        {
            Owner = owner;
            State = owner.State;
            LastPhaseId = owner.CurrentPhaseId;
            UtcTime = DateTime.UtcNow;
        }

        public IPipelineLifeOwner Owner { get; }
        public EAbilityPipelineState State { get; }
        public AbilityPipelinePhaseId LastPhaseId { get; }
        public DateTime UtcTime { get; }
    }

    /// <summary>
    /// 供诊断工具读取的不可变阶段定义节点。
    /// </summary>
    public sealed class PipelinePhaseDebugNode
    {
        public PipelinePhaseDebugNode(
            AbilityPipelinePhaseId phaseId,
            string phaseType,
            IReadOnlyList<PipelinePhaseDebugNode>? children = null)
            : this(
                phaseId.ToString(),
                phaseId,
                phaseType,
                children != null && children.Count > 0 ? EPipelineDebugNodeKind.Composite : EPipelineDebugNodeKind.Phase,
                string.Empty,
                children)
        {
        }

        public PipelinePhaseDebugNode(
            string nodeKey,
            AbilityPipelinePhaseId phaseId,
            string phaseType,
            EPipelineDebugNodeKind kind,
            string summary,
            IReadOnlyList<PipelinePhaseDebugNode>? children = null)
        {
            NodeKey = nodeKey ?? string.Empty;
            PhaseId = phaseId;
            PhaseType = phaseType ?? string.Empty;
            Kind = kind;
            Summary = summary ?? string.Empty;
            Children = children ?? Array.Empty<PipelinePhaseDebugNode>();
        }

        public string NodeKey { get; }
        public AbilityPipelinePhaseId PhaseId { get; }
        public string PhaseType { get; }
        public EPipelineDebugNodeKind Kind { get; }
        public string Summary { get; }
        public IReadOnlyList<PipelinePhaseDebugNode> Children { get; }
        public bool IsComposite => Children.Count > 0;
    }

    /// <summary>
    /// 诊断图中的一条有向边。
    /// </summary>
    public sealed class PipelinePhaseDebugEdge
    {
        public PipelinePhaseDebugEdge(
            string sourceNodeKey,
            string targetNodeKey,
            EPipelineDebugEdgeKind kind,
            string label = "",
            int childIndex = -1)
        {
            SourceNodeKey = sourceNodeKey ?? string.Empty;
            TargetNodeKey = targetNodeKey ?? string.Empty;
            Kind = kind;
            Label = label ?? string.Empty;
            ChildIndex = childIndex;
        }

        public string SourceNodeKey { get; }
        public string TargetNodeKey { get; }
        public EPipelineDebugEdgeKind Kind { get; }
        public string Label { get; }
        public int ChildIndex { get; }
    }

    /// <summary>
    /// 一次 Pipeline 定义捕获形成的不可变诊断图。
    /// </summary>
    public sealed class PipelineDebugGraphSnapshot
    {
        public static readonly PipelineDebugGraphSnapshot Empty = new PipelineDebugGraphSnapshot(
            Array.Empty<PipelinePhaseDebugNode>(),
            Array.Empty<PipelinePhaseDebugEdge>(),
            string.Empty);

        public PipelineDebugGraphSnapshot(
            IReadOnlyList<PipelinePhaseDebugNode>? roots,
            IReadOnlyList<PipelinePhaseDebugEdge>? edges,
            string structureId)
        {
            Roots = roots ?? Array.Empty<PipelinePhaseDebugNode>();
            Edges = edges ?? Array.Empty<PipelinePhaseDebugEdge>();
            StructureId = structureId ?? string.Empty;
        }

        public IReadOnlyList<PipelinePhaseDebugNode> Roots { get; }
        public IReadOnlyList<PipelinePhaseDebugEdge> Edges { get; }
        public string StructureId { get; }
    }

    /// <summary>
    /// 单个图节点的可选布局坐标。坐标位于只读图的逻辑空间。
    /// </summary>
    public readonly struct PipelineDebugNodeLayout
    {
        public PipelineDebugNodeLayout(string nodeKey, float x, float y)
        {
            NodeKey = nodeKey ?? string.Empty;
            X = x;
            Y = y;
        }

        public string NodeKey { get; }
        public float X { get; }
        public float Y { get; }
    }

    /// <summary>
    /// 定义资产可选提供的图布局。StructureId 不匹配时 Editor 必须回退到自动布局。
    /// </summary>
    public sealed class PipelineDebugGraphLayout
    {
        public PipelineDebugGraphLayout(
            string structureId,
            IReadOnlyList<PipelineDebugNodeLayout>? nodes,
            string sourceName = "")
        {
            StructureId = structureId ?? string.Empty;
            Nodes = nodes ?? Array.Empty<PipelineDebugNodeLayout>();
            SourceName = sourceName ?? string.Empty;
        }

        public string StructureId { get; }
        public IReadOnlyList<PipelineDebugNodeLayout> Nodes { get; }
        public string SourceName { get; }
    }

    /// <summary>
    /// 单个阶段节点的运行状态快照。
    /// </summary>
    public sealed class PipelinePhaseDebugState
    {
        public PipelinePhaseDebugState(
            string nodeKey,
            EPipelineDebugExecutionState state,
            int selectedChildIndex = -1,
            IReadOnlyList<EPipelineDebugConditionResult>? childConditions = null)
        {
            NodeKey = nodeKey ?? string.Empty;
            State = state;
            SelectedChildIndex = selectedChildIndex;
            ChildConditions = childConditions ?? Array.Empty<EPipelineDebugConditionResult>();
        }

        public string NodeKey { get; }
        public EPipelineDebugExecutionState State { get; }
        public int SelectedChildIndex { get; }
        public IReadOnlyList<EPipelineDebugConditionResult> ChildConditions { get; }
    }

    /// <summary>
    /// 一次运行在捕获时刻的阶段状态集合。
    /// </summary>
    public sealed class PipelineDebugRunState
    {
        public static readonly PipelineDebugRunState Empty = new PipelineDebugRunState(Array.Empty<PipelinePhaseDebugState>());

        public PipelineDebugRunState(IReadOnlyList<PipelinePhaseDebugState>? nodes)
        {
            Nodes = nodes ?? Array.Empty<PipelinePhaseDebugState>();
        }

        public IReadOnlyList<PipelinePhaseDebugState> Nodes { get; }
    }

    /// <summary>
    /// 可选的管线定义观测协议。调用时才创建诊断结构，不参与正常执行。
    /// </summary>
    public interface IPipelineDebugStructureProvider
    {
        IReadOnlyList<PipelinePhaseDebugNode> CaptureDebugStructure();
    }

    /// <summary>
    /// 可选的完整图定义提供协议。实现应返回与当前执行定义一致的只读 DTO。
    /// </summary>
    public interface IPipelineDebugGraphProvider
    {
        PipelineDebugGraphSnapshot CaptureDebugGraph();
    }

    /// <summary>
    /// Pipeline、Config 或其 ScriptableObject 定义可选实现的布局提供协议。
    /// </summary>
    public interface IPipelineDebugGraphLayoutProvider
    {
        PipelineDebugGraphLayout CaptureDebugGraphLayout();
    }

    /// <summary>
    /// 一次运行可选实现的阶段状态捕获协议。仅在观察者主动读取时创建快照。
    /// </summary>
    public interface IPipelineDebugStateProvider
    {
        PipelineDebugRunState CaptureDebugState();
    }
}
