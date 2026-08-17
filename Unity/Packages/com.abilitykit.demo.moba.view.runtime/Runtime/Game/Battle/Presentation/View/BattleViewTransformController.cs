using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewTransformController
    {
        private readonly BattleViewHandleStore _handles;
        private readonly BattleViewInterpolationClock _clock;
        private readonly BattleViewPositionSampler _sampler;
        private readonly BattleViewPositionApplier _applier;

        public BattleViewTransformController(
            BattleViewHandleStore handles,
            BattleViewAttachedVfxController attachedVfx,
            BattleViewInterpolationClock clock = null,
            BattleViewPositionSampler sampler = null,
            BattleViewPositionApplier applier = null)
        {
            _handles = handles;
            _clock = clock ?? new BattleViewInterpolationClock();
            _sampler = sampler ?? new BattleViewPositionSampler(handles);
            _applier = applier ?? new BattleViewPositionApplier(handles, attachedVfx);
        }

        public bool InterpolationEnabled { get; set; } = true;

        public float BackTimeTicks { get; set; } = 1f;

        public float MaxLagTicks { get; set; } = 4f;

        public float SmoothingHz
        {
            get => _applier.SmoothingHz;
            set => _applier.SmoothingHz = value;
        }

        public bool TryGetInterpolatedPos(EC.IEntityId id, out Vector3 pos)
        {
            pos = default;
            if (!_handles.TryGet(id, out var handle)) return false;
            if (handle.Destroyed) return false;

            if (!InterpolationEnabled)
            {
                if (handle.HasPendingPos)
                {
                    pos = handle.PendingPos;
                    return true;
                }

                return false;
            }

            if (handle.Pos.TryEvaluate(_clock.RenderTime, out pos)) return true;
            if (handle.HasPendingPos)
            {
                pos = handle.PendingPos;
                return true;
            }

            return false;
        }

        public void SampleEntity(in EC.IEntity entity, in Vector3 pos, IBattleRuntimeContext ctx)
        {
            _sampler.SampleEntity(entity, in pos, ctx);
        }

        public void Tick(
            IBattleRuntimeContext runtimeContext,
            IBattleEntityContext entityContext,
            float deltaTime)
        {
            if (runtimeContext == null || entityContext?.EntityWorld == null) return;
            if (deltaTime <= 0f) return;

            if (!InterpolationEnabled)
            {
                _applier.ApplyPendingPositions();
                return;
            }

            var frameAdvanced = _clock.Advance(
                runtimeContext,
                deltaTime,
                BackTimeTicks,
                MaxLagTicks,
                out var sampleTime);
            if (frameAdvanced)
            {
                _sampler.SampleAliveEntityPositions(entityContext, sampleTime);
            }

            _applier.ApplyInterpolatedPositions(_clock.RenderTime, deltaTime);
        }

        public void Reset()
        {
            _clock.Reset();
        }
    }
}
