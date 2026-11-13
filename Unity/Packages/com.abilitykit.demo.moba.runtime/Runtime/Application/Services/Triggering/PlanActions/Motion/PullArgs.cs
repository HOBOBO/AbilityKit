using AbilityKit.Demo.Moba.Services.Motion;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// pull Action 的强类型参数。
    /// </summary>
    public readonly struct PullArgs
    {
        /// <summary>
        /// 拉取速度，单位为距离/秒。
        /// </summary>
        public readonly float Speed;
        /// <summary>Zero disables scaling; otherwise samples caster-target XZ distance at effect commit.</summary>
        public readonly float DistanceScaleReference;
        public readonly float DistanceScaleNear;
        public readonly float DistanceScaleFar;

        /// <summary>
        /// 拉取持续时间，单位毫秒。
        /// </summary>
        public readonly float DurationMs;

        /// <summary>
        /// 拉取方向模式：0=从目标拉向技能释放者，1=从目标拉到指定距离，2=垂直击飞并回落。
        /// </summary>
        public readonly int DirectionMode;

        /// <summary>
        /// 目标距离，仅 DirectionMode=1 时使用。
        /// </summary>
        public readonly float TargetDistance;

        /// <summary>
        /// 运动优先级。
        /// </summary>
        public readonly int Priority;

        /// <summary>
        /// 位移组 ID；0 表示使用行为默认组。
        /// </summary>
        public readonly int MotionGroupId;

        public readonly MobaMotionContinuousSettings Continuous;

        public readonly MobaActionTargetRequest TargetRequest;
        public readonly MobaMotionOriginSource OriginSource;

        public PullArgs(float speed, float durationMs, int directionMode = 0, float targetDistance = 0f, int priority = 12, int motionGroupId = 0, MobaMotionContinuousSettings continuous = default, MobaActionTargetRequest targetRequest = default, float distanceScaleReference = 0f, float distanceScaleNear = 1f, float distanceScaleFar = 1f, MobaMotionOriginSource originSource = MobaMotionOriginSource.CasterPosition)
        {
            if (!System.Enum.IsDefined(typeof(MobaMotionOriginSource), originSource))
                throw new System.ArgumentOutOfRangeException(nameof(originSource));
            OriginSource = originSource;
            if (!IsFinite(distanceScaleReference) || distanceScaleReference < 0f ||
                !IsFinite(distanceScaleNear) || distanceScaleNear < 0f ||
                !IsFinite(distanceScaleFar) || distanceScaleFar < 0f ||
                (distanceScaleReference == 0f && (distanceScaleNear != 1f || distanceScaleFar != 1f)))
                throw new System.ArgumentOutOfRangeException(nameof(distanceScaleReference), "Distance scaling requires a positive finite reference and finite nonnegative endpoints.");
            DistanceScaleReference = distanceScaleReference;
            DistanceScaleNear = distanceScaleNear;
            DistanceScaleFar = distanceScaleFar;
            Speed = speed;
            DurationMs = durationMs;
            DirectionMode = directionMode;
            TargetDistance = targetDistance;
            Priority = priority;
            MotionGroupId = motionGroupId;
            Continuous = continuous;
            TargetRequest = targetRequest;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public float ResolveDistanceScale(float distance) => DistanceScaleReference == 0f ? 1f :
            DistanceScaleNear + (DistanceScaleFar - DistanceScaleNear) * System.Math.Max(0f, System.Math.Min(1f, distance / DistanceScaleReference));

        public static PullArgs Default => new PullArgs(0f, 0f, 0, 0f, 12, 0, MobaMotionContinuousSettings.Empty, MobaActionTargetRequest.ContextTarget());
    }
}
