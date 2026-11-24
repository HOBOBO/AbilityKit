using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Extensions.Time;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Timer;
using Xunit;

namespace AbilityKit.Host.Extension.Tests;

public sealed class WorldTimeLifecycleTests
{
    private static readonly WorldId Id = new("time-world");

    [Fact]
    public void FailedCreation_DoesNotPublishOrAdvancePreparedServices()
    {
        using var host = new TimeHost();
        host.NextInitializeFailure = new InvalidOperationException("initialization failed");

        Assert.Same(host.NextInitializeFailure, Assert.Throws<InvalidOperationException>(() => host.Create(Id)));
        var failed = host.LastWorld!;
        host.Runtime.Tick(0.25f);

        Assert.False(host.Clocks.TryGet(Id, out _));
        Assert.False(host.Timers.TryGet(Id, out _));
        Assert.Equal(0, failed.Clock.Frame.Value);
        Assert.Equal(0, failed.Callbacks);

        host.NextInitializeFailure = null;
        var replacement = host.Create(Id);
        Assert.NotSame(failed.Clock, replacement.Clock);
        Assert.NotSame(failed.Scheduler, replacement.Scheduler);
    }

    [Fact]
    public void DuplicateCreation_DoesNotResetLiveClockOrReplaceItsScheduler()
    {
        using var host = new TimeHost();
        var world = host.Create(Id);
        host.Runtime.Tick(0.125f);

        Assert.Throws<InvalidOperationException>(() => host.Create(Id));

        Assert.Equal(1, world.Clock.Frame.Value);
        Assert.Equal(0.125f, world.Clock.Time);
        Assert.True(host.Timers.TryGet(Id, out var scheduler));
        Assert.Same(world.Scheduler, scheduler);
        host.Runtime.Tick(0.125f);
        Assert.Equal(2, world.Clock.Frame.Value);
        Assert.Equal(1, world.Callbacks);
    }

    [Fact]
    public void ReplacedServiceRegistration_IsNotDrivenByTheModules()
    {
        using var host = new TimeHost();
        var clock = new FrameTime();
        var scheduler = new DefaultScheduler();
        host.Options.OnBeforeCreateWorld = options =>
        {
            options.ServiceBuilder.RegisterInstance<IFrameTime>(clock);
            options.ServiceBuilder.RegisterInstance<IScheduler>(scheduler);
        };

        var world = host.Create(Id);
        Assert.Same(clock, world.Clock);
        Assert.Same(scheduler, world.Scheduler);
        Assert.False(host.Clocks.TryGet(Id, out _));
        Assert.False(host.Timers.TryGet(Id, out _));
        host.Runtime.Tick(0.25f);
        Assert.Equal(0, clock.Frame.Value);
        Assert.Equal(0, world.Callbacks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetachedWorlds_NeverAdvanceAfterThrowingDestroyOrBatchShutdown(bool useFrameSync)
    {
        using var host = new TimeHost(useFrameSync);
        var single = host.Create(Id);
        single.DisposeFailure = new InvalidOperationException("destroy failed");
        Assert.Same(single.DisposeFailure, Assert.Throws<InvalidOperationException>(() => host.Runtime.DestroyWorld(Id)));
        Assert.False(host.Clocks.TryGet(Id, out _));
        Assert.False(host.Timers.TryGet(Id, out _));

        var batch = host.Create(Id);
        var other = host.Create(new WorldId("other"));
        batch.DisposeFailure = new InvalidOperationException("batch failed");
        Assert.Throws<AggregateException>(host.Manager.DisposeAll);
        Assert.False(host.Clocks.TryGet(other.Id, out _));
        Assert.False(host.Timers.TryGet(other.Id, out _));

        var replacement = host.Create(Id);
        host.Runtime.Tick(0.25f);
        foreach (var detached in new[] { single, batch, other })
        {
            Assert.Equal(0, detached.Clock.Frame.Value);
            Assert.Equal(0, detached.Callbacks);
            Assert.NotSame(detached.Clock, replacement.Clock);
            Assert.NotSame(detached.Scheduler, replacement.Scheduler);
        }
        Assert.Equal(1, replacement.Clock.Frame.Value);
        Assert.Equal(1, replacement.Callbacks);
    }

    internal sealed class TimeHost : IWorldFactory, IDisposable
    {
        private readonly FrameSyncDriverModule? _driver;

        public TimeHost(bool useFrameSync = false)
        {
            Manager = new WorldManager(this);
            Runtime = new HostRuntime(Manager, Options);
            if (useFrameSync)
            {
                _driver = new FrameSyncDriverModule();
                _driver.Install(Runtime, Options);
            }
            Clocks.Install(Runtime, Options);
            Timers.Install(Runtime, Options);
        }

        public WorldManager Manager { get; }
        public HostRuntime Runtime { get; }
        public HostRuntimeOptions Options { get; } = new();
        public ServerFrameTimeModule Clocks { get; } = new(0.125f);
        public TimerSchedulerModule Timers { get; } = new();
        public Exception? NextInitializeFailure { get; set; }
        public TimeWorld? LastWorld { get; private set; }

        public TimeWorld Create(WorldId id) => (TimeWorld)Runtime.CreateWorld(new WorldCreateOptions(id, "test"));
        public IWorld Create(WorldCreateOptions options) => LastWorld = new TimeWorld(options, NextInitializeFailure);

        public void Dispose()
        {
            Timers.Uninstall(Runtime, Options);
            Clocks.Uninstall(Runtime, Options);
            _driver?.Uninstall(Runtime, Options);
            Manager.DisposeAll();
        }
    }

    internal sealed class TimeWorld(WorldCreateOptions options, Exception? initializeFailure) : IWorld
    {
        private WorldContainer? _container;
        public WorldId Id => options.Id;
        public string WorldType => "test";
        public IWorldResolver Services => _container!;
        public IFrameTime Clock { get; private set; } = null!;
        public IScheduler Scheduler { get; private set; } = null!;
        public Exception? DisposeFailure { get; set; }
        public int Callbacks { get; private set; }

        public void Initialize()
        {
            _container = options.ServiceBuilder.Build();
            Clock = _container.Resolve<IFrameTime>();
            Scheduler = _container.Resolve<IScheduler>();
            Scheduler.ScheduleDelay(() => Callbacks++, 0.25f);
            if (initializeFailure != null) throw initializeFailure;
        }

        public void Tick(float deltaTime) { }

        public void Dispose()
        {
            if (_container != null) _container.Dispose();
            if (DisposeFailure != null) throw DisposeFailure;
        }
    }
}
