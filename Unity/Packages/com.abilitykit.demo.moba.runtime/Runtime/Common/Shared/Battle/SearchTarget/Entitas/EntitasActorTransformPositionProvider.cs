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

        public bool TryGetPosition(Battle.SearchTarget.EntityId entity, out Vec2 position)
        {
            position = default;

            if (!entity.IsValid || entity.Value > int.MaxValue) return false;
            if (_lookup == null) return false;

            if (!_lookup.TryGet((int)entity.Value, out var ent) || ent == null) return false;
            if (!ent.hasTransform) return false;

            var p = ent.transform.Value.Position;
            position = new Vec2(p.X, p.Z);
            return true;
        }
    }
}
