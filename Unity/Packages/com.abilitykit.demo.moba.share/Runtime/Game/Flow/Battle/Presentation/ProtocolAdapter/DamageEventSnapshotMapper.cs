using System;
using AbilityKit.Protocol.Moba.StateSync;
using ContractDamageEventKind = AbilityKit.Demo.Moba.Share.DamageEventKind;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Maps MOBA wire damage events into platform-neutral presentation contracts.
    /// </summary>
    public static class DamageEventSnapshotMapper
    {
        public static DamageEventData[] Map(MobaDamageEventSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return Array.Empty<DamageEventData>();
            }

            var events = new DamageEventData[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                events[i] = Map(in entries[i]);
            }

            return events;
        }

        public static DamageEventData Map(in MobaDamageEventSnapshotEntry entry)
        {
            return new DamageEventData(
                kind: (ContractDamageEventKind)entry.Kind,
                attackerId: entry.AttackerActorId,
                targetId: entry.TargetActorId,
                damageType: entry.DamageType,
                value: entry.Value,
                reasonKind: entry.ReasonKind,
                reasonParam: entry.ReasonParam,
                targetHp: entry.TargetHp,
                targetMaxHp: entry.TargetMaxHp,
                isKill: entry.TargetHp <= 0f);
        }
    }
}

