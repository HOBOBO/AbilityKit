using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public class SpawnAreaBatchSchema : MobaPlanActionSchemaBase<SpawnAreaBatchArgs>
    {
        public static readonly SpawnAreaBatchSchema Instance = new SpawnAreaBatchSchema();

        private readonly string _actionName;

        protected SpawnAreaBatchSchema(string actionName)
        {
            _actionName = actionName;
        }

        public SpawnAreaBatchSchema()
            : this(TriggeringConstants.Actions.SpawnAreaBatch)
        {
        }

        protected override string ActionName => _actionName;

        public override SpawnAreaBatchArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var areaId = ReadInt(namedArgs, ctx, 0, "area_id", "areaid", "aoe_id", "aoeid", "id");
            var positionMode = ReadInt(namedArgs, ctx, 0, "position_mode", "positionmode", "position");
            var radius = ReadFloat(namedArgs, ctx, 0f, "radius", "radius_override", "radiusoverride");
            var durationFrames = ReadInt(namedArgs, ctx, 0, "duration_frames", "durationframes", "lifetime_frames", "lifetimeframes");
            var durationMs = ReadInt(namedArgs, ctx, 0, "duration_ms", "durationms", "lifetime_ms", "lifetimems");
            var intervalFrames = ReadInt(namedArgs, ctx, 0, "stay_interval_frames", "stayintervalframes", "stay_frames", "stayframes");
            var collisionMask = ReadInt(namedArgs, ctx, 0, "collision_layer_mask", "collisionlayermask", "layer_mask", "layermask");
            var offsetX = ReadFloat(namedArgs, ctx, 0f, "offset_x", "offsetx", "x");
            var offsetY = ReadFloat(namedArgs, ctx, 0f, "offset_y", "offsety", "y");
            var offsetZ = ReadFloat(namedArgs, ctx, 0f, "offset_z", "offsetz", "z");
            var directionOffset = ReadFloat(namedArgs, ctx, 0f, "direction_offset", "directionoffset", "forward_offset", "forwardoffset");
            var lateralOffset = ReadFloat(namedArgs, ctx, 0f, "lateral_offset", "lateraloffset");
            var shapeKind = ReadInt(namedArgs, ctx, 2, "shape_kind", "shapekind", "shape");
            var width = ReadFloat(namedArgs, ctx, 0f, "width", "shape_width");
            var length = ReadFloat(namedArgs, ctx, 0f, "length", "shape_length");
            var initialRadius = ReadFloat(namedArgs, ctx, 0f, "initial_radius", "initialradius");
            var growth = ReadFloat(namedArgs, ctx, 0f, "radius_growth_per_frame", "radiusgrowthperframe", "growth_per_frame");
            var anchor = ReadBoolNonZero(namedArgs, ctx, false, "anchor_at_origin", "anchoratorigin", "front_anchor");
            var groupKey = ReadInt(namedArgs, ctx, 0, "area_group_key", "areagroupkey", "group_key", "groupkey");
            var claim = ReadBoolNonZero(namedArgs, ctx, false, "claim_targets_within_group", "claimtargetswithingroup", "group_target_claim");
            var patternKind = ReadInt(namedArgs, ctx, 0, "pattern_kind", "patternkind", "layout_kind", "layoutkind");
            var count = ReadInt(namedArgs, ctx, 0, "pattern_count", "patterncount", "count");
            var apothem = ReadFloat(namedArgs, ctx, 0f, "pattern_apothem", "patternapothem", "apothem");
            var startAngle = ReadFloat(namedArgs, ctx, 0f, "pattern_start_angle_deg", "patternstartangledeg", "start_angle_deg");
            var angleStep = ReadFloat(namedArgs, ctx, 0f, "pattern_angle_step_deg", "patternanglestepdeg", "angle_step_deg");
            var edgeWidth = ReadFloat(namedArgs, ctx, 0f, "pattern_edge_width", "patternedgewidth", "edge_width");
            var edgeLength = ReadFloat(namedArgs, ctx, 0f, "pattern_edge_length", "patternedgelength", "edge_length");
            var hasTargetRequest = HasExplicitTargetRequest(namedArgs);
            var targetRequest = hasTargetRequest ? MobaActionTargetSchemaReader.Read(namedArgs, ctx) : default;
            TryReadBlackboardTarget(namedArgs, out var resultTarget, "result", "result_id", "area_result");
            TryReadBlackboardTarget(namedArgs, out var resultCountTarget, "result_count", "spawn_count");
            return new SpawnAreaBatchArgs(areaId, positionMode, radius, durationFrames, durationMs,
                intervalFrames, collisionMask, offsetX, offsetY, offsetZ, directionOffset, lateralOffset,
                shapeKind, width, length, initialRadius, growth, anchor, groupKey, claim, count, apothem,
                startAngle, angleStep, edgeWidth, edgeLength, hasTargetRequest, targetRequest,
                resultTarget, resultCountTarget, patternKind);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            return RequireAny(args, "area_id", out error, "area_id", "areaid", "aoe_id", "aoeid", "id");
        }

        private static bool HasExplicitTargetRequest(Dictionary<string, ActionArgValue> namedArgs)
        {
            if (namedArgs == null || namedArgs.Count == 0) return false;
            return HasAny(namedArgs,
                "target_query_id", "targetqueryid", "query_template_id", "querytemplateid",
                "target_query_template_id", "targetquerytemplateid", "target_actor_id", "targetactorid",
                "target_payload_actor_id", "targetpayloadactorid", "target_self", "targetself",
                "target_mode", "targetmode", "target_source", "targetsource", "target_source_code",
                "targetsourcecode", "target_source_param", "targetsourceparam", "target_filter", "targetfilter",
                "target_filter_code", "targetfiltercode", "target_filter_param", "targetfilterparam",
                "target_radius", "targetradius", "target_half_angle_deg", "targethalfangledeg",
                "target_order", "targetorder", "target_order_code", "targetordercode",
                "target_order_param", "targetorderparam", "target_select", "targetselect",
                "target_select_code", "targetselectcode", "target_max_count", "targetmaxcount",
                "max_count", "maxcount");
        }

        private static bool HasAny(Dictionary<string, ActionArgValue> namedArgs, params string[] keys)
        {
            for (var i = 0; i < keys.Length; i++)
                if (namedArgs.ContainsKey(keys[i])) return true;
            return false;
        }
    }

    public sealed class SpawnAreaPatternSchema : SpawnAreaBatchSchema
    {
        public new static readonly SpawnAreaPatternSchema Instance = new SpawnAreaPatternSchema();

        private SpawnAreaPatternSchema()
            : base(TriggeringConstants.Actions.SpawnAreaPattern)
        {
        }
    }
}
