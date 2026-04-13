using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    public sealed class OwnerLinkComponent : IComponent
    {
        public int OwnerActorId;
        public int RootOwnerActorId;
    }
}
