using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>包外扩展点：描述符字段种类（枚举/黑板 key 引用）、校验、编辑器渲染数据源。</summary>
    public sealed class BtExtensionPointsTests
    {
        [Fact]
        public void BuiltinDescriptors_ExposeEnumAndKeyRefFields()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);

            // 组合节点 abortType 是枚举
            var sequence = registry.Descriptors.Single(d => d.TypeId == BtBuiltInNodeTypes.Sequence);
            var abort = sequence.PropertySchema.Single(f => f.Name == BtCompositeNode.AbortTypeProperty);
            Assert.Equal(BtPropertyFieldKind.Enum, abort.Kind);
            Assert.Equal(4, abort.Options.Count);

            // 黑板比较 leftKey 是 key 引用
            var compare = registry.Descriptors.Single(d => d.TypeId == BtBuiltInNodeTypes.BlackboardCompare);
            Assert.Contains(compare.PropertySchema, f =>
                f.Name == BtBlackboardCompareNode.LeftKeyProperty && f.Kind == BtPropertyFieldKind.BlackboardKeyRef);
            Assert.Contains(compare.PropertySchema, f =>
                f.Name == BtBlackboardCompareNode.OpProperty && f.Kind == BtPropertyFieldKind.Enum);
        }

        [Fact]
        public void Validator_RejectsEnumOutOfRange()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);

            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Sequence, "a")
                .Node("a", ScriptedAction)
                .Root("root");
            definition.Nodes[0].Properties.Set(BtCompositeNode.AbortTypeProperty, BtPropertyValue.Of(99L));

            var errors = BtTreeValidator.Validate(definition, registry);
            Assert.Contains(errors, e => e.Contains("enum index out of range"));
        }

        [Fact]
        public void Validator_RejectsUndeclaredKeyRef()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);

            // SetBlackboard 目标 key 引用未声明的黑板 key
            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.SetBlackboard)
                .Root("root");
            definition.Nodes[0].Properties.Set(BtSetBlackboardNode.KeyProperty, BtPropertyValue.Of("undeclared.key"));

            var errors = BtTreeValidator.Validate(definition, registry);
            Assert.Contains(errors, e => e.Contains("undeclared blackboard key 'undeclared.key'"));
        }

        [Fact]
        public void Validator_AcceptsDeclaredKeyRef()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);

            var definition = new TreeBuilder()
                .Blackboard("out.hold", BtValueType.Bool)
                .Node("root", BtBuiltInNodeTypes.SetBlackboard)
                .Root("root");
            definition.Nodes[0].Properties.Set(BtSetBlackboardNode.KeyProperty, BtPropertyValue.Of("out.hold"));
            definition.Nodes[0].Properties.Set(BtSetBlackboardNode.ValueKindProperty, BtPropertyValue.Of(0L));
            definition.Nodes[0].Properties.Set(BtSetBlackboardNode.ConstBoolProperty, BtPropertyValue.Of(true));

            Assert.Empty(BtTreeValidator.Validate(definition, registry));
        }

        // 包外扩展示例：在测试程序集（模拟外部项目）里定义带 enum + keyRef 的节点，
        // 经 ScanAssembly 后描述符携带完整 schema（编辑器据此渲染下拉，无需编辑器代码）。
        [BtNodeType("ext.mood.action", "情绪动作", "扩展示例", BtNodeKind.Action)]
        public sealed class ExternalMoodActionNode : BtActionNodeBase, BtNodeDescriptorProvider
        {
            public override BtNodeState OnTick(BtExecutionContext context) => BtNodeState.Success;

            public BtNodeDescriptor BuildDescriptor(BtNodeTypeAttribute attribute) => new(
                attribute.NodeTypeId, attribute.DisplayName, attribute.Category, BtNodeKind.Action, 0, 0,
                () => new ExternalMoodActionNode(),
                new BtPropertyField[]
                {
                    BtPropertyField.Enum("mood", new[] { "平静", "警觉", "狂怒" }, 1, "情绪状态"),
                    BtPropertyField.KeyRef("reportKey", "写入结果的黑板 key"),
                });
        }

        [Fact]
        public void ScanAssembly_CarriesEnumAndKeyRefSchema()
        {
            var registry = new BtNodeRegistry();
            registry.ScanAssembly(typeof(BtExtensionPointsTests).Assembly);

            Assert.True(registry.TryGetDescriptor("ext.mood.action", out var descriptor));
            var mood = descriptor.PropertySchema[0];
            Assert.Equal(BtPropertyFieldKind.Enum, mood.Kind);
            Assert.Equal(3, mood.Options.Count);
            Assert.Equal(1L, mood.Default!.Int64Value);
            Assert.Equal(BtPropertyFieldKind.BlackboardKeyRef, descriptor.PropertySchema[1].Kind);
        }
    }
}
