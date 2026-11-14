using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct SourceBuffArgs
    {
        public readonly int BuffId;
        public readonly bool Claim;
        public readonly MobaActionTargetRequest TargetRequest;
        public readonly MobaEffectMagnitudeSpec Magnitude;
        public readonly BlackboardWriteTarget MagnitudeResult;
        public readonly BlackboardWriteTarget Result, EligibleResult;
        public SourceBuffArgs(int buff, bool claim, BlackboardWriteTarget result, BlackboardWriteTarget eligible, MobaActionTargetRequest target, MobaEffectMagnitudeSpec magnitude, BlackboardWriteTarget magnitudeResult)
        { BuffId = buff; Claim = claim; Result = result; EligibleResult = eligible; TargetRequest = target; Magnitude = magnitude; MagnitudeResult = magnitudeResult; }
    }

    public sealed class SourceBuffSchema : MobaPlanActionSchemaBase<SourceBuffArgs>
    {
        public static readonly SourceBuffSchema Instance = new SourceBuffSchema();
        protected override string ActionName => AbilityKit.Demo.Moba.Systems.TriggeringConstants.Actions.SourceBuff;
        public override SourceBuffArgs ParseArgs(Dictionary<string, ActionArgValue> args, ExecCtx<IWorldResolver> ctx)
        {
            TryReadBlackboardTarget(args, out var result, "result");
            TryReadBlackboardTarget(args, out var eligible, "eligible_result");
            TryReadBlackboardTarget(args, out var magnitudeResult, "magnitude_result");
            return new SourceBuffArgs(ReadInt(args, ctx, 0, "buff_id"), ReadBool(args, ctx, false, "claim"), result, eligible, MobaActionTargetSchemaReader.Read(args, ctx, default), MobaEffectMagnitudeSchemaReader.Read(args, in ctx, 0f), magnitudeResult);
        }
        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error) =>
            RequireAny(args, "source Buff", out error, "buff_id");
    }

    /// <summary>Refresh or atomically claim a target Buff owned by the current caster.</summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.SourceBuff)]
    public sealed class SourceBuffPlanActionModule : MobaPlanActionModuleBase<SourceBuffArgs, SourceBuffPlanActionModule>
    {
        protected override IActionSchema<SourceBuffArgs, IWorldResolver> Schema => SourceBuffSchema.Instance;
        protected override void Execute(object triggerArgs, SourceBuffArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var input) ||
                !ctx.Context.TryResolve<MobaBuffService>(out var buffs) ||
                !ctx.Context.TryResolve<MobaCombatRulesService>(out var rules)) return;
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                var effectInput = new MobaEffectActionInput(in input);
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in input, in effectInput, ctx, "source_buff", targets)) return;
                foreach (var target in targets) Apply(target, input.CasterActorId, in input, args, ctx, buffs, rules);
            }
            finally { PooledMobaPlanActionLists.Release(targets); }
        }

        private void Apply(int target, int source, in MobaPlanActionInput input, SourceBuffArgs args,
            ExecCtx<IWorldResolver> ctx, MobaBuffService buffs, MobaCombatRulesService rules)
        {
            var eligible = source > 0 && target > 0 && rules.AreEnemies(source, target) &&
                rules.CanBeSearchedTarget(source, target).Passed && rules.CanReceiveDamage(source, target).Passed;
            var succeeded = false;
            if (eligible)
            {
                if (args.Claim)
                {
                    if (buffs.TryReadSourceBuff(target, args.BuffId, source, out var token))
                    {
                        var execution = input.ExecutionContext;
                        var value = 0f;
                        if (!args.Magnitude.Enabled || MobaEffectMagnitudeResolver.TryEvaluate(in args.Magnitude, in execution,
                            in ctx, source, source, target, default, out value, out _, out _))
                        {
                            succeeded = buffs.TryClaimSourceBuff(in token);
                            if (succeeded && !MobaPlanActionOutput.TryWrite(in ctx, in args.MagnitudeResult, value, out var writeError)) this.LogRejected(ctx, writeError);
                        }
                    }
                }
                else
                {
                    var effect = new MobaEffectActionInput(in input);
                    var origin = effect.BuildOrigin(source, target, MobaExecutionKind.EffectExecution, args.BuffId);
                    var buffOrigin = BuffOriginContext.FromOrigin(in origin);
                    succeeded = buffs.ApplyBuffImmediate(target, args.BuffId, source, 0, in buffOrigin);
                }
            }
            if (!MobaPlanActionOutput.TryWrite(in ctx, in args.Result, succeeded ? 1 : 0, out var error) ||
                !MobaPlanActionOutput.TryWrite(in ctx, in args.EligibleResult, eligible ? 1 : 0, out error)) this.LogRejected(ctx, error);
        }
    }
}
