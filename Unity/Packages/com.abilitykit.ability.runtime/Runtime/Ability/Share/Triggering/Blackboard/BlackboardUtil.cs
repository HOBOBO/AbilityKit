using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering.Blackboard
{
    public static class BlackboardUtil
    {
        public static void CopyKeys(IBlackboard from, IBlackboard to, IReadOnlyList<string> keys)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));
            if (keys == null) throw new ArgumentNullException(nameof(keys));

            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (key == null) continue;

                if (from.TryGet(key, out var value))
                {
                    to.Set(key, value);
                }
            }
        }
    }
}
