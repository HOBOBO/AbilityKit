using System;
using AbilityKit.Effects.Core.Defs;

namespace AbilityKit.Effects.Core
{
    [Obsolete("EffectDefUtil contains business-specific parsing and should live in the business package (e.g. demo.moba.runtime).", error: false)]
    internal static class EffectDefUtil
    {
        public static bool TryGetScopeKey(EffectScopeDef def, out EffectScopeKey key)
        {
            key = default;
            return false;
        }
    }
}
