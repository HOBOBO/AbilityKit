using System.Collections.Generic;
using AbilityKit.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 性能探针的统一入口。
    /// DetailMarkers 开启时探针名追加业务标识（如 moba.effect.action[803]），
    /// 火焰图随之细化到具体配置/动作；默认关闭以保持指标名低基数。
    /// </summary>
    public static class MobaPerformanceProfiling
    {
        /// <summary>
        /// 是否在探针名中追加业务明细标识（配置 ID / 动作名）。由宿主按需开启。
        /// </summary>
        public static bool DetailMarkers;

        public static bool TryBegin(
            IMobaBattleDiagnosticsService diagnostics,
            string channel,
            string marker,
            out ProbeScope scope)
        {
            return TryBegin(diagnostics, channel, marker, 0L, 0L, out scope);
        }

        public static bool TryBegin(
            IMobaBattleDiagnosticsService diagnostics,
            string channel,
            string marker,
            long detailId,
            long contextId,
            out ProbeScope scope)
        {
            scope = default;
            if (!ProfilerHub.IsEnabled ||
                diagnostics == null ||
                !diagnostics.IsEnabled(channel))
            {
                return false;
            }

            var qualifiedMarker = DetailMarkers && detailId != 0L
                ? marker + "[" + detailId + "]"
                : marker;
            scope = ProfilerHub.Begin(qualifiedMarker, contextId).ToScope();
            return true;
        }

        public static bool TryBeginNamed(
            IMobaBattleDiagnosticsService diagnostics,
            string channel,
            string marker,
            string detailName,
            long contextId,
            out ProbeScope scope)
        {
            scope = default;
            if (!ProfilerHub.IsEnabled ||
                diagnostics == null ||
                !diagnostics.IsEnabled(channel))
            {
                return false;
            }

            var qualifiedMarker = DetailMarkers && !string.IsNullOrEmpty(detailName)
                ? marker + ":" + detailName
                : marker;
            scope = ProfilerHub.Begin(qualifiedMarker, contextId).ToScope();
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

        public static void End(Stack<ProbeScope> stacks)
        {
            if (stacks == null || stacks.Count == 0) return;
            stacks.Pop().Dispose();
        }
    }
}
