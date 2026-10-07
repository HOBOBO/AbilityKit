using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Core.Mathematics;


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

            if (!input.HasCasterActor)
            {
                LogRejected(ctx, "requires caster actor.");
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
                LogRejected(ctx, "requires summon_id > 0.");
                return;
            }
            var positionMode = (SpawnSummonPositionMode)args.PositionMode;
            if (!input.TryResolveSpawnPosition(positionMode, out var spawnPos))
            {
                LogRejected(ctx, $"cannot resolve spawn position. mode={positionMode}");
                return;
            }

            var forward = input.HasAimDirection ? input.AimDirection : Vec3.Forward;
            var sourceContext = input.CreateSourceContext(casterActorId, summonId);
            if (float.IsNaN(args.DurationMs) || float.IsInfinity(args.DurationMs) || args.DurationMs < 0f || args.DurationMs > int.MaxValue)
            {
                LogRejected(ctx, "invalid summon duration_ms.");
                return;
            }
            var durationOverrideMs = (int)System.Math.Round(args.DurationMs);
            if (summonSvc.TrySummon(casterActorId, summonId, in spawnPos, in forward,
                    in sourceContext, out var summonActorId, durationOverrideMs))
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
    }
}
