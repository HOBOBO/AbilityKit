#nullable enable

using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.World
{
    /// <summary>
    /// 演示 World 从注册、创建、初始化、Tick 到销毁的最小生命周期。
    /// </summary>
    [Sample(601, "world", "lifecycle", "package-api", "web", "deterministic", "fixed-frame")]
    public sealed class WorldLifecycle : SampleBase
    {
        public override string Title => "World Lifecycle";
        public override string Description => "使用 WorldTypeRegistry 与 WorldManager 管理 World 生命周期";
        public override SampleCategory Category => SampleCategory.World;

        protected override void OnRun()
        {
            var registry = new WorldTypeRegistry();
            registry.Register("BattleWorld", CreateWorld);
            KeyValue("WorldLifecycle.RegisteredType", "BattleWorld");

            var worldManager = new WorldManager(new RegistryWorldFactory(registry));

            Section("注册并创建 World");
            var world = worldManager.Create(new WorldCreateOptions
            {
                Id = new WorldId("battle-001"),
                WorldType = "BattleWorld"
            });

            KeyValue("WorldId", world.Id.Value);
            KeyValue("WorldType", world.WorldType);
            KeyValue("Initialized", ((LifecycleWorld)world).Initialized.ToString());
            KeyValue("WorldLifecycle.Created", $"id={world.Id.Value},type={world.WorldType}");
            KeyValue("WorldLifecycle.Initialized", ((LifecycleWorld)world).Initialized.ToString());

            Divider();
            Section("固定帧驱动 WorldManager.Tick");
            for (var frame = 1; frame <= 3; frame++)
            {
                worldManager.Tick(0.05f);
                var clock = world.Services.Resolve<IWorldClock>();
                KeyValue($"Frame {frame}", $"WorldTime={clock.Time:F2}s");
                KeyValue("WorldLifecycle.Tick", $"frame={frame},time={clock.Time:F2}");
            }

            Divider();
            Section("销毁 World");
            var destroyed = worldManager.Destroy(world.Id);
            KeyValue("Destroyed", destroyed.ToString());
            KeyValue("WorldLifecycle.Destroyed", destroyed.ToString());
            KeyValue("RemainingWorlds", worldManager.Worlds.Count.ToString());

            Divider();
            Section("这个示例实际接入的包能力");
            Bullet("WorldTypeRegistry：按 WorldType 注册创建函数。 ");
            Bullet("RegistryWorldFactory：把 registry 适配为 IWorldFactory。 ");
            Bullet("WorldManager：负责 Create、Tick、Destroy 与生命周期调用。 ");
            Bullet("WorldClock：作为 World 服务随 Tick 推进确定性时间。 ");
        }

        private static IWorld CreateWorld(WorldCreateOptions options)
        {
            var builder = new WorldContainerBuilder();
            builder.RegisterServiceType<IWorldClock, WorldClock>(WorldLifetime.Singleton);
            builder.RegisterServiceType<IWorldLogger, NullWorldLogger>(WorldLifetime.Singleton);

            return new LifecycleWorld(options.Id, options.WorldType, builder.Build());
        }

        private sealed class LifecycleWorld : IWorld
        {
            private readonly IWorldResolver _services;
            private bool _disposed;

            public LifecycleWorld(WorldId id, string worldType, IWorldResolver services)
            {
                Id = id;
                WorldType = worldType;
                _services = services;
            }

            public WorldId Id { get; }
            public string WorldType { get; }
            public IWorldResolver Services => _services;
            public bool Initialized { get; private set; }

            public void Initialize()
            {
                Initialized = true;
            }

            public void Tick(float deltaTime)
            {
                _services.Resolve<IWorldClock>().Tick(deltaTime);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (_services is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
