using AbilityKit.Deterministic;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct JoinSharedShieldArgs
    {
        public readonly int PoolId;
        public readonly Fixed64 Weight;
        public readonly Fixed64 MaxConsumeValue;
        public readonly MobaActionTargetRequest TargetRequest;

        public JoinSharedShieldArgs(int poolId, Fixed64 weight, Fixed64 maxConsumeValue, in MobaActionTargetRequest targetRequest)
        {
            PoolId = poolId;
            Weight = weight;
            MaxConsumeValue = maxConsumeValue;
            TargetRequest = targetRequest;
        }
    }
}
