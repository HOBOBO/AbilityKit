using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Moba.Behavior;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 为带 ActorBrain 的 Actor 创建并驱动 BehaviorRuntime。
    /// BrainId 只通过战斗模板 Brain 目录解析；解析或驱动创建失败时不创建运行时。
    /// </summary>
    [WorldService(typeof(MobaBrainService), WorldLifetime.Scoped)]
    public sealed class MobaBrainService : IService
    {
        private const string DefaultBehaviorKind = "moba.actor.brain";
        private readonly MobaActorRegistry _registry;
        private readonly IMobaActorBrainCatalog _catalog;
        private readonly MobaConfigDatabase _config;
        private readonly MobaWorldQuery _worldQuery;
        private readonly BehaviorManager _behaviors = new BehaviorManager();
        private readonly MobaBrainDecisionDriverRegistry _decisionDrivers;
        [WorldInject(required: false)] private IFrameTime _frameTime;
        [WorldInject(required: false)] private SearchTargetService _searchTargets;

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

            if (_catalog == null || !_catalog.TryGet(brain.BrainId, out var definition))
            {
                Log.Error($"[MobaBrain] brain definition was not found. brainId={brain.BrainId} sourceKind={brain.SourceKind} sourceId={brain.SourceId}");
                return null;
            }

            var context = new MobaBrainDecisionCreateContext(
                in definition,
                _registry,
                _config,
                ownerActorId,
                brain.SourceKind,
                brain.SourceId,
                _searchTargets,
                GetCurrentTimeMs);

            if (!_decisionDrivers.TryCreate(in context, out var decision))
            {
                Log.Error($"[MobaBrain] brain driver failed to create a decision. brainId={brain.BrainId} driver={definition.DriverKind} definition={definition.DecisionName}");
                return null;
            }

            var runtime = _behaviors.CreateBehavior(new BehaviorCreateConfig
            {
                BehaviorKind = DefaultBehaviorKind,
                SourceContextId = brain.BrainId,
                OwnerId = new BehaviorEntityId(ownerActorId),
                Decision = decision,
                Executor = new MobaBrainExecutor(),
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

        public bool ActivateBrain(global::ActorEntity actor, int brainId, int sourceKind, int sourceId)
        {
            if (actor == null || !actor.hasActorId || brainId <= 0) return false;
            if (_catalog == null || !_catalog.TryGet(brainId, out _))
            {
                Log.Error($"[MobaBrain] source references an unknown brain. brainId={brainId} sourceKind={sourceKind} sourceId={sourceId}");
                return false;
            }

            DeactivateBrain(actor);
            actor.AddActorBrain(brainId, actor.actorId.Value, sourceKind, sourceId, 0L);
            return EnsureBehavior(actor) != null;
        }

        public bool DeactivateBrain(global::ActorEntity actor)
        {
            if (actor == null) return false;

            var hadBrain = actor.hasActorBrain;
            if (hadBrain)
            {
                var instanceId = actor.actorBrain.BehaviorInstanceId;
                if (instanceId > 0) _behaviors.Interrupt(instanceId, "BrainDisabled");
                actor.RemoveActorBrain();
            }

            if (actor.hasMoveInput) actor.ReplaceMoveInput(0f, 0f);
            return hadBrain;
        }

        public bool TryGetBehavior(long instanceId, out BehaviorRuntime behavior)
        {
            behavior = instanceId > 0 ? _behaviors.GetBehavior(instanceId) : null;
            return behavior != null;
        }

        public void Tick(float deltaTimeSeconds, long frame)
        {
            if (deltaTimeSeconds <= 0f) return;
            _behaviors.Tick(deltaTimeSeconds, frame);
        }

        private long GetCurrentTimeMs()
        {
            return MobaSkillRuntimeAccess.GetCurrentTimeMs(_frameTime);
        }

        public void Dispose()
        {
        }
    }
}
