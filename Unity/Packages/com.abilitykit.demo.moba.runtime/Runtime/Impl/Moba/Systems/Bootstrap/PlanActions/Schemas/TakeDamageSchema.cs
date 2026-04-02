using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// take_damage Action 的 Schema 定义
    /// </summary>
    public sealed class TakeDamageSchema : IActionSchema<TakeDamageArgs, IWorldResolver>
    {
        public static readonly TakeDamageSchema Instance = new TakeDamageSchema();

        public ActionId ActionId => TriggeringConstants.TakeDamageId;

        public Type ArgsType => typeof(TakeDamageArgs);

        public TakeDamageArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            float rate = 1f;
            int reasonParam = 0;

            if (namedArgs == null || namedArgs.Count == 0)
                return TakeDamageArgs.Default;

            foreach (var kv in namedArgs)
            {
                var rawValue = kv.Value.Ref.Kind == ENumericValueRefKind.Const
                    ? kv.Value.Ref.ConstValue
                    : ActionSchemaRegistry.ResolveNumericRef(kv.Value.Ref, ctx);

                switch (kv.Key.ToLowerInvariant())
                {
                    case "rate":
                    case "damage_rate":
                    case "damagerate":
                        rate = (float)rawValue;
                        break;
                    case "reason_param":
                    case "reasonparam":
                        reasonParam = (int)System.Math.Round(rawValue);
                        break;
                }
            }

            return new TakeDamageArgs(rate, reasonParam);
        }

        public bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            return true;
        }
    }
}
