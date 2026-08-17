using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
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
            return new GiveDamageArgs(damageValue, reasonParam, damageType, targetRequest, sourceAttackRatio, reasonKind, attributeSource);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            return RequireAny(args, "damage_value/source_attack_ratio", out error, "damage_value", "value", "damagevalue", "source_attack_ratio", "sourceattackratio", "attack_ratio", "attackratio");
        }
    }
}
