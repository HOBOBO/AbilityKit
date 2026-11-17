using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Configurable parameters for the battle camera.
    /// </summary>
    public sealed class BattleCameraConfig
    {
        /// <summary>
        /// Camera offset from the target (in world space).
        /// </summary>
        public Vector3 Offset = new Vector3(0f, 15f, -12f);

        /// <summary>
        /// Exponential damping speed used while following the target position.
        /// Higher values = snappier follow. 0 means instant snap.
        /// </summary>
        public float PositionLerpSpeed = 8f;

        /// <summary>
        /// Exponential damping speed used for the look-at target.
        /// Keeping this close to the position speed prevents snapshot steps from rotating the camera abruptly.
        /// 0 means instant snap.
        /// </summary>
        public float LookAtLerpSpeed = 8f;

        /// <summary>
        /// When true the camera immediately snaps to the first tracked position.
        /// After the first frame it will use lerp.
        /// </summary>
        public bool SnapOnFirstTarget = true;

        /// <summary>
        /// When true the camera only follows horizontal motion. The Y reference
        /// is locked to the first tracked position so jumps and vertical motion
        /// do not jitter the camera. Designed for top-down MOBA view.
        /// </summary>
        public bool LockVerticalAxis = true;

        /// <summary>
        /// Optional world-space look-at offset applied on top of the target position.
        /// </summary>
        public Vector3 LookAtOffset = Vector3.zero;

        /// <summary>
        /// If true, the camera's forward vector is always toward the tracked position.
        /// If false, the initial camera orientation is preserved.
        /// </summary>
        public bool AlwaysLookAtTarget = true;

        public float MinFieldOfView = 28f;
        public float MaxFieldOfView = 65f;
        public float ScrollZoomSpeed = 4f;

        public static BattleCameraConfig Default => new BattleCameraConfig();
    }
}
