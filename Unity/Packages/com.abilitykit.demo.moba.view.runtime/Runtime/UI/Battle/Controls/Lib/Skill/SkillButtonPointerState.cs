using UnityEngine;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    internal struct SkillButtonPointerState
    {
        private const int NoPointer = int.MinValue;

        public int PointerId { get; private set; }
        public bool Pressed { get; private set; }
        public bool LongPressFired { get; private set; }
        public bool Aiming { get; private set; }
        public float PressTime { get; private set; }
        public Vector2 PressScreenPos { get; private set; }
        public Vector2 LastScreenPos { get; private set; }

        public bool HasPointer => PointerId != NoPointer;

        public bool Begin(int pointerId, Vector2 screenPos, float pressTime)
        {
            if (HasPointer) return false;

            PointerId = pointerId;
            Pressed = true;
            LongPressFired = false;
            Aiming = false;
            PressTime = pressTime;
            PressScreenPos = screenPos;
            LastScreenPos = screenPos;
            return true;
        }

        public bool Matches(int pointerId)
        {
            return pointerId == PointerId;
        }

        public void UpdateScreenPos(Vector2 screenPos)
        {
            LastScreenPos = screenPos;
        }

        public void MarkLongPressFired()
        {
            LongPressFired = true;
        }

        public void SetAiming(bool aiming)
        {
            Aiming = aiming;
        }

        public void EndPress()
        {
            Pressed = false;
            PointerId = NoPointer;
        }

        public static SkillButtonPointerState Create()
        {
            return new SkillButtonPointerState
            {
                PointerId = NoPointer,
            };
        }
    }
}
