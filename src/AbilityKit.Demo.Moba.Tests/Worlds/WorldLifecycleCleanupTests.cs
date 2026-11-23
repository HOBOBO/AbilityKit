using System;
using System.Reflection;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Console.Battle.Session;
using AbilityKit.Demo.Moba.EntitasAdapters;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Worlds;

public sealed class WorldLifecycleCleanupTests
{
    [Fact]
    public void UninitializedEntitasWorld_ReleasesContextsOnce()
    {
        var contextsFactory = new TrackingContextsFactory();
        var world = new EntitasWorld(CreateOptions(contextsFactory));

        world.Dispose();
        world.Dispose();

        Assert.Null(world.Contexts);
        Assert.Equal(1, contextsFactory.ReleaseCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EntitasCompositionFailure_ReleasesScopeAndContextsOnce(bool failRelease)
    {
        var startupFailure = new InvalidOperationException("installer failed");
        var cleanupFailure = new InvalidOperationException("contexts release failed");
        var contextsFactory = new TrackingContextsFactory { Failure = failRelease ? cleanupFailure : null };
        var options = CreateOptions(contextsFactory);
        var installer = new FailingInstaller(startupFailure);
        options.Modules.Add(installer);
        var world = new EntitasWorld(options);
        var manager = new WorldManager(new SingleWorldFactory(world));

        if (failRelease)
        {
            var combined = Assert.Throws<AggregateException>(() => manager.Create(options));
            Assert.Equal(new Exception[] { startupFailure, cleanupFailure }, combined.InnerExceptions);
        }
        else
        {
            Assert.Same(startupFailure, Assert.Throws<InvalidOperationException>(() => manager.Create(options)));
        }

        Assert.Empty(manager.Worlds);
        Assert.Null(world.Services);
        Assert.Null(world.Contexts);
        world.Dispose();
        Assert.Equal(1, installer.Service.DisposeCount);
        Assert.Equal(1, contextsFactory.ReleaseCount);
    }

    [Fact]
    public void ConsoleShutdown_ClearsHooksAndContextEvenWhenWorldReleaseFails()
    {
        var cleanupFailure = new InvalidOperationException("contexts release failed");
        var contextsFactory = new TrackingContextsFactory { Failure = cleanupFailure };
        var options = CreateOptions(contextsFactory);
        var manager = new WorldManager(new SingleWorldFactory(new EntitasWorld(options)));
        manager.Create(options);

        var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        typeof(ConsoleBattleBootstrapper).GetField("_runtimeWorldManager", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(bootstrapper, manager);
        var hooks = Assert.IsType<ConsoleSessionHooks>(bootstrapper.Context.Hooks);
        var callbackCount = 0;
        hooks.PlanBuilt += () => callbackCount++;
        hooks.InvokePlanBuilt();
        Assert.Equal(1, callbackCount);

        var combined = Assert.Throws<AggregateException>(bootstrapper.Dispose);

        Assert.Contains(combined.Flatten().InnerExceptions, failure => ReferenceEquals(failure.InnerException, cleanupFailure));
        Assert.Empty(manager.Worlds);
        Assert.Null(bootstrapper.Context.Hooks);
        hooks.InvokePlanBuilt();
        Assert.Equal(1, callbackCount);
        bootstrapper.Dispose();
        Assert.Equal(1, contextsFactory.ReleaseCount);
    }

    private static WorldCreateOptions CreateOptions(IEntitasContextsFactory factory) =>
        new WorldCreateOptions(new WorldId("lifecycle"), "lifecycle").SetEntitasContextsFactory(factory);

    private sealed class SingleWorldFactory(IWorld world) : IWorldFactory
    {
        public IWorld Create(WorldCreateOptions options) => world;
    }

    private sealed class TrackingContextsFactory : IEntitasContextsFactory
    {
        private readonly global::Entitas.IContexts _contexts = new MobaEntitasContextsFactory().Create();
        public Exception? Failure { get; init; }
        public int ReleaseCount { get; private set; }

        public global::Entitas.IContexts Create() => _contexts;

        public void Release(global::Entitas.IContexts contexts)
        {
            Assert.Same(_contexts, contexts);
            ReleaseCount++;
            if (Failure != null) throw Failure;
        }
    }

    private sealed class FailingInstaller(Exception failure) : IWorldModule, IEntitasSystemsInstaller
    {
        public ScopeProbe Service { get; } = new();

        public void Configure(WorldContainerBuilder builder) =>
            builder.Register<ScopeProbe>(WorldLifetime.Scoped, _ => Service);

        public void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldResolver resolver)
        {
            resolver.Resolve<ScopeProbe>();
            throw failure;
        }
    }

    private sealed class ScopeProbe : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
}
