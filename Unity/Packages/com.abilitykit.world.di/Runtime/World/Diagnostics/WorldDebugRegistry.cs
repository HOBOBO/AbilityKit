using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.World.Diagnostics
{
    public static class WorldDebugRegistry
    {
        private static readonly Dictionary<string, WorldCompositionReport> Reports = new Dictionary<string, WorldCompositionReport>(StringComparer.Ordinal);

        public static void Report(WorldCompositionReport report)
        {
            if (report == null) return;
            var key = report.WorldId ?? string.Empty;
            Reports[key] = report;
        }

        public static bool TryGet(string worldId, out WorldCompositionReport report)
        {
            return Reports.TryGetValue(worldId ?? string.Empty, out report);
        }

        public static IReadOnlyCollection<WorldCompositionReport> GetAll()
        {
            return Reports.Values;
        }

        public static void Clear(string worldId)
        {
            Reports.Remove(worldId ?? string.Empty);
        }

        public static void ClearAll()
        {
            Reports.Clear();
        }
    }
}
