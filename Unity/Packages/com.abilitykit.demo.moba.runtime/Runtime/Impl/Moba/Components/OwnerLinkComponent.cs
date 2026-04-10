using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Share.Impl.Moba.Components
{
    [Actor]
    public sealed class OwnerLinkComponent : IComponent
    {
        public int OwnerActorId;
        public int RootOwnerActorId;
    }
}
