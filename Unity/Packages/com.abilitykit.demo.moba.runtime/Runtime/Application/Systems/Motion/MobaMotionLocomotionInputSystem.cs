using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba;

namespace AbilityKit.Demo.Moba.Systems.Motion
{
    [WorldSystem(order: MobaSystemOrder.MotionLocomotionInput, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaMotionLocomotionInputSystem : WorldSystemBase
    {
        private IWorldClock _clock;
        private MobaCombatRulesService _combatRules;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        private readonly Dictionary<int, LocomotionMotionSource> _locomotionByActorId = new Dictionary<int, LocomotionMotionSource>(128);
        private readonly Dictionary<int, int> _seenStampByActorId = new Dictionary<int, int>(128);
        private readonly List<int> _tmpRemoveActorIds = new List<int>(64);
        private int _stamp;
        private int _sampleLogCount;

        public MobaMotionLocomotionInputSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _clock);
            Services.TryResolve(out _combatRules);
            _group = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
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

                if (_combatRules != null && !_combatRules.CanMove(actorId))
                {
                    if (_locomotionByActorId.TryGetValue(actorId, out var blockedLoco) && blockedLoco != null)
                    {
                        blockedLoco.SetInput(0f, 0f);
                    }
                    continue;
                }

                var speed = new MobaAttrs(e).MoveSpeed;

                if (!_locomotionByActorId.TryGetValue(actorId, out var loco) || loco == null)
                {
                    // MoveInput is expressed in world X/Z space: the HUD joystick
                    // projects through the camera, while AI target movement writes
                    // a normalized world-space delta. Treating it as Local rotates
                    // the input again whenever an actor turns (for example after
                    // aiming or casting a directional skill).
                    loco = new LocomotionMotionSource(speed: speed, space: MotionInputSpace.World, priority: 0);
                    _locomotionByActorId[actorId] = loco;
                    m.Pipeline.AddSource(loco);
                }
                else
                {
                    loco.Speed = speed;
                }

                loco.SetInput(e.moveInput.Dx, e.moveInput.Dz);

                _sampleLogCount++;
                if (_sampleLogCount <= 5 || _sampleLogCount % 60 == 0)
                {
                    Log.Info($"[MobaMotionLocomotionInputSystem] ActorId={actorId}, MoveInput=({e.moveInput.Dx:F3},{e.moveInput.Dz:F3}), Speed={speed:F3}, Dt={_clock.DeltaTime:F4}, Count={_sampleLogCount}");
                }

                e.ReplaceMotion(
                    newPipeline: m.Pipeline,
                    newState: m.State,
                    newOutput: m.Output,
                    newSolver: m.Solver,
                    newPolicy: m.Policy,
                    newEvents: m.Events,
                    newInitialized: m.Initialized,
                    newHitTriggerRuntime: m.HitTriggerRuntime);
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

