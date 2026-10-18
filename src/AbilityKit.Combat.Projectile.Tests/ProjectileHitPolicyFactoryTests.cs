using AbilityKit.Combat.Projectile;
using Xunit;

namespace AbilityKit.Combat.Projectile.Tests;

public sealed class ProjectileHitPolicyFactoryTests
{
    private const int CustomKind = 9107;

    [Fact]
    public void RegisterFactory_creates_project_policy_with_configured_parameter()
    {
        try
        {
            ProjectileHitPolicyFactory.RegisterFactory(CustomKind, param => new TestPolicy(param));

            var policy = Assert.IsType<TestPolicy>(ProjectileHitPolicyFactory.Create(CustomKind, 23));

            Assert.Equal(23, policy.Parameter);
        }
        finally
        {
            ProjectileHitPolicyFactory.UnregisterFactory(CustomKind);
        }
    }

    [Fact]
    public void Unknown_kind_fails_closed_and_builtin_pierce_remains_available()
    {
        Assert.Same(ExitOnHitPolicy.Instance, ProjectileHitPolicyFactory.Create(9108, 0));
        Assert.Same(ProjectileHitPolicyFactory.Create(ProjectileHitPolicyKind.Pierce, -1),
            ProjectileHitPolicyFactory.Create(ProjectileHitPolicyKind.Pierce, -1));
        Assert.NotSame(ExitOnHitPolicy.Instance, ProjectileHitPolicyFactory.Create(ProjectileHitPolicyKind.Pierce, 2));
    }

    private sealed class TestPolicy : IProjectileHitPolicy
    {
        public TestPolicy(int parameter) { Parameter = parameter; }

        public int Parameter { get; }

        public bool ShouldExitOnHit(in ProjectileHitEvent hit, ref int hitsRemaining) => false;
    }
}
