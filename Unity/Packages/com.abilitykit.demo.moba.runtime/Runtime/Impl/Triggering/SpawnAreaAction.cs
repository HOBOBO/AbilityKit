using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Share.Impl.Moba.Services;

namespace AbilityKit.Ability.Impl.Triggering
{
    using AbilityKit.Ability;
    public sealed class SpawnAreaAction : ITriggerAction
    {
        private readonly float _radius;
        private readonly int _lifetimeMs;
        private readonly int _collisionLayerMask;
        private readonly int _stayIntervalMs;

        private readonly int _onEnterTriggerId;
        private readonly int _onExitTriggerId;
        private readonly int _onExpireTriggerId;

        public SpawnAreaAction(float radius, int lifetimeMs, int collisionLayerMask, int stayIntervalMs, int onEnterTriggerId, int onExitTriggerId, int onExpireTriggerId)
        {
            _radius = radius;
            _lifetimeMs = lifetimeMs;
            _collisionLayerMask = collisionLayerMask;
            _stayIntervalMs = stayIntervalMs;
            _onEnterTriggerId = onEnterTriggerId;
            _onExitTriggerId = onExitTriggerId;
            _onExpireTriggerId = onExpireTriggerId;
        }

        public static SpawnAreaAction FromDef(ActionDef def)
        {
            var args = def?.Args;
            var radius = TriggerActionArgUtil.TryGetFloat(args, "radius", 1f);
            var lifetimeMs = TriggerActionArgUtil.TryGetInt(args, "lifetimeMs", 0);
            var collisionLayerMask = TriggerActionArgUtil.TryGetInt(args, "collisionLayerMask", -1);
            var stayIntervalMs = TriggerActionArgUtil.TryGetInt(args, "stayIntervalMs", 0);

            var onEnterTriggerId = TriggerActionArgUtil.TryGetInt(args, "onEnterTriggerId", 0);
            var onExitTriggerId = TriggerActionArgUtil.TryGetInt(args, "onExitTriggerId", 0);
            var onExpireTriggerId = TriggerActionArgUtil.TryGetInt(args, "onExpireTriggerId", 0);

            return new SpawnAreaAction(radius, lifetimeMs, collisionLayerMask, stayIntervalMs, onEnterTriggerId, onExitTriggerId, onExpireTriggerId);
        }

        public void Execute(TriggerContext context)
        {
            var svc = context?.Services?.GetService(typeof(IProjectileService)) as IProjectileService;
            if (svc == null)
            {
                Log.Warning("[Trigger] spawn_area cannot resolve IProjectileService from DI");
                return;
            }

            if (!TriggerActionArgUtil.TryResolveActorId(context?.Source, out var ownerId) || ownerId <= 0)
            {
                Log.Warning("[Trigger] spawn_area requires context.Source with valid actorId");
                return;
            }

            var frameTime = context?.Services?.GetService(typeof(IFrameTime)) as IFrameTime;
            var dt = frameTime != null && frameTime.DeltaTime > 0f ? frameTime.DeltaTime : 0.033333f;
            var frame = frameTime != null ? frameTime.Frame.Value : 0;

            var payload = context != null ? context.Event.Payload : null;
            var aimPos = Vec3.Zero;
            if (payload is IAbilityPipelineContext pc)
            {
                aimPos = pc.GetAimPos();
            }
            else if (payload is IEffectContext ec && ec.TryGetSkill(out var skill))
            {
                aimPos = skill.AimPos;
            }

            if (aimPos.Equals(Vec3.Zero))
            {
                Log.Warning("[Trigger] spawn_area cannot resolve center (AimPos/pos)");
                return;
            }

            var lifetimeFrames = _lifetimeMs > 0 ? System.Math.Max(1, (int)System.MathF.Round((_lifetimeMs / 1000f) / dt)) : 1;
            var stayIntervalFrames = _stayIntervalMs > 0 ? System.Math.Max(1, (int)System.MathF.Round((_stayIntervalMs / 1000f) / dt)) : 0;

            var p = new AreaSpawnParams(ownerId, in aimPos, _radius, lifetimeFrames, _collisionLayerMask, stayIntervalFrames);
            var areaId = svc.SpawnArea(in p, frame);

            var registry = context?.Services?.GetService(typeof(MobaAreaTriggerRegistry)) as MobaAreaTriggerRegistry;
            registry?.Register(areaId, templateId: 0, ownerId, in aimPos, _radius, _collisionLayerMask, maxTargets: 0, _onEnterTriggerId, _onExitTriggerId, _onExpireTriggerId);
        }
    }
}
