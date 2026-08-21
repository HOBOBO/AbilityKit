using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Predicates;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Domains;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages
{
    /// <summary>
    /// 注册 MOBA 世界模块和触发器运行时服务。
    /// </summary>
    [MobaBootstrapStage]
    public sealed class WorldModulesStage : MobaBootstrapStageBase
    {
        public override string Name => MobaBootstrapStageNames.WorldModules;

        public override string[] Dependencies => new[]
        {
            MobaBootstrapStageNames.Config,
        };

        protected internal override void Configure(WorldContainerBuilder builder)
        {
            builder.TryRegisterType<AbilityKit.Triggering.Eventing.IEventBus, AbilityKit.Triggering.Eventing.EventBus>(WorldLifetime.Singleton);
            builder.Register<FunctionRegistry>(WorldLifetime.Singleton, _ =>
            {
                var functions = new FunctionRegistry();
                MobaPlanPredicateFunctions.Register(functions);
                return functions;
            });
            builder.Register<ActionRegistry>(WorldLifetime.Singleton, _ => new ActionRegistry());
            builder.Register<AbilityKit.Demo.Moba.Services.MobaBattleRouteRegistry>(WorldLifetime.Singleton, _ => AbilityKit.Demo.Moba.Services.MobaBattleRouteRegistry.CreateDefault());
            builder.Register<AbilityKit.Demo.Moba.Services.MobaInputCommandContractRegistry>(WorldLifetime.Singleton, _ => AbilityKit.Demo.Moba.Services.MobaInputCommandContractRegistry.CreateDefault());
            builder.Register<AbilityKit.Demo.Moba.Services.MobaSnapshotContractProfileRegistry>(WorldLifetime.Singleton, _ => AbilityKit.Demo.Moba.Services.MobaSnapshotContractProfileRegistry.CreateDefault());
            builder.TryRegister<MobaGameplayConfigSettings>(WorldLifetime.Scoped, _ => new MobaGameplayConfigSettings());
            builder.Register<AbilityKit.Demo.Moba.Services.MobaTriggerPayloadResolverRegistry>(WorldLifetime.Singleton, _ => new AbilityKit.Demo.Moba.Services.MobaTriggerPayloadResolverRegistry());
            builder.Register<AbilityKit.Demo.Moba.Services.MobaTriggerConditionRegistry>(WorldLifetime.Singleton, _ => new AbilityKit.Demo.Moba.Services.MobaTriggerConditionRegistry());
            builder.Register<IPayloadAccessorRegistry>(WorldLifetime.Singleton, _ =>
            {
                var payloads = new PayloadAccessorRegistry();
                var gameplayAccessor = new GameplayLifecyclePayloadAccessor();
                payloads.RegisterIntAccessor(gameplayAccessor, GameplayLifecyclePayloadAccessor.SupportsField);
                payloads.RegisterDoubleAccessor(gameplayAccessor, GameplayLifecyclePayloadAccessor.SupportsField);

                var battleAccessor = new MobaBattlePayloadAccessor();
                payloads.RegisterIntAccessor<AttackInfo>(battleAccessor, MobaBattlePayloadAccessor.SupportsAttackInfoField);
                payloads.RegisterIntAccessor<DamageResult>(battleAccessor, MobaBattlePayloadAccessor.SupportsDamageResultField);
                payloads.RegisterDoubleAccessor<DamageResult>(battleAccessor, MobaBattlePayloadAccessor.SupportsDamageResultField);
                payloads.RegisterIntAccessor<Events.Unit.UnitDieEventPayload>(battleAccessor, MobaBattlePayloadAccessor.SupportsUnitDieField);
                payloads.RegisterDoubleAccessor<Events.Unit.UnitDieEventPayload>(battleAccessor, MobaBattlePayloadAccessor.SupportsUnitDieField);

                var skillAccessor = new SkillPipelineContextPayloadAccessor(_);
                payloads.RegisterIntAccessor<SkillPipelineContext>(skillAccessor, SkillPipelineContextPayloadAccessor.SupportsField);
                payloads.RegisterDoubleAccessor<SkillPipelineContext>(skillAccessor, SkillPipelineContextPayloadAccessor.SupportsField);

                var skillCastAccessor = new SkillCastContextPayloadAccessor();
                payloads.RegisterIntAccessor<SkillCastContext>(skillCastAccessor, SkillCastContextPayloadAccessor.SupportsField);
                payloads.RegisterDoubleAccessor<SkillCastContext>(skillCastAccessor, SkillCastContextPayloadAccessor.SupportsField);

                var skillObjectAccessor = new SkillPipelineContextObjectPayloadAccessor(skillAccessor);
                payloads.RegisterIntAccessor<object>(skillObjectAccessor, SkillPipelineContextObjectPayloadAccessor.SupportsField);
                payloads.RegisterDoubleAccessor<object>(skillObjectAccessor, SkillPipelineContextObjectPayloadAccessor.SupportsField);
                return payloads;
            });
            builder.Register<IBlackboardResolver>(WorldLifetime.Singleton, _ => new DictionaryBlackboardResolver());
            builder.Register<IOwnerBlackboardStore>(WorldLifetime.Singleton, r =>
                new OwnerBlackboardStore(r.Resolve<IBlackboardResolver>()));
            builder.Register<INumericVarDomainRegistry>(WorldLifetime.Singleton, _ =>
            {
                var registry = new NumericVarDomainRegistry();
                registry.Register(new MobaGameplayNumericVarDomain());
                RegisterDefaultBlackboardDomain(registry, "bb");
                RegisterDefaultBlackboardDomain(registry, "actor");
                RegisterDefaultBlackboardDomain(registry, "skill");
                RegisterDefaultBlackboardDomain(registry, "effect");
                RegisterDefaultBlackboardDomain(registry, "projectile");
                RegisterDefaultBlackboardDomain(registry, "battle");
                RegisterDefaultBlackboardDomain(registry, "global");
                return registry;
            });
            builder.Register<AbilityKit.Triggering.Runtime.TriggerRunner<IWorldResolver>>(WorldLifetime.Scoped, r =>
            {
                var diagnosticsAdapter = new MobaTriggerDiagnosticsAdapter(r);
                return new AbilityKit.Triggering.Runtime.TriggerRunner<IWorldResolver>(
                    r.Resolve<AbilityKit.Triggering.Eventing.IEventBus>(),
                    r.Resolve<FunctionRegistry>(),
                    r.Resolve<ActionRegistry>(),
                    contextSource: new WorldResolverContextSource(r),
                    lifecycle: diagnosticsAdapter,
                    blackboards: r.Resolve<IBlackboardResolver>(),
                    payloads: r.Resolve<IPayloadAccessorRegistry>(),
                    numericDomains: r.Resolve<INumericVarDomainRegistry>(),
                    tracer: diagnosticsAdapter);
            });

            builder.AddModule(new EntitasEcsWorldModule());
            builder.AddModule(new ProjectileWorldModule());
            builder.AddModule(new MobaServicesAutoModule());
        }

        private static void RegisterDefaultBlackboardDomain(NumericVarDomainRegistry registry, string domainId)
        {
            registry.Register(new BlackboardNumericVarDomain(domainId, BlackboardIdMapper.BoardId(domainId)));
        }

        protected internal override void Install(
            Entitas.IContexts contexts,
            Entitas.Systems systems,
            IWorldResolver services)
        {
        }
    }

    /// <summary>
    /// 将 <see cref="IWorldResolver"/> 作为 TriggerRunner 的上下文源，
    /// 使计划路径谓词函数（如 has_buff）能通过 ExecCtx.Context 访问世界服务。
    /// </summary>
    internal sealed class WorldResolverContextSource : AbilityKit.Triggering.Runtime.ITriggerContextSource<IWorldResolver>
    {
        private readonly IWorldResolver _resolver;

        public WorldResolverContextSource(IWorldResolver resolver)
        {
            _resolver = resolver;
        }

        public IWorldResolver GetContext() => _resolver;
    }
}
