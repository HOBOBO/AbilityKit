using System.Linq;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>鍔犺浇鏍￠獙璐熷悜闆嗭細缁撴瀯銆佺被鍨嬨€佸睘鎬?schema銆侀粦鏉?schema銆?/summary>
    public sealed class TreeValidatorTests
    {
        private static TreeDefinition ValidTree()
        {
            return new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Sequence, "a")
                .Node("a", ScriptedAction)
                .Root("root");
        }

        private static bool HasDiagnostic(TreeDefinition definition, string code)
            => TreeValidator.ValidateDiagnostics(definition, CreateRegistry()).Any(d => d.Code == code);

        [Fact]
        public void ValidTree_Passes()
        {
            Assert.Empty(TreeValidator.Validate(ValidTree(), CreateRegistry()));
        }

        [Fact]
        public void SubtreeBindings_ValidateParentKeysAndDuplicates()
        {
            var definition = new TreeBuilder()
                .Blackboard("parent.value", TreeValueType.Int64)
                .Node("sub", BuiltInNodeTypes.Subtree)
                .Root("sub");
            definition.Nodes[0].Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of("child"));
            definition.Nodes[0].SubtreeBlackboard = new SubtreeBlackboardConfiguration
            {
                Bindings = new System.Collections.Generic.List<SubtreeBlackboardBinding>
                {
                    new() { SubtreeKey = "value", ParentKey = "parent.value" },
                    new() { SubtreeKey = "value", ParentKey = "missing" },
                },
            };

            var diagnostics = TreeValidator.ValidateDiagnostics(definition, CreateRegistry());
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "BT0553");
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "BT0554");
        }

        [Fact]
        public void UnknownNodeType_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[1].Type = "nope.unknown";
            Assert.True(HasDiagnostic(definition, "BT0400"));
        }

        [Fact]
        public void MissingRoot_IsRejected()
        {
            var definition = ValidTree();
            definition.RootNodeId = "missing";
            Assert.True(HasDiagnostic(definition, "BT0202"));
        }

        [Fact]
        public void Cycle_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "a")
                .Node("a", BuiltInNodeTypes.Sequence, "root")
                .Root("root");
            Assert.True(HasDiagnostic(definition, "BT0701"));
        }

        [Fact]
        public void MultipleParents_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "a", "a")
                .Node("a", ScriptedAction)
                .Root("root");
            Assert.True(HasDiagnostic(definition, "BT0700"));
        }

        [Fact]
        public void UnreachableNode_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes.Add(new NodeDefinition { Id = "orphan", Type = ScriptedAction });
            Assert.True(HasDiagnostic(definition, "BT0702"));
        }

        [Fact]
        public void DuplicateNodeId_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes.Add(new NodeDefinition { Id = "a", Type = ScriptedAction });
            Assert.True(HasDiagnostic(definition, "BT0201"));
        }

        [Fact]
        public void DecoratorWithTwoChildren_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Inverter, "a", "b")
                .Node("a", ScriptedAction)
                .Node("b", ScriptedAction)
                .Root("root");
            Assert.True(HasDiagnostic(definition, "BT0401"));
        }

        [Fact]
        public void ActionWithChild_IsRejected()
        {
            var definition = new TreeBuilder()
                .Node("root", ScriptedAction, "a")
                .Node("a", ScriptedAction)
                .Root("root");
            Assert.True(HasDiagnostic(definition, "BT0401"));
        }

        [Fact]
        public void UnknownProperty_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[1].Properties.Set("nope", PropertyValue.Of(1L));
            Assert.True(HasDiagnostic(definition, "BT0503"));
        }

        [Fact]
        public void PropertyTypeMismatch_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[1].Properties.Set(ScriptedResultActionNode.ResultKeyProperty, PropertyValue.Of(1L));
            Assert.True(HasDiagnostic(definition, "BT0500"));
        }

        [Fact]
        public void DuplicateBlackboardKey_IsRejected()
        {
            var definition = ValidTree();
            definition.Blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "test.result", Type = TreeValueType.Bool });
            Assert.True(HasDiagnostic(definition, "BT0301"));
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
            Assert.True(HasDiagnostic(definition, "BT0302"));
        }

        [Fact]
        public void InvalidAbortType_IsRejected()
        {
            var definition = ValidTree();
            definition.Nodes[0].Properties.Set(CompositeNode.AbortTypeProperty, PropertyValue.Of(99L));
            Assert.True(HasDiagnostic(definition, "BT0504"));
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
