using System.Collections.Generic;
using AbilityKit.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    internal static class MobaPerformanceProfiling
    {
        public static bool TryBegin(
            IMobaBattleDiagnosticsService diagnostics,
            string channel,
            string marker,
            out ProbeScope scope)
        {
            scope = default;
            if (!ProfilerHub.IsEnabled ||
                diagnostics == null ||
                !diagnostics.IsEnabled(channel))
            {
                return false;
            }

            scope = ProfilerHub.Begin(marker).ToScope();
            return true;
        }

        public static ProbeScope Begin(
            IMobaBattleDiagnosticsService diagnostics,
            string channel,
            string marker)
        {
            return TryBegin(diagnostics, channel, marker, out var scope)
                ? scope
                : default;
        }

        public static void End(Stack<ProbeScope> scopes)
        {
            if (scopes == null || scopes.Count == 0) return;
            scopes.Pop().Dispose();
        }
    }
}
