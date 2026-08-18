using BTCore.Runtime;
using BTCore.Runtime.Composites;
using BTCore.Runtime.Blackboards;
using Xunit;

namespace AbilityKit.BTCore.Tests;

public sealed class BTreeLifecycleTests
{
    [Fact]
    public void RebuildNodeIndex_IndexesNodesWithoutSerializerCallback()
    {
        var entry = new EntryNode { Guid = "entry", ChildGuid = "root" };
        var root = new Sequence { Guid = "root" };
        var data = new BTData();
        data.Nodes.Add(entry);
        data.Nodes.Add(root);

        data.RebuildNodeIndex();

        Assert.Same(entry, data.EntryNode);
        Assert.Same(root, data.GetNodeByGuid("root"));
    }

    [Fact]
    public void RebuildTree_Twice_DoesNotDuplicateParentChildren()
    {
        var tree = CreateTree(out var root, out _, out _);

        tree.RebuildTree();
        tree.RebuildTree();

        Assert.Collection(
            root.GetChildren(),
            child => Assert.Equal("first", child.Guid),
            child => Assert.Equal("second", child.Guid));
    }

    [Fact]
    public void Enable_Twice_RunsSingleTopology()
    {
        var tree = CreateTree(out _, out var first, out var second);
        tree.RebuildTree();

        tree.Enable();
        tree.Enable();
        tree.Update();

        Assert.Equal(1, first.UpdateCount);
        Assert.Equal(1, second.UpdateCount);
    }

    [Fact]
    public void RuntimeSnapshot_RestoresRunningStateBlackboardAndRunStack()
    {
        var source = CreateRunningTree("running");
        source.Blackboard.SetValue("memory.count", 17);
        source.Update();

        var snapshot = source.CaptureRuntimeSnapshot();
        var restored = CreateRunningTree("running");
        restored.Blackboard.SetValue("memory.count", -1);
        restored.RestoreRuntimeSnapshot(snapshot);
        var roundTrip = restored.CaptureRuntimeSnapshot();

        Assert.Equal(17, restored.Blackboard.GetValue<int>("memory.count"));
        Assert.Equal(snapshot.TreeState, roundTrip.TreeState);
        Assert.Equal(snapshot.PreIndex, roundTrip.PreIndex);
        Assert.Equal(snapshot.PreState, roundTrip.PreState);
        Assert.Equal(snapshot.Nodes.Select(node => (node.Guid, node.State)),
            roundTrip.Nodes.Select(node => (node.Guid, node.State)));
        Assert.Equal(snapshot.Nodes.Select(node => (node.Guid, node.ChildIndex)),
            roundTrip.Nodes.Select(node => (node.Guid, node.ChildIndex)));
        Assert.Equal(snapshot.RunStacks.Select(stack => string.Join(",", stack.Nodes)),
            roundTrip.RunStacks.Select(stack => string.Join(",", stack.Nodes)));
        Assert.Equal(8, restored.BTData.GetNodeByGuid("running") is RunningNode running ? running.Value : -1);
    }

    [Fact]
    public void RuntimeSnapshot_RestoresConditionalReevaluationState()
    {
        var source = CreateConditionalRunningTree(false);
        source.Update();
        var snapshot = source.CaptureRuntimeSnapshot();

        Assert.NotEmpty(snapshot.ConditionalReevaluates);
        var restored = CreateConditionalRunningTree(false);
        restored.RestoreRuntimeSnapshot(snapshot);
        var roundTrip = restored.CaptureRuntimeSnapshot();

        Assert.Equal(
            snapshot.ConditionalReevaluates.Select(item => (item.Index, item.State, item.CompositeIndex)),
            roundTrip.ConditionalReevaluates.Select(item => (item.Index, item.State, item.CompositeIndex)));
    }

    [Fact]
    public void RuntimeSnapshot_RejectsIncompatibleTreeIdentity()
    {
        var source = CreateRunningTree("first");
        source.Update();
        var snapshot = source.CaptureRuntimeSnapshot();
        var incompatible = CreateRunningTree("second");

        Assert.Throws<InvalidOperationException>(() => incompatible.RestoreRuntimeSnapshot(snapshot));
    }

    private static BTree CreateTree(
        out Sequence root,
        out CountingSuccessNode first,
        out CountingSuccessNode second)
    {
        var entry = new EntryNode { Guid = "entry", ChildGuid = "root" };
        root = new Sequence { Guid = "root" };
        first = new CountingSuccessNode { Guid = "first" };
        second = new CountingSuccessNode { Guid = "second" };
        root.ChildrenGuids.Add(first.Guid);
        root.ChildrenGuids.Add(second.Guid);

        var tree = new BTree();
        tree.BTData.Nodes.Add(entry);
        tree.BTData.Nodes.Add(root);
        tree.BTData.Nodes.Add(first);
        tree.BTData.Nodes.Add(second);
        return tree;
    }

    private static BTree CreateRunningTree(string nodeGuid)
    {
        var entry = new EntryNode { Guid = "entry", ChildGuid = "root" };
        var root = new Sequence { Guid = "root" };
        var running = new RunningNode { Guid = nodeGuid, Value = 8 };
        root.ChildrenGuids.Add(running.Guid);
        var tree = new BTree { Blackboard = new Blackboard() };
        tree.Blackboard.SetValue("memory.count", 0);
        tree.BTData.Nodes.Add(entry);
        tree.BTData.Nodes.Add(root);
        tree.BTData.Nodes.Add(running);
        tree.RebuildTree();
        tree.Enable();
        return tree;
    }

    private static BTree CreateConditionalRunningTree(bool conditionValue)
    {
        var entry = new EntryNode { Guid = "entry", ChildGuid = "selector" };
        var selector = new Selector { Guid = "selector", AbortType = AbortType.Both };
        var condition = new BlackboardCondition { Guid = "condition", Value = conditionValue };
        var running = new RunningNode { Guid = "running", Value = 8 };
        selector.ChildrenGuids.Add(condition.Guid);
        selector.ChildrenGuids.Add(running.Guid);

        var tree = new BTree { Blackboard = new Blackboard() };
        tree.BTData.Nodes.Add(entry);
        tree.BTData.Nodes.Add(selector);
        tree.BTData.Nodes.Add(condition);
        tree.BTData.Nodes.Add(running);
        tree.RebuildTree();
        tree.Enable();
        return tree;
    }

    private sealed class CountingSuccessNode : BTNode
    {
        public int UpdateCount { get; private set; }

        protected override NodeState OnUpdate()
        {
            UpdateCount++;
            return NodeState.Success;
        }
    }

    private sealed class RunningNode : BTNode, IBTNodeRuntimeSnapshot
    {
        public int Value { get; set; }
        public string RuntimeSnapshotType => "test.running.v1";
        protected override NodeState OnUpdate() => NodeState.Running;
        public string CaptureRuntimeSnapshot() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public void RestoreRuntimeSnapshot(string payload) =>
            Value = int.Parse(payload, System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class BlackboardCondition : global::BTCore.Runtime.Conditions.Condition
    {
        public bool Value { get; set; }
        protected override bool Validate() => Value;
    }
}
