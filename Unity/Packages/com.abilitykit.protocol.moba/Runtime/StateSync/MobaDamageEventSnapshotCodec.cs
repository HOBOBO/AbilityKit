using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    public enum DamageEventKind : byte
    {
        Damage = 1,
        Heal = 2,
    }

    [MemoryPackable]
    public partial struct MobaDamageEventSnapshotEntry
    {
        [MemoryPackOrder(0)] public int Kind;
        [MemoryPackOrder(1)] public int AttackerActorId;
        [MemoryPackOrder(2)] public int TargetActorId;
        [MemoryPackOrder(3)] public int DamageType;
        [MemoryPackOrder(4)] public float Value;
        [MemoryPackOrder(5)] public int ReasonKind;
        [MemoryPackOrder(6)] public int ReasonParam;
        [MemoryPackOrder(7)] public float TargetHp;
        [MemoryPackOrder(8)] public float TargetMaxHp;

        public MobaDamageEventSnapshotEntry(int kind, int attackerActorId, int targetActorId, int damageType, float value, int reasonKind, int reasonParam, float targetHp, float targetMaxHp)
        {
            Kind = kind;
            AttackerActorId = attackerActorId;
            TargetActorId = targetActorId;
            DamageType = damageType;
            Value = value;
            ReasonKind = reasonKind;
            ReasonParam = reasonParam;
            TargetHp = targetHp;
            TargetMaxHp = targetMaxHp;
        }
    }

    [MemoryPackable]
    public partial struct MobaDamageEventSnapshotPayload
    {
        [MemoryPackOrder(0)] public MobaDamageEventSnapshotEntry[] Entries;

        [MemoryPackConstructor]
        public MobaDamageEventSnapshotPayload(MobaDamageEventSnapshotEntry[] entries)
        {
            Entries = entries;
        }
    }

    public static class MobaDamageEventSnapshotCodec
    {
        public static byte[] Serialize(MobaDamageEventSnapshotEntry[] entries)
        {
            entries ??= Array.Empty<MobaDamageEventSnapshotEntry>();
            var payload = new MobaDamageEventSnapshotPayload { Entries = entries };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaDamageEventSnapshotEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return Array.Empty<MobaDamageEventSnapshotEntry>();

            var p = MemoryPackSerializer.Deserialize<MobaDamageEventSnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<MobaDamageEventSnapshotEntry>();
        }
    }
}
