using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
using CanonicalExecutionContext = AbilityKit.BehaviorTree.Execution.ExecutionContext;

namespace AbilityKit.BehaviorTree
{
    [Obsolete("Use AbilityKit.BehaviorTree.Execution.LifecycleExceptionPolicy.", false)]
    public enum BtLifecycleExceptionPolicy
    {
        Throw = 0,
        CaptureAndContinue = 1,
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.NodeStopReason.", false)]
    public enum BtNodeStopReason
    {
        None = 0,
        Completed = 1,
        Disabled = 2,
        Disposed = 3,
        Restarted = 4,
        Aborted = 5,
        Preempted = 6,
        EnableFailed = 7,
        Restored = 8,
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.ServiceResolver.", false)]
    public interface IBtServiceResolver
    {
        T Resolve<T>() where T : class;
        bool TryResolve<T>(out T service) where T : class;
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.DefaultServiceResolver.", false)]
    public sealed class BtServiceResolver : IBtServiceResolver
    {
        private readonly DefaultServiceResolver _inner = new();

        public BtServiceResolver Add<T>(T service) where T : class
        {
            _inner.Add(service);
            return this;
        }

        public T Resolve<T>() where T : class => _inner.Resolve<T>();
        public bool TryResolve<T>(out T service) where T : class => _inner.TryResolve(out service);
        internal ServiceResolver ToCanonical() => _inner;
    }

    internal sealed class LegacyServiceResolverAdapter : ServiceResolver
    {
        private readonly IBtServiceResolver _inner;

        public LegacyServiceResolverAdapter(IBtServiceResolver inner) => _inner = inner;
        public T Resolve<T>() where T : class => _inner.Resolve<T>();
        public bool TryResolve<T>(out T service) where T : class => _inner.TryResolve(out service);
    }

    internal sealed class CanonicalServiceResolverAdapter : IBtServiceResolver
    {
        private readonly ServiceResolver _inner;

        public CanonicalServiceResolverAdapter(ServiceResolver inner) => _inner = inner;
        public T Resolve<T>() where T : class => _inner.Resolve<T>();
        public bool TryResolve<T>(out T service) where T : class => _inner.TryResolve(out service);
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.ExecutionContext.", false)]
    public sealed class BtExecutionContext
    {
        private readonly CanonicalExecutionContext? _inner;

        public BtBlackboard Blackboard { get; }
        public IBtServiceResolver Services { get; }
        public int Frame { get; internal set; }
        public Fixed64 Time { get; internal set; }
        public BtNodeStopReason StopReason { get; private set; }

        public BtExecutionContext(BtBlackboard blackboard, IBtServiceResolver services)
        {
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            Services = services ?? new BtServiceResolver();
        }

        internal BtExecutionContext(CanonicalExecutionContext inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Blackboard = inner.Blackboard.Inner;
            Services = new CanonicalServiceResolverAdapter(inner.Services);
            Frame = inner.Frame;
            Time = inner.Time;
            StopReason = (BtNodeStopReason)(int)inner.StopReason;
        }

        internal CanonicalExecutionContext Inner => _inner ?? new CanonicalExecutionContext(
            AbilityKit.BehaviorTree.Blackboard.Blackboard.FromLegacy(Blackboard),
            Services is BtServiceResolver resolver ? resolver.ToCanonical() : new LegacyServiceResolverAdapter(Services));

        internal void BeginTick(int frame, Fixed64 time)
        {
            Frame = frame;
            Time = time;
        }

        internal void BeginStop(BtNodeStopReason reason)
        {
            StopReason = reason;
        }

        internal void EndStop()
        {
            StopReason = BtNodeStopReason.None;
        }

    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.NodeInitContext.", false)]
    public struct BtNodeInitContext
    {
        public BtTreeDefinition Tree { get; set; }
        public BtNodeDefinition Definition { get; set; }
        public BtPropertyReader Properties { get; set; }
        public int ChildCount { get; set; }
        public BtNodeRegistry Registry { get; set; }
        public DeterministicRandom Random { get; set; }
        public BtExecutionContext Context { get; set; }

        internal BtNodeInitContext(in NodeInitContext inner)
        {
            Tree = inner.Tree.ToLegacy();
            Definition = inner.Definition.ToLegacy();
            Properties = new BtPropertyReader(Definition.Properties);
            ChildCount = inner.ChildCount;
            Registry = inner.Registry.ToLegacy();
            Random = inner.Random;
            Context = new BtExecutionContext(inner.Context);
        }

        internal NodeInitContext Inner => new()
        {
            Tree = TreeDefinition.FromLegacy(Tree),
            Definition = NodeDefinition.FromLegacy(Definition),
            Properties = new PropertyReader(NodeDefinition.FromLegacy(Definition).Properties),
            ChildCount = ChildCount,
            Registry = NodeRegistry.FromLegacy(Registry),
            Random = Random,
            Context = Context.Inner,
        };
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.TreeRunOptions.", false)]
    public sealed class BtTreeRunOptions
    {
        public ulong Seed { get; set; } = 0x12345678UL;
        public bool RestartWhenComplete { get; set; }
        public string? DebugName { get; set; }
        public string? DebugOwnerLabel { get; set; }
        public BtLifecycleExceptionPolicy LifecycleExceptionPolicy { get; set; } = BtLifecycleExceptionPolicy.Throw;

        internal TreeRunOptions ToCanonical() => new()
        {
            Seed = Seed,
            RestartWhenComplete = RestartWhenComplete,
            DebugName = DebugName,
            DebugOwnerLabel = DebugOwnerLabel,
            LifecycleExceptionPolicy = (LifecycleExceptionPolicy)(int)LifecycleExceptionPolicy,
        };
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.LifecycleExceptionRecord.", false)]
    public sealed class BtLifecycleExceptionRecord
    {
        public string NodeId { get; }
        public string Callback { get; }
        public BtNodeStopReason StopReason { get; }
        public Exception Exception { get; }

        public BtLifecycleExceptionRecord(string nodeId, string callback, BtNodeStopReason stopReason, Exception exception)
        {
            NodeId = nodeId ?? "";
            Callback = callback ?? "";
            StopReason = stopReason;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        internal BtLifecycleExceptionRecord(LifecycleExceptionRecord source)
            : this(source.NodeId, source.Callback, (BtNodeStopReason)(int)source.StopReason, source.Exception)
        {
        }
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.NodeRuntimeSnapshot.", false)]
    public sealed class BtNodeRuntimeSnapshot
    {
        public string NodeId { get; set; } = "";
        public BtNodeState State { get; set; }
        public int RunningChildIndex { get; set; } = -1;
        public string? CustomState { get; set; }
        public ulong RandomS0 { get; set; }
        public ulong RandomS1 { get; set; }
        public ulong RandomSequence { get; set; }

        internal NodeRuntimeSnapshot ToCanonical() => new()
        {
            NodeId = NodeId,
            State = State.ToApi(),
            RunningChildIndex = RunningChildIndex,
            CustomState = CustomState,
            RandomS0 = RandomS0,
            RandomS1 = RandomS1,
            RandomSequence = RandomSequence,
        };

        internal static BtNodeRuntimeSnapshot FromCanonical(NodeRuntimeSnapshot source) => new()
        {
            NodeId = source.NodeId,
            State = source.State.ToLegacy(),
            RunningChildIndex = source.RunningChildIndex,
            CustomState = source.CustomState,
            RandomS0 = source.RandomS0,
            RandomS1 = source.RandomS1,
            RandomSequence = source.RandomSequence,
        };
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.RunStackSnapshot.", false)]
    public sealed class BtRunStackSnapshot
    {
        public List<int> NodeIndexes { get; set; } = new();

        internal RunStackSnapshot ToCanonical() => new() { NodeIndexes = new List<int>(NodeIndexes) };
        internal static BtRunStackSnapshot FromCanonical(RunStackSnapshot source)
            => new() { NodeIndexes = new List<int>(source.NodeIndexes) };
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.ConditionalReevaluateSnapshot.", false)]
    public sealed class BtConditionalReevaluateSnapshot
    {
        public int Index { get; set; }
        public BtNodeState State { get; set; }
        public int CompositeIndex { get; set; }
        public int BranchIndex { get; set; }

        internal ConditionalReevaluateSnapshot ToCanonical() => new()
        {
            Index = Index,
            State = State.ToApi(),
            CompositeIndex = CompositeIndex,
            BranchIndex = BranchIndex,
        };

        internal static BtConditionalReevaluateSnapshot FromCanonical(ConditionalReevaluateSnapshot source) => new()
        {
            Index = source.Index,
            State = source.State.ToLegacy(),
            CompositeIndex = source.CompositeIndex,
            BranchIndex = source.BranchIndex,
        };
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.TreeRuntimeSnapshot.", false)]
    public sealed class BtTreeRuntimeSnapshot
    {
        public const int CurrentSnapshotVersion = TreeRuntimeSnapshot.CurrentSnapshotVersion;

        public int SnapshotVersion { get; set; } = CurrentSnapshotVersion;
        public long DefinitionHash { get; set; }
        public bool Enabled { get; set; }
        public BtNodeState TreeState { get; set; }
        public List<BtNodeRuntimeSnapshot> Nodes { get; set; } = new();
        public List<BtRunStackSnapshot> RunStacks { get; set; } = new();
        public List<BtConditionalReevaluateSnapshot> ConditionalReevaluates { get; set; } = new();
        public BtBlackboardValueSnapshot? Blackboard { get; set; }

        internal TreeRuntimeSnapshot ToCanonical()
        {
            var snapshot = new TreeRuntimeSnapshot
            {
                SnapshotVersion = SnapshotVersion,
                DefinitionHash = DefinitionHash,
                Enabled = Enabled,
                TreeState = TreeState.ToApi(),
                Blackboard = Blackboard == null ? null : BlackboardValueSnapshot.FromLegacy(Blackboard),
            };
            foreach (var node in Nodes) snapshot.Nodes.Add(node.ToCanonical());
            foreach (var stack in RunStacks) snapshot.RunStacks.Add(stack.ToCanonical());
            foreach (var item in ConditionalReevaluates) snapshot.ConditionalReevaluates.Add(item.ToCanonical());
            return snapshot;
        }

        internal static BtTreeRuntimeSnapshot FromCanonical(TreeRuntimeSnapshot source)
        {
            var snapshot = new BtTreeRuntimeSnapshot
            {
                SnapshotVersion = source.SnapshotVersion,
                DefinitionHash = source.DefinitionHash,
                Enabled = source.Enabled,
                TreeState = source.TreeState.ToLegacy(),
                Blackboard = source.Blackboard?.ToLegacy(),
            };
            foreach (var node in source.Nodes) snapshot.Nodes.Add(BtNodeRuntimeSnapshot.FromCanonical(node));
            foreach (var stack in source.RunStacks) snapshot.RunStacks.Add(BtRunStackSnapshot.FromCanonical(stack));
            foreach (var item in source.ConditionalReevaluates) snapshot.ConditionalReevaluates.Add(BtConditionalReevaluateSnapshot.FromCanonical(item));
            return snapshot;
        }
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.RuntimeSnapshotMigrator.", false)]
    public interface IBtRuntimeSnapshotMigrator
    {
        int FromVersion { get; }
        int ToVersion { get; }
        BtTreeRuntimeSnapshot Migrate(BtTreeRuntimeSnapshot snapshot);
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.RuntimeSnapshotMigrationRegistry.", false)]
    public sealed class BtRuntimeSnapshotMigrationRegistry
    {
        private readonly Dictionary<int, IBtRuntimeSnapshotMigrator> _migrators = new();
        public static BtRuntimeSnapshotMigrationRegistry Global { get; } = new();

        public void Register(IBtRuntimeSnapshotMigrator migrator)
        {
            if (migrator == null) throw new ArgumentNullException(nameof(migrator));
            if (migrator.ToVersion <= migrator.FromVersion)
                throw new ArgumentException("BT snapshot migrators must move to a newer version.", nameof(migrator));
            _migrators[migrator.FromVersion] = migrator;
        }

        public BtTreeRuntimeSnapshot MigrateToCurrent(BtTreeRuntimeSnapshot snapshot)
            => Migrate(snapshot, BtTreeRuntimeSnapshot.CurrentSnapshotVersion);

        public BtTreeRuntimeSnapshot Migrate(BtTreeRuntimeSnapshot snapshot, int targetVersion)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var current = snapshot;
            while (current.SnapshotVersion < targetVersion)
            {
                if (!_migrators.TryGetValue(current.SnapshotVersion, out var migrator)
                    || migrator.ToVersion > targetVersion)
                {
                    throw new InvalidOperationException(
                        $"Unsupported BT runtime snapshot version '{current.SnapshotVersion}'.");
                }
                current = migrator.Migrate(current)
                    ?? throw new InvalidOperationException(
                        $"BT runtime snapshot migrator from version {migrator.FromVersion} returned null.");
                if (current.SnapshotVersion != migrator.ToVersion)
                {
                    throw new InvalidOperationException(
                        $"BT runtime snapshot migrator from version {migrator.FromVersion} returned version {current.SnapshotVersion}, expected {migrator.ToVersion}.");
                }
            }

            if (current.SnapshotVersion != targetVersion)
                throw new InvalidOperationException(
                    $"Unsupported BT runtime snapshot version '{current.SnapshotVersion}'.");
            return current;
        }
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.TreeDefinitionResolver.", false)]
    public interface IBtTreeDefinitionResolver
    {
        bool TryResolve(string treeId, out BtTreeDefinition definition);
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.SubtreeInstance.", false)]
    public sealed class BtSubtreeInstance
    {
        public string InlinedRootNodeId { get; }
        public string ReferencedTreeId { get; }

        public BtSubtreeInstance(string inlinedRootNodeId, string referencedTreeId)
        {
            InlinedRootNodeId = inlinedRootNodeId;
            ReferencedTreeId = referencedTreeId;
        }

        internal BtSubtreeInstance(SubtreeInstance source)
            : this(source.InlinedRootNodeId, source.ReferencedTreeId)
        {
        }
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.ExpansionResult.", false)]
    public sealed class BtExpansionResult
    {
        public BtTreeDefinition Definition { get; }
        public IReadOnlyDictionary<string, string> NodeSourceTree { get; }
        public IReadOnlyDictionary<string, string> NodeSourceNode { get; }
        public IReadOnlyList<BtSubtreeInstance> SubtreeInstances { get; }

        public BtExpansionResult(
            BtTreeDefinition definition,
            Dictionary<string, string> nodeSourceTree,
            Dictionary<string, string> nodeSourceNode,
            List<BtSubtreeInstance> subtreeInstances)
        {
            Definition = definition;
            NodeSourceTree = nodeSourceTree;
            NodeSourceNode = nodeSourceNode;
            SubtreeInstances = subtreeInstances;
        }

        internal BtExpansionResult(ExpansionResult source)
        {
            Definition = source.Definition.ToLegacy();
            NodeSourceTree = source.NodeSourceTree;
            NodeSourceNode = source.NodeSourceNode;
            var instances = new List<BtSubtreeInstance>(source.SubtreeInstances.Count);
            foreach (var instance in source.SubtreeInstances) instances.Add(new BtSubtreeInstance(instance));
            SubtreeInstances = instances;
        }
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.TreeCompiler.", false)]
    public static class BtTreeCompiler
    {
        public static BtExpansionResult ExpandReferences(BtTreeDefinition definition, IBtTreeDefinitionResolver resolver)
            => new(TreeCompiler.ExpandReferences(
                TreeDefinition.FromLegacy(definition),
                new LegacyTreeDefinitionResolverAdapter(resolver)));
    }

    internal sealed class LegacyTreeDefinitionResolverAdapter : TreeDefinitionResolver
    {
        private readonly IBtTreeDefinitionResolver _inner;

        public LegacyTreeDefinitionResolverAdapter(IBtTreeDefinitionResolver inner) => _inner = inner;

        public bool TryResolve(string treeId, out TreeDefinition definition)
        {
            if (_inner.TryResolve(treeId, out var legacy))
            {
                definition = TreeDefinition.FromLegacy(legacy);
                return true;
            }

            definition = null!;
            return false;
        }
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.TreeTopology.", false)]
    public sealed class BtTreeTopology
    {
        private readonly TreeTopology _inner;

        internal BtTreeTopology(TreeTopology inner) => _inner = inner;

        public int NodeCount => _inner.NodeCount;
        public bool TryGetNodeIndex(string nodeId, out int flatIndex) => _inner.TryGetNodeIndex(nodeId, out flatIndex);

        public bool TryGetNodeDefinition(string nodeId, out BtNodeDefinition definition)
        {
            if (_inner.TryGetNodeDefinition(nodeId, out var canonical))
            {
                definition = canonical.ToLegacy();
                return true;
            }

            definition = null!;
            return false;
        }

        public BtNodeDefinition GetNodeDefinition(int flatIndex) => _inner.GetNodeDefinition(flatIndex).ToLegacy();
        public static BtTreeTopology Compile(BtTreeDefinition definition, BtNodeRegistry registry)
            => new(TreeTopology.Compile(TreeDefinition.FromLegacy(definition), NodeRegistry.FromLegacy(registry)));
    }

    [Obsolete("Use AbilityKit.BehaviorTree.Execution.TreeRuntime.", false)]
    public sealed class BtTreeRuntime : IBtTreeDebugView, IBtTreeDebugDeltaView, IDisposable
    {
        private readonly TreeRuntime _inner;
        private readonly BtBlackboard _blackboard;

        private BtTreeRuntime(TreeRuntime inner)
        {
            _inner = inner;
            _blackboard = inner.Blackboard.Inner;
        }

        public BtTreeDefinition Definition => _inner.Definition.ToLegacy();
        public BtBlackboard Blackboard => _blackboard;
        public bool IsEnabled => _inner.IsEnabled;
        public BtNodeState TreeState => _inner.TreeState.ToLegacy();
        public BtNodeState RootNodeState => _inner.RootNodeState.ToLegacy();
        public int NodeCount => _inner.NodeCount;
        public BtTreeTopology Topology => new(_inner.Topology);
        public IReadOnlyDictionary<string, string>? NodeSourceTree => _inner.NodeSourceTree;
        public IReadOnlyDictionary<string, string>? NodeSourceNode => _inner.NodeSourceNode;

        public IReadOnlyList<BtSubtreeInstance> SubtreeInstances
        {
            get
            {
                var result = new List<BtSubtreeInstance>(_inner.SubtreeInstances.Count);
                foreach (var instance in _inner.SubtreeInstances) result.Add(new BtSubtreeInstance(instance));
                return result;
            }
        }

        public IReadOnlyList<BtLifecycleExceptionRecord> LifecycleExceptions
        {
            get
            {
                var result = new List<BtLifecycleExceptionRecord>(_inner.LifecycleExceptions.Count);
                foreach (var item in _inner.LifecycleExceptions) result.Add(new BtLifecycleExceptionRecord(item));
                return result;
            }
        }

        public BtLifecycleExceptionRecord? LastLifecycleException =>
            _inner.LastLifecycleException == null ? null : new BtLifecycleExceptionRecord(_inner.LastLifecycleException);

        public static BtTreeRuntime Create(
            BtTreeDefinition definition,
            BtNodeRegistry registry,
            IBtServiceResolver? services = null,
            BtTreeRunOptions? options = null,
            IBtTreeDefinitionResolver? subtreeResolver = null)
        {
            var resolver = subtreeResolver == null ? null : new LegacyTreeDefinitionResolverAdapter(subtreeResolver);
            var serviceResolver = services switch
            {
                null => null,
                BtServiceResolver defaults => defaults.ToCanonical(),
                _ => new LegacyServiceResolverAdapter(services),
            };

            return new BtTreeRuntime(TreeRuntime.Create(
                TreeDefinition.FromLegacy(definition),
                NodeRegistry.FromLegacy(registry),
                serviceResolver,
                options?.ToCanonical(),
                resolver));
        }

        public void Dispose() => _inner.Dispose();

        public void Enable(int frame = 0, Fixed64? time = null)
        {
            SyncBlackboardToCanonical();
            _inner.Enable(frame, time);
            SyncBlackboardFromCanonical();
        }

        public void Disable()
        {
            SyncBlackboardToCanonical();
            _inner.Disable();
            SyncBlackboardFromCanonical();
        }

        public void Update(int frame, Fixed64 time)
        {
            SyncBlackboardToCanonical();
            _inner.Update(frame, time);
            SyncBlackboardFromCanonical();
        }

        public void Restart()
        {
            SyncBlackboardToCanonical();
            _inner.Restart();
            SyncBlackboardFromCanonical();
        }

        public bool TryGetNodeIndex(string nodeId, out int flatIndex) => _inner.TryGetNodeIndex(nodeId, out flatIndex);

        public BtTreeRuntimeSnapshot CaptureState()
        {
            SyncBlackboardToCanonical();
            return BtTreeRuntimeSnapshot.FromCanonical(_inner.CaptureState());
        }

        public void RestoreState(BtTreeRuntimeSnapshot snapshot)
        {
            var migrated = BtRuntimeSnapshotMigrationRegistry.Global.MigrateToCurrent(snapshot);
            _inner.RestoreState(migrated.ToCanonical());
            SyncBlackboardFromCanonical();
        }

        private void SyncBlackboardToCanonical()
            => _inner.Blackboard.RestoreValues(BlackboardValueSnapshot.FromLegacy(_blackboard.CaptureValues()));

        private void SyncBlackboardFromCanonical()
            => _blackboard.RestoreValues(_inner.Blackboard.CaptureValues().ToLegacy());

        string IBtTreeDebugView.TreeId => ((TreeDebugView)_inner).TreeId;
        string IBtTreeDebugView.DisplayName => ((TreeDebugView)_inner).DisplayName;
        string IBtTreeDebugView.OwnerLabel => ((TreeDebugView)_inner).OwnerLabel;
        int IBtTreeDebugView.NodeCount => ((TreeDebugView)_inner).NodeCount;
        int IBtTreeDebugView.LastFrame => ((TreeDebugView)_inner).LastFrame;
        BtTreeDefinition IBtTreeDebugView.TreeDefinition => ((TreeDebugView)_inner).TreeDefinition.ToLegacy();
        IReadOnlyDictionary<string, string>? IBtTreeDebugView.NodeSourceTree => ((TreeDebugView)_inner).NodeSourceTree;
        IReadOnlyDictionary<string, string>? IBtTreeDebugView.NodeSourceNode => ((TreeDebugView)_inner).NodeSourceNode;
        IReadOnlyList<BtSubtreeInstance> IBtTreeDebugView.SubtreeInstances => SubtreeInstances;
        BtBlackboardValueSnapshot IBtTreeDebugView.GetBlackboard() => ((TreeDebugView)_inner).GetBlackboard().ToLegacy();
        BtTreeRuntimeSnapshot IBtTreeDebugView.CaptureState() => CaptureState();

        List<BtNodeDebugInfo> IBtTreeDebugView.GetNodeStates()
        {
            var source = ((TreeDebugView)_inner).GetNodeStates();
            var result = new List<BtNodeDebugInfo>(source.Count);
            foreach (var node in source)
            {
                result.Add(new BtNodeDebugInfo(
                    node.NodeId,
                    node.Name,
                    node.TypeId,
                    node.Kind.ToLegacy(),
                    node.State.ToLegacy(),
                    node.Depth,
                    node.OnStackCount,
                    node.RunningChildIndex,
                    node.SourceTreeId));
            }
            return result;
        }

        long IBtTreeDebugDeltaView.DebugSequence => ((TreeDebugDeltaView)_inner).DebugSequence;

        BtTreeDebugDelta IBtTreeDebugDeltaView.CaptureDebugDelta(long knownSequence, bool includeBlackboard)
            => ((TreeDebugDeltaView)_inner).CaptureDebugDelta(knownSequence, includeBlackboard).ToLegacy();
    }
}
