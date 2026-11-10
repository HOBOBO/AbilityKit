using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using AbilityKit.Diagnostics;

namespace AbilityKit.Diagnostics.Exporters
{
    /// <summary>
    /// Profiler snapshot exporter.
    /// </summary>
    public interface IExporter
    {
        string Extension { get; }
        void Export(ProfilerSnapshot snapshot, string filePath);
        string ExportToString(ProfilerSnapshot snapshot);
    }

    public sealed class JsonExporter : IExporter
    {
        // 无 BOM 的 UTF-8：folded/speedscope 等产物会被外部火焰图工具逐行解析，
        // 带 BOM 会让首行帧名带上不可见前缀。
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public string Extension => ".json";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            using (var writer = CreateWriter(filePath))
            {
                WriteTo(snapshot, writer);
            }
        }

        public string ExportToString(ProfilerSnapshot snapshot)
        {
            var sb = new StringBuilder(4096);
            using (var writer = new StringWriter(sb, CultureInfo.InvariantCulture))
            {
                WriteTo(snapshot, writer);
            }

            return sb.ToString();
        }

        private static void WriteTo(ProfilerSnapshot snapshot, TextWriter writer)
        {
            writer.WriteLine("{");
            AppendJsonProperty(writer, "sessionId", snapshot.SessionId, 1, true);
            AppendJsonProperty(writer, "timestamp", snapshot.Timestamp.ToString(CultureInfo.InvariantCulture), 1, true, rawValue: true);
            AppendJsonProperty(writer, "durationMs", GetDurationMilliseconds(snapshot).ToString("F4", CultureInfo.InvariantCulture), 1, true, rawValue: true);

            writer.WriteLine("  \"metrics\": {");
            var metricIndex = 0;
            if (snapshot.Metrics != null)
            {
                foreach (var kvp in snapshot.Metrics)
                {
                    var comma = ++metricIndex < snapshot.Metrics.Count ? "," : string.Empty;
                    var record = kvp.Value;
                    writer.WriteLine($"    \"{EscapeJson(kvp.Key)}\": {{\"category\": \"{EscapeJson(record.Category)}\", \"kind\": \"{record.Kind}\", \"unit\": \"{EscapeJson(record.Unit)}\", \"description\": \"{EscapeJson(record.Description)}\", \"tags\": \"{EscapeJson(JoinTags(record.Tags))}\"}}{comma}");
                }
            }
            writer.WriteLine("  },");

            writer.WriteLine("  \"options\": {");
            if (snapshot.Options != null)
            {
                AppendJsonProperty(writer, "enabled", snapshot.Options.Enabled ? "true" : "false", 2, true, rawValue: true);
                AppendJsonProperty(writer, "collectionLevel", snapshot.Options.CollectionLevel.ToString(), 2, true);
                AppendJsonProperty(writer, "defaultSampleRate", Format(snapshot.Options.DefaultSampleRate), 2, true, rawValue: true);
                AppendJsonProperty(writer, "maxSamplesPerMetric", snapshot.Options.MaxSamplesPerMetric.ToString(CultureInfo.InvariantCulture), 2, true, rawValue: true);
                AppendJsonProperty(writer, "maxDiagnosticEvents", snapshot.Options.MaxDiagnosticEvents.ToString(CultureInfo.InvariantCulture), 2, true, rawValue: true);
                AppendJsonProperty(writer, "disabledCategories", EscapeJson(JoinSet(snapshot.Options.DisabledCategories)), 2, true);
                AppendJsonProperty(writer, "categorySampleRates", EscapeJson(JoinRates(snapshot.Options.CategorySampleRates)), 2, true);
                AppendJsonProperty(writer, "metricSampleRates", EscapeJson(JoinRates(snapshot.Options.MetricSampleRates)), 2, false);
            }
            writer.WriteLine("  },");

            writer.WriteLine("  \"sessions\": [");
            if (snapshot.Sessions != null)
            {
                for (var i = 0; i < snapshot.Sessions.Count; i++)
                {
                    var record = snapshot.Sessions[i];
                    writer.WriteLine("    {");
                    AppendJsonProperty(writer, "sessionId", record.SessionId ?? string.Empty, 3, true);
                    AppendJsonProperty(writer, "label", record.Label ?? string.Empty, 3, true);
                    AppendJsonProperty(writer, "savedTimestamp", record.SavedTimestamp.ToString(CultureInfo.InvariantCulture), 3, true, rawValue: true);
                    AppendJsonProperty(writer, "durationMs", Format(record.DurationMilliseconds), 3, true, rawValue: true);
                    AppendJsonProperty(writer, "metrics", record.MetricCount.ToString(CultureInfo.InvariantCulture), 3, true, rawValue: true);
                    AppendJsonProperty(writer, "counters", record.CounterCount.ToString(CultureInfo.InvariantCulture), 3, true, rawValue: true);
                    AppendJsonProperty(writer, "gauges", record.GaugeCount.ToString(CultureInfo.InvariantCulture), 3, true, rawValue: true);
                    AppendJsonProperty(writer, "samples", record.SampleCount.ToString(CultureInfo.InvariantCulture), 3, true, rawValue: true);
                    AppendJsonProperty(writer, "events", record.EventCount.ToString(CultureInfo.InvariantCulture), 3, false, rawValue: true);
                    writer.WriteLine($"    }}{(i < snapshot.Sessions.Count - 1 ? "," : string.Empty)}");
                }
            }
            writer.WriteLine("  ],");

            writer.WriteLine($"  \"metricOperationsDropped\": {snapshot.MetricOperationsDropped},");

            writer.WriteLine("  \"counters\": {");
            var counterIndex = 0;
            foreach (var kvp in snapshot.Counters)
            {
                var comma = ++counterIndex < snapshot.Counters.Count ? "," : string.Empty;
                var record = kvp.Value;
                writer.WriteLine($"    \"{EscapeJson(kvp.Key)}\": {{\"value\": {record.Value}, \"delta\": {record.Delta}, \"samples\": {record.SampleCount}}}{comma}");
            }
            writer.WriteLine("  },");

            writer.WriteLine("  \"gauges\": {");
            var gaugeIndex = 0;
            foreach (var kvp in snapshot.Gauges)
            {
                var comma = ++gaugeIndex < snapshot.Gauges.Count ? "," : string.Empty;
                var record = kvp.Value;
                writer.WriteLine($"    \"{EscapeJson(kvp.Key)}\": {{\"value\": {record.Value}, \"timestamp\": {record.Timestamp}}}{comma}");
            }
            writer.WriteLine("  },");

            writer.WriteLine("  \"samples\": {");
            var sampleIndex = 0;
            foreach (var kvp in snapshot.Samples)
            {
                var summary = SampleSummary.From(kvp.Value);
                var comma = ++sampleIndex < snapshot.Samples.Count ? "," : string.Empty;
                writer.WriteLine($"    \"{EscapeJson(kvp.Key)}\": {{\"count\": {summary.Count}, \"sum\": {Format(summary.Sum)}, \"mean\": {Format(summary.Mean)}, \"min\": {Format(summary.Min)}, \"max\": {Format(summary.Max)}}}{comma}");
            }
            writer.WriteLine("  },");

            writer.WriteLine("  \"rates\": {");
            var rateIndex = 0;
            if (snapshot.Rates != null)
            {
                foreach (var kvp in snapshot.Rates)
                {
                    var comma = ++rateIndex < snapshot.Rates.Count ? "," : string.Empty;
                    var record = kvp.Value;
                    writer.WriteLine($"    \"{EscapeJson(kvp.Key)}\": {{\"total\": {record.TotalCount}, \"count1s\": {record.Count1Second}, \"count5s\": {record.Count5Seconds}, \"count60s\": {record.Count60Seconds}, \"peakPerSecond\": {record.PeakPerSecond}}}{comma}");
                }
            }
            writer.WriteLine("  },");

            writer.WriteLine("  \"durations\": {");
            var durationIndex = 0;
            if (snapshot.Durations != null)
            {
                foreach (var kvp in snapshot.Durations)
                {
                    var comma = ++durationIndex < snapshot.Durations.Count ? "," : string.Empty;
                    var record = kvp.Value;
                    writer.WriteLine($"    \"{EscapeJson(kvp.Key)}\": {{\"count\": {record.Count}, \"sumMs\": {Format(record.SumMilliseconds)}, \"meanMs\": {Format(record.MeanMilliseconds)}, \"minMs\": {Format(record.MinMilliseconds)}, \"maxMs\": {Format(record.MaxMilliseconds)}}}{comma}");
                }
            }
            writer.WriteLine("  },");

            writer.WriteLine("  \"events\": [");
            if (snapshot.Events != null)
            {
                for (var i = 0; i < snapshot.Events.Count; i++)
                {
                    var record = snapshot.Events[i];
                    writer.WriteLine("    {");
                    AppendJsonProperty(writer, "timestamp", record.Timestamp.ToString(CultureInfo.InvariantCulture), 3, true, rawValue: true);
                    AppendJsonProperty(writer, "severity", record.Severity.ToString(), 3, true);
                    AppendJsonProperty(writer, "category", record.Category ?? string.Empty, 3, true);
                    AppendJsonProperty(writer, "name", record.Name ?? string.Empty, 3, true);
                    AppendJsonProperty(writer, "message", record.Message ?? string.Empty, 3, true);
                    AppendJsonProperty(writer, "value", Format(record.Value), 3, true, rawValue: true);
                    AppendJsonProperty(writer, "threshold", Format(record.Threshold), 3, false, rawValue: true);
                    writer.WriteLine($"    }}{(i < snapshot.Events.Count - 1 ? "," : string.Empty)}");
                }
            }
            writer.WriteLine("  ],");

            writer.WriteLine("  \"spanLog\": {");
            if (snapshot.Spans != null && snapshot.Spans.Count > 0)
            {
                long minFrame = long.MaxValue, maxFrame = 0L, wallNanoseconds = 0L;
                var baseTimestamp = long.MaxValue;
                foreach (var span in snapshot.Spans)
                {
                    if (span.StartTimestamp < baseTimestamp) baseTimestamp = span.StartTimestamp;
                    if (span.Frame < minFrame) minFrame = span.Frame;
                    if (span.Frame > maxFrame) maxFrame = span.Frame;
                    wallNanoseconds += span.DurationNanoseconds;
                }

                long lastEnd = baseTimestamp;
                foreach (var span in snapshot.Spans)
                {
                    var end = span.StartTimestamp + (long)(span.DurationNanoseconds * Stopwatch.Frequency / 1000000000.0d);
                    if (end > lastEnd) lastEnd = end;
                }

                var wallMs = (lastEnd - baseTimestamp) * 1000.0d / Stopwatch.Frequency;
                AppendJsonProperty(writer, "count", snapshot.Spans.Count.ToString(CultureInfo.InvariantCulture), 2, true, rawValue: true);
                AppendJsonProperty(writer, "dropped", snapshot.SpansDropped.ToString(CultureInfo.InvariantCulture), 2, true, rawValue: true);
                AppendJsonProperty(writer, "spanNanoseconds", wallNanoseconds.ToString(CultureInfo.InvariantCulture), 2, true, rawValue: true);
                AppendJsonProperty(writer, "wallMs", Format(wallMs), 2, true, rawValue: true);
                AppendJsonProperty(writer, "frameRange", minFrame + ".." + maxFrame, 2, false);
            }
            else
            {
                AppendJsonProperty(writer, "count", "0", 2, false, rawValue: true);
            }

            writer.WriteLine("  },");

            writer.WriteLine("  \"flame\": [");
            var flameRoots = new List<FlameNode>();
            if (snapshot.Root != null)
            {
                foreach (var root in snapshot.Root.Roots.Values)
                {
                    flameRoots.Add(root);
                }
            }

            for (var i = 0; i < flameRoots.Count; i++)
            {
                AppendNodeJson(writer, flameRoots[i], 2, i < flameRoots.Count - 1);
            }

            writer.WriteLine("  ]");
            writer.WriteLine("}");
        }

        private static void AppendNodeJson(TextWriter writer, FlameNode node, int indentLevel, bool comma)
        {
            var indent = new string(' ', indentLevel * 2);
            writer.WriteLine($"{indent}{{");
            AppendJsonProperty(writer, "name", node.Name, indentLevel + 1, true);
            AppendJsonProperty(writer, "category", node.Category ?? string.Empty, indentLevel + 1, true);
            AppendJsonProperty(writer, "totalMs", Format(node.TotalNanoseconds / 1000000.0d), indentLevel + 1, true, rawValue: true);
            AppendJsonProperty(writer, "selfMs", Format(node.SelfNanoseconds / 1000000.0d), indentLevel + 1, true, rawValue: true);
            AppendJsonProperty(writer, "hits", node.HitCount.ToString(CultureInfo.InvariantCulture), indentLevel + 1, true, rawValue: true);
            writer.WriteLine($"{indent}  \"children\": [");

            var children = new List<FlameNode>(node.Children.Values);
            for (var i = 0; i < children.Count; i++)
            {
                AppendNodeJson(writer, children[i], indentLevel + 2, i < children.Count - 1);
            }

            writer.WriteLine($"{indent}  ]");
            writer.WriteLine($"{indent}}}{(comma ? "," : string.Empty)}");
        }

        private static void AppendJsonProperty(TextWriter writer, string name, string value, int indentLevel, bool comma, bool rawValue = false)
        {
            var indent = new string(' ', indentLevel * 2);
            var serialized = rawValue ? value : $"\"{EscapeJson(value)}\"";
            writer.WriteLine($"{indent}\"{name}\": {serialized}{(comma ? "," : string.Empty)}");
        }

        internal static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        internal static string Format(double value) => value.ToString("F4", CultureInfo.InvariantCulture);

        internal static string JoinTags(string[] tags)
        {
            return tags == null || tags.Length == 0 ? string.Empty : string.Join(";", tags);
        }

        internal static string JoinSet(ICollection<string> values)
        {
            return values == null || values.Count == 0 ? string.Empty : string.Join(";", values);
        }

        internal static string JoinRates(Dictionary<string, double> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>(values.Count);
            foreach (var kvp in values)
            {
                parts.Add(kvp.Key + "=" + Format(kvp.Value));
            }

            return string.Join(";", parts);
        }

        internal static double GetDurationMilliseconds(ProfilerSnapshot snapshot)
        {
            if (snapshot.Root == null) return 0d;
            return Math.Max(0L, snapshot.Root.EndTimestamp - snapshot.Root.StartTimestamp);
        }

        internal static TextWriter CreateWriter(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return new StreamWriter(filePath, false, Utf8NoBom, 4096);
        }

        internal static void WriteAllText(string filePath, string content)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content, Utf8NoBom);
        }
    }

    public sealed class CsvExporter : IExporter
    {
        public string Extension => ".csv";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            using (var writer = JsonExporter.CreateWriter(filePath))
            {
                WriteTo(snapshot, writer);
            }
        }

        public string ExportToString(ProfilerSnapshot snapshot)
        {
            var sb = new StringBuilder(2048);
            using (var writer = new StringWriter(sb, CultureInfo.InvariantCulture))
            {
                WriteTo(snapshot, writer);
            }

            return sb.ToString();
        }

        private static void WriteTo(ProfilerSnapshot snapshot, TextWriter writer)
        {
            writer.WriteLine("# Metrics");
            writer.WriteLine("Name,Category,Kind,Unit,Description,Tags");
            if (snapshot.Metrics != null)
            {
                foreach (var kvp in snapshot.Metrics)
                {
                    var record = kvp.Value;
                    writer.WriteLine($"{EscapeCsv(record.Name)},{EscapeCsv(record.Category)},{record.Kind},{EscapeCsv(record.Unit)},{EscapeCsv(record.Description)},{EscapeCsv(JsonExporter.JoinTags(record.Tags))}");
                }
            }

            writer.WriteLine();
            writer.WriteLine("# Options");
            writer.WriteLine("Enabled,CollectionLevel,DefaultSampleRate,MaxSamplesPerMetric,MaxDiagnosticEvents,DisabledCategories,CategorySampleRates,MetricSampleRates");
            if (snapshot.Options != null)
            {
                writer.WriteLine($"{snapshot.Options.Enabled},{snapshot.Options.CollectionLevel},{JsonExporter.Format(snapshot.Options.DefaultSampleRate)},{snapshot.Options.MaxSamplesPerMetric},{snapshot.Options.MaxDiagnosticEvents},{EscapeCsv(JsonExporter.JoinSet(snapshot.Options.DisabledCategories))},{EscapeCsv(JsonExporter.JoinRates(snapshot.Options.CategorySampleRates))},{EscapeCsv(JsonExporter.JoinRates(snapshot.Options.MetricSampleRates))}");
            }

            writer.WriteLine();
            writer.WriteLine("# BufferDiagnostics");
            writer.WriteLine("MetricOperationsDropped,SpansDropped");
            writer.WriteLine($"{snapshot.MetricOperationsDropped},{snapshot.SpansDropped}");

            writer.WriteLine();
            writer.WriteLine("# Sessions");
            writer.WriteLine("SessionId,Label,SavedTimestamp,DurationMs,MetricCount,CounterCount,GaugeCount,SampleCount,EventCount");
            if (snapshot.Sessions != null)
            {
                foreach (var record in snapshot.Sessions)
                {
                    writer.WriteLine($"{EscapeCsv(record.SessionId)},{EscapeCsv(record.Label)},{record.SavedTimestamp},{JsonExporter.Format(record.DurationMilliseconds)},{record.MetricCount},{record.CounterCount},{record.GaugeCount},{record.SampleCount},{record.EventCount}");
                }
            }

            writer.WriteLine();
            writer.WriteLine("# Counters");
            writer.WriteLine("Name,Value,Delta,SampleCount");
            foreach (var kvp in snapshot.Counters)
            {
                var record = kvp.Value;
                writer.WriteLine($"{EscapeCsv(kvp.Key)},{record.Value},{record.Delta},{record.SampleCount}");
            }

            writer.WriteLine();
            writer.WriteLine("# Gauges");
            writer.WriteLine("Name,Value,Timestamp");
            foreach (var kvp in snapshot.Gauges)
            {
                var record = kvp.Value;
                writer.WriteLine($"{EscapeCsv(kvp.Key)},{record.Value},{record.Timestamp}");
            }

            writer.WriteLine();
            writer.WriteLine("# Rates");
            writer.WriteLine("Name,Total,Count1s,Count5s,Count60s,PeakPerSecond");
            if (snapshot.Rates != null)
            {
                foreach (var kvp in snapshot.Rates)
                {
                    var record = kvp.Value;
                    writer.WriteLine($"{EscapeCsv(kvp.Key)},{record.TotalCount},{record.Count1Second},{record.Count5Seconds},{record.Count60Seconds},{record.PeakPerSecond}");
                }
            }

            writer.WriteLine();
            writer.WriteLine("# Durations");
            writer.WriteLine("Name,Count,SumMs,MeanMs,MinMs,MaxMs");
            if (snapshot.Durations != null)
            {
                foreach (var kvp in snapshot.Durations)
                {
                    var record = kvp.Value;
                    writer.WriteLine($"{EscapeCsv(kvp.Key)},{record.Count},{JsonExporter.Format(record.SumMilliseconds)},{JsonExporter.Format(record.MeanMilliseconds)},{JsonExporter.Format(record.MinMilliseconds)},{JsonExporter.Format(record.MaxMilliseconds)}");
                }
            }

            writer.WriteLine();
            writer.WriteLine("# Events");
            writer.WriteLine("Timestamp,Severity,Category,Name,Message,Value,Threshold");
            if (snapshot.Events != null)
            {
                foreach (var record in snapshot.Events)
                {
                    writer.WriteLine($"{record.Timestamp},{record.Severity},{EscapeCsv(record.Category)},{EscapeCsv(record.Name)},{EscapeCsv(record.Message)},{JsonExporter.Format(record.Value)},{JsonExporter.Format(record.Threshold)}");
                }
            }

            writer.WriteLine();
            writer.WriteLine("# Samples");
            writer.WriteLine("Name,Count,Sum,Mean,Min,Max");
            foreach (var kvp in snapshot.Samples)
            {
                var summary = SampleSummary.From(kvp.Value);
                writer.WriteLine($"{EscapeCsv(kvp.Key)},{summary.Count},{JsonExporter.Format(summary.Sum)},{JsonExporter.Format(summary.Mean)},{JsonExporter.Format(summary.Min)},{JsonExporter.Format(summary.Max)}");
            }

        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Contains(",") || value.Contains("\"") || value.Contains("\n")
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }
    }

    public sealed class FoldedExporter : IExporter
    {
        public string Extension => ".folded";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            using (var writer = JsonExporter.CreateWriter(filePath))
            {
                WriteTo(snapshot, writer);
            }
        }

        public string ExportToString(ProfilerSnapshot snapshot)
        {
            var sb = new StringBuilder(2048);
            using (var writer = new StringWriter(sb, CultureInfo.InvariantCulture))
            {
                WriteTo(snapshot, writer);
            }

            return sb.ToString();
        }

        private static void WriteTo(ProfilerSnapshot snapshot, TextWriter writer)
        {
            if (snapshot.Root != null)
            {
                foreach (var root in snapshot.Root.Roots.Values)
                {
                    AppendFolded(writer, root, string.Empty);
                }
            }
        }

        internal static void AppendFolded(TextWriter writer, FlameNode node, string path)
        {
            var currentPath = string.IsNullOrEmpty(path) ? SanitizeFrame(node.Name) : path + ";" + SanitizeFrame(node.Name);
            var weight = Math.Max(1L, node.SelfNanoseconds > 0 ? node.SelfNanoseconds : node.TotalNanoseconds);
            if (node.HitCount > 0)
            {
                writer.WriteLine($"{currentPath} {weight}");
            }

            foreach (var child in node.Children.Values)
            {
                AppendFolded(writer, child, currentPath);
            }
        }

        internal static string SanitizeFrame(string value)
        {
            return string.IsNullOrEmpty(value) ? "unnamed" : value.Replace(';', '/').Replace('\n', ' ').Replace('\r', ' ');
        }
    }

    internal readonly struct SampleSummary
    {
        public readonly int Count;
        public readonly double Sum;
        public readonly double Mean;
        public readonly double Min;
        public readonly double Max;

        private SampleSummary(int count, double sum, double mean, double min, double max)
        {
            Count = count;
            Sum = sum;
            Mean = mean;
            Min = min;
            Max = max;
        }

        public static SampleSummary From(List<double> values)
        {
            if (values == null || values.Count == 0)
            {
                return new SampleSummary(0, 0d, 0d, 0d, 0d);
            }

            double sum = 0d;
            double min = double.MaxValue;
            double max = double.MinValue;
            foreach (var value in values)
            {
                sum += value;
                if (value < min) min = value;
                if (value > max) max = value;
            }

            return new SampleSummary(values.Count, sum, sum / values.Count, min, max);
        }
    }

    public static class ExporterFactory
    {
        private static readonly Dictionary<string, IExporter> Exporters = new Dictionary<string, IExporter>(StringComparer.OrdinalIgnoreCase)
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
            return !string.IsNullOrEmpty(format) && Exporters.TryGetValue(format, out var exporter) ? exporter : Exporters["json"];
        }

        public static string[] GetSupportedFormats() => new[] { "json", "csv", "folded", "speedscope", "chrome", "flamegraph", "0x" };

        public static string[] GetRecommendedFormats() => new[] { "speedscope", "chrome", "flamegraph" };
    }
}
