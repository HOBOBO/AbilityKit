namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Platform-neutral skill state used by presentation runtimes.
    /// </summary>
    public readonly struct SkillStateData
    {
        public int ActorId { get; }
        public int Slot { get; }
        public int SkillId { get; }
        public int Level { get; }
        public int CooldownTotalMs { get; }
        public int CooldownRemainingMs { get; }
        public long CooldownEndTimeMs { get; }
        public long ServerTimeMs { get; }
        public SkillAvailabilityState Availability { get; }
        public int DisableReason { get; }
        public int MaxCharges { get; }
        public int CurrentCharges { get; }
        public int ChargeRecoveryRemainingMs { get; }
        public int SharedCooldownRemainingMs { get; }
        public int GlobalCooldownRemainingMs { get; }

        public SkillStateData(
            int actorId,
            int slot,
            int skillId,
            int level = 0,
            int cooldownTotalMs = 0,
            int cooldownRemainingMs = 0,
            long cooldownEndTimeMs = 0L,
            long serverTimeMs = 0L,
            SkillAvailabilityState availability = SkillAvailabilityState.Available,
            int disableReason = 0,
            int maxCharges = 0,
            int currentCharges = 0,
            int chargeRecoveryRemainingMs = 0,
            int sharedCooldownRemainingMs = 0,
            int globalCooldownRemainingMs = 0)
        {
            ActorId = actorId;
            Slot = slot;
            SkillId = skillId;
            Level = level;
            CooldownTotalMs = cooldownTotalMs;
            CooldownRemainingMs = cooldownRemainingMs;
            CooldownEndTimeMs = cooldownEndTimeMs;
            ServerTimeMs = serverTimeMs;
            Availability = availability;
            DisableReason = disableReason;
            MaxCharges = maxCharges;
            CurrentCharges = currentCharges;
            ChargeRecoveryRemainingMs = chargeRecoveryRemainingMs;
            SharedCooldownRemainingMs = sharedCooldownRemainingMs;
            GlobalCooldownRemainingMs = globalCooldownRemainingMs;
        }
    }

    /// <summary>
    /// Stable presentation-facing availability values carried by the skill-state protocol.
    /// Unknown numeric values are preserved when mapped from the wire.
    /// </summary>
    public enum SkillAvailabilityState
    {
        Available = 0,
        CoolingDown = 1,
        Disabled = 2,
    }
}
