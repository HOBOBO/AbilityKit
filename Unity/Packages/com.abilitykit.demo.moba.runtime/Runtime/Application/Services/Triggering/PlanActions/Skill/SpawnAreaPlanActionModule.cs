using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.Projectile;
using AbilityKit.Trace;
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

            var coreInput = MobaPlanActionInputResolver.Resolve(triggerArgs, ctx);
            var input = MobaPlanActionInputAssembler.AssembleSummon(in coreInput, ctx);
            var effectInput = new MobaEffectActionInput(in coreInput);
            LogInvestigation(ctx,
                $"resolved input caster={input.CasterActorId} hasCaster={input.HasCasterActor} target={input.TargetActorId} hasTarget={input.HasTargetActor} hasTraceScope={input.HasTraceScope} hasAimPos={input.HasAimPosition} hasAimDir={input.HasAimDirection} hasTargetRequest={args.HasTargetRequest}");
            if (!input.HasCasterActor)
            {
                LogRejected(ctx, "requires caster actor.");
                return;
            }

            var radius = args.RadiusOverride > 0f ? args.RadiusOverride : aoe.Radius;
            var lifetimeFrames = ResolveLifetimeFrames(args, aoe.DurationMs, ctx.Context);
            var collisionLayerMask = args.CollisionLayerMaskOverride != 0 ? args.CollisionLayerMaskOverride : aoe.CollisionLayerMask;
            var stayIntervalFrames = ResolveStayIntervalFrames(args, aoe.IntervalMs, ctx.Context);
            var delayFrames = ResolveDelayFrames(aoe.DelayMs, ctx.Context);
            var frame = ResolveFrame(ctx.Context);
            var offset = new Vec3(aoe.OffsetX + args.OffsetX, aoe.OffsetY + args.OffsetY, aoe.OffsetZ + args.OffsetZ);
            ctx.Context.TryResolve<MobaAreaRuntimeService>(out var areaRuntime);
            ctx.Context.TryResolve<MobaTraceRegistry>(out var trace);

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
                SpawnOneArea(projectiles, areaRuntime, trace, args, aoe, input, ctx, in center, input.TargetActorId, radius, lifetimeFrames, collisionLayerMask, stayIntervalFrames, delayFrames, frame);
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
                for (int i = 0; i < targets.Count; i++)
                {
                    var targetActorId = targets[i];
                    if (!TryGetActorPosition(actors, targetActorId, out var center))
                    {
                        LogRejected(ctx, $"cannot resolve target actor position. areaId={args.AreaId} target={targetActorId}");
                        continue;
                    }

                    center = center + offset;
                    LogInvestigation(ctx,
                        $"resolved target area params target={targetActorId} center=({center.X:0.###},{center.Y:0.###},{center.Z:0.###}) radius={radius:0.###} lifetimeFrames={lifetimeFrames} stayIntervalFrames={stayIntervalFrames} delayFrames={delayFrames} collisionMask={collisionLayerMask} frame={frame}");
                    if (SpawnOneArea(projectiles, areaRuntime, trace, args, aoe, input, ctx, in center, targetActorId, radius, lifetimeFrames, collisionLayerMask, stayIntervalFrames, delayFrames, frame))
                    {
                        spawned++;
                    }
                }

                if (spawned == 0)
                {
                    LogRejected(ctx, $"target area spawn produced no area. areaId={args.AreaId} targets={targets.Count}");
                }
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }

        private bool SpawnOneArea(
            IProjectileService projectiles,
            MobaAreaRuntimeService areaRuntime,
            MobaTraceRegistry trace,
            SpawnAreaArgs args,
            AoeMO aoe,
            MobaSummonActionInput input,
            ExecCtx<IWorldResolver> ctx,
            in Vec3 center,
            int targetActorId,
            float radius,
            int lifetimeFrames,
            int collisionLayerMask,
            int stayIntervalFrames,
            int delayFrames,
            int frame)
        {
            var sourceContextId = 0L;
            var rootContextId = 0L;
            var ownerContextId = 0L;
            var skillRuntimeHandle = default(MobaSkillCastRuntimeHandle);
            if (areaRuntime != null)
            {
                var origin = input.BuildOrigin(input.CasterActorId, targetActorId, MobaTraceKind.AreaSpawn, args.AreaId);
                skillRuntimeHandle = origin.SkillRuntimeHandle;
                LogInvestigation(ctx,
                    $"resolved origin immediate={origin.ImmediateContextId} parent={origin.EffectiveParentContextId} root={origin.EffectiveRootContextId} owner={origin.OwnerContextId} target={targetActorId}");
                if (origin.EffectiveParentContextId == 0L)
                {
                    AbilityKit.Core.Logging.Log.Warning($"[SpawnAreaPlanActionModule] rejected missing source context areaId={args.AreaId} caster={input.CasterActorId} target={targetActorId} immediate={origin.ImmediateContextId} parent={origin.EffectiveParentContextId} root={origin.EffectiveRootContextId} owner={origin.OwnerContextId}");
                    LogRejected(ctx, $"requires source context. areaId={args.AreaId} caster={input.CasterActorId} target={targetActorId}");
                    return false;
                }

                sourceContextId = trace != null
                    ? trace.CreateChildContext(
                        origin.EffectiveParentContextId,
                        MobaTraceKind.AreaSpawn,
                        args.AreaId,
                        input.CasterActorId,
                        targetActorId,
                        TraceEndpoint.Config(MobaRuntimeKindNames.Area, args.AreaId),
                        TraceEndpoint.Actor(targetActorId))
                    : 0L;
                if (sourceContextId == 0L)
                {
                    sourceContextId = origin.ImmediateContextId != 0L
                        ? origin.ImmediateContextId
                        : origin.EffectiveParentContextId;
                }

                rootContextId = origin.EffectiveRootContextId != 0L
                    ? origin.EffectiveRootContextId
                    : sourceContextId;
                ownerContextId = origin.OwnerContextId != 0L
                    ? origin.OwnerContextId
                    : sourceContextId;

                LogInvestigation(ctx,
                    $"resolved area trace source={sourceContextId} parent={origin.EffectiveParentContextId} root={rootContextId} owner={ownerContextId} target={targetActorId}");
            }
            else
            {
                LogInvestigation(ctx, "area runtime not available; continuing without trace registration.");
            }

            var spawnParams = new AreaSpawnParams(input.CasterActorId, in center, radius, lifetimeFrames, collisionLayerMask, stayIntervalFrames);
            var areaId = projectiles.SpawnArea(in spawnParams, frame);
            if (areaId.Value <= 0)
            {
                AbilityKit.Core.Logging.Log.Warning($"[SpawnAreaPlanActionModule] rejected spawn failed areaId={args.AreaId} caster={input.CasterActorId} target={targetActorId} center=({center.X:0.###},{center.Y:0.###},{center.Z:0.###}) radius={radius:0.###} lifetimeFrames={lifetimeFrames} collisionMask={collisionLayerMask} frame={frame}");
                LogRejected(ctx, $"spawn failed. areaId={args.AreaId} caster={input.CasterActorId} target={targetActorId} center=({center.X:0.###},{center.Y:0.###},{center.Z:0.###}) radius={radius:0.###} lifetimeFrames={lifetimeFrames}");
                return false;
            }

            if (areaRuntime != null)
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
                    skillRuntimeHandle);
            }

            LogApplied(ctx, $"templateId={args.AreaId} runtimeId={areaId.Value} caster={input.CasterActorId} target={targetActorId} radius={radius} lifetimeFrames={lifetimeFrames}");
            return true;
        }

        private static bool TryGetActorPosition(MobaActorLookupService actors, int actorId, out Vec3 position)
        {
            position = Vec3.Zero;
            if (actorId <= 0 || actors == null) return false;
            if (!actors.TryGetActorEntity(actorId, out var actor) || actor == null || !actor.hasTransform) return false;
            position = actor.transform.Value.Position;
            return true;
        }

        private static int ResolveLifetimeFrames(SpawnAreaArgs args, int configDurationMs, IWorldResolver services)
        {
            if (args.DurationFrames > 0) return args.DurationFrames;

            var durationMs = args.DurationMs > 0 ? args.DurationMs : configDurationMs;
            if (durationMs <= 0)
            {
                throw new InvalidOperationException($"SpawnArea requires a positive duration. areaId={args.AreaId}");
            }

            var frameTime = ResolveFrameTime(services);
            var seconds = durationMs / 1000f;
            var now = frameTime.Frame.Value;
            return Math.Max(1, frameTime.TimeToFrame(frameTime.Time + seconds).Value - now);
        }

        private static int ResolveDelayFrames(int configDelayMs, IWorldResolver services)
        {
            if (configDelayMs <= 0) return 0;

            var frameTime = ResolveFrameTime(services);
            var seconds = configDelayMs / 1000f;
            var now = frameTime.Frame.Value;
            return Math.Max(1, frameTime.TimeToFrame(frameTime.Time + seconds).Value - now);
        }

        private static int ResolveStayIntervalFrames(SpawnAreaArgs args, int configIntervalMs, IWorldResolver services)
        {
            if (args.StayIntervalFrames > 0) return args.StayIntervalFrames;
            if (configIntervalMs <= 0) return 0;

            var frameTime = ResolveFrameTime(services);
            var seconds = configIntervalMs / 1000f;
            var now = frameTime.Frame.Value;
            return Math.Max(1, frameTime.TimeToFrame(frameTime.Time + seconds).Value - now);
        }

        private static int ResolveFrame(IWorldResolver services)
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
