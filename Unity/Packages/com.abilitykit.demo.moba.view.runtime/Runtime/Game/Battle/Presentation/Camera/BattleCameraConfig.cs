using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 战斗相机的可配置参数。
    /// </summary>
    public sealed class BattleCameraConfig
    {
        /// <summary>
        /// 相对目标的相机偏移，使用世界空间坐标。
        /// </summary>
        public Vector3 Offset = new Vector3(0f, 15f, -12f);

        /// <summary>
        /// 跟随目标位置的指数阻尼速度。值越大跟随越快，0 表示立即对齐。
        /// </summary>
        public float PositionLerpSpeed = 8f;

        /// <summary>
        /// 注视目标的指数阻尼速度。与位置跟随速度保持接近，可避免快照跳变导致相机突然旋转；0 表示立即对齐。
        /// </summary>
        public float LookAtLerpSpeed = 8f;

        /// <summary>
        /// 为 true 时，相机立即对齐首次跟踪位置，后续帧使用线性插值。
        /// </summary>
        public bool SnapOnFirstTarget = true;

        /// <summary>
        /// 为 true 时，相机仅跟随水平移动。Y 轴基准锁定首次跟踪位置，避免跳跃和垂直运动造成抖动，适用于俯视 MOBA 视角。
        /// </summary>
        public bool LockVerticalAxis = true;

        /// <summary>
        /// 叠加在目标位置上的可选世界空间注视偏移。
        /// </summary>
        public Vector3 LookAtOffset = Vector3.zero;

        /// <summary>
        /// 为 true 时，相机前向始终指向跟踪位置；为 false 时，保留初始朝向。
        /// </summary>
        public bool AlwaysLookAtTarget = true;

        public float MinFieldOfView = 28f;
        public float MaxFieldOfView = 65f;
        public float ScrollZoomSpeed = 4f;

        public static BattleCameraConfig Default => new BattleCameraConfig();
    }
}
