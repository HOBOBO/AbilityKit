using System;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Motion
{
    [WorldSystem(order: MobaSystemOrder.MotionTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaMotionTickSystem : WorldSystemBase
    {
        private IWorldClock _clock;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaMotionTickSystem(global::Entitas.IContexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _clock);
            _group = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.Transform,
                global::ActorComponentsLookup.Motion));
        }

        protected override void OnExecute()
        {
            if (_clock == null) return;
            var dt = _clock.DeltaTime;
            if (dt <= 0f) return;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.hasMotion || !e.hasTransform || !e.hasActorId) continue;

                var m = e.motion;
                if (!m.Initialized) continue;
                if (m.Pipeline == null) continue;

                var t = e.transform.Value;
                var state = m.State;
                state.Position = t.Position;
                state.Forward = t.Forward;

                var output = m.Output;

                m.Pipeline.Tick(e.actorId.Value, ref state, dt, ref output);

                var desiredForward = output.NewForward.SqrMagnitude > 0f ? output.NewForward : state.Forward;
                var newRot = desiredForward.SqrMagnitude > 0f ? Quat.LookRotation(desiredForward, Vec3.Up) : t.Rotation;

                var newT = new Transform3(state.Position, newRot, t.Scale);
                e.ReplaceTransform(newT);

                e.ReplaceMotion(
                    newPipeline: m.Pipeline,
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

