using System;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    public enum SkillAimMode
    {
        Direction = 0,
        Point = 1,
    }

    [Serializable]
    public struct SkillButtonConfig
    {
        public float LongPressSeconds;
        public float DragThreshold;
        public bool EnableAim;

        public SkillAimMode AimMode;
        public float AimMaxRadius;

        public static SkillButtonConfig Default => new SkillButtonConfig
        {
            LongPressSeconds = 0.35f,
            DragThreshold = 12f,
            EnableAim = false,

            AimMode = SkillAimMode.Direction,
            AimMaxRadius = 180f,
        };
    }
}
