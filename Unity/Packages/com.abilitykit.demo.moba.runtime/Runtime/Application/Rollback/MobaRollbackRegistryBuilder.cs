using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Services.Passive;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.HFSM.Definition;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Services;
using AbilityKit.Combat.Projectile;
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
                world.Services.TryResolve<MobaBrainService>(out var brainsForEntityCleanup);
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
                    restoresMissingActors,
                    brainsForEntityCleanup));
                registry.Register(new MobaEntitasComponentRollbackProvider(
                    contexts.actor,
                    restoresMissingActors));
                if (actorCommands != null) registry.Register(actorCommands.StateProvider);
            }

            MobaOwnerBlackboardRollbackProvider ownerProvider = null;
            if (world.Services.TryResolve<IOwnerBlackboardStore>(out var ownerBlackboards) &&
                ownerBlackboards is IOwnerBlackboardSnapshotStore snapshotStore)
            {
                var ownerKeySources = new List<IMobaOwnerKeySource>();
                if (world.Services.TryResolve<MobaActorRegistry>(out var ownerKeyActors) && ownerKeyActors != null)
                    ownerKeySources.Add(new MobaActorRegistryOwnerKeySource(ownerKeyActors, snapshotStore));
                if (world.Services.TryResolve<MobaPassiveSkillLifecycleService>(out var passives) && passives != null)
                    ownerKeySources.Add(passives);
                if (world.Services.TryResolve<MobaTriggerPlanSubscriptionService>(out var subscriptions) && subscriptions != null)
                    ownerKeySources.Add(subscriptions);
                if (world.Services.TryResolve<MobaContinuousManager>(out var continuous) && continuous != null)
                    ownerKeySources.Add(continuous);

                ownerProvider = new MobaOwnerBlackboardRollbackProvider(snapshotStore, ownerKeySources);
            }

            var jointLifecycle = ownerProvider != null &&
                world.Services.TryResolve<MobaRuntimeContextService>(out _) &&
                world.Services.TryResolve<MobaSkillCastRuntimeService>(out _) &&
                world.Services.TryResolve<SkillCastCoordinator>(out _) &&
                world.Services.TryResolve<MobaBuffStateRecoveryProvider>(out _) &&
                world.Services.TryResolve<MobaContinuousManager>(out _) &&
                world.Services.TryResolve<MobaTriggerIntervalContinuousService>(out _) &&
                world.Services.TryResolve<MobaTriggerModifierContinuousService>(out _);
            if (jointLifecycle) registry.Register(new MobaSkillLifecycleRollbackProvider(world, ownerProvider));
            else if (ownerProvider != null) registry.Register(ownerProvider);

            if (world.Services.TryResolve<IFrameTime>(out var frameTime) &&
                frameTime is FrameTime mutableFrameTime)
            {
                registry.Register(new FrameTimeRollbackStateProvider(mutableFrameTime));
            }

            if (world.Services.TryResolve<IWorldClock>(out var clock) && clock is WorldClock mutableClock)
                registry.Register(new MobaWorldClockRollbackProvider(mutableClock));
            if (world.Services.TryResolve<MobaCombatActivityService>(out var combatActivity) && combatActivity != null &&
                world.Services.TryResolve<MobaActorRegistry>(out var combatActors) && combatActors != null)
                registry.Register(new MobaCombatActivityRollbackProvider(combatActivity, combatActors));

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

            if (world.Services.TryResolve<MobaStructureService>(out var structures) && structures != null)
                registry.Register(new MobaStructureRollbackProvider(structures));

            if (world.Services.TryResolve<MobaActorRegistry>(out var actorRegistry) &&
                actorRegistry != null)
            {
                registry.Register(new MobaActorTransformRollbackProvider(actorRegistry));
                registry.Register(new MobaActorHpRollbackProvider(actorRegistry));
                registry.Register(new MobaActorResourceRollbackProvider(actorRegistry));
                if (!jointLifecycle && world.Services.TryResolve<MobaBuffStateRecoveryProvider>(out var buffRecovery) && buffRecovery != null)
                    registry.Register(new MobaBuffLifecycleRollbackProvider(buffRecovery));
                else if (!jointLifecycle)
                    registry.Register(new MobaBuffTimerRollbackProvider(actorRegistry));
                registry.Register(new MobaSkillCooldownRollbackProvider(actorRegistry));
                world.Services.TryResolve<StateMachineDefinition>(out var characterDefinition);
                world.Services.TryResolve<MobaCharacterHfsmParameterService>(out var characterParameters);
                world.Services.TryResolve<MobaEffectExecutionService>(out var characterHfsmEffects);
                world.Services.TryResolve<MobaCharacterHfsmProfileCatalog>(out var characterProfiles);
                registry.Register(new MobaCharacterHfsmRollbackProvider(actorRegistry,
                    characterDefinition, characterParameters, characterHfsmEffects, characterProfiles));
                if (characterParameters != null)
                    registry.Register(new MobaCharacterHfsmParameterRollbackProvider(characterParameters));

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

            // ProjectileWorld owns the authoritative kinematic state for projectiles and areas.
            // Register it separately from actor transforms so a rollback cannot be overwritten by
            // the projectile tick system on the next frame.
            var projectileRollbackEnabled =
                world.Services.TryResolve<IProjectileService>(out var projectiles) && projectiles != null;
            if (projectileRollbackEnabled)
                registry.Register(new ProjectileRollbackProvider(projectiles));

            if (!jointLifecycle && world.Services.TryResolve<MobaRuntimeContextService>(out var runtimeContexts) && runtimeContexts != null)
                registry.Register(new MobaContextEntityRollbackProvider(runtimeContexts));

            if (world.Services.TryResolve<MobaTriggerExecutionRuntimeService>(out var triggerExecutions) && triggerExecutions != null)
            {
                registry.Register(triggerExecutions);
            }

            if (!jointLifecycle && world.Services.TryResolve<MobaSkillCastRuntimeService>(out var skillRuntimes) && skillRuntimes != null)
            {
                registry.Register(new MobaSkillRuntimeRollbackProvider(skillRuntimes));
            }
            if (!jointLifecycle && world.Services.TryResolve<SkillCastCoordinator>(out var skillCoordinator) && skillCoordinator != null)
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

            if (!jointLifecycle && world.Services.TryResolve<MobaContinuousManager>(out var modifierContinuous) && modifierContinuous != null)
            {
                world.Services.TryResolve<MobaTriggerIntervalContinuousService>(out var triggerIntervals);
                registry.Register(new MobaContinuousModifierCaptureRollbackProvider(modifierContinuous,
                    triggerIntervals));
            }

            if (!jointLifecycle && world.Services.TryResolve<MobaTriggerModifierContinuousService>(out var triggerModifiers) && triggerModifiers != null)
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
            {
                if (clock != null && !(clock is WorldClock))
                    throw new InvalidOperationException(
                        $"Battle rollback requires a restorable WorldClock; actual clock type: {clock.GetType().FullName}.");
                ValidateBattleRegistry(registry, minionWavesEnabled, projectileRollbackEnabled);
            }

            return registry;
        }

        private static void ValidateBattleRegistry(
            RollbackRegistry registry,
            bool minionWavesEnabled,
            bool projectileRollbackEnabled)
        {
            var requiredKeys = new List<int>
            {
                MobaEntitasEntityRollbackProvider.DefaultKey,
                MobaEntitasComponentRollbackProvider.DefaultKey,
                MobaActorTransformRollbackProvider.DefaultKey,
                MobaActorHpRollbackProvider.DefaultKey,
                MobaActorResourceRollbackProvider.DefaultKey,
                MobaSkillLifecycleRollbackProvider.DefaultKey,
                MobaSkillCooldownRollbackProvider.DefaultKey,
                MobaCharacterHfsmRollbackProvider.DefaultKey,
                MobaGameplayRollbackProvider.DefaultKey,
                MobaSkillEconomyRollbackProvider.DefaultKey,
                MobaSkillParamModifierRollbackProvider.DefaultKey,
                MobaDerivedSkillRollbackProvider.DefaultKey,
                MobaTriggerExecutionRuntimeService.DefaultKey,
                CommandRollbackStateProvider.DefaultKey,
                FrameTimeRollbackStateProvider.DefaultKey,
                RollbackWorldRandom.DefaultKey,
                MobaCombatActivityRollbackProvider.DefaultKey,
                MobaWorldClockRollbackProvider.DefaultKey,
            };
            if (minionWavesEnabled)
                requiredKeys.Insert(9, MobaMinionRollbackProvider.DefaultKey);
            if (projectileRollbackEnabled)
                requiredKeys.Add(ProjectileRollbackProvider.DefaultKey);
            var missing = new List<int>();
            foreach (var key in requiredKeys)
            {
                if (!registry.TryGet(key, out _)) missing.Add(key);
            }

            if (missing.Count != 0)
                throw new InvalidOperationException(
                    $"Battle rollback registry is incomplete. Missing provider keys: {string.Join(", ", missing)}");

            // Old local checkpoints must not silently keep predicted clock/combat facts.
            registry.RequireState(MobaCombatActivityRollbackProvider.DefaultKey);
            registry.RequireState(MobaWorldClockRollbackProvider.DefaultKey);

            if (!registry.TryGet(MobaSkillLifecycleRollbackProvider.DefaultKey, out var lifecycle) ||
                !(lifecycle is MobaSkillLifecycleRollbackProvider))
                throw new InvalidOperationException("Battle rollback requires the joint skill lifecycle envelope.");
            foreach (var key in new[] { MobaOwnerBlackboardRollbackProvider.DefaultKey, MobaSkillRuntimeRollbackProvider.DefaultKey,
                MobaSkillPipelineRollbackProvider.DefaultKey, MobaBuffLifecycleRollbackProvider.DefaultKey,
                MobaBuffTimerRollbackProvider.DefaultKey, MobaContinuousModifierCaptureRollbackProvider.DefaultKey,
                MobaTriggerModifierContinuousRollbackProvider.DefaultKey })
                if (registry.TryGet(key, out _))
                    throw new InvalidOperationException($"Battle rollback has duplicate lifecycle ownership at provider {key}.");

            if (MobaGeneratedRollbackProviderManifest.CreateDescriptors().Length == 0)
                throw new InvalidOperationException("MOBA rollback provider manifest is empty.");

        }

        private sealed class MobaActorRegistryOwnerKeySource : IMobaActorOwnerKeySource
        {
            private readonly MobaActorRegistry _actors;
            private readonly IOwnerBlackboardSnapshotStore _store;
            private readonly List<int> _actorIds = new List<int>();

            public MobaActorRegistryOwnerKeySource(
                MobaActorRegistry actors,
                IOwnerBlackboardSnapshotStore store)
            {
                _actors = actors ?? throw new ArgumentNullException(nameof(actors));
                _store = store ?? throw new ArgumentNullException(nameof(store));
            }

            public string Name => "actors";

            public void CopyActorIds(List<int> destination)
            {
                destination.Clear();
                destination.AddRange(_actors.CopyActorIdsInOrder());
            }

            public void CopyActiveOwnerKeys(List<long> destination)
            {
                if (destination == null) return;
                destination.Clear();
                _actorIds.Clear();
                _actorIds.AddRange(_actors.CopyActorIdsInOrder());
                for (var i = 0; i < _actorIds.Count; i++)
                {
                    var actorId = _actorIds[i];
                    if (_store.TryGet(actorId, out _)) destination.Add(actorId);
                }
            }
        }
    }
}
