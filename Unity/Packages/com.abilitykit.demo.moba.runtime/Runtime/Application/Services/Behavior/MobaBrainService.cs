using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Moba.Behavior;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// Actor 大脑服务：为带 ActorBrain 的 Actor 创建并驱动 BehaviorRuntime。
    ///
    /// 有效定义优先级：
    /// 1. 召唤物配置的 BrainTreeName 转换为 BTree 定义。
    /// 2. <see cref="IMobaActorBrainCatalog"/>（BrainId → 定义）。
    /// 3. 未登记回退 Idle。
    ///
    /// 定义对应的决策由 <see cref="MobaBrainDecisionDriverRegistry"/> 创建，
    /// 服务仅负责行为运行时的生命周期。
    /// </summary>
    [WorldService(typeof(MobaBrainService), WorldLifetime.Scoped)]
    public sealed class MobaBrainService : IService
    {
        private const string DefaultBehaviorKind = "moba.actor.brain";
        private const int BrainSourceKindSummon = 2;

        private readonly MobaActorRegistry _registry;
        private readonly IMobaActorBrainCatalog _catalog;
        private readonly MobaConfigDatabase _config;
        private readonly MobaWorldQuery _worldQuery;
        private readonly BehaviorManager _behaviors = new BehaviorManager();
        private readonly MobaBrainDecisionDriverRegistry _decisionDrivers;

        public MobaBrainDecisionDriverRegistry DecisionDrivers => _decisionDrivers;

        public MobaBrainService(
            MobaActorRegistry registry,
            IMobaActorBrainCatalog catalog,
            MobaConfigDatabase config = null)
            : this(registry, catalog, config, MobaBrainDecisionDriverRegistry.CreateDefault())
        {
        }

        public MobaBrainService(
            MobaActorRegistry registry,
            IMobaActorBrainCatalog catalog,
            MobaConfigDatabase config,
            MobaBrainDecisionDriverRegistry decisionDrivers)
        {
            _registry = registry;
            _catalog = catalog;
            _config = config;
            _decisionDrivers = decisionDrivers ?? MobaBrainDecisionDriverRegistry.CreateDefault();
            _worldQuery = new MobaWorldQuery(
                new MobaBrainEntityManager(registry),
                new MobaBrainBuffManager(registry),
                new MobaBrainAttributeSystem(registry));
        }

        public BehaviorRuntime EnsureBehavior(global::ActorEntity actor)
        {
            if (actor == null || !actor.hasActorId || !actor.hasActorBrain) return null;

            var brain = actor.actorBrain;
            if (brain.BrainId <= 0) return null;

            var existing = brain.BehaviorInstanceId > 0 ? _behaviors.GetBehavior(brain.BehaviorInstanceId) : null;
            if (existing != null && existing.Phase == BehaviorPhase.Running) return existing;

            var ownerActorId = actor.actorId.Value;

            var definition = ResolveDefinition(brain.BrainId, brain.SourceKind, brain.SourceId);
            var context = new MobaBrainDecisionCreateContext(
                in definition,
                _registry,
                _config,
                ownerActorId,
                brain.SourceKind,
                brain.SourceId);

            if (!_decisionDrivers.TryCreate(in context, out var decision))
            {
                var idleDefinition = new MobaActorBrainDefinition(brain.BrainId, MobaBrainDriverKind.Idle, "idle");
                var idleContext = new MobaBrainDecisionCreateContext(
                    in idleDefinition,
                    _registry,
                    _config,
                    ownerActorId,
                    brain.SourceKind,
                    brain.SourceId);
                _decisionDrivers.TryCreate(in idleContext, out decision);
            }

            if (decision == null) return null;

            var runtime = _behaviors.CreateBehavior(new BehaviorCreateConfig
            {
                BehaviorKind = DefaultBehaviorKind,
                SourceContextId = brain.BrainId,
                OwnerId = new BehaviorEntityId(ownerActorId),
                Decision = decision,
                Executor = new DefaultExecutor(),
                World = _worldQuery
            });

            actor.ReplaceActorBrain(
                brain.BrainId,
                brain.OwnerActorId,
                brain.SourceKind,
                brain.SourceId,
                runtime.InstanceId);

            return runtime;
        }

        public bool TryGetBehavior(long instanceId, out BehaviorRuntime behavior)
        {
            behavior = instanceId > 0 ? _behaviors.GetBehavior(instanceId) : null;
            return behavior != null;
        }

        private MobaActorBrainDefinition ResolveDefinition(int brainId, int sourceKind, int sourceId)
        {
            if (sourceKind == BrainSourceKindSummon
                && sourceId > 0
                && _config != null
                && _config.TryGetSummon(sourceId, out var summon)
                && summon != null
                && !string.IsNullOrWhiteSpace(summon.BrainTreeName))
            {
                return new MobaActorBrainDefinition(
                    brainId,
                    MobaBrainDriverKind.BTree,
                    summon.BrainTreeName);
            }

            var fallback = new MobaActorBrainDefinition(brainId, MobaBrainDriverKind.Idle, "idle");
            if (_catalog != null && _catalog.TryGet(brainId, out var definition))
            {
                return definition;
            }

            return fallback;
        }

        public void Tick(float deltaTimeSeconds, long frame)
        {
            if (deltaTimeSeconds <= 0f) return;
            _behaviors.Tick(deltaTimeSeconds, frame);
        }

        public void Dispose()
        {
        }
    }
}
