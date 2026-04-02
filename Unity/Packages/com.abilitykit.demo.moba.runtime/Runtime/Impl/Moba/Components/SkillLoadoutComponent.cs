using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Components
{
    [Actor]
    public sealed class SkillLoadoutComponent : IComponent
    {
        public ActiveSkillRuntime[] ActiveSkills;
        public PassiveSkillRuntime[] PassiveSkills;
    }
}
