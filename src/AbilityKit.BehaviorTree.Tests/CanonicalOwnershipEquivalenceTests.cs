using Xunit;
using ApiTreeDefinition = AbilityKit.BehaviorTree.Definition.TreeDefinition;
using ApiTreeJson = AbilityKit.BehaviorTree.Serialization.TreeJson;
#pragma warning disable CS0618 // legacy API 是本文件刻意测试的对象

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>
    /// canonical ownership 反转的门禁测试：legacy 与新 API 的定义/序列化必须行为等价。
    /// 只有在这些测试全绿的前提下，才允许把 legacy 类型的方法实现反转为“转发新 canonical 实现”。
    /// </summary>
    public sealed class CanonicalOwnershipEquivalenceTests
    {
        [Fact]
        public void CanonicalJson_LoadsIntoLegacyDefinition_PreservesHash()
        {
            var canonical = BuildCanonical();
            var canonicalJson = ApiTreeJson.Save(canonical);

            var legacyLoaded = BtTreeJson.Load(canonicalJson);

            Assert.Equal(canonical.ComputeDefinitionHash(), legacyLoaded.ComputeDefinitionHash());
        }

        [Fact]
        public void LegacyJson_LoadsIntoCanonicalDefinition_PreservesHash()
        {
            var canonical = BuildCanonical();
            var legacy = BtTreeJson.Load(ApiTreeJson.Save(canonical));

            var legacyJson = BtTreeJson.Save(legacy);
            var canonicalLoaded = ApiTreeJson.Load(legacyJson);

            Assert.Equal(canonical.ComputeDefinitionHash(), canonicalLoaded.ComputeDefinitionHash());
        }

        [Fact]
        public void CanonicalAndLegacy_HashIsStableAcrossBothSerializers()
        {
            var canonical = BuildCanonical();
            var legacy = BtTreeJson.Load(ApiTreeJson.Save(canonical));

            Assert.Equal(canonical.ComputeDefinitionHash(), legacy.ComputeDefinitionHash());
        }

        private static ApiTreeDefinition BuildCanonical()
        {
            return ApiTreeBuilder.Create("equiv.tree")
                .Node("root", "builtin.selector", (long)AbilityKit.BehaviorTree.Definition.AbortType.Both, "seq", "act")
                .Node("seq", "builtin.sequence", "cond", "act2")
                .Node("cond", "test.scriptedCondition")
                .Node("act", "test.scriptedAction")
                .Node("act2", "test.countingAction")
                .Blackboard("count", AbilityKit.BehaviorTree.Definition.ValueType.Int64, AbilityKit.BehaviorTree.Definition.PropertyValue.Of(42L))
                .Blackboard("flag", AbilityKit.BehaviorTree.Definition.ValueType.Bool, AbilityKit.BehaviorTree.Definition.PropertyValue.Of(true))
                .Root("root");
        }
    }
}
