using System;
using AbilityKit.Ability.Impl.Moba.Effects.Model;
using AbilityKit.Effects.Core;
using AbilityKit.Effects.Core.Defs;

namespace AbilityKit.Ability.Impl.Moba.Effects.Config
{
    internal static class MobaEffectDefUtil
    {
        public static bool TryGetScopeKey(EffectScopeDef def, out EffectScopeKey key)
        {
            key = default;
            if (def == null || string.IsNullOrEmpty(def.Kind)) return false;

            if (!TryParseScopeKindId(def.Kind, out var kindId)) return false;

            key = new EffectScopeKey(kindId, def.Id);
            return true;
        }

        public static bool TryParseScopeKindId(string kind, out byte kindId)
        {
            kindId = default;
            if (string.IsNullOrEmpty(kind)) return false;

            if (string.Equals(kind, "Global", StringComparison.OrdinalIgnoreCase)) { kindId = (byte)MobaEffectScopeKind.Global; return true; }
            if (string.Equals(kind, "Unit", StringComparison.OrdinalIgnoreCase)) { kindId = (byte)MobaEffectScopeKind.Unit; return true; }
            if (string.Equals(kind, "SkillId", StringComparison.OrdinalIgnoreCase)) { kindId = (byte)MobaEffectScopeKind.SkillId; return true; }
            if (string.Equals(kind, "LauncherId", StringComparison.OrdinalIgnoreCase)) { kindId = (byte)MobaEffectScopeKind.LauncherId; return true; }
            if (string.Equals(kind, "ProjectileId", StringComparison.OrdinalIgnoreCase)) { kindId = (byte)MobaEffectScopeKind.ProjectileId; return true; }
            if (string.Equals(kind, "AoEId", StringComparison.OrdinalIgnoreCase)) { kindId = (byte)MobaEffectScopeKind.AoEId; return true; }

            return false;
        }
    }
}
