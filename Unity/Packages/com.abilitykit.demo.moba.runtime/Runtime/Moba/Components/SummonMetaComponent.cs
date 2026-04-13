using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    public sealed class SummonMetaComponent : IComponent
    {
        public int SummonId;
        public bool DespawnOnOwnerDie;
    }
}
