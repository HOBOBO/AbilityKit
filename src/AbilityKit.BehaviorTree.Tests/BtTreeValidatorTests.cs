using System.Linq;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>加载校验负向集：结构、类型、属性 schema、黑板 schema。</summary>
    public sealed class BtTreeValidatorTests
    {
        private static BtTreeDefinition ValidTree()
        {
            return new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Sequence, "a")
                .Node("a", ScriptedAction)
                .Root("root");
        }

        [Fact]
        public void ValidTree_Passes()
        {
            Assert.Empty(BtTreeValidator.Validate(ValidTree(), CreateRegistry()));
        }

        [Fact]
        public void UnknownNodeType_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[1].Type = "nope.unknown";
            Assert.Single(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("unknown type"));
        }

        [Fact]
        public void MissingRoot_IsRejected()
        {
            var definition = ValidTree();
            definition.RootNodeId = "missing";
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("Root node"));
        }

        [Fact]
        public void Cycle_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Sequence, "a")
                .Node("a", BtBuiltInNodeTypes.Sequence, "root")
                .Root("root");
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("Cycle"));
        }

        [Fact]
        public void MultipleParents_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Sequence, "a", "a")
                .Node("a", ScriptedAction)
                .Root("root");
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("multiple parents"));
        }

        [Fact]
        public void UnreachableNode_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes.Add(new BtNodeDefinition { Id = "orphan", Type = ScriptedAction });
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("unreachable"));
        }

        [Fact]
        public void DuplicateNodeId_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes.Add(new BtNodeDefinition { Id = "a", Type = ScriptedAction });
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("duplicated"));
        }

        [Fact]
        public void DecoratorWithTwoChildren_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Inverter, "a", "b")
                .Node("a", ScriptedAction)
                .Node("b", ScriptedAction)
                .Root("root");
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("children"));
        }

        [Fact]
        public void ActionWithChild_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", ScriptedAction, "a")
                .Node("a", ScriptedAction)
                .Root("root");
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("children"));
        }

        [Fact]
        public void UnknownProperty_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[1].Properties.Set("nope", BtPropertyValue.Of(1L));
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("unknown property"));
        }

        [Fact]
        public void PropertyTypeMismatch_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[1].Properties.Set(ScriptedResultActionNode.ResultKeyProperty, BtPropertyValue.Of(1L));
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("schema expects"));
        }

        [Fact]
        public void DuplicateBlackboardKey_IsRejected()
        {
            var definition = ValidTree();
            definition.Blackboard.Keys.Add(new BtBlackboardKeyDefinition { Name = "test.result", Type = BtValueType.Bool });
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("duplicated"));
        }

        [Fact]
        public void BlackboardDefaultTypeMismatch_IsRejected()
        {
            var definition = ValidTree();
            definition.Blackboard.Keys.Add(new BtBlackboardKeyDefinition
            {
                Name = "other",
                Type = BtValueType.Int64,
                Default = BtPropertyValue.Of(true),
            });
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("default value type"));
        }

        [Fact]
        public void InvalidAbortType_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[0].Properties.Set(BtCompositeNode.AbortTypeProperty, BtPropertyValue.Of(99L));
            Assert.Contains(BtTreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("abortType"));
        }

        [Fact]
        public void Create_ThrowsOnInvalidDefinition()
        {
            var definition = ValidTree();
            definition.RootNodeId = "missing";
            Assert.Throws<System.InvalidOperationException>(
                () => BtTreeRuntime.Create(definition, CreateRegistry()));
        }
    }
}
