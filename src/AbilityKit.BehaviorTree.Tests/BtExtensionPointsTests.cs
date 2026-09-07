using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>鍖呭鎵╁睍鐐癸細鎻忚堪绗﹀瓧娈电绫伙紙鏋氫妇/榛戞澘 key 寮曠敤锛夈€佹牎楠屻€佺紪杈戝櫒娓叉煋鏁版嵁婧愩€?/summary>
    public sealed class BtExtensionPointsTests
    {
        [Fact]
        public void BuiltinDescriptors_ExposeEnumAndKeyRefFields()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            var sequence = registry.Descriptors.Single(d => d.TypeId == BuiltInNodeTypes.Sequence);
            var abort = sequence.PropertySchema.Single(f => f.Name == CompositeNode.AbortTypeProperty);
            Assert.Equal(PropertyFieldKind.Enum, abort.Kind);
            Assert.Equal(4, abort.Options.Count);

            // 榛戞澘姣旇緝 leftKey 鏄?key 寮曠敤
            var compare = registry.Descriptors.Single(d => d.TypeId == BuiltInNodeTypes.BlackboardCompare);
            Assert.Contains(compare.PropertySchema, f =>
                f.Name == BlackboardCompareNode.LeftKeyProperty && f.Kind == PropertyFieldKind.BlackboardKeyRef);
            Assert.Contains(compare.PropertySchema, f =>
                f.Name == BlackboardCompareNode.OpProperty && f.Kind == PropertyFieldKind.Enum);
        }

        [Fact]
        public void Validator_RejectsEnumOutOfRange()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);

            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "a")
                .Node("a", ScriptedAction)
                .Root("root");
            definition.Nodes[0].Properties.Set(CompositeNode.AbortTypeProperty, PropertyValue.Of(99L));

            var errors = TreeValidator.Validate(definition, registry);
            Assert.Contains(errors, e => e.Contains("enum index out of range"));
        }

        [Fact]
        public void Validator_RejectsUndeclaredKeyRef()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);

            // SetBlackboard 鐩爣 key 寮曠敤鏈０鏄庣殑榛戞澘 key
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.SetBlackboard)
                .Root("root");
            definition.Nodes[0].Properties.Set(SetBlackboardNode.KeyProperty, PropertyValue.Of("undeclared.key"));

            var errors = TreeValidator.Validate(definition, registry);
            Assert.Contains(errors, e => e.Contains("undeclared blackboard key 'undeclared.key'"));
        }

        [Fact]
        public void Validator_AcceptsDeclaredKeyRef()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);

            var definition = new TreeBuilder()
                .Blackboard("out.hold", TreeValueType.Bool)
                .Node("root", BuiltInNodeTypes.SetBlackboard)
                .Root("root");
            definition.Nodes[0].Properties.Set(SetBlackboardNode.KeyProperty, PropertyValue.Of("out.hold"));
            definition.Nodes[0].Properties.Set(SetBlackboardNode.ValueKindProperty, PropertyValue.Of(0L));
            definition.Nodes[0].Properties.Set(SetBlackboardNode.ConstBoolProperty, PropertyValue.Of(true));

            Assert.Empty(TreeValidator.Validate(definition, registry));
        }

        // 鍖呭鎵╁睍绀轰緥锛氬湪娴嬭瘯绋嬪簭闆嗭紙妯℃嫙澶栭儴椤圭洰锛夐噷瀹氫箟甯?enum + keyRef 鐨勮妭鐐癸紝
        [NodeType("ext.mood.action", "Mood Action", "Extension Example", NodeKind.Action)]
        public sealed class ExternalMoodActionNode : ActionNodeBase, NodeDescriptorProvider
        {
            public override NodeState OnTick(ExecutionContext context) => NodeState.Success;

            public NodeDescriptor BuildDescriptor(NodeTypeAttribute attribute) => new(
                attribute.NodeTypeId, attribute.DisplayName, attribute.Category, NodeKind.Action, 0, 0,
                () => new ExternalMoodActionNode(),
                new PropertyField[]
                {
                    PropertyField.Enum("mood", new[] { "Calm", "Alert", "Enraged" }, 1, "Mood state"),
                    PropertyField.KeyRef("reportKey", "Result blackboard key"),
                });
        }

        [Fact]
        public void ScanAssembly_CarriesEnumAndKeyRefSchema()
        {
            var registry = new NodeRegistry();
            registry.ScanAssembly(typeof(BtExtensionPointsTests).Assembly);

            Assert.True(registry.TryGetDescriptor("ext.mood.action", out var descriptor));
            var mood = descriptor.PropertySchema[0];
            Assert.Equal(PropertyFieldKind.Enum, mood.Kind);
            Assert.Equal(3, mood.Options.Count);
            Assert.Equal(1L, mood.Default!.Int64Value);
            Assert.Equal(PropertyFieldKind.BlackboardKeyRef, descriptor.PropertySchema[1].Kind);
        }
    }
}
