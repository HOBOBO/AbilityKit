using System;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct ResetCooldownArgs
    {
        public readonly int SkillId;
        public readonly int SkillSlot;
        public readonly int ReductionMs;
        public readonly double? ReductionRatio;
        public readonly MobaActionTargetRequest TargetRequest;

        public ResetCooldownArgs(int skillId, int skillSlot, int reductionMs, in MobaActionTargetRequest targetRequest, double? reductionRatio = null)
        {
            if (reductionRatio.HasValue && (double.IsNaN(reductionRatio.Value) || double.IsInfinity(reductionRatio.Value) ||
                reductionRatio.Value < 0 || reductionRatio.Value > 1 || reductionMs != 0))
                throw new ArgumentOutOfRangeException(nameof(reductionRatio));
            ReductionRatio = reductionRatio;
            SkillId = skillId;
            SkillSlot = skillSlot;
            ReductionMs = Math.Max(0, reductionMs);
            TargetRequest = targetRequest;
        }
    }
}
