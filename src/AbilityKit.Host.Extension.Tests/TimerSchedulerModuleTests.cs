using AbilityKit.Ability.World.Abstractions;
using Xunit;

namespace AbilityKit.Host.Extension.Tests;

public sealed class TimerSchedulerModuleTests
{
    [Fact]
    public void RegisteredWorldScheduler_IsDrivenOncePerHostTick()
    {
        using var host = new WorldTimeLifecycleTests.TimeHost();
        var world = host.Create(new WorldId("timer"));

        Assert.True(host.Timers.TryGet(world.Id, out var scheduler));
        Assert.Same(world.Scheduler, scheduler);
        host.Runtime.Tick(0.125f);
        Assert.Equal(0, world.Callbacks);
        host.Runtime.Tick(0.125f);
        Assert.Equal(1, world.Callbacks);
    }

    [Fact]
    public void UninstallAndReinstall_DoesNotResumeExistingWorldScheduler()
    {
        using var host = new WorldTimeLifecycleTests.TimeHost();
        var world = host.Create(new WorldId("timer"));

        host.Timers.Uninstall(host.Runtime, host.Options);
        Assert.False(host.Timers.TryGet(world.Id, out _));
        host.Timers.Install(host.Runtime, host.Options);
        host.Runtime.Tick(0.25f);
        Assert.False(host.Timers.TryGet(world.Id, out _));
        Assert.Equal(0, world.Callbacks);
    }
}
