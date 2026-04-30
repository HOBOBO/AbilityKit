using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbilityKit.Diagnostics;

namespace AbilityKit.Diagnostics.Exporters
{
    /// <summary>
    /// 导出器接口
    /// </summary>
    public interface IExporter
    {
        string Extension { get; }
        void Export(ProfilerSnapshot snapshot, string filePath);
        string ExportToString(ProfilerSnapshot snapshot);
    }

    /// <summary>
    /// JSON 导出器
    /// </summary>
    public sealed class JsonExporter : IExporter
    {
        public string Extension => ".json";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            var json = ExportToString(snapshot);
            File.WriteAllText(filePath, json);
        }

        public string ExportToString(ProfilerSnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"sessionId\": \"{snapshot.SessionId}\",");
            sb.AppendLine($"  \"timestamp\": {snapshot.Timestamp},");
            sb.AppendLine("  \"counters\": {");

            var counterIndex = 0;
            foreach (var kvp in snapshot.Counters)
            {
                var comma = ++counterIndex < snapshot.Counters.Count ? "," : "";
                sb.AppendLine($"    \"{EscapeJson(kvp.Key)}\": {kvp.Value.Value}{comma}");
            }
            sb.AppendLine("  },");

            sb.AppendLine("  \"samples\": {");
            var sampleIndex = 0;
            foreach (var kvp in snapshot.Samples)
            {
                var comma = ++sampleIndex < snapshot.Samples.Count ? "," : "";
                sb.AppendLine($"    \"{EscapeJson(kvp.Key)}\": {{");
                sb.AppendLine($"      \"count\": {kvp.Value.Count},");
                if (kvp.Value.Count > 0)
                {
                    double sum = 0, min = double.MaxValue, max = double.MinValue;
                    foreach (var v in kvp.Value)
                    {
                        sum += v;
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                    sb.AppendLine($"      \"sum\": {sum:F4},");
                    sb.AppendLine($"      \"mean\": {sum / kvp.Value.Count:F4},");
                    sb.AppendLine($"      \"min\": {min:F4},");
                    sb.AppendLine($"      \"max\": {max:F4}");
                }
                else
                {
                    sb.AppendLine($"      \"sum\": 0,");
                    sb.AppendLine($"      \"mean\": 0,");
                    sb.AppendLine($"      \"min\": 0,");
                    sb.AppendLine($"      \"max\": 0");
                }
                sb.AppendLine($"    }}{comma}");
            }
            sb.AppendLine("  },");

            sb.AppendLine("  \"flame\": {");
            ExportFlameNode(sb, snapshot.Root, "    ");
            sb.AppendLine("  }");

            sb.AppendLine("}");
            return sb.ToString();
        }

        private void ExportFlameNode(StringBuilder sb, FlameRoot root, string indent)
        {
            foreach (var categoryKvp in root.Roots)
            {
                sb.AppendLine($"{indent}\"{categoryKvp.Key}\": {{");
                ExportNode(sb, categoryKvp.Value, indent + "  ");
                sb.AppendLine($"{indent}}}");
            }
        }

        private void ExportNode(StringBuilder sb, FlameNode node, string indent)
        {
            sb.AppendLine($"{indent}\"{node.Name}\": {{");
            sb.AppendLine($"{indent}  \"totalMs\": {node.TotalNanoseconds / 1_000_000.0:F4},");
            sb.AppendLine($"{indent}  \"selfMs\": {node.SelfNanoseconds / 1_000_000.0:F4},");
            sb.AppendLine($"{indent}  \"hits\": {node.HitCount},");

            if (node.Children.Count > 0)
            {
                sb.AppendLine($"{indent}  \"children\": {{");
                var i = 0;
                foreach (var child in node.Children)
                {
                    var comma = ++i < node.Children.Count ? "," : "";
                    ExportNode(sb, child.Value, indent + "    ");
                    sb.AppendLine($"{indent}    }}{comma}");
                }
                sb.AppendLine($"{indent}  }}");
            }
            else
            {
                sb.AppendLine($"{indent}  \"children\": {{}}");
            }
            sb.AppendLine($"{indent}}}");
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    /// <summary>
    /// CSV 导出器
    /// </summary>
    public sealed class CsvExporter : IExporter
    {
        public string Extension => ".csv";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            var csv = ExportToString(snapshot);
            File.WriteAllText(filePath, csv);
        }

        public string ExportToString(ProfilerSnapshot snapshot)
        {
            var sb = new StringBuilder();

            // Counters
            sb.AppendLine("# Counters");
            sb.AppendLine("Name,Value,Delta");
            foreach (var kvp in snapshot.Counters)
            {
                sb.AppendLine($"{EscapeCsv(kvp.Key)},{kvp.Value.Value},{kvp.Value.Delta}");
            }
            sb.AppendLine();

            // Samples
            sb.AppendLine("# Samples");
            sb.AppendLine("Name,Count,Sum,Mean,Min,Max");
            foreach (var kvp in snapshot.Samples)
            {
                var list = kvp.Value;
                if (list.Count == 0)
                {
                    sb.AppendLine($"{EscapeCsv(kvp.Key)},0,0,0,0,0");
                    continue;
                }

                double sum = 0, min = double.MaxValue, max = double.MinValue;
                foreach (var v in list)
                {
                    sum += v;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
                sb.AppendLine($"{EscapeCsv(kvp.Key)},{list.Count},{sum:F4},{sum / list.Count:F4},{min:F4},{max:F4}");
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string s)
        {
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
            {
                return $"\"{s.Replace("\"", "\"\"")}\"";
            }
            return s;
        }
    }

    /// <summary>
    /// 折叠式火焰图格式导出器（适用于 Chrome/Firefox Perf工具）
    /// </summary>
    public sealed class FoldedExporter : IExporter
    {
        public string Extension => ".folded";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            var folded = ExportToString(snapshot);
            File.WriteAllText(filePath, folded);
        }

        public string ExportToString(ProfilerSnapshot snapshot)
        {
            var sb = new StringBuilder();

            // 使用堆栈跟踪格式: method1;method2;method3 count
            foreach (var categoryKvp in snapshot.Root.Roots)
            {
                ExportNodeStack(sb, categoryKvp.Value, "");
            }

            return sb.ToString();
        }

        private void ExportNodeStack(StringBuilder sb, FlameNode node, string path)
        {
            var newPath = string.IsNullOrEmpty(path) ? node.Name : $"{path};{node.Name}";

            if (node.HitCount > 0)
            {
                sb.AppendLine($"{newPath} {node.HitCount}");
            }

            foreach (var child in node.Children)
            {
                ExportNodeStack(sb, child.Value, newPath);
            }
        }
    }

    /// <summary>
    /// 导出器工厂
    /// </summary>
    public static class ExporterFactory
    {
        private static readonly Dictionary<string, IExporter> _exporters = new()
        {
            { "json", new JsonExporter() },
            { "csv", new CsvExporter() },
            { "folded", new FoldedExporter() },
            { "speedscope", new SpeedScopeExporter() },
            { "chrome", new ChromePerfExporter() },
            { "flamegraph", new FlameGraphExporter() },
            { "0x", new ZeroxExporter() }
        };

        public static IExporter Get(string format)
        {
            return _exporters.TryGetValue(format.ToLower(), out var exporter) ? exporter : new JsonExporter();
        }

        public static string[] GetSupportedFormats() => new[] { "json", "csv", "folded", "speedscope", "chrome", "flamegraph", "0x" };

        public static string[] GetRecommendedFormats() => new[] { "speedscope", "chrome", "flamegraph" };
    }
}
