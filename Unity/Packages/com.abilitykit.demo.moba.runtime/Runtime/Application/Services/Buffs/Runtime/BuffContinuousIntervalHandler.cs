using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Components;

using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;
using AbilityKit.Demo.Moba.Services.Observability;

namespace AbilityKit.Demo.Moba.Services.Buffs.Runtime {
    /// <summary>
    /// Buff 持续行为的间隔回调处理器：在 interval 到达时派发事件、表现 cue 和 interval 阶段效果。
    /// </summary>
    internal sealed class BuffContinuousIntervalHandler : IMobaContinuousIntervalHandler
    {
        private readonly MobaConfigDatabase _configs;
        private readonly BuffEventPublisher _events;
        private readonly BuffStageEffectExecutor _stageEffects;
        private readonly MobaBuffPresentationCueReporter _presentationCues;
        private readonly BuffContextRegistry _contextRegistry;
        private readonly IMobaBuffLifecycleHook _observationHook;
 
        public BuffContinuousIntervalHandler(
            MobaConfigDatabase configs,
            BuffEventPublisher events,
            BuffStageEffectExecutor stageEffects,
            MobaBuffPresentationCueReporter presentationCues,
            BuffContextRegistry contextRegistry,
            IMobaBuffLifecycleHook observationHook = null)
        {
            _configs = configs;
            _events = events;
            _stageEffects = stageEffects;
            _presentationCues = presentationCues;
            _contextRegistry = contextRegistry;
            _observationHook = observationHook;
        }

        public bool CanHandle(IContinuous continuous)
        {
            return continuous is BuffContinuousRuntime;
        }

        /// <summary>
        /// 处理单次 Buff 间隔推进。执行上下文优先来自持续行为，缺失字段回退到绑定的 BuffRuntime。
        /// </summary>
        public void OnInterval(IContinuous continuous, IMobaContinuousPeriodicConfig periodicConfig, in MobaCombatExecutionContext executionContext)
        {
            var buffContinuous = continuous as BuffContinuousRuntime;
            if (buffContinuous == null || periodicConfig == null) return;
            if (_configs == null || !_configs.TryGetBuff(buffContinuous.BuffId, out var buff) || buff == null) return;

            var runtime = buffContinuous.Runtime;
            if (runtime == null) return;

            var sourceActorId = executionContext.SourceActorId > 0 ? executionContext.SourceActorId : runtime.SourceId;
            var targetActorId = executionContext.TargetActorId > 0 ? executionContext.TargetActorId : buffContinuous.TargetActorId;
            var sourceContextId = executionContext.ParentContextId != 0 ? executionContext.ParentContextId : runtime.SourceContextId;
            _contextRegistry?.BindRuntimeContext(runtime, targetActorId, MobaRuntimeContextLifecycleState.Interval);
            _events?.PublishInterval(buff, sourceActorId, targetActorId, runtime);
            PublishObservation(buff, buffContinuous, sourceActorId, targetActorId, sourceContextId, executionContext.RootContextId, runtime);
            _presentationCues?.Ticked(buff, sourceActorId, targetActorId, runtime);
            _stageEffects?.Execute(periodicConfig.IntervalEffectIds, buff.Id, sourceActorId, targetActorId, sourceContextId, MobaBuffTriggering.Stages.Interval, runtime);
        }

        private void PublishObservation(
            BuffMO buff,
            BuffContinuousRuntime continuous,
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long rootContextId,
            BuffRuntime runtime)
        {
            if (buff == null || continuous == null || runtime == null ||
                _observationHook == null || !_observationHook.IsEnabled) return;

            try
            {
                var handle = runtime.SkillRuntimeHandle;
                if (rootContextId == 0) rootContextId = runtime.ContextSource.RootContextId;
                if (rootContextId == 0) rootContextId = runtime.Origin.EffectiveRootContextId;
                if (rootContextId == 0) rootContextId = sourceContextId;
                var contextId = runtime.RuntimeContextId != 0
                    ? runtime.RuntimeContextId
                    : sourceContextId;
                var observation = new MobaBuffLifecycleObservation(
                    MobaBuffLifecycleStage.Interval,
                    buff.Id,
                    sourceActorId,
                    targetActorId,
                    rootContextId,
                    contextId,
                    in handle,
                    runtime.StackCount,
                    runtime.StackCount,
                    0f,
                    runtime.Remaining,
                    runtime.IntervalRemainingSeconds,
                    buff.MaxStacks,
                    runtime.ModifierBindings?.Count ?? 0,
                    continuous.ModifierSourceId);
                _observationHook.OnObserved(in observation);
            }
            catch
            {
                // Diagnostic collection must not affect interval execution.
            }
        }

    }
}

