using System;
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
        private MobaCharacterHfsmProfileCatalog _profiles;
        private MobaCharacterHfsmParameterService _parameters;
        private MobaEffectExecutionService _effects;
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
            Services.TryResolve(out _profiles);
            Services.TryResolve(out _parameters);
            Services.TryResolve(out _effects);
            _definition ??= MobaCharacterHfsmProfile.CreateGenericDefinition();
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
                // Every actor with a skill loadout can use the shared character
                // HFSM. Summons and minions are intentionally included here so
                // their generic attack slots produce the same action snapshots
                // as heroes; no summon/hero identity branch is required.
                if (!actor.hasSkillLoadout) continue;
                long castInstanceId = 0;
                var skillId = 0;
                var skillSlot = 0;
                var aimPos = AbilityKit.Core.Mathematics.Vec3.Zero;
                var aimDir = AbilityKit.Core.Mathematics.Vec3.Zero;
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
                    skillSlot = cast.hasSkillCastSlot ? cast.skillCastSlot.Value : 0;
                    if (cast.hasSkillCastAim)
                    {
                        aimPos = cast.skillCastAim.Pos;
                        aimDir = cast.skillCastAim.Dir;
                    }
                }

                var alive = _rules == null || _rules.IsAlive(actor);
                var controlled = alive && _rules != null && _rules.IsStunned(actor.actorId.Value);
                var moving = alive && actor.hasMoveInput &&
                    (actor.moveInput.Dx != 0f || actor.moveInput.Dz != 0f) &&
                    (_rules == null || _rules.CanMove(actor.actorId.Value));
                var basicAttackSlot = actor.hasSkillLoadout
                    ? actor.skillLoadout.ActiveSkills?.Length ?? 0
                    : 0;
                if (!actor.hasCharacterHfsm)
                {
                    var heroId = actor.hasSkillLoadout ? actor.skillLoadout.HeroId : 0;
                    var definition = actor.hasSummonMeta
                        ? MobaCharacterHfsmProfile.CreateSummonDefinition()
                        : (_profiles?.Resolve(heroId) ?? MobaCharacterHfsmProfile.CreateGenericDefinition());
                    _parameters?.RegisterActorProfile(actor.actorId.Value, definition);
                    actor.AddCharacterHfsm(new MobaCharacterHfsmRuntime(actor.actorId.Value,
                        definition, frame > 0 ? frame - 1 : frame,
                        Fixed64.FromRaw(checked(deltaRaw * (frame > 0 ? frame - 1 : frame))),
                        parameters: _parameters, executeLogicTrigger: ExecuteHfsmLogicTrigger));
                }

                var machine = actor.characterHfsm.Runtime;
                if (machine == null || frame <= machine.Frame) continue;
                var time = Fixed64.FromRaw(checked(machine.Time.RawValue +
                    deltaRaw * (frame - machine.Frame)));
                machine.Tick(frame, time, alive, controlled, moving, castInstanceId, skillId,
                    skillSlot, basicAttackSlot, in aimPos, in aimDir);
            }
        }

        private void ExecuteHfsmLogicTrigger(int actorId, int triggerId, string stateId, int frame)
        {
            if (_effects == null)
                throw new InvalidOperationException("Character HFSM logic action requires MobaEffectExecutionService.");
            _effects.ExecuteRulePlan(triggerId, new MobaCharacterHfsmActionPayload
            {
                ActorId = actorId,
                StateId = stateId,
                Frame = frame
            });
        }

        protected override void OnTearDown()
        {
            if (_heroes == null) return;
            foreach (var actor in _heroes.GetEntities())
            {
                if (actor.hasCharacterHfsm) actor.RemoveAndDisposeCharacterHfsm();
                if (actor.hasActorId) _parameters?.ClearActorProfile(actor.actorId.Value);
            }
        }
    }
}
