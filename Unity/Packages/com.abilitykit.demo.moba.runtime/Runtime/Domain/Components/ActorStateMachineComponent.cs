using AbilityKit.Demo.Moba.Services.StateMachine;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    public sealed class ActorStateMachineComponent : IComponent
    {
        public string ProfileId;
        public MobaActorStateMachineRuntime Runtime;
    }
}
