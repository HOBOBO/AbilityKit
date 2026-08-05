using System;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;

namespace AbilityKit.Demo.Moba.Gameplay.Triggering
{
    public static class MobaBattlePayloadFields
    {
        public const string AttackerActorId = "attacker_actor_id";
        public const string TargetActorId = "target_actor_id";
        public const string DamageValue = "damage_value";
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
            return MobaBattlePayloadAccessor.SupportsDamageResultField(fieldId)
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
        nameof(MobaBattlePayloadFields.ReasonParam))]
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
        IPayloadIntAccessor<DamageResult>,
        IPayloadDoubleAccessor<DamageResult>,
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
