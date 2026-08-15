using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Components;

using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;

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
 
        public BuffContinuousIntervalHandler(MobaConfigDatabase configs, BuffEventPublisher events, BuffStageEffectExecutor stageEffects, MobaBuffPresentationCueReporter presentationCues, BuffContextRegistry contextRegistry)
        {
            _configs = configs;
            _events = events;
            _stageEffects = stageEffects;
            _presentationCues = presentationCues;
            _contextRegistry = contextRegistry;
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
            _presentationCues?.Ticked(buff, sourceActorId, targetActorId, runtime);
            _stageEffects?.Execute(periodicConfig.IntervalEffectIds, buff.Id, sourceActorId, targetActorId, sourceContextId, MobaBuffTriggering.Stages.Interval, runtime);
        }
    }
}

