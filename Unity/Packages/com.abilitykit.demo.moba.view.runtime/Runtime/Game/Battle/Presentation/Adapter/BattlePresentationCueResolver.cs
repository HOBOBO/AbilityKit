using System;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    public enum BattlePresentationCueDecisionKind
    {
        None = 0,
        Play = 1,
        Stop = 2,
    }

    public readonly struct BattlePresentationCueSpawnRequest
    {
        public BattlePresentationCueSpawnRequest(
            BattlePresentationCueRequestKey requestKey,
            int vfxId,
            int sourceActorId,
            int targetActorId,
            int firstTargetActorId,
            bool hasExplicitPosition,
            SnapshotVec3 explicitPosition,
            SnapshotVec3 offset,
            int durationMsOverride,
            float scale,
            float radius)
        {
            RequestKey = requestKey;
            VfxId = vfxId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            FirstTargetActorId = firstTargetActorId;
            HasExplicitPosition = hasExplicitPosition;
            ExplicitPosition = explicitPosition;
            Offset = offset;
            DurationMsOverride = durationMsOverride > 0 ? durationMsOverride : 0;
            Scale = scale > 0f ? scale : 1f;
            Radius = radius;
        }

        public BattlePresentationCueRequestKey RequestKey { get; }
        public int VfxId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public int FirstTargetActorId { get; }
        public bool HasExplicitPosition { get; }
        public SnapshotVec3 ExplicitPosition { get; }
        public SnapshotVec3 Offset { get; }
        public int DurationMsOverride { get; }
        public float Scale { get; }
        public float Radius { get; }
 
        public bool IsEmpty => RequestKey.IsEmpty || VfxId <= 0;
    }

    public readonly struct BattlePresentationCueDecision
    {
        public BattlePresentationCueDecision(
            BattlePresentationCueDecisionKind kind,
            BattlePresentationCueRequestKey requestKey,
            BattlePresentationCueSpawnRequest spawnRequest)
        {
            Kind = kind;
            RequestKey = requestKey;
            SpawnRequest = spawnRequest;
        }

        public BattlePresentationCueDecisionKind Kind { get; }
        public BattlePresentationCueRequestKey RequestKey { get; }
        public BattlePresentationCueSpawnRequest SpawnRequest { get; }

        public bool IsNone => Kind == BattlePresentationCueDecisionKind.None || RequestKey.IsEmpty;

        public static BattlePresentationCueDecision None => default;
    }

    public sealed class BattlePresentationCueResolver
    {
        private const int NumericParamRadius = 2;

        public BattlePresentationCueDecision Resolve(in PresentationCueData data)
        {
            var requestKey = BattlePresentationCueRequestKey.From(in data);
            if (requestKey.IsEmpty) return BattlePresentationCueDecision.None;

            if (data.PredictionState == PresentationCuePredictionState.Rejected)
            {
                return new BattlePresentationCueDecision(BattlePresentationCueDecisionKind.Stop, requestKey, default);
            }

            if (ShouldStart(data.Stage) || ShouldKeepActive(data.Stage))
            {
                var spawnRequest = CreateSpawnRequest(requestKey, in data);
                if (spawnRequest.IsEmpty) return BattlePresentationCueDecision.None;

                return new BattlePresentationCueDecision(BattlePresentationCueDecisionKind.Play, requestKey, spawnRequest);
            }

            if (ShouldStop(data.Stage))
            {
                return new BattlePresentationCueDecision(BattlePresentationCueDecisionKind.Stop, requestKey, default);
            }

            return BattlePresentationCueDecision.None;
        }

        public static bool ShouldStart(PresentationCueStage stage)
        {
            return stage == PresentationCueStage.ConditionPassed
                || stage == PresentationCueStage.BeforeAction
                || stage == PresentationCueStage.Executed
                || stage == PresentationCueStage.Started;
        }

        public static bool ShouldKeepActive(PresentationCueStage stage)
        {
            return stage == PresentationCueStage.Ticked
                || stage == PresentationCueStage.Refreshed
                || stage == PresentationCueStage.StackChanged;
        }

        public static bool ShouldStop(PresentationCueStage stage)
        {
            return stage == PresentationCueStage.ConditionFailed
                || stage == PresentationCueStage.Interrupted
                || stage == PresentationCueStage.Skipped
                || stage == PresentationCueStage.Expired
                || stage == PresentationCueStage.Removed
                || stage == PresentationCueStage.Completed;
        }

        public static int ResolveVfxId(in PresentationCueData data)
        {
            if (data.VfxId > 0) return data.VfxId;
            if (data.TemplateId > 0) return data.TemplateId;
            return 0;
        }

        public static BattlePresentationCueSpawnRequest CreateSpawnRequest(
            BattlePresentationCueRequestKey requestKey,
            in PresentationCueData data)
        {
            var vfxId = ResolveVfxId(in data);
            if (requestKey.IsEmpty || vfxId <= 0) return default;

            return new BattlePresentationCueSpawnRequest(
                requestKey,
                vfxId,
                data.SourceActorId,
                data.TargetActorId,
                ResolveFirstTargetActorId(in data),
                data.Positions != null && data.Positions.Count > 0,
                data.Positions != null && data.Positions.Count > 0 ? data.Positions[0] : default,
                new SnapshotVec3(data.OffsetX, data.OffsetY, data.OffsetZ),
                data.DurationMsOverride,
                data.Scale,
                ResolveRadius(in data));
        }

        public static int ResolveFirstTargetActorId(in PresentationCueData data)
        {
            if (data.Targets != null && data.Targets.Count > 0) return data.Targets[0];
            return 0;
        }

        public static float ResolveRadius(in PresentationCueData data)
        {
            if (data.NumericParamKeys == null || data.NumericParamValues == null) return 0f;
            var count = Math.Min(data.NumericParamKeys.Count, data.NumericParamValues.Count);
            for (var i = 0; i < count; i++)
            {
                if (data.NumericParamKeys[i] == NumericParamRadius) return data.NumericParamValues[i];
            }

            return 0f;
        }
    }

    public static class BattlePresentationCueReconciliationMapper
    {
        public static PresentationCueUpdate<
            BattlePresentationCueRequestKey,
            BattlePresentationCueSpawnRequest> CreateUpdate(
            in PresentationCueData data,
            in BattlePresentationCueDecision decision,
            long generation)
        {
            var signal = PresentationCueSignal.None;
            if (decision.Kind == BattlePresentationCueDecisionKind.Play)
            {
                signal = BattlePresentationCueResolver.ShouldKeepActive(data.Stage)
                    ? PresentationCueSignal.Refresh
                    : PresentationCueSignal.Activate;
            }
            else if (decision.Kind == BattlePresentationCueDecisionKind.Stop)
            {
                signal = PresentationCueSignal.Deactivate;
            }

            return new PresentationCueUpdate<
                BattlePresentationCueRequestKey,
                BattlePresentationCueSpawnRequest>(
                BattlePresentationCueRequestKey.From(in data),
                signal,
                ResolveOutcome(data.PredictionState),
                ResolveRevision(in data),
                generation,
                !decision.SpawnRequest.IsEmpty,
                decision.SpawnRequest);
        }

        public static PresentationCuePredictionOutcome ResolveOutcome(
            PresentationCuePredictionState state)
        {
            switch (state)
            {
                case PresentationCuePredictionState.Predicted:
                    return PresentationCuePredictionOutcome.Predicted;
                case PresentationCuePredictionState.ServerConfirmed:
                    return PresentationCuePredictionOutcome.Confirmed;
                case PresentationCuePredictionState.Corrected:
                    return PresentationCuePredictionOutcome.Corrected;
                case PresentationCuePredictionState.Rejected:
                    return PresentationCuePredictionOutcome.Rejected;
                default:
                    return PresentationCuePredictionOutcome.None;
            }
        }

        public static int ResolveRevision(in PresentationCueData data)
        {
            var outcome = ResolveOutcome(data.PredictionState);
            return outcome == PresentationCuePredictionOutcome.Confirmed
                || outcome == PresentationCuePredictionOutcome.Corrected
                || outcome == PresentationCuePredictionOutcome.Rejected
                ? data.ConfirmedFrame
                : data.PredictedFrame;
        }
    }

    public readonly struct BattlePresentationCueRequestKey : IEquatable<BattlePresentationCueRequestKey>
    {
        private readonly string _externalKey;
        private readonly int _triggerKey;
        private readonly int _triggerEventId;
        private readonly int _actionIndex;
        private readonly int _order;
        private readonly int _sourceActorId;
        private readonly int _targetActorId;
        private readonly int _predictionKey;
        private readonly bool _hasExternalKey;
        private readonly bool _hasPredictionKey;
        private readonly bool _hasValue;

        public bool IsEmpty => !_hasValue;

        private BattlePresentationCueRequestKey(string externalKey)
        {
            _externalKey = externalKey;
            _triggerKey = 0;
            _triggerEventId = 0;
            _actionIndex = 0;
            _order = 0;
            _sourceActorId = 0;
            _targetActorId = 0;
            _predictionKey = 0;
            _hasExternalKey = true;
            _hasPredictionKey = false;
            _hasValue = true;
        }

        private BattlePresentationCueRequestKey(int predictionKey)
        {
            _externalKey = null;
            _triggerKey = 0;
            _triggerEventId = 0;
            _actionIndex = 0;
            _order = 0;
            _sourceActorId = 0;
            _targetActorId = 0;
            _predictionKey = predictionKey;
            _hasExternalKey = false;
            _hasPredictionKey = true;
            _hasValue = predictionKey != 0;
        }

        private BattlePresentationCueRequestKey(
            int triggerKey,
            int triggerEventId,
            int actionIndex,
            int order,
            int sourceActorId,
            int targetActorId)
        {
            _externalKey = null;
            _triggerKey = triggerKey;
            _triggerEventId = triggerEventId;
            _actionIndex = actionIndex;
            _order = order;
            _sourceActorId = sourceActorId;
            _targetActorId = targetActorId;
            _predictionKey = 0;
            _hasExternalKey = false;
            _hasPredictionKey = false;
            _hasValue = true;
        }

        public static BattlePresentationCueRequestKey From(in PresentationCueData data)
        {
            if (data.PredictionKey != 0) return FromPredictionKey(data.PredictionKey);
            if (!string.IsNullOrWhiteSpace(data.InstanceKey)) return FromExternal(data.InstanceKey);
            if (!string.IsNullOrWhiteSpace(data.RequestKey)) return FromExternal(data.RequestKey);

            return FromGenerated(
                data.TriggerId,
                data.TriggerEventId,
                data.ActionIndex,
                data.Order,
                data.SourceActorId,
                data.TargetActorId);
        }

        public static BattlePresentationCueRequestKey FromExternal(string externalKey)
        {
            return string.IsNullOrWhiteSpace(externalKey) ? default : new BattlePresentationCueRequestKey(externalKey);
        }

        public static BattlePresentationCueRequestKey FromPredictionKey(int predictionKey)
        {
            return predictionKey == 0 ? default : new BattlePresentationCueRequestKey(predictionKey);
        }

        public static BattlePresentationCueRequestKey FromGenerated(
            int triggerKey,
            int triggerEventId,
            int actionIndex,
            int order,
            int sourceActorId,
            int targetActorId)
        {
            return new BattlePresentationCueRequestKey(triggerKey, triggerEventId, actionIndex, order, sourceActorId, targetActorId);
        }

        public bool Equals(BattlePresentationCueRequestKey other)
        {
            if (_hasValue != other._hasValue) return false;
            if (!_hasValue) return true;
            if (_hasPredictionKey != other._hasPredictionKey) return false;
            if (_hasPredictionKey) return _predictionKey == other._predictionKey;
            if (_hasExternalKey != other._hasExternalKey) return false;
            if (_hasExternalKey) return string.Equals(_externalKey, other._externalKey, StringComparison.Ordinal);

            if (_triggerKey > 0 || other._triggerKey > 0)
            {
                return _triggerKey == other._triggerKey && _order == other._order;
            }

            if (_triggerEventId > 0 || other._triggerEventId > 0)
            {
                return _triggerEventId == other._triggerEventId && _order == other._order;
            }

            return _actionIndex == other._actionIndex
                && _order == other._order
                && _sourceActorId == other._sourceActorId
                && _targetActorId == other._targetActorId;
        }

        public override bool Equals(object obj)
        {
            return obj is BattlePresentationCueRequestKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (!_hasValue) return 0;
            if (_hasPredictionKey) return _predictionKey;
            if (_hasExternalKey) return _externalKey != null ? StringComparer.Ordinal.GetHashCode(_externalKey) : 0;

            unchecked
            {
                if (_triggerKey > 0)
                {
                    return (_triggerKey * 397) ^ _order;
                }

                if (_triggerEventId > 0)
                {
                    return (_triggerEventId * 397) ^ _order;
                }

                var hash = _actionIndex;
                hash = (hash * 397) ^ _order;
                hash = (hash * 397) ^ _sourceActorId;
                hash = (hash * 397) ^ _targetActorId;
                return hash;
            }
        }
    }
}
