using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;
using AbilityKit.Demo.Moba.Services.Observability;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services.Buffs.Lifecycle
{
    /// <summary>
    /// Buff 生命周期派发器：统一维护事件、表现提示和 stage effect 的对外通知顺序。
    /// </summary>
    internal sealed class BuffLifecycleNotifier
    {
        private readonly BuffEventPublisher _events;
        private readonly BuffStageEffectExecutor _stageEffects;
        private readonly MobaBuffPresentationCueReporter _presentationCues;
        private readonly IMobaBuffLifecycleHook _observationHook;

        public BuffLifecycleNotifier(
            BuffEventPublisher events,
            BuffStageEffectExecutor stageEffects,
            MobaBuffPresentationCueReporter presentationCues,
            IMobaBuffLifecycleHook observationHook = null)
        {
            _events = events;
            _stageEffects = stageEffects;
            _presentationCues = presentationCues;
            _observationHook = observationHook;
        }

        public void AppliedNew(BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            if (buff == null || runtime == null) return;

            _events?.PublishApplyOrRefresh(buff, sourceActorId, targetActorId, durationSeconds, runtime);
            PublishObservation(MobaBuffLifecycleStage.Applied, buff, sourceActorId, targetActorId, durationSeconds, runtime, 0);
            _presentationCues?.Started(buff, sourceActorId, targetActorId, runtime);
            DispatchAddEffects(buff, sourceActorId, targetActorId, durationSeconds, runtime);
        }

        public void AppliedExisting(BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime, int oldStackCount, bool applied)
        {
            if (buff == null || runtime == null) return;

            _events?.PublishApplyOrRefresh(buff, sourceActorId, targetActorId, durationSeconds, runtime);
            if (applied)
            {
                var stage = runtime.StackCount != oldStackCount
                    ? MobaBuffLifecycleStage.StackChanged
                    : MobaBuffLifecycleStage.Refreshed;
                PublishObservation(stage, buff, sourceActorId, targetActorId, durationSeconds, runtime, oldStackCount);
            }
            ReportExistingApplied(buff, sourceActorId, targetActorId, runtime, oldStackCount, applied);
            if (applied)
            {
                DispatchAddEffects(buff, sourceActorId, targetActorId, durationSeconds, runtime);
            }
        }

        public void Removed(BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime, TraceLifecycleReason reason)
        {
            if (buff == null || runtime == null) return;

            _events?.PublishRemove(buff, sourceActorId, targetActorId, runtime, reason);
            PublishObservation(MobaBuffLifecycleStage.Removed, buff, sourceActorId, targetActorId, 0f, runtime, runtime.StackCount, reason);
            _presentationCues?.Ended(buff, sourceActorId, targetActorId, runtime, reason);
            _stageEffects?.Execute(buff.OnRemoveEffects, buff.Id, sourceActorId, targetActorId, runtime.SourceContextId, MobaBuffTriggering.Stages.Remove, runtime, reason);
        }

        private void DispatchAddEffects(BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            _stageEffects?.Execute(buff.OnAddEffects, buff.Id, sourceActorId, targetActorId, runtime.SourceContextId, MobaBuffTriggering.Stages.Add, runtime, durationSeconds: durationSeconds);
            _events?.PublishPerEffect(MobaBuffTriggering.Events.ApplyOrRefresh, buff.OnAddEffects, MobaBuffTriggering.Stages.Add, sourceActorId, targetActorId, runtime);
        }

        private void PublishObservation(
            MobaBuffLifecycleStage stage,
            BuffMO buff,
            int sourceActorId,
            int targetActorId,
            float durationSeconds,
            BuffRuntime runtime,
            int previousStackCount,
            TraceLifecycleReason reason = TraceLifecycleReason.None)
        {
            if (buff == null || runtime == null ||
                _observationHook == null || !_observationHook.IsEnabled) return;

            try
            {
                var continuous = runtime.Continuous;
                var handle = runtime.SkillRuntimeHandle;
                var source = runtime.ContextSource;
                var rootContextId = source.RootContextId != 0
                    ? source.RootContextId
                    : runtime.Origin.EffectiveRootContextId;
                if (rootContextId == 0) rootContextId = runtime.SourceContextId;
                var contextId = runtime.RuntimeContextId != 0
                    ? runtime.RuntimeContextId
                    : runtime.SourceContextId;
                var observation = new MobaBuffLifecycleObservation(
                    stage,
                    buff.Id,
                    sourceActorId,
                    targetActorId,
                    rootContextId,
                    contextId,
                    in handle,
                    runtime.StackCount,
                    previousStackCount,
                    durationSeconds,
                    runtime.Remaining,
                    runtime.IntervalRemainingSeconds,
                    buff.MaxStacks,
                    runtime.ModifierBindings?.Count ?? 0,
                    continuous?.ModifierSourceId ?? 0,
                    reason);
                _observationHook.OnObserved(in observation);
            }
            catch
            {
                // Diagnostic collection must not affect the committed Buff lifecycle.
            }
        }

        private void ReportExistingApplied(BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime, int oldStackCount, bool applied)
        {
            if (!applied) return;

            if (runtime.StackCount != oldStackCount)
            {
                _presentationCues?.StackChanged(buff, sourceActorId, targetActorId, runtime);
                return;
            }

            _presentationCues?.Refreshed(buff, sourceActorId, targetActorId, runtime);
        }
    }
}
