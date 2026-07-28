using System;

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
            int magnitudeType)
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

        public bool Equals(BattleDiagnosticActorAttributeModifier other)
        {
            return Scope.Equals(other.Scope) && Frame == other.Frame && ActorId == other.ActorId &&
                   AttributeId == other.AttributeId && Operation == other.Operation &&
                   Magnitude.Equals(other.Magnitude) && Priority == other.Priority &&
                   SourceId == other.SourceId && MagnitudeType == other.MagnitudeType;
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
            string name = "")
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

        public bool Equals(BattleDiagnosticActorBuff other)
        {
            return Scope.Equals(other.Scope) && Frame == other.Frame && ActorId == other.ActorId &&
                   BuffId == other.BuffId && SourceActorId == other.SourceActorId &&
                   StackCount == other.StackCount && RemainingSeconds.Equals(other.RemainingSeconds) &&
                   IntervalRemainingSeconds.Equals(other.IntervalRemainingSeconds) &&
                   SourceContextId == other.SourceContextId && RuntimeContextId == other.RuntimeContextId &&
                   RuntimeContextVersion == other.RuntimeContextVersion && SkillRuntime.Equals(other.SkillRuntime) &&
                   RootContextId == other.RootContextId && ModifierBindingCount == other.ModifierBindingCount &&
                   MaxStacks == other.MaxStacks && string.Equals(Name, other.Name, StringComparison.Ordinal);
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
            BattleDiagnosticEventPayload payload = default)
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (monotonicTimestamp < 0) throw new ArgumentOutOfRangeException(nameof(monotonicTimestamp));
            if (payloadVersion < 1) throw new ArgumentOutOfRangeException(nameof(payloadVersion));
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

            Scope = scope;
            Frame = frame;
            Sequence = sequence;
            MonotonicTimestamp = monotonicTimestamp;
            Kind = kind;
            Channel = channel;
            Outcome = outcome;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            ConfigId = configId;
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
        public int ConfigId { get; }
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
                   ConfigId == other.ConfigId && RootContextId == other.RootContextId &&
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
                hashCode = (hashCode * 397) ^ ConfigId;
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
}
