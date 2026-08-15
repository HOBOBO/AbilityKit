using AbilityKit.Game.Battle.Entity;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleHudAimPreviewState
    {
        public readonly int Slot;
        public readonly Vector3 CasterPosition;
        public readonly Vector3 AimDirection;
        public readonly float AimDistance;
        public readonly int SubmissionVersion;

        public BattleHudAimPreviewState(
            int slot,
            Vector3 casterPosition,
            Vector3 aimDirection,
            float aimDistance,
            int submissionVersion = 0)
        {
            Slot = slot;
            CasterPosition = casterPosition;
            AimDirection = aimDirection;
            AimDistance = aimDistance;
            SubmissionVersion = submissionVersion;
        }
    }

    internal sealed class BattleHudAimPreviewPositionResolver
    {
        private bool _hasLastCasterPosition;
        private Vector3 _lastCasterPosition;

        public bool TryResolve(BattleContext ctx, out BattleHudAimPreviewState state)
        {
            state = default;
            if (ctx == null) return false;

            if (!ctx.TryReadHudSkillAimPreview(out var slot, out var aimDx, out var aimDz, out var submissionVersion))
            {
                return false;
            }

            if (!TryResolveCasterPosition(ctx, out var casterPosition))
            {
                return false;
            }

            var aim = new Vector3(aimDx, 0f, aimDz);
            var distance = aim.magnitude;
            var direction = distance > 0.001f ? aim / distance : Vector3.forward;
            state = new BattleHudAimPreviewState(slot, casterPosition, direction, distance, submissionVersion);
            return true;
        }

        private bool TryResolveCasterPosition(BattleContext ctx, out Vector3 position)
        {
            position = default;
            if (ctx == null || !ctx.TryResolveLocalActorWorldPos(out position))
            {
                return TryUseLastCasterPosition(out position);
            }

            _lastCasterPosition = position;
            _hasLastCasterPosition = true;
            return true;
        }

        private bool TryUseLastCasterPosition(out Vector3 position)
        {
            position = _lastCasterPosition;
            return _hasLastCasterPosition;
        }
    }
}
