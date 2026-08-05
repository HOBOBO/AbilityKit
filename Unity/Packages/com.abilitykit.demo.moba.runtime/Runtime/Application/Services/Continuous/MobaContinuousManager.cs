using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Core.Continuous;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 作用域内的持续行为管理器，用于管理 Buff、技能管线、移动和其他运行时过程。
    /// </summary>
    [WorldService(typeof(IContinuousManager), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaContinuousManager), WorldLifetime.Scoped)]
    public sealed class MobaContinuousManager : DefaultContinuousManager, IWorldInitializable, System.IDisposable
    {
        private readonly List<IMobaContinuousIntervalHandler> _intervalHandlers = new List<IMobaContinuousIntervalHandler>();
        private MobaContinuousModifierProjectorRegistry _modifierProjectors;
        private MobaContinuousTickProcessor _tickProcessor;
        private MobaContinuousLifecycleBinder _lifecycleBinder;
        private MobaContinuousContextLifecycleBinder _contextLifecycleBinder;
        private MobaContinuousOwnerBoundTriggerLifecycleBinder _ownerBoundTriggerBinder;
        private BuffContinuousIntervalHandler _buffIntervalHandler;
        private MobaTriggerIntervalContinuousHandler _triggerIntervalHandler;

        public void OnInit(IWorldResolver services)
        {
            services.TryResolve(out MobaConfigDatabase configs);
            services.TryResolve(out AbilityKit.Triggering.Eventing.IEventBus eventBus);
            services.TryResolve(out MobaEffectExecutionService effects);
            services.TryResolve(out MobaTraceRegistry trace);
            services.TryResolve(out AbilityKit.Ability.Triggering.Runtime.ITriggerActionRunner actionRunner);
            services.TryResolve(out IFrameTime frameTime);
            services.TryResolve(out MobaPresentationCueSnapshotService cueSnapshots);
            services.TryResolve(out MobaRuntimeContextService runtimeContexts);
            services.TryResolve(out AbilityKit.Demo.Moba.Services.Triggering.MobaTriggerPlanSubscriptionService triggerSubscriptions);
            services.TryResolve(out AbilityKit.Demo.Moba.Services.Triggering.MobaOwnerBoundTriggerGateService ownerBoundTriggerGates);
            services.TryResolve(out MobaTriggerExecutionGateway triggerGateway);
            services.TryResolve(out MobaCombatActivityService combatActivity);
            if (triggerGateway == null) triggerGateway = new MobaTriggerExecutionGateway(effects, triggerSubscriptions);

            _modifierProjectors = new MobaContinuousModifierProjectorRegistry();
            _modifierProjectors.Register(new MobaAttributeContinuousModifierProjector());
            _modifierProjectors.Register(new MobaSkillParamContinuousModifierProjector());
            _modifierProjectors.OnInit(services);
            _lifecycleBinder = new MobaContinuousLifecycleBinder(_modifierProjectors);
            AddLifecycleBinder(_lifecycleBinder);
            _contextLifecycleBinder = new MobaContinuousContextLifecycleBinder(trace, actionRunner);
            AddLifecycleBinder(_contextLifecycleBinder);
            _ownerBoundTriggerBinder = new MobaContinuousOwnerBoundTriggerLifecycleBinder(triggerGateway, ownerBoundTriggerGates);
            AddLifecycleBinder(_ownerBoundTriggerBinder);
            var buffContextRegistry = new BuffContextRegistry(trace, runtimeContexts, actionRunner, frameTime);
            var events = new BuffEventPublisher(eventBus);
            var stageEffects = new BuffStageEffectExecutor(triggerGateway);
            var presentationCues = new MobaBuffPresentationCueReporter(configs, cueSnapshots);
            _buffIntervalHandler = new BuffContinuousIntervalHandler(configs, events, stageEffects, presentationCues, buffContextRegistry);
            _intervalHandlers.Add(_buffIntervalHandler);
            _triggerIntervalHandler = new MobaTriggerIntervalContinuousHandler(triggerGateway, combatActivity);
            _intervalHandlers.Add(_triggerIntervalHandler);
            _tickProcessor = new MobaContinuousTickProcessor(_intervalHandlers);
        }

        public void Reproject(IContinuous continuous)
        {
            _lifecycleBinder?.Reproject(continuous);
        }

        public void Tick(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0f) return;

            var active = GetAllActiveContinuous();
            for (var i = 0; i < active.Count; i++)
            {
                var continuous = active[i];
                if (continuous == null || continuous.IsTerminated || !continuous.IsActive || continuous.IsPaused) continue;

                if (continuous is IMobaTickableContinuous tickable)
                {
                    tickable.TickManaged(deltaTimeSeconds);
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
            _intervalHandlers.Clear();
            _ownerBoundTriggerBinder?.Dispose();
            _ownerBoundTriggerBinder = null;
            _buffIntervalHandler = null;
            _triggerIntervalHandler = null;
            _tickProcessor = null;
            _contextLifecycleBinder = null;
            _lifecycleBinder = null;
            _modifierProjectors = null;
        }
    }
}
