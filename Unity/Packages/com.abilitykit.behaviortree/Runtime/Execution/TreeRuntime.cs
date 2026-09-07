using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Nodes;
using Blackboard = AbilityKit.BehaviorTree.Blackboard.Blackboard;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Execution
{
    /// <summary>
    /// 行为树运行时：扁平化前序索引 + 运行+ 条件重评估（语义移植BTCore 已验证模型）    /// 全量确定性改造——节点随机流由种子派生并纳入快照，时钟由宿主tick 注入    /// 生命周期：Create（校实例Init> Enable -> Update* -> Restart / Dispose    /// </summary>
    public sealed class TreeRuntime : TreeDebugView, TreeDebugDeltaView, IDisposable
    {
        private sealed class ConditionalReevaluate
        {
            public int Index;
            public NodeState State;
            /// <summary>记录挂靠的中止组合节点（AbortType != None�?/summary>
            public int CompositeIndex;
            /// <summary>条件在中止组合节点下所属分支的相对子序号（LowerPriority 低优先级判定用）</summary>
            public int BranchIndex;

            public ConditionalReevaluate(int index, NodeState state, int compositeIndex, int branchIndex)
            {
                Index = index;
                State = state;
                CompositeIndex = compositeIndex;
                BranchIndex = branchIndex;
            }
        }

        private readonly TreeDefinition _definition;
        private readonly NodeRegistry _registry;
        private readonly TreeRunOptions _options;
        private readonly long _definitionHash;
        private readonly ExecutionContext _context;
        private readonly TreeTopology _topology;
        private readonly List<LifecycleExceptionRecord> _lifecycleExceptions = new();

        // 定义序节点实例与随机流（id 键控；扁平序Enable 时建立）
        private readonly Dictionary<string, NodeBase> _nodesById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DeterministicRandom> _randomsById = new(StringComparer.Ordinal);

        // 扁平化结构（Enable 时构建，Restart 不重建）
        private NodeBase[] _flatNodes = Array.Empty<NodeBase>();
        private NodeDefinition[] _flatDefinitions = Array.Empty<NodeDefinition>();
        private int[] _parentIndex = Array.Empty<int>();
        private int[][] _childrenIndex = Array.Empty<int[]>();       // Leaves have no children.
        private int[] _relativeChildIndex = Array.Empty<int>();
        private int[] _parentCompositeIndex = Array.Empty<int>();
        private int[][] _childConditionalIndex = Array.Empty<int[]>(); // 组合节点有效
        private NodeDebugStaticInfo[] _debugStaticInfos = Array.Empty<NodeDebugStaticInfo>();

        private readonly List<ConditionalReevaluate> _conditionalReevaluates = new();
        private readonly Dictionary<int, ConditionalReevaluate> _index2ConditionalReevaluate = new();

        // 运行栈列表；每栈为自底向顶的扁平索引序列
        private readonly List<List<int>> _runStacks = new();

        private bool _enabled;
        private int _lastFrame;
        private IReadOnlyDictionary<string, string>? _nodeSourceTree;
        private IReadOnlyDictionary<string, string>? _nodeSourceNode;
        private IReadOnlyList<SubtreeInstance> _subtreeInstances = Array.Empty<SubtreeInstance>();
        private NodeState _treeState = NodeState.Inactive;
        private int _preIndex = -1;
        private NodeState _preState = NodeState.Inactive;
        private AbilityKit.BehaviorTree.Diagnostics.DebugHandle? _debugHandle;
        private bool _disposed;
        private long _debugSequence;
        private long _lastDebugDeltaBaseSequence = -1;
        private NodeState[]? _lastDebugStates;
        private int[]? _lastDebugOnStack;
        private int[]? _lastDebugRunningChild;

        private sealed class NodeDebugStaticInfo
        {
            public string NodeId = "";
            public string Name = "";
            public string TypeId = "";
            public NodeKind Kind;
            public int Depth;
            public string? SourceTreeId;
        }

        /// <summary>当前运行定义的只读语义副本；修改返回对象不会影响运行中的实例</summary>
        public TreeDefinition Definition => _definition.DeepClone();
        public AbilityKit.BehaviorTree.Blackboard.Blackboard Blackboard => _context.Blackboard;
        public bool IsEnabled => _enabled;
        public NodeState TreeState => _treeState;
        public NodeState RootNodeState => _flatNodes.Length > 0 ? _flatNodes[0].State : NodeState.Inactive;
        public int NodeCount => _flatNodes.Length;
        public TreeTopology Topology => _topology;
        public IReadOnlyList<LifecycleExceptionRecord> LifecycleExceptions => _lifecycleExceptions;
        public LifecycleExceptionRecord? LastLifecycleException =>
            _lifecycleExceptions.Count == 0 ? null : _lifecycleExceptions[_lifecycleExceptions.Count - 1];
        /// <summary>子树展开后的节点来源树（nodeId -> treeId）；未用子树引用时为 null</summary>
        public IReadOnlyDictionary<string, string>? NodeSourceTree => _nodeSourceTree;

        /// <summary>子树展开后的节点来源 id（运nodeId -> authoring nodeId�?/summary>
        public IReadOnlyDictionary<string, string>? NodeSourceNode => _nodeSourceNode;

        /// <summary>子树实例（内联根 -> 被引treeId�?/summary>
        public IReadOnlyList<SubtreeInstance> SubtreeInstances => _subtreeInstances;

        private TreeRuntime(TreeDefinition definition, NodeRegistry registry, ServiceResolver services, TreeRunOptions? options)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            _definition = definition.DeepClone();
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _options = options ?? new TreeRunOptions();
            _definitionHash = _definition.ComputeDefinitionHash();

            var errors = TreeValidator.Validate(_definition, registry);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Invalid behavior tree definition '" + _definition.TreeId + "':\n" + string.Join("\n", errors));
            }
            _topology = TreeTopology.Compile(_definition, registry);

            _context = new ExecutionContext(AbilityKit.BehaviorTree.Blackboard.Blackboard.Create(_definition.Blackboard), services ?? new DefaultServiceResolver());

            foreach (var nodeDefinition in _definition.Nodes)
            {
                var node = registry.CreateNode(nodeDefinition.Type);
                node.NodeId = nodeDefinition.Id;
                var random = new DeterministicRandom(DeriveNodeSeed(_options.Seed, nodeDefinition.Id));
                var initContext = new NodeInitContext
                {
                    Tree = _definition,
                    Definition = nodeDefinition,
                    Properties = new PropertyReader(nodeDefinition.Properties),
                    ChildCount = nodeDefinition.ChildIds.Count,
                    Registry = registry,
                    Random = random,
                    Context = _context,
                };
                node.OnInit(in initContext);
                _nodesById.Add(nodeDefinition.Id, node);
                _randomsById.Add(nodeDefinition.Id, random);
            }

            BindTopology();
            RebuildDebugStaticInfos();

            if (!string.IsNullOrEmpty(_options.DebugName))
            {
                _debugHandle = DebugRegistry.Register(this);
            }
        }

        public static TreeRuntime Create(
            TreeDefinition definition,
            NodeRegistry registry,
            ServiceResolver? services = null,
            TreeRunOptions? options = null,
            TreeDefinitionResolver? subtreeResolver = null)
        {
            if (subtreeResolver != null)
            {
                var expansion = TreeCompiler.ExpandReferences(definition, subtreeResolver);
                var runtime = new TreeRuntime(expansion.Definition, registry, services, options)
                {
                    _nodeSourceTree = expansion.NodeSourceTree,
                    _nodeSourceNode = expansion.NodeSourceNode,
                    _subtreeInstances = expansion.SubtreeInstances,
                };
                runtime.RebuildDebugStaticInfos();
                return runtime;
            }
            return new TreeRuntime(definition, registry, services, options);
        }

        public void Dispose()
        {
            if (_disposed) return;
            Exception? stopError = null;
            try
            {
                DisableCore(NodeStopReason.Disposed);
            }
            catch (Exception ex)
            {
                stopError = ex;
            }
            finally
            {
                _disposed = true;
                if (_debugHandle != null)
                {
                    AbilityKit.BehaviorTree.Diagnostics.DebugRegistry.Unregister(_debugHandle);
                    _debugHandle = null;
                }
            }
            if (stopError != null) throw stopError;
        }

        private static ulong DeriveNodeSeed(ulong treeSeed, string nodeId)
        {
            var hash = (ulong)TreeDefinition.HashString(nodeId);
            return unchecked(treeSeed ^ (hash * 0x9E3779B97F4A7C15UL));
        }

        // ------------------------------------------------------------------
        // 启用与推        // ------------------------------------------------------------------

        /// <summary>构建执行拓扑并启动根节点。可重复调用（先重置全部运行态）</summary>
        public void Enable(int frame = 0, Fixed64? time = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TreeRuntime));
            if (_enabled) Disable();
            _context.BeginTick(frame, time ?? Fixed64.Zero);
            _lastFrame = frame;
            ResetState();

            _runStacks.Add(new List<int>());
            _enabled = true;
            try
            {
                PushNode(0, 0);
                MarkRuntimeChanged();
            }
            catch
            {
                try { DisableCore(NodeStopReason.EnableFailed); }
                catch (Exception) { /* Preserve the original OnStart failure. */ }
                throw;
            }
        }

        /// <summary>终止当前执行路径；所有运行节点按叶到根收到一OnStop</summary>
        public void Disable()
            => DisableCore(NodeStopReason.Disabled);

        private void DisableCore(NodeStopReason reason)
        {
            if (!_enabled) return;
            Exception? firstError = null;
            try
            {
                firstError = StopRunningNodes(reason);
            }
            finally
            {
                _enabled = false;
                ResetState();
                MarkRuntimeChanged();
            }
            if (firstError != null) throw firstError;
        }

        private Exception? StopRunningNodes(NodeStopReason reason)
        {
            Exception? firstError = null;
            var stopped = new HashSet<int>();
            for (var stackIndex = _runStacks.Count - 1; stackIndex >= 0; stackIndex--)
            {
                var stack = _runStacks[stackIndex];
                for (var index = stack.Count - 1; index >= 0; index--)
                {
                    var nodeIndex = stack[index];
                    if (!stopped.Add(nodeIndex)) continue;
                    firstError ??= StopNode(nodeIndex, reason);
                    _flatNodes[nodeIndex].State = NodeState.Inactive;
                }
            }
            return firstError;
        }

        private Exception? StopNode(int index, NodeStopReason reason)
        {
            try
            {
                _context.BeginStop(reason);
                _flatNodes[index].OnStop(_context);
                return null;
            }
            catch (Exception ex)
            {
                RecordLifecycleException(index, "OnStop", reason, ex);
                return _options.LifecycleExceptionPolicy == LifecycleExceptionPolicy.Throw ? ex : null;
            }
            finally
            {
                _context.EndStop();
            }
        }

        private void RecordLifecycleException(int index, string callback, NodeStopReason reason, Exception exception)
        {
            var nodeId = (uint)index < (uint)_flatDefinitions.Length ? _flatDefinitions[index].Id : "";
            _lifecycleExceptions.Add(new LifecycleExceptionRecord(nodeId, callback, reason, exception));
        }

        private void MarkRuntimeChanged()
        {
            _debugSequence++;
        }

        private void ResetState()
        {
            _treeState = NodeState.Inactive;
            _conditionalReevaluates.Clear();
            _index2ConditionalReevaluate.Clear();
            _runStacks.Clear();
            _preIndex = -1;
            _preState = NodeState.Inactive;

            foreach (var node in _nodesById.Values)
            {
                node.State = NodeState.Inactive;
            }
        }

        private void BindTopology()
        {
            _flatDefinitions = _topology.FlatDefinitions;
            _parentIndex = _topology.ParentIndex;
            _childrenIndex = _topology.ChildrenIndex;
            _relativeChildIndex = _topology.RelativeChildIndex;
            _parentCompositeIndex = _topology.ParentCompositeIndex;
            _flatNodes = new NodeBase[_flatDefinitions.Length];

            for (var i = 0; i < _flatDefinitions.Length; i++)
            {
                _flatNodes[i] = _nodesById[_flatDefinitions[i].Id];
            }

            _childConditionalIndex = new int[_flatNodes.Length][];
            for (var i = 0; i < _flatNodes.Length; i++)
            {
                if (_flatNodes[i] is not CompositeNode)
                {
                    _childConditionalIndex[i] = Array.Empty<int>();
                    continue;
                }

                var conditionalChildren = new List<int>();
                foreach (var childIndex in _childrenIndex[i])
                {
                    if (_flatNodes[childIndex] is ConditionNodeBase)
                    {
                        conditionalChildren.Add(childIndex);
                    }
                }
                _childConditionalIndex[i] = conditionalChildren.ToArray();
            }
        }

        private void RebuildDebugStaticInfos()
        {
            _debugStaticInfos = new NodeDebugStaticInfo[_flatDefinitions.Length];
            for (var i = 0; i < _flatDefinitions.Length; i++)
            {
                var definition = _flatDefinitions[i];
                var sourceTree = _nodeSourceTree != null
                    && _nodeSourceTree.TryGetValue(definition.Id, out var src)
                    ? src
                    : null;
                _registry.TryGetDescriptor(definition.Type, out var descriptor);
                _debugStaticInfos[i] = new NodeDebugStaticInfo
                {
                    NodeId = definition.Id,
                    Name = descriptor != null ? descriptor.DisplayName : definition.Type,
                    TypeId = definition.Type,
                    Kind = descriptor != null ? descriptor.Kind : NodeKind.Action,
                    Depth = _topology.Depth[i],
                    SourceTreeId = sourceTree,
                };
            }
        }

        private void Flatten()
        {
            var flatNodes = new List<NodeBase>(_definition.Nodes.Count);
            var flatDefinitions = new List<NodeDefinition>(_definition.Nodes.Count);
            var parentIndex = new List<int>(_definition.Nodes.Count);
            var relativeChildIndex = new List<int>(_definition.Nodes.Count);
            var parentCompositeIndex = new List<int>(_definition.Nodes.Count);
            var childrenIndex = new List<int[]>(_definition.Nodes.Count);

            Visit(
                _definition.RootNodeId,
                parent: -1,
                relativeIndex: -1,
                parentComposite: -1,
                flatNodes,
                flatDefinitions,
                parentIndex,
                relativeChildIndex,
                parentCompositeIndex,
                childrenIndex);

            _flatNodes = flatNodes.ToArray();
            _flatDefinitions = flatDefinitions.ToArray();
            _parentIndex = parentIndex.ToArray();
            _relativeChildIndex = relativeChildIndex.ToArray();
            _parentCompositeIndex = parentCompositeIndex.ToArray();
            _childrenIndex = childrenIndex.ToArray();
            _childConditionalIndex = new int[_flatNodes.Length][];

            for (var i = 0; i < _flatNodes.Length; i++)
            {
                if (_flatNodes[i] is not CompositeNode)
                {
                    _childConditionalIndex[i] = Array.Empty<int>();
                    continue;
                }

                var conditionalChildren = new List<int>();
                foreach (var childIndex in _childrenIndex[i])
                {
                    if (_flatNodes[childIndex] is ConditionNodeBase)
                    {
                        conditionalChildren.Add(childIndex);
                    }
                }
                _childConditionalIndex[i] = conditionalChildren.ToArray();
            }
        }

        private void Visit(
            string nodeId,
            int parent,
            int relativeIndex,
            int parentComposite,
            List<NodeBase> flatNodes,
            List<NodeDefinition> flatDefinitions,
            List<int> parentIndex,
            List<int> relativeChildIndex,
            List<int> parentCompositeIndex,
            List<int[]> childrenIndex)
        {
            var index = flatNodes.Count;
            flatNodes.Add(_nodesById[nodeId]);
            flatDefinitions.Add(FindDefinition(nodeId));
            parentIndex.Add(parent);
            relativeChildIndex.Add(relativeIndex);
            parentCompositeIndex.Add(parentComposite);

            var definition = flatDefinitions[index];
            var childIndexes = new int[definition.ChildIds.Count];
            childrenIndex.Add(childIndexes);

            for (var k = 0; k < definition.ChildIds.Count; k++)
            {
                // Child flat index equals the current number of flattened nodes.
                childIndexes[k] = flatNodes.Count;
                Visit(
                    definition.ChildIds[k],
                    index,
                    k,
                    flatNodes[index] is CompositeNode ? index : parentComposite,
                    flatNodes,
                    flatDefinitions,
                    parentIndex,
                    relativeChildIndex,
                    parentCompositeIndex,
                    childrenIndex);
            }
        }

        private NodeDefinition FindDefinition(string nodeId)
        {
            foreach (var node in _definition.Nodes)
            {
                if (string.Equals(node.Id, nodeId, StringComparison.Ordinal)) return node;
            }
            throw new InvalidOperationException($"BT node '{nodeId}' not found in definition.");
        }

        /// <summary>推进一tick。frame/time 由宿主注入，节点内禁止其他时间源</summary>
        public void Update(int frame, Fixed64 time)
        {
            if (!_enabled) return;
            _context.BeginTick(frame, time);
            _lastFrame = frame;

            ReevaluateConditionalNodes();

            for (var i = _runStacks.Count - 1; i >= 0; i--)
            {
                if (i >= _runStacks.Count) continue;
                var stack = _runStacks[i];
                _preIndex = -1;
                _preState = NodeState.Inactive;

                // A Running result stops additional same-frame advancement.
                // Parallel branch stacks can be removed while this loop runs.
                while (_preState != NodeState.Running && i < _runStacks.Count && stack.Count > 0)
                {
                    if (TryPreemptDecorators(i)) break;
                    var index = stack[stack.Count - 1];
                    if (_preIndex == index) break;

                    _preIndex = index;
                    _preState = RunNode(index, i, _preState);
                }
            }
            MarkRuntimeChanged();
        }

        /// <summary>根节点完成后重新进入（响应式决策循环）。未启用时无操作</summary>
        public void Restart()
        {
            if (!_enabled) return;
            var stopError = StopRunningNodes(NodeStopReason.Restarted);
            if (stopError != null) throw stopError;
            RemoveChildConditionalReevaluate(-1);
            _runStacks.Clear();
            _runStacks.Add(new List<int>());
            _treeState = NodeState.Inactive;
            PushNode(0, 0);
            MarkRuntimeChanged();
        }

        public bool TryGetNodeIndex(string nodeId, out int flatIndex)
            => _topology.TryGetNodeIndex(nodeId, out flatIndex);

        // ------------------------------------------------------------------
        // 核心执行（语义移植自 BTCore：运行栈 + 条件重评估）
        // ------------------------------------------------------------------

        private void PushNode(int index, int stackIndex)
        {
            var stack = _runStacks[stackIndex];
            if (stack.Count > 0 && stack[stack.Count - 1] == index) return;

            stack.Add(index);
            var node = _flatNodes[index];
            try
            {
                node.OnStart(_context);
            }
            catch (Exception ex)
            {
                RecordLifecycleException(index, "OnStart", NodeStopReason.None, ex);
                throw;
            }
            node.State = NodeState.Running;
        }

        private NodeState RunNode(int index, int stackIndex, NodeState preState)
        {
            PushNode(index, stackIndex);
            var node = _flatNodes[index];
            var state = preState;

            if (node is ParentNodeBase parentNode)
            {
                state = RunParentNode(index, stackIndex, state);
                state = parentNode.OverrideState(state);
            }
            else
            {
                state = node.OnTick(_context);
                node.State = state;
            }

            if (state != NodeState.Running)
            {
                state = PopNode(index, stackIndex, state, true, NodeStopReason.Completed);
            }

            return state;
        }

        private NodeState RunParentNode(int index, int stackIndex, NodeState preState)
        {
            var node = (ParentNodeBase)_flatNodes[index];

            // Running parallel nodes do not advance twice in the same tick.
            if (node.CanRunParallel() && node.OverrideState(NodeState.Running) == NodeState.Running)
            {
                return preState;
            }

            var childState = NodeState.Inactive;
            var preIndex = -1;
            while (node.CanExecute() && (childState != NodeState.Running || node.CanRunParallel()))
            {
                var childIndex = node.CurrentChildIndex;
                if (node.CanRunParallel())
                {
                    _runStacks.Add(new List<int>());
                    stackIndex = _runStacks.Count - 1;
                    node.OnChildStart();
                }

                // 可重复执行的装饰节点（Repeater/UntilSuccess 等）防同帧死循环
                var curIndex = childIndex;
                if (curIndex == preIndex)
                {
                    preState = NodeState.Running;
                    break;
                }

                // Entry gates such as cooldown can complete before starting a child.
                if (node is DecoratorNode gate && gate.TryTickOverride(_context, out var overridden))
                {
                    preState = PopNode(index, stackIndex, overridden);
                    break;
                }

                preIndex = curIndex;
                childState = preState = RunNode(_childrenIndex[index][childIndex], stackIndex, preState);
            }

            // A completed child can make CanExecute false; return the last child state.
            return preState;
        }

        private NodeState PopNode(
            int index,
            int stackIndex,
            NodeState state,
            bool popChildren = true,
            NodeStopReason stopReason = NodeStopReason.Completed)
        {
            var stack = _runStacks[stackIndex];
            if (stack.Count == 0 || stack[stack.Count - 1] != index)
            {
                throw new InvalidOperationException(
                    $"BT pop invariant violated: node {index} is not the top of stack {stackIndex}.");
            }
            stack.RemoveAt(stack.Count - 1);

            var node = _flatNodes[index];
            var stopError = StopNode(index, stopReason);
            if (stopError != null) throw stopError;
            node.State = state;

            var parentIndex = _parentIndex[index];
            if (parentIndex != -1)
            {
                // Completed condition nodes attach to the nearest aborting composite for reevaluation.
                if (node is ConditionNodeBase)
                {
                    var abortComposite = FindAbortComposite(index, out var branchIndex);
                    if (abortComposite != -1)
                    {
                        if (_index2ConditionalReevaluate.TryGetValue(index, out var reevaluate))
                        {
                            reevaluate.CompositeIndex = abortComposite;
                            reevaluate.BranchIndex = branchIndex;
                            reevaluate.State = state;
                        }
                        else
                        {
                            reevaluate = new ConditionalReevaluate(index, state, abortComposite, branchIndex);
                            _conditionalReevaluates.Add(reevaluate);
                            _index2ConditionalReevaluate.Add(index, reevaluate);
                        }
                    }
                }

                if (_flatNodes[parentIndex] is ParentNodeBase parent)
                {
                    parent.OnChildExecuted(_relativeChildIndex[index], state);
                }

                if (_flatNodes[parentIndex] is DecoratorNode decorator)
                {
                    state = decorator.Decorate(state);
                }
            }

            if (node is CompositeNode)
            {
                // 组合节点出栈（完成）：其子树的条件重评估记录随之失效
                RemoveChildConditionalReevaluate(index);
            }

            // Parallel branch exit cascades to descendant branch stacks.
            if (popChildren)
            {
                for (var i = _runStacks.Count - 1; i > stackIndex; i--)
                {
                    var backStack = _runStacks[i];
                    if (backStack.Count > 0 && IsParentNode(index, backStack[backStack.Count - 1]))
                    {
                        for (var j = backStack.Count - 1; j >= 0; j--)
                        {
                            PopNode(backStack[j], i, NodeState.Failure, false, NodeStopReason.Aborted);
                        }
                    }
                }
            }

            if (stack.Count > 0)
            {
                return state;
            }

            if (stackIndex == 0)
            {
                _treeState = state;
                if (_options.RestartWhenComplete)
                {
                    Restart();
                }
            }
            else
            {
                _runStacks.RemoveAt(stackIndex);
                state = NodeState.Running;
            }

            return state;
        }

        private void ReevaluateConditionalNodes()
        {
            for (var i = _conditionalReevaluates.Count - 1; i >= 0; i--)
            {
                var record = _conditionalReevaluates[i];
                if (record.CompositeIndex < 0) continue;
                if (_flatNodes[record.CompositeIndex] is not CompositeNode composite) continue;

                var conditionNode = _flatNodes[record.Index];
                var curState = conditionNode.OnTick(_context);
                conditionNode.State = curState;
                if (curState == record.State) continue;

                var shouldAbort = composite.AbortType switch
                {
                    AbortType.Self => true,
                    AbortType.LowerPriority => curState == NodeState.Success,
                    AbortType.Both => true,
                    _ => false,
                };
                record.State = curState;   // LowerPriority 翻假只更新基线、不中断

                if (!shouldAbort) continue;

                var runningBranch = FindRunningBranchUnder(record.CompositeIndex);
                if (runningBranch == -1) continue;
                // LowerPriority aborts only lower-priority running branches.
                // Self/Both abort the currently running descendant branch.
                if (composite.AbortType == AbortType.LowerPriority && runningBranch <= record.BranchIndex) continue;

                // Abort the running sibling branch and rewind the composite to the condition branch.
                AbortRunningBranch(record.CompositeIndex);
                composite.OnConditionalAbort(record.BranchIndex);
            }
        }

        /// <summary>
        /// 从条件节点沿"最近组合祖链向上，找到第一AbortType != None 的组合节点；
        /// 返回其扁平索引，并给出条件在该组合下所属分支的相对子序�?       /// </summary>
        private int FindAbortComposite(int conditionIndex, out int branchIndex)
        {
            var composite = _parentCompositeIndex[conditionIndex];
            while (composite != -1)
            {
                if (_flatNodes[composite] is CompositeNode node && node.AbortType != AbortType.None)
                {
                    var cur = conditionIndex;
                    while (_parentIndex[cur] != composite) cur = _parentIndex[cur];
                    branchIndex = _relativeChildIndex[cur];
                    return composite;
                }
                composite = _parentCompositeIndex[composite];
            }
            branchIndex = -1;
            return -1;
        }

        /// <summary>找到当前正在该组合节点下执行的分支（相对子序号）；无-1</summary>
        private int FindRunningBranchUnder(int compositeIndex)
        {
            for (var j = _runStacks.Count - 1; j >= 0; j--)
            {
                var stack = _runStacks[j];
                if (stack.Count == 0) continue;
                var cur = stack[stack.Count - 1];
                while (cur != -1 && _parentIndex[cur] != compositeIndex) cur = _parentIndex[cur];
                if (cur != -1) return _relativeChildIndex[cur];
            }
            return -1;
        }

        /// <summary>把该组合节点之上的运行分支自顶向下弹出（Stop 沿途），组合节点保留在栈上</summary>
        private void AbortRunningBranch(int compositeIndex)
        {
            for (var j = _runStacks.Count - 1; j >= 0; j--)
            {
                var stack = _runStacks[j];
                if (stack.Count == 0 || stack.IndexOf(compositeIndex) < 0) continue;

                while (stack.Count > 0 && stack[stack.Count - 1] != compositeIndex)
                {
                    PopNode(stack[stack.Count - 1], j, NodeState.Failure, true, NodeStopReason.Aborted);
                    if (j >= _runStacks.Count || !ReferenceEquals(_runStacks[j], stack)) return;
                }
                return;
            }
        }

        /// <summary>装饰器抢占：自栈顶向下找到第一个触TryTickOverride 的装饰器并中止其子树</summary>
        private bool TryPreemptDecorators(int stackIndex)
        {
            var stack = _runStacks[stackIndex];
            for (var j = stack.Count - 1; j >= 0; j--)
            {
                if (_flatNodes[stack[j]] is not DecoratorNode decorator) continue;
                if (!decorator.TryTickOverride(_context, out var state)) continue;

                while (stack.Count - 1 > j)
                {
                    PopNode(stack[stack.Count - 1], stackIndex, NodeState.Failure, true, NodeStopReason.Preempted);
                    if (stackIndex >= _runStacks.Count || !ReferenceEquals(_runStacks[stackIndex], stack)) return true;
                    if (stack.Count <= j) return true;
                }
                if (stack.Count > j)
                {
                    PopNode(stack[j], stackIndex, state, true, NodeStopReason.Completed);
                }
                return true;
            }
            return false;
        }

        private int FindCommonParentIndex(int conditionalIndex, int curNodeIndex)
        {
            var hashSet = new HashSet<int>();
            while (conditionalIndex != -1)
            {
                hashSet.Add(conditionalIndex);
                conditionalIndex = _parentIndex[conditionalIndex];
            }

            while (!hashSet.Contains(curNodeIndex))
            {
                curNodeIndex = _parentIndex[curNodeIndex];
            }

            return curNodeIndex;
        }

        private bool IsParentNode(int parentIndex, int childIndex)
        {
            for (var i = childIndex; i != -1; i = _parentIndex[i])
            {
                if (i == parentIndex) return true;
            }
            return false;
        }

        private void RemoveChildConditionalReevaluate(int index)
        {
            for (var i = _conditionalReevaluates.Count - 1; i >= 0; i--)
            {
                if (_conditionalReevaluates[i].CompositeIndex != index) continue;
                _index2ConditionalReevaluate.Remove(_conditionalReevaluates[i].Index);
                _conditionalReevaluates.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------
        // 快照与回        // ------------------------------------------------------------------

        public TreeRuntimeSnapshot CaptureState()
        {
            if (!_enabled)
                throw new InvalidOperationException("BT runtime must be enabled before capturing state.");

            var snapshot = new TreeRuntimeSnapshot
            {
                DefinitionHash = _definitionHash,
                Enabled = _enabled,
                TreeState = _treeState,
            };

            for (var i = 0; i < _flatNodes.Length; i++)
            {
                var node = _flatNodes[i];
                var nodeSnapshot = new NodeRuntimeSnapshot
                {
                    NodeId = _flatDefinitions[i].Id,
                    State = node.State,
                    RunningChildIndex = node is ParentNodeBase parent ? parent.CaptureRunningIndex() : -1,
                    CustomState = (node as NodeStateful)?.CaptureState(),
                };
                if (_randomsById.TryGetValue(_flatDefinitions[i].Id, out var random))
                {
                    random.CaptureState(out var s0, out var s1, out var sequence);
                    nodeSnapshot.RandomS0 = s0;
                    nodeSnapshot.RandomS1 = s1;
                    nodeSnapshot.RandomSequence = sequence;
                }
                snapshot.Nodes.Add(nodeSnapshot);
            }

            foreach (var stack in _runStacks)
            {
                snapshot.RunStacks.Add(new RunStackSnapshot { NodeIndexes = new List<int>(stack) });
            }

            foreach (var reevaluate in _conditionalReevaluates)
            {
                snapshot.ConditionalReevaluates.Add(new ConditionalReevaluateSnapshot
                {
                    Index = reevaluate.Index,
                    State = reevaluate.State,
                    CompositeIndex = reevaluate.CompositeIndex,
                    BranchIndex = reevaluate.BranchIndex,
                });
            }

            snapshot.Blackboard = _context.Blackboard.CaptureValues();
            return snapshot;
        }

        public void RestoreState(TreeRuntimeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!_enabled)
                throw new InvalidOperationException("BT runtime must be enabled before restoring state.");

            var migrated = RuntimeSnapshotMigrationRegistry.Global.MigrateToCurrent(snapshot);
            ValidateRestoreSnapshot(migrated);

            var previous = CaptureState();
            try
            {
                ApplyValidatedSnapshot(migrated);
            }
            catch
            {
                try { ApplyValidatedSnapshot(previous); }
                catch (Exception) { /* Preserve the restore failure; rollback is best effort for faulty custom nodes. */ }
                throw;
            }
            MarkRuntimeChanged();
        }

        private void ValidateRestoreSnapshot(TreeRuntimeSnapshot snapshot)
        {
            if (snapshot.SnapshotVersion != TreeRuntimeSnapshot.CurrentSnapshotVersion)
                throw new InvalidOperationException($"Unsupported BT runtime snapshot version '{snapshot.SnapshotVersion}'.");
            if (!snapshot.Enabled)
                throw new InvalidOperationException("BT runtime snapshot must represent an enabled tree.");
            if (snapshot.Nodes == null)
                throw new InvalidOperationException("BT runtime snapshot requires a non-null node list.");
            if (snapshot.RunStacks == null)
                throw new InvalidOperationException("BT runtime snapshot requires a non-null run-stack list.");
            if (snapshot.ConditionalReevaluates == null)
                throw new InvalidOperationException("BT runtime snapshot requires a non-null conditional reevaluate list.");
            if (snapshot.DefinitionHash != _definitionHash)
                throw new InvalidOperationException("BT runtime snapshot definition hash does not match the current tree.");
            if (snapshot.Nodes.Count != _flatNodes.Length)
                throw new InvalidOperationException("BT runtime snapshot node count does not match the enabled tree.");

            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                var nodeSnapshot = snapshot.Nodes[i]
                    ?? throw new InvalidOperationException("BT runtime snapshot contains a null node snapshot.");
                if (!string.Equals(nodeSnapshot.NodeId, _flatDefinitions[i].Id, StringComparison.Ordinal))
                    throw new InvalidOperationException("BT runtime snapshot node identity does not match the enabled tree.");
                ValidateNodeState(nodeSnapshot.State, "BT runtime snapshot contains an invalid node state.");
                if (_flatNodes[i] is ParentNodeBase)
                {
                    var childCount = _childrenIndex[i].Length;
                    if (nodeSnapshot.RunningChildIndex < -1 || nodeSnapshot.RunningChildIndex >= childCount)
                        throw new InvalidOperationException("BT runtime snapshot contains an invalid running child index.");
                }
                else if (nodeSnapshot.RunningChildIndex != -1)
                {
                    throw new InvalidOperationException("BT runtime snapshot contains running child state for a non-parent node.");
                }
                if (_flatNodes[i] is not NodeStateful && nodeSnapshot.CustomState != null)
                    throw new InvalidOperationException("BT runtime snapshot contains custom state for a stateless node.");
            }
            ValidateNodeState(snapshot.TreeState, "BT runtime snapshot contains an invalid tree state.");

            foreach (var stackSnapshot in snapshot.RunStacks)
            {
                if (stackSnapshot == null || stackSnapshot.NodeIndexes == null)
                    throw new InvalidOperationException("BT runtime snapshot contains a null run-stack.");
                var previousIndex = -1;
                foreach (var index in stackSnapshot.NodeIndexes)
                {
                    if (index < 0 || index >= _flatNodes.Length)
                        throw new InvalidOperationException("BT runtime snapshot contains an invalid run-stack node index.");
                    if (previousIndex != -1 && _parentIndex[index] != previousIndex)
                        throw new InvalidOperationException("BT runtime snapshot run-stack path is not parent-child contiguous.");
                    previousIndex = index;
                }
            }

            foreach (var item in snapshot.ConditionalReevaluates)
            {
                if (item == null)
                    throw new InvalidOperationException("BT runtime snapshot contains a null conditional reevaluate entry.");
                if (item.Index < 0 || item.Index >= _flatNodes.Length
                    || item.CompositeIndex < -1 || item.CompositeIndex >= _flatNodes.Length)
                    throw new InvalidOperationException("BT runtime snapshot contains an invalid conditional reevaluate index.");
                if (_flatNodes[item.Index] is not ConditionNodeBase)
                    throw new InvalidOperationException("BT runtime snapshot conditional reevaluate entry must reference a condition node.");
                if (item.CompositeIndex >= 0)
                {
                    if (_flatNodes[item.CompositeIndex] is not CompositeNode)
                        throw new InvalidOperationException("BT runtime snapshot conditional reevaluate entry must reference a composite node.");
                    if (item.BranchIndex < 0 || item.BranchIndex >= _childrenIndex[item.CompositeIndex].Length)
                        throw new InvalidOperationException("BT runtime snapshot contains an invalid conditional reevaluate branch index.");
                }
                ValidateNodeState(item.State, "BT runtime snapshot contains an invalid conditional reevaluate state.");
            }

            if (snapshot.Blackboard != null)
            {
                _context.Blackboard.ValidateValues(snapshot.Blackboard);
            }
        }

        private static void ValidateNodeState(NodeState state, string message)
        {
            if (state is not (NodeState.Inactive or NodeState.Running or NodeState.Success or NodeState.Failure))
                throw new InvalidOperationException(message);
        }

        private void ApplyValidatedSnapshot(TreeRuntimeSnapshot snapshot)
        {
            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                var nodeSnapshot = snapshot.Nodes[i];
                var node = _flatNodes[i];
                node.State = nodeSnapshot.State;
                if (node is ParentNodeBase parent)
                {
                    parent.RestoreRunningIndex(nodeSnapshot.RunningChildIndex);
                }
                if (node is NodeStateful stateful && nodeSnapshot.CustomState != null)
                {
                    stateful.RestoreState(nodeSnapshot.CustomState);
                }
                if (_randomsById.TryGetValue(_flatDefinitions[i].Id, out var random))
                {
                    random.RestoreState(nodeSnapshot.RandomS0, nodeSnapshot.RandomS1, nodeSnapshot.RandomSequence);
                }
            }

            _treeState = snapshot.TreeState;

            _runStacks.Clear();
            foreach (var stackSnapshot in snapshot.RunStacks)
            {
                _runStacks.Add(new List<int>(stackSnapshot.NodeIndexes));
            }

            _conditionalReevaluates.Clear();
            _index2ConditionalReevaluate.Clear();
            foreach (var item in snapshot.ConditionalReevaluates)
            {
                var reevaluate = new ConditionalReevaluate(item.Index, item.State, item.CompositeIndex, item.BranchIndex);
                _conditionalReevaluates.Add(reevaluate);
                _index2ConditionalReevaluate[item.Index] = reevaluate;
            }

            if (snapshot.Blackboard != null)
            {
                _context.Blackboard.RestoreValues(snapshot.Blackboard);
            }
        }

        // ------------------------------------------------------------------
        // IBtTreeDebugView（供编辑器拉取运行时调试状态）
        // ------------------------------------------------------------------

        string TreeDebugView.TreeId => _definition.TreeId;
        string TreeDebugView.DisplayName => _options.DebugName ?? _definition.TreeId;
        string TreeDebugView.OwnerLabel => _options.DebugOwnerLabel ?? "";

        int TreeDebugView.NodeCount => _flatNodes.Length;

        int TreeDebugView.LastFrame => _lastFrame;

        /// <summary>只读观察用途；观察端不得修改定义（实例节点已按它初始化�?/summary>
        TreeDefinition TreeDebugView.TreeDefinition => _definition.DeepClone();

        IReadOnlyDictionary<string, string>? TreeDebugView.NodeSourceTree => _nodeSourceTree;

        IReadOnlyDictionary<string, string>? TreeDebugView.NodeSourceNode => _nodeSourceNode;

        IReadOnlyList<SubtreeInstance> TreeDebugView.SubtreeInstances => _subtreeInstances;

        List<NodeDebugInfo> TreeDebugView.GetNodeStates()
            => BuildNodeDebugInfos(full: true, null, null, null);

        BlackboardValueSnapshot TreeDebugView.GetBlackboard() => _context.Blackboard.CaptureValues();

        long TreeDebugDeltaView.DebugSequence => _debugSequence;

        TreeDebugDelta TreeDebugDeltaView.CaptureDebugDelta(long knownSequence, bool includeBlackboard)
        {
            var full = knownSequence == 0 || knownSequence != _lastDebugDeltaBaseSequence;
            var nodes = BuildNodeDebugInfos(full, _lastDebugStates, _lastDebugOnStack, _lastDebugRunningChild);
            CaptureDebugDynamicState(out _lastDebugStates, out _lastDebugOnStack, out _lastDebugRunningChild);
            _lastDebugDeltaBaseSequence = _debugSequence;
            return new TreeDebugDelta
            {
                Sequence = _debugSequence,
                IsFull = full,
                LastFrame = _lastFrame,
                Nodes = nodes,
                Blackboard = includeBlackboard ? _context.Blackboard.CaptureValues() : null,
            };
        }

        private List<NodeDebugInfo> BuildNodeDebugInfos(
            bool full,
            NodeState[]? previousStates,
            int[]? previousOnStack,
            int[]? previousRunningChild)
        {
            CaptureDebugDynamicState(out var states, out var onStack, out var runningChild);
            var result = new List<NodeDebugInfo>(full ? _flatNodes.Length : 0);
            for (var i = 0; i < _flatNodes.Length; i++)
            {
                if (!full
                    && previousStates != null
                    && previousOnStack != null
                    && previousRunningChild != null
                    && previousStates.Length == _flatNodes.Length
                    && previousOnStack.Length == _flatNodes.Length
                    && previousRunningChild.Length == _flatNodes.Length
                    && previousStates[i] == states[i]
                    && previousOnStack[i] == onStack[i]
                    && previousRunningChild[i] == runningChild[i])
                {
                    continue;
                }

                var info = _debugStaticInfos[i];
                result.Add(new NodeDebugInfo(
                    info.NodeId,
                    info.Name,
                    info.TypeId,
                    info.Kind,
                    states[i],
                    info.Depth,
                    onStack[i],
                    runningChild[i],
                    info.SourceTreeId));
            }
            return result;
        }

        private void CaptureDebugDynamicState(
            out NodeState[] states,
            out int[] onStack,
            out int[] runningChild)
        {
            states = new NodeState[_flatNodes.Length];
            onStack = new int[_flatNodes.Length];
            runningChild = new int[_flatNodes.Length];
            for (var i = 0; i < runningChild.Length; i++) runningChild[i] = -1;

            foreach (var stack in _runStacks)
            {
                foreach (var index in stack)
                {
                    onStack[index]++;
                }
            }

            for (var i = 0; i < _flatNodes.Length; i++)
            {
                states[i] = _flatNodes[i].State;
                runningChild[i] = _flatNodes[i] is ParentNodeBase parent ? parent.CaptureRunningIndex() : -1;
            }
        }
    }
}
