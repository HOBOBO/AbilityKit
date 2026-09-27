using System;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Platform-neutral presentation contract for a damage or healing event.
    /// </summary>
    public readonly struct DamageEventData
    {
        public DamageEventKind Kind { get; }
        public int AttackerId { get; }
        public int TargetId { get; }
        public int DamageType { get; }
        public float Value { get; }
        public int ReasonKind { get; }
        public int ReasonParam { get; }
        public float TargetHp { get; }
        public float TargetMaxHp { get; }
        public bool IsKill { get; }

        public bool IsHeal => Kind == DamageEventKind.Heal;

        // Compatibility aliases retained for existing platform-neutral consumers.
        public int SourceId => ReasonParam;
        public int DamageValue => (int)MathF.Round(Value);
        public int TargetHpAfter => (int)MathF.Round(TargetHp);

        public DamageEventData(
            DamageEventKind kind,
            int attackerId,
            int targetId,
            int damageType,
            float value,
            int reasonKind,
            int reasonParam,
            float targetHp,
            float targetMaxHp,
            bool isKill)
        {
            Kind = kind;
            AttackerId = attackerId;
            TargetId = targetId;
            DamageType = damageType;
            Value = value;
            ReasonKind = reasonKind;
            ReasonParam = reasonParam;
            TargetHp = targetHp;
            TargetMaxHp = targetMaxHp;
            IsKill = isKill;
        }

        public DamageEventData(
            int attackerId,
            int targetId,
            int sourceId,
            int damageType,
            int damageValue,
            int targetHpAfter,
            bool isKill)
            : this(
                DamageEventKind.Damage,
                attackerId,
                targetId,
                damageType,
                damageValue,
                reasonKind: 0,
                reasonParam: sourceId,
                targetHp: targetHpAfter,
                targetMaxHp: 0f,
                isKill)
        {
        }
    }

    public enum DamageEventKind
    {
        None = 0,
        Damage = 1,
        Heal = 2,
    }
}

