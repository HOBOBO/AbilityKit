using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// give_damage Action 閻?Schema 鐎规矮绠?
    /// 鐎圭偟骞?IActionSchema閿涘本褰佹笟娑樺棘閺佹媽袙閺嬫劕鎷版宀冪槈闁槒绶?
    /// </summary>
    public sealed class GiveDamageSchema : IActionSchema<GiveDamageArgs, IWorldResolver>
    {
        public static readonly GiveDamageSchema Instance = new GiveDamageSchema();

        public ActionId ActionId => TriggeringConstants.GiveDamageId;

        public Type ArgsType => typeof(GiveDamageArgs);

        public GiveDamageArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            float damageValue = 0f;
            int reasonParam = 0;
            DamageType damageType = DamageType.Physical;

            if (namedArgs == null || namedArgs.Count == 0)
                return new GiveDamageArgs(damageValue, reasonParam, damageType);

            foreach (var kv in namedArgs)
            {
                var rawValue = kv.Value.Ref.Kind == ENumericValueRefKind.Const
                    ? kv.Value.Ref.ConstValue
                    : ActionSchemaRegistry.ResolveNumericRef(kv.Value.Ref, ctx);

                switch (kv.Key.ToLowerInvariant())
                {
                    case "damage_value":
                    case "value":
                    case "damagevalue":
                        damageValue = (float)rawValue;
                        break;
                    case "reason_param":
                    case "reasonparam":
                        reasonParam = (int)System.Math.Round(rawValue);
                        break;
                    case "damage_type":
                    case "damagetype":
                        damageType = (DamageType)(int)System.Math.Round(rawValue);
                        break;
                }
            }

            return new GiveDamageArgs(damageValue, reasonParam, damageType);
        }

        public bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            foreach (var kv in args)
            {
                switch (kv.Key.ToLowerInvariant())
                {
                    case "damage_value":
                    case "value":
                    case "damagevalue":
                        return true;
                }
            }
            error = "give_damage is missing required parameter 'damage_value'";
            return false;
        }
    }
}
