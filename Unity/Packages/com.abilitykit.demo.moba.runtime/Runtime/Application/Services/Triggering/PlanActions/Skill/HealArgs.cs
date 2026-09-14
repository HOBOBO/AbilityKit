using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct HealArgs
    {
        public readonly float Amount;
        public readonly DamageType HealType;
        public readonly int ReasonKind;
        public readonly int ReasonParam;
        public readonly MobaActionTargetRequest TargetRequest;
        public readonly MobaEffectMagnitudeSpec Magnitude;

        public HealArgs(float amount, DamageType healType, int reasonKind, int reasonParam, in MobaActionTargetRequest targetRequest, MobaEffectMagnitudeSpec magnitude = default)
        {
            Amount = amount;
            HealType = healType;
            ReasonKind = reasonKind;
            ReasonParam = reasonParam;
            TargetRequest = targetRequest;
            Magnitude = magnitude;
        }
    }
}
