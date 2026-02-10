using System;
using System.Collections.Generic;
using AbilityKit.Effects.Core.Defs;
using AbilityKit.Effects.Core.Model;

namespace AbilityKit.Effects.Core
{
    [Obsolete("EffectDefMapper contains business-specific mapping and should live in the business package (e.g. demo.moba.runtime).", error: false)]
    internal static class EffectDefMapper
    {
        public static bool TryMap(EffectDef dto, out EffectDefinition def)
        {
            def = null;
            return false;
        }
    }
}
