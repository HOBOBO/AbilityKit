namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct LeaveSharedShieldArgs
    {
        public readonly int PoolId;
        public readonly MobaActionTargetRequest TargetRequest;
        public LeaveSharedShieldArgs(int poolId, in MobaActionTargetRequest targetRequest) { PoolId = poolId; TargetRequest = targetRequest; }
    }
}
