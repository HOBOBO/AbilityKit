using System;
using AbilityKit.Ability.Share.Common.AttributeSystem;
using AbilityKit.GameplayTags;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Game.Battle;
using GameplayTagsUtil = AbilityKit.GameplayTags.GameplayTags;

namespace AbilityKit.Game.Editor
{
    internal static class BattleDebugEntityFilterImpl
    {
        public static bool Matches(IBattleDebugFacade facade, EcsEntityId id, string filter)
        {
            if (facade == null) return false;
            if (string.IsNullOrWhiteSpace(filter)) return true;

            var parts = filter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (!MatchesToken(facade, id, parts[i])) return false;
            }

            return true;
        }

        private static bool MatchesToken(IBattleDebugFacade facade, EcsEntityId id, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return true;

            var idx = token.IndexOf(':');
            if (idx <= 0)
            {
                return id.ToString().Contains(token, StringComparison.OrdinalIgnoreCase);
            }

            var key = token.Substring(0, idx).Trim();
            var expr = token.Substring(idx + 1).Trim();

            if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                return id.ToString().Contains(expr, StringComparison.OrdinalIgnoreCase);
            }

            if (!facade.TryResolveUnit(id, out var unit) || unit == null) return false;

            if (key.Equals("tag", StringComparison.OrdinalIgnoreCase))
            {
                if (unit.Tags == null) return false;
                if (!GameplayTagsUtil.TryGet(expr, out var tag)) return false;
                return unit.Tags.HasTag(tag);
            }

            if (key.Equals("attr", StringComparison.OrdinalIgnoreCase))
            {
                if (unit.Attributes == null) return false;

                if (!TryParseComparison(expr, out var name, out var op, out var rhs))
                {
                    return false;
                }

                if (!AttributeRegistry.Instance.TryGet(name, out var attrId))
                {
                    return false;
                }

                var v = unit.Attributes.GetValue(attrId);
                return Compare(v, op, rhs);
            }

            if (key.Equals("effect", StringComparison.OrdinalIgnoreCase) || key.Equals("effects", StringComparison.OrdinalIgnoreCase))
            {
                var count = unit.Effects?.Active?.Count ?? 0;

                if (string.IsNullOrEmpty(expr)) return count > 0;

                if (TryParseComparison(expr, out var _, out var op, out var rhs))
                {
                    return Compare(count, op, rhs);
                }

                if (int.TryParse(expr, out var exact))
                {
                    return count == exact;
                }

                return count > 0;
            }

            return false;
        }

        private static bool TryParseComparison(string expr, out string name, out string op, out float rhs)
        {
            name = null;
            op = null;
            rhs = 0;

            if (string.IsNullOrWhiteSpace(expr)) return false;

            var candidates = new[] { ">=", "<=", "==", "!=", ">", "<", "=" };
            for (int i = 0; i < candidates.Length; i++)
            {
                var c = candidates[i];
                var p = expr.IndexOf(c, StringComparison.Ordinal);
                if (p <= 0) continue;

                var left = expr.Substring(0, p).Trim();
                var right = expr.Substring(p + c.Length).Trim();
                if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;

                if (!float.TryParse(right, out rhs)) return false;

                name = left;
                op = c == "=" ? "==" : c;
                return true;
            }

            return false;
        }

        private static bool Compare(float lhs, string op, float rhs)
        {
            switch (op)
            {
                case ">":
                    return lhs > rhs;
                case ">=":
                    return lhs >= rhs;
                case "<":
                    return lhs < rhs;
                case "<=":
                    return lhs <= rhs;
                case "==":
                    return Math.Abs(lhs - rhs) < 0.00001f;
                case "!=":
                    return Math.Abs(lhs - rhs) >= 0.00001f;
                default:
                    return false;
            }
        }
    }
}
