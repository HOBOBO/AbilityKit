using AbilityKit.Demo.Moba;

namespace AbilityKit.Demo.Moba.Triggering.DamageActions
{
    public sealed class DamageActionSpec
    {
        public enum DamageTargetMode
        {
            Explicit = 0,
            QueryTemplate = 1,
            Source = 2,
            Target = 3,
            Self = 4,
            PayloadAttacker = 5,
            PayloadTarget = 6,
        }

        public float Value;
        public float Rate;

        public DamageType DamageType;
        public CritType CritType;
        public DamageReasonKind ReasonKind;
        public int ReasonParam;

        public int FormulaKind;

        public string AttackerKey;
        public string TargetKey;

        public DamageTargetMode TargetMode;
        public int QueryTemplateId;
        public string AimPosKey;

        public bool Log;

        public bool UseProjectileHitDecay;
    }
}
