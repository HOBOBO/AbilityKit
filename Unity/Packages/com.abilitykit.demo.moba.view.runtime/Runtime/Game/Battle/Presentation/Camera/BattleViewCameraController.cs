using AbilityKit.Game.Battle.Entity;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    ///
    /// 通过平滑线性插值跟随世界中的本地玩家实体。完全通过 <see cref="IBattleEntityQuery"/> 工作，不直接访问 ECS 实体。
    /// 相机始终读取与 HUD 技能瞄准预览一致的权威快照位置，避免玩家移动中开始施法时画面跳变。
    ///
    /// 跟踪实体尚未生成时，相机继续跟随最后已知有效位置，避免等待首份快照期间跟随停滞。
    ///
    /// </summary>
    public sealed class BattleViewCameraController
    {
        private readonly BattleCameraConfig _config;
        private readonly bool _allowMainCameraFallback;

        private Camera _camera;
        private bool _addedDepthTexture;
        private int _trackedActorId;
        private bool _hasTrackedActor;
        private Vector3 _currentPosition;
        private Vector3 _lookAtTarget;

        /*
         * 仅在 TrackActor() 后首帧为 true，此时下次 Tick 立即将相机对齐跟踪目标。
         */
        private bool _snapPending;

        /*
         * 水平跟随的 Y 轴基准。启用 LockVerticalAxis 时，高度初始化为首次跟踪位置，尚未解析到位置则用当前相机位置；后续跟随忽略目标 Y 坐标，避免跳跃等垂直运动造成抖动。
         */
        private float _lockedTargetY;
        private bool _hasLockedTargetY;

        /*
         * 最后已知有效目标位置。实体尚未生成时作为回退，避免相机等待首份快照时停滞。
         */
        private Vector3 _lastValidTargetPos;
        private bool _hasLastValidTargetPos;

        public BattleViewCameraController(BattleCameraConfig config = null,
            bool allowMainCameraFallback = true)
        {
            _config = config ?? BattleCameraConfig.Default;
            _allowMainCameraFallback = allowMainCameraFallback;
        }

        /// <summary>
        /// 受控 <see cref="Camera"/>，调用 <see cref="SetCamera"/> 前为 null。
        /// </summary>
        public Camera Camera => _camera;

        /// <summary>
        /// 正在跟踪的角色，0 表示未跟踪角色。
        /// </summary>
        public int TrackedActorId => _trackedActorId;

        public float MinFieldOfView => _config.MinFieldOfView;
        public float MaxFieldOfView => _config.MaxFieldOfView;

        /// <summary>
        /// 附加相机。传入 null 时使用 <see cref="Camera.main"/>。
        /// </summary>
        public void SetCamera(Camera camera)
        {
            var nextCamera = camera ?? (_allowMainCameraFallback ? Camera.main : null);
            if (_camera != nextCamera)
            {
                ReleaseDepthTexture();
                _camera = nextCamera;
                if (_camera != null)
                {
                    /*
                     * 关联的战斗材质通过 _CameraDepthTexture 计算不透明度及相交效果，为每个视图提供该输入。
                     */
                    _addedDepthTexture = (_camera.depthTextureMode & DepthTextureMode.Depth) == 0;
                    _camera.depthTextureMode |= DepthTextureMode.Depth;
                }
            }
            if (_camera != null && !_hasTrackedActor)
            {
                _currentPosition = _camera.transform.position;
                _lookAtTarget = _currentPosition + _camera.transform.forward;
            }
        }

        /// <summary>
        /// 开始跟踪指定角色，传入 0 停止跟踪。
        /// </summary>
        public void TrackActor(int actorId)
        {
            if (_trackedActorId == actorId) return;
            _trackedActorId = actorId;
            _hasTrackedActor = actorId > 0;

            if (_hasTrackedActor)
            {
                _snapPending = _config.SnapOnFirstTarget;
                _hasLockedTargetY = false;
                /*
                 * 保留 _lastValidTargetPos，使实体尚未生成时，相机仍可跟随最后已知位置。
                 */
            }
        }

        /// <summary>
        /// 更新相机以跟随当前跟踪角色。通过 <see cref="BattleViewFeature.Tick"/> 或其他逐帧入口，每帧调用一次。
        /// </summary>
        public void Tick(IBattleEntityQuery query, float deltaTime)
        {
            if (_camera == null && _allowMainCameraFallback)
            {
                SetCamera(null);
            }

            if (_camera == null || !_hasTrackedActor || _trackedActorId <= 0)
                return;

            Vector3 targetPos;
            bool hasCurrent = TryGetTrackedActorPosition(query, out targetPos);

            if (hasCurrent)
            {
                _lastValidTargetPos = targetPos;
                _hasLastValidTargetPos = true;
            }
            else if (_hasLastValidTargetPos)
            {
                /*
                 * 实体尚未生成，回退至最后已知位置，避免等待首份快照时相机停滞。
                 */
                targetPos = _lastValidTargetPos;
            }
            else
            {
                /*
                 * 当前位置及回退位置均不存在，没有可跟随目标。
                 */
                return;
            }

            /*
             * 每个跟踪目标只捕获一次 Y 轴基准，避免跳跃、跌落及击退等垂直运动造成相机抖动。
             */
            if (_config.LockVerticalAxis && !_hasLockedTargetY)
            {
                _lockedTargetY = targetPos.y;
                _hasLockedTargetY = true;
            }

            var followPos = targetPos;
            if (_config.LockVerticalAxis)
            {
                followPos.y = _lockedTargetY;
            }

            var worldTarget = followPos + _config.Offset;
            var desiredLookAtTarget = followPos + _config.LookAtOffset;
            var snapThisFrame = _snapPending;

            if (snapThisFrame)
            {
                _currentPosition = worldTarget;
                _lookAtTarget = desiredLookAtTarget;
                _snapPending = false;
            }
            else
            {
                var positionAlpha = CalculateDampingAlpha(_config.PositionLerpSpeed, deltaTime);
                _currentPosition = Vector3.LerpUnclamped(_currentPosition, worldTarget, positionAlpha);

                var lookAtAlpha = CalculateDampingAlpha(_config.LookAtLerpSpeed, deltaTime);
                _lookAtTarget = Vector3.LerpUnclamped(_lookAtTarget, desiredLookAtTarget, lookAtAlpha);
            }

            _camera.transform.position = _currentPosition;
            if (_config.AlwaysLookAtTarget &&
                (_lookAtTarget - _currentPosition).sqrMagnitude > 0.000001f)
            {
                _camera.transform.LookAt(_lookAtTarget);
            }
        }

        /// <summary>
        /// 停止跟踪并清除相机引用。
        /// </summary>
        public void Reset()
        {
            ReleaseDepthTexture();
            _trackedActorId = 0;
            _hasTrackedActor = false;
            _snapPending = false;
            _hasLockedTargetY = false;
            _lockedTargetY = 0f;
            _hasLastValidTargetPos = false;
            _lastValidTargetPos = Vector3.zero;
            _camera = null;
            _currentPosition = Vector3.zero;
            _lookAtTarget = Vector3.zero;
        }

        public void ApplyScrollZoom(float scrollDelta)
        {
            if (_camera == null || _camera.orthographic || Mathf.Approximately(scrollDelta, 0f)) return;

            var min = Mathf.Clamp(_config.MinFieldOfView, 1f, 179f);
            var max = Mathf.Clamp(_config.MaxFieldOfView, min, 179f);
            _camera.fieldOfView = Mathf.Clamp(
                _camera.fieldOfView - scrollDelta * _config.ScrollZoomSpeed,
                min,
                max);
        }

        private void ReleaseDepthTexture()
        {
            if (_addedDepthTexture && _camera != null)
                _camera.depthTextureMode &= ~DepthTextureMode.Depth;
            _addedDepthTexture = false;
        }

        private static float CalculateDampingAlpha(float speed, float deltaTime)
        {
            if (speed <= 0f) return 1f;
            if (deltaTime <= 0f) return 0f;
            return 1f - Mathf.Exp(-speed * deltaTime);
        }

        private bool TryGetTrackedActorPosition(IBattleEntityQuery query, out Vector3 pos)
        {
            pos = Vector3.zero;

            var netId = new BattleNetId(_trackedActorId);
            if (query.TryGetTransform(netId, out var transform) && transform != null)
            {
                pos = transform.Position;
                return true;
            }

            return false;
        }
    }
}
