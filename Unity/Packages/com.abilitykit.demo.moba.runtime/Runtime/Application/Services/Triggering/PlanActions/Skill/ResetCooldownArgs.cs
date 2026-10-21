using System;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct ResetCooldownArgs
    {
        public readonly int SkillId;
        public readonly int SkillSlot;
        public readonly int ReductionMs;
        public readonly MobaActionTargetRequest TargetRequest;

        public ResetCooldownArgs(int skillId, int skillSlot, int reductionMs, in MobaActionTargetRequest targetRequest)
        {
            SkillId = skillId;
            SkillSlot = skillSlot;
            ReductionMs = Math.Max(0, reductionMs);
            TargetRequest = targetRequest;
        }
    }
}
