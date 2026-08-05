using AbilityKit.Demo.Moba.Services.Behavior.BTree;
using BTCore.Runtime;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Behavior;

public sealed class MobaGeneratedBTreeNodeManifestTests
{
    [Fact]
    public void CreateNodeTypes_ContainsAllRuntimeNodes()
    {
        AppContext.SetSwitch("AbilityKit.Moba.DisableBTreeNodeReflectionFallback", true);
        try
        {
            var nodeTypes = MobaGeneratedBTreeNodeManifest.CreateNodeTypes();
            var reflectedNodeTypes = typeof(MobaBTreeDecision).Assembly
                .GetTypes()
                .Where(type => !type.IsAbstract
                               && typeof(BTNode).IsAssignableFrom(type)
                               && type.Namespace == typeof(MobaBTreeDecision).Namespace)
                .ToDictionary(type => type.Name, StringComparer.Ordinal);

            Assert.Equal(reflectedNodeTypes.Count, nodeTypes.Count);
            foreach (var pair in reflectedNodeTypes)
            {
                Assert.True(nodeTypes.TryGetValue(pair.Key, out var generatedType));
                Assert.Equal(pair.Value, generatedType);
            }

            Assert.Contains(nameof(MobaSelectReadySkillAction), nodeTypes.Keys);
            Assert.Contains(nameof(MobaSelectNearestEnemyAction), nodeTypes.Keys);
            Assert.Contains(nameof(MobaResolveTargetAimAction), nodeTypes.Keys);
            Assert.Contains(nameof(MobaCastSelectedSkillAction), nodeTypes.Keys);
            Assert.Contains(nameof(MobaMoveToEnemyAction), nodeTypes.Keys);
            Assert.Contains(nameof(MobaHoldPositionAction), nodeTypes.Keys);
            Assert.Contains(nameof(MobaArbitrateCombatIntentAction), nodeTypes.Keys);
        }
        finally
        {
            AppContext.SetSwitch("AbilityKit.Moba.DisableBTreeNodeReflectionFallback", false);
        }
    }
}
