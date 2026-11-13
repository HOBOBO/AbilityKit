using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.SpawnAreaBatch)]
    public sealed class SpawnAreaBatchPlanActionModule : MobaPlanActionModuleBase<SpawnAreaBatchArgs, SpawnAreaBatchPlanActionModule>
    {
        protected override IActionSchema<SpawnAreaBatchArgs, IWorldResolver> Schema => SpawnAreaBatchSchema.Instance;

        protected override void Execute(object triggerArgs, SpawnAreaBatchArgs args, ExecCtx<IWorldResolver> ctx)
        {
            ExecutePattern(triggerArgs, args, ctx, ActionName);
        }

        internal void ExecutePattern(object triggerArgs, SpawnAreaBatchArgs args, ExecCtx<IWorldResolver> ctx, string actionName)
        {
            actionName = string.IsNullOrWhiteSpace(actionName) ? TriggeringConstants.Actions.SpawnAreaBatch : actionName;
            if (args.AreaId <= 0)
            {
                Reject(ctx, actionName, "requires area_id > 0.");
                return;
            }
            if (args.PatternCount <= 0 || args.PatternCount > 64)
            {
                Reject(ctx, actionName, "pattern_count must be in the range 1..64.");
                return;
            }
            if (args.ClaimTargetsWithinGroup && args.GroupKey <= 0)
            {
                Reject(ctx, actionName, "group_key must be positive when claim_targets_within_group is enabled.");
                return;
            }

            if (!ctx.Context.TryResolve<IProjectileService>(out var projectiles) || projectiles == null)
            {
                Reject(ctx, actionName, "cannot resolve IProjectileService.");
                return;
            }
            if (!ctx.Context.TryResolve<MobaConfigDatabase>(out var configs) || configs == null ||
                !configs.TryGetAoe(args.AreaId, out var aoe) || aoe == null)
            {
                Reject(ctx, actionName, $"cannot resolve area config. areaId={args.AreaId}");
                return;
            }
            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var coreInput))
            {
                Reject(ctx, actionName, "requires combat execution context.");
                return;
            }
            var input = MobaPlanActionInputAssembler.AssembleSummon(in coreInput, ctx);
            if (!input.HasCasterActor)
            {
                Reject(ctx, actionName, "requires caster actor.");
                return;
            }
            if (!SpawnAreaPlanActionModule.TryResolveRuntimeDependencies(ctx.Context, out var areaRuntime, out var executionContexts, out var dependencyFailure))
            {
                Reject(ctx, actionName, dependencyFailure);
                return;
            }

            var radius = args.RadiusOverride > 0f ? args.RadiusOverride : aoe.Radius;
            var durationMs = args.DurationMs > 0 ? args.DurationMs : aoe.DurationMs;
            var intervalMs = aoe.IntervalMs;
            if (ctx.Context.TryResolve<MobaSkillParamModifierService>(out var paramModifiers) && paramModifiers != null)
            {
                ctx.Context.TryResolve<MobaSkillCastRuntimeService>(out var skillRuntimes);
                var skillHandle = coreInput.ExecutionContext.SkillRuntimeHandle;
                var record = MobaParameterRecord.FromSkillRuntime(MobaParameterStage.AreaSpawn,
                    skillRuntimes, in skillHandle, areaTemplateId: args.AreaId);
                radius = paramModifiers.Aoe.ResolveRadius(input.CasterActorId, radius, in record);
                if (args.DurationFrames <= 0)
                    durationMs = paramModifiers.Aoe.ResolveDurationMs(input.CasterActorId, durationMs, in record);
                if (args.StayIntervalFrames <= 0)
                    intervalMs = paramModifiers.Aoe.ResolveIntervalMs(input.CasterActorId, intervalMs, in record);
            }
            if (!IsFinitePositive(radius))
            {
                Reject(ctx, actionName, $"resolved area radius must be positive. areaId={args.AreaId} radius={radius}");
                return;
            }

            var commonArgs = args.ToAreaArgs(args.Width, args.Length, anchorAtOrigin: false);
            int lifetimeFrames;
            int stayIntervalFrames;
            int delayFrames;
            int frame;
            try
            {
                lifetimeFrames = SpawnAreaPlanActionModule.ResolveLifetimeFrames(commonArgs, durationMs, ctx.Context);
                stayIntervalFrames = SpawnAreaPlanActionModule.ResolveStayIntervalFrames(commonArgs, intervalMs, ctx.Context);
                delayFrames = SpawnAreaPlanActionModule.ResolveDelayFrames(aoe.DelayMs, ctx.Context);
                frame = SpawnAreaPlanActionModule.ResolveFrame(ctx.Context);
            }
            catch (Exception ex)
            {
                Reject(ctx, actionName, ex.Message);
                return;
            }

            var aimForward = new Vec3(input.AimDirection.X, 0f, input.AimDirection.Z);
            var areaForward = input.HasAimDirection ? DeterministicMathBridge.Normalize(in aimForward) : Vec3.Forward;
            if (areaForward.SqrMagnitude <= 0.000001f) areaForward = Vec3.Forward;
            var lateral = new Vec3(areaForward.Z, 0f, -areaForward.X);
            var offset = new Vec3(aoe.OffsetX + args.OffsetX, aoe.OffsetY + args.OffsetY, aoe.OffsetZ + args.OffsetZ)
                       + areaForward * args.DirectionOffset + lateral * args.LateralOffset;

            if (args.HasTargetRequest)
            {
                    if (!ctx.Context.TryResolve<MobaActorLookupService>(out var actors) || actors == null)
                    {
                        Reject(ctx, actionName, "cannot resolve MobaActorLookupService for target area batch.");
                        return;
                    }
                    var targets = PooledMobaPlanActionLists.GetIntList();
                    try
                    {
                        var effectInput = new MobaEffectActionInput(in coreInput);
                        if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx,
                                TriggeringConstants.Actions.SpawnAreaBatch, targets))
                        {
                            Reject(ctx, actionName, "target query returned no actor.");
                            return;
                        }
                        var spawned = 0;
                        var firstRuntimeAreaId = 0;
                        for (var i = 0; i < targets.Count; i++)
                        {
                            if (!MobaTemporaryEntityPositionResolver.TryGetActorPosition(actors, targets[i], out var targetCenter)) continue;
                            targetCenter = targetCenter + offset;
                            spawned += SpawnBatchAtCenter(projectiles, areaRuntime, executionContexts, commonArgs, aoe, input,
                                ctx, in targetCenter, radius, lifetimeFrames, stayIntervalFrames, delayFrames, frame,
                                in args, targets[i], ref firstRuntimeAreaId, actionName);
                        }
                        WriteResult(ctx, args, spawned, firstRuntimeAreaId, actionName);
                    }
                    finally
                    {
                        PooledMobaPlanActionLists.Release(targets);
                    }
                return;
            }

            if (!input.TryResolveSpawnPosition((SpawnSummonPositionMode)args.PositionMode, out var center))
            {
                Reject(ctx, actionName, $"cannot resolve spawn position. mode={args.PositionMode}");
                return;
            }
            center = center + offset;
            var firstId = 0;
            var total = SpawnBatchAtCenter(projectiles, areaRuntime, executionContexts, commonArgs, aoe, input,
                ctx, in center, radius, lifetimeFrames, stayIntervalFrames, delayFrames, frame,
                in args, input.TargetActorId, ref firstId, actionName);
            WriteResult(ctx, args, total, firstId, actionName);
        }

        private int SpawnBatchAtCenter(
            IProjectileService projectiles,
            MobaAreaRuntimeService areaRuntime,
            MobaExecutionContextRegistry executionContexts,
            SpawnAreaArgs commonArgs,
            AbilityKit.Demo.Moba.Config.BattleDemo.MO.AoeMO aoe,
            MobaSummonActionInput input,
            ExecCtx<IWorldResolver> ctx,
            in Vec3 center,
            float radius,
            int lifetimeFrames,
            int stayIntervalFrames,
            int delayFrames,
            int frame,
            in SpawnAreaBatchArgs patternArgs,
            int targetActorId,
            ref int firstRuntimeAreaId,
            string actionName)
        {
            var spawned = 0;
            for (var i = 0; i < patternArgs.PatternCount; i++)
            {
                if (!SpawnAreaPatternResolver.TryResolve(in patternArgs, i, out var element, out var patternError))
                {
                    Reject(ctx, actionName, patternError);
                    break;
                }
                var elementCenter = center + element.Offset;
                var edgeArgs = new SpawnAreaArgs(
                    commonArgs.AreaId, commonArgs.PositionMode, commonArgs.RadiusOverride,
                    commonArgs.DurationFrames, commonArgs.DurationMs, commonArgs.StayIntervalFrames,
                    commonArgs.CollisionLayerMaskOverride, commonArgs.OffsetX, commonArgs.OffsetY, commonArgs.OffsetZ,
                    (int)element.ShapeKind, 0f, element.Width, element.Length,
                    commonArgs.InitialRadius, commonArgs.RadiusGrowthPerFrame, false,
                    commonArgs.GroupKey, commonArgs.ClaimTargetsWithinGroup,
                    false, default, default, default, 0f, 0f);
                if (!SpawnAreaPlanActionModule.SpawnOneArea(new SpawnAreaPlanActionModule(), projectiles, areaRuntime, executionContexts,
                        edgeArgs, aoe, input, ctx, in elementCenter, in element.Forward, targetActorId,
                        radius, lifetimeFrames, 0, stayIntervalFrames, delayFrames, frame, out var runtimeAreaId))
                    continue;
                if (firstRuntimeAreaId == 0) firstRuntimeAreaId = runtimeAreaId;
                spawned++;
            }
            return spawned;
        }

        private static void WriteResult(ExecCtx<IWorldResolver> ctx, SpawnAreaBatchArgs args, int count, int firstRuntimeAreaId, string actionName)
        {
            if (count <= 0)
            {
                Reject(ctx, actionName, $"batch spawn produced no areas. areaId={args.AreaId}");
                return;
            }
            if (!MobaPlanActionOutput.TryWrite(in ctx, in args.ResultTarget, firstRuntimeAreaId, out var outputError) ||
                !MobaPlanActionOutput.TryWrite(in ctx, in args.ResultCountTarget, count, out outputError))
            {
                Reject(ctx, actionName, outputError);
                return;
            }
            MobaPlanActionDiagnostics.Applied(ctx.Context, actionName,
                $"templateId={args.AreaId} count={count} firstRuntimeId={firstRuntimeAreaId}");
        }

        private static void Reject(ExecCtx<IWorldResolver> ctx, string actionName, string reason)
        {
            ctx.Control?.RejectAction(reason);
            MobaPlanActionDiagnostics.Rejected(ctx.Context, actionName, reason);
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
