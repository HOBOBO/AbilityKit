namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public struct AddBuffArgs
    {
        public int[] BuffIds;
        public int DurationMs;
        public string DurationSkillLevelParameter;
        public MobaActionTargetRequest TargetRequest;

        public AddBuffArgs(int[] buffIds, in MobaActionTargetRequest targetRequest, int durationMs = 0, string durationSkillLevelParameter = null)
        {
            BuffIds = buffIds;
            DurationMs = durationMs;
            DurationSkillLevelParameter = durationSkillLevelParameter;
            TargetRequest = targetRequest;
        }
    }
}
