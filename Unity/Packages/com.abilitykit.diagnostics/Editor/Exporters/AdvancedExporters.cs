using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using AbilityKit.Diagnostics;

namespace AbilityKit.Diagnostics.Exporters
{
    internal static class TextWriterExtensions
    {
        public static TextWriter Append(this TextWriter writer, string value)
        {
            writer.Write(value);
            return writer;
        }

        public static TextWriter Append(this TextWriter writer, char value)
        {
            writer.Write(value);
            return writer;
        }

        public static TextWriter Append(this TextWriter writer, int value)
        {
            writer.Write(value.ToString(CultureInfo.InvariantCulture));
            return writer;
        }

        public static TextWriter Append(this TextWriter writer, long value)
        {
            writer.Write(value.ToString(CultureInfo.InvariantCulture));
            return writer;
        }

        public static TextWriter Append(this TextWriter writer, double value)
        {
            writer.Write(value.ToString(CultureInfo.InvariantCulture));
            return writer;
        }

        public static TextWriter AppendLine(this TextWriter writer, string value)
        {
            writer.WriteLine(value);
            return writer;
        }

        public static TextWriter AppendLine(this TextWriter writer)
        {
            writer.WriteLine();
            return writer;
        }
    }

    /// <summary>
    /// Speedscope import format exporter。快照含 span 时间线时输出真实时间序（可看逐帧尖峰与调用次序），
    /// 否则回退到聚合节点的事件对（层级占比视图）。
    /// </summary>
    public sealed class SpeedScopeExporter : IExporter
    {
        public string Extension => ".speedscope.json";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            using (var writer = JsonExporter.CreateWriter(filePath))
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
            var frames = new List<string>();
            var frameIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            var events = new List<(int Frame, long At, bool Open)>();
            var currentValue = 0L;
            var timeline = false;

            if (snapshot.Spans != null && snapshot.Spans.Count > 0)
            {
                timeline = true;
                var baseTimestamp = long.MaxValue;
                foreach (var span in snapshot.Spans)
                {
                    if (span.StartTimestamp < baseTimestamp) baseTimestamp = span.StartTimestamp;
                }

                events.Capacity = Math.Max(events.Capacity, snapshot.Spans.Count * 2);
                foreach (var span in snapshot.Spans)
                {
                    var index = GetFrameIndex(span.Name, frames, frameIndex);
                    var startValue = (long)((span.StartTimestamp - baseTimestamp) * 1000000000.0d / Stopwatch.Frequency);
                    var endValue = startValue + Math.Max(1L, span.DurationNanoseconds);
                    events.Add((index, Math.Max(0L, startValue), true));
                    events.Add((index, endValue, false));
                    if (endValue > currentValue) currentValue = endValue;
                }

                events.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : (a.Open == b.Open ? 0 : (a.Open ? 1 : -1)));
            }
            else if (snapshot.Root != null)
            {
                foreach (var root in snapshot.Root.Roots.Values)
                {
                    currentValue = AppendSpeedscopeEvents(root, currentValue, frames, frameIndex, events);
                }
            }

            writer.WriteLine("{");
            writer.WriteLine("  \"$schema\": \"https://www.speedscope.app/file-format-schema.json\",");
            writer.WriteLine("  \"shared\": {");
            writer.WriteLine("    \"frames\": [");
            for (var i = 0; i < frames.Count; i++)
            {
                writer.WriteLine($"      {{\"name\": \"{JsonExporter.EscapeJson(frames[i])}\"}}{(i < frames.Count - 1 ? "," : string.Empty)}");
            }
            writer.WriteLine("    ]");
            writer.WriteLine("  },");
            writer.WriteLine("  \"profiles\": [");
            writer.WriteLine("    {");
            writer.WriteLine("      \"type\": \"evented\",");
            writer.WriteLine($"      \"name\": \"AbilityKit Diagnostics{(timeline ? " (timeline)" : string.Empty)}\",");
            writer.WriteLine("      \"unit\": \"nanoseconds\",");
            writer.WriteLine("      \"startValue\": 0,");
            writer.WriteLine($"      \"endValue\": {Math.Max(1L, currentValue)},");
            writer.WriteLine("      \"events\": [");
            for (var i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                writer.WriteLine($"        {{\"type\": \"{(evt.Open ? "O" : "C")}\", \"frame\": {evt.Frame}, \"at\": {evt.At}}}{(i < events.Count - 1 ? "," : string.Empty)}");
            }
            writer.WriteLine("      ]");
            writer.WriteLine("    }");
            writer.WriteLine("  ],");
            writer.WriteLine($"  \"name\": \"AbilityKit Diagnostics {JsonExporter.EscapeJson(snapshot.SessionId)}\",");
            writer.WriteLine("  \"exporter\": \"AbilityKit.Diagnostics\"");
            writer.WriteLine("}");
        }

        private static long AppendSpeedscopeEvents(FlameNode node, long startValue, List<string> frames, Dictionary<string, int> frameIndex, List<(int Frame, long At, bool Open)> events)
        {
            var index = GetFrameIndex(node.Name, frames, frameIndex);
            var endValue = startValue + Math.Max(1L, node.TotalNanoseconds);
            events.Add((index, startValue, true));

            var childStart = startValue;
            foreach (var child in node.Children.Values)
            {
                childStart = AppendSpeedscopeEvents(child, childStart, frames, frameIndex, events);
            }

            events.Add((index, endValue, false));
            return endValue;
        }

        private static int GetFrameIndex(string name, List<string> frames, Dictionary<string, int> frameIndex)
        {
            name = string.IsNullOrEmpty(name) ? "unnamed" : name;
            if (frameIndex.TryGetValue(name, out var index)) return index;
            index = frames.Count;
            frames.Add(name);
            frameIndex[name] = index;
            return index;
        }
    }

    /// <summary>
    /// Chrome trace event exporter。快照含 span 时间线时输出真实时间轴（ts=相对首条 span 的微秒），
    /// 否则回退到聚合节点拼接的粗粒度时间轴。
    /// </summary>
    public sealed class ChromePerfExporter : IExporter
    {
        private readonly struct TraceEvent
        {
            public readonly string Name;
            public readonly string Category;
            public readonly double Timestamp;
            public readonly double Duration;
            public readonly int ThreadId;
            public readonly long Hits;
            public readonly double SelfMilliseconds;
            public readonly long Frame;
            public readonly long ContextId;
            public readonly bool IsSpan;

            public TraceEvent(string name, string category, double timestamp, double duration, int threadId, long hits, double selfMilliseconds, long frame, long contextId, bool isSpan)
            {
                Name = name; Category = category; Timestamp = timestamp; Duration = duration; ThreadId = threadId;
                Hits = hits; SelfMilliseconds = selfMilliseconds; Frame = frame; ContextId = contextId; IsSpan = isSpan;
            }
        }
        public string Extension => ".chrome-perf.json";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            using (var writer = JsonExporter.CreateWriter(filePath))
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
            var events = new List<TraceEvent>();
            var usesTimeline = snapshot.Spans != null && snapshot.Spans.Count > 0;
            if (usesTimeline)
            {
                AppendTimelineEvents(snapshot.Spans, events);
            }
            else if (snapshot.Root != null)
            {
                var startMicroseconds = 0d;
                foreach (var root in snapshot.Root.Roots.Values)
                {
                    AppendTraceEvents(root, startMicroseconds, 0, events);
                    startMicroseconds += Math.Max(1d, root.TotalNanoseconds / 1000.0d);
                }
            }

            writer.WriteLine("{");
            writer.WriteLine("  \"traceEvents\": [");
            for (var i = 0; i < events.Count; i++)
            {
                WriteEvent(writer, events[i]);
                writer.WriteLine(i < events.Count - 1 ? "," : string.Empty);
            }
            writer.WriteLine("  ],");
            writer.WriteLine("  \"metadata\": {");
            writer.WriteLine($"    \"sessionId\": \"{JsonExporter.EscapeJson(snapshot.SessionId)}\",");
            writer.WriteLine($"    \"timeline\": \"{(usesTimeline ? "spans" : "aggregated")}\",");
            if (usesTimeline)
            {
                writer.WriteLine($"    \"spansDropped\": {snapshot.SpansDropped},");
            }
            writer.WriteLine($"    \"metricOperationsDropped\": {snapshot.MetricOperationsDropped},");
            writer.WriteLine($"    \"exportedAt\": \"{DateTimeOffset.UtcNow:O}\"");
            writer.WriteLine("  }");
            writer.WriteLine("}");
        }

        private static void WriteEvent(TextWriter writer, TraceEvent evt)
        {
            writer.Write("    {\"name\":\""); writer.Write(JsonExporter.EscapeJson(evt.Name));
            writer.Write("\",\"cat\":\""); writer.Write(JsonExporter.EscapeJson(evt.Category));
            writer.Write("\",\"ph\":\"X\",\"ts\":"); writer.Write(evt.Timestamp.ToString("F3", CultureInfo.InvariantCulture));
            writer.Write(",\"dur\":"); writer.Write(evt.Duration.ToString("F3", CultureInfo.InvariantCulture));
            writer.Write(",\"pid\":1,\"tid\":"); writer.Write(evt.ThreadId.ToString(CultureInfo.InvariantCulture));
            if (evt.IsSpan)
            {
                writer.Write(",\"args\":{\"frame\":"); writer.Write(evt.Frame.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"cid\":"); writer.Write(evt.ContextId.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                writer.Write(",\"args\":{\"hits\":"); writer.Write(evt.Hits.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"selfMs\":"); writer.Write(JsonExporter.Format(evt.SelfMilliseconds));
            }
            writer.Write("}}");
        }

        private static void AppendTimelineEvents(List<ProfilerSpanRecord> spans, List<TraceEvent> events)
        {
            var baseTimestamp = long.MaxValue;
            foreach (var span in spans)
            {
                if (span.StartTimestamp < baseTimestamp) baseTimestamp = span.StartTimestamp;
            }

            var ordered = new List<ProfilerSpanRecord>(spans);
            ordered.Sort((a, b) => a.StartTimestamp != b.StartTimestamp
                ? a.StartTimestamp.CompareTo(b.StartTimestamp)
                : a.ThreadId.CompareTo(b.ThreadId));
            foreach (var span in ordered)
            {
                var startMicroseconds = (span.StartTimestamp - baseTimestamp) * 1000000.0d / Stopwatch.Frequency;
                var durationMicroseconds = Math.Max(0.1d, span.DurationNanoseconds / 1000.0d);
                events.Add(new TraceEvent(span.Name, "span", startMicroseconds, durationMicroseconds, span.ThreadId, 0, 0d, span.Frame, span.ContextId, true));
            }
        }

        private static void AppendTraceEvents(FlameNode node, double startMicroseconds, int depth, List<TraceEvent> events)
        {
            var category = string.IsNullOrEmpty(node.Category) ? "default" : node.Category;
            var durationMicroseconds = Math.Max(1d, node.TotalNanoseconds / 1000.0d);
            events.Add(new TraceEvent(node.Name, category, startMicroseconds, durationMicroseconds, depth + 1, node.HitCount, node.SelfNanoseconds / 1000000.0d, 0, 0, false));

            var childStart = startMicroseconds;
            foreach (var child in node.Children.Values)
            {
                AppendTraceEvents(child, childStart, depth + 1, events);
                childStart += Math.Max(1d, child.TotalNanoseconds / 1000.0d);
            }
        }
    }

    /// <summary>
    /// 静态 SVG 火焰图导出器：块宽 = 累计耗时，向上分叉 = 调用链，
    /// 悬停可见 total/self 毫秒数。配色为 8 槽类目色（按指标族首见顺序分配），
    /// 同一亮/暗两套经 OKLab CVD 校验的色阶。
    /// </summary>
    public sealed class FlameGraphExporter : IExporter
    {
        public string Extension => ".flame.svg";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            using (var writer = JsonExporter.CreateWriter(filePath))
            {
                FlameGraphSvgBuilder.Write(snapshot, writer);
            }
        }

        public string ExportToString(ProfilerSnapshot snapshot)
        {
            return FlameGraphSvgBuilder.Build(snapshot);
        }
    }

    /// <summary>
    /// SVG 火焰图布局与绘制。自底向上（根在最下方），子块按累计耗时降序排在父块跨度内。
    /// </summary>
    internal static class FlameGraphSvgBuilder
    {
        private const int Width = 1200;
        private const int RowHeight = 24;
        private const int RowGap = 1;
        private const int PlotPadding = 12;
        private const int LegendHeight = 34;
        private const int HeaderHeight = 58;
        private const double CharWidth = 6.0;
        private const int TopSelfCount = 8;

        // 8 槽类目色（亮/暗），首见顺序分配；第 9 个及以后的指标族折叠为中性灰。
        private static readonly string[] LightSeries =
        {
            "#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6", "#e34948"
        };

        private static readonly string[] DarkSeries =
        {
            "#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9", "#e66767"
        };

        public static string Build(ProfilerSnapshot snapshot)
        {
            var sb = new StringBuilder(8192);
            using (var writer = new StringWriter(sb, CultureInfo.InvariantCulture))
            {
                Write(snapshot, writer);
            }

            return sb.ToString();
        }

        public static void Write(ProfilerSnapshot snapshot, TextWriter writer)
        {
            var roots = new List<FlameNode>();
            if (snapshot.Root != null)
            {
                foreach (var root in snapshot.Root.Roots.Values)
                {
                    roots.Add(root);
                }
            }

            // 类目根节点自身从未被测量（TotalNanoseconds == 0），
            // 宽度预算必须用“有效总耗时 = max(自身累计, 子树有效和)”。
            var effectiveTotals = new Dictionary<FlameNode, long>(64);
            long grandTotal = 0;
            foreach (var root in roots)
            {
                grandTotal += ComputeEffectiveTotal(root, effectiveTotals);
            }

            var categories = new Dictionary<string, int>(StringComparer.Ordinal);
            var frames = new List<FrameLayout>(64);
            var maxDepth = 0;

            if (roots.Count > 0 && grandTotal > 0)
            {
                var plotWidth = (double)(Width - PlotPadding * 2);
                var x = (double)PlotPadding;
                foreach (var root in roots)
                {
                    var rootWidth = plotWidth * effectiveTotals[root] / grandTotal;
                    Place(root, x, rootWidth, 0, frames, effectiveTotals, ref maxDepth);
                    x += rootWidth;
                }
            }

            var height = HeaderHeight + LegendHeight + (maxDepth + 1) * (RowHeight + RowGap) + 24 + TopSelfCount * 18 + 14;
            writer.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(Width)
                .Append("\" height=\"").Append(height)
                .Append("\" viewBox=\"0 0 ").Append(Width).Append(' ').Append(height)
                .Append("\" font-family=\"Consolas, 'Courier New', monospace\">\n");

            AppendStylesAndHeader(writer, snapshot, grandTotal, categories);
            AppendLegend(writer, categories);

            // 自底向上：depth 0（类目根）在火焰区最下方。
            var flameBaseY = HeaderHeight + LegendHeight + maxDepth * (RowHeight + RowGap);
            foreach (var frame in frames)
            {
                AppendFrame(writer, frame, flameBaseY - frame.Depth * (RowHeight + RowGap), categories);
            }

            AppendTopSelfTime(writer, frames, flameBaseY + RowHeight + 18);
            writer.Append("</svg>\n");
        }

        private readonly struct FrameLayout
        {
            public readonly FlameNode Node;
            public readonly int Depth;
            public readonly double X;
            public readonly double Width;

            public FrameLayout(FlameNode node, int depth, double x, double width)
            {
                Node = node;
                Depth = depth;
                X = x;
                Width = width;
            }
        }

        private static long ComputeEffectiveTotal(FlameNode node, Dictionary<FlameNode, long> memo)
        {
            if (memo.TryGetValue(node, out var cached)) return cached;

            long childSum = 0;
            foreach (var child in node.Children.Values)
            {
                childSum += ComputeEffectiveTotal(child, memo);
            }

            var effective = Math.Max(node.TotalNanoseconds, childSum);
            memo[node] = effective;
            return effective;
        }

        private static void Place(
            FlameNode node,
            double x,
            double width,
            int depth,
            List<FrameLayout> frames,
            Dictionary<FlameNode, long> effectiveTotals,
            ref int maxDepth)
        {
            if (depth > maxDepth) maxDepth = depth;
            frames.Add(new FrameLayout(node, depth, x, width));

            if (node.Children.Count == 0 || width < 2d) return;

            var children = new List<FlameNode>(node.Children.Values);
            children.Sort((a, b) => effectiveTotals[b].CompareTo(effectiveTotals[a]));

            long childrenBudgetNanoseconds = 0;
            foreach (var child in children)
            {
                childrenBudgetNanoseconds += effectiveTotals[child];
            }

            var nodeTotal = Math.Max(1L, effectiveTotals[node]);
            // 子块按有效耗时占父总耗时的比例分配宽度，未覆盖的右侧留白即父块自身耗时。
            var budgetShare = Math.Min(1d, childrenBudgetNanoseconds / (double)nodeTotal);
            var childBudget = width * budgetShare;
            if (childBudget < 2d || childrenBudgetNanoseconds <= 0) return;

            var childX = x;
            foreach (var child in children)
            {
                var childWidth = childBudget * effectiveTotals[child] / childrenBudgetNanoseconds;
                Place(child, childX, childWidth, depth + 1, frames, effectiveTotals, ref maxDepth);
                childX += childWidth;
            }
        }

        private static void AppendStylesAndHeader(
            TextWriter sb,
            ProfilerSnapshot snapshot,
            long grandTotal,
            Dictionary<string, int> categories)
        {
            var title = string.IsNullOrEmpty(snapshot.SessionId)
                ? "AbilityKit profiler flame graph"
                : "AbilityKit profiler flame graph — " + snapshot.SessionId;

            sb.Append("<style>\n text { dominant-baseline: middle; }\n rect.f { stroke-width:1; }\n");
            sb.Append(" .ink { fill: #33322e; } .ink2 { fill: #6b6a63; } .surface { fill: #fcfcfb; } .c0 { fill: #d8d7d0; }\n");
            for (var i = 0; i < LightSeries.Length; i++)
            {
                sb.Append(" .c").Append(i + 1).Append(" { fill: ").Append(LightSeries[i]).Append("; }\n");
            }

            sb.Append(" @media (prefers-color-scheme: dark) { .ink { fill: #f0efec; } .ink2 { fill: #c3c2b7; } .surface { fill: #1a1a19; } .c0 { fill: #3a3a38; }\n");
            for (var i = 0; i < DarkSeries.Length; i++)
            {
                sb.Append("  .c").Append(i + 1).Append(" { fill: ").Append(DarkSeries[i]).Append("; }\n");
            }

            sb.Append("  rect.f { stroke: #1a1a19; } }\n</style>\n");
            sb.Append("<rect class=\"surface\" x=\"0\" y=\"0\" width=\"").Append(Width).Append("\" height=\"100%\"/>\n");
            sb.Append("<text class=\"ink\" x=\"").Append(PlotPadding).Append("\" y=\"22\" font-size=\"15\" font-weight=\"600\">")
                .Append(EscapeText(title)).Append("</text>\n");
            sb.Append("<text class=\"ink2\" x=\"").Append(PlotPadding).Append("\" y=\"40\" font-size=\"11\">total sampled ")
                .Append(FormatMilliseconds(grandTotal))
                .Append(" ms · ").Append(categories.Count).Append(" metric families · width = total time · hover for totals</text>\n");
        }

        private static void AppendLegend(TextWriter sb, Dictionary<string, int> categories)
        {
            if (categories.Count == 0) return;

            var x = (double)PlotPadding;
            var y = HeaderHeight + LegendHeight - 22;
            foreach (var pair in categories)
            {
                sb.Append("<rect class=\"c").Append(pair.Value + 1).Append("\" x=\"")
                    .Append(x.ToString("F1", CultureInfo.InvariantCulture)).Append("\" y=\"").Append(y)
                    .Append("\" width=\"12\" height=\"12\" rx=\"2\"/>\n");
                sb.Append("<text class=\"ink\" x=\"").Append((x + 17).ToString("F1", CultureInfo.InvariantCulture))
                    .Append("\" y=\"").Append(y + 6).Append("\" font-size=\"11\">")
                    .Append(EscapeText(pair.Key)).Append("</text>\n");
                x += 17 + 14 + pair.Key.Length * CharWidth;
            }
        }

        private static void AppendFrame(TextWriter sb, FrameLayout frame, double y, Dictionary<string, int> categories)
        {
            var node = frame.Node;
            var slot = GetSlot(node, categories);
            var width = Math.Max(frame.Width - 1d, 0.6d);
            var x = frame.X.ToString("F1", CultureInfo.InvariantCulture);

            sb.Append("<rect class=\"f c").Append(slot).Append("\" x=\"").Append(x)
                .Append("\" y=\"").Append(y.ToString("F1", CultureInfo.InvariantCulture))
                .Append("\" width=\"").Append(width.ToString("F1", CultureInfo.InvariantCulture))
                .Append("\" height=\"").Append(RowHeight).Append("\" rx=\"2\"><title>")
                .Append(EscapeText(node.Name)).Append("  total=").Append(FormatMilliseconds(node.TotalNanoseconds))
                .Append("ms self=").Append(FormatMilliseconds(node.SelfNanoseconds))
                .Append("ms hits=").Append(node.HitCount).Append("</title></rect>\n");

            if (frame.Width < 46d) return;

            var label = frame.Depth == 0 ? node.Name : StripFirstSegment(node.Name);
            var text = frame.Width > 120d
                ? label + "  " + FormatMilliseconds(node.TotalNanoseconds) + " ms"
                : label;
            var maxChars = (int)((frame.Width - 14) / CharWidth);
            if (maxChars < 4 || text.Length > maxChars)
            {
                if (maxChars < 5) return;
                text = text.Substring(0, maxChars - 1) + "…";
            }

            sb.Append("<text x=\"").Append((frame.X + 6).ToString("F1", CultureInfo.InvariantCulture))
                .Append("\" y=\"").Append((y + RowHeight / 2d + 1).ToString("F1", CultureInfo.InvariantCulture))
                .Append("\" font-size=\"11\" fill=\"#ffffff\" style=\"paint-order:stroke\" stroke=\"rgba(0,0,0,.35)\" stroke-width=\"2\">")
                .Append(EscapeText(text)).Append("</text>\n");
        }

        private static void AppendTopSelfTime(TextWriter sb, List<FrameLayout> frames, double startY)
        {
            var ranked = new List<FlameNode>(frames.Count);
            foreach (var frame in frames)
            {
                if (frame.Node.SelfNanoseconds > 0)
                {
                    ranked.Add(frame.Node);
                }
            }

            if (ranked.Count == 0) return;

            ranked.Sort((a, b) => b.SelfNanoseconds.CompareTo(a.SelfNanoseconds));
            var count = Math.Min(TopSelfCount, ranked.Count);
            sb.Append("<text class=\"ink\" x=\"").Append(PlotPadding).Append("\" y=\"")
                .Append(startY.ToString("F1", CultureInfo.InvariantCulture))
                .Append("\" font-size=\"12\" font-weight=\"600\">Top self time</text>\n");
            for (var i = 0; i < count; i++)
            {
                var node = ranked[i];
                var y = startY + 18 * (i + 1);
                sb.Append("<text class=\"ink2\" x=\"").Append(PlotPadding).Append("\" y=\"")
                    .Append(y.ToString("F1", CultureInfo.InvariantCulture)).Append("\" font-size=\"11\"> ")
                    .Append(i + 1).Append(". ").Append(EscapeText(node.Name)).Append(" — self ")
                    .Append(FormatMilliseconds(node.SelfNanoseconds)).Append(" ms / total ")
                    .Append(FormatMilliseconds(node.TotalNanoseconds)).Append(" ms</text>\n");
            }
        }

        private static int GetSlot(FlameNode node, Dictionary<string, int> categories)
        {
            var family = MetricFamily(node.Name);
            if (family == null)
            {
                return 0; // 类目根（中性灰）。
            }

            if (!categories.TryGetValue(family, out var slot))
            {
                // 超出 8 槽的指标族折叠为中性灰（slot = -1）。
                slot = categories.Count < LightSeries.Length ? categories.Count : -1;
                categories[family] = slot;
            }

            return slot + 1;
        }

        private static string MetricFamily(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            // 指标族 = 第二段（如 moba.trigger.evaluate -> trigger；只有一段的名字归为根）。
            var firstDot = name.IndexOf('.');
            if (firstDot < 0 || firstDot == name.Length - 1) return null;

            var rest = name.Substring(firstDot + 1);
            var secondDot = rest.IndexOf('.');
            return secondDot < 0 ? rest : rest.Substring(0, secondDot);
        }

        private static string StripFirstSegment(string name)
        {
            var firstDot = name.IndexOf('.');
            return firstDot < 0 || firstDot == name.Length - 1 ? name : name.Substring(firstDot + 1);
        }

        private static string FormatMilliseconds(long nanoseconds)
        {
            return (nanoseconds / 1000000d).ToString("F1", CultureInfo.InvariantCulture);
        }

        private static string EscapeText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }

    /// <summary>
    /// Simple tree JSON exporter for flamegraph visualizers.
    /// </summary>
    public sealed class ZeroxExporter : IExporter
    {
        public string Extension => ".0x.json";

        public void Export(ProfilerSnapshot snapshot, string filePath)
        {
            using (var writer = JsonExporter.CreateWriter(filePath))
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
            var roots = snapshot.Root == null
                ? new List<FlameNode>()
                : new List<FlameNode>(snapshot.Root.Roots.Values);
            writer.AppendLine("{");
            writer.AppendLine("  \"name\": \"AbilityKit Diagnostics\",");
            writer.AppendLine("  \"children\": [");
            for (var i = 0; i < roots.Count; i++)
            {
                AppendTreeNode(writer, roots[i], 2, i < roots.Count - 1);
            }
            writer.AppendLine("  ]");
            writer.AppendLine("}");
        }

        private static void AppendTreeNode(TextWriter sb, FlameNode node, int indentLevel, bool comma)
        {
            var indent = new string(' ', indentLevel * 2);
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}  \"name\": \"{JsonExporter.EscapeJson(node.Name)}\",");
            sb.AppendLine($"{indent}  \"value\": {Math.Max(1L, node.SelfNanoseconds > 0 ? node.SelfNanoseconds : node.TotalNanoseconds)},");
            sb.AppendLine($"{indent}  \"children\": [");

            var children = new List<FlameNode>(node.Children.Values);
            for (var i = 0; i < children.Count; i++)
            {
                AppendTreeNode(sb, children[i], indentLevel + 2, i < children.Count - 1);
            }

            sb.AppendLine($"{indent}  ]");
            sb.AppendLine($"{indent}}}{(comma ? "," : string.Empty)}");
        }
    }
}
