namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct RemoveSkillParamModifiersArgs
    {
        public RemoveSkillParamModifiersArgs(int sourceId, in MobaActionTargetRequest targetRequest)
        {
            SourceId = sourceId;
            TargetRequest = targetRequest;
        }

        public int SourceId { get; }
        public MobaActionTargetRequest TargetRequest { get; }
    }
}
