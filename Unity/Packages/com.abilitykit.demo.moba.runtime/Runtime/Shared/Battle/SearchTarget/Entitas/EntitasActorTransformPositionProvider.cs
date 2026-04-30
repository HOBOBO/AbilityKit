using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;

namespace AbilityKit.Battle.SearchTarget.Entitas
{
    public sealed class EntitasActorTransformPositionProvider : IPositionProvider
    {
        private readonly EntitasActorIdLookup _lookup;

        public EntitasActorTransformPositionProvider(EntitasActorIdLookup lookup)
        {
            _lookup = lookup;
        }

        public bool TryGetPosition(Battle.SearchTarget.IEntityId entity, out IVec2 position)
        {
            position = default;

            if (!entity.IsValid) return false;
            if (_lookup == null) return false;

            if (!_lookup.TryGet(entity.ActorId, out var ent) || ent == null) return false;
            if (!ent.hasTransform) return false;

            var p = ent.transform.Value.Position;
            position = new Vec2(p.X, p.Z);
            return true;
        }
    }
}
