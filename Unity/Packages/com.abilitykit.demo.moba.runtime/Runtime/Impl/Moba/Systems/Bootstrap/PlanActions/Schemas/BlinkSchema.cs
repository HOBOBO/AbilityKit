using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    /// <summary>
    /// blink Action 鐨?Schema 瀹氫箟
    /// </summary>
    public sealed class BlinkSchema : IActionSchema<BlinkArgs, IWorldResolver>
    {
        public static readonly BlinkSchema Instance = new BlinkSchema();

        public ActionId ActionId => TriggeringConstants.BlinkId;

        public Type ArgsType => typeof(BlinkArgs);

        public BlinkArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            float distance = 0f;
            int directionMode = 0;
            int priority = 15;
            bool applyToCaster = true;

            if (namedArgs == null || namedArgs.Count == 0)
                return new BlinkArgs(distance, directionMode, priority, applyToCaster);

            foreach (var kv in namedArgs)
            {
                var rawValue = kv.Value.Ref.Kind == ENumericValueRefKind.Const
                    ? kv.Value.Ref.ConstValue
                    : ActionSchemaRegistry.ResolveNumericRef(kv.Value.Ref, ctx);

                switch (kv.Key.ToLowerInvariant())
                {
                    case "distance":
                    case "dist":
                        distance = (float)rawValue;
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

            return new BlinkArgs(distance, directionMode, priority, applyToCaster);
        }

        public bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            foreach (var kv in args)
            {
                switch (kv.Key.ToLowerInvariant())
                {
                    case "distance":
                    case "dist":
                        return true;
                }
            }
            return true;
        }
    }
}
