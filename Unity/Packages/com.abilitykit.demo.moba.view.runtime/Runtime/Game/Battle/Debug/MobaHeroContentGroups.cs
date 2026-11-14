using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Availability classification for the demo's formal and placeholder hero content.
    /// This affects local content selection only; it does not change gameplay behavior.
    /// </summary>
    internal static class MobaHeroContentGroups
    {
        private static readonly HashSet<int> FormalHeroIds = new HashSet<int>
        {
            3001, 3002, 3003, 3004, 3005, 3006, 3007
        };

        internal static bool IsFormal(int heroId) => FormalHeroIds.Contains(heroId);

        internal static bool IsEnabled(int heroId, bool includePlaceholders) =>
            IsFormal(heroId) || includePlaceholders;
    }
}
