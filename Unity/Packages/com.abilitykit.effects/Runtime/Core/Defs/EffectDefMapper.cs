using System;
using System.Collections.Generic;
using AbilityKit.Effects.Core.Defs;
using AbilityKit.Effects.Core.Model;

namespace AbilityKit.Effects.Core
{
    public static class EffectDefMapper
    {
        public static bool TryMap(EffectDef dto, out EffectDefinition def)
        {
            def = null;
            if (dto == null) return false;
            if (string.IsNullOrEmpty(dto.EffectId)) return false;

            var scope = EffectScopeKey.Global();
            if (EffectDefUtil.TryGetScopeKey(dto.DefaultScope, out var parsed))
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

                if (!TryMapValue(key, it.Value, out var value)) continue;

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

        private static bool TryParseStatKey(string key, out EffectStatKey statKey)
        {
            statKey = default;
            if (string.IsNullOrEmpty(key)) return false;

            if (string.Equals(key, "Launcher.IntervalBaseAddFrames", StringComparison.OrdinalIgnoreCase)) { statKey = EffectStatKey.LauncherIntervalBaseAddFrames; return true; }
            if (string.Equals(key, "Launcher.IntervalPostMul", StringComparison.OrdinalIgnoreCase)) { statKey = EffectStatKey.LauncherIntervalPostMul; return true; }
            if (string.Equals(key, "Launcher.ExtraProjectilesPerShot", StringComparison.OrdinalIgnoreCase)) { statKey = EffectStatKey.LauncherExtraProjectilesPerShot; return true; }
            if (string.Equals(key, "Launcher.ExtraTotalCount", StringComparison.OrdinalIgnoreCase)) { statKey = EffectStatKey.LauncherExtraTotalCount; return true; }

            if (string.Equals(key, "Projectile.DamageMul", StringComparison.OrdinalIgnoreCase)) { statKey = EffectStatKey.ProjectileDamageMul; return true; }
            if (string.Equals(key, "Projectile.SpeedMul", StringComparison.OrdinalIgnoreCase)) { statKey = EffectStatKey.ProjectileSpeedMul; return true; }
            if (string.Equals(key, "Projectile.Pierce", StringComparison.OrdinalIgnoreCase)) { statKey = EffectStatKey.ProjectilePierce; return true; }

            return false;
        }

        private static bool TryMapValue(EffectStatKey key, EffectValueDef dto, out EffectValue value)
        {
            value = default;
            if (dto == null) return false;

            switch (key)
            {
                case EffectStatKey.LauncherIntervalBaseAddFrames:
                case EffectStatKey.LauncherExtraProjectilesPerShot:
                case EffectStatKey.LauncherExtraTotalCount:
                case EffectStatKey.ProjectilePierce:
                    value = new EffectValue(dto.I);
                    return true;

                case EffectStatKey.LauncherIntervalPostMul:
                case EffectStatKey.ProjectileDamageMul:
                case EffectStatKey.ProjectileSpeedMul:
                    value = new EffectValue(dto.F);
                    return true;

                default:
                    return false;
            }
        }
    }
}
