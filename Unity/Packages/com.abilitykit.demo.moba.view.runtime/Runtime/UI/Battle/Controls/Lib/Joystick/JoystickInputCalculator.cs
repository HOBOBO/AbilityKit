using UnityEngine;

namespace AbilityKit.Game.Battle.View.Lib.Joystick
{
    internal static class JoystickInputCalculator
    {
        public static JoystickOutput Calculate(
            Vector2 centerLocal,
            Vector2 currentLocal,
            JoystickConfig config,
            out Vector2 clamped)
        {
            var delta = currentLocal - centerLocal;
            var dist = delta.magnitude;
            var dead = Mathf.Max(0f, config.DeadZone);
            var radius = Mathf.Max(1f, config.Radius);

            float magnitude;
            if (dist <= dead)
            {
                clamped = Vector2.zero;
                magnitude = 0f;
            }
            else
            {
                var effective = Mathf.Min(dist, radius);
                clamped = delta * (effective / dist);
                magnitude = effective / radius;
            }

            return new JoystickOutput(clamped / radius, magnitude);
        }
    }
}
