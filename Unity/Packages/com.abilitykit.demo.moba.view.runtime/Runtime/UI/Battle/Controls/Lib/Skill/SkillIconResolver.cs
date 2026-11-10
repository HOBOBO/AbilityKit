using UnityEngine;
using System.Collections.Generic;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    /// <summary>
    /// Resolves the presentation sprite for a skill icon ID.
    /// The ID remains authoritative data; the resource path is a shared convention
    /// so individual heroes do not need special handling in the HUD.
    /// </summary>
    internal static class SkillIconResolver
    {
        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();
        private static readonly string[] PathFormats =
        {
            "moba/icons/{0}",
            "moba/icons/skill_{0}",
            "icons/{0}",
            "icons/skill_{0}"
        };

        public static Sprite Resolve(int iconId)
        {
            if (iconId <= 0) return null;
            if (Cache.TryGetValue(iconId, out var cached)) return cached;

            Sprite resolved = null;
            for (var i = 0; i < PathFormats.Length; i++)
            {
                var sprite = Resources.Load<Sprite>(string.Format(PathFormats[i], iconId));
                if (sprite != null)
                {
                    resolved = sprite;
                    break;
                }
            }

            // Cache misses as well. HUD presentation can refresh every frame while
            // a slot changes state; repeated Resources.Load calls are unnecessary.
            Cache[iconId] = resolved;
            return resolved;
        }
    }
}
