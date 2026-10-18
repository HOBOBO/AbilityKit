using UnityEngine;

namespace AbilityKit.Game.Battle.View.Lib.Joystick
{
    internal sealed class JoystickPointerSession
    {
        private const int NoPointer = int.MinValue;

        private int _pointerId = NoPointer;
        private Vector2 _centerLocal;
        private JoystickOutput _output;

        public bool IsActive => _pointerId != NoPointer;
        public Vector2 CenterLocal => _centerLocal;
        public JoystickOutput Output => _output;

        public bool Begin(int pointerId, Vector2 centerLocal)
        {
            if (IsActive) return false;

            _pointerId = pointerId;
            _centerLocal = centerLocal;
            _output = default;
            return true;
        }

        public bool Update(int pointerId, Vector2 currentLocal, JoystickConfig config, out JoystickOutput output, out Vector2 clamped)
        {
            if (pointerId != _pointerId)
            {
                output = _output;
                clamped = default;
                return false;
            }

            _output = JoystickInputCalculator.Calculate(_centerLocal, currentLocal, config, out clamped);
            output = _output;
            return true;
        }

        public bool End(int pointerId, out JoystickOutput output)
        {
            if (pointerId != _pointerId)
            {
                output = _output;
                return false;
            }

            _pointerId = NoPointer;
            _output = default;
            output = _output;
            return true;
        }
    }
}
