using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Config.Core;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaSkillLoadoutService))]
    public sealed class MobaSkillLoadoutService : IService, IWorldInitializable
    {
        private readonly MobaActorLookupService _actors;
        private readonly Dictionary<int, int[]> _skillIdsByActorId = new Dictionary<int, int[]>();
        private MobaSkillSlotCandidateService _candidates;
        private MobaCharacterHfsmParameterService _parameters;
        private MobaConfigDatabase _configs;
        private MobaSkillCastRuntimeService _casts;

        public MobaSkillLoadoutService(MobaActorLookupService actors)
        {
            _actors = actors;
        }

        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _candidates);
            services?.TryResolve(out _parameters);
            services?.TryResolve(out _configs);
            services?.TryResolve(out _casts);
        }

        /// <summary>An exact child in the effective skill's channel bypasses slot cooldown; flight requires explicit opt-in.</summary>
        public bool IsIndependentRecastReady(int actorId, int slot, int effectiveSkillId)
        {
            return _configs != null && _casts != null &&
                TryResolveSlot(actorId, slot, out var resolved) && resolved.EffectiveSkillId == effectiveSkillId &&
                _configs.TryGetSkill(effectiveSkillId, out var skill) &&
                _configs.TryGetSkillFlow(skill.CastFlowId, out var flow) && flow.RecastChannelId > 0 &&
                _casts.TrySelectRecastArea(actorId, flow.RecastChannelId, out _, out _, slot, resolved.BaseSkillId, flow.QueueRecastInFlight);
        }

        public void SetLoadout(int actorId, int[] skillIds)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            _skillIdsByActorId[actorId] = skillIds ?? Array.Empty<int>();
        }

        public bool RemoveActor(int actorId)
        {
            return actorId > 0 && _skillIdsByActorId.Remove(actorId);
        }

        public bool TryGetSkillId(int actorId, int slot, out int skillId)
        {
            if (!TryResolveSlot(actorId, slot, out var resolved))
            {
                skillId = 0;
                return false;
            }
            skillId = resolved.EffectiveSkillId;
            return true;
        }

        public bool TryResolveSlot(int actorId, int slot, out MobaResolvedSlotSkill resolved)
        {
            resolved = default;
            if (!TryGetBaseSkillId(actorId, slot, out var baseSkillId)) return false;
            resolved = _candidates?.Resolve(actorId, slot, baseSkillId, _parameters) ??
                new MobaResolvedSlotSkill(slot, baseSkillId, baseSkillId, 0, 0);
            return true;
        }

        public bool TryGetBaseSkillId(int actorId, int slot, out int skillId)
        {
            skillId = 0;
            if (actorId <= 0) return false;
            if (slot <= 0) return false;

            // 可用时优先读取 ECS 组件数据。
            if (_actors != null && _actors.TryGetActorEntity(actorId, out var entity) && entity != null && entity.hasSkillLoadout)
            {
                var skills = entity.skillLoadout.ActiveSkills;
                var idx = slot - 1;
                if (skills != null && idx >= 0 && idx < skills.Length)
                {
                    var rt = skills[idx];
                    if (rt != null)
                    {
                        skillId = rt.SkillId;
                        return skillId > 0;
                    }
                }
            }

            if (_skillIdsByActorId.TryGetValue(actorId, out var arr) && arr != null)
            {
                var idx = slot - 1;
                if (idx >= 0 && idx < arr.Length)
                {
                    skillId = arr[idx];
                    return skillId > 0;
                }
            }

            return false;
        }

        public void Dispose()
        {
            _skillIdsByActorId.Clear();
        }
    }
}
