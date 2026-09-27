
namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct RemoveBuffArgs
    {
        public readonly int BuffId;
        public readonly int SourceActorId;
        public readonly bool RemoveAll;
        public readonly bool RemoveSlow;
        public readonly MobaExecutionEndReason Reason;
        public readonly MobaActionTargetRequest TargetRequest;

        public RemoveBuffArgs(
            int buffId,
            int sourceActorId,
            bool removeAll,
            bool removeSlow,
            MobaExecutionEndReason reason,
            in MobaActionTargetRequest targetRequest)
        {
            BuffId = buffId;
            SourceActorId = sourceActorId;
            RemoveAll = removeAll;
            RemoveSlow = removeSlow;
            Reason = reason;
            TargetRequest = targetRequest;
        }
    }
}
