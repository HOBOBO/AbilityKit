using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 行为树运行时：扁平化前序索引 + 运行栈 + 条件重评估（语义移植自 BTCore 已验证模型），
    /// 全量确定性改造——节点随机流由种子派生并纳入快照，时钟由宿主每 tick 注入。
    /// 生命周期：Create（校验+实例化+Init）-> Enable -> Update* -> Restart / Dispose。
    /// </summary>
    public sealed class BtTreeRuntime : IBtTreeDebugView, IDisposable
    {
        private sealed class ConditionalReevaluate
        {
            public int Index;
            public BtNodeState State;
            /// <summary>记录挂靠的中止组合节点（AbortType != None）。</summary>
            public int CompositeIndex;
            /// <summary>条件在中止组合节点下所属分支的相对子序号（LowerPriority 高/低优先级判定用）。</summary>
            public int BranchIndex;

            public ConditionalReevaluate(int index, BtNodeState state, int compositeIndex, int branchIndex)
            {
                Index = index;
                State = state;
                CompositeIndex = compositeIndex;
                BranchIndex = branchIndex;
            }
        }

        private readonly BtTreeDefinition _definition;
        private readonly BtNodeRegistry _registry;
        private readonly BtTreeRunOptions _options;
        private readonly long _definitionHash;
        private readonly BtExecutionContext _context;

        // 定义序节点实例与随机流（id 键控；扁平序在 Enable 时建立）
        private readonly Dictionary<string, BtNodeBase> _nodesById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DeterministicRandom> _randomsById = new(StringComparer.Ordinal);

        // 扁平化结构（Enable 时构建，Restart 不重建）
        private BtNodeBase[] _flatNodes = Array.Empty<BtNodeBase>();
        private BtNodeDefinition[] _flatDefinitions = Array.Empty<BtNodeDefinition>();
        private int[] _parentIndex = Array.Empty<int>();
        private int[][] _childrenIndex = Array.Empty<int[]>();       // 叶子为 null
        private int[] _relativeChildIndex = Array.Empty<int>();
        private int[] _parentCompositeIndex = Array.Empty<int>();
        private int[][] _childConditionalIndex = Array.Empty<int[]>(); // 组合节点有效

        private readonly List<ConditionalReevaluate> _conditionalReevaluates = new();
        private readonly Dictionary<int, ConditionalReevaluate> _index2ConditionalReevaluate = new();

        // 运行栈列表；每栈为自底向顶的扁平索引序列
        private readonly List<List<int>> _runStacks = new();

        private bool _enabled;
        private int _lastFrame;
        private IReadOnlyDictionary<string, string>? _nodeSourceTree;
        private IReadOnlyList<BtSubtreeInstance> _subtreeInstances = Array.Empty<BtSubtreeInstance>();
        private BtNodeState _treeState = BtNodeState.Inactive;
        private int _preIndex = -1;
        private BtNodeState _preState = BtNodeState.Inactive;
        private BtTreeDebugHandle? _debugHandle;

        public BtTreeDefinition Definition => _definition;
        public BtBlackboard Blackboard => _context.Blackboard;
        public bool IsEnabled => _enabled;
        public BtNodeState TreeState => _treeState;
        public BtNodeState RootNodeState => _flatNodes.Length > 0 ? _flatNodes[0].State : BtNodeState.Inactive;
        public int NodeCount => _flatNodes.Length;
        /// <summary>子树展开后的节点来源树（nodeId -> treeId）；未用子树引用时为 null。</summary>
        public IReadOnlyDictionary<string, string>? NodeSourceTree => _nodeSourceTree;

        /// <summary>子树实例（内联根 -> 被引用 treeId）。</summary>
        public IReadOnlyList<BtSubtreeInstance> SubtreeInstances => _subtreeInstances;

        private BtTreeRuntime(BtTreeDefinition definition, BtNodeRegistry registry, IBtServiceResolver services, BtTreeRunOptions? options)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _options = options ?? new BtTreeRunOptions();
            _definitionHash = definition.ComputeDefinitionHash();

            var errors = BtTreeValidator.Validate(definition, registry);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Invalid behavior tree definition '" + definition.TreeId + "':\n" + string.Join("\n", errors));
            }

            _context = new BtExecutionContext(BtBlackboard.Create(definition.Blackboard), services ?? new BtServiceResolver());

            foreach (var nodeDefinition in definition.Nodes)
            {
                var node = registry.CreateNode(nodeDefinition.Type);
                node.NodeId = nodeDefinition.Id;
                var random = new DeterministicRandom(DeriveNodeSeed(_options.Seed, nodeDefinition.Id));
                var initContext = new BtNodeInitContext
                {
                    Tree = definition,
                    Definition = nodeDefinition,
                    Properties = new BtPropertyReader(nodeDefinition.Properties),
                    ChildCount = nodeDefinition.ChildIds.Count,
                    Registry = registry,
                    Random = random,
                    Context = _context,
                };
                node.OnInit(in initContext);
                _nodesById.Add(nodeDefinition.Id, node);
                _randomsById.Add(nodeDefinition.Id, random);
            }

            if (!string.IsNullOrEmpty(_options.DebugName))
            {
                _debugHandle = BtDebugRegistry.Register(this);
            }
        }

        public static BtTreeRuntime Create(
            BtTreeDefinition definition,
            BtNodeRegistry registry,
            IBtServiceResolver? services = null,
            BtTreeRunOptions? options = null,
            IBtTreeDefinitionResolver? subtreeResolver = null)
        {
            if (subtreeResolver != null)
            {
                var expansion = BtTreeCompiler.ExpandReferences(definition, subtreeResolver);
                return new BtTreeRuntime(expansion.Definition, registry, services, options)
                {
                    _nodeSourceTree = expansion.NodeSourceTree,
                    _subtreeInstances = expansion.SubtreeInstances,
                };
            }
            return new BtTreeRuntime(definition, registry, services, options);
        }

        public void Dispose()
        {
            if (_debugHandle != null)
            {
                BtDebugRegistry.Unregister(_debugHandle);
                _debugHandle = null;
            }
        }

        private static ulong DeriveNodeSeed(ulong treeSeed, string nodeId)
        {
            var hash = (ulong)BtTreeDefinition.HashString(nodeId);
            return unchecked(treeSeed ^ (hash * 0x9E3779B97F4A7C15UL));
        }

        // ------------------------------------------------------------------
        // 启用与推进
        // ------------------------------------------------------------------

        /// <summary>构建执行拓扑并启动根节点。可重复调用（先重置全部运行态）。</summary>
        public void Enable(int frame = 0, Fixed64? time = null)
        {
            _context.BeginTick(frame, time ?? Fixed64.Zero);
            _lastFrame = frame;
            ResetRuntimeState();
            Flatten();

            _runStacks.Add(new List<int>());
            PushNode(0, 0);
            _enabled = true;
        }

        private void ResetRuntimeState()
        {
            _treeState = BtNodeState.Inactive;
            _conditionalReevaluates.Clear();
            _index2ConditionalReevaluate.Clear();
            _runStacks.Clear();
            _preIndex = -1;
            _preState = BtNodeState.Inactive;

            foreach (var node in _nodesById.Values)
            {
                node.State = BtNodeState.Inactive;
            }
        }

        private void Flatten()
        {
            var flatNodes = new List<BtNodeBase>(_definition.Nodes.Count);
            var flatDefinitions = new List<BtNodeDefinition>(_definition.Nodes.Count);
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
                if (_flatNodes[i] is not BtCompositeNode)
                {
                    _childConditionalIndex[i] = Array.Empty<int>();
                    continue;
                }

                var conditionalChildren = new List<int>();
                foreach (var childIndex in _childrenIndex[i])
                {
                    if (_flatNodes[childIndex] is BtConditionNodeBase)
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
            List<BtNodeBase> flatNodes,
            List<BtNodeDefinition> flatDefinitions,
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
                // 子节点即将落位的扁平索引 = 当前已加入的节点数（BTCore 语义）
                childIndexes[k] = flatNodes.Count;
                Visit(
                    definition.ChildIds[k],
                    index,
                    k,
                    flatNodes[index] is BtCompositeNode ? index : parentComposite,
                    flatNodes,
                    flatDefinitions,
                    parentIndex,
                    relativeChildIndex,
                    parentCompositeIndex,
                    childrenIndex);
            }
        }

        private BtNodeDefinition FindDefinition(string nodeId)
        {
            foreach (var node in _definition.Nodes)
            {
                if (string.Equals(node.Id, nodeId, StringComparison.Ordinal)) return node;
            }
            throw new InvalidOperationException($"BT node '{nodeId}' not found in definition.");
        }

        /// <summary>推进一个 tick。frame/time 由宿主注入，节点内禁止其他时间源。</summary>
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
                _preState = BtNodeState.Inactive;

                // 1. 前一次状态已 Running 则本 tick 不再推进，防止同帧重复执行
                // 2. 并行分支栈可能被弹出删除，需要越界保护
                while (_preState != BtNodeState.Running && i < _runStacks.Count && stack.Count > 0)
                {
                    if (TryPreemptDecorators(i)) break;
                    var index = stack[stack.Count - 1];
                    if (_preIndex == index) break;

                    _preIndex = index;
                    _preState = RunNode(index, i, _preState);
                }
            }
        }

        /// <summary>根节点完成后重新进入（响应式决策循环）。未启用时无操作。</summary>
        public void Restart()
        {
            if (!_enabled) return;
            RemoveChildConditionalReevaluate(-1);
            _runStacks.Clear();
            _runStacks.Add(new List<int>());
            _treeState = BtNodeState.Inactive;
            PushNode(0, 0);
        }

        public bool TryGetNodeIndex(string nodeId, out int flatIndex)
        {
            flatIndex = -1;
            for (var i = 0; i < _flatDefinitions.Length; i++)
            {
                if (string.Equals(_flatDefinitions[i].Id, nodeId, StringComparison.Ordinal))
                {
                    flatIndex = i;
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 核心执行（语义移植自 BTCore：运行栈 + 条件重评估）
        // ------------------------------------------------------------------

        private void PushNode(int index, int stackIndex)
        {
            var stack = _runStacks[stackIndex];
            if (stack.Count > 0 && stack[stack.Count - 1] == index) return;

            stack.Add(index);
            var node = _flatNodes[index];
            node.OnStart(_context);
            node.State = BtNodeState.Running;
        }

        private BtNodeState RunNode(int index, int stackIndex, BtNodeState preState)
        {
            PushNode(index, stackIndex);
            var node = _flatNodes[index];
            var state = preState;

            if (node is BtParentNodeBase parentNode)
            {
                state = RunParentNode(index, stackIndex, state);
                state = parentNode.OverrideState(state);
            }
            else
            {
                state = node.OnTick(_context);
                node.State = state;
            }

            if (state != BtNodeState.Running)
            {
                state = PopNode(index, stackIndex, state);
            }

            return state;
        }

        private BtNodeState RunParentNode(int index, int stackIndex, BtNodeState preState)
        {
            var node = (BtParentNodeBase)_flatNodes[index];

            // 运行中的并行节点本 tick 不重复进入
            if (node.CanRunParallel() && node.OverrideState(BtNodeState.Running) == BtNodeState.Running)
            {
                return preState;
            }

            var childState = BtNodeState.Inactive;
            var preIndex = -1;
            while (node.CanExecute() && (childState != BtNodeState.Running || node.CanRunParallel()))
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
                    preState = BtNodeState.Running;
                    break;
                }

                // 入口门控：冷却等装饰器在子节点启动前即完成自身
                if (node is BtDecoratorNode gate && gate.TryTickOverride(_context, out var overridden))
                {
                    preState = PopNode(index, stackIndex, overridden);
                    break;
                }

                preIndex = curIndex;
                childState = preState = RunNode(_childrenIndex[index][childIndex], stackIndex, preState);
            }

            // 子节点运行完成弹出后可能使 CanExecute 变 false；childState 默认 Inactive，
            // 因此返回上一次运行子节点的状态
            return preState;
        }

        private BtNodeState PopNode(int index, int stackIndex, BtNodeState state, bool popChildren = true)
        {
            var stack = _runStacks[stackIndex];
            if (stack.Count == 0 || stack[stack.Count - 1] != index)
            {
                throw new InvalidOperationException(
                    $"BT pop invariant violated: node {index} is not the top of stack {stackIndex}.");
            }
            stack.RemoveAt(stack.Count - 1);

            var node = _flatNodes[index];
            node.OnStop(_context);
            node.State = state;

            var parentIndex = _parentIndex[index];
            if (parentIndex != -1)
            {
                // 条件节点出栈：挂靠到最近一个 AbortType != None 的组合祖先，生成条件重评估记录
                if (node is BtConditionNodeBase)
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

                if (_flatNodes[parentIndex] is BtParentNodeBase parent)
                {
                    parent.OnChildExecuted(_relativeChildIndex[index], state);
                }

                if (_flatNodes[parentIndex] is BtDecoratorNode decorator)
                {
                    state = decorator.Decorate(state);
                }
            }

            if (node is BtCompositeNode)
            {
                // 组合节点出栈（完成）：其子树的条件重评估记录随之失效
                RemoveChildConditionalReevaluate(index);
            }

            // 并行分支级联退出：一个分支终止时，其后代分支栈一并弹出
            if (popChildren)
            {
                for (var i = _runStacks.Count - 1; i > stackIndex; i--)
                {
                    var backStack = _runStacks[i];
                    if (backStack.Count > 0 && IsParentNode(index, backStack[backStack.Count - 1]))
                    {
                        for (var j = backStack.Count - 1; j >= 0; j--)
                        {
                            PopNode(backStack[j], i, BtNodeState.Failure, false);
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
                state = BtNodeState.Running;
            }

            return state;
        }

        private void ReevaluateConditionalNodes()
        {
            for (var i = _conditionalReevaluates.Count - 1; i >= 0; i--)
            {
                var record = _conditionalReevaluates[i];
                if (record.CompositeIndex < 0) continue;
                if (_flatNodes[record.CompositeIndex] is not BtCompositeNode composite) continue;

                var conditionNode = _flatNodes[record.Index];
                var curState = conditionNode.OnTick(_context);
                conditionNode.State = curState;
                if (curState == record.State) continue;

                var shouldAbort = composite.AbortType switch
                {
                    BtAbortType.Self => true,
                    BtAbortType.LowerPriority => curState == BtNodeState.Success,
                    BtAbortType.Both => true,
                    _ => false,
                };
                record.State = curState;   // LowerPriority 翻假只更新基线、不中断

                if (!shouldAbort) continue;

                var runningBranch = FindRunningBranchUnder(record.CompositeIndex);
                if (runningBranch == -1) continue;
                // LowerPriority 只在运行更低优先级分支（相对序号 > 条件分支）时中断；
                // Self/Both 任意方向翻转都中断当前运行后代（可穿透 None 序列）
                if (composite.AbortType == BtAbortType.LowerPriority && runningBranch <= record.BranchIndex) continue;

                // 中止当前运行的兄弟分支（Stop 沿途），组合节点回到条件分支重评
                AbortRunningBranch(record.CompositeIndex);
                composite.OnConditionalAbort(record.BranchIndex);
            }
        }

        /// <summary>
        /// 从条件节点沿"最近组合祖先"链向上，找到第一个 AbortType != None 的组合节点；
        /// 返回其扁平索引，并给出条件在该组合下所属分支的相对子序号。
        /// </summary>
        private int FindAbortComposite(int conditionIndex, out int branchIndex)
        {
            var composite = _parentCompositeIndex[conditionIndex];
            while (composite != -1)
            {
                if (_flatNodes[composite] is BtCompositeNode node && node.AbortType != BtAbortType.None)
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

        /// <summary>找到当前正在该组合节点下执行的分支（相对子序号）；无则 -1。</summary>
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

        /// <summary>把该组合节点之上的运行分支自顶向下弹出（Stop 沿途），组合节点保留在栈上。</summary>
        private void AbortRunningBranch(int compositeIndex)
        {
            for (var j = _runStacks.Count - 1; j >= 0; j--)
            {
                var stack = _runStacks[j];
                if (stack.Count == 0 || stack.IndexOf(compositeIndex) < 0) continue;

                while (stack.Count > 0 && stack[stack.Count - 1] != compositeIndex)
                {
                    PopNode(stack[stack.Count - 1], j, BtNodeState.Failure, true);
                    if (j >= _runStacks.Count || !ReferenceEquals(_runStacks[j], stack)) return;
                }
                return;
            }
        }

        /// <summary>装饰器抢占：自栈顶向下找到第一个触发 TryTickOverride 的装饰器并中止其子树。</summary>
        private bool TryPreemptDecorators(int stackIndex)
        {
            var stack = _runStacks[stackIndex];
            for (var j = stack.Count - 1; j >= 0; j--)
            {
                if (_flatNodes[stack[j]] is not BtDecoratorNode decorator) continue;
                if (!decorator.TryTickOverride(_context, out var state)) continue;

                while (stack.Count - 1 > j)
                {
                    PopNode(stack[stack.Count - 1], stackIndex, BtNodeState.Failure, true);
                    if (stackIndex >= _runStacks.Count || !ReferenceEquals(_runStacks[stackIndex], stack)) return true;
                    if (stack.Count <= j) return true;
                }
                if (stack.Count > j)
                {
                    PopNode(stack[j], stackIndex, state, true);
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
        // 快照与回滚
        // ------------------------------------------------------------------

        public BtTreeRuntimeSnapshot CaptureState()
        {
            if (!_enabled)
                throw new InvalidOperationException("BT runtime must be enabled before capturing state.");

            var snapshot = new BtTreeRuntimeSnapshot
            {
                DefinitionHash = _definitionHash,
                Enabled = _enabled,
                TreeState = _treeState,
            };

            for (var i = 0; i < _flatNodes.Length; i++)
            {
                var node = _flatNodes[i];
                var nodeSnapshot = new BtNodeRuntimeSnapshot
                {
                    NodeId = _flatDefinitions[i].Id,
                    State = node.State,
                    RunningChildIndex = node is BtParentNodeBase parent ? parent.CaptureRunningIndex() : -1,
                    CustomState = (node as IBtNodeStateful)?.CaptureState(),
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
                snapshot.RunStacks.Add(new BtRunStackSnapshot { NodeIndexes = new List<int>(stack) });
            }

            foreach (var reevaluate in _conditionalReevaluates)
            {
                snapshot.ConditionalReevaluates.Add(new BtConditionalReevaluateSnapshot
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

        public void RestoreState(BtTreeRuntimeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.SnapshotVersion != 1)
                throw new InvalidOperationException($"Unsupported BT runtime snapshot version '{snapshot.SnapshotVersion}'.");
            if (!_enabled)
                throw new InvalidOperationException("BT runtime must be enabled before restoring state.");
            if (snapshot.DefinitionHash != _definitionHash)
                throw new InvalidOperationException("BT runtime snapshot definition hash does not match the current tree.");
            if (snapshot.Nodes.Count != _flatNodes.Length)
                throw new InvalidOperationException("BT runtime snapshot node count does not match the enabled tree.");

            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                if (!string.Equals(snapshot.Nodes[i].NodeId, _flatDefinitions[i].Id, StringComparison.Ordinal))
                    throw new InvalidOperationException("BT runtime snapshot node identity does not match the enabled tree.");
            }

            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                var nodeSnapshot = snapshot.Nodes[i];
                var node = _flatNodes[i];
                node.State = nodeSnapshot.State;
                if (node is BtParentNodeBase parent)
                {
                    parent.RestoreRunningIndex(nodeSnapshot.RunningChildIndex);
                }
                if (node is IBtNodeStateful stateful && !string.IsNullOrEmpty(nodeSnapshot.CustomState))
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
                var stack = new List<int>();
                foreach (var index in stackSnapshot.NodeIndexes)
                {
                    if (index < 0 || index >= _flatNodes.Length)
                        throw new InvalidOperationException("BT runtime snapshot contains an invalid run-stack node index.");
                    stack.Add(index);
                }
                _runStacks.Add(stack);
            }

            _conditionalReevaluates.Clear();
            _index2ConditionalReevaluate.Clear();
            foreach (var item in snapshot.ConditionalReevaluates)
            {
                if (item.Index < 0 || item.Index >= _flatNodes.Length
                    || item.CompositeIndex < -1 || item.CompositeIndex >= _flatNodes.Length)
                    throw new InvalidOperationException("BT runtime snapshot contains an invalid conditional reevaluate index.");
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
        // IBtTreeDebugView（编辑器拉取）
        // ------------------------------------------------------------------

        string IBtTreeDebugView.TreeId => _definition.TreeId;
        string IBtTreeDebugView.DisplayName => _options.DebugName ?? _definition.TreeId;
        string IBtTreeDebugView.OwnerLabel => _options.DebugOwnerLabel ?? "";

        int IBtTreeDebugView.NodeCount => _flatNodes.Length;

        int IBtTreeDebugView.LastFrame => _lastFrame;

        /// <summary>只读观察用途；观察端不得修改定义（实例节点已按它初始化）。</summary>
        BtTreeDefinition IBtTreeDebugView.TreeDefinition => _definition;

        IReadOnlyDictionary<string, string>? IBtTreeDebugView.NodeSourceTree => _nodeSourceTree;

        IReadOnlyList<BtSubtreeInstance> IBtTreeDebugView.SubtreeInstances => _subtreeInstances;

        List<BtNodeDebugInfo> IBtTreeDebugView.GetNodeStates()
        {
            var onStack = new int[_flatNodes.Length];
            foreach (var stack in _runStacks)
            {
                foreach (var index in stack)
                {
                    onStack[index]++;
                }
            }

            var result = new List<BtNodeDebugInfo>(_flatNodes.Length);
            for (var i = 0; i < _flatNodes.Length; i++)
            {
                var depth = 0;
                for (var p = _parentIndex[i]; p != -1; p = _parentIndex[p]) depth++;
                var sourceTree = _nodeSourceTree != null
                    && _nodeSourceTree.TryGetValue(_flatDefinitions[i].Id, out var src)
                    ? src
                    : null;
                result.Add(new BtNodeDebugInfo(
                    _flatDefinitions[i].Id,
                    _flatDefinitions[i].Name,
                    _flatDefinitions[i].Type,
                    _registry.TryGetDescriptor(_flatDefinitions[i].Type, out var descriptor)
                        ? descriptor.Kind
                        : BtNodeKind.Action,
                    _flatNodes[i].State,
                    depth,
                    onStack[i],
                    _flatNodes[i] is BtParentNodeBase parent ? parent.CaptureRunningIndex() : -1,
                    sourceTree));
            }
            return result;
        }

        BtBlackboardValueSnapshot IBtTreeDebugView.GetBlackboard() => _context.Blackboard.CaptureValues();
    }
}
