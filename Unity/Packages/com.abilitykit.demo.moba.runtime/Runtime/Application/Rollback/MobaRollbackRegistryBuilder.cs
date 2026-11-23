using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Combat.Navigation;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Navigation;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Services.Passive;
using AbilityKit.Demo.Moba.Services.Projectile;
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
        /// <summary>
        /// 为客户端预测装配常规世界状态与静态导航依赖，并冻结实际 Path 消费的导航服务。
        /// 只能在预测会话装配时调用，普通世界使用 <see cref="Create"/>。
        /// </summary>
        public static RollbackRegistry CreateForPrediction(IWorld world)
        {
            if (world?.Services == null)
                throw new ArgumentException("Rollback world and services are required.", nameof(world));
            if (!world.Services.TryResolve<INavigationService>(out var queries) ||
                !(queries is MobaNavigationService navigation) ||
                !world.Services.TryResolve<MobaNavigationService>(out var builder) || !ReferenceEquals(navigation, builder))
                throw new InvalidOperationException("Prediction requires the same standard navigation service for map build and Path queries.");

            var registry = Create(world, restorePathSlots: true);
            if (world.Services.TryResolve<MobaEntityManager>(out var entities) &&
                entities.TryGetActorRollbackCommands(out var commands) &&
                world.Services.TryResolve<MobaBrainService>(out var brains))
                commands.EnableNaturalExpiryRetention(brains);
            navigation.EnablePredictionNavigation();
            registry.Register(new MobaPredictionNavigationRollbackProvider(navigation));
            registry.RequireState(MobaPredictionNavigationRollbackProvider.DefaultKey);
            return registry;
        }

        public static RollbackRegistry Create(IWorld world)
            => Create(world, restorePathSlots: false);

        private static RollbackRegistry Create(IWorld world, bool restorePathSlots)
        {
            var registry = new RollbackRegistry();
            if (world?.Services == null)
                throw new ArgumentException("Rollback world and services are required.", nameof(world));

            MobaEntitasComponentRollbackProvider componentProvider = null;
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
                    brainsForEntityCleanup,
                    restorePathSlots ? actorCommands : null));
                componentProvider = new MobaEntitasComponentRollbackProvider(
                    contexts.actor,
                    restoresMissingActors);
                registry.Register(componentProvider);
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
                world.Services.TryResolve<MobaTriggerModifierContinuousService>(out _) &&
                world.Services.TryResolve<MobaAreaRuntimeService>(out _);
            if (jointLifecycle)
            {
                registry.Register(new MobaSkillLifecycleRollbackProvider(world, ownerProvider, restorePathSlots));
                if (componentProvider != null && world.Services.TryResolve<MobaPassiveSkillLifecycleService>(out var restoredPassives))
                    componentProvider.PassiveRollbackRebind = restoredPassives;
            }
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
                registry.Register(new MobaActorMotionRollbackProvider(actorRegistry));
                /*
                 * 运行依赖和策略值独立归属，不复制 ActorMotion 或来源生命周期的状态。
                 */
                if (world.Services.TryResolve<AbilityKit.Demo.Moba.Services.Motion.MobaMotionRuntimeDependencyService>(out var motionDependencies) && motionDependencies != null)
                    registry.Register(new MobaMotionRuntimeDependencyRollbackProvider(actorRegistry, motionDependencies));
                registry.Register(new MobaActorHpRollbackProvider(actorRegistry));
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
                    world.Services.TryResolve<MobaEntityManager>(out var brainEntities);
                    world.Services.TryResolve<MobaSummonService>(out var brainSummons);
                    MobaActorRollbackCommandRuntime brainCommands = null;
                    if (restorePathSlots && brainEntities != null) brainEntities.TryGetActorRollbackCommands(out brainCommands);
                    registry.Register(new MobaBrainRollbackProvider(actorRegistry, brainService, brainCommands, brainSummons));
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

            if (projectileRollbackEnabled &&
                world.Services.TryResolve<MobaProjectileLinkService>(out var projectileLinks) && projectileLinks != null &&
                world.Services.TryResolve<MobaSkillCastRuntimeService>(out var projectileSkills) && projectileSkills != null &&
                world.Services.TryResolve<MobaActorRegistry>(out var projectileActors) && projectileActors != null)
                registry.Register(new MobaProjectileLinkRollbackProvider(projectileLinks, projectileSkills, projectiles, projectileActors));

            if (world.Services.TryResolve<MobaSummonService>(out var summonService) && summonService != null &&
                world.Services.TryResolve<MobaActorRegistry>(out var summonActors) && summonActors != null &&
                world.Services.TryResolve<MobaSkillCastRuntimeService>(out var summonSkills) && summonSkills != null)
                registry.Register(new MobaSummonRollbackProvider(summonService, summonActors, summonSkills));

            if (world.Services.TryResolve<MobaActorSpawnSnapshotService>(out var actorSpawnSnapshots) &&
                actorSpawnSnapshots != null)
                registry.Register(new MobaPendingActorSpawnRollbackProvider(actorSpawnSnapshots));

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
                    restoreProjection: false, lifecycleOwnsProjection: jointLifecycle));
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
                MobaActorMotionRollbackProvider.DefaultKey,
                MobaMotionRuntimeDependencyRollbackProvider.DefaultKey,
                MobaActorHpRollbackProvider.DefaultKey,
                MobaSkillLifecycleRollbackProvider.DefaultKey,
                MobaSkillCooldownRollbackProvider.DefaultKey,
                MobaCharacterHfsmRollbackProvider.DefaultKey,
                MobaGameplayRollbackProvider.DefaultKey,
                MobaSkillEconomyRollbackProvider.DefaultKey,
                MobaSkillParamModifierRollbackProvider.DefaultKey,
                MobaDerivedSkillRollbackProvider.DefaultKey,
                MobaTriggerExecutionRuntimeService.DefaultKey,
                MobaActorRollbackCommandRuntime.RollbackProviderKey,
                FrameTimeRollbackStateProvider.DefaultKey,
                RollbackWorldRandom.DefaultKey,
            MobaCombatActivityRollbackProvider.DefaultKey,
            MobaSummonRollbackProvider.DefaultKey,
            MobaPendingActorSpawnRollbackProvider.DefaultKey,
            MobaWorldClockRollbackProvider.DefaultKey,
            };
            if (minionWavesEnabled)
                requiredKeys.Insert(9, MobaMinionRollbackProvider.DefaultKey);
            if (projectileRollbackEnabled)
                requiredKeys.Add(ProjectileRollbackProvider.DefaultKey);
            if (projectileRollbackEnabled)
                requiredKeys.Add(MobaProjectileLinkRollbackProvider.DefaultKey);
            var missing = new List<int>();
            foreach (var key in requiredKeys)
            {
                if (!registry.TryGet(key, out _)) missing.Add(key);
            }

            if (missing.Count != 0)
                throw new InvalidOperationException(
                    $"Battle rollback registry is incomplete. Missing provider keys: {string.Join(", ", missing)}");

            /*
             * 显式结构顺序与状态键身份解耦；三个结构载荷均必须提供，不能从预测态补缺。
             * Motion 运行依赖仍为普通提供者，后续结构绑定方案另行确认。
             */
            registry.SetStructureRestoreOrder(
                MobaEntitasEntityRollbackProvider.DefaultKey,
                MobaActorRollbackCommandRuntime.RollbackProviderKey,
                MobaSkillLifecycleRollbackProvider.DefaultKey);
            registry.RequireState(MobaEntitasEntityRollbackProvider.DefaultKey);
            registry.RequireState(MobaActorRollbackCommandRuntime.RollbackProviderKey);
            registry.RequireState(MobaSkillLifecycleRollbackProvider.DefaultKey);

            /*
             * 旧检查点不得静默保留预测态的世界时钟或战斗事实。
             */
            registry.RequireState(MobaCombatActivityRollbackProvider.DefaultKey);
            registry.RequireState(MobaWorldClockRollbackProvider.DefaultKey);
            /*
             * 缺少独立依赖载荷的旧快照不能静默沿用预测态策略。
             */
            registry.RequireState(MobaMotionRuntimeDependencyRollbackProvider.DefaultKey);

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
