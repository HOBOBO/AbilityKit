using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbilityKit.Diagnostics;

namespace AbilityKit.Diagnostics.Exporters
{
    /// <summary>
    /// SpeedScope 格式导出器
    /// https://www.speedscope.app
    /// </summary>
    public sealed class SpeedScopeExporter : IExporter
    {
        public string Extension => ".speedscope.json";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            var json = ExportToString(snapshot);
            File.WriteAllText(filePath, json);
        }

        public string ExportToString(ProfilerSnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"$schema\": \"https://www.speedscope.app/schema/v1.json\",");
            sb.AppendLine("  \"exporter\": \"AbilityKit.Diagnostics\",");
            sb.AppendLine($"  \"version\": \"0.0.1\",");
            sb.AppendLine("  \"mode\": \"flamegraph\",");
            sb.AppendLine("  \"profile\": {");
            sb.AppendLine("    \"type\": \"evented\",");
            sb.AppendLine("    \"unit\": \"nanoseconds\",");
            sb.AppendLine("    \"names\": [");

            // 收集所有唯一的名称
            var names = CollectNames(snapshot.Root);
            for (int i = 0; i < names.Count; i++)
            {
                sb.AppendLine($"      \"{EscapeJson(names[i])}\"{(i < names.Count - 1 ? "," : "")}");
            }
            sb.AppendLine("    ],");
            sb.AppendLine("    \"startValue\": 0,");
            sb.AppendLine("    \"endValue\": " + GetTotalTime(snapshot.Root) + ",");
            sb.AppendLine("    \"events\": [");

            // 导出事件 - 遍历每个分类根节点
            var events = new List<string>();
            long timeOffset = 0;
            foreach (var rootKvp in snapshot.Root.Roots)
            {
                ExportEvents(sb, rootKvp.Value, timeOffset, "", events);
                timeOffset += rootKvp.Value.TotalNanoseconds;
            }

            for (int i = 0; i < events.Count; i++)
            {
                sb.AppendLine($"      {events[i]}{(i < events.Count - 1 ? "," : "")}");
            }

            sb.AppendLine("    ]");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private List<string> CollectNames(FlameRoot root)
        {
            var names = new HashSet<string>();
            foreach (var rootNode in root.Roots.Values)
            {
                CollectNamesRecursive(rootNode, names);
            }
            return new List<string>(names);
        }

        private void CollectNamesRecursive(FlameNode node, HashSet<string> names)
        {
            names.Add(node.Name);
            foreach (var child in node.Children.Values)
            {
                CollectNamesRecursive(child, names);
            }
        }

        private long GetTotalTime(FlameRoot root)
        {
            long total = 0;
            foreach (var r in root.Roots.Values)
            {
                total += r.TotalNanoseconds;
            }
            return total > 0 ? total : 1;
        }

        private void ExportEvents(StringBuilder sb, FlameNode node, long startTime, string path, List<string> events)
        {
            var newPath = string.IsNullOrEmpty(path) ? node.Name : path + ";" + node.Name;

            // B 事件 (开始)
            events.Add($"{{\"type\":\"B\",\"name\":\"{EscapeJson(node.Name)}\"}}");

            long currentTime = startTime;
            foreach (var child in node.Children.Values)
            {
                ExportEvents(sb, child, currentTime, newPath, events);
                currentTime += child.TotalNanoseconds;
            }

            // E 事件 (结束)
            events.Add($"{{\"type\":\"E\",\"name\":\"{EscapeJson(node.Name)}\"}}");
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }

    /// <summary>
    /// Chrome DevTools Performance 格式导出器
    /// https://developer.chrome.com/docs/devtools/performance/reference
    /// </summary>
    public sealed class ChromePerfExporter : IExporter
    {
        public string Extension => ".chrome-perf.json";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            var json = ExportToString(snapshot);
            File.WriteAllText(filePath, json);
        }

        public string ExportToString(ProfilerSnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"traceEvents\": [");

            var events = new List<string>();
            var nodes = new List<(FlameNode node, double startMs, double durationMs, int depth)>();

            // 收集节点
            foreach (var rootKvp in snapshot.Root.Roots)
            {
                CollectNodes(rootKvp.Value, 0, 0, rootKvp.Key, nodes);
            }

            // 排序按开始时间
            nodes.Sort((a, b) => a.startMs.CompareTo(b.startMs));

            // 生成事件
            long eventId = 0;
            foreach (var (node, startMs, durationMs, depth) in nodes)
            {
                // 类别映射
                var cat = node.Category ?? "default";
                var color = GetCategoryColor(cat);

                // 完整跟踪事件
                events.Add($"{{\"name\":\"{EscapeJson(node.Name)}\",\"cat\":\"{cat}\",\"ph\":\"X\",\"ts\":{startMs * 1000},\"dur\":{durationMs * 1000},\"pid\":0,\"tid\":0,\"args\":{{}}}}");

                // B/E 事件对
                // events.Add($"{{\"name\":\"{EscapeJson(node.Name)}\",\"cat\":\"{cat}\",\"ph\":\"B\",\"ts\":{startMs * 1000},\"pid\":0,\"tid\":0,\"args\":{{}}}}");
                // events.Add($"{{\"name\":\"{EscapeJson(node.Name)}\",\"cat\":\"{cat}\",\"ph\":\"E\",\"ts\":{(startMs + durationMs) * 1000},\"pid\":0,\"tid\":0,\"args\":{{}}}}");
            }

            for (int i = 0; i < events.Count; i++)
            {
                sb.AppendLine($"    {events[i]}{(i < events.Count - 1 ? "," : "")}");
            }

            sb.AppendLine("  ],");
            sb.AppendLine("  \"metadata\": {");
            sb.AppendLine($"    \"sessionId\": \"{snapshot.SessionId}\",");
            sb.AppendLine($"    \"exportedAt\": \"{DateTimeOffset.UtcNow:O}\"");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private void CollectNodes(FlameNode node, double startMs, int depth, string category, List<(FlameNode, double, double, int)> nodes)
        {
            var durationMs = node.TotalNanoseconds / 1_000_000.0;
            nodes.Add((node, startMs, durationMs, depth));

            double childStart = startMs;
            foreach (var child in node.Children.Values)
            {
                CollectNodes(child, childStart, depth + 1, category, nodes);
                childStart += child.TotalNanoseconds / 1_000_000.0;
            }
        }

        private static string GetCategoryColor(string category)
        {
            return category switch
            {
                "pipeline" => "olive",
                "trigger" => "blue",
                "ability" => "green",
                "context" => "purple",
                _ => "grey"
            };
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    /// <summary>
    /// 折叠式火焰图格式（ Brendan Gregg 格式）
    /// 用于 flamegraph.pl 工具
    /// https://github.com/brendangregg/FlameGraph
    /// </summary>
    public sealed class FlameGraphExporter : IExporter
    {
        public string Extension => ".folded.txt";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            var folded = ExportToString(snapshot);
            File.WriteAllText(filePath, folded);
        }

        public string ExportToString(ProfilerSnapshot snapshot)
        {
            var sb = new StringBuilder();

            foreach (var rootKvp in snapshot.Root.Roots)
            {
                ExportNodeStack(sb, rootKvp.Value, "");
            }

            return sb.ToString();
        }

        private void ExportNodeStack(StringBuilder sb, FlameNode node, string path)
        {
            var newPath = string.IsNullOrEmpty(path) ? node.Name : $"{path};{node.Name}";

            // 每一行: func1;func2;func3 count
            if (node.HitCount > 0)
            {
                sb.AppendLine($"{newPath} {node.HitCount}");
            }

            foreach (var child in node.Children.Values)
            {
                ExportNodeStack(sb, child, newPath);
            }
        }
    }

    /// <summary>
    /// 0xFF 火焰图格式
    /// 用于 0x pariatur 工具
    /// https://github.com/daniel交收/0x
    /// </summary>
    public sealed class ZeroxExporter : IExporter
    {
        public string Extension => ".0x.json";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            var json = ExportToString(snapshot);
            File.WriteAllText(filePath, json);
        }

        public string ExportToString(ProfilerSnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"type\": \"single\",");
            sb.AppendLine("  \"format\": \"leaf\",");
            sb.AppendLine("  \"nodes\": [");

            var nodes = new List<string>();
            foreach (var rootKvp in snapshot.Root.Roots)
            {
                ExportNodes(sb, rootKvp.Value, "", nodes);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                sb.AppendLine($"  {nodes[i]}{(i < nodes.Count - 1 ? "," : "")}");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private void ExportNodes(StringBuilder sb, FlameNode node, string path, List<string> nodes)
        {
            var newPath = string.IsNullOrEmpty(path) ? node.Name : $"{path};{node.Name}";
            var selfNs = Math.Max(0, node.SelfNanoseconds);

            var nodeJson = $"{{\"name\":\"{EscapeJson(newPath)}\",\"value\":{selfNs}}}";
            nodes.Add(nodeJson);

            foreach (var child in node.Children.Values)
            {
                ExportNodes(sb, child, newPath, nodes);
            }
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
