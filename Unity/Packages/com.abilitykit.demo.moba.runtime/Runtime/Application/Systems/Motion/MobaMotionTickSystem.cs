using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba;

namespace AbilityKit.Demo.Moba.Systems.Motion
{
    [WorldSystem(order: MobaSystemOrder.MotionTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaMotionTickSystem : WorldSystemBase
    {
        private IWorldClock _clock;
        private MobaMotionHitTriggerService _hitTriggers;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaMotionTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _clock);
            Services.TryResolve(out _hitTriggers);
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

                var result = m.Pipeline.Tick(e.actorId.Value, ref state, dt, ref output);

                // A piercing source still sweeps units on its last step, then applies its existing wall endpoint policy.
                if (output.CollisionSource is MobaCollisionDashMotionSource endpointDash && endpointDash.HasEndpointPolicy &&
                    (result.Hit.Hit || !endpointDash.IsActive) && m.Solver is AbilityKit.Combat.MotionSystem.Collision.ConfigurableMotionSolver endpointSolver)
                {
                    var endpointPolicy = endpointDash.EndpointPolicy;
                    var endpoint = state.Position;
                    var zero = Vec3.Zero;
                    var correction = endpointSolver.Resolve(e.actorId.Value, in endpoint, in zero, in endpointPolicy);
                    state.Position += correction.AppliedDelta;
                }

                var hitTriggerRuntime = m.HitTriggerRuntime;

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
                    newInitialized: true,
                    newHitTriggerRuntime: hitTriggerRuntime);

                // Write the collision position first. A callback may create new motion; never overwrite it afterwards.
                if (result.Hit.Hit && output.CollisionSource is MobaCollisionDashMotionSource collisionDash)
                {
                    if (collisionDash.TryClaimCollision() && _hitTriggers != null)
                    {
                        var callback = collisionDash.Callback;
                        _hitTriggers.TryExecute(e.actorId.Value, in result.Hit, in state.Position, in callback, collisionDash.SourceContextId);
                    }
                }
                else if (_hitTriggers != null && hitTriggerRuntime.IsValid && result.Hit.Hit &&
                    _hitTriggers.TryExecute(e.actorId.Value, in result.Hit, in state.Position, in hitTriggerRuntime))
                {
                    var current = e.motion;
                    if (current.HitTriggerRuntime.Equals(hitTriggerRuntime))
                        e.ReplaceMotion(current.Pipeline, current.State, current.Output, current.Solver,
                            current.Policy, current.Events, current.Initialized, default);
                }
            }
        }
    }
}

