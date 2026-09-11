#nullable enable

using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Pooling;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Starter
{
    /// <summary>
    /// Foundation starter: verifies that Core and World.DI can run without demo packages.
    /// </summary>
    [Sample(20, "starter", "foundation", "core", "world-di", "package-api", "web", "deterministic")]
    public sealed class FoundationStarter : SampleBase
    {
        public override string Title => "Foundation Starter";
        public override string Description => "用 Core + World.DI 跑通事件、对象池、服务注册和宿主驱动 Tick";
        public override SampleCategory Category => SampleCategory.Foundation;

        protected override void OnRun()
        {
            Section("Foundation 组合边界");
            KeyValue("FoundationStarter.Modules", "AbilityKit.Core, AbilityKit.World.DI");
            KeyValue("FoundationStarter.DemoDependency", "false");
            Bullet("Core 负责事件、对象池和基础设施。 ");
            Bullet("World.DI 负责战斗世界或玩法作用域中的服务装配。 ");
            Bullet("Sample 宿主负责时间推进，Starter 不绑定 Unity Update 或服务端框架。 ");

            Divider();
            Section("Core EventDispatcher");
            var dispatcher = new EventDispatcher();
            var receivedEvents = 0;
            var lastEvent = "none";
            var subscription = dispatcher.Subscribe<StarterEvent>("starter.ready", evt =>
            {
                receivedEvents++;
                lastEvent = $"{evt.ActorId}:{evt.Message}";
            });

            dispatcher.Publish("starter.ready", new StarterEvent("hero-001", "foundation-ready"), autoReleaseArgs: false);
            subscription.Unsubscribe();
            KeyValue("FoundationStarter.EventCount", receivedEvents.ToString());
            KeyValue("FoundationStarter.LastEvent", lastEvent);

            Divider();
            Section("Core ObjectPool");
            var pool = new ObjectPool<StarterCommand>(new ObjectPoolOptions<StarterCommand>(() => new StarterCommand())
            {
                DefaultCapacity = 1,
                MaxSize = 4,
                CollectionCheck = false,
                OnRelease = command => command.Reset()
            });

            var command = pool.Get();
            command.Set("Cast", "starter.fireball");
            KeyValue("FoundationStarter.CommandBeforeRelease", command.ToString());
            pool.Release(command);
            var stats = pool.Stats;
            KeyValue("FoundationStarter.PoolCreated", stats.CreatedTotal.ToString());
            KeyValue("FoundationStarter.PoolInactive", stats.InactiveCount.ToString());
            KeyValue("FoundationStarter.PoolReleaseTotal", stats.ReleaseTotal.ToString());

            Divider();
            Section("World.DI 服务容器");
            using var container = CreateFoundationContainer();
            var clock = container.Resolve<IWorldClock>();
            var rules = container.Resolve<IStarterRuleService>();
            var traceA = container.Resolve<StarterTrace>();
            var traceB = container.Resolve<StarterTrace>();

            rules.Apply("hero-001", "BootFoundation");
            clock.Tick(0.05f);
            clock.Tick(0.10f);
            KeyValue("FoundationStarter.WorldTime", clock.Time.ToString("F2"));
            KeyValue("FoundationStarter.LastRule", rules.LastRule);
            KeyValue("FoundationStarter.TransientDifferent", (!ReferenceEquals(traceA, traceB)).ToString());

            Divider();
            Section("宿主驱动 Tick");
            var sampleTicks = 0;
            Environment.OnTick += OnSampleTick;
            SimulateFrames(3, 0.02f);
            Environment.Tick();
            Environment.Tick();
            Environment.Tick();
            Environment.OnTick -= OnSampleTick;
            KeyValue("FoundationStarter.SampleTickCount", sampleTicks.ToString());
            KeyValue("FoundationStarter.SampleTime", Time.ToString("F2"));

            Divider();
            Section("已有 SkillCore 示例入口");
            KeyValue("FoundationStarter.Next.Pipeline", "pipeline/basic-phases");
            KeyValue("FoundationStarter.Next.Triggering", "triggering/basic-event-trigger");
            KeyValue("FoundationStarter.Next.TriggeringCondition", "triggering/condition-blackboard");
            KeyValue("FoundationStarter.Next.Attributes", "modifiers/attribute-basic");
            KeyValue("FoundationStarter.WebExport", "dotnet run --project src/AbilityKit.Samples -- --web sample-web");

            Divider();
            Section("验收结论");
            var passed = receivedEvents == 1
                && stats.ReleaseTotal == 1
                && clock.Time > 0f
                && !ReferenceEquals(traceA, traceB)
                && sampleTicks == 3;
            KeyValue("FoundationStarter.Result", passed ? "Passed" : "Failed");
            Bullet("该 Starter 只验证 P0 Foundation，不覆盖技能、战斗、同步或服务端链路。 ");
            Bullet("SkillCore 能力已由现有 Pipeline、Triggering、Modifiers 示例覆盖，后续应收编路线而不是重复造示例。 ");

            void OnSampleTick(float _)
            {
                sampleTicks++;
            }
        }

        private static WorldContainer CreateFoundationContainer()
        {
            var builder = new WorldContainerBuilder();
            builder.RegisterServiceType<IWorldClock, WorldClock>(WorldLifetime.Singleton);
            builder.RegisterType<IStarterRuleService, StarterRuleService>(WorldLifetime.Singleton);
            builder.Register<StarterTrace>(WorldLifetime.Transient, _ => new StarterTrace());
            return builder.Build();
        }

        private sealed class StarterEvent
        {
            public StarterEvent(string actorId, string message)
            {
                ActorId = actorId;
                Message = message;
            }

            public string ActorId { get; }
            public string Message { get; }
        }

        private sealed class StarterCommand
        {
            private string _kind = "none";
            private string _payload = "none";

            public void Set(string kind, string payload)
            {
                _kind = kind;
                _payload = payload;
            }

            public void Reset()
            {
                _kind = "none";
                _payload = "none";
            }

            public override string ToString()
            {
                return $"{_kind}:{_payload}";
            }
        }

        private interface IStarterRuleService
        {
            string LastRule { get; }
            void Apply(string actorId, string ruleName);
        }

        private sealed class StarterRuleService : IStarterRuleService
        {
            public string LastRule { get; private set; } = "none";

            public void Apply(string actorId, string ruleName)
            {
                LastRule = $"{actorId}.{ruleName}";
            }
        }

        private sealed class StarterTrace
        {
        }
    }
}
