using System;
using System.Runtime.CompilerServices;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using Xunit;

namespace AbilityKit.World.DI.Tests;

public sealed class WorldLifetimeOwnershipTests
{
    [Fact]
    public void Transient_IsInitializedButNotRetainedByItsResolver()
    {
        var builder = new WorldContainerBuilder();
        builder.RegisterType<TransientService, TransientService>(WorldLifetime.Transient);
        using var container = builder.Build();
        using var scope = container.CreateScope();

        var rootInstance = ResolveWeakly(container);
        var scopedInstance = ResolveWeakly(scope);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(rootInstance.IsAlive);
        Assert.False(scopedInstance.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ResolveWeakly(IWorldResolver resolver)
    {
        var service = resolver.Resolve<TransientService>();
        Assert.True(service.Initialized);
        return new WeakReference(service);
    }

    [Fact]
    public void ScopedAliases_InitializeAndDisposeImplementationOnce()
    {
        var builder = new WorldContainerBuilder();
        builder.RegisterType<TrackedService, TrackedService>(WorldLifetime.Scoped);
        builder.RegisterAlias(typeof(IPrimary), typeof(TrackedService), WorldLifetime.Scoped);
        builder.RegisterAlias(typeof(ISecondary), typeof(TrackedService), WorldLifetime.Scoped);
        using var container = builder.Build();
        var scope = container.CreateScope();

        var service = scope.Resolve<IPrimary>();
        Assert.Same(service, scope.Resolve<ISecondary>());
        Assert.Equal(1, ((TrackedService)service).InitCount);

        scope.Dispose();

        Assert.Equal(1, ((TrackedService)service).DeinitCount);
        Assert.Equal(1, ((TrackedService)service).DisposeCount);
    }

    [Fact]
    public void ScopedFactoriesReturningSameObject_DisposeItOnce()
    {
        var service = new TrackedService();
        var builder = new WorldContainerBuilder();
        builder.Register<IPrimary>(WorldLifetime.Scoped, _ => service);
        builder.Register<ISecondary>(WorldLifetime.Scoped, _ => service);
        using var container = builder.Build();
        var scope = container.CreateScope();

        scope.Resolve<IPrimary>();
        scope.Resolve<ISecondary>();
        scope.Dispose();

        Assert.Equal(1, service.InitCount);
        Assert.Equal(1, service.DeinitCount);
        Assert.Equal(1, service.DisposeCount);
    }

    [Fact]
    public void NonOwningSingleton_LeavesChildDisposalToParent()
    {
        var builder = new WorldContainerBuilder();
        builder.RegisterType<ParentService, ParentService>(WorldLifetime.Singleton);
        builder.TryRegisterNonOwning<ChildService>(WorldLifetime.Singleton, r => r.Resolve<ParentService>().Child);
        var container = builder.Build();

        var child = container.Resolve<ChildService>();
        container.Dispose();

        Assert.Equal(1, child.DisposeCount);
    }

    [Fact]
    public void RootResolution_DetectsSingletonTransientCycle()
    {
        var builder = new WorldContainerBuilder();
        builder.Register<SingletonCycle>(WorldLifetime.Singleton, r => new SingletonCycle(r.Resolve<TransientCycle>()));
        builder.Register<TransientCycle>(WorldLifetime.Transient, r => new TransientCycle(r.Resolve<SingletonCycle>()));
        builder.Register<string>(WorldLifetime.Singleton, _ => "still usable");
        using var container = builder.Build();

        var error = Assert.Throws<InvalidOperationException>(() => container.Resolve<SingletonCycle>());
        Assert.Contains("Circular dependency", error.Message);
        Assert.Equal("still usable", container.Resolve<string>());
    }

    [Fact]
    public void RootResolution_DetectsTransientOnlyCycle()
    {
        var builder = new WorldContainerBuilder();
        builder.Register<TransientCycle>(WorldLifetime.Transient, r => new TransientCycle(r.Resolve<SingletonCycle>()));
        builder.Register<SingletonCycle>(WorldLifetime.Transient, r => new SingletonCycle(r.Resolve<TransientCycle>()));
        using var container = builder.Build();

        var error = Assert.Throws<InvalidOperationException>(() => container.Resolve<TransientCycle>());
        Assert.Contains("Circular dependency", error.Message);
    }

    [Fact]
    public void RootConstructorInjection_ReportsCircularDependency()
    {
        var builder = new WorldContainerBuilder();
        builder.RegisterType<SingletonCycle, SingletonCycle>(WorldLifetime.Singleton);
        builder.RegisterType<TransientCycle, TransientCycle>(WorldLifetime.Transient);
        using var container = builder.Build();

        var error = Assert.Throws<InvalidOperationException>(() => container.Resolve<SingletonCycle>());
        Assert.Contains("Circular dependency", error.Message);
    }

    [Fact]
    public void RootResolution_AllowsSameServiceTypeFromAnotherContainer()
    {
        var innerBuilder = new WorldContainerBuilder();
        innerBuilder.Register<string>(WorldLifetime.Singleton, _ => "inner");
        using var inner = innerBuilder.Build();

        var outerBuilder = new WorldContainerBuilder();
        outerBuilder.Register<string>(WorldLifetime.Singleton, _ => inner.Resolve<string>());
        using var outer = outerBuilder.Build();

        Assert.Equal("inner", outer.Resolve<string>());
    }

    private sealed class TransientService : IWorldInitializable
    {
        public bool Initialized { get; private set; }
        public void OnInit(IWorldResolver services) => Initialized = true;
        public void Dispose() { }
    }

    private interface IPrimary { }
    private interface ISecondary { }

    private sealed class TrackedService : IPrimary, ISecondary, IWorldInitializable, IWorldDeinitializable, IDisposable
    {
        public int InitCount { get; private set; }
        public int DeinitCount { get; private set; }
        public int DisposeCount { get; private set; }
        public void OnInit(IWorldResolver services) => InitCount++;
        public void OnDeinit(IWorldResolver services) => DeinitCount++;
        public void Dispose() => DisposeCount++;
    }

    private sealed class ParentService : IDisposable
    {
        public ChildService Child { get; } = new ChildService();
        public void Dispose() => Child.Dispose();
    }

    private sealed class ChildService : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    private sealed class SingletonCycle
    {
        public SingletonCycle(TransientCycle dependency) { }
    }

    private sealed class TransientCycle
    {
        public TransientCycle(SingletonCycle dependency) { }
    }
}
