using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.TagSystem;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Effect.Components;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaSkillRuntimeService
    {
        public static class TriggerArgs
        {
            public const string SkillId = "skill.id";
            public const string SkillSlot = "skill.slot";
            public const string CasterActorId = "caster.actorId";
            public const string TargetActorId = "target.actorId";
            public const string AimDir = "aim.dir";
            public const string AimPos = "aim.pos";
        }

        private readonly IWorldServices _services;
        private readonly IFrameTime _time;
        private readonly IEventBus _eventBus;
        private readonly IUnitResolver _units;
        private readonly MobaSkillLoadoutService _loadout;
        private readonly MobaActorLookupService _actors;

        public MobaSkillRuntimeService(IWorldServices services, IFrameTime time, IEventBus eventBus, IUnitResolver units, MobaSkillLoadoutService loadout, MobaActorLookupService actors)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _eventBus = eventBus;
            _units = units ?? throw new ArgumentNullException(nameof(units));
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        }

        public bool CastBySlot(int actorId, int slot)
        {
            if (!_loadout.TryGetSkillId(actorId, slot, out var skillId))
            {
                return false;
            }

            return CastSkill(actorId, skillId, slot);
        }

        public bool CastSkill(int actorId, int skillId)
        {
            return CastSkill(actorId, skillId, slot: 0);
        }

        private bool CastSkill(int actorId, int skillId, int slot)
        {
            if (actorId <= 0) return false;
            if (skillId <= 0) return false;

            if (!_units.TryResolve(new EcsEntityId(actorId), out var unit) || unit == null)
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

            var servicesProvider = new WorldServiceProviderAdapter(_services);

            var args = new Dictionary<string, object>(6, StringComparer.Ordinal)
            {
                [TriggerArgs.SkillId] = skillId,
                [TriggerArgs.SkillSlot] = slot,
                [TriggerArgs.CasterActorId] = actorId,
                [TriggerArgs.TargetActorId] = actorId,
                [TriggerArgs.AimPos] = aimPos,
                [TriggerArgs.AimDir] = aimDir,
            };

            var spec = new GameplayEffectSpec(
                durationPolicy: EffectDurationPolicy.Duration,
                durationSeconds: 0.8f,
                periodSeconds: 0.2f,
                applicationRequirements: new GameplayTagRequirements(null, null),
                grantedTags: null,
                components: new IEffectComponent[]
                {
                    new TriggerEventEffectComponent(applyEventId: "skill.cast", args: args),
                },
                executePeriodicOnApply: true,
                cue: null
            );

            var ctx = new EffectExecutionContext(
                services: servicesProvider,
                time: _time,
                source: unit,
                target: unit,
                targetUnit: unit,
                eventBus: _eventBus
            );

            unit.Effects.Apply(spec, in ctx);
            return true;
        }
    }
}
