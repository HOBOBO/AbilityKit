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

    [Theory]
    [InlineData(WorldLifetime.Scoped)]
    [InlineData(WorldLifetime.Transient)]
    public void ScopeResolution_AllowsSameContractFromAnotherScope(WorldLifetime lifetime)
    {
        WorldScope outer = null!;
        WorldScope inner = null!;
        var builder = new WorldContainerBuilder();
        builder.Register<string>(lifetime, resolver => ReferenceEquals(resolver, outer)
            ? $"outer/{inner.Resolve<string>()}"
            : "inner");
        using var container = builder.Build();
        using var outerScope = outer = container.CreateScope();
        using var innerScope = inner = container.CreateScope();

        Assert.Equal("outer/inner", outerScope.Resolve<string>());
        Assert.Equal("inner", innerScope.Resolve<string>());
    }

    [Fact]
    public void ScopeResolution_RejectsCycleThatReturnsToTheOriginalScope()
    {
        WorldScope outer = null!;
        WorldScope inner = null!;
        var innerFactoryRan = false;
        var builder = new WorldContainerBuilder();
        builder.Register<string>(WorldLifetime.Scoped, resolver =>
        {
            if (ReferenceEquals(resolver, outer)) return inner.Resolve<string>();
            innerFactoryRan = true;
            return outer.Resolve<string>();
        });
        builder.Register<int>(WorldLifetime.Scoped, _ => 42);
        using var container = builder.Build();
        using var outerScope = outer = container.CreateScope();
        using var innerScope = inner = container.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(() => outerScope.Resolve<string>());
        Assert.Contains("Circular dependency detected.", error.Message);
        Assert.True(innerFactoryRan);
        Assert.Equal(42, outerScope.Resolve<int>());
    }

    [Theory]
    [InlineData(WorldLifetime.Singleton)]
    [InlineData(WorldLifetime.Scoped)]
    public void FailedInitialization_IsNotCached_AndTheNextExplicitResolveCanRetry(WorldLifetime lifetime)
    {
        var service = new InitializationProbe();
        service.Initialize = _ =>
        {
            if (service.InitializeCount == 1) throw new InvalidOperationException("initialization failed");
        };
        var factoryCalls = 0;
        var builder = new WorldContainerBuilder();
        builder.Register<InitializationProbe>(lifetime, _ =>
        {
            factoryCalls++;
            return service;
        });
        builder.Register<IInitializationProbe>(lifetime, _ => service);
        using var container = builder.Build();
        using var scope = container.CreateScope();
        IWorldResolver resolver = lifetime == WorldLifetime.Singleton ? container : scope;
        var owner = (IDisposable)resolver;

        var error = Assert.Throws<InvalidOperationException>(() => resolver.Resolve<InitializationProbe>());
        Assert.Equal("initialization failed", error.Message);
        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, service.InitializeCount);
        Assert.Equal(0, service.DeinitializeCount);
        Assert.Equal(0, service.DisposeCount);

        Assert.Same(service, resolver.Resolve<InitializationProbe>());
        Assert.Same(service, resolver.Resolve<IInitializationProbe>());
        Assert.Same(service, resolver.Resolve<InitializationProbe>());
        Assert.Equal(2, factoryCalls);
        Assert.Equal(2, service.InitializeCount);

        owner.Dispose();
        Assert.Equal(1, service.DeinitializeCount);
        Assert.Equal(1, service.DisposeCount);
    }

    [Theory]
    [InlineData(WorldLifetime.Singleton)]
    [InlineData(WorldLifetime.Scoped)]
    public void Initialization_DoesNotPublishItselfThroughAnotherOwnedContract(WorldLifetime lifetime)
    {
        var service = new InitializationProbe();
        service.Initialize = resolver => resolver.Resolve<IInitializationProbe>();
        var builder = new WorldContainerBuilder();
        builder.Register<InitializationProbe>(lifetime, _ => service);
        builder.Register<IInitializationProbe>(lifetime, _ => service);
        using var container = builder.Build();
        using var scope = container.CreateScope();
        IWorldResolver resolver = lifetime == WorldLifetime.Singleton ? container : scope;

        var error = Assert.Throws<InvalidOperationException>(() => resolver.Resolve<InitializationProbe>());
        Assert.Contains("Circular dependency detected.", error.Message);
        Assert.Equal(1, service.InitializeCount);

        service.Initialize = null;
        Assert.Same(service, resolver.Resolve<IInitializationProbe>());
        Assert.Equal(2, service.InitializeCount);
        Assert.Same(service, resolver.Resolve<InitializationProbe>());
        Assert.Equal(2, service.InitializeCount);
    }

    [Theory]
    [InlineData(WorldLifetime.Singleton)]
    [InlineData(WorldLifetime.Scoped)]
    public void FailedInitialization_DoesNotTransferCleanupToTheOwner(WorldLifetime lifetime)
    {
        var service = new InitializationProbe
        {
            Initialize = _ => throw new InvalidOperationException("initialization failed")
        };
        var dependency = new InitializationProbe();
        var builder = new WorldContainerBuilder();
        builder.Register<InitializationProbe>(lifetime, resolver =>
        {
            _ = resolver.Resolve<IInitializationProbe>();
            return service;
        });
        builder.Register<IInitializationProbe>(lifetime, _ => dependency);
        using var container = builder.Build();
        using var scope = container.CreateScope();
        IWorldResolver resolver = lifetime == WorldLifetime.Singleton ? container : scope;

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve<InitializationProbe>());
        ((IDisposable)resolver).Dispose();

        Assert.Equal(1, service.InitializeCount);
        Assert.Equal(0, service.DeinitializeCount);
        Assert.Equal(0, service.DisposeCount);
        Assert.Equal(1, dependency.InitializeCount);
        Assert.Equal(1, dependency.DeinitializeCount);
        Assert.Equal(1, dependency.DisposeCount);
    }

    private interface IInitializationProbe { }

    private sealed class InitializationProbe : IInitializationProbe, IWorldInitializable, IWorldDeinitializable
    {
        public Action<IWorldResolver>? Initialize { get; set; }
        public int InitializeCount { get; private set; }
        public int DeinitializeCount { get; private set; }
        public int DisposeCount { get; private set; }

        public void OnInit(IWorldResolver services)
        {
            InitializeCount++;
            Initialize?.Invoke(services);
        }

        public void OnDeinit(IWorldResolver services) => DeinitializeCount++;

        public void Dispose() => DisposeCount++;
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
