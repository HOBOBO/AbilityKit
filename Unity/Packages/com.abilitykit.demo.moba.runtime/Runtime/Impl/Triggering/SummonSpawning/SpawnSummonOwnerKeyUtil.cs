using System.Collections.Generic;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Ability.Impl.Triggering.SummonSpawning
{
    public static class SpawnSummonOwnerKeyUtil
    {
        private const string SourceContextIdKey = "effect.sourceContextId";

        public static long ResolveOwnerKey(SpawnSummonSpec.OwnerKeyMode mode, TriggerContext context, int casterActorId)
        {
            if (mode == SpawnSummonSpec.OwnerKeyMode.CasterActorId)
            {
                return casterActorId;
            }

            if (mode == SpawnSummonSpec.OwnerKeyMode.SourceContextId)
            {
                var args = context?.Event.Args;
                if (args != null && args.TryGetValue(SourceContextIdKey, out var v) && v != null)
                {
                    if (v is long l) return l;
                    if (v is int i) return i;
                    if (v is string s && long.TryParse(s, out var parsed)) return parsed;
                }
                return 0;
            }

            return 0;
        }

        public static void WithInjectedOwnerKey(TriggerContext context, long ownerKey, System.Action action)
        {
            if (action == null) return;

            var args = context?.Event.Args as IDictionary<string, object>;
            if (args == null)
            {
                action();
                return;
            }

            var hadOld = args.TryGetValue(SourceContextIdKey, out var old);
            try
            {
                if (ownerKey != 0) args[SourceContextIdKey] = ownerKey;
                else args.Remove(SourceContextIdKey);

                action();
            }
            finally
            {
                if (hadOld) args[SourceContextIdKey] = old;
                else args.Remove(SourceContextIdKey);
            }
        }
    }
}
