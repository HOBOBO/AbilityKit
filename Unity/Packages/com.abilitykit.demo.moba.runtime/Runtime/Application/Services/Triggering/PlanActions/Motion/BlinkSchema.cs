using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Systems;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// blink Action 参数 Schema 定义。
    /// </summary>
    public sealed class BlinkSchema : MobaPlanActionSchemaBase<BlinkArgs>
    {
        public static readonly BlinkSchema Instance = new BlinkSchema();

        protected override string ActionName => TriggeringConstants.Actions.Blink;

        public override BlinkArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var distance = ReadFloat(namedArgs, ctx, 0f, "distance", "dist");
            var directionMode = ReadInt(namedArgs, ctx, 0, "direction_mode", "directionmode", "dir_mode");
            var priority = ReadInt(namedArgs, ctx, 15, "priority");
            var applyToCaster = ReadBool(namedArgs, ctx, true, "apply_to_caster", "applytocaster");
            var passThroughWalls = ReadBool(namedArgs, ctx, false, "pass_through_walls", "passthroughwalls", "pass_walls", "passwalls");

            return new BlinkArgs(distance, directionMode, priority, applyToCaster, passThroughWalls);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            return true;
        }
    }
}
