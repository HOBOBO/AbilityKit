using AbilityKit.BehaviorTree.Authoring;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>黑板 key 引用索引与重命名重构。</summary>
    public sealed class BtKeyRefactorTests
    {
        private static BtNodeRegistry BuiltinRegistry()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);
            return registry;
        }

        private static BtTreeDefinition TreeWithReferences()
        {
            // Sequence[ SetBlackboard(key=out.hold), BlackboardCompare(leftKey=self.hp) ]
            var definition = new TreeBuilder()
                .Blackboard("out.hold", BtValueType.Bool)
                .Blackboard("self.hp", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Sequence, "write", "check")
                .Node("write", BtBuiltInNodeTypes.SetBlackboard)
                .Node("check", BtBuiltInNodeTypes.BlackboardCompare)
                .Root("root");
            definition.Nodes[1].Properties.Set(BtSetBlackboardNode.KeyProperty, BtPropertyValue.Of("out.hold"));
            definition.Nodes[1].Properties.Set(BtSetBlackboardNode.ValueKindProperty, BtPropertyValue.Of(0L));
            definition.Nodes[1].Properties.Set(BtSetBlackboardNode.ConstBoolProperty, BtPropertyValue.Of(true));
            definition.Nodes[2].Properties.Set(BtBlackboardCompareNode.LeftKeyProperty, BtPropertyValue.Of("self.hp"));
            definition.Nodes[2].Properties.Set(BtBlackboardCompareNode.OpProperty, BtPropertyValue.Of(0L));
            definition.Nodes[2].Properties.Set(BtBlackboardCompareNode.RightInt64Property, BtPropertyValue.Of(0L));
            return definition;
        }

        [Fact]
        public void FindReferences_LocatesKeyRefProperties()
        {
            var definition = TreeWithReferences();
            var registry = BuiltinRegistry();

            var holdRefs = BtKeyReferenceIndex.FindReferences(definition, registry, "out.hold");
            Assert.Single(holdRefs);
            Assert.Equal(("write", BtSetBlackboardNode.KeyProperty), holdRefs[0]);

            var hpRefs = BtKeyReferenceIndex.FindReferences(definition, registry, "self.hp");
            Assert.Single(hpRefs);
            Assert.Equal(("check", BtBlackboardCompareNode.LeftKeyProperty), hpRefs[0]);

            Assert.Empty(BtKeyReferenceIndex.FindReferences(definition, registry, "no.such.key"));
        }

        [Fact]
        public void RenameKey_UpdatesSchemaAndAllReferences()
        {
            var definition = TreeWithReferences();
            var registry = BuiltinRegistry();

            var affected = BtKeyReferenceIndex.RenameKey(definition, registry, "self.hp", "self.health");

            Assert.Single(affected);
            Assert.Equal(("check", BtBlackboardCompareNode.LeftKeyProperty), affected[0]);
            Assert.False(definition.Blackboard.TryGetType("self.hp", out _));
            Assert.True(definition.Blackboard.TryGetType("self.health", out _));
            // 引用已同步
            Assert.True(definition.Nodes[2].Properties.TryGet(BtBlackboardCompareNode.LeftKeyProperty, out var value));
            Assert.Equal("self.health", value.StringValue);
        }

        [Fact]
        public void RenameKey_Collision_Throws()
        {
            var definition = TreeWithReferences();
            Assert.Throws<System.InvalidOperationException>(
                () => BtKeyReferenceIndex.RenameKey(definition, BuiltinRegistry(), "self.hp", "out.hold"));
        }

        [Fact]
        public void RenameKey_UnknownOldName_Throws()
        {
            var definition = TreeWithReferences();
            Assert.Throws<System.InvalidOperationException>(
                () => BtKeyReferenceIndex.RenameKey(definition, BuiltinRegistry(), "nope", "x"));
        }

        [Fact]
        public void RenameKey_SameName_IsNoOp()
        {
            var definition = TreeWithReferences();
            var affected = BtKeyReferenceIndex.RenameKey(definition, BuiltinRegistry(), "self.hp", "self.hp");
            Assert.Empty(affected);
        }
    }
}
