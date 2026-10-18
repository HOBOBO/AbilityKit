using AbilityKit.Game.Battle.Entity;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Follows the local player's entity in the world using smooth lerp.
    /// Works entirely via <see cref="IBattleEntityQuery"/> so it never directly touches ECS entities.
    /// The camera always reads the authoritative snapshot position — the same position that
    /// the HUD skill aim preview uses — so there is no visual pop when the player starts
    /// casting a skill while moving.
    ///
    /// When the tracked entity has not yet spawned, the camera continues to follow
    /// the last known valid position so that the follow does not stall while waiting
    /// for the first snapshot to arrive.
    /// </summary>
    public sealed class BattleViewCameraController
    {
        private readonly BattleCameraConfig _config;
        private readonly bool _allowMainCameraFallback;

        private Camera _camera;
        private int _trackedActorId;
        private bool _hasTrackedActor;
        private Vector3 _currentPosition;
        private Vector3 _lookAtTarget;

        // True only on the first frame after TrackActor() — when set, the next Tick
        // snaps the camera to the tracked target instead of lerping.
        private bool _snapPending;

        // Y axis reference for horizontal follow. When LockVerticalAxis is enabled,
        // the camera height is initialised to the first tracked position (or the
        // current camera position if none has been resolved yet), and subsequent
        // follow frames ignore the target's Y coordinate so vertical motion
        // (e.g. jumping) does not jitter the camera.
        private float _lockedTargetY;
        private bool _hasLockedTargetY;

        // Last known valid target position. Used as a fallback when the entity has
        // not yet spawned so that the camera does not stall waiting for the first snapshot.
        private Vector3 _lastValidTargetPos;
        private bool _hasLastValidTargetPos;

        public BattleViewCameraController(BattleCameraConfig config = null,
            bool allowMainCameraFallback = true)
        {
            _config = config ?? BattleCameraConfig.Default;
            _allowMainCameraFallback = allowMainCameraFallback;
        }

        /// <summary>
        /// The <see cref="Camera"/> being controlled. Null until <see cref="SetCamera"/> is called.
        /// </summary>
        public Camera Camera => _camera;

        /// <summary>
        /// The actor being tracked. 0 means no actor is tracked.
        /// </summary>
        public int TrackedActorId => _trackedActorId;

        /// <summary>
        /// Attach a camera. If null, the controller uses <see cref="Camera.main"/>.
        /// </summary>
        public void SetCamera(Camera camera)
        {
            _camera = camera ?? (_allowMainCameraFallback ? Camera.main : null);
            if (_camera != null && !_hasTrackedActor)
            {
                _currentPosition = _camera.transform.position;
                _lookAtTarget = _currentPosition + _camera.transform.forward;
            }
        }

        /// <summary>
        /// Begin tracking a specific actor. Pass 0 to stop tracking.
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
                // Keep _lastValidTargetPos so that if the entity has not spawned yet,
                // the camera can still follow the last known position.
            }
        }

        /// <summary>
        /// Update the camera to follow the currently tracked actor.
        /// Call once per frame from <see cref="BattleViewFeature.Tick"/> or any other per-frame hook.
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
                // Entity not yet spawned — fall back to the last known position so the
                // camera does not stall while waiting for the first snapshot to arrive.
                targetPos = _lastValidTargetPos;
            }
            else
            {
                // No current position and no fallback — nothing to follow.
                return;
            }

            // Capture Y reference once per track target so vertical motion (jumps,
            // fall-off ledges, knock-backs) does not jitter the camera.
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
        /// Stops tracking and clears the camera reference.
        /// </summary>
        public void Reset()
        {
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
