namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct RefundSkillCostOnKillArgs
    {
        public readonly int SkillId;
        public RefundSkillCostOnKillArgs(int skillId) { SkillId = skillId; }
    }
}
