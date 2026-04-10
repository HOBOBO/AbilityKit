using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Impl.Moba.Components;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaSkillLoadoutService : IService
    {
        private readonly MobaActorLookupService _actors;
        private readonly Dictionary<int, int[]> _skillIdsByActorId = new Dictionary<int, int[]>();

        public MobaSkillLoadoutService(MobaActorLookupService actors)
        {
            _actors = actors;
        }

        public void SetLoadout(int actorId, int[] skillIds)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            _skillIdsByActorId[actorId] = skillIds ?? Array.Empty<int>();
        }

        public bool TryGetSkillId(int actorId, int slot, out int skillId)
        {
            skillId = 0;
            if (actorId <= 0) return false;
            if (slot <= 0) return false;

            // Prefer ECS component data if available.
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
