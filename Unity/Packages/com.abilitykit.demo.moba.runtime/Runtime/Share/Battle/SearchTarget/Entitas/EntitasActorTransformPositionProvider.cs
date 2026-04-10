using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Entitas
{
    public sealed class EntitasActorTransformPositionProvider : IPositionProvider
    {
        private readonly EntitasActorIdLookup _lookup;

        public EntitasActorTransformPositionProvider(EntitasActorIdLookup lookup)
        {
            _lookup = lookup;
        }

        public bool TryGetPositionXZ(EcsEntityId id, out Vector2 positionXZ)
        {
            positionXZ = default;

            if (!id.IsValid) return false;
            if (_lookup == null) return false;

            if (!_lookup.TryGet(id.ActorId, out var entity) || entity == null) return false;
            if (!entity.hasTransform) return false;

            var p = entity.transform.Value.Position;
            positionXZ = new Vector2(p.X, p.Z);
            return true;
        }
    }
}
