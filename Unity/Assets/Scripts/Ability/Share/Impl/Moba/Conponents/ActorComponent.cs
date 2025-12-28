using AbilityKit.Ability.Share.Math;
using Entitas;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class TransformComponent : IComponent
    {
        public Transform3 Value;
    }

    [Actor]
    public sealed class ColliderComponent : IComponent
    {
        public ColliderShape LocalShape;
    }

    [Actor]
    public sealed class CollisionLayerComponent : IComponent
    {
        public int Mask;
    }

    [Actor]
    public sealed class CollisionIdComponent : IComponent
    {
        public ColliderId Value;
    }
}
