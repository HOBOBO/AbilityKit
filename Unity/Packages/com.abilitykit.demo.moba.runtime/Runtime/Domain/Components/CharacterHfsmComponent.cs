using AbilityKit.Demo.Moba.Services.StateMachine;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    public sealed class CharacterHfsmComponent : IComponent
    {
        public MobaCharacterHfsmRuntime Runtime;
    }
}
