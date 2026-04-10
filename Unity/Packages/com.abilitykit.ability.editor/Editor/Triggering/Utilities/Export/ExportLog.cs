#if UNITY_EDITOR
using System;
using AbilityKit.Core.Common.Log;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class ExportLog
    {
        private const string Prefix = "[TriggerExport] ";

        public static void Info(string message)
        {
            Log.Info(Prefix + (message ?? string.Empty));
        }

        public static void Warning(string message)
        {
            Log.Warning(Prefix + (message ?? string.Empty));
        }

        public static void Error(string message)
        {
            Log.Error(Prefix + (message ?? string.Empty));
        }

        public static void Exception(Exception ex, string message)
        {
            Log.Exception(ex, Prefix + (message ?? string.Empty));
        }
    }
}
#endif
