namespace AbilityKit.Ability.Share.Common.Projectile
{
    public readonly struct ProjectileScheduleParams
    {
        public readonly int StartFrame;
        public readonly int IntervalFrames;
        public readonly int Count;

        public ProjectileScheduleParams(int startFrame, int intervalFrames, int count)
        {
            StartFrame = startFrame;
            IntervalFrames = intervalFrames;
            Count = count;
        }

        public static ProjectileScheduleParams Once(int startFrame)
        {
            return new ProjectileScheduleParams(startFrame, intervalFrames: 0, count: 1);
        }

        public static ProjectileScheduleParams Repeat(int startFrame, int intervalFrames, int count)
        {
            return new ProjectileScheduleParams(startFrame, intervalFrames, count);
        }

        public static ProjectileScheduleParams Infinite(int startFrame, int intervalFrames)
        {
            return new ProjectileScheduleParams(startFrame, intervalFrames, count: -1);
        }
    }
}
