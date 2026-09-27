using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Maps MOBA wire skill states into platform-neutral presentation contracts.
    /// </summary>
    public static class SkillStateSnapshotMapper
    {
        public static SkillStateData[] Map(MobaSkillStateSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return Array.Empty<SkillStateData>();
            }

            var states = new SkillStateData[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                states[i] = Map(in entries[i]);
            }

            return states;
        }

        public static SkillStateData Map(in MobaSkillStateSnapshotEntry entry)
        {
            return new SkillStateData(
                actorId: entry.ActorId,
                slot: entry.Slot,
                skillId: entry.SkillId,
                level: entry.Level,
                cooldownTotalMs: entry.CooldownTotalMs,
                cooldownRemainingMs: entry.CooldownRemainingMs,
                cooldownEndTimeMs: entry.CooldownEndTimeMs,
                serverTimeMs: entry.ServerTimeMs,
                availability: (SkillAvailabilityState)(int)entry.Availability,
                disableReason: entry.DisableReason,
                maxCharges: entry.MaxCharges,
                currentCharges: entry.CurrentCharges,
                chargeRecoveryRemainingMs: entry.ChargeRecoveryRemainingMs,
                sharedCooldownRemainingMs: entry.SharedCooldownRemainingMs,
                globalCooldownRemainingMs: entry.GlobalCooldownRemainingMs);
        }
    }
}
