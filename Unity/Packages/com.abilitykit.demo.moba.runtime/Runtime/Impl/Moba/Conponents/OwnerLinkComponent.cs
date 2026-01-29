using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class OwnerLinkComponent : IComponent
    {
        public int OwnerActorId;
        public int RootOwnerActorId;
    }
}
