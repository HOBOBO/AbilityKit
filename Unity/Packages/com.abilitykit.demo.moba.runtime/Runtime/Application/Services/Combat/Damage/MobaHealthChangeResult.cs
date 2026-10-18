namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaHealthChangeKind
    {
        Damage = 0,
        Heal = 1,
        Respawn = 2,
    }

    public enum MobaHealthChangeStatus
    {
        Rejected = 0,
        Committed = 1,
    }

    [System.Flags]
    public enum MobaHealthChangeFlags
    {
        None = 0,
        Clamped = 1 << 0,
        Overkill = 1 << 1,
    }

    public readonly struct MobaHealthChangeResult : IMobaActorContextProvider, IMobaOriginContextProvider
    {
        public MobaHealthChangeResult(
            MobaHealthChangeKind kind,
            int sourceActorId,
            int targetActorId,
            int valueType,
            int reasonKind,
            int reasonParam,
            float requestedValue,
            float appliedValue,
            float oldHp,
            float targetHp,
            float targetMaxHp,
            in MobaGameplayOrigin origin)
            : this(
                kind,
                sourceActorId,
                targetActorId,
                valueType,
                reasonKind,
                reasonParam,
                requestedValue,
                appliedValue,
                oldHp,
                targetHp,
                targetMaxHp,
                in origin,
                CalculateLegacyFlags(requestedValue, appliedValue))
        {
        }

        public MobaHealthChangeResult(
            MobaHealthChangeKind kind,
            int sourceActorId,
            int targetActorId,
            int valueType,
            int reasonKind,
            int reasonParam,
            float requestedValue,
            float appliedValue,
            float oldHp,
            float targetHp,
            float targetMaxHp,
            in MobaGameplayOrigin origin,
            MobaHealthChangeFlags flags)
        {
            Kind = kind;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            ValueType = valueType;
            ReasonKind = reasonKind;
            ReasonParam = reasonParam;
            RequestedValue = requestedValue;
            AppliedValue = appliedValue;
            OldHp = oldHp;
            TargetHp = targetHp;
            TargetMaxHp = targetMaxHp;
            Origin = origin;
            Status = appliedValue > 0f ? MobaHealthChangeStatus.Committed : MobaHealthChangeStatus.Rejected;
            Flags = flags;
        }

        private static MobaHealthChangeFlags CalculateLegacyFlags(float requestedValue, float appliedValue)
        {
            if (appliedValue <= 0f || requestedValue <= 0f) return MobaHealthChangeFlags.None;
            return appliedValue < requestedValue
                ? MobaHealthChangeFlags.Clamped
                : MobaHealthChangeFlags.None;
        }

        public MobaHealthChangeKind Kind { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public int ValueType { get; }
        public int ReasonKind { get; }
        public int ReasonParam { get; }
        public float RequestedValue { get; }
        public float AppliedValue { get; }
        public float OldHp { get; }
        public float TargetHp { get; }
        public float TargetMaxHp { get; }
        public MobaGameplayOrigin Origin { get; }
        public MobaHealthChangeStatus Status { get; }
        public MobaHealthChangeFlags Flags { get; }
        public bool Succeeded => Status == MobaHealthChangeStatus.Committed;
        public bool Rejected => Status == MobaHealthChangeStatus.Rejected;
        public bool WasClamped => (Flags & MobaHealthChangeFlags.Clamped) != 0;
        public bool WasOverkill => (Flags & MobaHealthChangeFlags.Overkill) != 0;
        public bool BecameDead => Kind == MobaHealthChangeKind.Damage && OldHp > 0f && TargetHp <= 0f;

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = SourceActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = TargetActorId;
            return actorId > 0;
        }

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            origin = Origin;
            return origin.IsValid;
        }
    }
}
