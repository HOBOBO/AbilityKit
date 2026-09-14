using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct AddShieldArgs
    {
        public readonly int ShieldId;
        public readonly float Value;
        public readonly float AbsorbRatio;
        public readonly int Priority;
        public readonly int DamageTypeMask;
        public readonly int DurationFrames;
        public readonly int DurationMs;
        public readonly ShieldStackingPolicy StackingPolicy;
        public readonly ShieldConsumePolicy ConsumePolicy;
        public readonly MobaActionTargetRequest TargetRequest;
        public readonly MobaEffectMagnitudeSpec Magnitude;
        public readonly BlackboardWriteTarget ResultTarget;
        public readonly BlackboardWriteTarget ResultCountTarget;

        public AddShieldArgs(
            int shieldId,
            float value,
            float absorbRatio,
            int priority,
            int damageTypeMask,
            int durationFrames,
            int durationMs,
            ShieldStackingPolicy stackingPolicy,
            ShieldConsumePolicy consumePolicy,
            in MobaActionTargetRequest targetRequest,
            MobaEffectMagnitudeSpec magnitude = default,
            BlackboardWriteTarget resultTarget = default,
            BlackboardWriteTarget resultCountTarget = default)
        {
            ShieldId = shieldId;
            Value = value;
            AbsorbRatio = absorbRatio;
            Priority = priority;
            DamageTypeMask = damageTypeMask;
            DurationFrames = durationFrames;
            DurationMs = durationMs;
            StackingPolicy = stackingPolicy;
            ConsumePolicy = consumePolicy;
            TargetRequest = targetRequest;
            Magnitude = magnitude;
            ResultTarget = resultTarget;
            ResultCountTarget = resultCountTarget;
        }
    }
}
