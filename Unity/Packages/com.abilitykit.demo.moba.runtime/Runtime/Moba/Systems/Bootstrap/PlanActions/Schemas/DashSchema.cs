using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// dash Action 鐨?Schema 瀹氫箟
    /// </summary>
    public sealed class DashSchema : IActionSchema<DashArgs, IWorldResolver>
    {
        public static readonly DashSchema Instance = new DashSchema();

        public ActionId ActionId => TriggeringConstants.DashId;

        public Type ArgsType => typeof(DashArgs);

        public DashArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            float speed = 0f;
            float durationMs = 0f;
            int directionMode = 0;
            int priority = 10;
            bool applyToCaster = true;

            if (namedArgs == null || namedArgs.Count == 0)
                return new DashArgs(speed, durationMs, directionMode, priority, applyToCaster);

            foreach (var kv in namedArgs)
            {
                var rawValue = kv.Value.Ref.Kind == ENumericValueRefKind.Const
                    ? kv.Value.Ref.ConstValue
                    : ActionSchemaRegistry.ResolveNumericRef(kv.Value.Ref, ctx);

                switch (kv.Key.ToLowerInvariant())
                {
                    case "speed":
                        speed = (float)rawValue;
                        break;
                    case "duration_ms":
                    case "duration":
                    case "durationms":
                        durationMs = (float)rawValue;
                        break;
                    case "direction_mode":
                    case "directionmode":
                    case "dir_mode":
                        directionMode = (int)System.Math.Round(rawValue);
                        break;
                    case "priority":
                        priority = (int)System.Math.Round(rawValue);
                        break;
                    case "apply_to_caster":
                    case "applytocaster":
                        applyToCaster = rawValue > 0.5f;
                        break;
                }
            }

            return new DashArgs(speed, durationMs, directionMode, priority, applyToCaster);
        }

        public bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            foreach (var kv in args)
            {
                switch (kv.Key.ToLowerInvariant())
                {
                    case "speed":
                        return true;
                    case "duration_ms":
                    case "duration":
                    case "durationms":
                        return true;
                }
            }
            return true;
        }
    }
}
