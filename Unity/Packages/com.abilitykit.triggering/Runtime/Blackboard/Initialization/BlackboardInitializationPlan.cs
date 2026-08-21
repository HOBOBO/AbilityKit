using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Blackboard
{
    public static class BlackboardInitializationScopes
    {
        public const string Global = "global";
        public const string Owner = "owner";

        public static bool IsGlobal(string scope)
        {
            return string.IsNullOrWhiteSpace(scope) ||
                   string.Equals(scope, Global, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsOwner(string scope)
        {
            return string.Equals(scope, Owner, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(scope, "owner_bound", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(scope, "ownerbound", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Serializable]
    public sealed class BlackboardInitializationPlan
    {
        public int BoardId;
        public string Name;
        public string Scope;
        public string OwnerId;
        public List<BlackboardInitializationKey> Keys = new List<BlackboardInitializationKey>();
    }

    [Serializable]
    public sealed class BlackboardInitializationKey
    {
        public int KeyId;
        public string Name;
        public BlackboardKeyType Type;
        public int IntValue;
        public bool BoolValue;
        public float FloatValue;
        public double DoubleValue;
        public string StringValue;
        public bool CanRead = true;
        public bool CanWrite = true;
    }

    public static class BlackboardInitialization
    {
        public static void Apply(
            IEnumerable<BlackboardInitializationPlan> plans,
            IMutableBlackboardResolver resolver,
            bool replaceExisting = true)
        {
            Apply(plans, resolver, scope: null, replaceExisting: replaceExisting);
        }

        public static void Apply(
            IEnumerable<BlackboardInitializationPlan> plans,
            IMutableBlackboardResolver resolver,
            string scope,
            bool replaceExisting = true)
        {
            if (plans == null) return;
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            foreach (var plan in plans)
            {
                if (plan == null || plan.BoardId == 0) continue;
                if (!MatchesScope(plan.Scope, scope)) continue;
                if (!replaceExisting && resolver.TryResolve(plan.BoardId, out _)) continue;

                var keys = plan.Keys;
                var board = new DictionaryBlackboard(keys != null ? keys.Count : 0);
                if (keys != null)
                {
                    for (var i = 0; i < keys.Count; i++)
                    {
                        ApplyKey(board, keys[i]);
                    }
                }

                resolver.Register(plan.BoardId, board);
            }
        }

        private static bool MatchesScope(string planScope, string requestedScope)
        {
            if (string.IsNullOrWhiteSpace(requestedScope)) return true;
            if (BlackboardInitializationScopes.IsGlobal(requestedScope))
                return BlackboardInitializationScopes.IsGlobal(planScope);
            if (BlackboardInitializationScopes.IsOwner(requestedScope))
                return BlackboardInitializationScopes.IsOwner(planScope);
            return string.Equals(planScope, requestedScope, StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyKey(DictionaryBlackboard board, BlackboardInitializationKey key)
        {
            if (key == null || key.KeyId == 0) return;
            board.DefineKey(key.KeyId, key.Type, key.CanRead, key.CanWrite);
            switch (key.Type)
            {
                case BlackboardKeyType.Bool:
                    board.SetBool(key.KeyId, key.BoolValue);
                    break;
                case BlackboardKeyType.Int:
                    board.SetInt(key.KeyId, key.IntValue);
                    break;
                case BlackboardKeyType.Float:
                    board.SetFloat(key.KeyId, key.FloatValue);
                    break;
                case BlackboardKeyType.Double:
                    board.SetDouble(key.KeyId, key.DoubleValue);
                    break;
                case BlackboardKeyType.String:
                    board.SetString(key.KeyId, key.StringValue ?? string.Empty);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported Blackboard initialization type: {key.Type}. keyId={key.KeyId}");
            }
        }
    }
}
