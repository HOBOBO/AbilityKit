using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaSkillLoadoutService
    {
        private readonly Dictionary<int, int[]> _skillIdsByActorId = new Dictionary<int, int[]>();

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
    }
}
