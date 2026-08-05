using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudActorResolver : IBattleHudActorPositionResolver
    {
        private readonly BattleContext _ctx;

        public BattleHudActorResolver(BattleContext ctx)
        {
            _ctx = ctx;
        }

        public bool TryGetActorWorldPos(int actorId, out Vector3 pos)
        {
            pos = default;
            if (_ctx?.EntityQuery == null) return false;
            if (!_ctx.EntityQuery.TryResolve(new BattleNetId(actorId), out var entity)) return false;
            if (!entity.TryGetRef(out BattleTransformComponent transform) || transform == null) return false;

            pos = transform.Position;
            return true;
        }

        public bool TryResolveActorId(EC.IEntityId id, out int actorId)
        {
            actorId = 0;
            if (_ctx?.EntityQuery == null) return false;
            if (!_ctx.EntityQuery.World.IsAlive(id)) return false;

            var entity = _ctx.EntityQuery.World.Wrap(id);
            if (!entity.TryGetRef(out BattleNetIdComponent netIdComp) || netIdComp == null) return false;

            actorId = netIdComp.NetId.Value;
            return actorId > 0;
        }
    }
}
