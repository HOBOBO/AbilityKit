using AbilityKit.Ability.Host.Extensions.Time;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Timer;
using Xunit;

namespace AbilityKit.Host.Extension.Tests;

/// <summary>TimerSchedulerModule：把 IScheduler 注册为每世界服务，并由 Host PostTick 驱动。</summary>
public sealed class TimerSchedulerModuleTests
{
    [Fact]
    public void Registers_per_world_scheduler_and_drives_it_on_post_tick()
    {
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(new StubWorldManager(), options);
        var module = new TimerSchedulerModule();
        module.Install(runtime, options);

        // 模拟世界创建：BeforeCreateWorld 钩子把 IScheduler 注册进该世界的 ServiceBuilder。
        var createOptions = new WorldCreateOptions(new WorldId("w1"), "test");
        options.BeforeCreateWorld.Invoke(createOptions);

        // 世界容器能解析出 IScheduler。
        using var container = createOptions.ServiceBuilder!.Build();
        Assert.True(container.TryResolve<IScheduler>(out var scheduler));
        Assert.NotNull(scheduler);

        // 模块自有查询也应命中同一实例。
        Assert.True(module.TryGet(createOptions.Id, out var viaModule));
        Assert.Same(scheduler, viaModule);

        // 调度一个延时任务，PostTick 驱动后触发。
        int fired = 0;
        scheduler.ScheduleDelay(() => fired++, 1f);

        options.PostTick.Invoke(0.5f);
        Assert.Equal(0, fired);

        options.PostTick.Invoke(0.5f);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Uninstall_removes_hooks_and_clears_schedulers()
    {
        var options = new HostRuntimeOptions();
        var runtime = new HostRuntime(new StubWorldManager(), options);
        var module = new TimerSchedulerModule();
        module.Install(runtime, options);

        var createOptions = new WorldCreateOptions(new WorldId("w1"), "test");
        options.BeforeCreateWorld.Invoke(createOptions);
        Assert.True(module.TryGet(createOptions.Id, out _));

        module.Uninstall(runtime, options);

        Assert.False(module.TryGet(createOptions.Id, out _));
    }

    private sealed class StubWorldManager : IWorldManager
    {
        private readonly Dictionary<WorldId, IWorld> _worlds = new();

        public IReadOnlyDictionary<WorldId, IWorld> Worlds => _worlds;

        public IWorld Create(WorldCreateOptions options) => throw new NotSupportedException();

        public bool TryGet(WorldId id, out IWorld world) => _worlds.TryGetValue(id, out world!);

        public bool Destroy(WorldId id) => _worlds.Remove(id);

        public void Tick(float deltaTime) { }

        public void DisposeAll() => _worlds.Clear();
    }
}
