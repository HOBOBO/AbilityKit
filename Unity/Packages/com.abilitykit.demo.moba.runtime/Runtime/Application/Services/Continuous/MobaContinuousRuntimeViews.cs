using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaContinuousRuntimeDebugSource
    {
        bool TryGetRuntimeDebugInfo(out MobaContinuousRuntimeDebugInfo info);
    }

    public readonly struct MobaContinuousLifecycleReason
    {
        public MobaContinuousLifecycleReason(string lastEvent, string reason, ContinuousEndReason endReason)
        {
            LastEvent = lastEvent;
            Reason = reason;
            EndReason = endReason;
        }

        public string LastEvent { get; }
        public string Reason { get; }
        public ContinuousEndReason EndReason { get; }
        public bool HasReason => !string.IsNullOrEmpty(LastEvent) || !string.IsNullOrEmpty(Reason);
        public static MobaContinuousLifecycleReason None => default;
    }

    public readonly struct MobaContinuousRuntimeDebugInfo
    {
        public MobaContinuousRuntimeDebugInfo(
            string kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long parentContextId,
            long rootContextId,
            long ownerContextId,
            MobaSkillCastRuntimeHandle skillRuntimeHandle,
            MobaContextSourceView contextSource)
        {
            Kind = kind;
            ConfigId = configId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            SourceContextId = sourceContextId;
            ParentContextId = parentContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SkillRuntimeHandle = skillRuntimeHandle;
            ContextSource = contextSource;
        }

        public string Kind { get; }
        public int ConfigId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long SourceContextId { get; }
        public long ParentContextId { get; }
        public long RootContextId { get; }
        public long OwnerContextId { get; }
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }
        public MobaContextSourceView ContextSource { get; }
    }

    public sealed class MobaContinuousRuntimeView
    {
        public MobaContinuousRuntimeView(
            IContinuous continuous,
            string id,
            string kind,
            int configId,
            long ownerId,
            int ownerActorId,
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long parentContextId,
            long rootContextId,
            long ownerContextId,
            MobaSkillCastRuntimeHandle skillRuntimeHandle,
            ContinuousState state,
            bool isActive,
            bool isPaused,
            bool isTerminated,
            float elapsedSeconds,
            float durationSeconds,
            float remainingSeconds,
            int stack,
            int maxStack,
            float intervalSeconds,
            float intervalRemainingSeconds,
            IReadOnlyList<int> intervalEffectIds,
            IReadOnlyList<MobaContinuousRuntimeTagView> tags,
            IReadOnlyList<MobaContinuousRuntimeModifierView> modifiers,
            IReadOnlyList<MobaContinuousModifierExplainResult> modifierExplanations,
            MobaContinuousTagRuleResult lastTagRuleResult,
            MobaContinuousTagRuleResult currentTagRuleResult,
            MobaContextSourceView contextSource)
        {
            Continuous = continuous;
            Id = id;
            Kind = kind;
            ConfigId = configId;
            OwnerId = ownerId;
            OwnerActorId = ownerActorId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            SourceContextId = sourceContextId;
            ParentContextId = parentContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SkillRuntimeHandle = skillRuntimeHandle;
            State = state;
            IsActive = isActive;
            IsPaused = isPaused;
            IsTerminated = isTerminated;
            ElapsedSeconds = elapsedSeconds;
            DurationSeconds = durationSeconds;
            RemainingSeconds = remainingSeconds;
            Stack = stack;
            MaxStack = maxStack;
            IntervalSeconds = intervalSeconds;
            IntervalRemainingSeconds = intervalRemainingSeconds;
            IntervalEffectIds = intervalEffectIds ?? Array.Empty<int>();
            Tags = tags ?? Array.Empty<MobaContinuousRuntimeTagView>();
            Modifiers = modifiers ?? Array.Empty<MobaContinuousRuntimeModifierView>();
            ModifierExplanations = modifierExplanations ?? Array.Empty<MobaContinuousModifierExplainResult>();
            LastTagRuleResult = lastTagRuleResult;
            CurrentTagRuleResult = currentTagRuleResult;
            ContextSource = contextSource;
        }

        public IContinuous Continuous { get; }
        public string Id { get; }
        public string Kind { get; }
        public int ConfigId { get; }
        public long OwnerId { get; }
        public int OwnerActorId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long SourceContextId { get; }
        public long ParentContextId { get; }
        public long RootContextId { get; }
        public long OwnerContextId { get; }
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }
        public ContinuousState State { get; }
        public bool IsActive { get; }
        public bool IsPaused { get; }
        public bool IsTerminated { get; }
        public float ElapsedSeconds { get; }
        public float DurationSeconds { get; }
        public float RemainingSeconds { get; }
        public int Stack { get; }
        public int MaxStack { get; }
        public float IntervalSeconds { get; }
        public float IntervalRemainingSeconds { get; }
        public IReadOnlyList<int> IntervalEffectIds { get; }
        public IReadOnlyList<MobaContinuousRuntimeTagView> Tags { get; }
        public IReadOnlyList<MobaContinuousRuntimeModifierView> Modifiers { get; }
        public IReadOnlyList<MobaContinuousModifierExplainResult> ModifierExplanations { get; }
        public MobaContinuousTagRuleResult LastTagRuleResult { get; }
        public MobaContinuousTagRuleResult CurrentTagRuleResult { get; }
        public MobaContextSourceView ContextSource { get; }
        public MobaContextSourceBoundary ContextSourceBoundary => ContextSource.Boundary;
        public bool HasLiveRuntimeSource => ContextSource.Boundary == MobaContextSourceBoundary.LiveRuntime;
    }

    public readonly struct MobaContinuousRuntimeTagView
    {
        public MobaContinuousRuntimeTagView(int value, string name, GameplayTagSource source)
        {
            Value = value;
            Name = name;
            Source = source;
        }

        public int Value { get; }
        public string Name { get; }
        public GameplayTagSource Source { get; }
    }

    public readonly struct MobaContinuousRuntimeModifierView
    {
        public MobaContinuousRuntimeModifierView(
            int targetKind,
            int targetId,
            int op,
            float value,
            MagnitudeSourceType magnitudeType,
            float magnitudeBaseValue,
            float magnitudeCoefficient,
            int evaluationPolicy,
            int priority,
            int stack,
            int modifierSourceId)
        {
            TargetKind = targetKind;
            TargetId = targetId;
            Op = op;
            Value = value;
            MagnitudeType = magnitudeType;
            MagnitudeBaseValue = magnitudeBaseValue;
            MagnitudeCoefficient = magnitudeCoefficient;
            EvaluationPolicy = evaluationPolicy;
            Priority = priority;
            Stack = stack;
            ModifierSourceId = modifierSourceId;
        }

        public int TargetKind { get; }
        public int TargetId { get; }
        public int Op { get; }
        public float Value { get; }
        public MagnitudeSourceType MagnitudeType { get; }
        public float MagnitudeBaseValue { get; }
        public float MagnitudeCoefficient { get; }
        public int EvaluationPolicy { get; }
        public int Priority { get; }
        public int Stack { get; }
        public int ModifierSourceId { get; }
    }
}
