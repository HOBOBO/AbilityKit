using System.Linq;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>鍔犺浇鏍￠獙璐熷悜闆嗭細缁撴瀯銆佺被鍨嬨€佸睘鎬?schema銆侀粦鏉?schema銆?/summary>
    public sealed class BtTreeValidatorTests
    {
        private static TreeDefinition ValidTree()
        {
            return new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Sequence, "a")
                .Node("a", ScriptedAction)
                .Root("root");
        }

        [Fact]
        public void ValidTree_Passes()
        {
            Assert.Empty(TreeValidator.Validate(ValidTree(), CreateRegistry()));
        }

        [Fact]
        public void UnknownNodeType_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[1].Type = "nope.unknown";
            Assert.Single(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("unknown type"));
        }

        [Fact]
        public void MissingRoot_IsRejected()
        {
            var definition = ValidTree();
            definition.RootNodeId = "missing";
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("Root node"));
        }

        [Fact]
        public void Cycle_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "a")
                .Node("a", BuiltInNodeTypes.Sequence, "root")
                .Root("root");
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("Cycle"));
        }

        [Fact]
        public void MultipleParents_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "a", "a")
                .Node("a", ScriptedAction)
                .Root("root");
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("multiple parents"));
        }

        [Fact]
        public void UnreachableNode_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes.Add(new NodeDefinition { Id = "orphan", Type = ScriptedAction });
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("unreachable"));
        }

        [Fact]
        public void DuplicateNodeId_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes.Add(new NodeDefinition { Id = "a", Type = ScriptedAction });
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("duplicated"));
        }

        [Fact]
        public void DecoratorWithTwoChildren_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Inverter, "a", "b")
                .Node("a", ScriptedAction)
                .Node("b", ScriptedAction)
                .Root("root");
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("children"));
        }

        [Fact]
        public void ActionWithChild_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", ScriptedAction, "a")
                .Node("a", ScriptedAction)
                .Root("root");
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("children"));
        }

        [Fact]
        public void UnknownProperty_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[1].Properties.Set("nope", PropertyValue.Of(1L));
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("unknown property"));
        }

        [Fact]
        public void PropertyTypeMismatch_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[1].Properties.Set(ScriptedResultActionNode.ResultKeyProperty, PropertyValue.Of(1L));
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("schema expects"));
        }

        [Fact]
        public void DuplicateBlackboardKey_IsRejected()
        {
            var definition = ValidTree();
            definition.Blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "test.result", Type = TreeValueType.Bool });
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("duplicated"));
        }

        [Fact]
        public void BlackboardDefaultTypeMismatch_IsRejected()
        {
            var definition = ValidTree();
            definition.Blackboard.Keys.Add(new BlackboardKeyDefinition
            {
                Name = "other",
                Type = TreeValueType.Int64,
                Default = PropertyValue.Of(true),
            });
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("default value type"));
        }

        [Fact]
        public void InvalidAbortType_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[0].Properties.Set(CompositeNode.AbortTypeProperty, PropertyValue.Of(99L));
            Assert.Contains(TreeValidator.Validate(definition, CreateRegistry()),
                e => e.Contains("abortType"));
        }

        [Fact]
        public void Create_ThrowsOnInvalidDefinition()
        {
            var definition = ValidTree();
            definition.RootNodeId = "missing";
            Assert.Throws<System.InvalidOperationException>(
                () => TreeRuntime.Create(definition, CreateRegistry()));
        }
    }
}
