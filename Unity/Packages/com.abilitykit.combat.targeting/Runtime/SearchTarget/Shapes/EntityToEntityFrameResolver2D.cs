using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Shapes;
using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Shapes
{
    public sealed class EntityToEntityFrameResolver2D : IShapeFrameResolver2D
    {
        private readonly EcsEntityId _source;
        private readonly EcsEntityId _target;
        private readonly bool _useMidPointAsOrigin;

        public EntityToEntityFrameResolver2D(EcsEntityId source, EcsEntityId target, bool useMidPointAsOrigin)
        {
            _source = source;
            _target = target;
            _useMidPointAsOrigin = useMidPointAsOrigin;
        }

        public bool RequiresPosition => true;

        public bool ResolveFrame(SearchContext context, out ShapeFrame2D frame)
        {
            frame = default;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            if (!pos.TryGetPositionXZ(_source, out var a)) return false;
            if (!pos.TryGetPositionXZ(_target, out var b)) return false;

            var dir = b - a;
            var forward = dir.sqrMagnitude > 0f ? dir.normalized : Vector2.up;
            var origin = _useMidPointAsOrigin ? (a + b) * 0.5f : a;
            frame = new ShapeFrame2D(origin, forward);
            return true;
        }
    }
}
