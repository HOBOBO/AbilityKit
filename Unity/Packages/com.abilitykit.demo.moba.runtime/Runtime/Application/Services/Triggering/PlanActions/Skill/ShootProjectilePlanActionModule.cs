using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 发射投射物的 Plan Action 模块，使用强类型参数 Schema API。
    /// </summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.ShootProjectile)]
    public sealed class ShootProjectilePlanActionModule : MobaPlanActionModuleBase<ShootProjectileArgs, ShootProjectilePlanActionModule>
    {
        protected override IActionSchema<ShootProjectileArgs, IWorldResolver> Schema => ShootProjectileSchema.Instance;

        [MobaModifierConsumes(MobaSkillParamModifierKeys.Emitter.Ids.LauncherId,
            MobaParameterStage.LauncherCreate, MobaModifierSampleTiming.CreateSnapshot)]
        [MobaModifierConsumes(MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId,
            MobaParameterStage.LauncherCreate, MobaModifierSampleTiming.CreateSnapshot)]
        [MobaModifierConsumes(MobaSkillParamModifierKeys.Emitter.Ids.DurationMs,
            MobaParameterStage.LauncherCreate, MobaModifierSampleTiming.CreateSnapshot)]
        protected override void Execute(object triggerArgs, ShootProjectileArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<MobaProjectileService>(out var projectileSvc) || projectileSvc == null)
            {
                LogRejected(ctx, "cannot resolve MobaProjectileService.");
                return;
            }

            if (!ctx.Context.TryResolve<MobaConfigDatabase>(out var configs) || configs == null)
            {
                LogRejected(ctx, "cannot resolve MobaConfigDatabase.");
                return;
            }

            if (!MobaPlanActionInputResolver.TryResolve(
                    triggerArgs,
                    ctx,
                    out var coreInput))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            var input = MobaPlanActionInputAssembler.AssembleProjectile(in coreInput);
            if (!input.HasCasterActor)
            {
                LogRejected(ctx, "caster actor not found");
                return;
            }

            var casterActorId = input.CasterActorId;
            var launcherId = args.LauncherId;
            var projectileId = args.ProjectileId;
            var paramResolver = ctx.Context.TryResolve<MobaSkillParamModifierService>(out var resolvedParamResolver)
                ? resolvedParamResolver
                : null;
            ctx.Context.TryResolve<MobaSkillCastRuntimeService>(out var skillRuntimes);
            var skillHandle = coreInput.ExecutionContext.SkillRuntimeHandle;

            // The cast preparation/runtime owns the locked target for the whole cast.
            // Some pipeline adapters wrap the trigger payload without copying the
            // target onto the transient execution context; recover it from the live
            // skill runtime before falling back to a directional shot.  This keeps
            // targeted projectiles deterministic in the local composition as well as
            // in the normal authored pipeline.
            var lockedTargetActorId = input.TargetActorId;
            if (lockedTargetActorId <= 0 && skillRuntimes != null && skillHandle.IsValid &&
                skillRuntimes.TryGet(in skillHandle, out var skillRuntime) && skillRuntime != null)
            {
                lockedTargetActorId = skillRuntime.TargetActorId;
            }

            if (ctx.Context.TryResolve<MobaReferenceCandidateService>(out var references) && references != null)
            {
                var launcherRecord = MobaParameterRecord.FromSkillRuntime(
                    MobaParameterStage.LauncherCreate, skillRuntimes, in skillHandle);
                launcherId = references.Resolve(casterActorId, MobaSkillParamModifierKeys.Emitter.Ids.LauncherId,
                    launcherId, in launcherRecord);
                var projectileRecord = MobaParameterRecord.FromSkillRuntime(
                    MobaParameterStage.LauncherCreate, skillRuntimes, in skillHandle,
                    launcherTemplateId: launcherId);
                projectileId = references.Resolve(casterActorId, MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId,
                    projectileId, in projectileRecord);
            }

            if (launcherId <= 0 || projectileId <= 0)
            {
                LogRejected(ctx, $"invalid args. launcherId={launcherId} projectileId={projectileId}");
                return;
            }

            var aimPos = input.HasAimPosition ? input.AimPosition : Vec3.Zero;
            var aimDir = input.HasAimDirection ? input.AimDirection : Vec3.Zero;
            var maxDistance = 0f;
            var targetActorId = 0;

            ProjectileLauncherMO launcher = null;
            ProjectileMO projectile = null;
            if (!configs.TryGetProjectileLauncher(launcherId, out launcher) || launcher == null)
            {
                LogRejected(ctx, $"launcher config not found. launcherId={launcherId}");
                return;
            }

            if (!configs.TryGetProjectile(projectileId, out projectile) || projectile == null)
            {
                LogRejected(ctx, $"projectile config not found. projectileId={projectileId}");
                return;
            }

            var countPerShot = launcher.CountPerShot;
            var fanAngleDeg = launcher.FanAngleDeg;
            var durationRecord = MobaParameterRecord.FromSkillRuntime(
                MobaParameterStage.LauncherCreate, skillRuntimes, in skillHandle,
                launcherTemplateId: launcherId, projectileTemplateId: projectileId);
            var durationMs = paramResolver != null
                ? paramResolver.Projectile.ResolveDurationMs(casterActorId, launcher.DurationMs, in durationRecord)
                : launcher.DurationMs;
            MobaResolvedShootProjectileParams launchParams;
            try
            {
                launchParams = new MobaResolvedShootProjectileParams(launcherId, projectileId, countPerShot, fanAngleDeg, durationMs);
            }
            catch (System.ArgumentOutOfRangeException ex)
            {
                LogRejected(ctx, ex.Message);
                return;
            }
 
            var casterPos = Vec3.Zero;
            if (ctx.Context.TryResolve<MobaActorRegistry>(out var actorRegistry)
                && actorRegistry != null
                && actorRegistry.TryGet(casterActorId, out var casterEntity)
                && casterEntity != null
                && casterEntity.hasTransform)
            {
                casterPos = casterEntity.transform.Value.Position;
            }

            var hasTargetRequest = args.TargetRequest.UsesTemplate
                || args.TargetRequest.TargetActorId > 0
                || args.TargetRequest.TargetPayloadActorId > 0
                || args.TargetRequest.SourceCode != MobaActionTargetSourceCode.ContextTarget;
            // A skill cast may already carry the authoritative target selected during
            // cast preparation.  That target must be used even for a non-homing
            // projectile: `track_target` controls whether the projectile follows the
            // target after launch, while target resolution controls its initial aim
            // and source context.  Previously the context target was only resolved
            // when tracking was enabled, so targeted projectiles such as Jhin Q were
            // launched with targetActorId=0 and could not hit the locked target.
            var requiresTargetResolution = hasTargetRequest || lockedTargetActorId > 0;
            if (requiresTargetResolution)
            {
                var effectInput = new MobaEffectActionInput(in coreInput);
                var targets = PooledMobaPlanActionLists.GetIntList();
                try
                {
                    // A plain ContextTarget is already authoritative when the skill cast
                    // preparation selected a target.  Do not send this path through the
                    // generic SearchTargetService: local/demo compositions may execute a
                    // projectile after the search feature has been torn down, and the
                    // selected actor must still be a valid initial aim and hit target.
                    // Template-backed requests deliberately keep using the query because
                    // their filters/order/max-count rules are part of the skill contract.
                    if (!args.TargetRequest.UsesTemplate
                        && args.TargetRequest.SourceCode == MobaActionTargetSourceCode.ContextTarget
                        && lockedTargetActorId > 0)
                    {
                        targets.Add(lockedTargetActorId);
                    }
                    else
                    {
                        // Tracking is opportunistic for attacks: a target selected by the cast
                        // is authoritative, while a configured nearby-target query may acquire
                        // one. If the query finds nothing, retain the submitted aim direction
                        // so targetless attacks remain valid directional releases.
                        var optionalTrackingQuery = args.TrackTarget
                            && lockedTargetActorId <= 0
                            && args.TargetRequest.TargetActorId <= 0
                            && args.TargetRequest.TargetPayloadActorId <= 0;
                        var resolved = optionalTrackingQuery
                            ? MobaActionTargetResolver.TryResolveTargetCollection(in args.TargetRequest, in coreInput, in effectInput, ctx, ActionName, targets)
                            : MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, ActionName, targets);
                        if ((!resolved || targets.Count == 0) && !optionalTrackingQuery)
                        {
                            LogRejected(ctx, "target query found no target");
                            return;
                        }
                    }

                    if (targets.Count > 0) targetActorId = targets[0];
                }
                finally
                {
                    PooledMobaPlanActionLists.Release(targets);
                }

                if (targetActorId <= 0 && lockedTargetActorId > 0)
                {
                    targetActorId = lockedTargetActorId;
                }

                if (targetActorId > 0)
                {
                    if (actorRegistry == null
                        || !actorRegistry.TryGet(targetActorId, out var targetEntity)
                        || targetEntity == null
                        || !targetEntity.hasTransform)
                    {
                        LogRejected(ctx, $"target actor is missing transform. targetActorId={targetActorId}");
                        return;
                    }

                    aimPos = targetEntity.transform.Value.Position;
                    var targetDelta = new Vec3(aimPos.X - casterPos.X, 0f, aimPos.Z - casterPos.Z);
                    if (!targetDelta.Equals(Vec3.Zero)) aimDir = DeterministicMathBridge.Normalize(in targetDelta);
                }
            }
            else if (!aimDir.Equals(Vec3.Zero))
            {
                aimDir = DeterministicMathBridge.Normalize(in aimDir);
            }
            else if (!aimPos.Equals(Vec3.Zero))
            {
                var delta = new Vec3(aimPos.X - casterPos.X, 0f, aimPos.Z - casterPos.Z);
                if (!delta.Equals(Vec3.Zero)) aimDir = DeterministicMathBridge.Normalize(in delta);
            }

            // A tracked projectile follows a 3D path, so the planar cast distance is
            // not a valid travel-distance budget for it. The projectile template's
            // MaxDistance remains the authoritative safety limit in TryLaunch.
            if (input.HasAimPosition && !args.TrackTarget)
            {
                var aimDelta = new Vec3(aimPos.X - casterPos.X, 0f, aimPos.Z - casterPos.Z);
                maxDistance = DeterministicMathBridge.Magnitude(in aimDelta);
            }

            aimPos = casterPos;

            var sourceContext = input.CreateSourceContext(casterActorId, targetActorId, projectile.Id);
            if (!projectileSvc.TryLaunch(casterActorId, launcher, projectile, launchParams.CountPerShot, launchParams.FanAngleDeg, launchParams.DurationMs, args.ContinuousProcessId, args.TrackTarget, in aimPos, in aimDir, in sourceContext, out var launchResult, maxDistance))
            {
                LogRejected(ctx, $"launch failed. launcherId={launchParams.LauncherId} projectileId={launchParams.ProjectileId} targetActorId={targetActorId} lockedTargetActorId={lockedTargetActorId} error={launchResult.Error ?? "<none>"}");
                return;
            }
            var executionContext = coreInput.ExecutionContext;
            PublishBasicAttackLaunched(triggerArgs, in executionContext, skillRuntimes, configs, ctx.Context);
            if (!MobaPlanActionOutput.TryWrite(in ctx, in args.ResultTarget, launchResult.LauncherActorId, out var outputError) ||
                !MobaPlanActionOutput.TryWrite(in ctx, in args.ResultCountTarget, launchResult.TotalCount, out outputError))
            {
                LogRejected(ctx, outputError);
                return;
            }

            LogApplied(ctx, $"launch requested. casterActorId={casterActorId} launcherId={launchParams.LauncherId} projectileId={launchParams.ProjectileId} launcherActorId={launchResult.LauncherActorId} targetActorId={targetActorId} trackTarget={args.TrackTarget} countPerShot={launchParams.CountPerShot} fanAngleDeg={launchParams.FanAngleDeg:0.###} durationMs={launchParams.DurationMs} continuousProcessId={args.ContinuousProcessId} hasAimPos={input.HasAimPosition} hasAimDir={input.HasAimDirection}");
        }

        private static void PublishBasicAttackLaunched(
            object triggerArgs,
            in MobaCombatExecutionContext executionContext,
            MobaSkillCastRuntimeService skillRuntimes,
            MobaConfigDatabase configs,
            IWorldResolver services)
        {
            var skillCast = triggerArgs as SkillCastContext;
            var runtimeHandle = executionContext.SkillRuntimeHandle;
            if (skillCast == null && skillRuntimes != null && runtimeHandle.IsValid &&
                skillRuntimes.TryGet(in runtimeHandle, out var runtime))
            {
                skillCast = new SkillCastContext
                {
                    SkillId = runtime.SkillId,
                    BaseSkillId = runtime.BaseSkillId,
                    SkillSlot = runtime.SkillSlot,
                    SkillLevel = runtime.SkillLevel,
                    Sequence = runtime.Sequence,
                    RuntimeHandle = runtime.Handle,
                    RuntimeId = runtime.RuntimeId,
                    SourceContextId = runtime.RootContextId,
                    CasterActorId = runtime.CasterActorId,
                    TargetActorId = runtime.TargetActorId,
                    AimPos = runtime.AimPos,
                    AimDir = runtime.AimDir,
                    WorldServices = services,
                };
            }

            if (skillCast == null || configs == null ||
                !configs.TryGetSkill(skillCast.SkillId, out var launchedSkill) ||
                launchedSkill == null || launchedSkill.SkillType != SkillType.NormalAttack)
            {
                return;
            }

            MobaSkillTriggering.Publish(MobaSkillTriggering.Events.BasicAttackLaunched, skillCast);
        }
    }
}
