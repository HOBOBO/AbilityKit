using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// pull Action 鐨?Schema 瀹氫箟
    /// </summary>
    public sealed class PullSchema : IActionSchema<PullArgs, IWorldResolver>
    {
        public static readonly PullSchema Instance = new PullSchema();

        public ActionId ActionId => TriggeringConstants.PullId;

        public Type ArgsType => typeof(PullArgs);

        public PullArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            float speed = 0f;
            float durationMs = 0f;
            int directionMode = 0;
            float targetDistance = 0f;
            int priority = 12;

            if (namedArgs == null || namedArgs.Count == 0)
                return new PullArgs(speed, durationMs, directionMode, targetDistance, priority);

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
                    case "target_distance":
                    case "targetdistance":
                        targetDistance = (float)rawValue;
                        break;
                    case "priority":
                        priority = (int)System.Math.Round(rawValue);
                        break;
                }
            }

            return new PullArgs(speed, durationMs, directionMode, targetDistance, priority);
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
