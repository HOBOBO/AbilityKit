using System;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Battle;
using AbilityKit.GameplayTags;
using GameplayTagsUtil = AbilityKit.GameplayTags.GameplayTags;

namespace AbilityKit.Game.Editor
{
    internal static class BattleDebugEntityFilterImpl
    {
        public static bool Matches(
            IBattleDiagnosticReadOnlySession session,
            BattleDebugEntityId id,
            string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;

            var parts = filter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (!MatchesToken(session, id, parts[i])) return false;
            }

            return true;
        }

        private static bool MatchesToken(
            IBattleDiagnosticReadOnlySession session,
            BattleDebugEntityId id,
            string token)
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

            if (session == null) return false;

            if (key.Equals("tag", StringComparison.OrdinalIgnoreCase))
            {
                if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorTags)) return false;
                if (!GameplayTagsUtil.TryGet(expr, out var tag)) return false;
                var tags = session.QueryActorTags(1, 0, id.ActorId);
                if (!tags.Status.CanDisplayResults || tags.Items == null) return false;
                for (var i = 0; i < tags.Items.Count; i++)
                {
                    if (GameplayTagManager.Instance.Matches(GameplayTag.FromId(tags.Items[i].TagId), tag))
                        return true;
                }
                return false;
            }

            if (key.Equals("attr", StringComparison.OrdinalIgnoreCase))
            {
                if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorAttributes)) return false;

                if (!TryParseComparison(expr, out var name, out var op, out var rhs))
                {
                    return false;
                }

                var attributes = session.QueryActorAttributes(1, 0, id.ActorId);
                if (!attributes.Status.CanDisplayResults || attributes.Items == null) return false;
                for (var i = 0; i < attributes.Items.Count; i++)
                {
                    var attribute = attributes.Items[i];
                    if (string.Equals(attribute.Name, name, StringComparison.Ordinal) ||
                        (int.TryParse(name, out var rawId) && attribute.AttributeId == rawId))
                        return Compare(attributes.Items[i].FinalValue, op, rhs);
                }
                return false;
            }

            if (key.Equals("effect", StringComparison.OrdinalIgnoreCase) || key.Equals("effects", StringComparison.OrdinalIgnoreCase))
            {
                if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorEffects)) return false;
                var effects = session.QueryActorEffects(1, 0, id.ActorId);
                if (effects.Status.Phase != BattleDiagnosticQueryPhase.Ready &&
                    effects.Status.Phase != BattleDiagnosticQueryPhase.Empty &&
                    effects.Status.Phase != BattleDiagnosticQueryPhase.Partial)
                    return false;
                var count = effects.Items?.Count ?? 0;

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
