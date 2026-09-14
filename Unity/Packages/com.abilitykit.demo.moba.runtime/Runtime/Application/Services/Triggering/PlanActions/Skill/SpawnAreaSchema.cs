using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class SpawnAreaSchema : MobaPlanActionSchemaBase<SpawnAreaArgs>
    {
        public static readonly SpawnAreaSchema Instance = new SpawnAreaSchema();

        protected override string ActionName => TriggeringConstants.Actions.SpawnArea;

        public override SpawnAreaArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var areaId = ReadInt(namedArgs, ctx, 0, "area_id", "areaid", "aoe_id", "aoeid", "id");
            var positionMode = ReadInt(namedArgs, ctx, 0, "position_mode", "positionmode", "position");
            var radiusOverride = ReadFloat(namedArgs, ctx, 0f, "radius", "radius_override", "radiusoverride");
            var durationFrames = ReadInt(namedArgs, ctx, 0, "duration_frames", "durationframes", "lifetime_frames", "lifetimeframes");
            var durationMs = ReadInt(namedArgs, ctx, 0, "duration_ms", "durationms", "lifetime_ms", "lifetimems");
            var stayIntervalFrames = ReadInt(namedArgs, ctx, 0, "stay_interval_frames", "stayintervalframes", "stay_frames", "stayframes");
            var collisionLayerMaskOverride = ReadInt(namedArgs, ctx, 0, "collision_layer_mask", "collisionlayermask", "layer_mask", "layermask");
            var offsetX = ReadFloat(namedArgs, ctx, 0f, "offset_x", "offsetx", "x");
            var offsetY = ReadFloat(namedArgs, ctx, 0f, "offset_y", "offsety", "y");
            var offsetZ = ReadFloat(namedArgs, ctx, 0f, "offset_z", "offsetz", "z");
            var hasTargetRequest = HasExplicitTargetRequest(namedArgs);
            var targetRequest = hasTargetRequest ? MobaActionTargetSchemaReader.Read(namedArgs, ctx) : default;
            TryReadBlackboardTarget(namedArgs, out var resultTarget, "result", "result_id", "area_result");
            TryReadBlackboardTarget(namedArgs, out var resultCountTarget, "result_count", "spawn_count");
            return new SpawnAreaArgs(areaId, positionMode, radiusOverride, durationFrames, durationMs, stayIntervalFrames, collisionLayerMaskOverride, offsetX, offsetY, offsetZ, hasTargetRequest, targetRequest, resultTarget, resultCountTarget);
        }

        private static bool HasExplicitTargetRequest(Dictionary<string, ActionArgValue> namedArgs)
        {
            if (namedArgs == null || namedArgs.Count == 0) return false;
            return HasAny(namedArgs,
                "target_query_id", "targetqueryid",
                "query_template_id", "querytemplateid",
                "target_query_template_id", "targetquerytemplateid",
                "target_actor_id", "targetactorid",
                "target_payload_actor_id", "targetpayloadactorid",
                "target_self", "targetself",
                "target_mode", "targetmode",
                "target_source", "targetsource",
                "target_source_code", "targetsourcecode",
                "target_source_param", "targetsourceparam",
                "target_filter", "targetfilter",
                "target_filter_code", "targetfiltercode",
                "target_filter_param", "targetfilterparam",
                "target_radius", "targetradius",
                "target_half_angle_deg", "targethalfanglegdeg",
                "target_order", "targetorder",
                "target_order_code", "targetordercode",
                "target_order_param", "targetorderparam",
                "target_select", "targetselect",
                "target_select_code", "targetselectcode",
                "target_max_count", "targetmaxcount",
                "max_count", "maxcount");
        }

        private static bool HasAny(Dictionary<string, ActionArgValue> namedArgs, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (namedArgs.ContainsKey(keys[i])) return true;
            }

            return false;
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            return RequireAny(args, "area_id", out error, "area_id", "areaid", "aoe_id", "aoeid", "id");
        }
    }
}
