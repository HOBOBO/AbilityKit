using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct ExecuteTriggerArgs
    {
        public ExecuteTriggerArgs(int triggerId)
        {
            TriggerId = triggerId;
        }

        public int TriggerId { get; }
    }

    public sealed class ExecuteTriggerSchema : MobaPlanActionSchemaBase<ExecuteTriggerArgs>
    {
        public static readonly ExecuteTriggerSchema Instance = new ExecuteTriggerSchema();

        protected override string ActionName => TriggeringConstants.Actions.ExecuteTrigger;

        public override ExecuteTriggerArgs ParseArgs(
            Dictionary<string, ActionArgValue> namedArgs,
            ExecCtx<IWorldResolver> ctx)
        {
            return new ExecuteTriggerArgs(ReadInt(namedArgs, ctx, 0, "trigger_id", "triggerId"));
        }

        public override bool TryValidateArgs(
            ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args,
            out string error)
        {
            return RequireAny(args, "trigger_id", out error, "trigger_id", "triggerId");
        }
    }

    [PlanActionModule(order: MobaPlanActionModuleOrders.ExecuteTrigger)]
    public sealed class ExecuteTriggerPlanActionModule :
        MobaPlanActionModuleBase<ExecuteTriggerArgs, ExecuteTriggerPlanActionModule>
    {
        protected override IActionSchema<ExecuteTriggerArgs, IWorldResolver> Schema => ExecuteTriggerSchema.Instance;

        protected override void Execute(object triggerArgs, ExecuteTriggerArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (triggerArgs == null || args.TriggerId <= 0) return;
            if (!TryResolveRequired(ctx, out MobaEffectExecutionService effects)) return;
            effects.ExecuteRulePlan(args.TriggerId, triggerArgs);
        }
    }
}
