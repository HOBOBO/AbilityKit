using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewPositionApplier
    {
        private readonly BattleViewHandleStore _handles;
        private readonly BattleViewAttachedVfxController _attachedVfx;

        /// <summary>
        /// Frame-rate independent smoothing frequency in Hz. Effective blend
        /// factor per frame is 1 - exp(-deltaTime * SmoothingHz). Set to 0
        /// or negative to disable smoothing and write the target directly.
        /// </summary>
        public float SmoothingHz = 12f;

        /// <summary>
        /// Maximum visual turn rate in degrees per second. The simulation owns the
        /// authoritative facing; the view turns toward it progressively for a
        /// readable MOBA-style locomotion pose.
        /// </summary>
        public float TurnSpeedDegreesPerSecond = 540f;

        public BattleViewPositionApplier(BattleViewHandleStore handles, BattleViewAttachedVfxController attachedVfx)
        {
            _handles = handles;
            _attachedVfx = attachedVfx;
        }

        public void ApplyPendingPositions(float deltaTime = 0f)
        {
            _handles.ForEach((_, handle) =>
            {
                if (handle == null || handle.Destroyed) return;
                if (handle.GameObject == null) return;
                if (!handle.HasPendingPos) return;

                var pos = handle.PendingPos;
                ApplyPosition(handle, in pos, deltaTime);
            });
        }

        /// <summary>
        /// Test-only entry point that applies a single position to a single handle
        /// with an explicit deltaTime. Used by NUnit tests in this assembly to
        /// exercise smoothing and extrapolation paths without an ECS world.
        /// </summary>
        internal void ApplyPendingPositionsForTest(BattleViewHandle handle, Vector3 pos, float deltaTime)
        {
            if (handle == null || handle.GameObject == null) return;
            ApplyPosition(handle, in pos, deltaTime);
        }

        internal void ApplyPendingPositionsForTest(
            BattleViewHandle handle,
            Vector3 pos,
            Vector3 forward,
            float deltaTime)
        {
            if (handle == null || handle.GameObject == null) return;

            var horizontalForward = new Vector3(forward.x, 0f, forward.z);
            if (horizontalForward.sqrMagnitude > 0.0001f)
            {
                handle.PendingForward = horizontalForward.normalized;
                handle.HasPendingForward = true;
            }

            ApplyPosition(handle, in pos, deltaTime);
        }

        public void ApplyInterpolatedPositions(double renderTime, float deltaTime)
        {
            _handles.ForEach((_, handle) =>
            {
                if (handle == null || handle.Destroyed) return;
                if (handle.GameObject == null) return;

                if (!handle.Pos.TryEvaluate(renderTime, out var pos))
                {
                    if (handle.HasPendingPos) pos = handle.PendingPos;
                    else return;
                }

                ApplyPosition(handle, in pos, deltaTime);
            });
        }

        private void ApplyPosition(BattleViewHandle handle, in Vector3 pos, float deltaTime)
        {
            var go = handle.GameObject;
            var current = handle.HasLastDisplayed ? handle.LastDisplayedPos : go.transform.position;
            var next = Smooth(current, pos, deltaTime);

            go.transform.position = next;
            if (handle.HasPendingForward)
            {
                var targetRotation = Quaternion.LookRotation(handle.PendingForward, Vector3.up);
                var turnStep = TurnSpeedDegreesPerSecond > 0f && deltaTime > 0f
                    ? TurnSpeedDegreesPerSecond * deltaTime
                    : float.PositiveInfinity;
                go.transform.rotation = Quaternion.RotateTowards(go.transform.rotation, targetRotation, turnStep);
            }
            handle.LastDisplayedPos = next;
            handle.HasLastDisplayed = true;

            _attachedVfx?.SyncPosition(handle, in next);
        }

        private Vector3 Smooth(in Vector3 current, in Vector3 target, float deltaTime)
        {
            // No smoothing on the first frame or when disabled.
            if (SmoothingHz <= 0f || deltaTime <= 0f)
            {
                return target;
            }

            // Frame-rate independent exponential smoothing.
            var alpha = 1f - Mathf.Exp(-deltaTime * SmoothingHz);
            if (alpha > 1f) alpha = 1f;
            return Vector3.LerpUnclamped(current, target, alpha);
        }
    }
}
