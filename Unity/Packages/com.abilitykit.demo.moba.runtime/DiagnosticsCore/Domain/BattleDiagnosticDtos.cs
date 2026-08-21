using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public enum BattleDiagnosticActorKind
    {
        Unknown = 0,
        Hero = 1,
        Minion = 2,
        Monster = 3,
        Building = 4,
        Summon = 5,
        Projectile = 6,
        Area = 7
    }

    public enum BattleDiagnosticEventKind
    {
        Unknown = 0,
        SkillRuntimeStarted = 1,
        SkillRuntimeEnded = 2,
        TraceNodeStarted = 3,
        TraceNodeEnded = 4,
        Damage = 5,
        Heal = 6,
        BuffAdded = 7,
        BuffRemoved = 8,
        ProjectileSpawned = 9,
        ProjectileEnded = 10,
        AreaSpawned = 11,
        AreaEnded = 12,
        Warning = 13,
        Exception = 14,
        Sync = 15,
        SummonSpawned = 16,
        SummonEnded = 17,
        EffectStarted = 18,
        EffectEnded = 19,
        ProjectileHit = 20,
        TriggerAnalysis = 21,
        SkillFailure = 22
    }

    public enum BattleDiagnosticDefinitionKind
    {
        Unknown = 0,
        Skill = 1,
        Trigger = 2,
        Effect = 3,
        Buff = 4,
        Projectile = 5,
        Area = 6,
        Summon = 7,
        Actor = 8
    }

    public static class BattleDiagnosticDefinitionKinds
    {
        public static BattleDiagnosticDefinitionKind FromEventKind(BattleDiagnosticEventKind kind)
        {
            switch (kind)
            {
                case BattleDiagnosticEventKind.SkillRuntimeStarted:
                case BattleDiagnosticEventKind.SkillRuntimeEnded:
                case BattleDiagnosticEventKind.SkillFailure:
                    return BattleDiagnosticDefinitionKind.Skill;
                case BattleDiagnosticEventKind.TriggerAnalysis:
                    return BattleDiagnosticDefinitionKind.Trigger;
                case BattleDiagnosticEventKind.EffectStarted:
                case BattleDiagnosticEventKind.EffectEnded:
                    return BattleDiagnosticDefinitionKind.Effect;
                case BattleDiagnosticEventKind.BuffAdded:
                case BattleDiagnosticEventKind.BuffRemoved:
                    return BattleDiagnosticDefinitionKind.Buff;
                case BattleDiagnosticEventKind.ProjectileSpawned:
                case BattleDiagnosticEventKind.ProjectileEnded:
                case BattleDiagnosticEventKind.ProjectileHit:
                    return BattleDiagnosticDefinitionKind.Projectile;
                case BattleDiagnosticEventKind.AreaSpawned:
                case BattleDiagnosticEventKind.AreaEnded:
                    return BattleDiagnosticDefinitionKind.Area;
                case BattleDiagnosticEventKind.SummonSpawned:
                case BattleDiagnosticEventKind.SummonEnded:
                    return BattleDiagnosticDefinitionKind.Summon;
                default:
                    return BattleDiagnosticDefinitionKind.Unknown;
            }
        }
    }

    public enum BattleDiagnosticEventOutcome
    {
        None = 0,
        Succeeded = 1,
        Failed = 2,
        Cancelled = 3,
        Interrupted = 4
    }

    public enum BattleDiagnosticTraceNodeState
    {
        Active = 0,
        Ended = 1,
        Failed = 2,
        ForceEnded = 3,
        Truncated = 4
    }

    public readonly struct BattleDiagnosticActorAttribute : IEquatable<BattleDiagnosticActorAttribute>
    {
        public BattleDiagnosticActorAttribute(
            BattleDiagnosticSessionScope scope,
            int frame,
            long actorId,
            int attributeId,
            float baseValue,
            float finalValue,
            int modifierCount,
            string name = "")
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (attributeId <= 0) throw new ArgumentOutOfRangeException(nameof(attributeId));
            if (modifierCount < 0) throw new ArgumentOutOfRangeException(nameof(modifierCount));

            Scope = scope;
            Frame = frame;
            ActorId = actorId;
            AttributeId = attributeId;
            BaseValue = baseValue;
            FinalValue = finalValue;
            ModifierCount = modifierCount;
            Name = name ?? string.Empty;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public int Frame { get; }
        public long ActorId { get; }
        public int AttributeId { get; }
        public float BaseValue { get; }
        public float FinalValue { get; }
        public int ModifierCount { get; }
        public string Name { get; }

        public bool Equals(BattleDiagnosticActorAttribute other)
        {
            return Scope.Equals(other.Scope) && Frame == other.Frame && ActorId == other.ActorId &&
                   AttributeId == other.AttributeId && BaseValue.Equals(other.BaseValue) &&
                   FinalValue.Equals(other.FinalValue) && ModifierCount == other.ModifierCount &&
                   string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticActorAttribute other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ Frame;
                hashCode = (hashCode * 397) ^ ActorId.GetHashCode();
                hashCode = (hashCode * 397) ^ AttributeId;
                hashCode = (hashCode * 397) ^ BaseValue.GetHashCode();
                hashCode = (hashCode * 397) ^ FinalValue.GetHashCode();
                hashCode = (hashCode * 397) ^ ModifierCount;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Name ?? string.Empty);
                return hashCode;
            }
        }
    }

    public readonly struct BattleDiagnosticActorAttributeModifier : IEquatable<BattleDiagnosticActorAttributeModifier>
    {
        public BattleDiagnosticActorAttributeModifier(
            BattleDiagnosticSessionScope scope,
            int frame,
            long actorId,
            int attributeId,
            int operation,
            float magnitude,
            int priority,
            int sourceId,
            int magnitudeType,
            float declaredValue = 0f,
            float stackedValue = 0f,
            float projectedValue = 0f,
            float currentValue = 0f,
            bool hasCurrentValue = false,
            float capturedValue = 0f,
            bool hasCapturedValue = false,
            int evaluationPolicy = 0,
            int stackCount = 1,
            string captureMode = "",
            string explanation = "")
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (attributeId <= 0) throw new ArgumentOutOfRangeException(nameof(attributeId));

            Scope = scope;
            Frame = frame;
            ActorId = actorId;
            AttributeId = attributeId;
            Operation = operation;
            Magnitude = magnitude;
            Priority = priority;
            SourceId = sourceId;
            MagnitudeType = magnitudeType;
            DeclaredValue = declaredValue;
            StackedValue = stackedValue;
            ProjectedValue = projectedValue;
            CurrentValue = currentValue;
            HasCurrentValue = hasCurrentValue;
            CapturedValue = capturedValue;
            HasCapturedValue = hasCapturedValue;
            EvaluationPolicy = evaluationPolicy;
            StackCount = stackCount;
            CaptureMode = captureMode ?? string.Empty;
            Explanation = explanation ?? string.Empty;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public int Frame { get; }
        public long ActorId { get; }
        public int AttributeId { get; }
        public int Operation { get; }
        public float Magnitude { get; }
        public int Priority { get; }
        public int SourceId { get; }
        public int MagnitudeType { get; }
        public float DeclaredValue { get; }
        public float StackedValue { get; }
        public float ProjectedValue { get; }
        public float CurrentValue { get; }
        public bool HasCurrentValue { get; }
        public float CapturedValue { get; }
        public bool HasCapturedValue { get; }
        public int EvaluationPolicy { get; }
        public int StackCount { get; }
        public string CaptureMode { get; }
        public string Explanation { get; }
        public bool HasExplanation => HasCurrentValue || HasCapturedValue || !string.IsNullOrEmpty(Explanation);

        public bool Equals(BattleDiagnosticActorAttributeModifier other)
        {
            return Scope.Equals(other.Scope) && Frame == other.Frame && ActorId == other.ActorId &&
                   AttributeId == other.AttributeId && Operation == other.Operation &&
                   Magnitude.Equals(other.Magnitude) && Priority == other.Priority &&
                   SourceId == other.SourceId && MagnitudeType == other.MagnitudeType &&
                   DeclaredValue.Equals(other.DeclaredValue) && StackedValue.Equals(other.StackedValue) &&
                   ProjectedValue.Equals(other.ProjectedValue) && CurrentValue.Equals(other.CurrentValue) &&
                   HasCurrentValue == other.HasCurrentValue && CapturedValue.Equals(other.CapturedValue) &&
                   HasCapturedValue == other.HasCapturedValue && EvaluationPolicy == other.EvaluationPolicy &&
                   StackCount == other.StackCount &&
                   string.Equals(CaptureMode, other.CaptureMode, StringComparison.Ordinal) &&
                   string.Equals(Explanation, other.Explanation, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticActorAttributeModifier other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ Frame;
                hashCode = (hashCode * 397) ^ ActorId.GetHashCode();
                hashCode = (hashCode * 397) ^ AttributeId;
                hashCode = (hashCode * 397) ^ Operation;
                hashCode = (hashCode * 397) ^ Magnitude.GetHashCode();
                hashCode = (hashCode * 397) ^ Priority;
                hashCode = (hashCode * 397) ^ SourceId;
                hashCode = (hashCode * 397) ^ MagnitudeType;
                hashCode = (hashCode * 397) ^ DeclaredValue.GetHashCode();
                hashCode = (hashCode * 397) ^ StackedValue.GetHashCode();
                hashCode = (hashCode * 397) ^ ProjectedValue.GetHashCode();
                hashCode = (hashCode * 397) ^ CurrentValue.GetHashCode();
                hashCode = (hashCode * 397) ^ HasCurrentValue.GetHashCode();
                hashCode = (hashCode * 397) ^ CapturedValue.GetHashCode();
                hashCode = (hashCode * 397) ^ HasCapturedValue.GetHashCode();
                hashCode = (hashCode * 397) ^ EvaluationPolicy;
                hashCode = (hashCode * 397) ^ StackCount;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(CaptureMode ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Explanation ?? string.Empty);
                return hashCode;
            }
        }
    }

    public readonly struct BattleDiagnosticActorBuff : IEquatable<BattleDiagnosticActorBuff>
    {
        public BattleDiagnosticActorBuff(
            BattleDiagnosticSessionScope scope,
            int frame,
            long actorId,
            int buffId,
            long sourceActorId,
            int stackCount,
            float remainingSeconds,
            float intervalRemainingSeconds,
            long sourceContextId,
            long runtimeContextId,
            long runtimeContextVersion,
            BattleDiagnosticRuntimeHandle skillRuntime,
            long rootContextId,
            int modifierBindingCount,
            int maxStacks = 0,
            string name = "",
            int modifierSourceId = 0)
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (buffId <= 0) throw new ArgumentOutOfRangeException(nameof(buffId));
            if (stackCount < 0) throw new ArgumentOutOfRangeException(nameof(stackCount));
            if (remainingSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(remainingSeconds));
            if (intervalRemainingSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(intervalRemainingSeconds));
            if (runtimeContextVersion < 0) throw new ArgumentOutOfRangeException(nameof(runtimeContextVersion));
            if (modifierBindingCount < 0) throw new ArgumentOutOfRangeException(nameof(modifierBindingCount));
            if (maxStacks < 0) throw new ArgumentOutOfRangeException(nameof(maxStacks));

            Scope = scope;
            Frame = frame;
            ActorId = actorId;
            BuffId = buffId;
            SourceActorId = sourceActorId;
            StackCount = stackCount;
            RemainingSeconds = remainingSeconds;
            IntervalRemainingSeconds = intervalRemainingSeconds;
            SourceContextId = sourceContextId;
            RuntimeContextId = runtimeContextId;
            RuntimeContextVersion = runtimeContextVersion;
            SkillRuntime = skillRuntime;
            RootContextId = rootContextId;
            ModifierBindingCount = modifierBindingCount;
            MaxStacks = maxStacks;
            Name = name ?? string.Empty;
            ModifierSourceId = modifierSourceId;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public int Frame { get; }
        public long ActorId { get; }
        public int BuffId { get; }
        public long SourceActorId { get; }
        public int StackCount { get; }
        public float RemainingSeconds { get; }
        public float IntervalRemainingSeconds { get; }
        public long SourceContextId { get; }
        public long RuntimeContextId { get; }
        public long RuntimeContextVersion { get; }
        public BattleDiagnosticRuntimeHandle SkillRuntime { get; }
        public long RootContextId { get; }
        public int ModifierBindingCount { get; }
        public int MaxStacks { get; }
        public string Name { get; }
        public int ModifierSourceId { get; }

        public bool Equals(BattleDiagnosticActorBuff other)
        {
            return Scope.Equals(other.Scope) && Frame == other.Frame && ActorId == other.ActorId &&
                   BuffId == other.BuffId && SourceActorId == other.SourceActorId &&
                   StackCount == other.StackCount && RemainingSeconds.Equals(other.RemainingSeconds) &&
                   IntervalRemainingSeconds.Equals(other.IntervalRemainingSeconds) &&
                   SourceContextId == other.SourceContextId && RuntimeContextId == other.RuntimeContextId &&
                   RuntimeContextVersion == other.RuntimeContextVersion && SkillRuntime.Equals(other.SkillRuntime) &&
                   RootContextId == other.RootContextId && ModifierBindingCount == other.ModifierBindingCount &&
                   MaxStacks == other.MaxStacks && string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                   ModifierSourceId == other.ModifierSourceId;
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticActorBuff other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ Frame;
                hashCode = (hashCode * 397) ^ ActorId.GetHashCode();
                hashCode = (hashCode * 397) ^ BuffId;
                hashCode = (hashCode * 397) ^ SourceActorId.GetHashCode();
                hashCode = (hashCode * 397) ^ StackCount;
                hashCode = (hashCode * 397) ^ RemainingSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ IntervalRemainingSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ SourceContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ RuntimeContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ RuntimeContextVersion.GetHashCode();
                hashCode = (hashCode * 397) ^ SkillRuntime.GetHashCode();
                hashCode = (hashCode * 397) ^ RootContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ ModifierBindingCount;
                hashCode = (hashCode * 397) ^ MaxStacks;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Name ?? string.Empty);
                hashCode = (hashCode * 397) ^ ModifierSourceId;
                return hashCode;
            }
        }
    }

    public enum BattleDiagnosticEffectDurationPolicy
    {
        Instant = 0,
        Duration = 1,
        Infinite = 2
    }

    public readonly struct BattleDiagnosticActorEffect : IEquatable<BattleDiagnosticActorEffect>
    {
        public BattleDiagnosticActorEffect(
            BattleDiagnosticSessionScope scope,
            int frame,
            long actorId,
            int instanceId,
            BattleDiagnosticEffectDurationPolicy durationPolicy,
            int stackCount,
            float elapsedSeconds,
            float remainingSeconds,
            bool hasRemainingTime,
            float nextTickInSeconds,
            bool hasPeriodicTick,
            float durationSeconds,
            float periodSeconds,
            int componentCount,
            bool executePeriodicOnApply)
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (instanceId <= 0) throw new ArgumentOutOfRangeException(nameof(instanceId));
            if (!Enum.IsDefined(typeof(BattleDiagnosticEffectDurationPolicy), durationPolicy))
                throw new ArgumentOutOfRangeException(nameof(durationPolicy));
            if (stackCount < 0) throw new ArgumentOutOfRangeException(nameof(stackCount));
            ValidateNonNegativeFinite(elapsedSeconds, nameof(elapsedSeconds));
            ValidateNonNegativeFinite(remainingSeconds, nameof(remainingSeconds));
            ValidateNonNegativeFinite(nextTickInSeconds, nameof(nextTickInSeconds));
            ValidateNonNegativeFinite(durationSeconds, nameof(durationSeconds));
            ValidateNonNegativeFinite(periodSeconds, nameof(periodSeconds));
            if (componentCount < 0) throw new ArgumentOutOfRangeException(nameof(componentCount));

            Scope = scope;
            Frame = frame;
            ActorId = actorId;
            InstanceId = instanceId;
            DurationPolicy = durationPolicy;
            StackCount = stackCount;
            ElapsedSeconds = elapsedSeconds;
            RemainingSeconds = remainingSeconds;
            HasRemainingTime = hasRemainingTime;
            NextTickInSeconds = nextTickInSeconds;
            HasPeriodicTick = hasPeriodicTick;
            DurationSeconds = durationSeconds;
            PeriodSeconds = periodSeconds;
            ComponentCount = componentCount;
            ExecutePeriodicOnApply = executePeriodicOnApply;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public int Frame { get; }
        public long ActorId { get; }
        public int InstanceId { get; }
        public BattleDiagnosticEffectDurationPolicy DurationPolicy { get; }
        public int StackCount { get; }
        public float ElapsedSeconds { get; }
        public float RemainingSeconds { get; }
        public bool HasRemainingTime { get; }
        public float NextTickInSeconds { get; }
        public bool HasPeriodicTick { get; }
        public float DurationSeconds { get; }
        public float PeriodSeconds { get; }
        public int ComponentCount { get; }
        public bool ExecutePeriodicOnApply { get; }

        private static void ValidateNonNegativeFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw new ArgumentOutOfRangeException(parameterName);
        }

        public bool Equals(BattleDiagnosticActorEffect other)
        {
            return Scope.Equals(other.Scope) && Frame == other.Frame && ActorId == other.ActorId &&
                   InstanceId == other.InstanceId && DurationPolicy == other.DurationPolicy &&
                   StackCount == other.StackCount && ElapsedSeconds.Equals(other.ElapsedSeconds) &&
                   RemainingSeconds.Equals(other.RemainingSeconds) && HasRemainingTime == other.HasRemainingTime &&
                   NextTickInSeconds.Equals(other.NextTickInSeconds) && HasPeriodicTick == other.HasPeriodicTick &&
                   DurationSeconds.Equals(other.DurationSeconds) && PeriodSeconds.Equals(other.PeriodSeconds) &&
                   ComponentCount == other.ComponentCount && ExecutePeriodicOnApply == other.ExecutePeriodicOnApply;
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticActorEffect other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ Frame;
                hashCode = (hashCode * 397) ^ ActorId.GetHashCode();
                hashCode = (hashCode * 397) ^ InstanceId;
                hashCode = (hashCode * 397) ^ (int)DurationPolicy;
                hashCode = (hashCode * 397) ^ StackCount;
                hashCode = (hashCode * 397) ^ ElapsedSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ RemainingSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ HasRemainingTime.GetHashCode();
                hashCode = (hashCode * 397) ^ NextTickInSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ HasPeriodicTick.GetHashCode();
                hashCode = (hashCode * 397) ^ DurationSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ PeriodSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ ComponentCount;
                hashCode = (hashCode * 397) ^ ExecutePeriodicOnApply.GetHashCode();
                return hashCode;
            }
        }
    }

    public readonly struct BattleDiagnosticActorTag : IEquatable<BattleDiagnosticActorTag>
    {
        public BattleDiagnosticActorTag(
            BattleDiagnosticSessionScope scope,
            int frame,
            long actorId,
            int tagId,
            string name = "")
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (tagId <= 0) throw new ArgumentOutOfRangeException(nameof(tagId));

            Scope = scope;
            Frame = frame;
            ActorId = actorId;
            TagId = tagId;
            Name = name ?? string.Empty;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public int Frame { get; }
        public long ActorId { get; }
        public int TagId { get; }
        public string Name { get; }

        public bool Equals(BattleDiagnosticActorTag other)
        {
            return Scope.Equals(other.Scope) && Frame == other.Frame && ActorId == other.ActorId &&
                   TagId == other.TagId && string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticActorTag other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ Frame;
                hashCode = (hashCode * 397) ^ ActorId.GetHashCode();
                hashCode = (hashCode * 397) ^ TagId;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Name ?? string.Empty);
                return hashCode;
            }
        }
    }

    public readonly struct BattleDiagnosticWorldSummary : IEquatable<BattleDiagnosticWorldSummary>
    {
        public BattleDiagnosticWorldSummary(
            BattleDiagnosticSessionScope scope,
            int frame,
            long monotonicTimestamp,
            int actorCount,
            int activeSkillRuntimeCount,
            int activeTraceRootCount,
            string stateHash = "")
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (monotonicTimestamp < 0) throw new ArgumentOutOfRangeException(nameof(monotonicTimestamp));
            if (actorCount < 0) throw new ArgumentOutOfRangeException(nameof(actorCount));
            if (activeSkillRuntimeCount < 0) throw new ArgumentOutOfRangeException(nameof(activeSkillRuntimeCount));
            if (activeTraceRootCount < 0) throw new ArgumentOutOfRangeException(nameof(activeTraceRootCount));

            Scope = scope;
            Frame = frame;
            MonotonicTimestamp = monotonicTimestamp;
            ActorCount = actorCount;
            ActiveSkillRuntimeCount = activeSkillRuntimeCount;
            ActiveTraceRootCount = activeTraceRootCount;
            StateHash = stateHash ?? string.Empty;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public int Frame { get; }
        public long MonotonicTimestamp { get; }
        public int ActorCount { get; }
        public int ActiveSkillRuntimeCount { get; }
        public int ActiveTraceRootCount { get; }
        public string StateHash { get; }

        public bool Equals(BattleDiagnosticWorldSummary other)
        {
            return Scope.Equals(other.Scope) &&
                   Frame == other.Frame &&
                   MonotonicTimestamp == other.MonotonicTimestamp &&
                   ActorCount == other.ActorCount &&
                   ActiveSkillRuntimeCount == other.ActiveSkillRuntimeCount &&
                   ActiveTraceRootCount == other.ActiveTraceRootCount &&
                   string.Equals(StateHash, other.StateHash, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticWorldSummary other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ Frame;
                hashCode = (hashCode * 397) ^ MonotonicTimestamp.GetHashCode();
                hashCode = (hashCode * 397) ^ ActorCount;
                hashCode = (hashCode * 397) ^ ActiveSkillRuntimeCount;
                hashCode = (hashCode * 397) ^ ActiveTraceRootCount;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(StateHash ?? string.Empty);
                return hashCode;
            }
        }
    }

    public readonly struct BattleDiagnosticActorSummary : IEquatable<BattleDiagnosticActorSummary>
    {
        public BattleDiagnosticActorSummary(
            BattleDiagnosticSessionScope scope,
            int frame,
            long actorId,
            BattleDiagnosticActorKind kind,
            int configId,
            int teamId,
            float positionX,
            float positionY,
            float positionZ,
            float health,
            float maximumHealth,
            bool isAlive,
            string displayName = "")
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (maximumHealth < 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));

            Scope = scope;
            Frame = frame;
            ActorId = actorId;
            Kind = kind;
            ConfigId = configId;
            TeamId = teamId;
            PositionX = positionX;
            PositionY = positionY;
            PositionZ = positionZ;
            Health = health;
            MaximumHealth = maximumHealth;
            IsAlive = isAlive;
            DisplayName = displayName ?? string.Empty;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public int Frame { get; }
        public long ActorId { get; }
        public BattleDiagnosticActorKind Kind { get; }
        public int ConfigId { get; }
        public int TeamId { get; }
        public float PositionX { get; }
        public float PositionY { get; }
        public float PositionZ { get; }
        public float Health { get; }
        public float MaximumHealth { get; }
        public bool IsAlive { get; }
        public string DisplayName { get; }

        public bool Equals(BattleDiagnosticActorSummary other)
        {
            return Scope.Equals(other.Scope) && Frame == other.Frame && ActorId == other.ActorId &&
                   Kind == other.Kind && ConfigId == other.ConfigId && TeamId == other.TeamId &&
                   PositionX.Equals(other.PositionX) && PositionY.Equals(other.PositionY) &&
                   PositionZ.Equals(other.PositionZ) && Health.Equals(other.Health) &&
                   MaximumHealth.Equals(other.MaximumHealth) && IsAlive == other.IsAlive &&
                   string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticActorSummary other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ Frame;
                hashCode = (hashCode * 397) ^ ActorId.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Kind;
                hashCode = (hashCode * 397) ^ ConfigId;
                hashCode = (hashCode * 397) ^ TeamId;
                hashCode = (hashCode * 397) ^ PositionX.GetHashCode();
                hashCode = (hashCode * 397) ^ PositionY.GetHashCode();
                hashCode = (hashCode * 397) ^ PositionZ.GetHashCode();
                hashCode = (hashCode * 397) ^ Health.GetHashCode();
                hashCode = (hashCode * 397) ^ MaximumHealth.GetHashCode();
                hashCode = (hashCode * 397) ^ IsAlive.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(DisplayName ?? string.Empty);
                return hashCode;
            }
        }
    }

    public readonly struct BattleDiagnosticEvent : IEquatable<BattleDiagnosticEvent>
    {
        public BattleDiagnosticEvent(
            BattleDiagnosticSessionScope scope,
            int frame,
            long sequence,
            long monotonicTimestamp,
            BattleDiagnosticEventKind kind,
            BattleDiagnosticEventChannel channel,
            BattleDiagnosticEventOutcome outcome,
            long sourceActorId = 0,
            long targetActorId = 0,
            int configId = 0,
            long rootContextId = 0,
            long contextId = 0,
            BattleDiagnosticRuntimeHandle skillRuntime = default,
            long attackId = 0,
            int payloadVersion = 1,
            string summary = "",
            BattleDiagnosticEventPayload payload = default,
            BattleDiagnosticDefinitionKind definitionKind = BattleDiagnosticDefinitionKind.Unknown,
            int sourceActorGeneration = 0,
            int targetActorGeneration = 0,
            BattleDiagnosticRuntimeObjectKind subjectObjectKind = BattleDiagnosticRuntimeObjectKind.Unknown,
            long subjectRuntimeId = 0L,
            int subjectGeneration = 0)
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (monotonicTimestamp < 0) throw new ArgumentOutOfRangeException(nameof(monotonicTimestamp));
            if (payloadVersion < 1) throw new ArgumentOutOfRangeException(nameof(payloadVersion));
            if (!Enum.IsDefined(typeof(BattleDiagnosticDefinitionKind), definitionKind))
                throw new ArgumentOutOfRangeException(nameof(definitionKind));
            if (sourceActorGeneration < 0) throw new ArgumentOutOfRangeException(nameof(sourceActorGeneration));
            if (targetActorGeneration < 0) throw new ArgumentOutOfRangeException(nameof(targetActorGeneration));
            if (subjectGeneration < 0) throw new ArgumentOutOfRangeException(nameof(subjectGeneration));
            if (payload.HasValue && payloadVersion != payload.SchemaVersion)
            {
                throw new ArgumentException(
                    "Payload version must match the structured payload schema version.",
                    nameof(payloadVersion));
            }

            if (payload.Kind == BattleDiagnosticPayloadKind.SyncSnapshotReceived &&
                kind != BattleDiagnosticEventKind.Sync)
            {
                throw new ArgumentException(
                    "SyncSnapshotReceived payload requires a Sync event kind.",
                    nameof(payload));
            }

            if (payload.Kind == BattleDiagnosticPayloadKind.TriggerAnalysis &&
                kind != BattleDiagnosticEventKind.TriggerAnalysis)
            {
                throw new ArgumentException(
                    "TriggerAnalysis payload requires a TriggerAnalysis event kind.",
                    nameof(payload));
            }

            if (payload.Kind == BattleDiagnosticPayloadKind.SkillFailure &&
                kind != BattleDiagnosticEventKind.SkillFailure)
            {
                throw new ArgumentException(
                    "SkillFailure payload requires a SkillFailure event kind.",
                    nameof(payload));
            }

            if (payload.Kind == BattleDiagnosticPayloadKind.BuffLifecycle &&
                kind != BattleDiagnosticEventKind.BuffAdded &&
                kind != BattleDiagnosticEventKind.BuffRemoved)
            {
                throw new ArgumentException(
                    "BuffLifecycle payload requires a BuffAdded or BuffRemoved event kind.",
                    nameof(payload));
            }

            Scope = scope;
            Frame = frame;
            Sequence = sequence;
            MonotonicTimestamp = monotonicTimestamp;
            Kind = kind;
            Channel = channel;
            Outcome = outcome;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            SourceActor = BattleDiagnosticRuntimeObjectReference.Create(
                BattleDiagnosticRuntimeObjectKind.Actor,
                sourceActorId,
                sourceActorGeneration);
            TargetActor = BattleDiagnosticRuntimeObjectReference.Create(
                BattleDiagnosticRuntimeObjectKind.Actor,
                targetActorId,
                targetActorGeneration);
            SubjectObject = BattleDiagnosticRuntimeObjectReference.Create(
                subjectObjectKind,
                subjectRuntimeId,
                subjectGeneration);
            ConfigId = configId;
            DefinitionKind = definitionKind == BattleDiagnosticDefinitionKind.Unknown
                ? BattleDiagnosticDefinitionKinds.FromEventKind(kind)
                : definitionKind;
            RootContextId = rootContextId;
            ContextId = contextId;
            SkillRuntime = skillRuntime;
            AttackId = attackId;
            PayloadVersion = payloadVersion;
            Summary = summary ?? string.Empty;
            Payload = payload;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public int Frame { get; }
        public long Sequence { get; }
        public long MonotonicTimestamp { get; }
        public BattleDiagnosticEventKind Kind { get; }
        public BattleDiagnosticEventChannel Channel { get; }
        public BattleDiagnosticEventOutcome Outcome { get; }
        public long SourceActorId { get; }
        public long TargetActorId { get; }
        public BattleDiagnosticRuntimeObjectReference SourceActor { get; }
        public BattleDiagnosticRuntimeObjectReference TargetActor { get; }
        public BattleDiagnosticRuntimeObjectReference SubjectObject { get; }
        public int ConfigId { get; }
        public BattleDiagnosticDefinitionKind DefinitionKind { get; }
        public long RootContextId { get; }
        public long ContextId { get; }
        public BattleDiagnosticRuntimeHandle SkillRuntime { get; }
        public long AttackId { get; }
        public int PayloadVersion { get; }
        public string Summary { get; }
        public BattleDiagnosticEventPayload Payload { get; }
        public bool IsFailure => Outcome == BattleDiagnosticEventOutcome.Failed;
        public bool IsUnfinished => Outcome == BattleDiagnosticEventOutcome.None;

        public bool Equals(BattleDiagnosticEvent other)
        {
            return Scope.Equals(other.Scope) && Frame == other.Frame && Sequence == other.Sequence &&
                   MonotonicTimestamp == other.MonotonicTimestamp && Kind == other.Kind &&
                   Channel == other.Channel && Outcome == other.Outcome &&
                   SourceActorId == other.SourceActorId && TargetActorId == other.TargetActorId &&
                   SourceActor.Equals(other.SourceActor) && TargetActor.Equals(other.TargetActor) &&
                   SubjectObject.Equals(other.SubjectObject) &&
                   ConfigId == other.ConfigId && DefinitionKind == other.DefinitionKind &&
                   RootContextId == other.RootContextId &&
                   ContextId == other.ContextId && SkillRuntime.Equals(other.SkillRuntime) &&
                   AttackId == other.AttackId && PayloadVersion == other.PayloadVersion &&
                   string.Equals(Summary, other.Summary, StringComparison.Ordinal) &&
                   Payload.Equals(other.Payload);
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticEvent other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ Frame;
                hashCode = (hashCode * 397) ^ Sequence.GetHashCode();
                hashCode = (hashCode * 397) ^ MonotonicTimestamp.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Kind;
                hashCode = (hashCode * 397) ^ (int)Channel;
                hashCode = (hashCode * 397) ^ (int)Outcome;
                hashCode = (hashCode * 397) ^ SourceActorId.GetHashCode();
                hashCode = (hashCode * 397) ^ TargetActorId.GetHashCode();
                hashCode = (hashCode * 397) ^ SourceActor.GetHashCode();
                hashCode = (hashCode * 397) ^ TargetActor.GetHashCode();
                hashCode = (hashCode * 397) ^ SubjectObject.GetHashCode();
                hashCode = (hashCode * 397) ^ ConfigId;
                hashCode = (hashCode * 397) ^ (int)DefinitionKind;
                hashCode = (hashCode * 397) ^ RootContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ ContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ SkillRuntime.GetHashCode();
                hashCode = (hashCode * 397) ^ AttackId.GetHashCode();
                hashCode = (hashCode * 397) ^ PayloadVersion;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Summary ?? string.Empty);
                hashCode = (hashCode * 397) ^ Payload.GetHashCode();
                return hashCode;
            }
        }
    }

    public readonly struct BattleDiagnosticTraceNodeSummary : IEquatable<BattleDiagnosticTraceNodeSummary>
    {
        public BattleDiagnosticTraceNodeSummary(
            BattleDiagnosticSessionScope scope,
            long rootContextId,
            long contextId,
            long parentContextId,
            int startFrame,
            int endFrame,
            BattleDiagnosticTraceNodeState state,
            long actorId = 0,
            int configId = 0,
            string kind = "",
            string endReason = "",
            int skillId = 0,
            int castFlowId = 0,
            string phaseId = "")
        {
            if (rootContextId == 0) throw new ArgumentOutOfRangeException(nameof(rootContextId));
            if (contextId == 0) throw new ArgumentOutOfRangeException(nameof(contextId));
            if (!BattleDiagnosticFrames.IsValid(startFrame)) throw new ArgumentOutOfRangeException(nameof(startFrame));
            if (BattleDiagnosticFrames.IsValid(endFrame) && endFrame < startFrame)
                throw new ArgumentOutOfRangeException(nameof(endFrame));

            Scope = scope;
            RootContextId = rootContextId;
            ContextId = contextId;
            ParentContextId = parentContextId;
            StartFrame = startFrame;
            EndFrame = endFrame;
            State = state;
            ActorId = actorId;
            ConfigId = configId;
            Kind = kind ?? string.Empty;
            EndReason = endReason ?? string.Empty;
            SkillId = skillId;
            CastFlowId = castFlowId;
            PhaseId = phaseId ?? string.Empty;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public long RootContextId { get; }
        public long ContextId { get; }
        public long ParentContextId { get; }
        public int StartFrame { get; }
        public int EndFrame { get; }
        public BattleDiagnosticTraceNodeState State { get; }
        public long ActorId { get; }
        public int ConfigId { get; }
        public string Kind { get; }
        public string EndReason { get; }
        public int SkillId { get; }
        public int CastFlowId { get; }
        public string PhaseId { get; }
        public bool IsActive => State == BattleDiagnosticTraceNodeState.Active;

        public bool Equals(BattleDiagnosticTraceNodeSummary other)
        {
            return Scope.Equals(other.Scope) && RootContextId == other.RootContextId &&
                   ContextId == other.ContextId && ParentContextId == other.ParentContextId &&
                   StartFrame == other.StartFrame && EndFrame == other.EndFrame && State == other.State &&
                   ActorId == other.ActorId && ConfigId == other.ConfigId &&
                   string.Equals(Kind, other.Kind, StringComparison.Ordinal) &&
                   string.Equals(EndReason, other.EndReason, StringComparison.Ordinal) &&
                   SkillId == other.SkillId && CastFlowId == other.CastFlowId &&
                   string.Equals(PhaseId, other.PhaseId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticTraceNodeSummary other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ RootContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ ContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ ParentContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ StartFrame;
                hashCode = (hashCode * 397) ^ EndFrame;
                hashCode = (hashCode * 397) ^ (int)State;
                hashCode = (hashCode * 397) ^ ActorId.GetHashCode();
                hashCode = (hashCode * 397) ^ ConfigId;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Kind ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(EndReason ?? string.Empty);
                hashCode = (hashCode * 397) ^ SkillId;
                hashCode = (hashCode * 397) ^ CastFlowId;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(PhaseId ?? string.Empty);
                return hashCode;
            }
        }
    }

    public enum BattleDiagnosticMetricCategory
    {
        Unknown = 0,
        Prediction = 1,
        Network = 2,
        Rollback = 3,
        TimeSync = 4,
        Reconciliation = 5,
        Simulation = 6
    }

    public enum BattleDiagnosticMetricValueKind
    {
        Gauge = 0,
        Counter = 1,
        Flag = 2
    }

    public static class BattleDiagnosticFrameMetricKeys
    {
        public const string PredictionConfirmedFrame = "prediction.confirmed_frame";
        public const string PredictionPredictedFrame = "prediction.predicted_frame";
        public const string PredictionAheadFrames = "prediction.ahead_frames";
        public const string PredictionBacklog = "prediction.backlog";
        public const string PredictionWindow = "prediction.window";
        public const string PredictionStalled = "prediction.stalled";
        public const string NetworkDelayFrames = "network.delay_frames";
        public const string NetworkBufferedCount = "network.buffered_count";
        public const string NetworkTargetGap = "network.target_gap";
        public const string NetworkDuplicateTotal = "network.duplicate_total";
        public const string NetworkLateTotal = "network.late_total";
        public const string RollbackActive = "rollback.active";
        public const string RollbackReplayToFrame = "rollback.replay_to_frame";
        public const string RollbackLastFrame = "rollback.last_frame";
        public const string RollbackTotal = "rollback.total";
        public const string RollbackRestoreFailedTotal = "rollback.restore_failed_total";
        public const string SimulationLastUpdateSteps = "simulation.last_update_steps";
        public const string SimulationBacklogSteps = "simulation.backlog_steps";
        public const string SimulationOverBudgetUpdateTotal = "simulation.over_budget_update_total";
        public const string SimulationDroppedTimeSecondsTotal = "simulation.dropped_time_seconds_total";
        public const string SimulationInvalidDeltaTotal = "simulation.invalid_delta_total";
    }

    /// <summary>A compact, frame-addressable value produced by a runtime diagnostics hook.</summary>
    public readonly struct BattleDiagnosticMetricSample : IEquatable<BattleDiagnosticMetricSample>
    {
        public BattleDiagnosticMetricSample(
            BattleDiagnosticSessionScope scope,
            long sequence,
            int frame,
            long monotonicTimestamp,
            BattleDiagnosticMetricCategory category,
            BattleDiagnosticMetricValueKind valueKind,
            string metric,
            double value,
            string dimension = "")
        {
            if (!scope.IsValid) throw new ArgumentException("A valid session scope is required.", nameof(scope));
            if (sequence <= 0L) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (monotonicTimestamp < 0L) throw new ArgumentOutOfRangeException(nameof(monotonicTimestamp));
            if (!Enum.IsDefined(typeof(BattleDiagnosticMetricCategory), category) ||
                category == BattleDiagnosticMetricCategory.Unknown)
                throw new ArgumentOutOfRangeException(nameof(category));
            if (!Enum.IsDefined(typeof(BattleDiagnosticMetricValueKind), valueKind))
                throw new ArgumentOutOfRangeException(nameof(valueKind));
            if (string.IsNullOrWhiteSpace(metric)) throw new ArgumentException("A stable metric key is required.", nameof(metric));
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));

            Scope = scope;
            Sequence = sequence;
            Frame = frame;
            MonotonicTimestamp = monotonicTimestamp;
            Category = category;
            ValueKind = valueKind;
            Metric = metric;
            Value = value;
            Dimension = dimension ?? string.Empty;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public long Sequence { get; }
        public int Frame { get; }
        public long MonotonicTimestamp { get; }
        public BattleDiagnosticMetricCategory Category { get; }
        public BattleDiagnosticMetricValueKind ValueKind { get; }
        public string Metric { get; }
        public double Value { get; }
        public string Dimension { get; }

        public bool Equals(BattleDiagnosticMetricSample other)
        {
            return Scope.Equals(other.Scope) && Sequence == other.Sequence && Frame == other.Frame &&
                   MonotonicTimestamp == other.MonotonicTimestamp && Category == other.Category &&
                   ValueKind == other.ValueKind && string.Equals(Metric, other.Metric, StringComparison.Ordinal) &&
                   Value.Equals(other.Value) && string.Equals(Dimension, other.Dimension, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticMetricSample other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ Sequence.GetHashCode();
                hashCode = (hashCode * 397) ^ Frame;
                hashCode = (hashCode * 397) ^ MonotonicTimestamp.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Category;
                hashCode = (hashCode * 397) ^ (int)ValueKind;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Metric ?? string.Empty);
                hashCode = (hashCode * 397) ^ Value.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Dimension ?? string.Empty);
                return hashCode;
            }
        }
    }

    /// <summary>A loss-aware frame bucket projected from one metric series.</summary>
    public readonly struct BattleDiagnosticMetricAggregate : IEquatable<BattleDiagnosticMetricAggregate>
    {
        public BattleDiagnosticMetricAggregate(
            BattleDiagnosticSessionScope scope,
            int firstFrame,
            int lastFrame,
            long firstMonotonicTimestamp,
            long lastMonotonicTimestamp,
            BattleDiagnosticMetricCategory category,
            BattleDiagnosticMetricValueKind valueKind,
            string metric,
            string dimension,
            double firstValue,
            double lastValue,
            double minimumValue,
            double maximumValue,
            int sampleCount)
        {
            if (!scope.IsValid) throw new ArgumentException("A valid session scope is required.", nameof(scope));
            if (!BattleDiagnosticFrames.IsValid(firstFrame)) throw new ArgumentOutOfRangeException(nameof(firstFrame));
            if (lastFrame < firstFrame) throw new ArgumentOutOfRangeException(nameof(lastFrame));
            if (firstMonotonicTimestamp < 0L) throw new ArgumentOutOfRangeException(nameof(firstMonotonicTimestamp));
            if (lastMonotonicTimestamp < firstMonotonicTimestamp)
                throw new ArgumentOutOfRangeException(nameof(lastMonotonicTimestamp));
            if (!Enum.IsDefined(typeof(BattleDiagnosticMetricCategory), category) ||
                category == BattleDiagnosticMetricCategory.Unknown)
                throw new ArgumentOutOfRangeException(nameof(category));
            if (!Enum.IsDefined(typeof(BattleDiagnosticMetricValueKind), valueKind))
                throw new ArgumentOutOfRangeException(nameof(valueKind));
            if (string.IsNullOrWhiteSpace(metric)) throw new ArgumentException("A stable metric key is required.", nameof(metric));
            if (sampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(sampleCount));
            if (double.IsNaN(firstValue) || double.IsInfinity(firstValue))
                throw new ArgumentOutOfRangeException(nameof(firstValue));
            if (double.IsNaN(lastValue) || double.IsInfinity(lastValue))
                throw new ArgumentOutOfRangeException(nameof(lastValue));
            if (double.IsNaN(minimumValue) || double.IsInfinity(minimumValue))
                throw new ArgumentOutOfRangeException(nameof(minimumValue));
            if (double.IsNaN(maximumValue) || double.IsInfinity(maximumValue) || maximumValue < minimumValue)
                throw new ArgumentOutOfRangeException(nameof(maximumValue));

            Scope = scope;
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
            FirstMonotonicTimestamp = firstMonotonicTimestamp;
            LastMonotonicTimestamp = lastMonotonicTimestamp;
            Category = category;
            ValueKind = valueKind;
            Metric = metric;
            Dimension = dimension ?? string.Empty;
            FirstValue = firstValue;
            LastValue = lastValue;
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
            SampleCount = sampleCount;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public int FirstFrame { get; }
        public int LastFrame { get; }
        public long FirstMonotonicTimestamp { get; }
        public long LastMonotonicTimestamp { get; }
        public BattleDiagnosticMetricCategory Category { get; }
        public BattleDiagnosticMetricValueKind ValueKind { get; }
        public string Metric { get; }
        public string Dimension { get; }
        public double FirstValue { get; }
        public double LastValue { get; }
        public double MinimumValue { get; }
        public double MaximumValue { get; }
        public int SampleCount { get; }

        public bool Equals(BattleDiagnosticMetricAggregate other)
        {
            return Scope.Equals(other.Scope) && FirstFrame == other.FirstFrame && LastFrame == other.LastFrame &&
                   FirstMonotonicTimestamp == other.FirstMonotonicTimestamp &&
                   LastMonotonicTimestamp == other.LastMonotonicTimestamp && Category == other.Category &&
                   ValueKind == other.ValueKind && string.Equals(Metric, other.Metric, StringComparison.Ordinal) &&
                   string.Equals(Dimension, other.Dimension, StringComparison.Ordinal) &&
                   FirstValue.Equals(other.FirstValue) && LastValue.Equals(other.LastValue) &&
                   MinimumValue.Equals(other.MinimumValue) && MaximumValue.Equals(other.MaximumValue) &&
                   SampleCount == other.SampleCount;
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticMetricAggregate other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ FirstFrame;
                hashCode = (hashCode * 397) ^ LastFrame;
                hashCode = (hashCode * 397) ^ (int)Category;
                hashCode = (hashCode * 397) ^ (int)ValueKind;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Metric ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Dimension ?? string.Empty);
                hashCode = (hashCode * 397) ^ LastValue.GetHashCode();
                hashCode = (hashCode * 397) ^ MinimumValue.GetHashCode();
                hashCode = (hashCode * 397) ^ MaximumValue.GetHashCode();
                hashCode = (hashCode * 397) ^ SampleCount;
                return hashCode;
            }
        }
    }

    public enum BattleDiagnosticMetricAssessmentMode
    {
        None = 0,
        LatestHigh = 1,
        WindowMaximumHigh = 2,
        WindowDeltaHigh = 3
    }

    public enum BattleDiagnosticMetricSeverity
    {
        Normal = 0,
        Warning = 1,
        Critical = 2
    }

    public readonly struct BattleDiagnosticMetricDescriptor
    {
        public BattleDiagnosticMetricDescriptor(
            string metric,
            BattleDiagnosticMetricCategory category,
            BattleDiagnosticMetricValueKind valueKind,
            string displayName,
            string unit,
            string group,
            int order,
            double suggestedMinimum = double.NaN,
            double suggestedMaximum = double.NaN,
            BattleDiagnosticMetricAssessmentMode assessmentMode = BattleDiagnosticMetricAssessmentMode.None,
            double warningThreshold = double.NaN,
            double criticalThreshold = double.NaN)
        {
            if (string.IsNullOrWhiteSpace(metric)) throw new ArgumentException("A stable metric key is required.", nameof(metric));
            if (!Enum.IsDefined(typeof(BattleDiagnosticMetricCategory), category) ||
                category == BattleDiagnosticMetricCategory.Unknown)
                throw new ArgumentOutOfRangeException(nameof(category));
            if (!Enum.IsDefined(typeof(BattleDiagnosticMetricValueKind), valueKind))
                throw new ArgumentOutOfRangeException(nameof(valueKind));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
            if (!Enum.IsDefined(typeof(BattleDiagnosticMetricAssessmentMode), assessmentMode))
                throw new ArgumentOutOfRangeException(nameof(assessmentMode));
            var hasSuggestedRange = !double.IsNaN(suggestedMinimum) || !double.IsNaN(suggestedMaximum);
            if (hasSuggestedRange &&
                (double.IsNaN(suggestedMinimum) || double.IsInfinity(suggestedMinimum) ||
                 double.IsNaN(suggestedMaximum) || double.IsInfinity(suggestedMaximum) ||
                 suggestedMaximum <= suggestedMinimum))
                throw new ArgumentException("Suggested metric ranges require finite increasing bounds.");
            var hasAssessment = assessmentMode != BattleDiagnosticMetricAssessmentMode.None;
            if (hasAssessment &&
                (double.IsNaN(warningThreshold) || double.IsInfinity(warningThreshold) ||
                 double.IsNaN(criticalThreshold) || double.IsInfinity(criticalThreshold) ||
                 criticalThreshold < warningThreshold))
                throw new ArgumentException("Metric assessments require finite increasing thresholds.");

            Metric = metric;
            Category = category;
            ValueKind = valueKind;
            DisplayName = displayName;
            Unit = unit ?? string.Empty;
            Group = group ?? string.Empty;
            Order = order;
            SuggestedMinimum = suggestedMinimum;
            SuggestedMaximum = suggestedMaximum;
            AssessmentMode = assessmentMode;
            WarningThreshold = warningThreshold;
            CriticalThreshold = criticalThreshold;
        }

        public string Metric { get; }
        public BattleDiagnosticMetricCategory Category { get; }
        public BattleDiagnosticMetricValueKind ValueKind { get; }
        public string DisplayName { get; }
        public string Unit { get; }
        public string Group { get; }
        public int Order { get; }
        public double SuggestedMinimum { get; }
        public double SuggestedMaximum { get; }
        public BattleDiagnosticMetricAssessmentMode AssessmentMode { get; }
        public double WarningThreshold { get; }
        public double CriticalThreshold { get; }
        public bool HasSuggestedRange => !double.IsNaN(SuggestedMinimum) && !double.IsNaN(SuggestedMaximum);
        public bool HasAssessment => AssessmentMode != BattleDiagnosticMetricAssessmentMode.None;

        public BattleDiagnosticMetricDescriptor WithThresholds(
            double warningThreshold,
            double criticalThreshold,
            double suggestedMinimum = double.NaN,
            double suggestedMaximum = double.NaN)
        {
            return new BattleDiagnosticMetricDescriptor(
                Metric,
                Category,
                ValueKind,
                DisplayName,
                Unit,
                Group,
                Order,
                double.IsNaN(suggestedMinimum) ? SuggestedMinimum : suggestedMinimum,
                double.IsNaN(suggestedMaximum) ? SuggestedMaximum : suggestedMaximum,
                AssessmentMode,
                warningThreshold,
                criticalThreshold);
        }
    }

    public readonly struct BattleDiagnosticMetricAssessment
    {
        public BattleDiagnosticMetricAssessment(
            in BattleDiagnosticMetricDescriptor descriptor,
            string dimension,
            BattleDiagnosticMetricSeverity severity,
            double actualValue,
            int firstFrame,
            int lastFrame,
            int sampleCount)
        {
            Descriptor = descriptor;
            Dimension = dimension ?? string.Empty;
            Severity = severity;
            ActualValue = actualValue;
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
            SampleCount = sampleCount;
        }

        public BattleDiagnosticMetricDescriptor Descriptor { get; }
        public string Dimension { get; }
        public BattleDiagnosticMetricSeverity Severity { get; }
        public double ActualValue { get; }
        public int FirstFrame { get; }
        public int LastFrame { get; }
        public int SampleCount { get; }
        public bool IsIssue => Severity >= BattleDiagnosticMetricSeverity.Warning;
        public double ActiveThreshold => Severity == BattleDiagnosticMetricSeverity.Critical
            ? Descriptor.CriticalThreshold
            : Descriptor.WarningThreshold;
    }

    public readonly struct BattleDiagnosticMetricProfileContext : IEquatable<BattleDiagnosticMetricProfileContext>
    {
        public BattleDiagnosticMetricProfileContext(
            string project,
            string gameMode = "",
            string networkMode = "",
            string deviceTier = "")
        {
            Project = project ?? string.Empty;
            GameMode = gameMode ?? string.Empty;
            NetworkMode = networkMode ?? string.Empty;
            DeviceTier = deviceTier ?? string.Empty;
        }

        public string Project { get; }
        public string GameMode { get; }
        public string NetworkMode { get; }
        public string DeviceTier { get; }

        public bool Equals(BattleDiagnosticMetricProfileContext other)
        {
            return EqualsValue(Project, other.Project) && EqualsValue(GameMode, other.GameMode) &&
                   EqualsValue(NetworkMode, other.NetworkMode) && EqualsValue(DeviceTier, other.DeviceTier);
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticMetricProfileContext other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.OrdinalIgnoreCase.GetHashCode(Project ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(GameMode ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(NetworkMode ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(DeviceTier ?? string.Empty);
                return hashCode;
            }
        }

        internal static bool EqualsValue(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    public readonly struct BattleDiagnosticMetricThresholdOverride
    {
        public BattleDiagnosticMetricThresholdOverride(
            string metric,
            double warningThreshold,
            double criticalThreshold,
            double suggestedMinimum = double.NaN,
            double suggestedMaximum = double.NaN)
        {
            if (string.IsNullOrWhiteSpace(metric)) throw new ArgumentException("A stable metric key is required.", nameof(metric));
            if (double.IsNaN(warningThreshold) || double.IsInfinity(warningThreshold))
                throw new ArgumentOutOfRangeException(nameof(warningThreshold));
            if (double.IsNaN(criticalThreshold) || double.IsInfinity(criticalThreshold) ||
                criticalThreshold < warningThreshold)
                throw new ArgumentOutOfRangeException(nameof(criticalThreshold));
            var hasSuggestedRange = !double.IsNaN(suggestedMinimum) || !double.IsNaN(suggestedMaximum);
            if (hasSuggestedRange &&
                (double.IsNaN(suggestedMinimum) || double.IsInfinity(suggestedMinimum) ||
                 double.IsNaN(suggestedMaximum) || double.IsInfinity(suggestedMaximum) ||
                 suggestedMaximum <= suggestedMinimum))
                throw new ArgumentException("Suggested metric ranges require finite increasing bounds.");
            Metric = metric;
            WarningThreshold = warningThreshold;
            CriticalThreshold = criticalThreshold;
            SuggestedMinimum = suggestedMinimum;
            SuggestedMaximum = suggestedMaximum;
        }

        public string Metric { get; }
        public double WarningThreshold { get; }
        public double CriticalThreshold { get; }
        public double SuggestedMinimum { get; }
        public double SuggestedMaximum { get; }
    }

    public sealed class BattleDiagnosticMetricProfileLayer
    {
        private readonly BattleDiagnosticMetricThresholdOverride[] _overrides;

        public BattleDiagnosticMetricProfileLayer(
            string name,
            int priority,
            IEnumerable<BattleDiagnosticMetricThresholdOverride> overrides,
            string project = "",
            string gameMode = "",
            string networkMode = "",
            string deviceTier = "")
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A profile layer name is required.", nameof(name));
            Name = name;
            Priority = priority;
            Project = project ?? string.Empty;
            GameMode = gameMode ?? string.Empty;
            NetworkMode = networkMode ?? string.Empty;
            DeviceTier = deviceTier ?? string.Empty;
            _overrides = overrides == null
                ? Array.Empty<BattleDiagnosticMetricThresholdOverride>()
                : new List<BattleDiagnosticMetricThresholdOverride>(overrides).ToArray();
        }

        public string Name { get; }
        public int Priority { get; }
        public string Project { get; }
        public string GameMode { get; }
        public string NetworkMode { get; }
        public string DeviceTier { get; }
        public IReadOnlyList<BattleDiagnosticMetricThresholdOverride> Overrides => _overrides;
        public int Specificity => Count(Project) + Count(GameMode) + Count(NetworkMode) + Count(DeviceTier);

        public bool Matches(in BattleDiagnosticMetricProfileContext context)
        {
            return Matches(Project, context.Project) && Matches(GameMode, context.GameMode) &&
                   Matches(NetworkMode, context.NetworkMode) && Matches(DeviceTier, context.DeviceTier);
        }

        private static int Count(string value) => string.IsNullOrEmpty(value) ? 0 : 1;

        private static bool Matches(string selector, string actual)
        {
            return string.IsNullOrEmpty(selector) ||
                   string.Equals(selector, actual ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class BattleDiagnosticResolvedMetricProfile
    {
        private readonly BattleDiagnosticMetricDescriptor[] _descriptors;
        private readonly string[] _matchedLayers;

        internal BattleDiagnosticResolvedMetricProfile(
            in BattleDiagnosticMetricProfileContext context,
            BattleDiagnosticMetricDescriptor[] descriptors,
            string[] matchedLayers,
            string name = "")
        {
            Context = context;
            _descriptors = descriptors ?? Array.Empty<BattleDiagnosticMetricDescriptor>();
            _matchedLayers = matchedLayers ?? Array.Empty<string>();
            Name = string.IsNullOrEmpty(name)
                ? _matchedLayers.Length == 0 ? "Default" : "Default + " + string.Join(" + ", _matchedLayers)
                : name;
        }

        public string Name { get; }
        public BattleDiagnosticMetricProfileContext Context { get; }
        public IReadOnlyList<BattleDiagnosticMetricDescriptor> Descriptors => _descriptors;
        public IReadOnlyList<string> MatchedLayers => _matchedLayers;

        public bool TryGet(string metric, out BattleDiagnosticMetricDescriptor descriptor)
        {
            for (var i = 0; i < _descriptors.Length; i++)
            {
                if (!string.Equals(_descriptors[i].Metric, metric, StringComparison.Ordinal)) continue;
                descriptor = _descriptors[i];
                return true;
            }
            descriptor = default;
            return false;
        }
    }

    public readonly struct BattleDiagnosticMetricProfileDifference
    {
        public BattleDiagnosticMetricProfileDifference(
            in BattleDiagnosticMetricDescriptor captured,
            in BattleDiagnosticMetricDescriptor current)
        {
            Metric = captured.Metric;
            DisplayName = captured.DisplayName;
            Unit = captured.Unit;
            CapturedWarningThreshold = captured.WarningThreshold;
            CurrentWarningThreshold = current.WarningThreshold;
            CapturedCriticalThreshold = captured.CriticalThreshold;
            CurrentCriticalThreshold = current.CriticalThreshold;
            CapturedSuggestedMinimum = captured.SuggestedMinimum;
            CurrentSuggestedMinimum = current.SuggestedMinimum;
            CapturedSuggestedMaximum = captured.SuggestedMaximum;
            CurrentSuggestedMaximum = current.SuggestedMaximum;
        }

        public string Metric { get; }
        public string DisplayName { get; }
        public string Unit { get; }
        public double CapturedWarningThreshold { get; }
        public double CurrentWarningThreshold { get; }
        public double CapturedCriticalThreshold { get; }
        public double CurrentCriticalThreshold { get; }
        public double CapturedSuggestedMinimum { get; }
        public double CurrentSuggestedMinimum { get; }
        public double CapturedSuggestedMaximum { get; }
        public double CurrentSuggestedMaximum { get; }
        public bool WarningChanged => !Same(CapturedWarningThreshold, CurrentWarningThreshold);
        public bool CriticalChanged => !Same(CapturedCriticalThreshold, CurrentCriticalThreshold);
        public bool SuggestedRangeChanged =>
            !Same(CapturedSuggestedMinimum, CurrentSuggestedMinimum) ||
            !Same(CapturedSuggestedMaximum, CurrentSuggestedMaximum);

        private static bool Same(double left, double right) =>
            left.Equals(right) || double.IsNaN(left) && double.IsNaN(right);
    }

    public sealed class BattleDiagnosticMetricProfileComparison
    {
        private readonly BattleDiagnosticMetricProfileDifference[] _thresholdDifferences;

        internal BattleDiagnosticMetricProfileComparison(
            BattleDiagnosticResolvedMetricProfile captured,
            BattleDiagnosticResolvedMetricProfile current,
            BattleDiagnosticMetricProfileDifference[] thresholdDifferences)
        {
            Captured = captured;
            Current = current;
            _thresholdDifferences = thresholdDifferences ??
                                    Array.Empty<BattleDiagnosticMetricProfileDifference>();
        }

        public BattleDiagnosticResolvedMetricProfile Captured { get; }
        public BattleDiagnosticResolvedMetricProfile Current { get; }
        public bool ContextMatches => Captured.Context.Equals(Current.Context);
        public IReadOnlyList<BattleDiagnosticMetricProfileDifference> ThresholdDifferences =>
            _thresholdDifferences;
        public bool HasDifferences => !ContextMatches || _thresholdDifferences.Length > 0;
    }

    public static class BattleDiagnosticMetricProfileComparer
    {
        public static BattleDiagnosticMetricProfileComparison Compare(
            BattleDiagnosticResolvedMetricProfile captured,
            BattleDiagnosticResolvedMetricProfile current)
        {
            if (captured == null) throw new ArgumentNullException(nameof(captured));
            if (current == null) throw new ArgumentNullException(nameof(current));
            var differences = new List<BattleDiagnosticMetricProfileDifference>();
            for (var i = 0; i < captured.Descriptors.Count; i++)
            {
                var capturedDescriptor = captured.Descriptors[i];
                if (!capturedDescriptor.HasAssessment ||
                    !current.TryGet(capturedDescriptor.Metric, out var currentDescriptor))
                    continue;
                var difference = new BattleDiagnosticMetricProfileDifference(
                    in capturedDescriptor,
                    in currentDescriptor);
                if (difference.WarningChanged || difference.CriticalChanged ||
                    difference.SuggestedRangeChanged)
                    differences.Add(difference);
            }
            return new BattleDiagnosticMetricProfileComparison(
                captured,
                current,
                differences.ToArray());
        }
    }

    public static class BattleDiagnosticMetricProfileResolver
    {
        public static BattleDiagnosticResolvedMetricProfile Resolve(
            in BattleDiagnosticMetricProfileContext context,
            IReadOnlyList<BattleDiagnosticMetricProfileLayer> layers = null)
        {
            var descriptors = new BattleDiagnosticMetricDescriptor[BattleDiagnosticFrameMetricCatalog.All.Count];
            for (var i = 0; i < descriptors.Length; i++)
                descriptors[i] = BattleDiagnosticFrameMetricCatalog.All[i];
            var matches = new List<BattleDiagnosticMetricProfileLayer>();
            if (layers != null)
            {
                for (var i = 0; i < layers.Count; i++)
                {
                    var layer = layers[i];
                    if (layer != null && layer.Matches(in context)) matches.Add(layer);
                }
            }
            matches.Sort(CompareLayers);

            for (var i = 0; i < matches.Count; i++)
            {
                var layer = matches[i];
                for (var j = 0; j < layer.Overrides.Count; j++)
                {
                    var item = layer.Overrides[j];
                    for (var k = 0; k < descriptors.Length; k++)
                    {
                        if (!string.Equals(descriptors[k].Metric, item.Metric, StringComparison.Ordinal)) continue;
                        descriptors[k] = descriptors[k].WithThresholds(
                            item.WarningThreshold,
                            item.CriticalThreshold,
                            item.SuggestedMinimum,
                            item.SuggestedMaximum);
                        break;
                    }
                }
            }

            var names = new string[matches.Count];
            for (var i = 0; i < matches.Count; i++) names[i] = matches[i].Name;
            return new BattleDiagnosticResolvedMetricProfile(in context, descriptors, names);
        }

        public static BattleDiagnosticResolvedMetricProfile Restore(
            in BattleDiagnosticMetricProfileContext context,
            string name,
            IReadOnlyList<BattleDiagnosticMetricThresholdOverride> overrides)
        {
            var layer = new BattleDiagnosticMetricProfileLayer(
                "Captured",
                0,
                overrides ?? Array.Empty<BattleDiagnosticMetricThresholdOverride>());
            var resolved = Resolve(in context, new[] { layer });
            var descriptors = new BattleDiagnosticMetricDescriptor[resolved.Descriptors.Count];
            for (var i = 0; i < descriptors.Length; i++) descriptors[i] = resolved.Descriptors[i];
            return new BattleDiagnosticResolvedMetricProfile(
                in context,
                descriptors,
                new[] { "Captured" },
                string.IsNullOrEmpty(name) ? "Captured" : name);
        }

        private static int CompareLayers(
            BattleDiagnosticMetricProfileLayer left,
            BattleDiagnosticMetricProfileLayer right)
        {
            var comparison = left.Specificity.CompareTo(right.Specificity);
            if (comparison != 0) return comparison;
            comparison = left.Priority.CompareTo(right.Priority);
            return comparison != 0
                ? comparison
                : string.Compare(left.Name, right.Name, StringComparison.Ordinal);
        }
    }

    public static class BattleDiagnosticMetricProfileRegistry
    {
        private static readonly object Sync = new object();
        private static readonly List<BattleDiagnosticMetricProfileLayer> Layers =
            new List<BattleDiagnosticMetricProfileLayer>();
        private static BattleDiagnosticMetricProfileContext _context =
            new BattleDiagnosticMetricProfileContext("AbilityKit.Demo.Moba");
        private static long _revision;

        public static long Revision
        {
            get { lock (Sync) return _revision; }
        }

        public static BattleDiagnosticMetricProfileContext Context
        {
            get { lock (Sync) return _context; }
        }

        public static void SetContext(in BattleDiagnosticMetricProfileContext context)
        {
            lock (Sync)
            {
                if (_context.Equals(context)) return;
                _context = context;
                _revision++;
            }
        }

        public static void RegisterOrReplace(BattleDiagnosticMetricProfileLayer layer)
        {
            if (layer == null) throw new ArgumentNullException(nameof(layer));
            lock (Sync)
            {
                for (var i = 0; i < Layers.Count; i++)
                {
                    if (!string.Equals(Layers[i].Name, layer.Name, StringComparison.Ordinal)) continue;
                    Layers[i] = layer;
                    _revision++;
                    return;
                }
                Layers.Add(layer);
                _revision++;
            }
        }

        public static void ReplaceAll(
            in BattleDiagnosticMetricProfileContext context,
            IEnumerable<BattleDiagnosticMetricProfileLayer> layers)
        {
            var replacements = layers == null
                ? new List<BattleDiagnosticMetricProfileLayer>()
                : new List<BattleDiagnosticMetricProfileLayer>(layers);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < replacements.Count; i++)
            {
                var layer = replacements[i];
                if (layer == null) throw new ArgumentException("Profile layers cannot contain null entries.", nameof(layers));
                if (!names.Add(layer.Name))
                    throw new ArgumentException("Profile layer names must be unique: " + layer.Name, nameof(layers));
            }
            lock (Sync)
            {
                _context = context;
                Layers.Clear();
                Layers.AddRange(replacements);
                _revision++;
            }
        }

        public static bool Remove(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            lock (Sync)
            {
                for (var i = 0; i < Layers.Count; i++)
                {
                    if (!string.Equals(Layers[i].Name, name, StringComparison.Ordinal)) continue;
                    Layers.RemoveAt(i);
                    _revision++;
                    return true;
                }
                return false;
            }
        }

        public static BattleDiagnosticResolvedMetricProfile Resolve()
        {
            lock (Sync)
            {
                return BattleDiagnosticMetricProfileResolver.Resolve(in _context, Layers);
            }
        }
    }

    public readonly struct BattleDiagnosticCompoundMetricRule
    {
        public BattleDiagnosticCompoundMetricRule(
            string id,
            string displayName,
            BattleDiagnosticMetricCategory category,
            string primaryMetric,
            string secondaryMetric)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable rule id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            if (category == BattleDiagnosticMetricCategory.Unknown) throw new ArgumentOutOfRangeException(nameof(category));
            if (string.IsNullOrWhiteSpace(primaryMetric)) throw new ArgumentException("A primary metric is required.", nameof(primaryMetric));
            if (string.IsNullOrWhiteSpace(secondaryMetric)) throw new ArgumentException("A secondary metric is required.", nameof(secondaryMetric));
            Id = id;
            DisplayName = displayName;
            Category = category;
            PrimaryMetric = primaryMetric;
            SecondaryMetric = secondaryMetric;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public BattleDiagnosticMetricCategory Category { get; }
        public string PrimaryMetric { get; }
        public string SecondaryMetric { get; }
    }

    public readonly struct BattleDiagnosticCompoundMetricAssessment
    {
        public BattleDiagnosticCompoundMetricAssessment(
            in BattleDiagnosticCompoundMetricRule rule,
            string dimension,
            BattleDiagnosticMetricSeverity severity,
            in BattleDiagnosticMetricAssessment primary,
            in BattleDiagnosticMetricAssessment secondary)
        {
            Rule = rule;
            Dimension = dimension ?? string.Empty;
            Severity = severity;
            Primary = primary;
            Secondary = secondary;
            FirstFrame = Math.Min(primary.FirstFrame, secondary.FirstFrame);
            LastFrame = Math.Max(primary.LastFrame, secondary.LastFrame);
        }

        public BattleDiagnosticCompoundMetricRule Rule { get; }
        public string Dimension { get; }
        public BattleDiagnosticMetricSeverity Severity { get; }
        public BattleDiagnosticMetricAssessment Primary { get; }
        public BattleDiagnosticMetricAssessment Secondary { get; }
        public int FirstFrame { get; }
        public int LastFrame { get; }
    }

    public static class BattleDiagnosticFrameMetricCatalog
    {
        private static readonly BattleDiagnosticMetricDescriptor[] Descriptors =
        {
            Metric(BattleDiagnosticFrameMetricKeys.PredictionConfirmedFrame, BattleDiagnosticMetricCategory.Prediction, BattleDiagnosticMetricValueKind.Gauge, "Confirmed Frame", "frame", "prediction.cursor", 10),
            Metric(BattleDiagnosticFrameMetricKeys.PredictionPredictedFrame, BattleDiagnosticMetricCategory.Prediction, BattleDiagnosticMetricValueKind.Gauge, "Predicted Frame", "frame", "prediction.cursor", 20),
            Metric(BattleDiagnosticFrameMetricKeys.PredictionAheadFrames, BattleDiagnosticMetricCategory.Prediction, BattleDiagnosticMetricValueKind.Gauge, "Prediction Ahead", "frames", "prediction.pressure", 30, 0d, 8d, BattleDiagnosticMetricAssessmentMode.WindowMaximumHigh, 4d, 8d),
            Metric(BattleDiagnosticFrameMetricKeys.PredictionBacklog, BattleDiagnosticMetricCategory.Prediction, BattleDiagnosticMetricValueKind.Gauge, "Prediction Backlog", "frames", "prediction.pressure", 40, 0d, 8d, BattleDiagnosticMetricAssessmentMode.WindowMaximumHigh, 4d, 8d),
            Metric(BattleDiagnosticFrameMetricKeys.PredictionWindow, BattleDiagnosticMetricCategory.Prediction, BattleDiagnosticMetricValueKind.Gauge, "Prediction Window", "frames", "prediction.pressure", 50, 0d, 8d),
            Metric(BattleDiagnosticFrameMetricKeys.PredictionStalled, BattleDiagnosticMetricCategory.Prediction, BattleDiagnosticMetricValueKind.Flag, "Prediction Stalled", "flag", "prediction.health", 60, 0d, 1d, BattleDiagnosticMetricAssessmentMode.WindowMaximumHigh, 0.5d, 1d),

            Metric(BattleDiagnosticFrameMetricKeys.NetworkDelayFrames, BattleDiagnosticMetricCategory.Network, BattleDiagnosticMetricValueKind.Gauge, "Buffer Delay", "frames", "network.buffer", 110, 0d, 12d),
            Metric(BattleDiagnosticFrameMetricKeys.NetworkBufferedCount, BattleDiagnosticMetricCategory.Network, BattleDiagnosticMetricValueKind.Gauge, "Buffered Frames", "frames", "network.buffer", 120, 0d, 12d),
            Metric(BattleDiagnosticFrameMetricKeys.NetworkTargetGap, BattleDiagnosticMetricCategory.Network, BattleDiagnosticMetricValueKind.Gauge, "Target Gap", "frames", "network.buffer", 130, 0d, 6d, BattleDiagnosticMetricAssessmentMode.WindowMaximumHigh, 2d, 5d),
            Metric(BattleDiagnosticFrameMetricKeys.NetworkDuplicateTotal, BattleDiagnosticMetricCategory.Network, BattleDiagnosticMetricValueKind.Counter, "Duplicate Packets", "count", "network.delivery", 140, assessmentMode: BattleDiagnosticMetricAssessmentMode.WindowDeltaHigh, warningThreshold: 1d, criticalThreshold: 5d),
            Metric(BattleDiagnosticFrameMetricKeys.NetworkLateTotal, BattleDiagnosticMetricCategory.Network, BattleDiagnosticMetricValueKind.Counter, "Late Packets", "count", "network.delivery", 150, assessmentMode: BattleDiagnosticMetricAssessmentMode.WindowDeltaHigh, warningThreshold: 1d, criticalThreshold: 5d),

            Metric(BattleDiagnosticFrameMetricKeys.RollbackActive, BattleDiagnosticMetricCategory.Rollback, BattleDiagnosticMetricValueKind.Flag, "Rollback Active", "flag", "rollback.activity", 210, 0d, 1d, BattleDiagnosticMetricAssessmentMode.WindowMaximumHigh, 0.5d, double.MaxValue),
            Metric(BattleDiagnosticFrameMetricKeys.RollbackReplayToFrame, BattleDiagnosticMetricCategory.Rollback, BattleDiagnosticMetricValueKind.Gauge, "Replay Target", "frame", "rollback.cursor", 220),
            Metric(BattleDiagnosticFrameMetricKeys.RollbackLastFrame, BattleDiagnosticMetricCategory.Rollback, BattleDiagnosticMetricValueKind.Gauge, "Last Rollback Frame", "frame", "rollback.cursor", 230),
            Metric(BattleDiagnosticFrameMetricKeys.RollbackTotal, BattleDiagnosticMetricCategory.Rollback, BattleDiagnosticMetricValueKind.Counter, "Rollbacks", "count", "rollback.health", 240, assessmentMode: BattleDiagnosticMetricAssessmentMode.WindowDeltaHigh, warningThreshold: 1d, criticalThreshold: 3d),
            Metric(BattleDiagnosticFrameMetricKeys.RollbackRestoreFailedTotal, BattleDiagnosticMetricCategory.Rollback, BattleDiagnosticMetricValueKind.Counter, "Restore Failures", "count", "rollback.health", 250, assessmentMode: BattleDiagnosticMetricAssessmentMode.WindowDeltaHigh, warningThreshold: 1d, criticalThreshold: 1d)
        };

        private static readonly BattleDiagnosticCompoundMetricRule[] CompoundRules =
        {
            new BattleDiagnosticCompoundMetricRule(
                "prediction.backlog_stall",
                "Prediction cannot drain its backlog",
                BattleDiagnosticMetricCategory.Prediction,
                BattleDiagnosticFrameMetricKeys.PredictionBacklog,
                BattleDiagnosticFrameMetricKeys.PredictionStalled),
            new BattleDiagnosticCompoundMetricRule(
                "network.late_target_gap",
                "Late delivery is widening the target gap",
                BattleDiagnosticMetricCategory.Network,
                BattleDiagnosticFrameMetricKeys.NetworkTargetGap,
                BattleDiagnosticFrameMetricKeys.NetworkLateTotal),
            new BattleDiagnosticCompoundMetricRule(
                "rollback.restore_failure",
                "Rollback recovery is failing",
                BattleDiagnosticMetricCategory.Rollback,
                BattleDiagnosticFrameMetricKeys.RollbackTotal,
                BattleDiagnosticFrameMetricKeys.RollbackRestoreFailedTotal)
        };

        public static IReadOnlyList<BattleDiagnosticMetricDescriptor> All => Descriptors;
        public static IReadOnlyList<BattleDiagnosticCompoundMetricRule> AllCompoundRules => CompoundRules;

        public static bool TryGet(string metric, out BattleDiagnosticMetricDescriptor descriptor)
        {
            for (var i = 0; i < Descriptors.Length; i++)
            {
                if (!string.Equals(Descriptors[i].Metric, metric, StringComparison.Ordinal)) continue;
                descriptor = Descriptors[i];
                return true;
            }
            descriptor = default;
            return false;
        }

        public static bool TryGet(
            string metric,
            BattleDiagnosticResolvedMetricProfile profile,
            out BattleDiagnosticMetricDescriptor descriptor)
        {
            return profile != null
                ? profile.TryGet(metric, out descriptor)
                : TryGet(metric, out descriptor);
        }

        public static IReadOnlyList<BattleDiagnosticMetricAssessment> Evaluate(
            IReadOnlyList<BattleDiagnosticMetricAggregate> aggregates,
            BattleDiagnosticResolvedMetricProfile profile = null)
        {
            var builders = new Dictionary<string, AssessmentBuilder>(StringComparer.Ordinal);
            if (aggregates != null)
            {
                for (var i = 0; i < aggregates.Count; i++)
                {
                    var aggregate = aggregates[i];
                    if (!TryGet(aggregate.Metric, profile, out var descriptor) || !descriptor.HasAssessment) continue;
                    var key = aggregate.Metric + "\n" + aggregate.Dimension;
                    if (!builders.TryGetValue(key, out var builder))
                    {
                        builder = new AssessmentBuilder(in descriptor, in aggregate);
                        builders.Add(key, builder);
                    }
                    else
                    {
                        builder.Add(in aggregate);
                    }
                }
            }

            var result = new List<BattleDiagnosticMetricAssessment>(builders.Count);
            foreach (var pair in builders)
            {
                var assessment = pair.Value.Build();
                if (assessment.IsIssue) result.Add(assessment);
            }
            result.Sort(CompareAssessments);
            return result;
        }

        public static IReadOnlyList<BattleDiagnosticCompoundMetricAssessment> EvaluateCompounds(
            IReadOnlyList<BattleDiagnosticMetricAssessment> assessments)
        {
            var result = new List<BattleDiagnosticCompoundMetricAssessment>();
            if (assessments == null || assessments.Count == 0) return result;
            for (var ruleIndex = 0; ruleIndex < CompoundRules.Length; ruleIndex++)
            {
                var rule = CompoundRules[ruleIndex];
                for (var i = 0; i < assessments.Count; i++)
                {
                    var primary = assessments[i];
                    if (!string.Equals(primary.Descriptor.Metric, rule.PrimaryMetric, StringComparison.Ordinal))
                        continue;
                    for (var j = 0; j < assessments.Count; j++)
                    {
                        var secondary = assessments[j];
                        if (!string.Equals(secondary.Descriptor.Metric, rule.SecondaryMetric, StringComparison.Ordinal) ||
                            !string.Equals(primary.Dimension, secondary.Dimension, StringComparison.Ordinal))
                            continue;
                        var severity = primary.Severity > secondary.Severity
                            ? primary.Severity
                            : secondary.Severity;
                        result.Add(new BattleDiagnosticCompoundMetricAssessment(
                            in rule,
                            primary.Dimension,
                            severity,
                            in primary,
                            in secondary));
                        break;
                    }
                }
            }
            result.Sort(CompareCompoundAssessments);
            return result;
        }

        private static BattleDiagnosticMetricDescriptor Metric(
            string metric,
            BattleDiagnosticMetricCategory category,
            BattleDiagnosticMetricValueKind valueKind,
            string displayName,
            string unit,
            string group,
            int order,
            double suggestedMinimum = double.NaN,
            double suggestedMaximum = double.NaN,
            BattleDiagnosticMetricAssessmentMode assessmentMode = BattleDiagnosticMetricAssessmentMode.None,
            double warningThreshold = double.NaN,
            double criticalThreshold = double.NaN)
        {
            return new BattleDiagnosticMetricDescriptor(
                metric,
                category,
                valueKind,
                displayName,
                unit,
                group,
                order,
                suggestedMinimum,
                suggestedMaximum,
                assessmentMode,
                warningThreshold,
                criticalThreshold);
        }

        private static int CompareAssessments(
            BattleDiagnosticMetricAssessment left,
            BattleDiagnosticMetricAssessment right)
        {
            var comparison = right.Severity.CompareTo(left.Severity);
            if (comparison != 0) return comparison;
            comparison = right.ActualValue.CompareTo(left.ActualValue);
            if (comparison != 0) return comparison;
            return left.Descriptor.Order.CompareTo(right.Descriptor.Order);
        }

        private static int CompareCompoundAssessments(
            BattleDiagnosticCompoundMetricAssessment left,
            BattleDiagnosticCompoundMetricAssessment right)
        {
            var comparison = right.Severity.CompareTo(left.Severity);
            return comparison != 0
                ? comparison
                : string.Compare(left.Rule.Id, right.Rule.Id, StringComparison.Ordinal);
        }

        private sealed class AssessmentBuilder
        {
            private readonly BattleDiagnosticMetricDescriptor _descriptor;
            private readonly string _dimension;
            private readonly int _firstFrame;
            private int _lastFrame;
            private readonly double _firstValue;
            private double _lastValue;
            private double _maximumValue;
            private int _sampleCount;

            public AssessmentBuilder(
                in BattleDiagnosticMetricDescriptor descriptor,
                in BattleDiagnosticMetricAggregate aggregate)
            {
                _descriptor = descriptor;
                _dimension = aggregate.Dimension;
                _firstFrame = aggregate.FirstFrame;
                _lastFrame = aggregate.LastFrame;
                _firstValue = aggregate.FirstValue;
                _lastValue = aggregate.LastValue;
                _maximumValue = aggregate.MaximumValue;
                _sampleCount = aggregate.SampleCount;
            }

            public void Add(in BattleDiagnosticMetricAggregate aggregate)
            {
                _lastFrame = aggregate.LastFrame;
                _lastValue = aggregate.LastValue;
                _maximumValue = Math.Max(_maximumValue, aggregate.MaximumValue);
                _sampleCount += aggregate.SampleCount;
            }

            public BattleDiagnosticMetricAssessment Build()
            {
                double actual;
                switch (_descriptor.AssessmentMode)
                {
                    case BattleDiagnosticMetricAssessmentMode.LatestHigh:
                        actual = _lastValue;
                        break;
                    case BattleDiagnosticMetricAssessmentMode.WindowDeltaHigh:
                        actual = Math.Max(0d, _lastValue - _firstValue);
                        break;
                    default:
                        actual = _maximumValue;
                        break;
                }

                var severity = actual >= _descriptor.CriticalThreshold
                    ? BattleDiagnosticMetricSeverity.Critical
                    : actual >= _descriptor.WarningThreshold
                        ? BattleDiagnosticMetricSeverity.Warning
                        : BattleDiagnosticMetricSeverity.Normal;
                return new BattleDiagnosticMetricAssessment(
                    in _descriptor,
                    _dimension,
                    severity,
                    actual,
                    _firstFrame,
                    _lastFrame,
                    _sampleCount);
            }
        }
    }
}
