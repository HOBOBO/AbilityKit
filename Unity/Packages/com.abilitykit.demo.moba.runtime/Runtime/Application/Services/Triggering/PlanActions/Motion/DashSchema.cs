using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Systems;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// dash Action 参数 Schema 定义。
    /// </summary>
    public sealed class DashSchema : MobaPlanActionSchemaBase<DashArgs>
    {
        public static readonly DashSchema Instance = new DashSchema();

        protected override string ActionName => TriggeringConstants.Actions.Dash;

        public override DashArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var speed = ReadFloat(namedArgs, ctx, 0f, "speed");
            var durationMs = ReadFloat(namedArgs, ctx, 0f, "duration_ms", "duration", "durationms");
            var directionMode = ReadInt(namedArgs, ctx, 0, "direction_mode", "directionmode", "dir_mode");
            var priority = ReadInt(namedArgs, ctx, 10, "priority");
            var applyToCaster = ReadBool(namedArgs, ctx, true, "apply_to_caster", "applytocaster");
            var hitTriggerPlanId = ReadInt(namedArgs, ctx, 0, "hit_trigger_plan_id", "hitTriggerPlanId", "hit_trigger_id", "hitTriggerId");
            var motionGroupId = ReadInt(namedArgs, ctx, 0, "motion_group_id", "motiongroupid", "motion_group", "motiongroup", "group_id", "groupid");
            var moveToAimPosition = ReadBool(namedArgs, ctx, false, "move_to_aim_position", "movetoaimposition", "to_aim_position", "toaimposition");
            var continuous = ReadContinuousSettings(namedArgs, ctx);
            var passThroughWalls = ReadBool(namedArgs, ctx, false, "pass_through_walls", "passthroughwalls", "pass_walls", "passwalls");

            return new DashArgs(speed, durationMs, directionMode, priority, applyToCaster, hitTriggerPlanId, motionGroupId, moveToAimPosition, continuous, passThroughWalls);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            return true;
        }

        private static MobaMotionContinuousSettings ReadContinuousSettings(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var continuousProcessId = ReadInt(namedArgs, ctx, 0, "continuous_process_id", "continuousprocessid", "continuous_id", "continuousid");
            var continuousTagTemplateId = ReadInt(namedArgs, ctx, 0, "continuous_tag_template_id", "continuoustagtemplateid", "tag_template_id", "tagtemplateid");
            var triggerIds = ReadPositiveInts(namedArgs, ctx, "trigger_ids", "triggerids", "owner_trigger_ids", "ownertriggerids");
            var intervalMs = ReadInt(namedArgs, ctx, 0, "interval_ms", "intervalms", "interval");
            var intervalTriggerIds = ReadPositiveInts(namedArgs, ctx, "interval_trigger_ids", "intervaltriggerids", "interval_effect_ids", "intervaleffectids");
            return new MobaMotionContinuousSettings(continuousProcessId, continuousTagTemplateId, triggerIds, intervalMs, intervalTriggerIds);
        }
    }
}
