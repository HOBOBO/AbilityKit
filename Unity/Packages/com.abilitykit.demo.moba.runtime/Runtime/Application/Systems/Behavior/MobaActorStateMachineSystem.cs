using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services;
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
        private MobaBrainService _brainService;
        private readonly Dictionary<global::ActorEntity, MobaActorStateMachineBinding> _failedBindings =
            new Dictionary<global::ActorEntity, MobaActorStateMachineBinding>();
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
            Services.TryResolve(out _brainService);
            _brainActors = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.ActorBrain));
            _stateMachineActors = Contexts.Actor().GetGroup(global::ActorMatcher.ActorStateMachine);
        }

        protected override void OnExecute()
        {
            ReconcileConfiguredStateMachines();

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
            _failedBindings.Clear();
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

        private void ReconcileConfiguredStateMachines()
        {
            if (_brainActors == null || _stateMachineActors == null) return;

            RemoveStaleFailureEntries();
            var attached = _stateMachineActors.GetEntities();
            for (var i = 0; i < attached.Length; i++)
            {
                if (_brains == null)
                {
                    attached[i].RemoveActorStateMachine();
                    continue;
                }

                ReconcileAttachedStateMachine(attached[i]);
            }

            if (_brains == null || _factory == null) return;

            var entities = _brainActors.GetEntities();
            for (var i = 0; i < entities.Length; i++)
            {
                var actor = entities[i];
                if (actor.hasActorStateMachine) continue;

                if (TryResolveHfsmBinding(actor, out var binding)
                    && !IsFailedBinding(actor, in binding))
                {
                    TryAttach(actor, in binding);
                }
            }
        }

        private void ReconcileAttachedStateMachine(global::ActorEntity actor)
        {
            if (!TryResolveHfsmBinding(actor, out var binding))
            {
                _failedBindings.Remove(actor);
                actor.RemoveActorStateMachine();
                return;
            }

            _brainService?.ReleaseBehavior(actor, "HfsmOwnership");

            var component = actor.actorStateMachine;
            if (component.Runtime != null
                && component.Runtime.Binding.Equals(binding)
                && string.Equals(component.ProfileId, binding.ProfileId, StringComparison.Ordinal))
            {
                _failedBindings.Remove(actor);
                return;
            }

            actor.RemoveActorStateMachine();
            if (_factory != null && !IsFailedBinding(actor, in binding)) TryAttach(actor, in binding);
        }

        private bool TryResolveHfsmBinding(
            global::ActorEntity actor,
            out MobaActorStateMachineBinding binding)
        {
            binding = default;
            if (actor == null || !actor.hasActorId || !actor.hasActorBrain) return false;

            var brainId = actor.actorBrain.BrainId;
            if (!_brains.TryGet(brainId, out var definition)
                || definition.DriverKind != MobaBrainDriverKind.Hfsm)
            {
                return false;
            }

            binding = MobaActorStateMachineBinding.From(actor, definition.DecisionName);
            return true;
        }

        private void TryAttach(global::ActorEntity actor, in MobaActorStateMachineBinding binding)
        {
            if (_factory == null) return;
            _brainService?.ReleaseBehavior(actor, "HfsmOwnership");

            try
            {
                if (_factory.TryCreate(actor, binding.ProfileId, out var runtime) && runtime != null)
                {
                    _failedBindings.Remove(actor);
                    actor.AddActorStateMachine(binding.ProfileId, runtime);
                    return;
                }

                Log.Error($"[MobaHFSM] state-machine create failed. actor={binding.ActorId} brain={binding.BrainId} profile={binding.ProfileId}");
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaHFSM] state-machine create failed. actor={binding.ActorId} brain={binding.BrainId} profile={binding.ProfileId}");
            }

            _failedBindings[actor] = binding;
        }

        private bool IsFailedBinding(
            global::ActorEntity actor,
            in MobaActorStateMachineBinding binding)
        {
            return _failedBindings.TryGetValue(actor, out var failed) && failed.Equals(binding);
        }

        private void RemoveStaleFailureEntries()
        {
            if (_failedBindings.Count == 0) return;

            List<global::ActorEntity> stale = null;
            foreach (var pair in _failedBindings)
            {
                if (pair.Key != null
                    && pair.Key.isEnabled
                    && TryResolveHfsmBinding(pair.Key, out var binding)
                    && binding.Equals(pair.Value))
                {
                    continue;
                }

                stale ??= new List<global::ActorEntity>();
                stale.Add(pair.Key);
            }

            if (stale == null) return;
            for (var i = 0; i < stale.Count; i++) _failedBindings.Remove(stale[i]);
        }

        private float ResolveDeltaTime()
        {
            if (_clock != null) return _clock.DeltaTime;
            return _frameTime != null ? _frameTime.DeltaTime : 0f;
        }
    }
}
