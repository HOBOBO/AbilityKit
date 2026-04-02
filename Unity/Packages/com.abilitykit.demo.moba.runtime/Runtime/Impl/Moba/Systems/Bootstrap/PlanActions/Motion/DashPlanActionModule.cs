using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.Share.Common.MotionSystem.Generic;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// 冲刺位移的Plan Action模块
    /// 使用 FixedDeltaMotionSource 实现朝指定方向的冲刺
    /// </summary>
    [PlanActionModule(order: 13)]
    public sealed class DashPlanActionModule : NamedArgsPlanActionModuleBase<DashArgs, IWorldResolver, DashPlanActionModule>
    {
        protected override ActionId ActionId => TriggeringConstants.DashId;
        protected override IActionSchema<DashArgs, IWorldResolver> Schema => DashSchema.Instance;

        protected override void Execute(object triggerArgs, DashArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.Speed <= 0f || args.DurationMs <= 0f)
            {
                Log.Warning($"[Plan] dash requires positive speed and duration. speed={args.Speed} duration={args.DurationMs}");
                return;
            }

            var actorId = args.ApplyToCaster
                ? ResolveCasterActorId(triggerArgs)
                : ResolveTargetActorId(triggerArgs);

            if (actorId <= 0)
            {
                Log.Warning($"[Plan] dash cannot resolve actor. applyToCaster={args.ApplyToCaster}");
                return;
            }

            if (!ctx.Context.TryResolve<MobaActorRegistry>(out var registry) || registry == null)
            {
                Log.Warning($"[Plan] dash requires MobaActorRegistry service");
                return;
            }

            if (!registry.TryGet(actorId, out var entity) || entity == null || !entity.hasMotion)
            {
                Log.Warning($"[Plan] dash requires actor has Motion component. actorId={actorId}");
                return;
            }

            var m = entity.motion;
            if (!m.Initialized || m.Pipeline == null)
            {
                Log.Warning($"[Plan] dash requires Motion initialized. actorId={actorId}");
                return;
            }

            var dir = ResolveDirection(triggerArgs, args.DirectionMode, registry, actorId);
            if (dir.SqrMagnitude <= 0f)
            {
                dir = entity.hasTransform ? entity.transform.Value.Forward : Vec3.Forward;
            }

            var velocity = dir * args.Speed;
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

        private static Vec3 ResolveDirection(object args, int mode, MobaActorRegistry registry, int selfActorId)
        {
            if (mode == 0)
            {
                if (PlanContextValueResolver.TryGetAimDir(args, out var aimDir) && aimDir.SqrMagnitude > 0f)
                    return new Vec3(aimDir.X, 0f, aimDir.Z).Normalized;
            }
            else if (mode == 1)
            {
                var targetId = ResolveTargetActorId(args);
                if (targetId > 0 && targetId != selfActorId)
                {
                    if (registry.TryGet(selfActorId, out var self) && self.hasTransform &&
                        registry.TryGet(targetId, out var target) && target.hasTransform)
                    {
                        var delta = target.transform.Value.Position - self.transform.Value.Position;
                        if (delta.SqrMagnitude > 0f)
                            return new Vec3(delta.X, 0f, delta.Z).Normalized;
                    }
                }
            }

            return Vec3.Forward;
        }
    }
}
