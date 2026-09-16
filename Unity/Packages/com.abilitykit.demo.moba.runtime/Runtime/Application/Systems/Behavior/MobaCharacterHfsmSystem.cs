using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.HFSM.Definition;

namespace AbilityKit.Demo.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.CharacterHfsmTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaCharacterHfsmSystem : WorldSystemBase
    {
        private IFrameTime _frameTime;
        private IWorldClock _clock;
        private MobaCombatRulesService _rules;
        private StateMachineDefinition _definition;
        private Entitas.IGroup<global::ActorEntity> _heroes;
        private Entitas.IGroup<global::ActorEntity> _casts;

        public MobaCharacterHfsmSystem(Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services) { }

        protected override void OnInit()
        {
            Services.TryResolve(out _frameTime);
            Services.TryResolve(out _clock);
            Services.TryResolve(out _rules);
            Services.TryResolve(out _definition);
            _definition ??= MobaCharacterHfsmProfile.CreateDefinition();
            _heroes = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId, global::ActorComponentsLookup.UnitSubType));
            _casts = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.SkillCastOwnerActorId,
                global::ActorComponentsLookup.SkillCastInstanceId,
                global::ActorComponentsLookup.SkillCastRunningTag));
        }

        protected override void OnExecute()
        {
            if (_frameTime == null || _clock == null || _heroes == null) return;
            var frame = _frameTime.Frame.Value;
            var deltaRaw = Fixed64.FromSingle(_clock.DeltaTime).RawValue;
            var heroes = _heroes.GetEntities();
            var casts = _casts.GetEntities();
            for (var i = 0; i < heroes.Length; i++)
            {
                var actor = heroes[i];
                if (actor.unitSubType.Value != UnitSubType.Hero) continue;
                long castInstanceId = 0;
                var skillId = 0;
                var castStartFrame = int.MinValue;
                for (var j = 0; j < casts.Length; j++)
                {
                    var cast = casts[j];
                    if (cast.skillCastOwnerActorId.Value != actor.actorId.Value) continue;
                    var start = cast.hasSkillCastStartFrame ? cast.skillCastStartFrame.Value : int.MinValue;
                    if (start < castStartFrame ||
                        (start == castStartFrame && cast.skillCastInstanceId.Value <= castInstanceId)) continue;
                    castStartFrame = start;
                    castInstanceId = cast.skillCastInstanceId.Value;
                    skillId = cast.hasSkillCastSkillId ? cast.skillCastSkillId.Value : 0;
                }

                var alive = _rules == null || _rules.IsAlive(actor);
                var controlled = alive && _rules != null && _rules.IsStunned(actor.actorId.Value);
                var moving = alive && actor.hasMoveInput &&
                    (actor.moveInput.Dx != 0f || actor.moveInput.Dz != 0f) &&
                    (_rules == null || _rules.CanMove(actor.actorId.Value));
                if (!actor.hasCharacterHfsm)
                    actor.AddCharacterHfsm(new MobaCharacterHfsmRuntime(actor.actorId.Value,
                        _definition, frame > 0 ? frame - 1 : frame,
                        Fixed64.FromRaw(checked(deltaRaw * (frame > 0 ? frame - 1 : frame)))));

                var machine = actor.characterHfsm.Runtime;
                if (machine == null || frame <= machine.Frame) continue;
                var time = Fixed64.FromRaw(checked(machine.Time.RawValue +
                    deltaRaw * (frame - machine.Frame)));
                machine.Tick(frame, time, alive, controlled, moving, castInstanceId, skillId);
            }
        }

        protected override void OnTearDown()
        {
            if (_heroes == null) return;
            foreach (var actor in _heroes.GetEntities())
                if (actor.hasCharacterHfsm) actor.RemoveCharacterHfsm();
        }
    }
}
