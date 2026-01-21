using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class SummonMetaComponent : IComponent
    {
        public int SummonId;
        public bool DespawnOnOwnerDie;
    }
}
