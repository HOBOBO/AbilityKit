using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaSnapshotEmitterRegistryTests
{
    [Fact]
    public void GeneratedManifest_ContainsAllRuntimeEmitters()
    {
        var registry = new MobaSnapshotEmitterRegistry();

        var generatedCount = MobaGeneratedSnapshotEmitterManifest.Register(registry);

        Assert.Equal(11, generatedCount);
        Assert.Equal(11, registry.Count);

        var runtimeAssembly = typeof(MobaSnapshotEmitterRegistry).Assembly;
        var reflectedEmitters = runtimeAssembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IMobaSnapshotEmitter).IsAssignableFrom(type))
            .Select(type => (Type: type, Attribute: type.GetCustomAttributes(typeof(MobaSnapshotEmitterAttribute), false)
                .Cast<MobaSnapshotEmitterAttribute>()
                .FirstOrDefault()))
            .Where(item => item.Attribute != null)
            .ToArray();
        Assert.Equal(reflectedEmitters.Length, generatedCount);
        foreach (var emitter in reflectedEmitters)
        {
            Assert.True(registry.ContainsRegistration(emitter.Attribute!.Priority, emitter.Type));
        }
    }

    [Fact]
    public void CreateDefault_WorksWithReflectionFallbackDisabled()
    {
        AppContext.SetSwitch("AbilityKit.Moba.DisableSnapshotEmitterReflectionFallback", true);
        try
        {
            var registry = MobaSnapshotEmitterRegistry.CreateDefault();

            Assert.True(registry.Count >= 11);
            Assert.True(registry.ContainsRegistration(999, typeof(ExternalSnapshotEmitter)));
        }
        finally
        {
            AppContext.SetSwitch("AbilityKit.Moba.DisableSnapshotEmitterReflectionFallback", false);
        }
    }

    [MobaSnapshotEmitter(999)]
    private sealed class ExternalSnapshotEmitter : IMobaSnapshotEmitter
    {
        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }
    }
}
