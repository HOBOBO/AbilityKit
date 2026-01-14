using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class SkillLoadoutComponent : IComponent
    {
        public SkillRuntime[] ActiveSkills;
        public SkillRuntime[] PassiveSkills;
    }
}
