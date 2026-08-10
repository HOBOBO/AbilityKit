namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaHealthChangeKind
    {
        Damage = 0,
        Heal = 1,
        Respawn = 2,
    }

    public readonly struct MobaHealthChangeResult
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
        public bool Succeeded => AppliedValue > 0f;
        public bool BecameDead => Kind == MobaHealthChangeKind.Damage && OldHp > 0f && TargetHp <= 0f;
    }
}
