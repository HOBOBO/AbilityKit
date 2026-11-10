using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    public sealed class SkillLoadoutComponent : IComponent
    {
        public int HeroId;
        public ActiveSkillRuntime[] ActiveSkills;
        public PassiveSkillRuntime[] PassiveSkills;
    }
}
