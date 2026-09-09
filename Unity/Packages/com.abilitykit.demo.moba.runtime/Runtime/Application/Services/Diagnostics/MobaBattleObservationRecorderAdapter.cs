using System;
using System.Collections.Generic;
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
        internal const int TriggerAggregateWindowFrames = 60;
        internal const int TriggerAggregateCapacity = 256;

        [WorldInject(required: false)]
        private IMobaBattleDiagnosticEventSink _sink = null;
        private readonly Dictionary<TriggerAggregateKey, TriggerAggregateState>
            _triggerAggregates = new Dictionary<TriggerAggregateKey, TriggerAggregateState>();

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
                if (ShouldAggregate(in observation))
                {
                    CollectAggregated(in observation);
                    return;
                }

                FlushTriggerLane(in observation);
                CollectRaw(in observation);
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

        private static bool ShouldAggregate(in MobaTriggerAnalysisObservation observation)
        {
            return observation.TriggerId > 0 &&
                   BattleDiagnosticFrames.IsValid(observation.Frame) &&
                   observation.Stage == MobaTriggerAnalysisStage.Conditions &&
                   observation.Result == MobaTriggerAnalysisResult.Failed;
        }

        private void CollectRaw(in MobaTriggerAnalysisObservation observation)
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

        private void CollectAggregated(in MobaTriggerAnalysisObservation observation)
        {
            var key = new TriggerAggregateKey(in observation);
            if (!_triggerAggregates.TryGetValue(key, out var state))
            {
                FlushOtherAggregatesInLane(in observation, key);
                EnsureAggregateCapacity();
                _triggerAggregates.Add(key, new TriggerAggregateState(in observation));
                CollectRaw(in observation);
                return;
            }

            if (observation.Frame < state.LastObservedFrame ||
                observation.Frame - state.LastObservedFrame > TriggerAggregateWindowFrames)
            {
                FlushAggregate(key, state);
                _triggerAggregates[key] = new TriggerAggregateState(in observation);
                CollectRaw(in observation);
                return;
            }

            state.Add(in observation);
            if (state.LastFrame - state.FirstFrame + 1 >= TriggerAggregateWindowFrames)
            {
                FlushAggregate(key, state);
                state.ResetPending(observation.Frame);
            }
        }

        private void FlushTriggerLane(in MobaTriggerAnalysisObservation observation)
        {
            if (_triggerAggregates.Count == 0) return;

            var keys = new List<TriggerAggregateKey>();
            foreach (var pair in _triggerAggregates)
            {
                if (!pair.Key.IsSameLane(in observation)) continue;
                FlushAggregate(pair.Key, pair.Value);
                keys.Add(pair.Key);
            }

            for (var i = 0; i < keys.Count; i++) _triggerAggregates.Remove(keys[i]);
        }

        private void FlushOtherAggregatesInLane(
            in MobaTriggerAnalysisObservation observation,
            TriggerAggregateKey currentKey)
        {
            if (_triggerAggregates.Count == 0) return;

            var keys = new List<TriggerAggregateKey>();
            foreach (var pair in _triggerAggregates)
            {
                if (pair.Key.Equals(currentKey) || !pair.Key.IsSameLane(in observation)) continue;
                FlushAggregate(pair.Key, pair.Value);
                keys.Add(pair.Key);
            }

            for (var i = 0; i < keys.Count; i++) _triggerAggregates.Remove(keys[i]);
        }

        private void EnsureAggregateCapacity()
        {
            if (_triggerAggregates.Count < TriggerAggregateCapacity) return;

            var hasOldest = false;
            var oldestKey = default(TriggerAggregateKey);
            TriggerAggregateState oldestState = null;
            foreach (var pair in _triggerAggregates)
            {
                if (hasOldest && pair.Value.LastObservedFrame >= oldestState.LastObservedFrame) continue;
                hasOldest = true;
                oldestKey = pair.Key;
                oldestState = pair.Value;
            }

            if (!hasOldest) return;
            FlushAggregate(oldestKey, oldestState);
            _triggerAggregates.Remove(oldestKey);
        }

        private void FlushAggregate(TriggerAggregateKey key, TriggerAggregateState state)
        {
            if (state == null || state.OccurrenceCount <= 0) return;

            var payload = new BattleDiagnosticTriggerAnalysisAggregatePayload(
                key.TriggerId,
                key.ContextKind,
                key.OriginKind,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                key.DetailCode,
                state.OccurrenceCount,
                state.FirstFrame,
                state.LastFrame,
                state.FirstContextId,
                state.LastContextId,
                state.FirstRootContextId,
                state.LastRootContextId,
                key.FailureKey,
                state.SampleReason);
            var draft = MobaEffectDiagnosticProducer.CreateTriggerAnalysisAggregateDraft(
                in payload,
                key.SourceActorId,
                key.TargetActorId);
            _sink.TryCollect(in draft);
        }

        private void FlushAllTriggerAggregates()
        {
            foreach (var pair in _triggerAggregates)
            {
                FlushAggregate(pair.Key, pair.Value);
            }
            _triggerAggregates.Clear();
        }

        private static int ToMilliseconds(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return 0;
            var milliseconds = seconds * 1000f;
            return milliseconds >= int.MaxValue ? int.MaxValue : (int)(milliseconds + 0.5f);
        }

        public void Dispose()
        {
            try
            {
                FlushAllTriggerAggregates();
            }
            catch
            {
                // Diagnostic aggregation must not affect world disposal.
            }
        }

        private readonly struct TriggerAggregateKey : IEquatable<TriggerAggregateKey>
        {
            public TriggerAggregateKey(in MobaTriggerAnalysisObservation observation)
            {
                TriggerId = observation.TriggerId;
                ContextKind = observation.ContextKind;
                OriginKind = observation.OriginKind;
                DetailCode = observation.DetailCode;
                SourceActorId = observation.SourceActorId;
                TargetActorId = observation.TargetActorId;
                FailureKey = observation.FailureKey ?? string.Empty;
            }

            public int TriggerId { get; }
            public int ContextKind { get; }
            public int OriginKind { get; }
            public int DetailCode { get; }
            public int SourceActorId { get; }
            public int TargetActorId { get; }
            public string FailureKey { get; }

            public bool IsSameLane(in MobaTriggerAnalysisObservation observation)
            {
                return TriggerId == observation.TriggerId &&
                       ContextKind == observation.ContextKind &&
                       OriginKind == observation.OriginKind &&
                       SourceActorId == observation.SourceActorId &&
                       TargetActorId == observation.TargetActorId;
            }

            public bool Equals(TriggerAggregateKey other)
            {
                return TriggerId == other.TriggerId &&
                       ContextKind == other.ContextKind &&
                       OriginKind == other.OriginKind &&
                       DetailCode == other.DetailCode &&
                       SourceActorId == other.SourceActorId &&
                       TargetActorId == other.TargetActorId &&
                       string.Equals(FailureKey, other.FailureKey, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TriggerAggregateKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = TriggerId;
                    hashCode = (hashCode * 397) ^ ContextKind;
                    hashCode = (hashCode * 397) ^ OriginKind;
                    hashCode = (hashCode * 397) ^ DetailCode;
                    hashCode = (hashCode * 397) ^ SourceActorId;
                    hashCode = (hashCode * 397) ^ TargetActorId;
                    return (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(FailureKey);
                }
            }
        }

        private sealed class TriggerAggregateState
        {
            public TriggerAggregateState(in MobaTriggerAnalysisObservation observation)
            {
                LastObservedFrame = observation.Frame;
                ResetPending(observation.Frame);
            }

            public int OccurrenceCount { get; private set; }
            public int FirstFrame { get; private set; }
            public int LastFrame { get; private set; }
            public int LastObservedFrame { get; private set; }
            public long FirstContextId { get; private set; }
            public long LastContextId { get; private set; }
            public long FirstRootContextId { get; private set; }
            public long LastRootContextId { get; private set; }
            public string SampleReason { get; private set; } = string.Empty;

            public void Add(in MobaTriggerAnalysisObservation observation)
            {
                if (OccurrenceCount == 0)
                {
                    FirstFrame = observation.Frame;
                    FirstContextId = observation.ContextId;
                    FirstRootContextId = observation.RootContextId;
                    SampleReason = observation.Reason ?? string.Empty;
                }

                OccurrenceCount++;
                LastFrame = observation.Frame;
                LastObservedFrame = observation.Frame;
                LastContextId = observation.ContextId;
                LastRootContextId = observation.RootContextId;
            }

            public void ResetPending(int lastObservedFrame)
            {
                OccurrenceCount = 0;
                FirstFrame = BattleDiagnosticFrames.Invalid;
                LastFrame = BattleDiagnosticFrames.Invalid;
                LastObservedFrame = lastObservedFrame;
                FirstContextId = 0L;
                LastContextId = 0L;
                FirstRootContextId = 0L;
                LastRootContextId = 0L;
                SampleReason = string.Empty;
            }
        }
    }
}
