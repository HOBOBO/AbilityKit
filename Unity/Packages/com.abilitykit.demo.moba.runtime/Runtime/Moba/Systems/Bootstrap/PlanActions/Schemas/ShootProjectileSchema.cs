using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// shoot_projectile Action 鐨?Schema 瀹氫箟
    /// </summary>
    public sealed class ShootProjectileSchema : IActionSchema<ShootProjectileArgs, IWorldResolver>
    {
        public static readonly ShootProjectileSchema Instance = new ShootProjectileSchema();

        public ActionId ActionId => TriggeringConstants.ShootProjectileId;

        public Type ArgsType => typeof(ShootProjectileArgs);

        public ShootProjectileArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            int launcherId = 0;
            int projectileId = 0;

            if (namedArgs == null || namedArgs.Count == 0)
                return ShootProjectileArgs.Default;

            foreach (var kv in namedArgs)
            {
                var rawValue = kv.Value.Ref.Kind == ENumericValueRefKind.Const
                    ? kv.Value.Ref.ConstValue
                    : ActionSchemaRegistry.ResolveNumericRef(kv.Value.Ref, ctx);

                switch (kv.Key.ToLowerInvariant())
                {
                    case "launcher_id":
                    case "launcherid":
                    case "launcher":
                        launcherId = (int)System.Math.Round(rawValue);
                        break;
                    case "projectile_id":
                    case "projectileid":
                    case "projectile":
                        projectileId = (int)System.Math.Round(rawValue);
                        break;
                }
            }

            return new ShootProjectileArgs(launcherId, projectileId);
        }

        public bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            return true;
        }
    }
}
