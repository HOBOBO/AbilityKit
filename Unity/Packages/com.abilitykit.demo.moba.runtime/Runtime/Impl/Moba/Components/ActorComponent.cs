using AbilityKit.Core.Math;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Share.Impl.Moba.Components
{
    [Actor]
    [PrimaryEntityIndex]
    public sealed class ActorIdComponent : IComponent
    {
        public int Value;
    }

    [Actor]
    public sealed class TransformComponent : IComponent
    {
        public Transform3 Value;
    }

    [Actor]
    public sealed class MoveInputComponent : IComponent
    {
        public float Dx;
        public float Dz;
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
