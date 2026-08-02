using AbilityKit.Demo.Moba.Components;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba;

namespace AbilityKit.Demo.Moba.Systems.Motion
{
    [WorldSystem(order: MobaSystemOrder.MotionInit, Phase = WorldSystemPhase.PreExecute)]
    public sealed class MobaMotionInitSystem : WorldSystemBase
    {
        private const float DefaultActorCollisionRadius = 0.5f;

        private global::Entitas.IGroup<global::ActorEntity> _group;
        private IMotionSolver _defaultSolver;

        public MobaMotionInitSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            _group = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
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
                    pipeline.Policy ??= MobaMotionGroupConfigResolver.CreatePolicy(Services);
                }

                var solver = m.Solver ?? ResolveDefaultSolver();
                if (solver != null) pipeline.Solver = solver;
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
                    newSolver: solver,
                    newPolicy: m.Policy,
                    newEvents: m.Events,
                    newInitialized: true,
                    newHitTriggerRuntime: m.HitTriggerRuntime);
            }
        }

        private IMotionSolver ResolveDefaultSolver()
        {
            if (_defaultSolver != null) return _defaultSolver;

            if (!Services.TryResolve<ICollisionService>(out var collisionService) || collisionService == null || collisionService.World == null)
            {
                return null;
            }

            Services.TryResolve<MobaActorRegistry>(out var actors);
            var motionWorld = new MobaMotionCollisionWorldAdapter(collisionService.World, actors);
            _defaultSolver = new ConfigurableMotionSolver(motionWorld, ResolveDefaultConstraints);
            return _defaultSolver;
        }

        private static MotionConstraints ResolveDefaultConstraints(int moverId, in MotionState state, in MotionOutput input, float dt)
        {
            var collision = new MotionCollisionConstraints(
                enable: true,
                allowPassThrough: false,
                endOverlapPolicy: MotionEndOverlapPolicy.AllowInside,
                radius: DefaultActorCollisionRadius,
                skin: 0f,
                obstacleMask: MobaCollisionLayers.WorldMask,
                ignoreMask: 0,
                slideAlongWalls: true,
                maxSlideIterations: 2,
                wallSlideSpeedRecovery: 1f);

            return new MotionConstraints(collision, MotionLeashConstraints.Disabled);
        }
    }
}

