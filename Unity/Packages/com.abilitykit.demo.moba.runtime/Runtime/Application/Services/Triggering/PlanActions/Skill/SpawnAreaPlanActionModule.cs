using System;
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
using AoeMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.AoeMO;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.SpawnArea)]
    public sealed class SpawnAreaPlanActionModule : MobaPlanActionModuleBase<SpawnAreaArgs, SpawnAreaPlanActionModule>
    {
        protected override IActionSchema<SpawnAreaArgs, IWorldResolver> Schema => SpawnAreaSchema.Instance;

        [MobaModifierConsumes(MobaSkillParamModifierKeys.Aoe.Ids.Radius,
            MobaParameterStage.AreaSpawn, MobaModifierSampleTiming.CreateSnapshot)]
        [MobaModifierConsumes(MobaSkillParamModifierKeys.Aoe.Ids.DurationMs,
            MobaParameterStage.AreaSpawn, MobaModifierSampleTiming.CreateSnapshot)]
        [MobaModifierConsumes(MobaSkillParamModifierKeys.Aoe.Ids.IntervalMs,
            MobaParameterStage.AreaSpawn, MobaModifierSampleTiming.CreateSnapshot)]
        protected override void Execute(object triggerArgs, SpawnAreaArgs args, ExecCtx<IWorldResolver> ctx)
        {
            LogInvestigation(ctx,
                $"begin areaId={args.AreaId} positionMode={(SpawnSummonPositionMode)args.PositionMode} radiusOverride={args.RadiusOverride:0.###} durationMs={args.DurationMs} durationFrames={args.DurationFrames}");

            if (!ctx.Context.TryResolve<IProjectileService>(out var projectiles) || projectiles == null)
            {
                LogRejected(ctx, "cannot resolve IProjectileService.");
                return;
            }

            if (!ctx.Context.TryResolve<MobaConfigDatabase>(out var configs) || configs == null || !configs.TryGetAoe(args.AreaId, out var aoe) || aoe == null)
            {
                LogRejected(ctx, $"cannot resolve area config. areaId={args.AreaId}");
                return;
            }

            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var coreInput))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            var input = MobaPlanActionInputAssembler.AssembleSummon(in coreInput, ctx);
            var effectInput = new MobaEffectActionInput(in coreInput);
            LogInvestigation(ctx,
                $"resolved input caster={input.CasterActorId} hasCaster={input.HasCasterActor} target={input.TargetActorId} hasTarget={input.HasTargetActor} hasExecutionScope={input.HasExecutionScope} hasAimPos={input.HasAimPosition} hasAimDir={input.HasAimDirection} hasTargetRequest={args.HasTargetRequest}");
            if (!input.HasCasterActor)
            {
                LogRejected(ctx, "requires caster actor.");
                return;
            }

            if (!TryResolveRuntimeDependencies(ctx.Context, out var areaRuntime, out var executionContexts, out var dependencyFailure))
            {
                LogRejected(ctx, dependencyFailure);
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
                    skillRuntimes, in skillHandle,
                    projectileTemplateId: coreInput.ExecutionContext.ProjectileTemplateId,
                    areaTemplateId: args.AreaId);
                radius = paramModifiers.Aoe.ResolveRadius(input.CasterActorId, radius, in record);
                if (args.DurationFrames <= 0)
                    durationMs = paramModifiers.Aoe.ResolveDurationMs(input.CasterActorId, durationMs, in record);
                if (args.StayIntervalFrames <= 0)
                    intervalMs = paramModifiers.Aoe.ResolveIntervalMs(input.CasterActorId, intervalMs, in record);
            }
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
            {
                LogRejected(ctx, $"resolved area radius must be positive. areaId={args.AreaId} radius={radius}");
                return;
            }
            var lifetimeFrames = ResolveLifetimeFrames(args, durationMs, ctx.Context);
            var collisionLayerMask = args.CollisionLayerMaskOverride != 0 ? args.CollisionLayerMaskOverride : aoe.CollisionLayerMask;
            if (intervalMs < 0)
            {
                LogRejected(ctx, $"resolved area interval cannot be negative. areaId={args.AreaId} intervalMs={intervalMs}");
                return;
            }
            var stayIntervalFrames = ResolveStayIntervalFrames(args, intervalMs, ctx.Context);
            var delayFrames = ResolveDelayFrames(aoe.DelayMs, ctx.Context);
            var frame = ResolveFrame(ctx.Context);
            var aimDirection = new Vec3(input.AimDirection.X, 0f, input.AimDirection.Z);
            var areaForward = input.HasAimDirection
                ? DeterministicMathBridge.Normalize(in aimDirection)
                : Vec3.Forward;
            if (areaForward.SqrMagnitude <= 0.000001f) areaForward = Vec3.Forward;
            var offset = new Vec3(aoe.OffsetX + args.OffsetX, aoe.OffsetY + args.OffsetY, aoe.OffsetZ + args.OffsetZ)
                       + areaForward * args.DirectionOffset
                       + new Vec3(areaForward.Z, 0f, -areaForward.X) * args.LateralOffset;

            if (!args.HasTargetRequest)
            {
                var positionMode = (SpawnSummonPositionMode)args.PositionMode;
                if (!input.TryResolveSpawnPosition(positionMode, out var center))
                {
                    AbilityKit.Core.Logging.Log.Warning($"[SpawnAreaPlanActionModule] rejected resolve spawn position areaId={args.AreaId} mode={positionMode} caster={input.CasterActorId} target={input.TargetActorId} hasAimPos={input.HasAimPosition} hasAimDir={input.HasAimDirection} aimPos=({input.AimPosition.X:0.###},{input.AimPosition.Y:0.###},{input.AimPosition.Z:0.###}) aimDir=({input.AimDirection.X:0.###},{input.AimDirection.Y:0.###},{input.AimDirection.Z:0.###})");
                    LogRejected(ctx, $"cannot resolve spawn position. mode={positionMode} caster={input.CasterActorId} target={input.TargetActorId} hasAimPos={input.HasAimPosition} hasAimDir={input.HasAimDirection}");
                    return;
                }

                center = center + offset;
                LogInvestigation(ctx,
                    $"resolved area params center=({center.X:0.###},{center.Y:0.###},{center.Z:0.###}) radius={radius:0.###} lifetimeFrames={lifetimeFrames} stayIntervalFrames={stayIntervalFrames} delayFrames={delayFrames} collisionMask={collisionLayerMask} frame={frame}");
                if (SpawnOneArea(this, projectiles, areaRuntime, executionContexts, args, aoe, input, ctx, in center, in areaForward, input.TargetActorId, radius, lifetimeFrames, collisionLayerMask, stayIntervalFrames, delayFrames, frame, out var runtimeAreaId) &&
                    (!MobaPlanActionOutput.TryWrite(in ctx, in args.ResultTarget, runtimeAreaId, out var outputError) ||
                     !MobaPlanActionOutput.TryWrite(in ctx, in args.ResultCountTarget, 1, out outputError)))
                {
                    LogRejected(ctx, outputError);
                }
                return;
            }

            if (!ctx.Context.TryResolve<MobaActorLookupService>(out var actors) || actors == null)
            {
                LogRejected(ctx, "cannot resolve MobaActorLookupService for target area spawn.");
                return;
            }

            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, TriggeringConstants.Actions.SpawnArea, targets) || targets.Count == 0)
                {
                    LogRejected(ctx, $"target query returned no actor. areaId={args.AreaId} queryId={args.TargetRequest.QueryTemplateId} caster={input.CasterActorId}");
                    return;
                }

                var spawned = 0;
                var firstRuntimeAreaId = 0;
                for (int i = 0; i < targets.Count; i++)
                {
                    var targetActorId = targets[i];
                    if (!MobaTemporaryEntityPositionResolver.TryGetActorPosition(actors, targetActorId, out var center))
                    {
                        LogRejected(ctx, $"cannot resolve target actor position. areaId={args.AreaId} target={targetActorId}");
                        continue;
                    }

                    center = center + offset;
                    LogInvestigation(ctx,
                        $"resolved target area params target={targetActorId} center=({center.X:0.###},{center.Y:0.###},{center.Z:0.###}) radius={radius:0.###} lifetimeFrames={lifetimeFrames} stayIntervalFrames={stayIntervalFrames} delayFrames={delayFrames} collisionMask={collisionLayerMask} frame={frame}");
                    if (SpawnOneArea(this, projectiles, areaRuntime, executionContexts, args, aoe, input, ctx, in center, in areaForward, targetActorId, radius, lifetimeFrames, collisionLayerMask, stayIntervalFrames, delayFrames, frame, out var runtimeAreaId))
                    {
                        if (firstRuntimeAreaId == 0) firstRuntimeAreaId = runtimeAreaId;
                        spawned++;
                    }
                }

                if (spawned == 0)
                {
                    LogRejected(ctx, $"target area spawn produced no area. areaId={args.AreaId} targets={targets.Count}");
                }
                else if (!MobaPlanActionOutput.TryWrite(in ctx, in args.ResultTarget, firstRuntimeAreaId, out var outputError) ||
                         !MobaPlanActionOutput.TryWrite(in ctx, in args.ResultCountTarget, spawned, out outputError))
                {
                    LogRejected(ctx, outputError);
                }
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }

        internal static bool SpawnOneArea(
            SpawnAreaPlanActionModule logger,
            IProjectileService projectiles,
            MobaAreaRuntimeService areaRuntime,
            MobaExecutionContextRegistry executionContexts,
            SpawnAreaArgs args,
            AoeMO aoe,
            MobaSummonActionInput input,
            ExecCtx<IWorldResolver> ctx,
            in Vec3 center,
            in Vec3 forward,
            int targetActorId,
            float radius,
            int lifetimeFrames,
            int collisionLayerMask,
            int stayIntervalFrames,
            int delayFrames,
            int frame,
            out int runtimeAreaId)
        {
            runtimeAreaId = 0;
            var origin = input.BuildOrigin(input.CasterActorId, targetActorId, MobaExecutionKind.AreaSpawn, args.AreaId);
            var skillRuntimeHandle = origin.SkillRuntimeHandle;
            logger.LogInvestigation(ctx,
                $"resolved origin immediate={origin.ImmediateContextId} parent={origin.EffectiveParentContextId} root={origin.EffectiveRootContextId} owner={origin.OwnerContextId} target={targetActorId}");
            if (origin.EffectiveParentContextId == 0L)
            {
                AbilityKit.Core.Logging.Log.Warning($"[SpawnAreaPlanActionModule] rejected missing source context areaId={args.AreaId} caster={input.CasterActorId} target={targetActorId} immediate={origin.ImmediateContextId} parent={origin.EffectiveParentContextId} root={origin.EffectiveRootContextId} owner={origin.OwnerContextId}");
                logger.LogRejected(ctx, $"requires source context. areaId={args.AreaId} caster={input.CasterActorId} target={targetActorId}");
                return false;
            }

            var areaNode = executionContexts.Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.AreaSpawn,
                args.AreaId,
                input.CasterActorId,
                targetActorId,
                origin.EffectiveParentContextId,
                origin.EffectiveRootContextId,
                origin.OwnerContextId,
                frame));
            var sourceContextId = areaNode.ContextId;
            if (sourceContextId == 0L)
            {
                AbilityKit.Core.Logging.Log.Warning($"[SpawnAreaPlanActionModule] rejected area context creation failed areaId={args.AreaId} caster={input.CasterActorId} target={targetActorId} parent={origin.EffectiveParentContextId}");
                logger.LogRejected(ctx, $"area context creation failed. areaId={args.AreaId} caster={input.CasterActorId} target={targetActorId}");
                return false;
            }

            var rootContextId = origin.EffectiveRootContextId != 0L
                ? origin.EffectiveRootContextId
                : sourceContextId;
            var ownerContextId = origin.OwnerContextId != 0L
                ? origin.OwnerContextId
                : sourceContextId;

            logger.LogInvestigation(ctx,
                $"resolved area context source={sourceContextId} parent={origin.EffectiveParentContextId} root={rootContextId} owner={ownerContextId} target={targetActorId}");

            var shapeKind = args.ShapeKind < (int)OverlapShapeKind.Circle || args.ShapeKind > (int)OverlapShapeKind.Ring
                ? OverlapShapeKind.Circle
                : (OverlapShapeKind)args.ShapeKind;
            var initialRadius = args.InitialRadius > 0f ? args.InitialRadius : radius;
            var innerRadius = args.InnerRadius > 0f ? args.InnerRadius : aoe.InnerRadius;
            var width = args.Width > 0f ? args.Width : radius * 2f;
            var length = args.Length > 0f ? args.Length : radius * 2f;
            var spawnParams = new AreaSpawnParams(
                input.CasterActorId,
                in center,
                radius,
                lifetimeFrames,
                collisionLayerMask,
                stayIntervalFrames,
                shapeKind,
                forward,
                args.HalfAngleDeg,
                width,
                length,
                args.AnchorAtOrigin,
                args.RadiusGrowthPerFrame,
                initialRadius,
                armDelayFrames: delayFrames,
                consumeOnFirstEnter: aoe.ConsumeOnFirstEnter,
                followOwner: aoe.AttachMode == 1,
                followActorId: input.CasterActorId,
                rootContextId: rootContextId,
                groupKey: args.GroupKey,
                claimTargetsWithinGroup: args.ClaimTargetsWithinGroup,
                innerRadius: innerRadius,
                boundarySweep: aoe.OnCrossTriggerIds.Length > 0);
            var areaId = projectiles.SpawnArea(in spawnParams, frame);
            if (areaId.Value <= 0)
            {
                if (sourceContextId != 0L)
                {
                    executionContexts.End(sourceContextId, (int)MobaExecutionEndReason.Failed, frame);
                }

                AbilityKit.Core.Logging.Log.Warning($"[SpawnAreaPlanActionModule] rejected spawn failed areaId={args.AreaId} caster={input.CasterActorId} target={targetActorId} center=({center.X:0.###},{center.Y:0.###},{center.Z:0.###}) radius={radius:0.###} lifetimeFrames={lifetimeFrames} collisionMask={collisionLayerMask} frame={frame}");
                logger.LogRejected(ctx, $"spawn failed. areaId={args.AreaId} caster={input.CasterActorId} target={targetActorId} center=({center.X:0.###},{center.Y:0.###},{center.Z:0.###}) radius={radius:0.###} lifetimeFrames={lifetimeFrames}");
                return false;
            }

            try
            {
                areaRuntime.RegisterSpawn(
                    areaId,
                    args.AreaId,
                    input.CasterActorId,
                    in center,
                    radius,
                    collisionLayerMask,
                    aoe.MaxTargets,
                    frame,
                    delayFrames,
                    sourceContextId,
                    rootContextId,
                    ownerContextId,
                    (int)shapeKind,
                    in forward,
                    args.HalfAngleDeg,
                    width,
                    length,
                    skillRuntimeHandle,
                    args.GroupKey,
                    args.ClaimTargetsWithinGroup,
                    innerRadius);
            }
            catch (Exception ex)
            {
                // Failed registration must not enqueue AreaExpireEvent: the runtime
                // metadata is removed as part of rollback, so the sync system would
                // otherwise receive an event it can no longer resolve.
                if (projectiles is IAreaRollbackService rollbackAreas)
                {
                    rollbackAreas.CancelArea(areaId);
                }
                else
                {
                    // Compatibility fallback for custom projectile services.
                    projectiles.DespawnArea(areaId, frame);
                }
                areaRuntime.RollbackSpawn(areaId, sourceContextId);
                try
                {
                    executionContexts.End(sourceContextId, (int)MobaExecutionEndReason.Failed, frame);
                }
                catch (Exception cleanupEx)
                {
                    AbilityKit.Core.Logging.Log.Exception(cleanupEx, $"[SpawnAreaPlanActionModule] end area context failed during spawn rollback. areaId={areaId.Value} sourceContextId={sourceContextId}");
                }

                AbilityKit.Core.Logging.Log.Exception(ex, $"[SpawnAreaPlanActionModule] register spawn failed and was rolled back. areaId={areaId.Value} templateId={args.AreaId} caster={input.CasterActorId} target={targetActorId}");
                logger.LogRejected(ctx, $"runtime registration failed. areaId={args.AreaId} runtimeId={areaId.Value}");
                return false;
            }

            logger.LogApplied(ctx, $"templateId={args.AreaId} runtimeId={areaId.Value} caster={input.CasterActorId} target={targetActorId} radius={radius} lifetimeFrames={lifetimeFrames}");
            runtimeAreaId = areaId.Value;
            return true;
        }

        internal static bool TryResolveRuntimeDependencies(
            IWorldResolver services,
            out MobaAreaRuntimeService areaRuntime,
            out MobaExecutionContextRegistry executionContexts,
            out string failure)
        {
            areaRuntime = null;
            executionContexts = null;
            if (services == null)
            {
                failure = "requires world services for area runtime registration.";
                return false;
            }

            if (!services.TryResolve(out areaRuntime) || areaRuntime == null)
            {
                failure = "cannot resolve mandatory MobaAreaRuntimeService.";
                return false;
            }

            if (!services.TryResolve(out executionContexts) || executionContexts == null)
            {
                failure = "cannot resolve mandatory MobaExecutionContextRegistry.";
                return false;
            }

            failure = null;
            return true;
        }

        /// <summary>秒→帧数：FrameTime 走定点直达（不经 float Time 中转）；测试替身回退旧路径。</summary>
        private static int SecondsToFrameCount(IFrameTime frameTime, float seconds)
        {
            if (frameTime is FrameTime fixedTime)
            {
                return fixedTime.FrameAfterSeconds(seconds).Value - fixedTime.Frame.Value;
            }

            return frameTime.TimeToFrame(frameTime.Time + seconds).Value - frameTime.Frame.Value;
        }

        internal static int ResolveLifetimeFrames(SpawnAreaArgs args, int durationMs, IWorldResolver services)
        {
            if (args.DurationFrames > 0) return args.DurationFrames;
            if (durationMs <= 0)
            {
                throw new InvalidOperationException($"SpawnArea requires a positive duration. areaId={args.AreaId}");
            }

            var frameTime = ResolveFrameTime(services);
            return Math.Max(1, SecondsToFrameCount(frameTime, durationMs / 1000f));
        }

        internal static int ResolveDelayFrames(int configDelayMs, IWorldResolver services)
        {
            if (configDelayMs <= 0) return 0;

            var frameTime = ResolveFrameTime(services);
            return Math.Max(1, SecondsToFrameCount(frameTime, configDelayMs / 1000f));
        }

        internal static int ResolveStayIntervalFrames(SpawnAreaArgs args, int configIntervalMs, IWorldResolver services)
        {
            if (args.StayIntervalFrames > 0) return args.StayIntervalFrames;
            if (configIntervalMs <= 0) return 0;

            var frameTime = ResolveFrameTime(services);
            return Math.Max(1, SecondsToFrameCount(frameTime, configIntervalMs / 1000f));
        }

        internal static int ResolveFrame(IWorldResolver services)
        {
            return ResolveFrameTime(services).Frame.Value;
        }

        private static IFrameTime ResolveFrameTime(IWorldResolver services)
        {
            if (services != null && services.TryResolve<IFrameTime>(out var frameTime) && frameTime != null)
            {
                return frameTime;
            }

            throw new InvalidOperationException("SpawnArea requires IFrameTime for deterministic frame resolution.");
        }
    }
}
