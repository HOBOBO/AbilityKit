using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct QueryTargetCollectionArgs
    {
        public QueryTargetCollectionArgs(
            in MobaActionTargetRequest targetRequest,
            in BlackboardWriteTarget resultTarget)
        {
            TargetRequest = targetRequest;
            ResultTarget = resultTarget;
        }

        public MobaActionTargetRequest TargetRequest { get; }
        public BlackboardWriteTarget ResultTarget { get; }
    }
}
