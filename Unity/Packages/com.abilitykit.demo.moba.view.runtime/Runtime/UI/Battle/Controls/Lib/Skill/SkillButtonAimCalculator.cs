using UnityEngine;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    internal static class SkillButtonAimCalculator
    {
        public static Vector2 CalculateAim(
            RectTransform uiRootRect,
            RectTransform buttonRect,
            Camera uiCamera,
            SkillButtonConfig config,
            Vector2 screenPos)
        {
            if (!TryGetLocalDrag(uiRootRect, buttonRect, uiCamera, screenPos, out var delta)) return Vector2.zero;

            if (config.AimMode == SkillAimMode.Point)
            {
                var radius = Mathf.Max(1f, config.AimMaxRadius);
                var dist = delta.magnitude;
                if (dist <= 0.0001f) return Vector2.zero;

                var clamped = dist > radius ? delta * (radius / dist) : delta;
                return clamped / radius;
            }

            var directionDist = delta.magnitude;
            if (directionDist <= 0.0001f) return Vector2.zero;
            return delta / directionDist;
        }

        public static bool TryGetIndicatorPoints(
            RectTransform uiRootRect,
            RectTransform buttonRect,
            Camera uiCamera,
            SkillButtonConfig config,
            Vector2 screenPos,
            out Vector2 from,
            out Vector2 to,
            out float radius)
        {
            from = Vector2.zero;
            to = Vector2.zero;
            radius = Mathf.Max(1f, config.AimMaxRadius);

            if (uiRootRect == null || buttonRect == null) return false;

            from = ScreenToLocalInRect(
                uiRootRect,
                RectTransformUtility.WorldToScreenPoint(uiCamera, buttonRect.position),
                uiCamera);
            to = ScreenToLocalInRect(uiRootRect, screenPos, uiCamera);

            if (config.AimMode == SkillAimMode.Point)
            {
                var delta = to - from;
                var dist = delta.magnitude;
                if (dist > radius)
                {
                    to = from + delta * (radius / dist);
                }
            }

            return true;
        }

        private static bool TryGetLocalDrag(
            RectTransform uiRootRect,
            RectTransform buttonRect,
            Camera uiCamera,
            Vector2 screenPos,
            out Vector2 delta)
        {
            delta = Vector2.zero;
            if (uiRootRect == null || buttonRect == null) return false;

            var from = ScreenToLocalInRect(
                uiRootRect,
                RectTransformUtility.WorldToScreenPoint(uiCamera, buttonRect.position),
                uiCamera);
            var to = ScreenToLocalInRect(uiRootRect, screenPos, uiCamera);
            delta = to - from;
            return true;
        }

        private static Vector2 ScreenToLocalInRect(RectTransform rect, Vector2 screenPos, Camera uiCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, uiCamera, out var local);
            return local;
        }
    }
}
