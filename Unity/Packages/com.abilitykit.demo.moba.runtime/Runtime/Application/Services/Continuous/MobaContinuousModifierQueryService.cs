using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Attributes.Core;
using AbilityKit.Core.Continuous;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaContinuousModifierQueryService
    {
        IReadOnlyList<MobaContinuousModifierEntry> GetActiveModifiers(int ownerActorId);
        IReadOnlyList<MobaContinuousModifierEntry> GetActiveModifiers(int ownerActorId, int targetKind);
        IReadOnlyList<MobaContinuousModifierEntry> GetActiveModifiers(int ownerActorId, int targetKind, int targetId);
        IReadOnlyList<MobaContinuousModifierExplainResult> ExplainActiveModifiers(int ownerActorId);
        IReadOnlyList<MobaContinuousModifierExplainResult> ExplainActiveModifiers(int ownerActorId, int targetKind);
        IReadOnlyList<MobaContinuousModifierExplainResult> ExplainActiveModifiers(int ownerActorId, int targetKind, int targetId);
        bool TryExplainModifier(MobaContinuousModifierEntry entry, out MobaContinuousModifierExplainResult result);
        float EvaluateNumeric(int ownerActorId, int targetKind, int targetId, float baseValue);
    }

    public readonly struct MobaContinuousModifierMagnitudeView
    {
        public MobaContinuousModifierMagnitudeView(MagnitudeSource source, float calculatedValue, bool hasCalculatedValue)
        {
            Type = source.Type;
            BaseValue = source.BaseValue;
            Coefficient = source.Coefficient;
            Duration = source.Duration;
            AttributeKind = source.AttributeKey.Category;
            AttributeSubCategory = source.AttributeKey.SubCategory;
            AttributeCustomId = source.AttributeKey.CustomId;
            DecayType = (int)source.DecayType;
            IsTimeVarying = source.IsTimeVarying;
            CalculatedValue = calculatedValue;
            HasCalculatedValue = hasCalculatedValue;
        }

        public MagnitudeSourceType Type { get; }
        public float BaseValue { get; }
        public float Coefficient { get; }
        public float Duration { get; }
        public byte AttributeKind { get; }
        public byte AttributeSubCategory { get; }
        public byte AttributeCustomId { get; }
        public int DecayType { get; }
        public bool IsTimeVarying { get; }
        public float CalculatedValue { get; }
        public bool HasCalculatedValue { get; }
    }

    public readonly struct MobaContinuousModifierExplainResult
    {
        public MobaContinuousModifierExplainResult(
            IContinuous continuous,
            int ownerActorId,
            int targetKind,
            int targetId,
            int op,
            float value,
            int evaluationPolicy,
            int priority,
            int stack,
            int modifierSourceId,
            MobaContinuousModifierMagnitudeView declaredMagnitude,
            MobaContinuousModifierMagnitudeView stackedMagnitude,
            MobaContinuousModifierMagnitudeView projectedMagnitude,
            float currentValue,
            bool hasCurrentValue,
            float capturedValue,
            bool hasCapturedValue,
            string captureMode,
            string reason)
        {
            Continuous = continuous;
            OwnerActorId = ownerActorId;
            TargetKind = targetKind;
            TargetId = targetId;
            Op = op;
            Value = value;
            EvaluationPolicy = evaluationPolicy;
            Priority = priority;
            Stack = stack;
            ModifierSourceId = modifierSourceId;
            DeclaredMagnitude = declaredMagnitude;
            StackedMagnitude = stackedMagnitude;
            ProjectedMagnitude = projectedMagnitude;
            CurrentValue = currentValue;
            HasCurrentValue = hasCurrentValue;
            CapturedValue = capturedValue;
            HasCapturedValue = hasCapturedValue;
            CaptureMode = captureMode;
            Reason = reason;
        }

        public IContinuous Continuous { get; }
        public int OwnerActorId { get; }
        public int TargetKind { get; }
        public int TargetId { get; }
        public int Op { get; }
        public float Value { get; }
        public int EvaluationPolicy { get; }
        public int Priority { get; }
        public int Stack { get; }
        public int ModifierSourceId { get; }
        public MobaContinuousModifierMagnitudeView DeclaredMagnitude { get; }
        public MobaContinuousModifierMagnitudeView StackedMagnitude { get; }
        public MobaContinuousModifierMagnitudeView ProjectedMagnitude { get; }
        public float CurrentValue { get; }
        public bool HasCurrentValue { get; }
        public float CapturedValue { get; }
        public bool HasCapturedValue { get; }
        public string CaptureMode { get; }
        public string Reason { get; }
        public bool IsSnapshot => EvaluationPolicy == MobaContinuousModifierEvaluationPolicy.OnApplySnapshot;
        public bool IsRealtime => !IsSnapshot;
    }

    public readonly struct MobaContinuousModifierCaptureRecord
    {
        public MobaContinuousModifierCaptureRecord(
            IContinuous continuous,
            int ownerActorId,
            int targetKind,
            int targetId,
            int op,
            float value,
            int evaluationPolicy,
            int priority,
            int stack,
            int modifierSourceId,
            MagnitudeSource declaredMagnitude,
            MagnitudeSource stackedMagnitude,
            MagnitudeSource projectedMagnitude,
            float capturedValue,
            bool hasCapturedValue,
            string captureMode,
            string reason)
        {
            Continuous = continuous;
            OwnerActorId = ownerActorId;
            TargetKind = targetKind;
            TargetId = targetId;
            Op = op;
            Value = value;
            EvaluationPolicy = evaluationPolicy;
            Priority = priority;
            Stack = stack;
            ModifierSourceId = modifierSourceId;
            DeclaredMagnitude = declaredMagnitude;
            StackedMagnitude = stackedMagnitude;
            ProjectedMagnitude = projectedMagnitude;
            CapturedValue = capturedValue;
            HasCapturedValue = hasCapturedValue;
            CaptureMode = captureMode;
            Reason = reason;
        }

        public IContinuous Continuous { get; }
        public int OwnerActorId { get; }
        public int TargetKind { get; }
        public int TargetId { get; }
        public int Op { get; }
        public float Value { get; }
        public int EvaluationPolicy { get; }
        public int Priority { get; }
        public int Stack { get; }
        public int ModifierSourceId { get; }
        public MagnitudeSource DeclaredMagnitude { get; }
        public MagnitudeSource StackedMagnitude { get; }
        public MagnitudeSource ProjectedMagnitude { get; }
        public float CapturedValue { get; }
        public bool HasCapturedValue { get; }
        public string CaptureMode { get; }
        public string Reason { get; }
    }

    public class MobaContinuousModifierCaptureStore
    {
        /// <summary>
        /// 当前作用域（战斗世界）的捕获存储实例。
        /// 由 <see cref="MobaContinuousModifierQueryService"/> 在初始化时设置。
        /// </summary>
        public static MobaContinuousModifierCaptureStore Instance { get; set; } = new MobaContinuousModifierCaptureStore();

        private readonly Dictionary<Key, MobaContinuousModifierCaptureRecord> _records = new Dictionary<Key, MobaContinuousModifierCaptureRecord>();

        public void Record(in MobaContinuousModifierCaptureRecord record)
        {
            if (record.Continuous == null || record.ModifierSourceId == 0) return;
            _records[Key.From(record.Continuous, record.ModifierSourceId, record.TargetKind, record.TargetId, record.Op, record.Priority)] = record;
        }

        public bool TryGet(IContinuous continuous, int sourceId, IMobaContinuousModifierSpec spec, out MobaContinuousModifierCaptureRecord record)
        {
            if (continuous != null && spec != null && sourceId != 0)
                return _records.TryGetValue(Key.From(continuous, sourceId, spec.TargetKind, spec.TargetId, spec.Op, spec.Priority), out record);

            record = default;
            return false;
        }

        public void Clear(int sourceId)
        {
            if (sourceId == 0 || _records.Count == 0) return;

            List<Key> remove = null;
            foreach (var pair in _records)
            {
                if (pair.Key.SourceId != sourceId) continue;
                remove ??= new List<Key>();
                remove.Add(pair.Key);
            }

            if (remove == null) return;
            for (int i = 0; i < remove.Count; i++)
                _records.Remove(remove[i]);
        }

        private readonly struct Key : IEquatable<Key>
        {
            private Key(IContinuous continuous, int sourceId, int targetKind, int targetId, int op, int priority)
            {
                Continuous = continuous;
                SourceId = sourceId;
                TargetKind = targetKind;
                TargetId = targetId;
                Op = op;
                Priority = priority;
            }

            public IContinuous Continuous { get; }
            public int SourceId { get; }
            public int TargetKind { get; }
            public int TargetId { get; }
            public int Op { get; }
            public int Priority { get; }

            public static Key From(IContinuous continuous, int sourceId, int targetKind, int targetId, int op, int priority)
            {
                return new Key(continuous, sourceId, targetKind, targetId, op, priority);
            }

            public bool Equals(Key other)
            {
                return ReferenceEquals(Continuous, other.Continuous) &&
                       SourceId == other.SourceId &&
                       TargetKind == other.TargetKind &&
                       TargetId == other.TargetId &&
                       Op == other.Op &&
                       Priority == other.Priority;
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = Continuous != null ? Continuous.GetHashCode() : 0;
                    hash = (hash * 397) ^ SourceId;
                    hash = (hash * 397) ^ TargetKind;
                    hash = (hash * 397) ^ TargetId;
                    hash = (hash * 397) ^ Op;
                    hash = (hash * 397) ^ Priority;
                    return hash;
                }
            }
        }
    }

    [WorldService(typeof(IMobaContinuousModifierQueryService))]
    [WorldService(typeof(MobaContinuousModifierQueryService))]
    public sealed class MobaContinuousModifierQueryService : IMobaContinuousModifierQueryService, IWorldInitializable, IService
    {
        private IContinuousManager _continuous;
        private MobaActorLookupService _actors;
        private readonly MobaContinuousModifierCaptureStore _captureStore = new MobaContinuousModifierCaptureStore();

        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _continuous);
            services?.TryResolve(out _actors);
            MobaContinuousModifierCaptureStore.Instance = _captureStore;
        }

        public void Dispose()
        {
            _continuous = null;
            _actors = null;
        }

        public IReadOnlyList<MobaContinuousModifierEntry> GetActiveModifiers(int ownerActorId)
        {
            return Collect(ownerActorId, targetKind: 0, targetId: 0, filterKind: false, filterTarget: false);
        }

        public IReadOnlyList<MobaContinuousModifierEntry> GetActiveModifiers(int ownerActorId, int targetKind)
        {
            return Collect(ownerActorId, targetKind, targetId: 0, filterKind: true, filterTarget: false);
        }

        public IReadOnlyList<MobaContinuousModifierEntry> GetActiveModifiers(int ownerActorId, int targetKind, int targetId)
        {
            return Collect(ownerActorId, targetKind, targetId, filterKind: true, filterTarget: true);
        }

        public IReadOnlyList<MobaContinuousModifierExplainResult> ExplainActiveModifiers(int ownerActorId)
        {
            return Explain(ownerActorId, targetKind: 0, targetId: 0, filterKind: false, filterTarget: false);
        }

        public IReadOnlyList<MobaContinuousModifierExplainResult> ExplainActiveModifiers(int ownerActorId, int targetKind)
        {
            return Explain(ownerActorId, targetKind, targetId: 0, filterKind: true, filterTarget: false);
        }

        public IReadOnlyList<MobaContinuousModifierExplainResult> ExplainActiveModifiers(int ownerActorId, int targetKind, int targetId)
        {
            return Explain(ownerActorId, targetKind, targetId, filterKind: true, filterTarget: true);
        }

        public bool TryExplainModifier(MobaContinuousModifierEntry entry, out MobaContinuousModifierExplainResult result)
        {
            var ownerActorId = entry.Projection?.OwnerActorId ?? 0;
            if (entry.Continuous == null || entry.Spec == null || ownerActorId <= 0)
            {
                result = default;
                return false;
            }

            result = BuildExplainResult(entry, ResolveAttributeContext(ownerActorId));
            return true;
        }

        public float EvaluateNumeric(int ownerActorId, int targetKind, int targetId, float baseValue)
        {
            return MobaContinuousModifierMath.EvaluateNumeric(baseValue, GetActiveModifiers(ownerActorId, targetKind, targetId), ResolveAttributeContext(ownerActorId));
        }

        private IReadOnlyList<MobaContinuousModifierExplainResult> Explain(int ownerActorId, int targetKind, int targetId, bool filterKind, bool filterTarget)
        {
            var entries = Collect(ownerActorId, targetKind, targetId, filterKind, filterTarget);
            if (entries == null || entries.Count == 0) return Array.Empty<MobaContinuousModifierExplainResult>();

            var context = ResolveAttributeContext(ownerActorId);
            var result = new List<MobaContinuousModifierExplainResult>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
                result.Add(BuildExplainResult(entries[i], context));

            return result;
        }

        private MobaContinuousModifierExplainResult BuildExplainResult(MobaContinuousModifierEntry entry, AttributeContext context)
        {
            var spec = entry.Spec;
            var projection = entry.Projection;
            var ownerActorId = projection?.OwnerActorId ?? 0;
            var sourceId = projection?.ModifierSourceId ?? 0;
            var declared = spec.Magnitude;
            var stacked = ApplyStack(declared, entry.Stack);
            var projected = stacked;
            var capturedValue = 0f;
            var hasCapturedValue = false;
            var captureMode = spec.EvaluationPolicy == MobaContinuousModifierEvaluationPolicy.OnApplySnapshot ? "OnApplySnapshot" : "Realtime";
            var reason = "Runtime declaration";

            if (_captureStore.TryGet(entry.Continuous, sourceId, spec, out var record))
            {
                projected = record.ProjectedMagnitude;
                capturedValue = record.CapturedValue;
                hasCapturedValue = record.HasCapturedValue;
                captureMode = record.CaptureMode;
                reason = record.Reason;
            }
            else if (spec.EvaluationPolicy == MobaContinuousModifierEvaluationPolicy.OnApplySnapshot)
            {
                capturedValue = stacked.Calculate(context?.Level ?? 1f, context);
                hasCapturedValue = true;
                projected = MagnitudeSource.Fixed(capturedValue);
                reason = "Snapshot capture was not recorded; value is reconstructed from current context";
            }

            var currentValue = stacked.Calculate(context?.Level ?? 1f, context);
            var projectedValue = projected.Calculate(context?.Level ?? 1f, context);

            return new MobaContinuousModifierExplainResult(
                entry.Continuous,
                ownerActorId,
                spec.TargetKind,
                spec.TargetId,
                spec.Op,
                spec.Value,
                spec.EvaluationPolicy,
                spec.Priority,
                entry.Stack,
                sourceId,
                new MobaContinuousModifierMagnitudeView(declared, declared.Calculate(context?.Level ?? 1f, context), true),
                new MobaContinuousModifierMagnitudeView(stacked, currentValue, true),
                new MobaContinuousModifierMagnitudeView(projected, projectedValue, true),
                currentValue,
                true,
                capturedValue,
                hasCapturedValue,
                captureMode,
                reason);
        }

        private AttributeContext ResolveAttributeContext(int ownerActorId)
        {
            if (ownerActorId <= 0 || _actors == null) return null;
            if (!_actors.TryGetActorEntity(ownerActorId, out var entity) || entity == null) return null;
            return entity.hasAttributeGroup ? entity.attributeGroup.Ctx : null;
        }

        private IReadOnlyList<MobaContinuousModifierEntry> Collect(int ownerActorId, int targetKind, int targetId, bool filterKind, bool filterTarget)
        {
            if (ownerActorId <= 0 || _continuous == null) return Array.Empty<MobaContinuousModifierEntry>();

            var active = _continuous.GetOwnerActiveContinuous(ownerActorId);
            if (active == null || active.Count == 0) return Array.Empty<MobaContinuousModifierEntry>();

            List<MobaContinuousModifierEntry> result = null;
            for (int i = 0; i < active.Count; i++)
            {
                var continuous = active[i];
                if (continuous == null || !continuous.IsActive) continue;
                if (!(continuous.Config is IMobaContinuousModifierConfig modifierConfig)) continue;
                if (!(continuous.Config is IMobaContinuousProjectionConfig projection)) continue;
                if (projection.OwnerActorId != ownerActorId) continue;

                var modifiers = modifierConfig.Modifiers;
                if (modifiers == null || modifiers.Count == 0) continue;

                var stack = GetStack(continuous.Config);
                for (int m = 0; m < modifiers.Count; m++)
                {
                    var spec = modifiers[m];
                    if (spec == null) continue;
                    if (filterKind && spec.TargetKind != targetKind) continue;
                    if (filterTarget && spec.TargetId != targetId) continue;

                    result ??= new List<MobaContinuousModifierEntry>();
                    result.Add(new MobaContinuousModifierEntry(continuous, projection, spec, stack));
                }
            }

            return result ?? (IReadOnlyList<MobaContinuousModifierEntry>)Array.Empty<MobaContinuousModifierEntry>();
        }

        private static MagnitudeSource ApplyStack(MagnitudeSource magnitude, int stack)
        {
            if (stack <= 1) return magnitude;
            return magnitude.WithBaseValue(magnitude.BaseValue * stack);
        }

        private static int GetStack(IContinuousConfig config)
        {
            return config is IStackConfig stackConfig && stackConfig.Stack > 1 ? stackConfig.Stack : 1;
        }
    }
}
