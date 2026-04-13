using System;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.MotionSystem.Core;
using AbilityKit.Core.Common.MotionSystem.Generic;
using AbilityKit.Core.Math;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// 拉拽位移的Plan Action模块
    /// 将目标拉向释放者或指定位置
    /// </summary>
    [PlanActionModule(order: 15)]
    public sealed class PullPlanActionModule : NamedArgsPlanActionModuleBase<PullArgs, IWorldResolver, PullPlanActionModule>
    {
        protected override ActionId ActionId => TriggeringConstants.PullId;
        protected override IActionSchema<PullArgs, IWorldResolver> Schema => PullSchema.Instance;

        protected override void Execute(object triggerArgs, PullArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.Speed <= 0f || args.DurationMs <= 0f)
            {
                Log.Warning($"[Plan] pull requires positive speed and duration. speed={args.Speed} duration={args.DurationMs}");
                return;
            }

            var targetId = ResolveTargetActorId(triggerArgs);
            if (targetId <= 0)
            {
                Log.Warning($"[Plan] pull requires valid target actor");
                return;
            }

            var casterId = ResolveCasterActorId(triggerArgs);
            if (!ctx.Context.TryResolve<MobaActorRegistry>(out var registry) || registry == null)
            {
                Log.Warning($"[Plan] pull requires MobaActorRegistry service");
                return;
            }

            if (!registry.TryGet(targetId, out var targetEntity) || targetEntity == null || !targetEntity.hasMotion)
            {
                Log.Warning($"[Plan] pull requires target has Motion component. targetId={targetId}");
                return;
            }

            var m = targetEntity.motion;
            if (!m.Initialized || m.Pipeline == null)
            {
                Log.Warning($"[Plan] pull requires target Motion initialized. targetId={targetId}");
                return;
            }

            var pullDir = ResolvePullDirection(args.DirectionMode, args.TargetDistance, registry, casterId, targetId);
            if (pullDir.SqrMagnitude <= 0f)
            {
                Log.Warning($"[Plan] pull cannot resolve pull direction. mode={args.DirectionMode}");
                return;
            }

            var velocity = pullDir * args.Speed;
            var duration = args.DurationMs / 1000f;
            var source = new FixedDeltaMotionSource(velocity, duration, args.Priority, MotionGroups.Control, MotionStacking.OverrideLowerPriority);

            m.Pipeline.AddSource(source);
        }

        private static int ResolveCasterActorId(object args)
        {
            if (PlanContextValueResolver.TryGetCasterActorId(args, out var casterId) && casterId > 0)
                return casterId;
            return 0;
        }

        private static int ResolveTargetActorId(object args)
        {
            if (PlanContextValueResolver.TryGetTargetActorId(args, out var targetId) && targetId > 0)
                return targetId;
            return 0;
        }

        private static Vec3 ResolvePullDirection(int mode, float targetDistance, MobaActorRegistry registry, int casterId, int targetId)
        {
            if (mode == 0)
            {
                if (casterId <= 0 || casterId == targetId)
                    return Vec3.Zero;

                if (!registry.TryGet(casterId, out var caster) || !caster.hasTransform ||
                    !registry.TryGet(targetId, out var target) || !target.hasTransform)
                    return Vec3.Zero;

                var delta = caster.transform.Value.Position - target.transform.Value.Position;
                if (delta.SqrMagnitude > 0.01f)
                    return delta.Normalized;

                return Vec3.Forward;
            }
            else if (mode == 1)
            {
                if (!registry.TryGet(targetId, out var target) || !target.hasTransform)
                    return Vec3.Zero;

                var pos = target.transform.Value.Position;
                var pullDir = new Vec3(-target.transform.Value.Forward.X, 0f, -target.transform.Value.Forward.Z).Normalized;
                return pullDir;
            }
            else if (mode == 2)
            {
                return Vec3.Up;
            }

            return Vec3.Zero;
        }
    }
}
