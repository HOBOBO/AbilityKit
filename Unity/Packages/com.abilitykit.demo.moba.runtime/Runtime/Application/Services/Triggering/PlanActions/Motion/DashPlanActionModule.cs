using System;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Systems;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 冲刺位移的Plan Action模块
    /// 使用 FixedDeltaMotionSource 实现朝指定方向的冲刺
    /// </summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.Dash)]
    public sealed class DashPlanActionModule : MobaPlanActionModuleBase<DashArgs, DashPlanActionModule>
    {
        private const float DashCollisionRadius = 0.5f;

        protected override IActionSchema<DashArgs, IWorldResolver> Schema => DashSchema.Instance;

        protected override void Execute(object triggerArgs, DashArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if ((!args.MoveToAimPosition && args.Speed <= 0f) || args.DurationMs <= 0f)
            {
                LogRejected(ctx, $"requires positive speed unless moving to aim position, and positive duration. speed={args.Speed} duration={args.DurationMs} moveToAimPosition={args.MoveToAimPosition}");
                return;
            }

            var input = MobaMovementActionInputResolver.Resolve(triggerArgs, ctx);
            var actorId = input.ResolveActorId(args.ApplyToCaster);

            if (actorId <= 0)
            {
                LogRejected(ctx, $"cannot resolve actor. applyToCaster={args.ApplyToCaster}");
                return;
            }

            var registry = input.Actors;
            if (registry == null)
            {
                LogRejected(ctx, "requires MobaActorRegistry service");
                return;
            }

            if (!registry.TryGet(actorId, out var entity) || entity == null || !entity.hasMotion)
            {
                LogRejected(ctx, $"requires actor has Motion component. actorId={actorId}");
                return;
            }

            var m = entity.motion;
            if (!m.Initialized || m.Pipeline == null)
            {
                LogRejected(ctx, $"requires Motion initialized. actorId={actorId}");
                return;
            }

            var duration = args.DurationMs / 1000f;
            var dir = input.ResolveDashOrBlinkDirection(args.DirectionMode, actorId);
            var fallbackToForward = false;
            var velocity = Vec3.Zero;
            var aimDelta = Vec3.Zero;
            if (args.MoveToAimPosition)
            {
                if (!input.TryGetPlanarDeltaToAimPosition(actorId, out aimDelta))
                {
                    LogRejected(ctx, $"requires aim position for point dash. actorId={actorId} hasAimPosition={input.ActionInput.HasAimPosition}");
                    return;
                }

                velocity = aimDelta / duration;
                dir = aimDelta.Normalized;
            }
            else
            {
                fallbackToForward = dir.SqrMagnitude <= 0f;
                if (fallbackToForward)
                {
                    dir = entity.hasTransform ? entity.transform.Value.Forward : Vec3.Forward;
                }

                velocity = dir * args.Speed;
            }
            var group = MobaMotionGroupConfigResolver.Resolve(ctx.Context, args.MotionGroupId, MotionGroups.Ability, args.Priority, 10);

            // 穿墙 dash：行进忽略墙体，终点落障碍物内沿方向投影到边界。否则沿用 actor 默认（block + 墙滑）。
            MotionCollisionConstraints collisionPolicy = default;
            var hasCollisionPolicy = false;
            if (args.PassThroughWalls)
            {
                collisionPolicy = new MotionCollisionConstraints(
                    enable: true,
                    allowPassThrough: true,
                    endOverlapPolicy: MotionEndOverlapPolicy.ProjectAlongDirection,
                    radius: DashCollisionRadius,
                    skin: 0f,
                    obstacleMask: MobaCollisionLayers.WorldMask,
                    ignoreMask: 0,
                    slideAlongWalls: false,
                    maxSlideIterations: 1);
                hasCollisionPolicy = true;
            }

            var source = new FixedDeltaMotionSource(velocity, duration, group.Priority, group.GroupId, group.Stacking, collisionPolicy, hasCollisionPolicy);

            Log.Info($"[DashPlanActionModule] activate request actorId={actorId}, caster={input.CasterActorId}, directionMode={args.DirectionMode}, moveToAimPosition={args.MoveToAimPosition}, fallbackToForward={fallbackToForward}, dir=({dir.X:F3},{dir.Y:F3},{dir.Z:F3}), aimDelta=({aimDelta.X:F3},{aimDelta.Y:F3},{aimDelta.Z:F3}), speed={args.Speed:F3}, velocity=({velocity.X:F3},{velocity.Y:F3},{velocity.Z:F3}), duration={duration:F3}, groupId={group.GroupId}, priority={group.Priority}, stacking={group.Stacking}, hitTrigger={args.HitTriggerPlanId}");

            var hitTriggerRuntime = default(MobaMotionHitTriggerRuntime);
            if (args.HitTriggerPlanId > 0 && input.ActionInput.HasTraceScope)
            {
                hitTriggerRuntime = new MobaMotionHitTriggerRuntime(
                    args.HitTriggerPlanId,
                    actorId,
                    input.ActionInput.TraceScope.EffectConfigId,
                    input.ActionInput.TraceScope);
            }

            if (!MobaMotionContinuousActionRuntime.TryActivate(
                    ctx,
                    input,
                    "DashMotion",
                    input.CasterActorId > 0 ? input.CasterActorId : actorId,
                    actorId,
                    actorId,
                    duration,
                    source,
                    args.Continuous,
                    hitTriggerRuntime,
                    out var rejectReason))
            {
                LogRejected(ctx, rejectReason);
                return;
            }

            Log.Info($"[DashPlanActionModule] activated actorId={actorId}, duration={duration:F3}, sourceActive={source.IsActive}");
        }

    }
}
