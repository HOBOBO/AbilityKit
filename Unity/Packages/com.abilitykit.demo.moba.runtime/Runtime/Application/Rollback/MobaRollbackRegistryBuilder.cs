using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Services.Passive;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.HFSM.Definition;
using AbilityKit.Ability.World;
using AbilityKit.Demo.Moba.Services.EntityManager;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Defines the complete rollback state owned by the MOBA simulation.
    /// All prediction and server rollback sessions must build their registry through this entry point.
    /// </summary>
    public static class MobaRollbackRegistryBuilder
    {
        public static RollbackRegistry Create(IWorld world)
        {
            var registry = new RollbackRegistry();
            if (world?.Services == null) return registry;

            if (world is IEntitasWorld entitasWorld && entitasWorld.Contexts is global::Contexts contexts &&
                world.Services.TryResolve<ActorIdAllocator>(out var actorIds) && actorIds != null &&
                world.Services.TryResolve<MobaActorRegistry>(out var actors) && actors != null)
            {
                world.Services.TryResolve<MobaEntityManager>(out var entityManager);
                world.Services.TryResolve<MobaSummonService>(out var summons);
                registry.Register(new MobaEntitasEntityRollbackProvider(contexts.actor, actorIds, actors, entityManager, summons));
                registry.Register(new MobaEntitasComponentRollbackProvider(contexts.actor));
            }

            if (world.Services.TryResolve<IOwnerBlackboardStore>(out var ownerBlackboards) &&
                ownerBlackboards is IOwnerBlackboardSnapshotStore snapshotStore)
            {
                var ownerKeySources = new List<IMobaOwnerKeySource>();
                if (world.Services.TryResolve<MobaPassiveSkillLifecycleService>(out var passives) && passives != null)
                    ownerKeySources.Add(passives);
                if (world.Services.TryResolve<MobaTriggerPlanSubscriptionService>(out var subscriptions) && subscriptions != null)
                    ownerKeySources.Add(subscriptions);
                if (world.Services.TryResolve<MobaContinuousManager>(out var continuous) && continuous != null)
                    ownerKeySources.Add(continuous);

                registry.Register(new MobaOwnerBlackboardRollbackProvider(snapshotStore, ownerKeySources));
            }

            if (world.Services.TryResolve<IFrameTime>(out var frameTime) &&
                frameTime is FrameTime mutableFrameTime)
            {
                registry.Register(new FrameTimeRollbackStateProvider(mutableFrameTime));
            }

            if (world.Services.TryResolve<MobaActorRegistry>(out var actorRegistry) &&
                actorRegistry != null)
            {
                registry.Register(new MobaActorTransformRollbackProvider(actorRegistry));
                registry.Register(new MobaActorHpRollbackProvider(actorRegistry));
                registry.Register(new MobaActorResourceRollbackProvider(actorRegistry));
                registry.Register(new MobaBuffTimerRollbackProvider(actorRegistry));
                registry.Register(new MobaSkillCooldownRollbackProvider(actorRegistry));
                world.Services.TryResolve<StateMachineDefinition>(out var characterDefinition);
                registry.Register(new MobaCharacterHfsmRollbackProvider(actorRegistry, characterDefinition));

                if (world.Services.TryResolve<MobaBrainService>(out var brainService) && brainService != null)
                {
                    registry.Register(new MobaBrainRollbackProvider(actorRegistry, brainService));
                }

                if (world.Services.TryResolve<MobaShieldService>(out var shieldService) &&
                    shieldService != null)
                {
                    registry.Register(new MobaShieldRollbackProvider(actorRegistry, shieldService));
                }

                if (world.Services.TryResolve<MobaActorStateMachineFactory>(out var stateMachineFactory) &&
                    stateMachineFactory != null)
                {
                    registry.Register(new MobaActorStateMachineRollbackProvider(actorRegistry, stateMachineFactory));
                }
            }

            if (world.Services.TryResolve<PassiveSkillTriggerEventRollbackLog>(out var passiveLog) &&
                passiveLog != null)
            {
                registry.Register(passiveLog);
            }

            if (world.Services.TryResolve<RollbackWorldRandom>(out var random) && random != null)
            {
                registry.Register(random);
            }

            if (world.Services.TryResolve<MobaRuntimeContextService>(out var runtimeContexts) && runtimeContexts != null)
                registry.Register(new MobaContextEntityRollbackProvider(runtimeContexts));

            if (world.Services.TryResolve<MobaTriggerExecutionRuntimeService>(out var triggerExecutions) && triggerExecutions != null)
            {
                registry.Register(triggerExecutions);
            }

            if (world.Services.TryResolve<MobaSkillCastRuntimeService>(out var skillRuntimes) && skillRuntimes != null)
            {
                registry.Register(new MobaSkillRuntimeRollbackProvider(skillRuntimes));
            }

            if (world.Services.TryResolve<MobaSkillEconomyService>(out var skillEconomy) && skillEconomy != null)
            {
                registry.Register(new MobaSkillEconomyRollbackProvider(skillEconomy));
            }

            if (world.Services.TryResolve<MobaSkillParamModifierService>(out var skillParamModifiers) && skillParamModifiers != null)
            {
                registry.Register(new MobaSkillParamModifierRollbackProvider(skillParamModifiers));
            }

            if (world.Services.TryResolve<MobaDerivedSkillService>(out var derivedSkills) && derivedSkills != null)
            {
                registry.Register(new MobaDerivedSkillRollbackProvider(derivedSkills));
            }

            return registry;
        }
    }
}
