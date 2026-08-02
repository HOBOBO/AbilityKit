using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Trajectory;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.Jump)]
    public sealed class JumpPlanActionModule : MobaPlanActionModuleBase<JumpArgs, JumpPlanActionModule>
    {
        protected override IActionSchema<JumpArgs, IWorldResolver> Schema => JumpSchema.Instance;

        protected override void Execute(object triggerArgs, JumpArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.Height <= 0f || args.DurationMs <= 0f)
            {
                LogRejected(ctx, $"requires positive height and duration. height={args.Height} duration={args.DurationMs}");
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

            var motion = entity.motion;
            if (!motion.Initialized || motion.Pipeline == null)
            {
                LogRejected(ctx, $"requires Motion initialized. actorId={actorId}");
                return;
            }

            var duration = args.DurationMs / 1000f;
            var group = MobaMotionGroupConfigResolver.Resolve(ctx.Context, args.MotionGroupId, MotionGroups.Ability, args.Priority, 10);
            var trajectory = new JumpTrajectory(args.Height, duration);
            var source = new TrajectoryMotionSource(
                trajectory,
                group.Priority,
                group.GroupId,
                group.Stacking,
                MotionCollisionConstraints.Disabled,
                hasCollisionPolicy: true);
            var landingRuntime = default(MobaMotionLandingTriggerRuntime);

            if (args.LandingTriggerIds != null && args.LandingTriggerIds.Count > 0)
            {
                if (!MobaPlanActionExecutionContextResolver.TryResolve(triggerArgs, ctx, out var executionContext))
                {
                    LogRejected(ctx, "requires combat execution context for landing triggers");
                    return;
                }

                var sourceConfigId = input.ActionInput.HasTraceScope ? input.ActionInput.TraceScope.EffectConfigId : executionContext.ConfigId;
                landingRuntime = new MobaMotionLandingTriggerRuntime(args.LandingTriggerIds, actorId, sourceConfigId, executionContext);
            }

            if (!MobaMotionContinuousActionRuntime.TryActivate(
                    ctx,
                    input,
                    "JumpMotion",
                    input.CasterActorId > 0 ? input.CasterActorId : actorId,
                    actorId,
                    actorId,
                    trajectory.Duration + (1f / 30f),
                    source,
                    args.Continuous,
                    default,
                    landingRuntime,
                    out var rejectReason))
            {
                LogRejected(ctx, rejectReason);
                return;
            }

            Log.Info($"[JumpPlanActionModule] activated actorId={actorId}, height={args.Height:F3}, duration={duration:F3}, landingTriggers={args.LandingTriggerIds.Count}, groupId={group.GroupId}, priority={group.Priority}, stacking={group.Stacking}");
        }

        private sealed class JumpTrajectory : ITrajectory3D
        {
            private readonly float _height;
            private readonly float _duration;

            public JumpTrajectory(float height, float duration)
            {
                _height = height > 0f ? height : 0f;
                _duration = duration > 0.0001f ? duration : 0.0001f;
            }

            public float Duration => _duration;

            public Vec3 SamplePosition(float time)
            {
                if (time <= 0f || _height <= 0f) return Vec3.Zero;
                if (time >= _duration) return Vec3.Zero;

                var normalized = time / _duration;
                var heightFactor = 1f - (2f * normalized - 1f) * (2f * normalized - 1f);
                return Vec3.Up * (_height * heightFactor);
            }

            public bool TrySampleForward(float time, out Vec3 forward)
            {
                forward = Vec3.Forward;
                return false;
            }
        }
    }
}
