using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class DamageStructureFromMinionSchema : MobaPlanActionSchemaBase<DamageStructureFromMinionArgs>
    {
        public static readonly DamageStructureFromMinionSchema Instance = new DamageStructureFromMinionSchema();
        protected override string ActionName => "damage_structure_from_minion";

        public override DamageStructureFromMinionArgs ParseArgs(
            Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx) => default;

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            return args.Length == 0;
        }
    }
}
