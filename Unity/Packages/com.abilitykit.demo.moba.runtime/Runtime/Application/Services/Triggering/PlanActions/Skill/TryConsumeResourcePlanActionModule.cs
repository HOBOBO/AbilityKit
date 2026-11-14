using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct TryConsumeResourceArgs
    {
        public readonly ResourceType ResourceType;
        public readonly float Amount;
        public readonly int DeathCastSkillId;
        public readonly BlackboardWriteTarget Result;
        public TryConsumeResourceArgs(ResourceType resourceType, float amount, int deathCastSkillId, BlackboardWriteTarget result)
        { ResourceType = resourceType; Amount = amount; DeathCastSkillId = deathCastSkillId; Result = result; }
    }

    public sealed class TryConsumeResourceSchema : MobaPlanActionSchemaBase<TryConsumeResourceArgs>
    {
        public static readonly TryConsumeResourceSchema Instance = new TryConsumeResourceSchema();
        protected override string ActionName => "try_consume_resource";
        public override TryConsumeResourceArgs ParseArgs(Dictionary<string, ActionArgValue> args, ExecCtx<IWorldResolver> ctx)
        {
            TryReadBlackboardTarget(args, out var result, "result");
            return new TryConsumeResourceArgs(ReadEnum(args, ctx, ResourceType.None, "resource_type"),
                ReadFloat(args, ctx, 0, "amount"), ReadInt(args, ctx, 0, "death_cast_skill_id"), result);
        }
        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
            => RequireAny(args, "resource_type", out error, "resource_type") &&
               RequireAny(args, "amount", out error, "amount") && RequireAny(args, "result", out error, "result");
    }

    /// <summary>Standalone periodic payment. Never skips payment because the originating Cast has an economy transaction.</summary>
    [PlanActionModule(order: 10)]
    public sealed class TryConsumeResourcePlanActionModule : MobaPlanActionModuleBase<TryConsumeResourceArgs, TryConsumeResourcePlanActionModule>
    {
        protected override IActionSchema<TryConsumeResourceArgs, IWorldResolver> Schema => TryConsumeResourceSchema.Instance;
        protected override void Execute(object triggerArgs, TryConsumeResourceArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.ResourceType == ResourceType.None || args.ResourceType == ResourceType.Hp ||
                args.Amount < 0 || float.IsNaN(args.Amount) || float.IsInfinity(args.Amount))
            { LogRejected(ctx, "Invalid periodic resource payment."); return; }
            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var input) || !input.HasCasterActor ||
                !ctx.Context.TryResolve<MobaActorLookupService>(out var actors) ||
                !actors.TryGetActorEntity(input.CasterActorId, out var actor) ||
                !ctx.Context.TryResolve<MobaCombatRulesService>(out var rules))
            { LogRejected(ctx, "Periodic payment requires a caster and combat rules."); return; }
            var paid = TryPay(actor, rules.IsAlive(actor), args.ResourceType, args.Amount, args.DeathCastSkillId);
            if (!MobaPlanActionOutput.TryWrite(in ctx, in args.Result, paid ? 1 : 0, out var writeError))
                LogRejected(ctx, writeError);
        }

        public static bool TryPay(global::ActorEntity actor, bool alive, ResourceType resourceType, float amount, int deathCastSkillId)
        {
            if (actor == null || resourceType == ResourceType.None || resourceType == ResourceType.Hp ||
                !Enum.IsDefined(typeof(ResourceType), resourceType) || amount < 0 || float.IsNaN(amount) || float.IsInfinity(amount))
                return false;
            if (!alive)
                return deathCastSkillId > 0 && actor.hasCharacterHfsm &&
                    actor.characterHfsm.Runtime?.DeathCasting?.CanCast(actor, deathCastSkillId, admission: false) == true;
            return MobaResourceMutation.TryConsume(actor, resourceType, MobaResourceFixedConvert.ToFixed(amount), out _, out _);
        }
    }
}
