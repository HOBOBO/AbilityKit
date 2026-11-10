using System.Collections.Generic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Shared.Logging;
using AbilityKit.Game.Battle.Shared.Time;
using AbilityKit.Game.Flow;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Battle.Vfx
{
    internal sealed class BattleVfxFollowTargetPositionResolver
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly HashSet<ulong> _interpFallbackWarned = new HashSet<ulong>();
        private readonly IBattleLogger _logger;
        private readonly IBattleViewTimeSource _time;
#endif

        public BattleVfxFollowTargetPositionResolver(
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            IBattleLogger logger = null,
            IBattleViewTimeSource time = null
#endif
        )
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _logger = logger ?? new UnityBattleLogger();
            _time = time ?? UnityBattleViewTimeSource.Shared;
#endif
        }

        public bool TryResolve(EC.IECWorld world, EC.IEntity entity, BattleViewBinder binder, out Vector3 position)
        {
            return TryResolve(world, entity, binder, query: null, out position, out _);
        }

        public bool TryResolve(EC.IECWorld world, EC.IEntity entity, BattleViewBinder binder, out Vector3 position, out Vector3 forward)
        {
            return TryResolve(world, entity, binder, query: null, out position, out forward);
        }

        public bool TryResolve(EC.IECWorld world, EC.IEntity entity, BattleViewBinder binder, IBattleEntityQuery query, out Vector3 position, out Vector3 forward)
        {
            position = default;
            forward = default;
            if (world == null) return false;
            if (!entity.TryGetRef(out BattleViewFollowComponent follow) || follow == null) return false;
            if (!TryResolveTarget(world, follow, query, out var targetId)) return false;

            var target = world.Wrap(targetId);
            if (target.TryGetRef(out BattleTransformComponent transform) && transform != null)
            {
                forward = transform.Forward;
            }

            if (binder != null && binder.TryGetInterpolatedPos(targetId, out var viewPos))
            {
                position = viewPos;
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WarnInterpolationFallback(entity.Id, targetId, binder);
#endif

            if (target.TryGetRef(out transform) && transform != null)
            {
                position = transform.Position;
                return true;
            }

            return false;
        }

        internal static bool TryResolveTarget(EC.IECWorld world, BattleViewFollowComponent follow, IBattleEntityQuery query, out EC.IEntityId targetId)
        {
            targetId = follow.Target;
            if (targetId.Index != 0 && world.IsAlive(targetId)) return true;

            if (follow.TargetActorId <= 0 || query == null) return false;
            if (!query.TryResolve(new BattleNetId(follow.TargetActorId), out var target)) return false;
            if (!target.IsValid || !world.IsAlive(target.Id)) return false;

            follow.Target = target.Id;
            targetId = target.Id;
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void WarnInterpolationFallback(EC.IEntityId vfxId, EC.IEntityId targetId, BattleViewBinder binder)
        {
            if (binder == null) return;

            var key = ((ulong)(uint)vfxId.Index << 32) | (uint)targetId.Index;
            if (_interpFallbackWarned.Add(key))
            {
                _logger.Warning($"[BattleVfxManager] VFX follow fallback to logic position: vfx={vfxId.Index} target={targetId.Index} frame={_time.FrameCount}");
            }
        }
#endif
    }
}
