using System;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;

namespace AbilityKit.Demo.Moba.Gameplay.Triggering
{
    public static class MobaBattlePayloadFields
    {
        public const string AttackerActorId = "attacker_actor_id";
        public const string TargetActorId = "target_actor_id";
        public const string DamageValue = "damage_value";
        public const string BaseDamage = "base_damage";
        public const string DamageRate = "damage_rate";
        public const string FlatBonus = "flat_bonus";
        public const string FinalDamage = "final_damage";
        public const string RawDamage = "raw_damage";
        public const string MitigatedDamage = "mitigated_damage";
        public const string ShieldAbsorb = "shield_absorb";
        public const string HpDamage = "hp_damage";
        public const string HealerActorId = "healer_actor_id";
        public const string HealType = "heal_type";
        public const string AllowDeadTarget = "allow_dead_target";
        public const string ChangeKind = "change_kind";
        public const string SourceActorId = "source_actor_id";
        public const string ValueType = "value_type";
        public const string RequestedValue = "requested_value";
        public const string AppliedValue = "applied_value";
        public const string OverhealValue = "overheal_value";
        public const string OldHp = "old_hp";
        public const string TargetHp = "target_hp";
        public const string TargetMaxHp = "target_max_hp";
        public const string DamageType = "damage_type";
        public const string CritType = "crit_type";
        public const string ReasonKind = "reason_kind";
        public const string ReasonParam = "reason_param";
        public const string UnitActorId = "unit_actor_id";
        public const string KillerActorId = "killer_actor_id";

        public static int FieldId(string fieldName)
        {
            return StableStringId.Get("payload:" + fieldName);
        }

        public static bool IsKnownFieldId(int fieldId)
        {
            return MobaBattlePayloadAccessor.SupportsAttackInfoField(fieldId)
                || MobaBattlePayloadAccessor.SupportsAttackCalcInfoField(fieldId)
                || MobaBattlePayloadAccessor.SupportsDamageResultField(fieldId)
                || MobaBattlePayloadAccessor.SupportsHealRequestField(fieldId)
                || MobaBattlePayloadAccessor.SupportsHealthChangeResultField(fieldId)
                || MobaBattlePayloadAccessor.SupportsUnitDieField(fieldId);
        }
    }

    [GeneratePayloadFieldIds(
        typeof(MobaBattlePayloadFields),
        "SupportsAttackInfoField",
        false,
        nameof(MobaBattlePayloadFields.AttackerActorId),
        nameof(MobaBattlePayloadFields.TargetActorId),
        nameof(MobaBattlePayloadFields.DamageType),
        nameof(MobaBattlePayloadFields.CritType),
        nameof(MobaBattlePayloadFields.ReasonKind),
        nameof(MobaBattlePayloadFields.ReasonParam),
        nameof(MobaBattlePayloadFields.BaseDamage),
        nameof(MobaBattlePayloadFields.DamageRate),
        nameof(MobaBattlePayloadFields.FlatBonus),
        nameof(MobaBattlePayloadFields.FinalDamage))]
    [GeneratePayloadFieldIds(
        typeof(MobaBattlePayloadFields),
        "SupportsAttackCalcInfoField",
        false,
        nameof(MobaBattlePayloadFields.AttackerActorId),
        nameof(MobaBattlePayloadFields.TargetActorId),
        nameof(MobaBattlePayloadFields.DamageType),
        nameof(MobaBattlePayloadFields.CritType),
        nameof(MobaBattlePayloadFields.ReasonKind),
        nameof(MobaBattlePayloadFields.ReasonParam),
        nameof(MobaBattlePayloadFields.BaseDamage),
        nameof(MobaBattlePayloadFields.DamageRate),
        nameof(MobaBattlePayloadFields.FlatBonus),
        nameof(MobaBattlePayloadFields.FinalDamage),
        nameof(MobaBattlePayloadFields.RawDamage),
        nameof(MobaBattlePayloadFields.MitigatedDamage),
        nameof(MobaBattlePayloadFields.ShieldAbsorb),
        nameof(MobaBattlePayloadFields.HpDamage))]
    [GeneratePayloadFieldIds(
        typeof(MobaBattlePayloadFields),
        "SupportsDamageResultField",
        false,
        nameof(MobaBattlePayloadFields.AttackerActorId),
        nameof(MobaBattlePayloadFields.TargetActorId),
        nameof(MobaBattlePayloadFields.DamageType),
        nameof(MobaBattlePayloadFields.CritType),
        nameof(MobaBattlePayloadFields.ReasonKind),
        nameof(MobaBattlePayloadFields.ReasonParam),
        nameof(MobaBattlePayloadFields.DamageValue),
        nameof(MobaBattlePayloadFields.TargetHp),
        nameof(MobaBattlePayloadFields.TargetMaxHp))]
    [GeneratePayloadFieldIds(
        typeof(MobaBattlePayloadFields),
        "SupportsHealRequestField",
        false,
        nameof(MobaBattlePayloadFields.HealerActorId),
        nameof(MobaBattlePayloadFields.TargetActorId),
        nameof(MobaBattlePayloadFields.HealType),
        nameof(MobaBattlePayloadFields.RequestedValue),
        nameof(MobaBattlePayloadFields.ReasonKind),
        nameof(MobaBattlePayloadFields.ReasonParam),
        nameof(MobaBattlePayloadFields.AllowDeadTarget))]
    [GeneratePayloadFieldIds(
        typeof(MobaBattlePayloadFields),
        "SupportsHealthChangeResultField",
        false,
        nameof(MobaBattlePayloadFields.ChangeKind),
        nameof(MobaBattlePayloadFields.SourceActorId),
        nameof(MobaBattlePayloadFields.TargetActorId),
        nameof(MobaBattlePayloadFields.ValueType),
        nameof(MobaBattlePayloadFields.RequestedValue),
        nameof(MobaBattlePayloadFields.AppliedValue),
        nameof(MobaBattlePayloadFields.OverhealValue),
        nameof(MobaBattlePayloadFields.OldHp),
        nameof(MobaBattlePayloadFields.TargetHp),
        nameof(MobaBattlePayloadFields.TargetMaxHp),
        nameof(MobaBattlePayloadFields.ReasonKind),
        nameof(MobaBattlePayloadFields.ReasonParam))]
    [GeneratePayloadFieldIds(
        typeof(MobaBattlePayloadFields),
        "SupportsUnitDieField",
        false,
        nameof(MobaBattlePayloadFields.UnitActorId),
        nameof(MobaBattlePayloadFields.TargetActorId),
        nameof(MobaBattlePayloadFields.KillerActorId),
        nameof(MobaBattlePayloadFields.AttackerActorId),
        nameof(MobaBattlePayloadFields.DamageType),
        nameof(MobaBattlePayloadFields.ReasonKind),
        nameof(MobaBattlePayloadFields.ReasonParam),
        nameof(MobaBattlePayloadFields.DamageValue))]
    public sealed partial class MobaBattlePayloadAccessor :
        IPayloadIntAccessor<AttackInfo>,
        IPayloadDoubleAccessor<AttackInfo>,
        IPayloadIntAccessor<AttackCalcInfo>,
        IPayloadDoubleAccessor<AttackCalcInfo>,
        IPayloadIntAccessor<DamageResult>,
        IPayloadDoubleAccessor<DamageResult>,
        IPayloadIntAccessor<MobaHealRequest>,
        IPayloadDoubleAccessor<MobaHealRequest>,
        IPayloadIntAccessor<MobaHealthChangeResult>,
        IPayloadDoubleAccessor<MobaHealthChangeResult>,
        IPayloadIntAccessor<UnitDieEventPayload>,
        IPayloadDoubleAccessor<UnitDieEventPayload>
    {
        public bool TryGet(in AttackInfo args, int fieldId, out int value)
        {
            value = 0;
            if (args == null) return false;

            if (fieldId == AttackerActorIdId)
            {
                value = args.AttackerActorId;
                return true;
            }

            if (fieldId == TargetActorIdId)
            {
                value = args.TargetActorId;
                return true;
            }

            if (fieldId == DamageTypeId)
            {
                value = (int)args.DamageType;
                return true;
            }

            if (fieldId == CritTypeId)
            {
                value = (int)args.CritType;
                return true;
            }

            if (fieldId == ReasonKindId)
            {
                value = (int)args.ReasonKind;
                return true;
            }

            if (fieldId == ReasonParamId)
            {
                value = args.ReasonParam;
                return true;
            }

            return false;
        }

        public bool TryGet(in AttackInfo args, int fieldId, out double value)
        {
            value = 0d;
            if (args == null) return false;

            if (fieldId == BaseDamageId) value = args.BaseDamage.Value;
            else if (fieldId == DamageRateId) value = args.DamageRate.Value;
            else if (fieldId == FlatBonusId) value = args.FlatBonus.Value;
            else if (fieldId == FinalDamageId) value = args.FinalDamage.Value;
            else if (TryGet(in args, fieldId, out int intValue)) value = intValue;
            else return false;

            return true;
        }

        public bool TryGet(in AttackCalcInfo args, int fieldId, out int value)
        {
            value = 0;
            return args != null && TryGet(in args.Attack, fieldId, out value);
        }

        public bool TryGet(in AttackCalcInfo args, int fieldId, out double value)
        {
            value = 0d;
            if (args == null) return false;

            if (fieldId == RawDamageId) value = args.RawDamage.Value;
            else if (fieldId == MitigatedDamageId) value = args.MitigatedDamage.Value;
            else if (fieldId == ShieldAbsorbId) value = args.ShieldAbsorb.Value;
            else if (fieldId == HpDamageId) value = args.HpDamage.Value;
            else return TryGet(in args.Attack, fieldId, out value);

            return true;
        }

        public bool TryGet(in DamageResult args, int fieldId, out int value)
        {
            value = 0;
            if (args == null) return false;

            if (fieldId == AttackerActorIdId)
            {
                value = args.AttackerActorId;
                return true;
            }

            if (fieldId == TargetActorIdId)
            {
                value = args.TargetActorId;
                return true;
            }

            if (fieldId == DamageTypeId)
            {
                value = (int)args.DamageType;
                return true;
            }

            if (fieldId == CritTypeId)
            {
                value = (int)args.CritType;
                return true;
            }

            if (fieldId == ReasonKindId)
            {
                value = (int)args.ReasonKind;
                return true;
            }

            if (fieldId == ReasonParamId)
            {
                value = args.ReasonParam;
                return true;
            }

            return false;
        }

        public bool TryGet(in DamageResult args, int fieldId, out double value)
        {
            value = 0d;
            if (args == null) return false;

            if (fieldId == DamageValueId)
            {
                value = args.Value;
                return true;
            }

            if (fieldId == TargetHpId)
            {
                value = args.TargetHp;
                return true;
            }

            if (fieldId == TargetMaxHpId)
            {
                value = args.TargetMaxHp;
                return true;
            }

            if (TryGet(in args, fieldId, out int intValue))
            {
                value = intValue;
                return true;
            }

            return false;
        }

        public bool TryGet(in MobaHealRequest args, int fieldId, out int value)
        {
            if (fieldId == HealerActorIdId) value = args.HealerActorId;
            else if (fieldId == TargetActorIdId) value = args.TargetActorId;
            else if (fieldId == HealTypeId) value = args.HealType;
            else if (fieldId == ReasonKindId) value = args.ReasonKind;
            else if (fieldId == ReasonParamId) value = args.ReasonParam;
            else if (fieldId == AllowDeadTargetId) value = args.AllowDeadTarget ? 1 : 0;
            else
            {
                value = 0;
                return false;
            }

            return true;
        }

        public bool TryGet(in MobaHealRequest args, int fieldId, out double value)
        {
            if (fieldId == RequestedValueId)
            {
                value = args.Value;
                return true;
            }

            if (TryGet(in args, fieldId, out int intValue))
            {
                value = intValue;
                return true;
            }

            value = 0d;
            return false;
        }

        public bool TryGet(in MobaHealthChangeResult args, int fieldId, out int value)
        {
            if (fieldId == ChangeKindId) value = (int)args.Kind;
            else if (fieldId == SourceActorIdId) value = args.SourceActorId;
            else if (fieldId == TargetActorIdId) value = args.TargetActorId;
            else if (fieldId == ValueTypeId) value = args.ValueType;
            else if (fieldId == ReasonKindId) value = args.ReasonKind;
            else if (fieldId == ReasonParamId) value = args.ReasonParam;
            else
            {
                value = 0;
                return false;
            }

            return true;
        }

        public bool TryGet(in MobaHealthChangeResult args, int fieldId, out double value)
        {
            if (fieldId == RequestedValueId) value = args.RequestedValue;
            else if (fieldId == AppliedValueId) value = args.AppliedValue;
            else if (fieldId == OverhealValueId)
                value = args.Kind == MobaHealthChangeKind.Heal
                    ? Math.Max(0d, args.RequestedValue - args.AppliedValue)
                    : 0d;
            else if (fieldId == OldHpId) value = args.OldHp;
            else if (fieldId == TargetHpId) value = args.TargetHp;
            else if (fieldId == TargetMaxHpId) value = args.TargetMaxHp;
            else if (TryGet(in args, fieldId, out int intValue)) value = intValue;
            else
            {
                value = 0d;
                return false;
            }

            return true;
        }

        public bool TryGet(in UnitDieEventPayload args, int fieldId, out int value)
        {
            if (fieldId == UnitActorIdId || fieldId == TargetActorIdId)
            {
                value = args.ActorId;
                return true;
            }

            if (fieldId == KillerActorIdId || fieldId == AttackerActorIdId)
            {
                value = args.KillerActorId;
                return true;
            }

            if (fieldId == DamageTypeId)
            {
                value = args.DamageType;
                return true;
            }

            if (fieldId == ReasonKindId)
            {
                value = args.ReasonKind;
                return true;
            }

            if (fieldId == ReasonParamId)
            {
                value = args.ReasonParam;
                return true;
            }

            value = 0;
            return false;
        }

        public bool TryGet(in UnitDieEventPayload args, int fieldId, out double value)
        {
            if (fieldId == DamageValueId)
            {
                value = args.DamageValue;
                return true;
            }

            if (TryGet(in args, fieldId, out int intValue))
            {
                value = intValue;
                return true;
            }

            value = 0d;
            return false;
        }
    }
}
