using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Observability;
using AbilityKit.Demo.Moba.Rollback;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 作用域内的持续行为管理器，用于管理 Buff、技能管线、移动和其他运行时过程。
    /// </summary>
    [WorldService(typeof(IContinuousManager), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaContinuousManager), WorldLifetime.Scoped)]
    public sealed class MobaContinuousManager : DefaultContinuousManager, IWorldInitializable, IMobaOwnerKeySource, System.IDisposable
    {
        public event Action<IContinuous> TagRequirementsChanged;
        private readonly List<IMobaContinuousIntervalHandler> _intervalHandlers = new List<IMobaContinuousIntervalHandler>();
        private MobaContinuousModifierProjectorRegistry _modifierProjectors;
        private MobaContinuousTickProcessor _tickProcessor;
        private IMobaEffectiveTagQueryService _effectiveTags;
        private IWorldResolver _services;
        private MobaContinuousLifecycleBinder _lifecycleBinder;
        private MobaContinuousContextLifecycleBinder _contextLifecycleBinder;
        private MobaContinuousOwnerBoundTriggerLifecycleBinder _ownerBoundTriggerBinder;
        private BuffContinuousIntervalHandler _buffIntervalHandler;
        private MobaTriggerIntervalContinuousHandler _triggerIntervalHandler;

        public void OnInit(IWorldResolver services)
        {
            _services = services;
            services.TryResolve(out MobaConfigDatabase configs);
            services.TryResolve(out AbilityKit.Triggering.Eventing.IEventBus eventBus);
            services.TryResolve(out MobaEffectExecutionService effects);
            services.TryResolve(out MobaExecutionContextRegistry executionContexts);
            services.TryResolve(out IFrameTime frameTime);
            services.TryResolve(out MobaPresentationCueSnapshotService cueSnapshots);
            services.TryResolve(out MobaRuntimeContextService runtimeContexts);
            services.TryResolve(out IMobaBuffLifecycleHook observationHook);
            services.TryResolve(out AbilityKit.Demo.Moba.Services.Triggering.MobaTriggerPlanSubscriptionService triggerSubscriptions);
            services.TryResolve(out AbilityKit.Demo.Moba.Services.Triggering.MobaOwnerBoundTriggerGateService ownerBoundTriggerGates);
            services.TryResolve(out MobaTriggerExecutionGateway triggerGateway);
            services.TryResolve(out MobaCombatActivityService combatActivity);
            if (triggerGateway == null) triggerGateway = new MobaTriggerExecutionGateway(effects, triggerSubscriptions);

            _modifierProjectors = new MobaContinuousModifierProjectorRegistry();
            _modifierProjectors.Register(new MobaAttributeContinuousModifierProjector());
            _modifierProjectors.Register(new MobaSkillParamContinuousModifierProjector());
            _modifierProjectors.Register(new MobaSkillSlotCandidateProjector());
            _modifierProjectors.Register(new MobaReferenceCandidateProjector());
            _modifierProjectors.OnInit(services);
            _lifecycleBinder = new MobaContinuousLifecycleBinder(_modifierProjectors);
            AddLifecycleBinder(_lifecycleBinder);
            _contextLifecycleBinder = new MobaContinuousContextLifecycleBinder();
            AddLifecycleBinder(_contextLifecycleBinder);
            _ownerBoundTriggerBinder = new MobaContinuousOwnerBoundTriggerLifecycleBinder(triggerGateway, ownerBoundTriggerGates);
            AddLifecycleBinder(_ownerBoundTriggerBinder);
            var buffContextRegistry = new BuffContextRegistry(executionContexts, runtimeContexts, frameTime);
            var events = new BuffEventPublisher(eventBus);
            var stageEffects = new BuffStageEffectExecutor(triggerGateway, executionContexts, frameTime);
            var presentationCues = new MobaBuffPresentationCueReporter(configs, cueSnapshots);
            _buffIntervalHandler = new BuffContinuousIntervalHandler(
                configs,
                events,
                stageEffects,
                presentationCues,
                buffContextRegistry,
                observationHook);
            _intervalHandlers.Add(_buffIntervalHandler);
            _triggerIntervalHandler = new MobaTriggerIntervalContinuousHandler(triggerGateway, combatActivity);
            _intervalHandlers.Add(_triggerIntervalHandler);
            _tickProcessor = new MobaContinuousTickProcessor(_intervalHandlers);
        }

        public void Reproject(IContinuous continuous)
        {
            try { _lifecycleBinder?.Reproject(continuous); }
            catch (Exception failure)
            {
                EndAfterProjectionFailure(continuous, failure);
                throw;
            }
        }

        private void EndAfterProjectionFailure(IContinuous continuous, Exception failure)
        {
            Exception cleanupFailure = null;
            try
            {
                if (continuous != null && !continuous.IsTerminated)
                    TryEnd(continuous, ContinuousEndReason.CleanedUp);
            }
            catch (Exception error) { cleanupFailure = error; }
            try { Unregister(continuous, ContinuousEndReason.CleanedUp); }
            catch (Exception error)
            {
                cleanupFailure = cleanupFailure == null ? error : new AggregateException(cleanupFailure, error);
            }
            if (cleanupFailure != null)
                throw new AggregateException("Modifier projection and continuous cleanup both failed.",
                    failure, cleanupFailure);
        }

        public void RefreshTagRequirements(IContinuous continuous)
        {
            var ownerId = continuous?.Config?.OwnerId ?? 0L;
            if (ownerId <= 0 || ownerId > int.MaxValue) return;
            if (_effectiveTags == null) _services?.TryResolve(out _effectiveTags);
            _effectiveTags?.MarkDirty((int)ownerId);
            if (_services != null && _services.TryResolve<IMobaContinuousTagRuleService>(out var rules))
                rules.ReconcileOwnerFor(continuous);
            TagRequirementsChanged?.Invoke(continuous);
        }

        internal int NextModifierSourceId => _lifecycleBinder?.NextSourceId ?? 1;

        internal void ValidateNextModifierSourceId(int nextSourceId)
        {
            _lifecycleBinder?.ValidateNextSourceId(nextSourceId);
        }

        internal void RestoreNextModifierSourceId(int nextSourceId)
        {
            _lifecycleBinder?.RestoreNextSourceId(nextSourceId);
        }

        public void EndOwnerActor(int actorId)
        {
            if (actorId <= 0) return;
            var registered = GetAllContinuous();
            for (var i = 0; i < registered.Count; i++)
            {
                var continuous = registered[i];
                if (continuous == null || continuous.IsTerminated ||
                    !(continuous.Config is IMobaContinuousProjectionConfig projection) ||
                    projection.OwnerActorId != actorId) continue;
                TryEnd(continuous, ContinuousEndReason.CleanedUp);
            }
        }

        public string Name => "continuous";

        public void CopyActiveOwnerKeys(List<long> destination)
        {
            if (destination == null) return;
            _ownerBoundTriggerBinder?.CopyActiveOwnerKeys(destination);
            if (_ownerBoundTriggerBinder == null) destination.Clear();
        }

        public void Tick(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0f) return;
            if (_effectiveTags == null && (_services == null || !_services.TryResolve(out _effectiveTags)))
                throw new System.InvalidOperationException("MOBA continuous execution requires effective tags.");

            var active = GetAllActiveContinuous();
            for (var i = 0; i < active.Count; i++)
            {
                var continuous = active[i];
                if (continuous == null || continuous.IsTerminated || !continuous.IsActive || continuous.IsPaused) continue;
                if (!_effectiveTags.CanExecute(continuous))
                {
                    (continuous as IMobaContinuousExecutionBlockedHandler)?.OnExecutionBlocked();
                    continue;
                }

                if (continuous is IMobaTickableContinuous tickable)
                {
                    tickable.TickManaged(deltaTimeSeconds);
                }

                if (continuous.IsActive && !continuous.IsTerminated &&
                    continuous.Config is IMobaContinuousModifierConfig modifiers &&
                    modifiers.Modifiers != null)
                {
                    for (var m = 0; m < modifiers.Modifiers.Count; m++)
                    {
                        var magnitude = modifiers.Modifiers[m]?.Magnitude;
                        if (magnitude?.Type != AbilityKit.Modifiers.MagnitudeSourceType.TimeDecay ||
                            continuous.ElapsedSeconds - deltaTimeSeconds >= magnitude.Value.Duration)
                            continue;
                        try { _lifecycleBinder?.ReprojectTimeVarying(continuous); }
                        catch (Exception failure)
                        {
                            EndAfterProjectionFailure(continuous, failure);
                            throw;
                        }
                        break;
                    }
                }

                _tickProcessor?.Tick(continuous, deltaTimeSeconds);

                if (continuous is IMobaContinuousRuntimeStateSync stateSync)
                {
                    stateSync.SyncManagedState();
                }
            }
        }

        public void Dispose()
        {
            TagRequirementsChanged = null;
            _intervalHandlers.Clear();
            _ownerBoundTriggerBinder?.Dispose();
            _ownerBoundTriggerBinder = null;
            _buffIntervalHandler = null;
            _triggerIntervalHandler = null;
            _tickProcessor = null;
            _effectiveTags = null;
            _services = null;
            _contextLifecycleBinder = null;
            _lifecycleBinder = null;
            _modifierProjectors = null;
        }
    }
}
