using System;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.MotionSystem.Core;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    /// <summary>
    /// 闪烁位移的Plan Action模块
    /// 瞬间传送到指定方向和距离的位置
    /// </summary>
    [PlanActionModule(order: 14)]
    public sealed class BlinkPlanActionModule : NamedArgsPlanActionModuleBase<BlinkArgs, IWorldResolver, BlinkPlanActionModule>
    {
        protected override ActionId ActionId => TriggeringConstants.BlinkId;
        protected override IActionSchema<BlinkArgs, IWorldResolver> Schema => BlinkSchema.Instance;

        protected override void Execute(object triggerArgs, BlinkArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.Distance <= 0f)
            {
                Log.Warning($"[Plan] blink requires positive distance. distance={args.Distance}");
                return;
            }

            var actorId = args.ApplyToCaster
                ? ResolveCasterActorId(triggerArgs)
                : ResolveTargetActorId(triggerArgs);

            if (actorId <= 0)
            {
                Log.Warning($"[Plan] blink cannot resolve actor. applyToCaster={args.ApplyToCaster}");
                return;
            }

            if (!ctx.Context.TryResolve<MobaActorRegistry>(out var registry) || registry == null)
            {
                Log.Warning($"[Plan] blink requires MobaActorRegistry service");
                return;
            }

            if (!registry.TryGet(actorId, out var entity) || entity == null || !entity.hasMotion)
            {
                Log.Warning($"[Plan] blink requires actor has Motion component. actorId={actorId}");
                return;
            }

            if (!entity.hasTransform)
            {
                Log.Warning($"[Plan] blink requires actor has Transform component. actorId={actorId}");
                return;
            }

            var m = entity.motion;
            var t = entity.transform.Value;

            var dir = ResolveDirection(triggerArgs, args.DirectionMode, registry, actorId);
            if (dir.SqrMagnitude <= 0f)
            {
                dir = t.Forward;
            }

            var delta = dir * args.Distance;
            var newPos = new Vec3(t.Position.X + delta.X, t.Position.Y + delta.Y, t.Position.Z + delta.Z);

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
                m.Initialized);

            var newTransform = new Transform3(in newPos, in t.Rotation, in t.Scale);
            entity.ReplaceTransform(newTransform);
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
