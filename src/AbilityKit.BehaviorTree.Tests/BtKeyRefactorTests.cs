using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>榛戞澘 key 寮曠敤绱㈠紩涓庨噸鍛藉悕閲嶆瀯銆?/summary>
    public sealed class BtKeyRefactorTests
    {
        private static NodeRegistry BuiltinRegistry()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            return registry;
        }

        private static TreeDefinition TreeWithReferences()
        {
            // Sequence[ SetBlackboard(key=out.hold), BlackboardCompare(leftKey=self.hp) ]
            var definition = new ApiTreeBuilder()
                .Blackboard("out.hold", ValueType.Bool)
                .Blackboard("self.hp", ValueType.Int64)
                .Node("root", BuiltInNodeTypes.Sequence, "write", "check")
                .Node("write", BuiltInNodeTypes.SetBlackboard)
                .Node("check", BuiltInNodeTypes.BlackboardCompare)
                .Root("root");
            definition.Nodes[1].Properties.Set("key", PropertyValue.Of("out.hold"));
            definition.Nodes[1].Properties.Set("valueKind", PropertyValue.Of(0L));
            definition.Nodes[1].Properties.Set("constBool", PropertyValue.Of(true));
            definition.Nodes[2].Properties.Set("leftKey", PropertyValue.Of("self.hp"));
            definition.Nodes[2].Properties.Set("op", PropertyValue.Of(0L));
            definition.Nodes[2].Properties.Set("rightInt64", PropertyValue.Of(0L));
            return definition;
        }

        [Fact]
        public void FindReferences_LocatesKeyRefProperties()
        {
            var definition = TreeWithReferences();
            var registry = BuiltinRegistry();

            var holdRefs = KeyReferenceIndex.FindReferences(definition, registry, "out.hold");
            Assert.Single(holdRefs);
            Assert.Equal(("write", "key"), holdRefs[0]);

            var hpRefs = KeyReferenceIndex.FindReferences(definition, registry, "self.hp");
            Assert.Single(hpRefs);
            Assert.Equal(("check", "leftKey"), hpRefs[0]);

            Assert.Empty(KeyReferenceIndex.FindReferences(definition, registry, "no.such.key"));
        }

        [Fact]
        public void RenameKey_UpdatesSchemaAndAllReferences()
        {
            var definition = TreeWithReferences();
            var registry = BuiltinRegistry();

            var affected = KeyReferenceIndex.RenameKey(definition, registry, "self.hp", "self.health");

            Assert.Single(affected);
            Assert.Equal(("check", "leftKey"), affected[0]);
            Assert.False(definition.Blackboard.TryGetType("self.hp", out _));
            Assert.True(definition.Blackboard.TryGetType("self.health", out _));
            Assert.True(definition.Nodes[2].Properties.TryGet("leftKey", out var value));
            Assert.Equal("self.health", value.StringValue);
        }

        [Fact]
        public void RenameKey_Collision_Throws()
        {
            var definition = TreeWithReferences();
            Assert.Throws<System.InvalidOperationException>(
                () => KeyReferenceIndex.RenameKey(definition, BuiltinRegistry(), "self.hp", "out.hold"));
        }

        [Fact]
        public void RenameKey_UnknownOldName_Throws()
        {
            var definition = TreeWithReferences();
            Assert.Throws<System.InvalidOperationException>(
                () => KeyReferenceIndex.RenameKey(definition, BuiltinRegistry(), "nope", "x"));
        }

        [Fact]
        public void RenameKey_SameName_IsNoOp()
        {
            var definition = TreeWithReferences();
            var affected = KeyReferenceIndex.RenameKey(definition, BuiltinRegistry(), "self.hp", "self.hp");
            Assert.Empty(affected);
        }
    }
}

