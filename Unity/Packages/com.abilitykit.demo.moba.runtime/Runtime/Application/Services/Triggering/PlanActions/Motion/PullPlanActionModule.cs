using System;
using System.Collections.Generic;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Combat.MotionSystem.Trajectory;
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
    /// 拉拽位移的Plan Action模块
    /// 将目标拉向释放者或指定位置
    /// </summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.Pull)]
    public sealed class PullPlanActionModule : MobaPlanActionModuleBase<PullArgs, PullPlanActionModule>
    {
        protected override IActionSchema<PullArgs, IWorldResolver> Schema => PullSchema.Instance;

        protected override void Execute(object triggerArgs, PullArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.Speed <= 0f || args.DurationMs <= 0f)
            {
                LogRejected(ctx, $"requires positive speed and duration. speed={args.Speed} duration={args.DurationMs}");
                return;
            }

            var input = MobaMovementActionInputResolver.Resolve(triggerArgs, ctx);
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                var coreInput = input.ActionInput;
                var effectInput = new MobaEffectActionInput(in coreInput);
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, TriggeringConstants.Actions.Pull, targets))
                {
                    return;
                }

                ExecuteForTargets(input, args, ctx, targets);
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }

        private void ExecuteForTargets(MobaMovementActionInput input, PullArgs args, ExecCtx<IWorldResolver> ctx, List<int> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                LogRejected(ctx, "requires valid target actor");
                return;
            }

            var registry = input.Actors;
            if (registry == null)
            {
                LogRejected(ctx, "requires MobaActorRegistry service");
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                ExecuteForTarget(input, args, ctx, registry, targets[i]);
            }
        }

        private void ExecuteForTarget(MobaMovementActionInput input, PullArgs args, ExecCtx<IWorldResolver> ctx, MobaActorRegistry registry, int targetId)
        {
            if (targetId <= 0)
            {
                LogRejected(ctx, "requires valid target actor");
                return;
            }

            if (!registry.TryGet(targetId, out var targetEntity) || targetEntity == null || !targetEntity.hasMotion)
            {
                LogRejected(ctx, $"requires target has Motion component. targetId={targetId}");
                return;
            }

            var m = targetEntity.motion;
            if (!m.Initialized || m.Pipeline == null)
            {
                LogRejected(ctx, $"requires target Motion initialized. targetId={targetId}");
                return;
            }

            var pullDir = input.ResolvePullDirection(args.DirectionMode, targetId);
            if (pullDir.SqrMagnitude <= 0f)
            {
                LogRejected(ctx, $"cannot resolve pull direction. mode={args.DirectionMode} targetId={targetId}");
                return;
            }

            var duration = args.DurationMs / 1000f;
            var group = MobaMotionGroupConfigResolver.Resolve(ctx.Context, args.MotionGroupId, MotionGroups.Control, args.Priority, 12);
            var sourceDuration = duration;
            IMotionSource source;
            var motionKind = "PullMotion";

            if (args.DirectionMode == 2)
            {
                var height = args.Speed * duration;
                var trajectory = new KnockupTrajectory(height, duration);
                sourceDuration = trajectory.Duration + (1f / 30f);
                source = new TrajectoryMotionSource(
                    trajectory,
                    group.Priority,
                    group.GroupId,
                    group.Stacking,
                    MotionCollisionConstraints.Disabled,
                    hasCollisionPolicy: true);
                motionKind = "KnockupMotion";
            }
            else
            {
                var velocity = pullDir * args.Speed;
                source = new FixedDeltaMotionSource(velocity, duration, group.Priority, group.GroupId, group.Stacking);
            }

            if (!MobaMotionContinuousActionRuntime.TryActivate(
                    ctx,
                    input,
                    motionKind,
                    input.CasterActorId,
                    targetId,
                    targetId,
                    sourceDuration,
                    source,
                    args.Continuous,
                    default,
                    out var rejectReason))
            {
                LogRejected(ctx, rejectReason);
            }
        }

        private sealed class KnockupTrajectory : ITrajectory3D
        {
            private readonly float _height;
            private readonly float _ascentDuration;
            private readonly float _duration;

            public KnockupTrajectory(float height, float ascentDuration)
            {
                _height = height > 0f ? height : 0f;
                _ascentDuration = ascentDuration > 0.0001f ? ascentDuration : 0.0001f;
                _duration = _ascentDuration * 2f;
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
                forward = new Vec3(0f, 0f, 1f);
                return false;
            }
        }
    }
}
