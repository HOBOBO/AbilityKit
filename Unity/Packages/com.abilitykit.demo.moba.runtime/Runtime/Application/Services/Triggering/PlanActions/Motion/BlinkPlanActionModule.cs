using System;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Systems;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 闪烁位移的Plan Action模块
    /// 瞬间传送到指定方向和距离的位置
    /// </summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.Blink)]
    public sealed class BlinkPlanActionModule : MobaPlanActionModuleBase<BlinkArgs, BlinkPlanActionModule>
    {
        private const float BlinkCollisionRadius = 0.5f;

        protected override IActionSchema<BlinkArgs, IWorldResolver> Schema => BlinkSchema.Instance;

        protected override void Execute(object triggerArgs, BlinkArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.Distance <= 0f)
            {
                LogRejected(ctx, $"requires positive distance. distance={args.Distance}");
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

            if (!entity.hasTransform)
            {
                LogRejected(ctx, $"requires actor has Transform component. actorId={actorId}");
                return;
            }

            var m = entity.motion;
            var t = entity.transform.Value;

            var dir = input.ResolveDashOrBlinkDirection(args.DirectionMode, actorId);
            if (dir.SqrMagnitude <= 0f)
            {
                dir = t.Forward;
            }

            var desiredDelta = dir * args.Distance;
            var startPos = t.Position;
            // 回退（solver 非 ConfigurableMotionSolver 时）：裸传送。
            var newPos = new Vec3(startPos.X + desiredDelta.X, startPos.Y + desiredDelta.Y, startPos.Z + desiredDelta.Z);

            // 经碰撞解析落安全终点：block（PassThroughWalls=false）= 全距 sweep 钳到墙前；
            // pass（PassThroughWalls=true）= 穿墙，终点落障碍物内则沿方向投影到边界。
            if (m.Solver is ConfigurableMotionSolver configurable)
            {
                var blinkPolicy = new MotionCollisionConstraints(
                    enable: true,
                    allowPassThrough: args.PassThroughWalls,
                    endOverlapPolicy: MotionEndOverlapPolicy.ProjectAlongDirection,
                    radius: BlinkCollisionRadius,
                    skin: 0f,
                    obstacleMask: MobaCollisionLayers.WorldMask,
                    ignoreMask: 0,
                    slideAlongWalls: false,
                    maxSlideIterations: 1);
                var resolved = configurable.Resolve(actorId, in startPos, in desiredDelta, in blinkPolicy);
                newPos = new Vec3(startPos.X + resolved.AppliedDelta.X, startPos.Y + resolved.AppliedDelta.Y, startPos.Z + resolved.AppliedDelta.Z);
            }

            var state = m.State;
            var newState = new MotionState(in newPos)
            {
                Velocity = state.Velocity,
                Forward = state.Forward,
                Time = state.Time
            };

            entity.ReplaceMotion(
                m.Pipeline,
                newState,
                m.Output,
                m.Solver,
                m.Policy,
                m.Events,
                m.Initialized,
                m.HitTriggerRuntime);

            var newTransform = new Transform3(in newPos, in t.Rotation, in t.Scale);
            entity.ReplaceTransform(newTransform);
        }

    }
}
