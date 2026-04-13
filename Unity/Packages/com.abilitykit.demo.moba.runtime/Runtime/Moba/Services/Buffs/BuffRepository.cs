using System.Collections.Generic;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class BuffRepository
    {
        public List<BuffRuntime> GetOrCreateList(global::ActorEntity target)
        {
            if (target == null) return null;

            if (!target.hasBuffs)
            {
                target.AddBuffs(new List<BuffRuntime>());
            }

            var list = target.buffs.Active;
            if (list == null)
            {
                list = new List<BuffRuntime>();
                target.ReplaceBuffs(list);
            }

            return list;
        }

        public static int FindExistingBuffIndex(List<BuffRuntime> list, int buffId)
        {
            if (list == null) return -1;

            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b == null) continue;
                if (b.BuffId == buffId) return i;
            }

            return -1;
        }
    }
}
