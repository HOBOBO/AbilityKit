using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct ConsumeBuffQualificationArgs
    {
        public readonly int HitTriggerId;
        public ConsumeBuffQualificationArgs(int id)
        { if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id)); HitTriggerId = id; }
    }

    public sealed class ConsumeBuffQualificationSchema : MobaPlanActionSchemaBase<ConsumeBuffQualificationArgs>
    {
        public static readonly ConsumeBuffQualificationSchema Instance = new ConsumeBuffQualificationSchema();
        protected override string ActionName => "consume_buff_qualification";
        public override ConsumeBuffQualificationArgs ParseArgs(Dictionary<string, ActionArgValue> args, ExecCtx<IWorldResolver> ctx) =>
            new ConsumeBuffQualificationArgs(ReadInt(args, ctx, 0, "hit_trigger_id"));
        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error) =>
            RequireAny(args, "hit trigger", out error, "hit_trigger_id");
    }

    /// <summary>Explicit contact callback; does not subscribe to HP damage results.</summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.ConsumeBuffQualification)]
    public sealed class ConsumeBuffQualificationPlanActionModule : MobaPlanActionModuleBase<ConsumeBuffQualificationArgs, ConsumeBuffQualificationPlanActionModule>
    {
        protected override IActionSchema<ConsumeBuffQualificationArgs, IWorldResolver> Schema => ConsumeBuffQualificationSchema.Instance;
        protected override void Execute(object triggerArgs, ConsumeBuffQualificationArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var input) ||
                !ctx.Context.TryResolve<MobaSkillCastRuntimeService>(out var casts) ||
                !ctx.Context.TryResolve<MobaActorLookupService>(out var actors) ||
                !ctx.Context.TryResolve<MobaBuffService>(out var buffs) ||
                !ctx.Context.TryResolve<MobaTriggerExecutionGateway>(out var triggers))
            { LogRejected(ctx, "requires skill contact execution context and services"); return; }
            var effect = new MobaEffectActionInput(in input);
            if (!effect.HasCasterActor || !effect.HasTargetActor ||
                !actors.TryGetActorEntity(effect.CasterActorId, out var owner)) return;
            var handle = input.ExecutionContext.SkillRuntimeHandle;
            if (!casts.TryClaimBuffQualification(in handle, owner, out var buff, out var source, out var context)) return;
            if (!buffs.RemoveBuffInstanceImmediate(effect.CasterActorId, buff, source, context, MobaExecutionEndReason.Completed))
                throw new InvalidOperationException("Claimed buff qualification could not be removed.");
            var request = MobaTriggerExecutionRequest<object>.Create(args.HitTriggerId, triggerArgs, "skill.qualified_hit");
            triggers.ExecuteDirectTrigger(in request);
        }
    }
}
