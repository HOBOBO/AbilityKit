
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Events;
using AbilityKit.Demo.Moba.Services.Motion;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    [EntitasBlob]
    public sealed class MotionComponent : IComponent
    {
        public MotionPipeline Pipeline;
        public MotionState State;
        public MotionOutput Output;

        // Optional injection points. If null, Pipeline defaults apply.
        public IMotionSolver Solver;
        public MotionPipelinePolicy Policy;
        public IMotionEventSink Events;

        // Optional initialization flag for systems.
        public bool Initialized;

        // Runtime trigger carried by ability-driven motion sources such as dash.
        public MobaMotionHitTriggerRuntime HitTriggerRuntime;
    }
}
