using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Motion
{
    [WorldSystem(order: MobaSystemOrder.MotionLocomotionInput, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaMotionLocomotionInputSystem : WorldSystemBase
    {
        private IWorldClock _clock;
        private Entitas.IGroup<global::ActorEntity> _group;

        private readonly Dictionary<int, LocomotionMotionSource> _locomotionByActorId = new Dictionary<int, LocomotionMotionSource>(128);
        private readonly Dictionary<int, int> _seenStampByActorId = new Dictionary<int, int>(128);
        private readonly List<int> _tmpRemoveActorIds = new List<int>(64);
        private int _stamp;

        public MobaMotionLocomotionInputSystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _clock);
            _group = Contexts.actor.GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.Motion,
                global::ActorComponentsLookup.MoveInput));
        }

        protected override void OnExecute()
        {
            if (_clock == null) return;

            _stamp++;
            if (_stamp == int.MaxValue) _stamp = 1;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.hasActorId || !e.hasMotion || !e.hasMoveInput) continue;

                var m = e.motion;
                if (!m.Initialized || m.Pipeline == null) continue;

                var actorId = e.actorId.Value;
                if (actorId <= 0) continue;

                _seenStampByActorId[actorId] = _stamp;

                if (!_locomotionByActorId.TryGetValue(actorId, out var loco) || loco == null)
                {
                    loco = new LocomotionMotionSource(speed: 4.5f, space: MotionInputSpace.Local, priority: 0);
                    _locomotionByActorId[actorId] = loco;
                    m.Pipeline.AddSource(loco);
                }

                loco.SetInput(e.moveInput.Dx, e.moveInput.Dz);

                e.ReplaceMotion(
                    newPipeline: m.Pipeline,
                    newState: m.State,
                    newOutput: m.Output,
                    newSolver: m.Solver,
                    newPolicy: m.Policy,
                    newEvents: m.Events,
                    newInitialized: m.Initialized);
            }

            if (_locomotionByActorId.Count > 0)
            {
                _tmpRemoveActorIds.Clear();
                foreach (var kv in _locomotionByActorId)
                {
                    if (!_seenStampByActorId.TryGetValue(kv.Key, out var s) || s != _stamp)
                    {
                        _tmpRemoveActorIds.Add(kv.Key);
                    }
                }

                for (int i = 0; i < _tmpRemoveActorIds.Count; i++)
                {
                    var id = _tmpRemoveActorIds[i];
                    _locomotionByActorId.Remove(id);
                    _seenStampByActorId.Remove(id);
                }
            }
        }
    }
}
