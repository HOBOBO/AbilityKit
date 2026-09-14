using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// shoot_projectile Action 的强类型参数。
    /// </summary>
    public readonly struct ShootProjectileArgs
    {
        /// <summary>
        /// 发射器 ID。
        /// </summary>
        public readonly int LauncherId;

        /// <summary>
        /// 弹体 ID。
        /// </summary>
        public readonly int ProjectileId;

        public readonly int ContinuousProcessId;
        public readonly MobaActionTargetRequest TargetRequest;
        public readonly bool TrackTarget;
        public readonly BlackboardWriteTarget ResultTarget;
        public readonly BlackboardWriteTarget ResultCountTarget;

        public ShootProjectileArgs(
            int launcherId,
            int projectileId,
            int continuousProcessId,
            in MobaActionTargetRequest targetRequest,
            bool trackTarget,
            BlackboardWriteTarget resultTarget = default,
            BlackboardWriteTarget resultCountTarget = default)
        {
            LauncherId = launcherId;
            ProjectileId = projectileId;
            ContinuousProcessId = continuousProcessId;
            TargetRequest = targetRequest;
            TrackTarget = trackTarget;
            ResultTarget = resultTarget;
            ResultCountTarget = resultCountTarget;
        }

        public static ShootProjectileArgs Default => new ShootProjectileArgs(0, 0, 0, default, false);
    }
}
