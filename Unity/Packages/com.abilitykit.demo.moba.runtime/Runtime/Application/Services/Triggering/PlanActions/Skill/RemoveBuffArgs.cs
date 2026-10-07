
namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct RemoveBuffArgs
    {
        public readonly int BuffId;
        public readonly int SourceActorId;
        public readonly bool RemoveAll;
        public readonly int TagId;
        public readonly MobaExecutionEndReason Reason;
        public readonly MobaActionTargetRequest TargetRequest;

        public RemoveBuffArgs(
            int buffId,
            int sourceActorId,
            bool removeAll,
            int tagId,
            MobaExecutionEndReason reason,
            in MobaActionTargetRequest targetRequest)
        {
            BuffId = buffId;
            SourceActorId = sourceActorId;
            RemoveAll = removeAll;
            TagId = tagId;
            Reason = reason;
            TargetRequest = targetRequest;
        }
    }
}
