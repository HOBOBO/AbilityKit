using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Share.Common.AttributeSystem;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffsApply, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffApplySystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private IUnitResolver _units;

        private Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffApplySystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _configs);
            Services.TryGet(out _units);
            _group = Contexts.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.ApplyBuffRequest));
        }

        protected override void OnExecute()
        {
            if (_configs == null || _units == null) return;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId || !e.hasApplyBuffRequest) continue;

                var req = e.applyBuffRequest;
                e.RemoveApplyBuffRequest();

                if (req.BuffId <= 0) continue;

                if (!_units.TryResolve(new EcsEntityId(e.actorId.Value), out var unit) || unit == null) continue;

                if (!_configs.TryGetBuff(req.BuffId, out var buff) || buff == null) continue;

                if (!e.hasBuffs)
                {
                    e.AddBuffs(new List<BuffRuntime>());
                }

                var list = e.buffs.Active;
                if (list == null)
                {
                    list = new List<BuffRuntime>();
                    e.ReplaceBuffs(list);
                }

                AttributeEffectHandle handle = null;
                if (req.Effect != null)
                {
                    handle = unit.Attributes.ApplyEffect(req.Effect);
                }

                var duration = req.DurationOverrideMs > 0 ? req.DurationOverrideMs : buff.DurationMs;

                list.Add(new BuffRuntime
                {
                    BuffId = buff.Id,
                    Remaining = duration > 0 ? duration / 1000f : 0f,
                    SourceId = req.SourceId,
                    Handle = handle
                });
            }
        }
    }
}
