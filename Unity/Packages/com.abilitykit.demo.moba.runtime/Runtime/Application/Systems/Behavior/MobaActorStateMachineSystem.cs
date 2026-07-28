using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.StateMachine;

namespace AbilityKit.Demo.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.ActorStateMachineTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaActorStateMachineSystem : WorldSystemBase
    {
        private IFrameTime _frameTime;
        private IWorldClock _clock;
        private IMobaActorBrainCatalog _brains;
        private MobaActorStateMachineFactory _factory;
        private Entitas.IGroup<global::ActorEntity> _brainActors;
        private Entitas.IGroup<global::ActorEntity> _stateMachineActors;

        public MobaActorStateMachineSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _frameTime);
            Services.TryResolve(out _clock);
            Services.TryResolve(out _brains);
            Services.TryResolve(out _factory);
            _brainActors = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.ActorBrain));
            _stateMachineActors = Contexts.Actor().GetGroup(global::ActorMatcher.ActorStateMachine);
        }

        protected override void OnExecute()
        {
            AttachConfiguredStateMachines();

            var deltaTime = ResolveDeltaTime();
            if (deltaTime <= 0f || _stateMachineActors == null) return;

            var entities = _stateMachineActors.GetEntities();
            for (var i = 0; i < entities.Length; i++)
            {
                var runtime = entities[i].actorStateMachine.Runtime;
                if (runtime == null) continue;

                if (_frameTime != null)
                    runtime.Tick(_frameTime.Frame, deltaTime);
                else
                    runtime.Tick(deltaTime);
            }
        }

        protected override void OnTearDown()
        {
            if (_stateMachineActors == null) return;

            var entities = _stateMachineActors.GetEntities();
            for (var i = 0; i < entities.Length; i++)
            {
                if (entities[i].hasActorStateMachine)
                {
                    entities[i].RemoveActorStateMachine();
                }
            }

            _brainActors = null;
            _stateMachineActors = null;
        }

        private void AttachConfiguredStateMachines()
        {
            if (_brainActors == null || _brains == null || _factory == null) return;

            var entities = _brainActors.GetEntities();
            for (var i = 0; i < entities.Length; i++)
            {
                var actor = entities[i];
                if (actor.hasActorStateMachine) continue;

                var brainId = actor.actorBrain.BrainId;
                if (!_brains.TryGet(brainId, out var definition)
                    || definition.DriverKind != MobaBrainDriverKind.Hfsm
                    || !_factory.TryCreate(actor, definition.DecisionName, out var runtime))
                {
                    continue;
                }

                actor.AddActorStateMachine(definition.DecisionName, runtime);
            }
        }

        private float ResolveDeltaTime()
        {
            if (_clock != null) return _clock.DeltaTime;
            return _frameTime != null ? _frameTime.DeltaTime : 0f;
        }
    }
}
