using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba.Effects.Model;
using AbilityKit.Effects.Core;
using AbilityKit.Effects.Core.Defs;
using AbilityKit.Effects.Core.Model;

namespace AbilityKit.Ability.Impl.Moba.Effects.Config
{
    internal static class MobaEffectDefMapper
    {
        public static bool TryMap(EffectDef dto, out EffectDefinition def)
        {
            def = null;
            if (dto == null) return false;
            if (string.IsNullOrEmpty(dto.EffectId)) return false;

            var scope = MobaEffectScopeKeys.Global();
            if (MobaEffectDefUtil.TryGetScopeKey(dto.DefaultScope, out var parsed))
            {
                scope = parsed;
            }

            var stats = MapStats(dto.Items);
            def = new EffectDefinition(dto.EffectId, in scope, stats);
            return true;
        }

        private static EffectStatItem[] MapStats(EffectItemDef[] items)
        {
            if (items == null || items.Length == 0) return Array.Empty<EffectStatItem>();

            var list = new List<EffectStatItem>(items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                if (it == null) continue;
                if (!string.Equals(it.Type, "Stat", StringComparison.OrdinalIgnoreCase)) continue;

                if (!TryParseStatKey(it.Key, out var key)) continue;
                if (!TryParseOp(it.Op, out var op)) continue;

                if (!TryMapValue((MobaEffectStatKey)key, it.Value, out var value)) continue;

                list.Add(new EffectStatItem(key, op, in value));
            }

            return list.Count == 0 ? Array.Empty<EffectStatItem>() : list.ToArray();
        }

        private static bool TryParseOp(string op, out EffectOp effectOp)
        {
            effectOp = default;
            if (string.IsNullOrEmpty(op)) return false;

            if (string.Equals(op, "Add", StringComparison.OrdinalIgnoreCase)) { effectOp = EffectOp.Add; return true; }
            if (string.Equals(op, "Mul", StringComparison.OrdinalIgnoreCase)) { effectOp = EffectOp.Mul; return true; }

            return false;
        }

        private static bool TryParseStatKey(string key, out int statKey)
        {
            statKey = default;
            if (string.IsNullOrEmpty(key)) return false;

            if (string.Equals(key, "Launcher.IntervalBaseAddFrames", StringComparison.OrdinalIgnoreCase)) { statKey = (int)MobaEffectStatKey.LauncherIntervalBaseAddFrames; return true; }
            if (string.Equals(key, "Launcher.IntervalPostMul", StringComparison.OrdinalIgnoreCase)) { statKey = (int)MobaEffectStatKey.LauncherIntervalPostMul; return true; }
            if (string.Equals(key, "Launcher.ExtraProjectilesPerShot", StringComparison.OrdinalIgnoreCase)) { statKey = (int)MobaEffectStatKey.LauncherExtraProjectilesPerShot; return true; }
            if (string.Equals(key, "Launcher.ExtraTotalCount", StringComparison.OrdinalIgnoreCase)) { statKey = (int)MobaEffectStatKey.LauncherExtraTotalCount; return true; }

            if (string.Equals(key, "Projectile.DamageMul", StringComparison.OrdinalIgnoreCase)) { statKey = (int)MobaEffectStatKey.ProjectileDamageMul; return true; }
            if (string.Equals(key, "Projectile.SpeedMul", StringComparison.OrdinalIgnoreCase)) { statKey = (int)MobaEffectStatKey.ProjectileSpeedMul; return true; }
            if (string.Equals(key, "Projectile.Pierce", StringComparison.OrdinalIgnoreCase)) { statKey = (int)MobaEffectStatKey.ProjectilePierce; return true; }

            return false;
        }

        private static bool TryMapValue(MobaEffectStatKey key, EffectValueDef dto, out EffectValue value)
        {
            value = default;
            if (dto == null) return false;

            switch (key)
            {
                case MobaEffectStatKey.LauncherIntervalBaseAddFrames:
                case MobaEffectStatKey.LauncherExtraProjectilesPerShot:
                case MobaEffectStatKey.LauncherExtraTotalCount:
                case MobaEffectStatKey.ProjectilePierce:
                    value = new EffectValue(dto.I);
                    return true;

                case MobaEffectStatKey.LauncherIntervalPostMul:
                case MobaEffectStatKey.ProjectileDamageMul:
                case MobaEffectStatKey.ProjectileSpeedMul:
                    value = new EffectValue(dto.F);
                    return true;

                default:
                    return false;
            }
        }
    }
}
