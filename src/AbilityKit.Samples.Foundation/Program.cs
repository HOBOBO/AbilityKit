using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;

namespace AbilityKit.Samples.Foundation;

internal static class Program
{
    private const string WorldType = "foundation.sample";
    private const int TotalFrames = 60;
    private const float DeltaTime = 1f / 30f;

    private static void Main()
    {
        // core.Logging：默认 sink 是 NullLogSink，先接一个控制台 sink 让框架日志可见。
        Log.SetSink(new ConsoleLogSink());

        Log.Info("=== AbilityKit Foundation Starter（core + world.di）===");

        // 1. 注册 world 类型 → 工厂：把 options 携带的模块按依赖排序注入 builder，Build 出容器。
        var registry = new WorldTypeRegistry();
        registry.Register(WorldType, options =>
        {
            options.ServiceBuilder ??= new WorldContainerBuilder();
            var plan = WorldModulePlanner.Create(options.Modules.ToArray());
            foreach (var entry in plan.Entries)
            {
                options.ServiceBuilder.AddModule(entry.Module);
            }

            return new FoundationWorld(options.Id, options.WorldType, options.ServiceBuilder.Build());
        });

        // 2. WorldManager 统一管理世界的创建 / Tick / 销毁。
        var manager = new WorldManager(new RegistryWorldFactory(registry));

        // 3. 声明一个世界：服务由 GameplayModule 注册，模块装配交给 planner。
        var createOptions = new WorldCreateOptions(new WorldId("starter"), WorldType)
        {
            ServiceBuilder = new WorldContainerBuilder(),
            Modules = { new GameplayModule() }
        };
        IWorld world = manager.Create(createOptions);

        // 4. core.Eventing：从世界服务解析事件分发器并订阅生成事件。
        var events = world.Services.Resolve<EventDispatcher>();
        var subscription = events.Subscribe<EntitySpawnedEvent>(
            SampleEventIds.EntitySpawned,
            e => Log.Info($"[Event] entity.spawned id={e.EntityId}, x={e.PositionX:0.0}"));

        // 5. 宿主驱动 Tick：60 帧 @ 30fps。
        for (int frame = 1; frame <= TotalFrames; frame++)
        {
            manager.Tick(DeltaTime);
        }

        // 6. 收尾：销毁世界，触发服务 OnDeinit 与 scope / 容器逆序释放。
        subscription.Unsubscribe();
        manager.Destroy(world.Id);
        Log.Info("=== Starter 完成 ===");
    }

    /// <summary>core.Logging 的最小扩展点实现：ILogSink → 控制台。</summary>
    private sealed class ConsoleLogSink : ILogSink
    {
        public void Info(string message) => Console.WriteLine($"[INFO ] {message}");

        public void Warning(string message) => Console.WriteLine($"[WARN ] {message}");

        public void Error(string message) => Console.WriteLine($"[ERROR] {message}");

        public void Exception(Exception exception, string message = null!)
            => Console.WriteLine($"[EXCPT] {message} {exception}");
    }
}
