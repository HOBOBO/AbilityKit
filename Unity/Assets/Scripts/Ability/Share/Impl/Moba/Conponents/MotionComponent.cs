
using AbilityKit.Ability.Share.Common.MotionSystem.Collision;
using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.Share.Common.MotionSystem.Events;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
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
    }
}
