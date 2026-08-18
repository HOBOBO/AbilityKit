using AbilityKit.Ability.HotReload;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using Xunit;

namespace AbilityKit.HotReload.Tests;

public sealed class HotReloadRuntimeTests : IDisposable
{
    public HotReloadRuntimeTests()
    {
        HotReloadStaticRegistry.Clear();
    }

    public void Dispose()
    {
        HotReloadStaticRegistry.Clear();
    }

    [Fact]
    public void Apply_ReplacesFeatureOnlyAfterNewFeatureIsReady()
    {
        var world = new TestWorld("shared-id");
        var firstSystem = new TrackingSystem();
        var secondSystem = new TrackingSystem();
        var first = new TestEntry(firstSystem);
        var second = new TestEntry(secondSystem);

        Assert.True(HotReloadRuntime.Apply(world, first, out var firstError), firstError);
        world.Systems.Execute();

        Assert.True(HotReloadRuntime.Apply(world, second, out var secondError), secondError);
        world.Systems.Execute();

        Assert.Equal(1, firstSystem.InitializeCount);
        Assert.Equal(1, firstSystem.ExecuteCount);
        Assert.Equal(1, firstSystem.TearDownCount);
        Assert.Equal(1, first.UninstallCount);
        Assert.Equal(1, secondSystem.InitializeCount);
        Assert.Equal(1, secondSystem.ExecuteCount);
    }

    [Fact]
    public void Apply_WhenInstallFails_PreservesCurrentFeatureAndEntry()
    {
        var world = new TestWorld("install-failure");
        var currentSystem = new TrackingSystem();
        var current = new TestEntry(currentSystem);
        Assert.True(HotReloadRuntime.Apply(world, current, out _));

        var failedSystem = new TrackingSystem();
        var failed = new TestEntry(failedSystem) { InstallError = new InvalidOperationException("install failed") };

        Assert.False(HotReloadRuntime.Apply(world, failed, out var error));
        world.Systems.Execute();

        Assert.Contains("install", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, current.UninstallCount);
        Assert.Equal(1, currentSystem.ExecuteCount);
        Assert.Equal(0, failedSystem.InitializeCount);
    }

    [Fact]
    public void Apply_WhenInitializeFails_PreservesCurrentFeatureAndCleansCandidate()
    {
        var world = new TestWorld("initialize-failure");
        var currentSystem = new TrackingSystem();
        var current = new TestEntry(currentSystem);
        Assert.True(HotReloadRuntime.Apply(world, current, out _));

        var failedSystem = new TrackingSystem { InitializeError = new InvalidOperationException("initialize failed") };
        var failed = new TestEntry(failedSystem);

        Assert.False(HotReloadRuntime.Apply(world, failed, out var error));
        world.Systems.Execute();

        Assert.Contains("initialize", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, current.UninstallCount);
        Assert.Equal(1, currentSystem.ExecuteCount);
        Assert.Equal(1, failedSystem.TearDownCount);
    }

    [Fact]
    public void Apply_WhenPreviousUninstallFails_DoesNotCommitCandidate()
    {
        var world = new TestWorld("uninstall-failure");
        var currentSystem = new TrackingSystem();
        var current = new TestEntry(currentSystem) { UninstallError = new InvalidOperationException("uninstall failed") };
        Assert.True(HotReloadRuntime.Apply(world, current, out _));

        var candidateSystem = new TrackingSystem();

        Assert.False(HotReloadRuntime.Apply(world, new TestEntry(candidateSystem), out var error));
        world.Systems.Execute();

        Assert.Contains("uninstall", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, currentSystem.ExecuteCount);
        Assert.Equal(1, candidateSystem.InitializeCount);
        Assert.Equal(1, candidateSystem.TearDownCount);
    }

    [Fact]
    public void Apply_WhenPreviousTearDownFails_DoesNotCommitCandidate()
    {
        var world = new TestWorld("teardown-failure");
        var currentSystem = new TrackingSystem { TearDownError = new InvalidOperationException("teardown failed") };
        Assert.True(HotReloadRuntime.Apply(world, new TestEntry(currentSystem), out _));
        var candidateSystem = new TrackingSystem();

        Assert.False(HotReloadRuntime.Apply(world, new TestEntry(candidateSystem), out var error));

        Assert.Contains("tear down", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, candidateSystem.InitializeCount);
        Assert.Equal(1, candidateSystem.TearDownCount);
    }

    [Fact]
    public void Apply_UsesWorldIdentityInsteadOfWorldId()
    {
        var firstWorld = new TestWorld("same-id");
        var secondWorld = new TestWorld("same-id");
        var firstSystem = new TrackingSystem();
        var secondSystem = new TrackingSystem();

        Assert.True(HotReloadRuntime.Apply(firstWorld, new TestEntry(firstSystem), out _));
        Assert.True(HotReloadRuntime.Apply(secondWorld, new TestEntry(secondSystem), out _));

        firstWorld.Systems.Execute();
        secondWorld.Systems.Execute();

        Assert.Equal(1, firstSystem.ExecuteCount);
        Assert.Equal(1, secondSystem.ExecuteCount);
    }

    [Fact]
    public void ReleaseWorld_DetachesAndCleansCurrentHotfix()
    {
        var world = new TestWorld("release");
        var system = new TrackingSystem();
        var entry = new TestEntry(system);
        Assert.True(HotReloadRuntime.Apply(world, entry, out _));

        Assert.True(HotReloadRuntime.ReleaseWorld(world, out var error), error);
        world.Systems.Execute();

        Assert.Equal(1, entry.UninstallCount);
        Assert.Equal(1, system.TearDownCount);
        Assert.Equal(0, system.ExecuteCount);
        Assert.True(HotReloadRuntime.ReleaseWorld(world, out _));
    }

    [Fact]
    public void WorldTearDown_AutomaticallyReleasesCurrentHotfix()
    {
        var world = new TestWorld("world-teardown");
        var system = new TrackingSystem();
        var entry = new TestEntry(system);
        Assert.True(HotReloadRuntime.Apply(world, entry, out _));

        world.Systems.TearDown();

        Assert.Equal(1, entry.UninstallCount);
        Assert.Equal(1, system.TearDownCount);
        Assert.True(HotReloadRuntime.ReleaseWorld(world, out _));
    }

    [Fact]
    public void Apply_WhenStaticResetFails_DoesNotInstallCandidate()
    {
        var world = new TestWorld("static-reset-failure");
        var system = new TrackingSystem();
        HotReloadStaticRegistry.Register("failure", () => throw new InvalidOperationException("reset failed"));

        Assert.False(HotReloadRuntime.Apply(world, new TestEntry(system), out var error));

        Assert.Contains("static reset", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, system.InitializeCount);
    }

    [Fact]
    public void Apply_RejectsReentrantTransitionForTheSameWorld()
    {
        var world = new TestWorld("reentrant");
        var nestedResult = true;
        string? nestedError = null;
        var entry = new TestEntry(new TrackingSystem())
        {
            OnInstall = () => nestedResult = HotReloadRuntime.Apply(
                world,
                new TestEntry(new TrackingSystem()),
                out nestedError),
        };

        Assert.True(HotReloadRuntime.Apply(world, entry, out var error), error);

        Assert.False(nestedResult);
        Assert.Contains("reentrant", nestedError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StaticRegistry_ReplacesDuplicateIdsAndReportsFailures()
    {
        var firstCount = 0;
        var replacementCount = 0;
        HotReloadStaticRegistry.Register("same", () => firstCount++);
        HotReloadStaticRegistry.Register("same", () => replacementCount++);
        HotReloadStaticRegistry.Register("failure", () => throw new InvalidOperationException("reset failed"));

        var error = Assert.Throws<AggregateException>(HotReloadStaticRegistry.ResetAll);

        Assert.Equal(0, firstCount);
        Assert.Equal(1, replacementCount);
        Assert.Single(error.InnerExceptions);
        Assert.True(HotReloadStaticRegistry.Unregister("failure"));
        Assert.False(HotReloadStaticRegistry.Unregister("failure"));
    }

    [Fact]
    public void ServiceOverlay_UsesExplicitOverrideAndRemoval()
    {
        var inner = new TestResolver();
        inner.Set(typeof(string), "base");
        var overlay = new HotfixServiceOverlay(inner);

        overlay.Set(typeof(string), "override");
        Assert.Equal("override", overlay.Resolve<string>());

        Assert.True(overlay.Remove(typeof(string)));
        Assert.Equal("base", overlay.Resolve<string>());
        Assert.Throws<ArgumentNullException>(() => overlay.Set(typeof(string), null!));
    }

    [Fact]
    public void ReleaseWorld_AggregatesCleanupFailuresAndStillDetaches()
    {
        var world = new TestWorld("release-failure");
        var system = new TrackingSystem { TearDownError = new InvalidOperationException("teardown failed") };
        var entry = new TestEntry(system) { UninstallError = new InvalidOperationException("uninstall failed") };
        Assert.True(HotReloadRuntime.Apply(world, entry, out _));

        Assert.False(HotReloadRuntime.ReleaseWorld(world, out var error));
        world.Systems.Execute();

        Assert.Contains("uninstall failed", error, StringComparison.Ordinal);
        Assert.Contains("teardown failed", error, StringComparison.Ordinal);
        Assert.Equal(0, system.ExecuteCount);
        Assert.True(HotReloadRuntime.ReleaseWorld(world, out _));
    }

    private sealed class TestEntry : IHotfixEntry
    {
        private readonly TrackingSystem _system;

        public TestEntry(TrackingSystem system)
        {
            _system = system;
        }

        public string Name => "test";
        public Exception? InstallError { get; init; }
        public Exception? UninstallError { get; init; }
        public Action? OnInstall { get; init; }
        public int UninstallCount { get; private set; }

        public void Install(Entitas.IContexts contexts, Entitas.Systems systems, IWorldResolver services)
        {
            OnInstall?.Invoke();
            if (InstallError != null)
                throw InstallError;
            systems.Add(_system);
        }

        public void Uninstall(Entitas.IContexts contexts, Entitas.Systems systems, IWorldResolver services)
        {
            UninstallCount++;
            if (UninstallError != null)
                throw UninstallError;
        }
    }

    private sealed class TrackingSystem : Entitas.IInitializeSystem, Entitas.IExecuteSystem, Entitas.ITearDownSystem
    {
        public Exception? InitializeError { get; init; }
        public Exception? TearDownError { get; init; }
        public int InitializeCount { get; private set; }
        public int ExecuteCount { get; private set; }
        public int TearDownCount { get; private set; }

        public void Initialize()
        {
            InitializeCount++;
            if (InitializeError != null)
                throw InitializeError;
        }

        public void Execute()
        {
            ExecuteCount++;
        }

        public void TearDown()
        {
            TearDownCount++;
            if (TearDownError != null)
                throw TearDownError;
        }
    }

    private sealed class TestWorld : IEntitasWorld
    {
        public TestWorld(string id)
        {
            Id = new WorldId(id);
            Contexts = new TestContexts();
            Systems = new Entitas.Systems();
            Services = new TestResolver();
        }

        public WorldId Id { get; }
        public string WorldType => "test";
        public IWorldResolver Services { get; }
        public Entitas.IContexts Contexts { get; }
        public Entitas.Systems Systems { get; }
        public void Initialize() { }
        public void Tick(float deltaTime) { }
        public void Dispose() { }
    }

    private sealed class TestContexts : Entitas.IContexts
    {
        public Entitas.IContext[] allContexts { get; } = Array.Empty<Entitas.IContext>();
    }

    private sealed class TestResolver : IWorldResolver
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Set(Type serviceType, object instance)
        {
            _services[serviceType] = instance;
        }

        public object Resolve(Type serviceType)
        {
            return _services.TryGetValue(serviceType, out var instance)
                ? instance
                : throw new InvalidOperationException($"Service not registered: {serviceType}");
        }

        public T Resolve<T>() => (T)Resolve(typeof(T));

        public bool TryResolve(Type serviceType, out object instance)
        {
            return _services.TryGetValue(serviceType, out instance!);
        }

        public bool TryResolve<T>(out T instance)
        {
            instance = default!;
            return false;
        }
    }
}
