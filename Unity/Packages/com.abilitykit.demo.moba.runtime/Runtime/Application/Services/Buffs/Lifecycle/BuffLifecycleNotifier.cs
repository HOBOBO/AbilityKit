using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;
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
        private readonly IMobaBattleDiagnosticEventSink _diagnostics;

        public BuffLifecycleNotifier(
            BuffEventPublisher events,
            BuffStageEffectExecutor stageEffects,
            MobaBuffPresentationCueReporter presentationCues,
            IMobaBattleDiagnosticEventSink diagnostics = null)
        {
            _events = events;
            _stageEffects = stageEffects;
            _presentationCues = presentationCues;
            _diagnostics = diagnostics;
        }

        public void AppliedNew(BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            if (buff == null || runtime == null) return;

            _events?.PublishApplyOrRefresh(buff, sourceActorId, targetActorId, durationSeconds, runtime);
            CollectDiagnostic(BattleDiagnosticBuffLifecycleStage.Applied, buff, sourceActorId, targetActorId, durationSeconds, runtime, 0);
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
                    ? BattleDiagnosticBuffLifecycleStage.StackChanged
                    : BattleDiagnosticBuffLifecycleStage.Refreshed;
                CollectDiagnostic(stage, buff, sourceActorId, targetActorId, durationSeconds, runtime, oldStackCount);
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
            CollectDiagnostic(BattleDiagnosticBuffLifecycleStage.Removed, buff, sourceActorId, targetActorId, 0f, runtime, runtime.StackCount, reason);
            _presentationCues?.Ended(buff, sourceActorId, targetActorId, runtime, reason);
            _stageEffects?.Execute(buff.OnRemoveEffects, buff.Id, sourceActorId, targetActorId, runtime.SourceContextId, MobaBuffTriggering.Stages.Remove, runtime, reason);
        }

        private void DispatchAddEffects(BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            _stageEffects?.Execute(buff.OnAddEffects, buff.Id, sourceActorId, targetActorId, runtime.SourceContextId, MobaBuffTriggering.Stages.Add, runtime, durationSeconds: durationSeconds);
            _events?.PublishPerEffect(MobaBuffTriggering.Events.ApplyOrRefresh, buff.OnAddEffects, MobaBuffTriggering.Stages.Add, sourceActorId, targetActorId, runtime);
        }

        private void CollectDiagnostic(
            BattleDiagnosticBuffLifecycleStage stage,
            BuffMO buff,
            int sourceActorId,
            int targetActorId,
            float durationSeconds,
            BuffRuntime runtime,
            int previousStackCount,
            TraceLifecycleReason reason = TraceLifecycleReason.None)
        {
            if (_diagnostics == null || buff == null || runtime == null) return;

            try
            {
                var continuous = runtime.Continuous;
                var payload = new BattleDiagnosticBuffLifecyclePayload(
                    stage,
                    runtime.StackCount < 0 ? 0 : runtime.StackCount,
                    previousStackCount < 0 ? 0 : previousStackCount,
                    ToMilliseconds(durationSeconds),
                    ToMilliseconds(runtime.Remaining),
                    ToMilliseconds(runtime.IntervalRemainingSeconds),
                    buff.MaxStacks < 0 ? 0 : buff.MaxStacks,
                    runtime.ModifierBindings?.Count ?? 0,
                    continuous?.ModifierSourceId ?? 0,
                    (int)reason);
                var handle = runtime.SkillRuntimeHandle;
                var skillRuntime = handle.IsValid
                    ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                    : default;
                var source = runtime.ContextSource;
                var rootContextId = source.RootContextId != 0
                    ? source.RootContextId
                    : runtime.Origin.EffectiveRootContextId;
                if (rootContextId == 0) rootContextId = runtime.SourceContextId;
                var contextId = runtime.RuntimeContextId != 0
                    ? runtime.RuntimeContextId
                    : runtime.SourceContextId;
                var draft = new MobaBattleDiagnosticEventDraft(
                    stage == BattleDiagnosticBuffLifecycleStage.Removed
                        ? BattleDiagnosticEventKind.BuffRemoved
                        : BattleDiagnosticEventKind.BuffAdded,
                    BattleDiagnosticEventChannel.Buff,
                    BattleDiagnosticEventOutcome.Succeeded,
                    sourceActorId,
                    targetActorId,
                    buff.Id,
                    rootContextId,
                    contextId,
                    skillRuntime,
                    payloadVersion: BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion,
                    summary: $"buffId={buff.Id}, stage={stage}, stack={runtime.StackCount}",
                    payload: BattleDiagnosticEventPayload.FromBuffLifecycle(in payload));
                _diagnostics.TryCollect(in draft);
            }
            catch
            {
                // Diagnostic collection must not affect the committed Buff lifecycle.
            }
        }

        private static int ToMilliseconds(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return 0;
            var milliseconds = seconds * 1000f;
            return milliseconds >= int.MaxValue ? int.MaxValue : (int)(milliseconds + 0.5f);
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
