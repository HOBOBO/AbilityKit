using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct TransferShieldArgs
    {
        public readonly int InstanceId;
        public readonly int SourceActorId;
        public readonly ShieldTransferPolicy Policy;
        public readonly Fixed64 Ratio;
        public readonly MobaActionTargetRequest TargetRequest;

        public TransferShieldArgs(int instanceId, int sourceActorId, ShieldTransferPolicy policy, Fixed64 ratio, in MobaActionTargetRequest targetRequest)
        {
            InstanceId = instanceId;
            SourceActorId = sourceActorId;
            Policy = policy;
            Ratio = ratio;
            TargetRequest = targetRequest;
        }
    }
}
