using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    [WorldService(typeof(MobaMotionHitTriggerService))]
    public sealed class MobaMotionHitTriggerService : IService
    {
        private const string EventMotionHit = "motion.hit";

        [WorldInject(required: false)] private MobaActorRegistry _registry = null;
        [WorldInject(required: false)] private MobaTriggerExecutionGateway _triggers = null;
        [WorldInject(required: false)] private IFrameTime _frameTime = null;

        public bool TryExecute(int moverActorId, in MotionHit hit, in Vec3 point, in MobaMotionHitTriggerRuntime runtime)
        {
            if (!runtime.IsValid) return false;
            if (!hit.Hit || hit.TargetId <= 0) return false;
            if (_triggers == null) return false;

            var hitCollider = new ColliderId(hit.TargetId);
            var targetActorId = ResolveActorIdByCollider(hitCollider);
            if (targetActorId <= 0 || targetActorId == moverActorId) return false;

            var sourceActorId = runtime.SourceActorId > 0 ? runtime.SourceActorId : moverActorId;
            if (sourceActorId <= 0) return false;

            var payload = new MobaMotionHitArgs
            {
                TriggerId = runtime.TriggerId,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                SourceContextId = runtime.ExecutionScope.EffectContextId,
                SourceConfigId = runtime.SourceConfigId,
                Frame = _frameTime != null ? _frameTime.Frame.Value : 0,
                MotionTargetId = hit.TargetId,
                HitCollider = hitCollider,
                Point = point,
                Normal = hit.Normal,
                Runtime = runtime.WithSourceActor(sourceActorId),
            };

            var request = MobaTriggerExecutionRequest<MobaMotionHitArgs>.Create(runtime.TriggerId, payload, EventMotionHit);
            _triggers.ExecuteDirectTrigger(in request);
            return true;
        }

        private int ResolveActorIdByCollider(ColliderId id)
        {
            if (_registry == null) return 0;
            if (id.Value <= 0) return 0;

            try
            {
                foreach (var kv in _registry.Entries)
                {
                    var e = kv.Value;
                    if (e == null || !e.hasActorId || !e.hasCollisionId) continue;
                    if (e.collisionId.Value.Equals(id))
                    {
                        return e.actorId.Value;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, "[MobaMotionHitTriggerService] ResolveActorIdByCollider failed");
            }

            return 0;
        }

        public void Dispose()
        {
        }
    }
}
