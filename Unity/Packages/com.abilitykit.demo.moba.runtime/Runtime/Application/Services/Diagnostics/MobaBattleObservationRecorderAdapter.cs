using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services.Observability;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IMobaTriggerAnalysisHook), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaEffectLifecycleHook), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaBuffLifecycleHook), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaBattleObservationRecorderAdapter), WorldLifetime.Scoped)]
    public sealed class MobaBattleObservationRecorderAdapter :
        IMobaTriggerAnalysisHook,
        IMobaEffectLifecycleHook,
        IMobaBuffLifecycleHook,
        IService
    {
        [WorldInject(required: false)]
        private IMobaBattleDiagnosticEventSink _sink = null;

        public MobaBattleObservationRecorderAdapter()
        {
        }

        public MobaBattleObservationRecorderAdapter(IMobaBattleDiagnosticEventSink sink)
        {
            _sink = sink;
        }

        bool IMobaTriggerAnalysisHook.IsEnabled =>
            _sink.IsEnabled(BattleDiagnosticEventChannel.Trigger);

        bool IMobaEffectLifecycleHook.IsEnabled =>
            _sink.IsEnabled(BattleDiagnosticEventChannel.Effect);

        bool IMobaBuffLifecycleHook.IsEnabled =>
            _sink.IsEnabled(BattleDiagnosticEventChannel.Buff);

        void IMobaTriggerAnalysisHook.OnObserved(
            in MobaTriggerAnalysisObservation observation)
        {
            if (!_sink.IsEnabled(BattleDiagnosticEventChannel.Trigger)) return;

            try
            {
                var draft = MobaEffectDiagnosticProducer.CreateTriggerAnalysisDraft(
                    observation.TriggerId,
                    observation.ContextKind,
                    observation.OriginKind,
                    (BattleDiagnosticTriggerAnalysisStage)observation.Stage,
                    (BattleDiagnosticTriggerAnalysisResult)observation.Result,
                    observation.SourceActorId,
                    observation.TargetActorId,
                    observation.ContextId,
                    observation.RootContextId,
                    observation.DetailCode,
                    observation.CurrentDepth,
                    observation.CurrentFrameCount,
                    observation.CurrentRootCount,
                    observation.CurrentSameTriggerCount,
                    observation.FailureKey,
                    observation.Reason);
                _sink.TryCollect(in draft);
            }
            catch
            {
                // Observation recording must never affect trigger execution.
            }
        }

        void IMobaEffectLifecycleHook.OnObserved(
            in MobaEffectLifecycleObservation observation)
        {
            if (!_sink.IsEnabled(BattleDiagnosticEventChannel.Effect)) return;

            try
            {
                MobaBattleDiagnosticEventDraft draft;
                switch (observation.Stage)
                {
                    case MobaEffectLifecycleStage.Started:
                        draft = MobaEffectDiagnosticProducer.CreateEffectStartedDraft(
                            observation.EffectConfigId,
                            observation.TriggerId,
                            observation.SourceActorId,
                            observation.TargetActorId,
                            observation.EffectContextId,
                            observation.RootContextId);
                        break;
                    case MobaEffectLifecycleStage.Ended:
                        draft = MobaEffectDiagnosticProducer.CreateEffectEndedDraft(
                            observation.EffectConfigId,
                            observation.TriggerId,
                            observation.SourceActorId,
                            observation.TargetActorId,
                            observation.EffectContextId,
                            observation.RootContextId,
                            observation.Succeeded);
                        break;
                    default:
                        return;
                }
                _sink.TryCollect(in draft);
            }
            catch
            {
                // Observation recording must never affect effect execution.
            }
        }

        void IMobaBuffLifecycleHook.OnObserved(
            in MobaBuffLifecycleObservation observation)
        {
            if (!_sink.IsEnabled(BattleDiagnosticEventChannel.Buff)) return;
            if (observation.Stage < MobaBuffLifecycleStage.Applied ||
                observation.Stage > MobaBuffLifecycleStage.Removed) return;

            try
            {
                var payloadData = new BattleDiagnosticBuffLifecyclePayload(
                    (BattleDiagnosticBuffLifecycleStage)observation.Stage,
                    NonNegative(observation.StackCount),
                    NonNegative(observation.PreviousStackCount),
                    ToMilliseconds(observation.DurationSeconds),
                    ToMilliseconds(observation.RemainingSeconds),
                    ToMilliseconds(observation.IntervalRemainingSeconds),
                    NonNegative(observation.MaxStacks),
                    NonNegative(observation.ModifierBindingCount),
                    observation.ModifierSourceId,
                    (int)observation.RemoveReason);
                var handle = observation.SkillRuntime;
                var skillRuntime = handle.IsValid
                    ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                    : default;
                var draft = new MobaBattleDiagnosticEventDraft(
                    observation.Stage == MobaBuffLifecycleStage.Removed
                        ? BattleDiagnosticEventKind.BuffRemoved
                        : BattleDiagnosticEventKind.BuffAdded,
                    BattleDiagnosticEventChannel.Buff,
                    BattleDiagnosticEventOutcome.Succeeded,
                    observation.SourceActorId,
                    observation.TargetActorId,
                    observation.BuffId,
                    observation.RootContextId != 0L
                        ? observation.RootContextId
                        : observation.ContextId,
                    observation.ContextId,
                    skillRuntime,
                    payloadVersion: BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion,
                    summary: $"buffId={observation.BuffId}, stage={observation.Stage}, stack={observation.StackCount}",
                    payload: BattleDiagnosticEventPayload.FromBuffLifecycle(in payloadData));
                _sink.TryCollect(in draft);
            }
            catch
            {
                // Observation recording must never affect Buff execution.
            }
        }

        private static int NonNegative(int value) => value < 0 ? 0 : value;

        private static int ToMilliseconds(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return 0;
            var milliseconds = seconds * 1000f;
            return milliseconds >= int.MaxValue ? int.MaxValue : (int)(milliseconds + 0.5f);
        }

        public void Dispose()
        {
        }
    }
}
