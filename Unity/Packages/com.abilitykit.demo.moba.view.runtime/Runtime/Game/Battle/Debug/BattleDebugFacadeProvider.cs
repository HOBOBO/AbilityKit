using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Battle
{
    public static class BattleDebugFacadeProvider
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, IBattleDebugFacade> ByScope =
            new Dictionary<string, IBattleDebugFacade>(StringComparer.Ordinal);

        /// <summary>仅用于 development single-active 兼容；正式调用方应按 scope 查询。</summary>
        public static IBattleDebugFacade Current { get; set; }

        public static bool TryGet(string scope, out IBattleDebugFacade facade)
        {
            facade = null;
            if (string.IsNullOrWhiteSpace(scope)) return false;
            lock (Gate)
            {
                return ByScope.TryGetValue(scope, out facade);
            }
        }

        internal static void Publish(string scope, IBattleDebugFacade facade)
        {
            if (string.IsNullOrWhiteSpace(scope) || facade == null) return;
            lock (Gate)
            {
                ByScope[scope] = facade;
            }
        }

        internal static void Withdraw(string scope, IBattleDebugFacade owner)
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
