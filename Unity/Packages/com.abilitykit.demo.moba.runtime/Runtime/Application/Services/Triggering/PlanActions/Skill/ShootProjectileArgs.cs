using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;

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
        public readonly bool UseAimDistance;
        public readonly BlackboardWriteTarget ResultTarget;
        public readonly BlackboardWriteTarget ResultCountTarget;
        /// <summary>
        /// 发射前按配置计算数值，后续效果读取本次施法黑板中的结果。
        /// </summary>
        public readonly MobaEffectMagnitudeSpec LaunchMagnitude;

        public ShootProjectileArgs(
            int launcherId,
            int projectileId,
            int continuousProcessId,
            in MobaActionTargetRequest targetRequest,
            bool trackTarget,
            BlackboardWriteTarget resultTarget = default,
            BlackboardWriteTarget resultCountTarget = default, bool useAimDistance = true,
            MobaEffectMagnitudeSpec launchMagnitude = default)
        {
            LauncherId = launcherId;
            ProjectileId = projectileId;
            ContinuousProcessId = continuousProcessId;
            TargetRequest = targetRequest;
            TrackTarget = trackTarget; UseAimDistance = useAimDistance;
            ResultTarget = resultTarget;
            ResultCountTarget = resultCountTarget;
            LaunchMagnitude = launchMagnitude;
        }

        public static ShootProjectileArgs Default => new ShootProjectileArgs(0, 0, 0, default, false);
    }
}
