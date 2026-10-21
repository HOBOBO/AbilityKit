using System;
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
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Worlds.Blueprints;

namespace AbilityKit.Demo.Moba.Rollback
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MobaRollbackProviderAttribute : System.Attribute
    {
        public int Key { get; }
        public bool AutoResolve { get; }

        public MobaRollbackProviderAttribute(int key, bool autoResolve = false)
        {
            Key = key;
            AutoResolve = autoResolve;
        }
    }

    public readonly struct MobaGeneratedRollbackProviderDescriptor
    {
        public readonly int Key;
        public readonly string TypeName;
        public readonly bool AutoResolve;

        public MobaGeneratedRollbackProviderDescriptor(int key, string typeName, bool autoResolve)
        {
            Key = key;
            TypeName = typeName;
            AutoResolve = autoResolve;
        }
    }

    internal static partial class MobaGeneratedRollbackProviderManifest
    {
        public static MobaGeneratedRollbackProviderDescriptor[] CreateDescriptors()
        {
            var descriptors = new List<MobaGeneratedRollbackProviderDescriptor>();
            AddGeneratedDescriptors(descriptors);
            return descriptors.ToArray();
        }

        public static void RegisterResolved(IWorld world, RollbackRegistry registry)
        {
            if (world?.Services == null || registry == null) return;
            AddGeneratedResolved(world.Services, registry);
        }

        static partial void AddGeneratedDescriptors(List<MobaGeneratedRollbackProviderDescriptor> descriptors);
        static partial void AddGeneratedResolved(AbilityKit.Ability.World.DI.IWorldResolver services, RollbackRegistry registry);
    }

    /// <summary>
    /// Defines the complete rollback state owned by the MOBA simulation.
    /// All prediction and server rollback sessions must build their registry through this entry point.
    /// </summary>
    public static class MobaRollbackRegistryBuilder
    {
        public static RollbackRegistry Create(IWorld world)
        {
            var registry = new RollbackRegistry();
            if (world?.Services == null)
                throw new ArgumentException("Rollback world and services are required.", nameof(world));

            if (world is IEntitasWorld entitasWorld && entitasWorld.Contexts is global::Contexts contexts &&
                world.Services.TryResolve<ActorIdAllocator>(out var actorIds) && actorIds != null &&
                world.Services.TryResolve<MobaActorRegistry>(out var actors) && actors != null)
            {
                world.Services.TryResolve<MobaEntityManager>(out var entityManager);
                world.Services.TryResolve<MobaSummonService>(out var summons);
                MobaActorRollbackCommandRuntime actorCommands = null;
                if (entityManager != null &&
                    world.Services.TryResolve<IFrameTime>(out var commandFrameTime) &&
                    commandFrameTime != null)
                {
                    actorCommands = entityManager.GetOrCreateActorRollbackCommands(
                        contexts.actor,
                        actors,
                        commandFrameTime);
                }

                var restoresMissingActors = actorCommands != null;
                registry.Register(new MobaEntitasEntityRollbackProvider(
                    contexts.actor,
                    actorIds,
                    actors,
                    entityManager,
                    summons,
                    restoresMissingActors));
                registry.Register(new MobaEntitasComponentRollbackProvider(
                    contexts.actor,
                    restoresMissingActors));
                if (actorCommands != null) registry.Register(actorCommands.StateProvider);
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

            if (world.Services.TryResolve<IWorldRandom>(out var worldRandom) &&
                worldRandom is RollbackWorldRandom rollbackRandom)
                registry.Register(rollbackRandom);

            if (world.Services.TryResolve<MobaGameplayService>(out var gameplay) && gameplay != null &&
                world.Services.TryResolve<MobaGameplayVariableService>(out var gameplayVariables) && gameplayVariables != null)
            {
                world.Services.TryResolve<MobaUnitDeathSubscriber>(out var deaths);
                registry.Register(new MobaGameplayRollbackProvider(gameplay, gameplayVariables, deaths));
            }

            var minionWavesEnabled = world.Services.TryResolve<IMobaGameplayFeatureSet>(out var gameplayFeatures) &&
                gameplayFeatures != null && gameplayFeatures.IsConfigured &&
                gameplayFeatures.IsEnabled(MobaGameplayFeatureIds.MinionWaves);
            if (minionWavesEnabled &&
                world.Services.TryResolve<MobaMinionWaveService>(out var minionWaves) && minionWaves != null &&
                world.Services.TryResolve<MobaMinionCombatService>(out var minionCombat) && minionCombat != null)
            {
                registry.Register(new MobaMinionRollbackProvider(minionWaves, minionCombat));
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
            if (world.Services.TryResolve<SkillCastCoordinator>(out var skillCoordinator) && skillCoordinator != null)
            {
                registry.Register(new MobaSkillPipelineRollbackProvider(skillCoordinator));
            }

            if (world.Services.TryResolve<MobaSkillEconomyService>(out var skillEconomy) && skillEconomy != null)
            {
                registry.Register(new MobaSkillEconomyRollbackProvider(skillEconomy));
            }

            if (world.Services.TryResolve<MobaSkillParamModifierService>(out var skillParamModifiers) && skillParamModifiers != null)
            {
                registry.Register(new MobaSkillParamModifierRollbackProvider(skillParamModifiers,
                    restoreProjection: false));
            }

            if (world.Services.TryResolve<MobaContinuousManager>(out var modifierContinuous) && modifierContinuous != null)
            {
                world.Services.TryResolve<MobaTriggerIntervalContinuousService>(out var triggerIntervals);
                registry.Register(new MobaContinuousModifierCaptureRollbackProvider(modifierContinuous,
                    triggerIntervals));
            }

            if (world.Services.TryResolve<MobaTriggerModifierContinuousService>(out var triggerModifiers) && triggerModifiers != null)
            {
                registry.Register(new MobaTriggerModifierContinuousRollbackProvider(triggerModifiers));
            }

            if (world.Services.TryResolve<MobaDerivedSkillService>(out var derivedSkills) && derivedSkills != null)
            {
                registry.Register(new MobaDerivedSkillRollbackProvider(derivedSkills));
            }

            MobaGeneratedRollbackProviderManifest.RegisterResolved(world, registry);

            if (string.Equals(world.WorldType, MobaBattleWorldBlueprint.Type, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(world.WorldType, "MobaBattle", StringComparison.OrdinalIgnoreCase))
                ValidateBattleRegistry(registry, minionWavesEnabled);

            return registry;
        }

        private static void ValidateBattleRegistry(RollbackRegistry registry, bool minionWavesEnabled)
        {
            var requiredKeys = new List<int>
            {
                MobaEntitasEntityRollbackProvider.DefaultKey,
                MobaEntitasComponentRollbackProvider.DefaultKey,
                MobaActorTransformRollbackProvider.DefaultKey,
                MobaActorHpRollbackProvider.DefaultKey,
                MobaActorResourceRollbackProvider.DefaultKey,
                MobaBuffTimerRollbackProvider.DefaultKey,
                MobaSkillCooldownRollbackProvider.DefaultKey,
                MobaCharacterHfsmRollbackProvider.DefaultKey,
                MobaGameplayRollbackProvider.DefaultKey,
                MobaOwnerBlackboardRollbackProvider.DefaultKey,
                MobaContextEntityRollbackProvider.DefaultKey,
                MobaSkillRuntimeRollbackProvider.DefaultKey,
                MobaSkillPipelineRollbackProvider.DefaultKey,
                MobaSkillEconomyRollbackProvider.DefaultKey,
                MobaSkillParamModifierRollbackProvider.DefaultKey,
                MobaDerivedSkillRollbackProvider.DefaultKey,
                MobaContinuousModifierCaptureRollbackProvider.DefaultKey,
                MobaTriggerExecutionRuntimeService.DefaultKey,
                CommandRollbackStateProvider.DefaultKey,
                FrameTimeRollbackStateProvider.DefaultKey,
                RollbackWorldRandom.DefaultKey,
            };
            if (minionWavesEnabled)
                requiredKeys.Insert(9, MobaMinionRollbackProvider.DefaultKey);
            var missing = new List<int>();
            foreach (var key in requiredKeys)
            {
                if (!registry.TryGet(key, out _)) missing.Add(key);
            }

            if (MobaGeneratedRollbackProviderManifest.CreateDescriptors().Length == 0)
                throw new InvalidOperationException("MOBA rollback provider manifest is empty.");

            if (missing.Count != 0)
                throw new InvalidOperationException(
                    $"Battle rollback registry is incomplete. Missing provider keys: {string.Join(", ", missing)}");
        }
    }
}
