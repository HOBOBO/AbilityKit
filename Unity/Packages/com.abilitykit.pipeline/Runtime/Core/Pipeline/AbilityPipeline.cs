using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Pipeline.Pooling;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 抽象核心管线流程。
    /// </summary>
    public abstract partial class AbilityPipeline<TCtx> : IAbilityPipeline<TCtx>, IPipelineDebugStructureProvider, IPipelineDebugGraphProvider
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 管线事件集合。
        /// </summary>
        public AbilityPipelineEvents<TCtx> Events { get; } = new AbilityPipelineEvents<TCtx>();

        /// <summary>
        /// 当前管线使用的运行时上下文。
        /// </summary>
        public PipelineRuntime Runtime { get; set; } = Pipeline.DefaultRuntime;

        private readonly List<IAbilityPipelinePhase<TCtx>> _phases = new List<IAbilityPipelinePhase<TCtx>>(8);

        /// <summary>
        /// 启动一次管线运行。
        /// </summary>
        public IAbilityPipelineRun<TCtx> Start(IAbilityPipelineConfig config, TCtx context)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var runPhases = AbilityPipelinePhaseRuntime.CreateRunPhases(_phases);
            return new Run(this, Runtime ?? Pipeline.DefaultRuntime, config, context, runPhases);
        }

        /// <summary>
        /// 重置管线内所有阶段运行态。
        /// </summary>
        public virtual void Reset()
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                _phases[i].Reset();
            }
        }

        /// <summary>
        /// 将阶段追加到管线末尾。
        /// </summary>
        public void AddPhase(IAbilityPipelinePhase<TCtx> phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Add(phase);
        }

        /// <summary>
        /// 将阶段插入到指定索引位置。
        /// </summary>
        public void InsertPhase(int index, IAbilityPipelinePhase<TCtx> phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Insert(index, phase);
        }

        /// <summary>
        /// 按阶段 ID 移除第一个匹配阶段。
        /// </summary>
        public void RemovePhase(AbilityPipelinePhaseId phaseId)
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                if (_phases[i].PhaseId == phaseId)
                {
                    _phases.RemoveAt(i);
                    return;
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<PipelinePhaseDebugNode> CaptureDebugStructure()
        {
            return CaptureDebugGraph().Roots;
        }

        /// <inheritdoc />
        public PipelineDebugGraphSnapshot CaptureDebugGraph()
        {
            var roots = new PipelinePhaseDebugNode[_phases.Count];
            var edges = new List<PipelinePhaseDebugEdge>(_phases.Count * 2);
            for (int i = 0; i < _phases.Count; i++)
            {
                roots[i] = CapturePhaseNode(_phases[i], i.ToString(), edges);
                if (i > 0)
                {
                    edges.Add(new PipelinePhaseDebugEdge(
                        roots[i - 1].NodeKey,
                        roots[i].NodeKey,
                        EPipelineDebugEdgeKind.Flow));
                }
            }
            return new PipelineDebugGraphSnapshot(roots, edges, ComputeStructureId(roots, edges));
        }

        private static PipelinePhaseDebugNode CapturePhaseNode(
            IAbilityPipelinePhase<TCtx> phase,
            string nodeKey,
            ICollection<PipelinePhaseDebugEdge> edges)
        {
            var subPhases = phase.SubPhases;
            int childCount = subPhases?.Count ?? 0;
            var children = childCount == 0
                ? Array.Empty<PipelinePhaseDebugNode>()
                : new PipelinePhaseDebugNode[childCount];
            EPipelineDebugNodeKind kind = GetDebugNodeKind(phase);

            for (int i = 0; i < childCount; i++)
            {
                string childKey = nodeKey + "/" + i;
                children[i] = CapturePhaseNode(subPhases![i], childKey, edges);
                edges.Add(new PipelinePhaseDebugEdge(
                    nodeKey,
                    childKey,
                    GetChildEdgeKind(kind),
                    GetChildEdgeLabel(phase, kind, i),
                    i));
            }

            return new PipelinePhaseDebugNode(
                nodeKey,
                phase.PhaseId,
                phase.GetType().Name,
                kind,
                GetDebugSummary(phase, kind),
                children);
        }

        private static EPipelineDebugNodeKind GetDebugNodeKind(IAbilityPipelinePhase<TCtx> phase)
        {
            if (phase is AbilityConditionalPhase<TCtx>) return EPipelineDebugNodeKind.Conditional;
            if (phase is AbilityParallelPhase<TCtx>) return EPipelineDebugNodeKind.Parallel;
            if (phase is AbilitySequencePhase<TCtx>) return EPipelineDebugNodeKind.Sequence;
            if (phase is AbilityGatePhase<TCtx>) return EPipelineDebugNodeKind.Gate;
            return phase.IsComposite ? EPipelineDebugNodeKind.Composite : EPipelineDebugNodeKind.Phase;
        }

        private static EPipelineDebugEdgeKind GetChildEdgeKind(EPipelineDebugNodeKind kind)
        {
            return kind switch
            {
                EPipelineDebugNodeKind.Sequence => EPipelineDebugEdgeKind.Sequence,
                EPipelineDebugNodeKind.Parallel => EPipelineDebugEdgeKind.Parallel,
                EPipelineDebugNodeKind.Conditional => EPipelineDebugEdgeKind.Condition,
                _ => EPipelineDebugEdgeKind.Child
            };
        }

        private static string GetChildEdgeLabel(
            IAbilityPipelinePhase<TCtx> phase,
            EPipelineDebugNodeKind kind,
            int childIndex)
        {
            if (kind == EPipelineDebugNodeKind.Sequence) return (childIndex + 1).ToString();
            if (kind == EPipelineDebugNodeKind.Parallel) return "Parallel";
            if (phase is AbilityConditionalPhase<TCtx> conditional && childIndex < conditional.Branches.Count)
            {
                var condition = conditional.Branches[childIndex].Condition;
                string name = GetDebugConditionName(condition);
                return name == "Else" ? name : name + " · " + condition.CheckStrategy;
            }
            return string.Empty;
        }

        private static string GetDebugConditionName(IAbilityConditionNode condition)
        {
            Type type = condition.GetType();
            if (type.DeclaringType == typeof(PipelineGraph) && type.Name == "AlwaysConditionNode") return "Else";

            try
            {
                string? displayName = condition.ToString();
                if (!string.IsNullOrWhiteSpace(displayName)
                    && displayName != type.FullName
                    && displayName != type.Name)
                {
                    return displayName;
                }
            }
            catch
            {
            }
            return type.Name;
        }

        private static string GetDebugSummary(IAbilityPipelinePhase<TCtx> phase, EPipelineDebugNodeKind kind)
        {
            return kind switch
            {
                EPipelineDebugNodeKind.Sequence => "Sequential children",
                EPipelineDebugNodeKind.Parallel => "Concurrent children",
                EPipelineDebugNodeKind.Conditional when phase is AbilityConditionalPhase<TCtx> conditional =>
                    "No match: " + conditional.NoConditionBehavior,
                EPipelineDebugNodeKind.Gate when phase is AbilityGatePhase<TCtx> gate =>
                    GetDebugConditionName(gate.Condition) + " · " + gate.Condition.CheckStrategy,
                EPipelineDebugNodeKind.Composite => "Composite phase",
                _ => string.Empty
            };
        }

        private static string ComputeStructureId(
            IReadOnlyList<PipelinePhaseDebugNode> roots,
            IReadOnlyList<PipelinePhaseDebugEdge> edges)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < roots.Count; i++) AppendNodeHash(roots[i], ref hash);
            for (int i = 0; i < edges.Count; i++)
            {
                AppendHash(edges[i].SourceNodeKey, ref hash);
                AppendHash(edges[i].TargetNodeKey, ref hash);
                AppendHash(edges[i].Kind.ToString(), ref hash);
                AppendHash(edges[i].Label, ref hash);
            }
            return hash.ToString("X16");
        }

        private static void AppendNodeHash(PipelinePhaseDebugNode node, ref ulong hash)
        {
            AppendHash(node.NodeKey, ref hash);
            AppendHash(node.PhaseId.ToString(), ref hash);
            AppendHash(node.PhaseType, ref hash);
            AppendHash(node.Kind.ToString(), ref hash);
            for (int i = 0; i < node.Children.Count; i++) AppendNodeHash(node.Children[i], ref hash);
        }

        private static void AppendHash(string value, ref ulong hash)
        {
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 1099511628211UL;
            }
            hash ^= 0xff;
            hash *= 1099511628211UL;
        }

        /// <summary>
        /// 释放管线运行绑定的上下文。
        /// </summary>
        protected abstract void ReleaseContext(TCtx context);

        private sealed class Run : IAbilityPipelineRun<TCtx>, IPipelineLifeOwner, IPipelineDebugStateProvider
        {
            private readonly AbilityPipeline<TCtx> _owner;
            private readonly PipelineRuntime _runtime;
            private readonly IAbilityPipelineConfig _config;
            private readonly List<IAbilityPipelinePhase<TCtx>> _phases;
            private readonly int _ownerId;

            private bool _isCancelled;
            private int _currentPhaseIndex;
            private IAbilityPipelinePhase<TCtx>? _currentPhase;
            private IAbilityPipelinePhase<TCtx>? _failedPhase;
            private PipelineDebugRunState? _terminalDebugState;

            public EAbilityPipelineState State { get; private set; }

            public TCtx Context { get; }

            public AbilityPipelinePhaseId CurrentPhaseId => Context.CurrentPhaseId;

            public bool IsPaused { get; private set; }

            public PipelineDebugRunState CaptureDebugState()
            {
                return _terminalDebugState ?? CaptureDebugStateCore();
            }

            private PipelineDebugRunState CaptureDebugStateCore()
            {
                var result = new List<PipelinePhaseDebugState>(_phases.Count * 2);
                for (int i = 0; i < _phases.Count; i++)
                {
                    EPipelineDebugExecutionState state;
                    if (ReferenceEquals(_phases[i], _failedPhase)) state = EPipelineDebugExecutionState.Failed;
                    else if (i < _currentPhaseIndex || State == EAbilityPipelineState.Completed) state = EPipelineDebugExecutionState.Completed;
                    else if (ReferenceEquals(_phases[i], _currentPhase)) state = EPipelineDebugExecutionState.Active;
                    else state = EPipelineDebugExecutionState.Pending;
                    CapturePhaseState(_phases[i], i.ToString(), state, result);
                }
                return new PipelineDebugRunState(result);
            }

            private static void CapturePhaseState(
                IAbilityPipelinePhase<TCtx> phase,
                string nodeKey,
                EPipelineDebugExecutionState state,
                ICollection<PipelinePhaseDebugState> result)
            {
                int selectedChild = GetSelectedChildIndex(phase);
                IReadOnlyList<EPipelineDebugConditionResult> conditions = Array.Empty<EPipelineDebugConditionResult>();
                if (phase is AbilityConditionalPhase<TCtx> conditional)
                {
                    var copy = new EPipelineDebugConditionResult[conditional.DebugBranchResults.Count];
                    for (int i = 0; i < copy.Length; i++) copy[i] = conditional.DebugBranchResults[i];
                    conditions = copy;
                }

                result.Add(new PipelinePhaseDebugState(nodeKey, state, selectedChild, conditions));

                var children = phase.SubPhases;
                if (children == null) return;
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    EPipelineDebugExecutionState childState;
                    if (state == EPipelineDebugExecutionState.Pending)
                    {
                        childState = EPipelineDebugExecutionState.Pending;
                    }
                    else if (state == EPipelineDebugExecutionState.Skipped)
                    {
                        childState = EPipelineDebugExecutionState.Skipped;
                    }
                    else if (child.IsComplete)
                    {
                        childState = EPipelineDebugExecutionState.Completed;
                    }
                    else if (IsChildActive(phase, i))
                    {
                        childState = state == EPipelineDebugExecutionState.Failed
                            ? EPipelineDebugExecutionState.Failed
                            : EPipelineDebugExecutionState.Active;
                    }
                    else if (state == EPipelineDebugExecutionState.Completed
                             || IsRejectedConditionalChild(phase, i))
                    {
                        childState = EPipelineDebugExecutionState.Skipped;
                    }
                    else
                    {
                        childState = EPipelineDebugExecutionState.Pending;
                    }

                    CapturePhaseState(child, nodeKey + "/" + i, childState, result);
                }
            }

            private static int GetSelectedChildIndex(IAbilityPipelinePhase<TCtx> phase)
            {
                if (phase is AbilityConditionalPhase<TCtx> conditional) return conditional.DebugCurrentBranchIndex;
                if (phase is AbilityCompositePhase<TCtx> composite) return composite.DebugCurrentSubPhaseIndex;
                return -1;
            }

            private static bool IsChildActive(IAbilityPipelinePhase<TCtx> phase, int childIndex)
            {
                if (phase is AbilityConditionalPhase<TCtx> conditional)
                {
                    return conditional.DebugCurrentBranchIndex == childIndex;
                }
                if (phase is AbilityParallelPhase<TCtx> parallel)
                {
                    return parallel.DebugIsChildActive(childIndex);
                }
                return phase is AbilityCompositePhase<TCtx> composite
                    && composite.DebugCurrentSubPhaseIndex == childIndex;
            }

            private static bool IsRejectedConditionalChild(IAbilityPipelinePhase<TCtx> phase, int childIndex)
            {
                return phase is AbilityConditionalPhase<TCtx> conditional
                    && childIndex < conditional.DebugBranchResults.Count
                    && conditional.DebugBranchResults[childIndex] == EPipelineDebugConditionResult.Rejected;
            }

            private readonly AbilityPipelinePhaseId[] _activePhaseIds = new AbilityPipelinePhaseId[1];

            IReadOnlyList<AbilityPipelinePhaseId> IPipelineLifeOwner.ActivePhases
            {
                get
                {
                    if (_currentPhase == null)
                    {
                        return Array.Empty<AbilityPipelinePhaseId>();
                    }

                    _activePhaseIds[0] = _currentPhase.PhaseId;
                    return _activePhaseIds;
                }
            }

            // 管线生命周期拥有者接口实现
            int IPipelineLifeOwner.OwnerId => _ownerId;
            string IPipelineLifeOwner.OwnerName => _owner.GetType().Name + "#" + _ownerId;

            public Run(AbilityPipeline<TCtx> owner, PipelineRuntime runtime, IAbilityPipelineConfig config, TCtx context, List<IAbilityPipelinePhase<TCtx>> phases)
            {
                _owner = owner;
                _runtime = runtime;
                _config = config;
                Context = context;
                _phases = phases;
                _ownerId = PipelineRunIdGenerator.Next();

                State = EAbilityPipelineState.Executing;
                IsPaused = false;
                _currentPhaseIndex = 0;
                _currentPhase = null;

                Context.PipelineState = EAbilityPipelineState.Executing;

                _owner.Events?.OnPipelineStart?.Invoke(Context);

                _runtime.Registry.Register(this);
                PipelineDebugHooks.NotifyRunStarted<TCtx>(this, _owner, _config, this);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.RunStart, default, State, string.Empty);
            }

            public void Tick(float deltaTime)
            {
                if (State != EAbilityPipelineState.Executing) return;
                if (_isCancelled)
                {
                    Fail();
                    return;
                }
                if (IsPaused) return;
                if (Context.IsAborted)
                {
                    Fail();
                    return;
                }

                try
                {
                    if (_currentPhase != null)
                    {
                        _currentPhase.OnUpdate(Context, deltaTime);
                        if (Context.IsAborted)
                        {
                            Fail();
                            return;
                        }

                        if (!_currentPhase.IsComplete)
                        {
                            return;
                        }

                        OnPhaseComplete(_currentPhase);
                        _currentPhase = null;
                        _currentPhaseIndex++;
                    }

                    ExecutePipeline();

                    if (Context.IsAborted)
                    {
                        Fail();
                    }
                }
                catch (Exception e)
                {
                    HandlePhaseError(_currentPhase, e);
                }

                _owner.Events?.OnTick?.Invoke(Context, deltaTime, State);
            }

            public void Pause()
            {
                if (State != EAbilityPipelineState.Executing) return;
                if (IsPaused) return;
                IsPaused = true;
                Context.IsPaused = true;
                _owner.Events?.OnPipelinePause?.Invoke(Context);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.Pause, CurrentPhaseId, State, string.Empty);
            }

            public void Resume()
            {
                if (State != EAbilityPipelineState.Executing) return;
                if (!IsPaused) return;
                IsPaused = false;
                Context.IsPaused = false;
                _owner.Events?.OnPipelineResume?.Invoke(Context);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.Resume, CurrentPhaseId, State, string.Empty);
            }

            public void Interrupt()
            {
                if (State != EAbilityPipelineState.Executing) return;

                if (_currentPhase is IInterruptiblePhase<TCtx> interruptible)
                {
                    interruptible.OnInterrupt(Context);
                }

                InterruptSubPhases(_currentPhase);

                Context.IsAborted = true;
                _owner.Events?.OnPipelineInterrupt?.Invoke(Context, true);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.Interrupt, CurrentPhaseId, State, string.Empty);
                Fail();
            }

            public void Cancel()
            {
                _isCancelled = true;
            }

            private void ExecutePipeline()
            {
                while (_currentPhaseIndex < _phases.Count && State == EAbilityPipelineState.Executing)
                {
                    if (Context.IsAborted)
                    {
                        Fail();
                        return;
                    }

                    var phase = _phases[_currentPhaseIndex];

                    if (!phase.ShouldExecute(Context))
                    {
                        _currentPhaseIndex++;
                        continue;
                    }

                    try
                    {
                        ExecutePhase(phase);

                        if (Context.IsAborted)
                        {
                            Fail();
                            return;
                        }

                        if (!phase.IsComplete)
                        {
                            _currentPhase = phase;
                            return;
                        }

                        OnPhaseComplete(phase);
                        _currentPhaseIndex++;
                    }
                    catch (Exception e)
                    {
                        HandlePhaseError(phase, e);
                        return;
                    }
                }

                if (_currentPhaseIndex >= _phases.Count)
                {
                    Complete();
                }
            }

            private void ExecutePhase(IAbilityPipelinePhase<TCtx> phase)
            {
                OnPhaseStart(phase);
                phase.Execute(Context);
            }

            private void OnPhaseStart(IAbilityPipelinePhase<TCtx> phase)
            {
                Context.CurrentPhaseId = phase.PhaseId;
                _owner.ExecuteExtensionPhaseStart(phase.PhaseId, Context, phase);
                _owner.Events?.OnPhaseStart?.Invoke(phase, Context);
                _owner.Events?.RecordTracePhase(_runtime, this, EPipelineTraceEventType.PhaseStart, phase.PhaseId, phase.GetType().Name, State);
            }

            private void OnPhaseComplete(IAbilityPipelinePhase<TCtx> phase)
            {
                _owner.ExecuteExtensionPhaseComplete(phase.PhaseId, Context, phase);
                _owner.Events?.OnPhaseComplete?.Invoke(phase, Context);
                _owner.Events?.RecordTracePhase(_runtime, this, EPipelineTraceEventType.PhaseComplete, phase.PhaseId, phase.GetType().Name, State);
            }

            private void HandlePhaseError(IAbilityPipelinePhase<TCtx>? phase, Exception e)
            {
                if (State != EAbilityPipelineState.Executing) return;
                _failedPhase = phase;
                State = EAbilityPipelineState.Failed;
                Context.PipelineState = EAbilityPipelineState.Failed;
                Exception failure = e;

                if (phase != null)
                {
                    try { phase.HandleError(Context, e); }
                    catch (Exception handlerException)
                    {
                        failure = new AggregateException(
                            "The phase and its error handler both failed.",
                            e,
                            handlerException);
                    }
                    _owner.Events?.OnPhaseError?.Invoke(phase, Context, failure);
                }

                TrySetContextFailReason(phase, failure);
                _owner.Events?.OnPipelineError?.Invoke(Context, failure);
                _owner.Events?.OnPipelineFailed?.Invoke(Context, failure);
                _owner.Events?.RecordTracePhase(
                    _runtime,
                    this,
                    EPipelineTraceEventType.PhaseError,
                    phase?.PhaseId ?? default,
                    failure.Message,
                    State);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.RunEnd, CurrentPhaseId, State, failure.Message);

                Cleanup();
            }

            private void TrySetContextFailReason(IAbilityPipelinePhase<TCtx>? phase, Exception e)
            {
                if (Context == null || e == null) return;

                try
                {
                    var phaseName = phase?.GetType().Name ?? "<unknown-phase>";
                    var phaseId = phase?.PhaseId.ToString() ?? "<unknown-phase-id>";
                    var message = $"{phaseName} failed (phaseId={phaseId}): {e.GetType().Name}: {e.Message}";
                    Context.SetData("FailReason", message);

                    var failReasonProperty = Context.GetType().GetProperty("FailReason");
                    if (failReasonProperty != null && failReasonProperty.CanWrite && failReasonProperty.PropertyType == typeof(string))
                    {
                        failReasonProperty.SetValue(Context, message);
                    }
                }
                catch (Exception cleanupException)
                {
                    Log.Exception(cleanupException, $"Pipeline context release failed. pipeline={_owner.GetType().Name} state={State}");
                }
            }

            private void Complete()
            {
                if (State != EAbilityPipelineState.Executing) return;
                State = EAbilityPipelineState.Completed;
                Context.PipelineState = EAbilityPipelineState.Completed;
                _owner.Events?.OnPipelineComplete?.Invoke(Context);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.RunEnd, CurrentPhaseId, State, "Completed");

                Cleanup();
            }

            private void Fail()
            {
                if (State != EAbilityPipelineState.Executing) return;
                State = EAbilityPipelineState.Failed;
                Context.PipelineState = EAbilityPipelineState.Failed;

                var failure = CreatePipelineFailureException();
                _owner.Events?.OnPipelineError?.Invoke(Context, null);
                _owner.Events?.OnPipelineFailed?.Invoke(Context, failure);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.RunEnd, CurrentPhaseId, State, failure.Message);

                Cleanup();
            }

            private Exception CreatePipelineFailureException()
            {
                if (Context != null && Context.TryGetData<string>("FailReason", out var failReason) && !string.IsNullOrEmpty(failReason))
                {
                    return new InvalidOperationException(failReason);
                }

                var phaseId = CurrentPhaseId.ToString();
                return new InvalidOperationException($"Pipeline failed without exception (state={State}, phaseId={phaseId}).");
            }

            private void InterruptSubPhases(IAbilityPipelinePhase<TCtx>? phase)
            {
                if (phase == null) return;

                var subPhases = phase.SubPhases;
                for (int i = 0; i < subPhases.Count; i++)
                {
                    if (subPhases[i] is IInterruptiblePhase<TCtx> interruptible)
                    {
                        interruptible.OnInterrupt(Context);
                    }
                }
            }

            private void Cleanup()
            {
                _terminalDebugState = CaptureDebugStateCore();
                PipelineDebugHooks.NotifyRunEnded(this);
                try
                {
                    _owner.ReleaseContext(Context);
                }
                catch
                {
                }
                finally
                {
                    _runtime.Registry.Unregister(this);
                    PipelinePools.ReleaseRunPhaseList(_phases);
                }
            }
        }
    }
}
