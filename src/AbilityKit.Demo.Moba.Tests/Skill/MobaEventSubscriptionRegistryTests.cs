using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using System.Reflection;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaEventSubscriptionRegistryTests
{
    [Fact]
    public void Constructor_UsesGeneratedExactAndPrefixMappings()
    {
        AppContext.SetSwitch("AbilityKit.Moba.DisableEventMappingReflectionFallback", true);
        try
        {
            var registry = new MobaEventSubscriptionRegistry();

            Assert.True(registry.TryGetArgsType(DamagePipelineEvents.AfterApply, out var exactType));
            Assert.Equal(typeof(DamageResult), exactType);
            Assert.True(registry.TryGetArgsType("skill.generated_manifest_probe", out var prefixType));
            Assert.Equal(typeof(SkillCastContext), prefixType);

            var declaredMappings = typeof(MobaTriggerEventAttribute).Assembly
                .GetTypes()
                .SelectMany(type => type.GetCustomAttributes<MobaTriggerEventAttribute>(inherit: false))
                .ToArray();
            Assert.Equal(22, declaredMappings.Length);
            foreach (var mapping in declaredMappings)
            {
                var eventId = mapping.IsPrefix
                    ? mapping.EventIdOrPrefix + "generated_manifest_probe"
                    : mapping.EventIdOrPrefix;
                Assert.True(registry.TryGetArgsType(eventId, out var mappedType), eventId);
                Assert.Equal(mapping.ArgsType, mappedType);
            }
        }
        finally
        {
            AppContext.SetSwitch("AbilityKit.Moba.DisableEventMappingReflectionFallback", false);
        }
    }

    [Fact]
    public void RegisterExact_RejectsConflictingDuplicate()
    {
        var registry = new MobaEventSubscriptionRegistry();
        registry.RegisterExact("test.duplicate", typeof(int));

        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterExact("test.duplicate", typeof(string)));
    }
}
