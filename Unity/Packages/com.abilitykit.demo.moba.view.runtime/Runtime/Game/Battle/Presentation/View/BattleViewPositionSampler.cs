using AbilityKit.Demo.Moba.View.Abstractions.Battle.View;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewPositionSampler
    {
        private readonly BattleViewHandleStore _handles;

        public BattleViewPositionSampler(BattleViewHandleStore handles)
        {
            _handles = handles;
        }

        public void SampleEntity(in EC.IEntity entity, in Vector3 pos, in Vector3 forward, IBattleRuntimeContext ctx)
        {
            var sampleTime = ResolveSampleTime(ctx);
            SampleEntity(entity, in pos, in forward, sampleTime);
        }

        public void SampleAliveEntityPositions(IBattleEntityContext ctx, double sampleTime)
        {
            if (ctx?.EntityWorld == null) return;
 
            ctx.EntityWorld.ForEachAlive(entity =>
            {
                if (!entity.TryGetRef(out BattleNetIdComponent netIdComp) || netIdComp == null) return;
                if (!entity.TryGetRef(out BattleTransformComponent transform) || transform == null) return;

                SampleEntity(entity, in transform.Position, in transform.Forward, sampleTime);
            });
        }

        private void SampleEntity(in EC.IEntity entity, in Vector3 pos, in Vector3 forward, double sampleTime)
        {
            var handle = _handles.GetOrCreate(entity.Id);
            if (handle.Destroyed) return;

            handle.PendingPos = pos;
            handle.HasPendingPos = true;
            var horizontalForward = new Vector3(forward.x, 0f, forward.z);
            var movement = handle.HasLastSampledPos ? pos - handle.LastSampledPos : Vector3.zero;
            var horizontalMovement = new Vector3(movement.x, 0f, movement.z);
            var sourceForwardChanged = false;
            if (horizontalForward.sqrMagnitude > 0.0001f)
            {
                horizontalForward.Normalize();
                sourceForwardChanged = !handle.HasLastSourceForward ||
                    Vector3.Dot(handle.LastSourceForward, horizontalForward) < 0.9999f;
                handle.LastSourceForward = horizontalForward;
                handle.HasLastSourceForward = true;
            }

            if (horizontalMovement.sqrMagnitude > 0.000001f)
            {
                // Prefer the actual resolved movement direction. This covers
                // locomotion sources that only update position/velocity.
                handle.PendingForward = horizontalMovement.normalized;
                handle.HasPendingForward = true;
            }
            else if (horizontalForward.sqrMagnitude > 0.0001f &&
                     (sourceForwardChanged || !handle.HasPendingForward))
            {
                // Repeated same-frame samples can have no displacement. Keep the
                // last movement heading unless the source's facing really changed.
                handle.PendingForward = horizontalForward;
                handle.HasPendingForward = true;
            }
            handle.LastSampledPos = pos;
            handle.HasLastSampledPos = true;
            handle.Pos.Add(sampleTime, in handle.PendingPos);

            if (entity.TryGetRef(out BattleNetIdComponent netIdComp) && netIdComp != null)
            {
                var actorId = netIdComp.NetId.Value;
                if (actorId > 0)
                {
                    _handles.SetActorId(handle, actorId, entity.Id);
                }
            }
        }

        private static double ResolveSampleTime(IBattleRuntimeContext ctx)
        {
            if (ctx == null) return 0d;

            return BattleViewSampleTimeResolver.Resolve(ctx.LastFrame, ctx.Plan.World.TickRate);
        }
    }
}
