using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Core.Mathematics;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.SpawnSummon)]
    public sealed class SpawnSummonPlanActionModule : MobaPlanActionModuleBase<SpawnSummonArgs, SpawnSummonPlanActionModule>
    {
        protected override IActionSchema<SpawnSummonArgs, IWorldResolver> Schema => SpawnSummonSchema.Instance;

        [MobaModifierConsumes(MobaSkillParamModifierKeys.Summon.Ids.SummonId,
            MobaParameterStage.SummonCreate, MobaModifierSampleTiming.CreateSnapshot)]
        protected override void Execute(object triggerArgs, SpawnSummonArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!TryResolveRequired(ctx, out MobaSummonService summonSvc))
            {
                return;
            }

            if (!MobaPlanActionInputResolver.TryResolveSummon(triggerArgs, ctx, out var input))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            var requestIdentity = input.ExecutionContext.ExecutionSnapshot.SummonInputIdentity;
            var requestTicket = summonSvc.ReserveInputRequest(in requestIdentity);
            if (summonSvc.TryGetInputRequestResult(in requestTicket, out var priorAccepted, out var priorActorId))
            {
                if (priorAccepted &&
                    (!MobaPlanActionOutput.TryWrite(in ctx, in args.ResultTarget, priorActorId, out var priorError) ||
                     !MobaPlanActionOutput.TryWrite(in ctx, in args.ResultCountTarget, 1, out priorError)))
                    LogRejected(ctx, priorError);
                return;
            }

            if (!input.HasCasterActor)
            {
                RejectRequest(summonSvc, in requestTicket, ctx, "requires caster actor.");
                return;
            }

            var casterActorId = input.CasterActorId;
            var summonId = args.SummonId;
            if (ctx.Context.TryResolve<MobaReferenceCandidateService>(out var references) && references != null)
            {
                ctx.Context.TryResolve<MobaSkillCastRuntimeService>(out var skillRuntimes);
                var skillHandle = input.ExecutionContext.SkillRuntimeHandle;
                var record = MobaParameterRecord.FromSkillRuntime(MobaParameterStage.SummonCreate,
                    skillRuntimes, in skillHandle);
                summonId = references.Resolve(casterActorId, MobaSkillParamModifierKeys.Summon.Ids.SummonId,
                    summonId, in record);
            }

            if (summonId <= 0)
            {
                RejectRequest(summonSvc, in requestTicket, ctx, "requires summon_id > 0.");
                return;
            }
            var positionMode = (SpawnSummonPositionMode)args.PositionMode;
            if (!input.TryResolveSpawnPosition(positionMode, out var spawnPos))
            {
                RejectRequest(summonSvc, in requestTicket, ctx, $"cannot resolve spawn position. mode={positionMode}");
                return;
            }

            var forward = input.HasAimDirection ? input.AimDirection : Vec3.Forward;
            var sourceContext = input.CreateSourceContext(casterActorId, summonId);
            var durationMs = args.DurationMs;
            if (!string.IsNullOrEmpty(args.DurationSkillLevelParameter))
            {
                var handle = input.ExecutionContext.SkillRuntimeHandle;
                if (!ctx.Context.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) || runtimes == null ||
                    !runtimes.TryGetLevelParameter(handle, args.DurationSkillLevelParameter, out durationMs))
                { RejectRequest(summonSvc, in requestTicket, ctx, "summon duration parameter is unavailable."); return; }
            }
            if (float.IsNaN(durationMs) || float.IsInfinity(durationMs) || durationMs < 0f || (double)durationMs > int.MaxValue)
            {
                RejectRequest(summonSvc, in requestTicket, ctx, "invalid summon duration_ms.");
                return;
            }
            List<KeyValuePair<BattleAttributeType, float>> birthAttributes = null;
            if (args.BirthAttributes != null)
            {
                birthAttributes = new List<KeyValuePair<BattleAttributeType, float>>(args.BirthAttributes.Length);
                foreach (var item in args.BirthAttributes)
                {
                    var magnitude = item.Magnitude;
                    var execution = input.ExecutionContext;
                    if (!MobaEffectMagnitudeResolver.TryEvaluate(in magnitude, in execution, in ctx,
                            casterActorId, casterActorId, input.TargetActorId, default,
                            out var value, out _, out var failure))
                    { RejectRequest(summonSvc, in requestTicket, ctx, failure); return; }
                    birthAttributes.Add(new KeyValuePair<BattleAttributeType, float>(item.Attribute, value));
                }
            }
            var durationOverrideMs = (int)System.Math.Round(durationMs);
            if (summonSvc.TrySummon(in requestTicket, casterActorId, summonId, in spawnPos, in forward,
                    in sourceContext, out var summonActorId, durationOverrideMs, birthAttributes))
            {
                if (!MobaPlanActionOutput.TryWrite(in ctx, in args.ResultTarget, summonActorId, out var outputError) ||
                    !MobaPlanActionOutput.TryWrite(in ctx, in args.ResultCountTarget, 1, out outputError))
                {
                    LogRejected(ctx, outputError);
                    return;
                }
                LogApplied(ctx, $"caster={casterActorId} summonId={summonId} summonActorId={summonActorId}");
            }
        }

        private void RejectRequest(MobaSummonService summonService,
            in MobaSummonRequestTicket ticket, ExecCtx<IWorldResolver> ctx, string reason)
        {
            summonService.RejectInputRequest(in ticket);
            LogRejected(ctx, reason);
        }
    }
}
