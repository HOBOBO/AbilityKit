namespace AbilityKit.Demo.Moba.Diagnostics.Tests.TriggerAuthoring
{
    internal static class MobaTriggerAuthoringTestIds
    {
        public const int ReservedStart = 99_100_000;
        public const int ReservedEnd = 99_199_999;

        public const int SkillConfigReservedStart = 99_200_000;
        public const int SkillConfigReservedEnd = 99_299_999;

        public const int TargetingStart = 99_110_000;
        public const int TargetingEnd = 99_119_999;
        public const int TargetCollectionDamage = 99_110_001;

        public const int BuffStart = 99_120_000;
        public const int BuffEnd = 99_129_999;
        public const int SelfBuff = 99_120_001;

        public const int ProjectileStart = 99_130_000;
        public const int ProjectileEnd = 99_139_999;
        public const int TrackedProjectile = 99_130_001;

        public const int MotionStart = 99_140_000;
        public const int MotionEnd = 99_149_999;
        public const int ForwardDash = 99_140_001;

        public const int CompositeStart = 99_190_000;
        public const int CompositeEnd = 99_199_999;
        public const int ShieldAndResource = 99_190_001;
        public const int BoundedGroupPull = 99_190_002;
        public const int PersistentArea = 99_190_003;
        public const int OverhealToShield = 99_190_004;
        public const int P0ComplexShowcase = 99_190_010;
        public const int P2ApplySkillParameters = 99_190_020;
        public const int P2ClearSkillParameters = 99_190_021;

        public static bool IsReserved(int id)
        {
            return id >= ReservedStart && id <= ReservedEnd;
        }
    }
}
