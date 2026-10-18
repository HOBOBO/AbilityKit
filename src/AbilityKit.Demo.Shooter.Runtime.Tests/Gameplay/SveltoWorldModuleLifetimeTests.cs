using AbilityKit.Ability.World.DI;
using AbilityKit.World.Svelto;
using Svelto.ECS;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Gameplay;

public sealed class SveltoWorldModuleLifetimeTests
{
    [Fact]
    public void ChildResourcesAreExposedWithoutContainerOwnership()
    {
        var builder = new WorldContainerBuilder().AddModule(new SveltoWorldModule());
        var container = builder.Build();

        var context = container.Resolve<SveltoWorldContext>();
        Assert.Same(context, container.Resolve<ISveltoWorldContext>());
        Assert.Same(context.EnginesRoot, container.Resolve<EnginesRoot>());
        Assert.Equal(WorldServiceOwnership.External, container.GetDescriptor(typeof(EnginesRoot)).Ownership);

        container.Dispose();

        Assert.False(context.EnginesRoot.IsValid());
    }
}
