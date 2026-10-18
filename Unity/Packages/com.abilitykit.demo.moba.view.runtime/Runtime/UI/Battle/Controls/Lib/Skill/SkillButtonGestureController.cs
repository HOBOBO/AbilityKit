using UnityEngine;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    internal sealed class SkillButtonGestureController
    {
        private SkillButtonPointerState _pointer = SkillButtonPointerState.Create();
        private SkillButtonConfig _config = SkillButtonConfig.Default;

        public bool Pressed => _pointer.Pressed;
        public bool Aiming => _pointer.Aiming;
        public bool LongPressFired => _pointer.LongPressFired;
        public Vector2 LastScreenPos => _pointer.LastScreenPos;

        public void Configure(SkillButtonConfig config)
        {
            _config = config;
            if (_config.DragThreshold <= 0f) _config.DragThreshold = SkillButtonConfig.Default.DragThreshold;
        }

        public void Reset()
        {
            _pointer = SkillButtonPointerState.Create();
        }

        public bool Begin(int pointerId, Vector2 screenPos, float pressTime)
        {
            return _pointer.Begin(pointerId, screenPos, pressTime);
        }

        public bool Tick(float now)
        {
            if (!_pointer.Pressed) return false;
            if (_pointer.LongPressFired) return false;
            if (_config.LongPressSeconds <= 0f) return false;
            if (now - _pointer.PressTime < _config.LongPressSeconds) return false;

            _pointer.MarkLongPressFired();
            return true;
        }

        public bool Drag(int pointerId, Vector2 screenPos, out bool shouldBeginAim, out bool shouldUpdateAim)
        {
            shouldBeginAim = false;
            shouldUpdateAim = false;

            if (!_pointer.Pressed) return false;
            if (!_pointer.Matches(pointerId)) return false;

            _pointer.UpdateScreenPos(screenPos);
            shouldBeginAim = _config.EnableAim && !_pointer.Aiming && ShouldBeginAim();
            shouldUpdateAim = _pointer.Aiming;
            return true;
        }

        public bool End(int pointerId, out bool wasAiming, out bool longPressFired)
        {
            wasAiming = _pointer.Aiming;
            longPressFired = _pointer.LongPressFired;

            if (!_pointer.Matches(pointerId)) return false;

            _pointer.EndPress();
            return true;
        }

        public void SetAiming(bool aiming)
        {
            _pointer.SetAiming(aiming);
        }

        private bool ShouldBeginAim()
        {
            var drag = (_pointer.LastScreenPos - _pointer.PressScreenPos).magnitude;
            return drag >= _config.DragThreshold;
        }
    }
}
