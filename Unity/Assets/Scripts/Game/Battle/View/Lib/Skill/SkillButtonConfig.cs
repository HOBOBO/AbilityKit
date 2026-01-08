using System;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    [Serializable]
    public struct SkillButtonConfig
    {
        public float LongPressSeconds;
        public float DragThreshold;
        public bool EnableAim;

        public static SkillButtonConfig Default => new SkillButtonConfig
        {
            LongPressSeconds = 0.35f,
            DragThreshold = 12f,
            EnableAim = false,
        };
    }
}
