using System;
using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Motion
{
    [WorldSystem(order: MobaSystemOrder.MotionInit, Phase = WorldSystemPhase.PreExecute)]
    public sealed class MobaMotionInitSystem : WorldSystemBase
    {
        private Entitas.IGroup<global::ActorEntity> _group;

        public MobaMotionInitSystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            _group = Contexts.actor.GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.Transform,
                global::ActorComponentsLookup.Motion));
        }

        protected override void OnExecute()
        {
            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.hasMotion || !e.hasTransform || !e.hasActorId) continue;

                var m = e.motion;
                if (m.Initialized && m.Pipeline != null) continue;

                var t = e.transform.Value;

                var pipeline = m.Pipeline ?? new MotionPipeline();

                if (m.Policy != null)
                {
                    pipeline.Policy = m.Policy;
                }
                else
                {
                    pipeline.Policy ??= MotionPipelinePolicy.CreateDefault();
                }

                if (m.Solver != null) pipeline.Solver = m.Solver;
                if (m.Events != null) pipeline.Events = m.Events;

                var state = m.State;
                if (!m.Initialized)
                {
                    state = new MotionState(t.Position);
                    state.Forward = t.Forward;
                }

                var output = m.Output;
                output.Clear();

                e.ReplaceMotion(
                    newPipeline: pipeline,
                    newState: state,
                    newOutput: output,
                    newSolver: m.Solver,
                    newPolicy: m.Policy,
                    newEvents: m.Events,
                    newInitialized: true);
            }
        }
    }
}
