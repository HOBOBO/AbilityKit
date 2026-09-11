#nullable enable

using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.World
{
    /// <summary>
    /// 演示 WorldContainerBuilder 如何注册和解析 World 内服务。
    /// </summary>
    [Sample(602, "world", "di", "basics", "package-api", "web", "deterministic")]
    public sealed class WorldDIOverview : SampleBase
    {
        public override string Title => "World DI Basics";
        public override string Description => "使用 WorldContainerBuilder 注册 Singleton、Transient 与 Scoped 服务";
        public override SampleCategory Category => SampleCategory.World;

        protected override void OnRun()
        {
            Section("构建 World 服务容器");
            var world = CreateWorld(new WorldCreateOptions
            {
                Id = new WorldId("di-world"),
                WorldType = "TrainingWorld"
            });

            world.Initialize();
            KeyValue("WorldId", world.Id.Value);
            KeyValue("Initialized", ((DiWorld)world).Initialized.ToString());
            KeyValue("WorldDI.Initialized", ((DiWorld)world).Initialized.ToString());

            Divider();
            Section("Singleton 与 Transient 生命周期");
            var rulesA = world.Services.Resolve<ICombatRuleService>();
            var rulesB = world.Services.Resolve<ICombatRuleService>();
            var requestA = world.Services.Resolve<RequestTrace>();
            var requestB = world.Services.Resolve<RequestTrace>();

            KeyValue("SingletonSame", ReferenceEquals(rulesA, rulesB).ToString());
            KeyValue("WorldDI.SingletonSame", ReferenceEquals(rulesA, rulesB).ToString());
            KeyValue("TransientSame", ReferenceEquals(requestA, requestB).ToString());
            KeyValue("WorldDI.TransientSame", ReferenceEquals(requestA, requestB).ToString());
            KeyValue("RequestA", requestA.Id.ToString());
            KeyValue("RequestB", requestB.Id.ToString());

            Divider();
            Section("服务协作与 World Tick");
            rulesA.ApplyDamage("slime", 25);
            world.Tick(0.10f);
            world.Tick(0.15f);

            var clock = world.Services.Resolve<IWorldClock>();
            KeyValue("WorldTime", clock.Time.ToString("F2"));
            KeyValue("WorldDI.WorldTime", clock.Time.ToString("F2"));
            KeyValue("DamageLog", rulesA.LastLog);
            KeyValue("WorldDI.DamageLog", rulesA.LastLog);

            Divider();
            Section("接入 WorldManager");
            var registry = new WorldTypeRegistry();
            registry.Register("TrainingWorld", CreateWorld);
            var manager = new WorldManager(new RegistryWorldFactory(registry));
            var managed = manager.Create(new WorldCreateOptions
            {
                Id = new WorldId("managed-di-world"),
                WorldType = "TrainingWorld"
            });
            KeyValue("ManagedWorld", managed.Id.Value);
            KeyValue("WorldDI.ManagedWorld", managed.Id.Value);
            manager.DisposeAll();
            world.Dispose();

            Divider();
            Section("这个示例实际接入的包能力");
            Bullet("WorldContainerBuilder：注册 World 内服务和业务服务。 ");
            Bullet("WorldLifetime.Singleton：同一个 World 内复用同一个服务实例。 ");
            Bullet("WorldLifetime.Transient：每次 Resolve 创建新的短生命周期对象。 ");
            Bullet("IWorldResolver：业务从 World.Services 中解析依赖。 ");
        }

        private static IWorld CreateWorld(WorldCreateOptions options)
        {
            var builder = new WorldContainerBuilder();
            builder.RegisterServiceType<IWorldClock, WorldClock>(WorldLifetime.Singleton);
            builder.RegisterServiceType<IWorldLogger, NullWorldLogger>(WorldLifetime.Singleton);
            builder.RegisterType<ICombatRuleService, CombatRuleService>(WorldLifetime.Singleton);
            builder.Register<RequestTrace>(WorldLifetime.Transient, _ => new RequestTrace());

            return new DiWorld(options.Id, options.WorldType, builder.Build());
        }

        private interface ICombatRuleService
        {
            string LastLog { get; }
            void ApplyDamage(string targetId, int amount);
        }

        private sealed class CombatRuleService : ICombatRuleService
        {
            public string LastLog { get; private set; } = "none";

            public void ApplyDamage(string targetId, int amount)
            {
                LastLog = $"{targetId} takes {amount} damage";
            }
        }

        private sealed class RequestTrace
        {
            private static int _nextId;

            public RequestTrace()
            {
                Id = ++_nextId;
            }

            public int Id { get; }
        }

        private sealed class DiWorld : IWorld
        {
            private readonly IWorldResolver _services;
            private bool _disposed;

            public DiWorld(WorldId id, string worldType, IWorldResolver services)
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

                if (_services is System.IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
