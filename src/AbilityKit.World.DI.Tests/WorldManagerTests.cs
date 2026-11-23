using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using Xunit;

namespace AbilityKit.World.DI.Tests;

public sealed class WorldManagerTests
{
    [Fact]
    public void Create_InitializesBeforePublishing_AndRejectsDuplicateBeforeCallingFactory()
    {
        var id = new WorldId("battle");
        var world = new TestWorld(id);
        var factoryCalls = 0;
        var manager = new WorldManager(new TestFactory(_ =>
        {
            factoryCalls++;
            return world;
        }));
        world.OnInitialize = () => Assert.False(manager.TryGet(id, out _));
        var options = new WorldCreateOptions(id, world.WorldType);

        Assert.Same(world, manager.Create(options));
        Assert.True(manager.TryGet(id, out var registered));
        Assert.Same(world, registered);
        Assert.Throws<InvalidOperationException>(() => manager.Create(options));
        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, world.InitializeCount);
    }

    [Fact]
    public void Destroy_RemovesTheWorldBeforeDisposal_AndDoesNotRetryAFailedDisposal()
    {
        var id = new WorldId("battle");
        var world = new TestWorld(id);
        var manager = new WorldManager(new TestFactory(_ => world));
        manager.Create(new WorldCreateOptions(id, world.WorldType));
        var failure = new InvalidOperationException("dispose failed");
        world.OnDispose = () =>
        {
            Assert.False(manager.TryGet(id, out _));
            Assert.False(manager.Destroy(id));
            throw failure;
        };

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => manager.Destroy(id)));
        manager.DisposeAll();

        Assert.Empty(manager.Worlds);
        Assert.Equal(1, world.DisposeCount);
    }

    [Fact]
    public void Tick_ContinuesAfterOneWorldFails()
    {
        var ticked = new List<WorldId>();
        var manager = new WorldManager(new TestFactory(options => new TestWorld(options.Id)
        {
            OnTick = _ =>
            {
                ticked.Add(options.Id);
                if (ticked.Count == 1) throw new InvalidOperationException("tick failed");
            }
        }));
        var first = new WorldId("first");
        var second = new WorldId("second");
        manager.Create(new WorldCreateOptions(first, "test"));
        manager.Create(new WorldCreateOptions(second, "test"));

        manager.Tick(0.05f);

        Assert.Equal(2, ticked.Count);
        Assert.Contains(first, ticked);
        Assert.Contains(second, ticked);
        Assert.Equal(2, manager.Worlds.Count);
    }

    [Fact]
    public void Create_PropagatesFactoryFailure_AndAllowsRetry()
    {
        var id = new WorldId("battle");
        var world = new TestWorld(id);
        var attempts = 0;
        var failure = new InvalidOperationException("factory failed");
        var manager = new WorldManager(new TestFactory(_ => ++attempts == 1 ? throw failure : world));
        var options = new WorldCreateOptions(id, "test");

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => manager.Create(options)));
        Assert.Empty(manager.Worlds);
        Assert.Same(world, manager.Create(options));
    }

    [Fact]
    public void Create_RejectsNullFactoryResult()
    {
        var manager = new WorldManager(new TestFactory(_ => null!));
        var failure = Assert.Throws<InvalidOperationException>(() =>
            manager.Create(new WorldCreateOptions(new WorldId("expected"), "test")));

        Assert.Contains("returned null", failure.Message);
        Assert.Empty(manager.Worlds);
    }

    [Fact]
    public void Create_RejectsUnexpectedIdentityBeforeInitialize_AndDisposesCandidate()
    {
        var candidate = new TestWorld(new WorldId("unexpected"));
        var manager = new WorldManager(new TestFactory(_ => candidate));
        var failure = Assert.Throws<InvalidOperationException>(() =>
            manager.Create(new WorldCreateOptions(new WorldId("expected"), "test")));

        Assert.Contains("requested=expected, actual=unexpected", failure.Message);
        Assert.Empty(manager.Worlds);
        Assert.Equal(0, candidate.InitializeCount);
        Assert.Equal(1, candidate.DisposeCount);
    }

    [Fact]
    public void Create_DoesNotDisposeAlreadyRegisteredInstanceReturnedByFactory()
    {
        var existing = new TestWorld(new WorldId("existing"));
        var manager = new WorldManager(new TestFactory(_ => existing));
        manager.Create(new WorldCreateOptions(existing.Id, "test"));

        Assert.Throws<InvalidOperationException>(() =>
            manager.Create(new WorldCreateOptions(new WorldId("different"), "test")));

        Assert.Same(existing, manager.Worlds[existing.Id]);
        Assert.Equal(1, existing.InitializeCount);
        Assert.Equal(0, existing.DisposeCount);
    }

    [Fact]
    public void Create_ReentrantCallCannotRepublishOrDisposePendingInstance()
    {
        var world = new TestWorld(new WorldId("pending"));
        var options = new WorldCreateOptions(world.Id, "test");
        var manager = new WorldManager(new TestFactory(_ => world));
        world.OnInitialize = () =>
        {
            Assert.Throws<InvalidOperationException>(() => manager.Create(options));
            Assert.Throws<InvalidOperationException>(() =>
                manager.Create(new WorldCreateOptions(new WorldId("different"), "test")));
            Assert.Empty(manager.Worlds);
        };

        Assert.Same(world, manager.Create(options));
        Assert.Equal(1, world.InitializeCount);
        Assert.Equal(0, world.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_DisposesInitializationFailureOnce_AndPreservesErrors(bool cleanupThrows)
    {
        var id = new WorldId("battle");
        var startupFailure = new InvalidOperationException("initialize failed");
        var cleanupFailure = new NotSupportedException("cleanup failed");
        var world = new TestWorld(id)
        {
            OnInitialize = () => throw startupFailure,
            OnDispose = cleanupThrows ? () => throw cleanupFailure : null
        };
        var replacement = new TestWorld(id);
        var attempts = 0;
        var options = new WorldCreateOptions(id, "test");
        var manager = new WorldManager(new TestFactory(_ => ++attempts == 1 ? world : replacement));

        if (cleanupThrows)
        {
            var combined = Assert.Throws<AggregateException>(() => manager.Create(options));
            Assert.Equal(new Exception[] { startupFailure, cleanupFailure }, combined.InnerExceptions);
        }
        else
        {
            Assert.Same(startupFailure, Assert.Throws<InvalidOperationException>(() => manager.Create(options)));
        }

        Assert.Empty(manager.Worlds);
        Assert.Equal(1, world.DisposeCount);
        Assert.Same(replacement, manager.Create(options));
        manager.DisposeAll();
        Assert.Equal(1, world.DisposeCount);
    }

    [Fact]
    public void DisposeAll_DetachesBeforeCallbacks_ReportsEveryFailure_AndCanBeReused()
    {
        var factoryCalls = 0;
        var manager = new WorldManager(new TestFactory(options =>
        {
            factoryCalls++;
            return new TestWorld(options.Id);
        }));
        var first = (TestWorld)manager.Create(new WorldCreateOptions(new WorldId("first"), "test"));
        var middle = (TestWorld)manager.Create(new WorldCreateOptions(new WorldId("middle"), "test"));
        var last = (TestWorld)manager.Create(new WorldCreateOptions(new WorldId("last"), "test"));
        var firstFailure = new InvalidOperationException("first failed");
        var lastFailure = new NotSupportedException("last failed");
        first.OnDispose = () => throw firstFailure;
        middle.OnDispose = () =>
        {
            Assert.Empty(manager.Worlds);
            Assert.False(manager.Destroy(first.Id));
            manager.DisposeAll();
            Assert.Throws<InvalidOperationException>(() =>
                manager.Create(new WorldCreateOptions(new WorldId("new"), "test")));
        };
        last.OnDispose = () => throw lastFailure;

        var combined = Assert.Throws<AggregateException>(manager.DisposeAll);
        Assert.Equal(2, combined.InnerExceptions.Count);
        Assert.Contains(combined.InnerExceptions, error =>
            error.Message.Contains("worldId=first", StringComparison.Ordinal) && ReferenceEquals(error.InnerException, firstFailure));
        Assert.Contains(combined.InnerExceptions, error =>
            error.Message.Contains("worldId=last", StringComparison.Ordinal) && ReferenceEquals(error.InnerException, lastFailure));
        Assert.Equal(3, factoryCalls);
        Assert.Empty(manager.Worlds);
        manager.DisposeAll();
        Assert.All(new[] { first, middle, last }, world => Assert.Equal(1, world.DisposeCount));
        Assert.NotSame(first, manager.Create(new WorldCreateOptions(first.Id, "test")));
    }

    private sealed class TestFactory(Func<WorldCreateOptions, IWorld> factory) : IWorldFactory
    {
        public IWorld Create(WorldCreateOptions options) => factory(options);
    }

    private sealed class TestWorld(WorldId id) : IWorld
    {
        public WorldId Id { get; } = id;
        public string WorldType => "test";
        public IWorldResolver Services => throw new NotSupportedException();
        public Action? OnInitialize { get; set; }
        public Action<float>? OnTick { get; set; }
        public Action? OnDispose { get; set; }
        public int InitializeCount { get; private set; }
        public int DisposeCount { get; private set; }

        public void Initialize()
        {
            InitializeCount++;
            OnInitialize?.Invoke();
        }

        public void Tick(float deltaTime) => OnTick?.Invoke(deltaTime);

        public void Dispose()
        {
            DisposeCount++;
            OnDispose?.Invoke();
        }
    }
}
