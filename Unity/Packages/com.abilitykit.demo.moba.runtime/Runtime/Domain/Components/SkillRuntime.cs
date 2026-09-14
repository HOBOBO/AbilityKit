namespace AbilityKit.Demo.Moba.Components
{
    public sealed class ActiveSkillRuntime
    {
        public int SkillId;
        public int Level;
        public int CooldownDurationMs;
        public long CooldownEndTimeMs;
        public int MaxCharges;
        public int CurrentCharges;
        public int ChargeRecoveryMs;
        public long NextChargeRecoveryTimeMs;
        public int CooldownGroupId;
        public bool ChargesConfigured;
        public bool IgnoreGlobalCooldown;
    }

    public sealed class PassiveSkillRuntime
    {
        public int PassiveSkillId;
        public int Level;
        public int CooldownDurationMs;
        public long CooldownEndTimeMs;
    }
}
