using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class GameplayCounterDamageSchema : MobaPlanActionSchemaBase<GameplayCounterDamageArgs>
    {
        public static readonly GameplayCounterDamageSchema Instance = new GameplayCounterDamageSchema();
        protected override string ActionName => TriggeringConstants.Actions.AdjustDamageFromGameplayCounter;

        public override GameplayCounterDamageArgs ParseArgs(Dictionary<string, ActionArgValue> a, ExecCtx<IWorldResolver> ctx)
        {
            return new GameplayCounterDamageArgs(
                ReadInt(a, ctx, 0, "key_id", "keyid", "id"),
                ReadInt(a, ctx, 0, "scope_payload_field_id", "scope_field_id", "field_id"),
                ReadFloat(a, ctx, 1, "threshold", "limit"),
                ReadFloat(a, ctx, 1, "value", "modifier_value"),
                ReadInt(a, ctx, 0, "source_id", "sourceid"),
                ReadEnum(a, ctx, DamageReasonKind.None, "reason_kind", "reasonkind"),
                ReadInt(a, ctx, 0, "reason_param", "reasonparam"),
                ReadEnum(a, ctx, DamageNumberSlot.BaseDamage, "number_slot", "slot"),
                ReadEnum(a, ctx, CombatNumberModifierOp.Mul, "op", "modifier_op"));
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            if (HasAny(args, "key_id", "keyid", "id") && HasAny(args, "scope_payload_field_id", "scope_field_id", "field_id") && HasAny(args, "threshold", "limit")) { error = null; return true; }
            error = "adjust_damage_from_gameplay_counter requires key_id, scope_payload_field_id and threshold."; return false;
        }
    }

    public sealed class ConsumeGameplayCounterSchema : MobaPlanActionSchemaBase<ConsumeGameplayCounterArgs>
    {
        public static readonly ConsumeGameplayCounterSchema Instance = new ConsumeGameplayCounterSchema();
        protected override string ActionName => TriggeringConstants.Actions.ConsumeGameplayCounter;
        public override ConsumeGameplayCounterArgs ParseArgs(Dictionary<string, ActionArgValue> a, ExecCtx<IWorldResolver> ctx) =>
            new ConsumeGameplayCounterArgs(
                ReadInt(a, ctx, 0, "key_id", "keyid", "id"),
                ReadInt(a, ctx, 0, "scope_payload_field_id", "scope_field_id", "field_id"),
                ReadFloat(a, ctx, 1, "threshold", "limit"),
                ReadFloat(a, ctx, 0, "reset_value", "reset"),
                ReadEnum(a, ctx, DamageReasonKind.BasicAttack, "reason_kind", "reasonkind"),
                ReadInt(a, ctx, 0, "reason_param", "reasonparam"),
                ReadInt(a, ctx, 0, "trigger_id", "triggerId", "on_consume_trigger_id"),
                ReadBool(a, ctx, false, "per_skill_cast", "perSkillCast", "cast_scoped"));
        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            if (HasAny(args, "key_id", "keyid", "id") && HasAny(args, "scope_payload_field_id", "scope_field_id", "field_id") && HasAny(args, "threshold", "limit")) { error = null; return true; }
            error = "consume_gameplay_counter requires key_id, scope_payload_field_id and threshold."; return false;
        }
    }
}
