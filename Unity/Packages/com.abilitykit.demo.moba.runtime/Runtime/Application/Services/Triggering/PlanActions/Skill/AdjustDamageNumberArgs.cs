namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public enum DamageNumberSlot
    {
        BaseDamage = 0,
        DamageRate = 1,
        FlatBonus = 2,
        FinalDamage = 3,
    }

    public readonly struct AdjustDamageNumberArgs
    {
        public readonly DamageNumberSlot NumberSlot;
        public readonly CombatNumberModifierOp Op;
        public readonly float Value;
        public readonly int SourceId;
        public readonly DamageReasonKind ReasonKind;
        public readonly int ReasonParam;
        public readonly bool RequireSkillRuntime;
        public readonly bool SkipFirstHit;
        public readonly float RepeatTargetDecayFactor;
        public readonly int TargetHitCountKeyBase;
        public readonly float TargetMissingHpRatioCoefficient;

        public AdjustDamageNumberArgs(
            DamageNumberSlot numberSlot,
            CombatNumberModifierOp op,
            float value,
            int sourceId,
            DamageReasonKind reasonKind,
            int reasonParam,
            bool requireSkillRuntime,
            bool skipFirstHit,
            float repeatTargetDecayFactor,
            int targetHitCountKeyBase,
            float targetMissingHpRatioCoefficient)
        {
            NumberSlot = numberSlot;
            Op = op;
            Value = value;
            SourceId = sourceId;
            ReasonKind = reasonKind;
            ReasonParam = reasonParam;
            RequireSkillRuntime = requireSkillRuntime;
            SkipFirstHit = skipFirstHit;
            RepeatTargetDecayFactor = repeatTargetDecayFactor;
            TargetHitCountKeyBase = targetHitCountKeyBase;
            TargetMissingHpRatioCoefficient = targetMissingHpRatioCoefficient;
        }
    }
}
