using System;
using AbilityKit.Ability.Share;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class MobaDamageEventSnapshotCodec_Obsolete
    {
        public enum EventKind
        {
            Damage = 1,
            Heal = 2,
        }

        public static byte[] Serialize(Entry[] entries)
        {
            entries ??= Array.Empty<Entry>();
            return BinaryObjectCodec.Encode(new SnapshotPayload(entries));
        }

        public static Entry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length < 4) return Array.Empty<Entry>();
            var p = BinaryObjectCodec.Decode<SnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<Entry>();
        }

        public readonly struct SnapshotPayload
        {
            [BinaryMember(0)] public readonly Entry[] Entries;

            public SnapshotPayload(Entry[] entries)
            {
                Entries = entries;
            }
        }

        public readonly struct Entry
        {
            [BinaryMember(0)] public readonly int Kind;
            [BinaryMember(1)] public readonly int AttackerActorId;
            [BinaryMember(2)] public readonly int TargetActorId;
            [BinaryMember(3)] public readonly int DamageType;
            [BinaryMember(4)] public readonly float Value;
            [BinaryMember(5)] public readonly int ReasonKind;
            [BinaryMember(6)] public readonly int ReasonParam;
            [BinaryMember(7)] public readonly float TargetHp;
            [BinaryMember(8)] public readonly float TargetMaxHp;

            public Entry(int kind, int attackerActorId, int targetActorId, int damageType, float value, int reasonKind, int reasonParam, float targetHp, float targetMaxHp)
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

            public static Entry Damage(int attackerActorId, int targetActorId, int damageType, float value, int reasonKind, int reasonParam, float targetHp, float targetMaxHp)
            {
                return new Entry((int)EventKind.Damage, attackerActorId, targetActorId, damageType, value, reasonKind, reasonParam, targetHp, targetMaxHp);
            }

            public static Entry Heal(int healerActorId, int targetActorId, int healType, float value, int reasonKind, int reasonParam, float targetHp, float targetMaxHp)
            {
                return new Entry((int)EventKind.Heal, healerActorId, targetActorId, healType, value, reasonKind, reasonParam, targetHp, targetMaxHp);
            }
        }
    }
}
