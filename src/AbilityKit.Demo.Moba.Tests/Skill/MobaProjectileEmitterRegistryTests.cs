using AbilityKit.Demo.Moba.Services.Projectile.Launch;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaProjectileEmitterRegistryTests
{
    [Fact]
    public void CreateDefault_UsesGeneratedManifest()
    {
        AppContext.SetSwitch("AbilityKit.Moba.DisableProjectileEmitterReflectionFallback", true);
        try
        {
            var registry = MobaProjectileEmitterRegistry.CreateDefault();

            Assert.Equal(1, registry.Count);
            Assert.True(registry.TryCreate(ProjectileEmitterType.Linear, out var sequence));
            Assert.IsType<RepeatProjectileLaunchSequence>(sequence);
            Assert.Equal(ProjectileEmitterType.Linear, registry.DefaultEmitterType);
            Assert.True(registry.TryCreateDefault(out var defaultSequence));
            Assert.IsType<RepeatProjectileLaunchSequence>(defaultSequence);
        }
        finally
        {
            AppContext.SetSwitch("AbilityKit.Moba.DisableProjectileEmitterReflectionFallback", false);
        }
    }

    [Fact]
    public void Register_RejectsAmbiguousPriority()
    {
        var registry = new MobaProjectileEmitterRegistry();
        registry.Register(ProjectileEmitterType.Linear, () => new RepeatProjectileLaunchSequence(), priority: 5);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(ProjectileEmitterType.Linear, () => new RepeatProjectileLaunchSequence(), priority: 5));

        Assert.Contains("Ambiguous MOBA projectile emitter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_RejectsDifferentDefaultEmitterTypes()
    {
        var registry = new MobaProjectileEmitterRegistry();
        registry.Register(
            ProjectileEmitterType.Linear,
            () => new RepeatProjectileLaunchSequence(),
            isDefault: true);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(
                (ProjectileEmitterType)999,
                () => new RepeatProjectileLaunchSequence(),
                isDefault: true));

        Assert.Contains("Ambiguous default MOBA projectile emitter", exception.Message, StringComparison.Ordinal);
    }
}
