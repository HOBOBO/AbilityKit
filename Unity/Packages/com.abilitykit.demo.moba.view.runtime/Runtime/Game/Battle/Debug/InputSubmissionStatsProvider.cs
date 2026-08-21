using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public static class InputSubmissionStatsProvider
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, InputSubmissionStatsSnapshot> ByScope =
            new Dictionary<string, InputSubmissionStatsSnapshot>(StringComparer.Ordinal);

        /// <summary>仅用于 development single-active 兼容；正式调用方应按 scope 查询。</summary>
        public static InputSubmissionStatsSnapshot Current { get; set; }

        public static bool TryGet(string scope, out InputSubmissionStatsSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(scope)) return false;
            lock (Gate)
            {
                return ByScope.TryGetValue(scope, out snapshot);
            }
        }

        internal static void Publish(string scope, InputSubmissionStatsSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(scope) || snapshot == null) return;
            lock (Gate)
            {
                ByScope[scope] = snapshot;
            }
        }

        internal static void Withdraw(string scope, InputSubmissionStatsSnapshot owner)
        {
            if (string.IsNullOrWhiteSpace(scope) || owner == null) return;
            lock (Gate)
            {
                if (ByScope.TryGetValue(scope, out var current) &&
                    ReferenceEquals(current, owner))
                {
                    ByScope.Remove(scope);
                }
            }
        }
    }
}
