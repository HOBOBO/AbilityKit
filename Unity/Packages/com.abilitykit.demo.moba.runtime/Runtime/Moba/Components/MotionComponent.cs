
using AbilityKit.Core.Common.MotionSystem.Collision;
using AbilityKit.Core.Common.MotionSystem.Core;
using AbilityKit.Core.Common.MotionSystem.Events;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
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
