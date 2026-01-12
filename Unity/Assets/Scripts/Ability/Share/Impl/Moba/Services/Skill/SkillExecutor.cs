using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillExecutor : IService
    {
        private readonly IWorldServices _services;
        private readonly IWorldClock _clock;
        private readonly IFrameTime _time;
        private readonly IEventBus _eventBus;
        private readonly IUnitResolver _units;
        private readonly MobaSkillLoadoutService _loadout;
        private readonly MobaActorLookupService _actors;
        private readonly IMobaSkillPipelineLibrary _library;

        private readonly Dictionary<int, SkillPipelineRunner> _runners = new Dictionary<int, SkillPipelineRunner>();

        public SkillExecutor(
            IWorldServices services,
            IWorldClock clock,
            IFrameTime time,
            IEventBus eventBus,
            IUnitResolver units,
            MobaSkillLoadoutService loadout,
            MobaActorLookupService actors,
            IMobaSkillPipelineLibrary library)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _eventBus = eventBus;
            _units = units ?? throw new ArgumentNullException(nameof(units));
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        private SkillPipelineRunner GetOrCreateRunner(int actorId)
        {
            if (!_runners.TryGetValue(actorId, out var r) || r == null)
            {
                r = new SkillPipelineRunner(actorId);
                _runners[actorId] = r;
            }

            return r;
        }

        public bool CastBySlot(int actorId, int slot)
        {
            if (!_loadout.TryGetSkillId(actorId, slot, out var skillId))
            {
                return false;
            }

            return CastSkill(actorId, skillId, slot);
        }

        public bool HandleInput(int actorId, in SkillInputEvent evt)
        {
            if (actorId <= 0) return false;
            if (evt.Slot <= 0) return false;

            switch (evt.Phase)
            {
                case SkillInputPhase.Press:
                    return CastBySlot(actorId, evt.Slot);
                case SkillInputPhase.Hold:
                case SkillInputPhase.Release:
                case SkillInputPhase.Cancel:
                default:
                    // Not implemented yet: reserved for charge/channel/confirm/cancel.
                    return false;
            }
        }

        public bool CastSkill(int actorId, int skillId)
        {
            return CastSkill(actorId, skillId, slot: 0);
        }

        private bool CastSkill(int actorId, int skillId, int slot)
        {
            if (actorId <= 0) return false;
            if (skillId <= 0) return false;

            if (!_units.TryResolve(new EcsEntityId(actorId), out var caster) || caster == null)
            {
                return false;
            }

            var aimPos = Vec3.Zero;
            var aimDir = Vec3.Forward;

            if (_actors.TryGetActorEntity(actorId, out var actorEntity) && actorEntity != null && actorEntity.hasTransform)
            {
                var t = actorEntity.transform.Value;
                aimPos = t.Position;
                aimDir = t.Rotation.Rotate(Vec3.Forward).Normalized;
            }

            if (!_library.TryGet(skillId, out var preConfig, out var prePhases, out var castConfig, out var castPhases))
            {
                return false;
            }

            var req = new SkillCastRequest(
                skillId: skillId,
                skillSlot: slot,
                casterActorId: actorId,
                targetActorId: actorId,
                aimPos: in aimPos,
                aimDir: in aimDir,
                worldServices: _services,
                eventBus: _eventBus,
                casterUnit: caster,
                targetUnit: caster
            );

            var runner = GetOrCreateRunner(actorId);
            return runner.Start(preConfig, prePhases, castConfig, castPhases, abilityInstance: this, in req);
        }

        public void Step(int actorId)
        {
            if (actorId <= 0) return;
            if (!_runners.TryGetValue(actorId, out var r) || r == null) return;

            var dt = _clock.DeltaTime;
            if (dt <= 0f) return;

            r.Step(dt);
        }

        public void Dispose()
        {
            _runners.Clear();
        }
    }
}
