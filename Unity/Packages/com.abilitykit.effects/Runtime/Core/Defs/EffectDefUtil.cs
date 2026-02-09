using System;
using AbilityKit.Effects.Core.Defs;

namespace AbilityKit.Effects.Core
{
    public static class EffectDefUtil
    {
        public static bool TryGetScopeKey(EffectScopeDef def, out EffectScopeKey key)
        {
            key = default;
            if (def == null || string.IsNullOrEmpty(def.Kind)) return false;

            if (!TryParseScopeKind(def.Kind, out var kind)) return false;

            key = new EffectScopeKey(kind, def.Id);
            return true;
        }

        public static bool TryParseScopeKind(string kind, out EffectScopeKind scopeKind)
        {
            scopeKind = default;
            if (string.IsNullOrEmpty(kind)) return false;

            if (string.Equals(kind, "Global", StringComparison.OrdinalIgnoreCase)) { scopeKind = EffectScopeKind.Global; return true; }
            if (string.Equals(kind, "Unit", StringComparison.OrdinalIgnoreCase)) { scopeKind = EffectScopeKind.Unit; return true; }
            if (string.Equals(kind, "SkillId", StringComparison.OrdinalIgnoreCase)) { scopeKind = EffectScopeKind.SkillId; return true; }
            if (string.Equals(kind, "LauncherId", StringComparison.OrdinalIgnoreCase)) { scopeKind = EffectScopeKind.LauncherId; return true; }
            if (string.Equals(kind, "ProjectileId", StringComparison.OrdinalIgnoreCase)) { scopeKind = EffectScopeKind.ProjectileId; return true; }
            if (string.Equals(kind, "AoEId", StringComparison.OrdinalIgnoreCase)) { scopeKind = EffectScopeKind.AoEId; return true; }

            return false;
        }
    }
}
