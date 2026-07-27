using BTCore.Runtime;
using BTCore.Runtime.Composites;
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

    private sealed class CountingSuccessNode : BTNode
    {
        public int UpdateCount { get; private set; }

        protected override NodeState OnUpdate()
        {
            UpdateCount++;
            return NodeState.Success;
        }
    }
}
