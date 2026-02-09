using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class ProjectileEffectSnapshotComponent : IComponent
    {
        public float DamageMul;
        public float SpeedMul;
        public int Pierce;
    }
}
