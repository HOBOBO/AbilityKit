using System;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    public enum SkillAimMode
    {
        Direction = 0,
        Point = 1,
    }

    public enum SkillAimIndicatorShape
    {
        Hidden = 0,
        DirectionLine = 1,
        TargetCircle = 2,
        SelfCircle = 3,
        Sector = 4,
        DirectionArea = 5,
        DashLine = 6,
        LockProjectile = 7,
        FanArea = 8,
    }

    public enum SkillUsePointMode
    {
        None = 0,
        Aim = 1,
        TargetPoint = 2,
    }

    [Serializable]
    public struct SkillButtonConfig
    {
        public float LongPressSeconds;
        public float DragThreshold;
        public bool EnableAim;

        public SkillAimMode AimMode;
        public float AimMaxRadius;

        public SkillUsePointMode UsePointMode;
        public float SelectRange;
        public bool FaceToAim;

        public SkillAimIndicatorShape IndicatorShape;
        public float IndicatorLengthPixels;
        public float IndicatorWidthPixels;

        public static SkillButtonConfig Default => new SkillButtonConfig
        {
            LongPressSeconds = 0.35f,
            DragThreshold = 12f,
            EnableAim = false,

            AimMode = SkillAimMode.Direction,
            AimMaxRadius = 180f,

            UsePointMode = SkillUsePointMode.None,
            SelectRange = 0f,
            FaceToAim = false,

            IndicatorShape = SkillAimIndicatorShape.Hidden,
            IndicatorLengthPixels = 180f,
            IndicatorWidthPixels = 52f,
        };
    }
}
