#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Diagnostics;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 将 JSON 格式层产生的诊断映射到计划加载诊断，不改变加载结果。
    /// </summary>
    public static class TriggerPlanJsonDiagnosticAdapter
    {
        /// <summary>
        /// 按原顺序记录诊断，保留来源、路径、消息与原严重度文本。
        /// 未提供诊断序列或序列为空时不产生记录；未识别的严重度按提示计数。
        /// </summary>
        public static void RecordTo(TriggeringDiagnosticCollector collector, IEnumerable<TriggerPlanJsonDiagnostic>? diagnostics)
        {
            if (collector == null) throw new ArgumentNullException(nameof(collector));
            if (diagnostics == null) return;

            foreach (var diagnostic in diagnostics)
            {
                var severity = diagnostic.Severity switch
                {
                    TriggerPlanJsonDiagnosticSeverity.Error => TriggeringLoadDiagnosticSeverity.Error,
                    TriggerPlanJsonDiagnosticSeverity.Warning => TriggeringLoadDiagnosticSeverity.Warning,
                    _ => TriggeringLoadDiagnosticSeverity.Info
                };
                collector.RecordLoadDiagnostic(severity, diagnostic.Message, diagnostic.SourceName, diagnostic.Path, diagnostic.Severity.ToString());
            }
        }
    }
}
