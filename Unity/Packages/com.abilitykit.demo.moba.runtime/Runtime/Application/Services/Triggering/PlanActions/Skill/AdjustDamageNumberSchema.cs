using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class AdjustDamageNumberSchema : MobaPlanActionSchemaBase<AdjustDamageNumberArgs>
    {
        public static readonly AdjustDamageNumberSchema Instance = new AdjustDamageNumberSchema();

        protected override string ActionName => TriggeringConstants.Actions.AdjustDamageNumber;

        public override AdjustDamageNumberArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var numberSlot = ReadEnum(namedArgs, ctx, DamageNumberSlot.BaseDamage, "number_slot", "numberslot", "slot");
            var op = ReadEnum(namedArgs, ctx, CombatNumberModifierOp.Mul, "op", "modifier_op", "modifierop");
            var value = ReadFloat(namedArgs, ctx, 0f, "value", "modifier_value", "modifiervalue");
            var sourceId = ReadInt(namedArgs, ctx, 0, "source_id", "sourceid");
            var reasonKind = ReadEnum(namedArgs, ctx, DamageReasonKind.None, "reason_kind", "reasonkind");
            var reasonParam = ReadInt(namedArgs, ctx, 0, "reason_param", "reasonparam");
            var requireSkillRuntime = ReadBool(namedArgs, ctx, true, "require_skill_runtime", "requireskillruntime");
            var skipFirstHit = ReadBool(namedArgs, ctx, true, "skip_first_hit", "skipfirsthit");
            var repeatTargetDecayFactor = ReadFloat(namedArgs, ctx, 0f, "repeat_target_decay_factor", "repeattargetdecayfactor", "decay_factor", "decayfactor");
            var targetHitCountKeyBase = ReadInt(namedArgs, ctx, 1200000, "target_hit_count_key_base", "targethitcountkeybase");
            var targetMissingHpRatioCoefficient = ReadFloat(namedArgs, ctx, 0f, "target_missing_hp_ratio_coefficient", "targetmissinghpratiocoefficient", "missing_hp_ratio_coefficient", "missinghpratiocoefficient");

            return new AdjustDamageNumberArgs(
                numberSlot,
                op,
                value,
                sourceId,
                reasonKind,
                reasonParam,
                requireSkillRuntime,
                skipFirstHit,
                repeatTargetDecayFactor,
                targetHitCountKeyBase,
                targetMissingHpRatioCoefficient);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            if (HasAny(args, "value", "modifier_value", "modifiervalue")
                || HasAny(args, "repeat_target_decay_factor", "repeattargetdecayfactor", "decay_factor", "decayfactor")
                || HasAny(args, "target_missing_hp_ratio_coefficient", "targetmissinghpratiocoefficient", "missing_hp_ratio_coefficient", "missinghpratiocoefficient"))
            {
                error = null;
                return true;
            }

            error = "adjust_damage_number requires value, repeat_target_decay_factor, or target_missing_hp_ratio_coefficient.";
            return false;
        }
    }
}
