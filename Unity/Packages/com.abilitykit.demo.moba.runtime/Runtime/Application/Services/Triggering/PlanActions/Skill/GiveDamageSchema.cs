using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Demo.Moba.Systems;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// give_damage Action 的强类型参数 Schema。
    /// </summary>
    public sealed class GiveDamageSchema : MobaPlanActionSchemaBase<GiveDamageArgs>
    {
        public static readonly GiveDamageSchema Instance = new GiveDamageSchema();

        protected override string ActionName => TriggeringConstants.Actions.GiveDamage;

        public override GiveDamageArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var damageValue = ReadFloat(namedArgs, ctx, 0f, "damage_value", "value", "damagevalue");
            var reasonParam = ReadInt(namedArgs, ctx, 0, "reason_param", "reasonparam");
            var damageType = ReadEnum(namedArgs, ctx, DamageType.Physical, "damage_type", "damagetype");
            var sourceAttackRatio = ReadFloat(namedArgs, ctx, 0f, "source_attack_ratio", "sourceattackratio", "attack_ratio", "attackratio");
            var reasonKind = ReadEnum(namedArgs, ctx, DamageReasonKind.Skill, "reason_kind", "reasonkind");
            var attributeSource = ReadEnum(
                namedArgs,
                ctx,
                DamageAttributeSourceKind.AttributionActor,
                "attribute_source",
                "attributesource",
                "attribute_source_kind",
                "attributesourcekind");
            var targetRequest = MobaActionTargetSchemaReader.Read(namedArgs, ctx);
            var magnitude = MobaEffectMagnitudeSchemaReader.Read(namedArgs, in ctx, damageValue);
            var maxTargetHits = ReadInt(namedArgs, ctx, 0, "max_target_hits", "maxtargethits", "target_hit_limit", "targethitlimit");
            var targetHitCountKeyBase = ReadInt(namedArgs, ctx, 1200000, "target_hit_count_key_base", "targethitcountkeybase");
            var hasDamageValueRef = TryReadNumericRef(namedArgs, out var damageValueRef, "damage_value_ref", "damagevalueref");
            var damageValueSkillLevelParameter = MobaEffectMagnitudeSchemaReader.ReadString(namedArgs, in ctx, "damage_value_skill_level_parameter", "damagevalueskilllevelparameter");
            var damageValueRefParameter = MobaEffectMagnitudeSchemaReader.ReadString(namedArgs, in ctx, "damage_value_ref_skill_level_parameter", "damagevaluerefskilllevelparameter");
            var missingHpCoefficient = ReadFloat(namedArgs, ctx, 0f, "target_missing_hp_ratio_coefficient", "targetmissinghpratiocoefficient");
            var missingHpParameter = MobaEffectMagnitudeSchemaReader.ReadString(namedArgs, in ctx, "target_missing_hp_ratio_skill_level_parameter", "targetmissinghpratioskilllevelparameter");
            var missingHpBonusMin = ReadFloat(namedArgs, ctx, 0f, "target_missing_hp_bonus_min", "targetmissinghpbonusmin");
            var missingHpBonusMax = ReadFloat(namedArgs, ctx, 0f, "target_missing_hp_bonus_max", "targetmissinghpbonusmax");
            var damageScale = ReadFloat(namedArgs, ctx, 1f, "damage_scale", "damagescale");
            var damageScaleParameter = MobaEffectMagnitudeSchemaReader.ReadString(namedArgs, in ctx, "damage_scale_skill_level_parameter", "damagescaleskilllevelparameter");
            var damageMin = ReadFloat(namedArgs, ctx, 0f, "damage_min", "damagemin");
            var damageMax = ReadFloat(namedArgs, ctx, 0f, "damage_max", "damagemax");
            return new GiveDamageArgs(
                damageValue, reasonParam, damageType, targetRequest, sourceAttackRatio, reasonKind,
                attributeSource, magnitude, maxTargetHits, targetHitCountKeyBase,
                damageValueRef, hasDamageValueRef, damageValueSkillLevelParameter, damageValueRefParameter,
                missingHpCoefficient, missingHpParameter, missingHpBonusMin, missingHpBonusMax,
                damageScale, damageScaleParameter, damageMin, damageMax,
                HasAny(namedArgs, "target_missing_hp_bonus_min", "targetmissinghpbonusmin"),
                HasAny(namedArgs, "target_missing_hp_bonus_max", "targetmissinghpbonusmax"),
                HasAny(namedArgs, "damage_min", "damagemin"),
                HasAny(namedArgs, "damage_max", "damagemax"),
                ReadFloat(namedArgs, ctx, 0f, "target_max_hp_ratio_coefficient"),
                MobaEffectMagnitudeSchemaReader.ReadString(namedArgs, in ctx, "target_max_hp_ratio_skill_level_parameter"));
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            if (!ValidateOptionalString(args, "target_max_hp_ratio_skill_level_parameter", out error, "target_max_hp_ratio_skill_level_parameter")) return false;
            if (HasAny(args, "target_max_hp_ratio_coefficient"))
            {
                if (HasAny(args, "target_max_hp_ratio_skill_level_parameter") ||
                    !TryReadConstantNumber(args, out var ratio, "target_max_hp_ratio_coefficient") ||
                    double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio < 0)
                { error = "target max HP coefficient must use one non-negative constant or skill level parameter"; return false; }
            }
            if (HasAny(args, "damage_value_ref", "damagevalueref")
                && !RequireNumericValue(args, "damage_value_ref", out error, "damage_value_ref", "damagevalueref"))
                return false;
            if (!ValidateOptionalString(args, "damage_value_skill_level_parameter", out error,
                    "damage_value_skill_level_parameter", "damagevalueskilllevelparameter")
                || !ValidateOptionalString(args, "damage_value_ref_skill_level_parameter", out error,
                    "damage_value_ref_skill_level_parameter", "damagevaluerefskilllevelparameter")
                || !ValidateOptionalString(args, "target_missing_hp_ratio_skill_level_parameter", out error,
                    "target_missing_hp_ratio_skill_level_parameter", "targetmissinghpratioskilllevelparameter")
                || !ValidateOptionalString(args, "damage_scale_skill_level_parameter", out error,
                    "damage_scale_skill_level_parameter", "damagescaleskilllevelparameter"))
                return false;
            if (HasAny(args, "damage_value_ref_skill_level_parameter", "damagevaluerefskilllevelparameter")
                && !HasAny(args, "damage_value_ref", "damagevalueref"))
            {
                error = "damage_value_ref_skill_level_parameter requires damage_value_ref";
                return false;
            }
            if (HasAny(args, "damage_value_skill_level_parameter", "damagevalueskilllevelparameter")
                && (MobaEffectMagnitudeSchemaReader.HasMagnitudeArgs(args)
                    || HasAny(args, "damage_value", "value", "damagevalue")))
            {
                error = "damage value must use either a fixed value, a skill level parameter, or magnitude_*";
                return false;
            }
            if ((HasAny(args, "target_missing_hp_bonus_min", "targetmissinghpbonusmin")
                    || HasAny(args, "target_missing_hp_bonus_max", "targetmissinghpbonusmax"))
                && !HasAny(args, "target_missing_hp_ratio_coefficient", "targetmissinghpratiocoefficient",
                    "target_missing_hp_ratio_skill_level_parameter", "targetmissinghpratioskilllevelparameter"))
            {
                error = "target missing HP bonus limits require a target missing HP ratio coefficient";
                return false;
            }
            if (!ValidateOptionalConstantNumber(args, "target_missing_hp_ratio_coefficient", out error,
                    "target_missing_hp_ratio_coefficient", "targetmissinghpratiocoefficient")
                || !ValidateOptionalConstantNumber(args, "target_missing_hp_bonus_min", out error,
                    "target_missing_hp_bonus_min", "targetmissinghpbonusmin")
                || !ValidateOptionalConstantNumber(args, "target_missing_hp_bonus_max", out error,
                    "target_missing_hp_bonus_max", "targetmissinghpbonusmax")
                || !ValidateOptionalConstantNumber(args, "damage_scale", out error,
                    "damage_scale", "damagescale")
                || !ValidateOptionalConstantNumber(args, "damage_min", out error,
                    "damage_min", "damagemin")
                || !ValidateOptionalConstantNumber(args, "damage_max", out error,
                    "damage_max", "damagemax"))
                return false;
            if (HasAny(args, "target_missing_hp_ratio_skill_level_parameter", "targetmissinghpratioskilllevelparameter")
                && HasAny(args, "target_missing_hp_ratio_coefficient", "targetmissinghpratiocoefficient"))
            {
                error = "target missing HP coefficient must use either a fixed coefficient or a skill level parameter";
                return false;
            }
            if (HasAny(args, "damage_scale_skill_level_parameter", "damagescaleskilllevelparameter")
                && HasAny(args, "damage_scale", "damagescale"))
            {
                error = "damage scale must use either a fixed value or a skill level parameter";
                return false;
            }
            if (HasAny(args, "target_missing_hp_bonus_min", "targetmissinghpbonusmin")
                && HasAny(args, "target_missing_hp_bonus_max", "targetmissinghpbonusmax")
                && TryReadConstantNumber(args, out var missingMin, "target_missing_hp_bonus_min", "targetmissinghpbonusmin")
                && TryReadConstantNumber(args, out var missingMax, "target_missing_hp_bonus_max", "targetmissinghpbonusmax")
                && missingMax < missingMin)
            {
                error = "target_missing_hp_bonus_max must be greater than or equal to target_missing_hp_bonus_min";
                return false;
            }
            if ((HasAny(args, "target_missing_hp_bonus_min", "targetmissinghpbonusmin")
                 && TryReadConstantNumber(args, out var missingBonusMin, "target_missing_hp_bonus_min", "targetmissinghpbonusmin")
                 && missingBonusMin < 0d)
                || (HasAny(args, "target_missing_hp_bonus_max", "targetmissinghpbonusmax")
                    && TryReadConstantNumber(args, out var missingBonusMax, "target_missing_hp_bonus_max", "targetmissinghpbonusmax")
                    && missingBonusMax < 0d))
            {
                error = "target missing HP bonus limits must be non-negative";
                return false;
            }
            if (HasAny(args, "damage_max", "damagemax")
                && TryReadConstantNumber(args, out var damageMax, "damage_max", "damagemax")
                && ((HasAny(args, "damage_min", "damagemin")
                     && TryReadConstantNumber(args, out var damageMin, "damage_min", "damagemin")
                     && damageMax < damageMin)
                    || damageMax < 0d))
            {
                error = "damage_max must be non-negative and greater than or equal to damage_min";
                return false;
            }
            if (HasAny(args, "damage_min", "damagemin")
                && TryReadConstantNumber(args, out var damageMinValue, "damage_min", "damagemin")
                && damageMinValue < 0d)
            {
                error = "damage_min must be non-negative";
                return false;
            }
            if (HasAny(args, "target_missing_hp_ratio_coefficient", "targetmissinghpratiocoefficient")
                && TryReadConstantNumber(args, out var missingCoefficient, "target_missing_hp_ratio_coefficient", "targetmissinghpratiocoefficient")
                && missingCoefficient < 0d)
            {
                error = "target missing HP ratio coefficient must be non-negative";
                return false;
            }
            if (HasAny(args, "damage_scale", "damagescale")
                && TryReadConstantNumber(args, out var damageScale, "damage_scale", "damagescale")
                && damageScale < 0d)
            {
                error = "damage_scale must be non-negative";
                return false;
            }
            if (HasAny(args, "damage_value_ref", "damagevalueref") && MobaEffectMagnitudeSchemaReader.HasMagnitudeArgs(args))
            {
                error = "damage_value_ref cannot be combined with magnitude_*";
                return false;
            }
            if (MobaEffectMagnitudeSchemaReader.HasMagnitudeArgs(args)) { error = null; return true; }
            if (HasAny(args, "target_missing_hp_ratio_skill_level_parameter", "targetmissinghpratioskilllevelparameter")
                || HasAny(args, "target_missing_hp_ratio_coefficient", "targetmissinghpratiocoefficient"))
            {
                error = null;
                return true;
            }
            return RequireAny(args, "damage_value/source_attack_ratio/damage_value_ref/damage_value_skill_level_parameter", out error, "damage_value", "value", "damagevalue", "source_attack_ratio", "sourceattackratio", "attack_ratio", "attackratio", "damage_value_ref", "damagevalueref", "damage_value_skill_level_parameter", "damagevalueskilllevelparameter");
        }

        private static bool TryReadNumericRef(Dictionary<string, ActionArgValue> args, out AbilityKit.Triggering.Runtime.Plan.NumericValueRef valueRef, params string[] aliases)
        {
            valueRef = default;
            if (args == null) return false;
            foreach (var pair in args)
            {
                if (!MatchesAlias(pair.Key, aliases)) continue;
                if (pair.Value.Kind != ActionArgKind.NumericValue)
                    throw new ArgumentException("give_damage parameter 'damage_value_ref' must be numeric");
                valueRef = pair.Value.Ref;
                return true;
            }
            return false;
        }

        private static bool MatchesAlias(string key, string[] aliases)
        {
            if (string.IsNullOrEmpty(key) || aliases == null) return false;
            for (var i = 0; i < aliases.Length; i++)
                if (string.Equals(key, aliases[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool HasAny(Dictionary<string, ActionArgValue> args, params string[] aliases)
        {
            if (args == null || aliases == null || aliases.Length == 0) return false;
            foreach (var key in args.Keys)
                if (MatchesAlias(key, aliases)) return true;
            return false;
        }

        private static bool TryReadConstantNumber(
            ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args,
            out double value,
            params string[] aliases)
        {
            value = default;
            for (var i = 0; i < args.Length; i++)
            {
                var pair = args[i];
                if (!MatchesAlias(pair.Key, aliases)) continue;
                if (pair.Value.Kind != ActionArgKind.NumericValue || pair.Value.Ref.Kind != ENumericValueRefKind.Const)
                    return false;
                value = pair.Value.Ref.ConstValue;
                return true;
            }
            return false;
        }

        private static bool ValidateOptionalString(
            ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args,
            string displayName,
            out string error,
            params string[] aliases)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (!MatchesAlias(args[i].Key, aliases)) continue;
                if (args[i].Value.Kind != ActionArgKind.StringValue
                    || string.IsNullOrWhiteSpace(args[i].Value.StringValue))
                {
                    error = $"{displayName} must be a non-empty string";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool ValidateOptionalConstantNumber(
            ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args,
            string displayName,
            out string error,
            params string[] aliases)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (!MatchesAlias(args[i].Key, aliases)) continue;
                if (args[i].Value.Kind != ActionArgKind.NumericValue
                    || args[i].Value.Ref.Kind != ENumericValueRefKind.Const)
                {
                    error = $"{displayName} must be a constant number";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
