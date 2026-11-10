using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using AbilityKit.Diagnostics;
using AbilityKit.Diagnostics.Exporters;

namespace AbilityKit.Diagnostics.Editor.Windows
{
    /// <summary>
    /// AbilityKit diagnostics editor window.
    /// </summary>
    public sealed class DiagnosticsWindow : EditorWindow
    {
        private EditorProfiler _profiler;
        private Vector2 _scrollPosition;
        private string _selectedTab = "Overview";
        private readonly string[] _tabs = { "Overview", "Metrics", "Options", "Sessions", "Counters", "Rates", "Gauges", "Durations", "Samples", "Events", "Flame" };
        private bool _isRecording;
        private double _lastUpdateTime;
        private string _exportPath;
        private ProfilerSnapshot? _cachedSnapshot;
        private double _cachedSnapshotTime;
        private bool _snapshotDirty = true;
        private const double SnapshotRefreshIntervalSeconds = 0.5d;

        [MenuItem("Window/AbilityKit/Diagnostics")]
        public static void ShowWindow()
        {
            var window = GetWindow<DiagnosticsWindow>("Diagnostics");
            window.minSize = new Vector2(720, 460);
        }

        private void OnEnable()
        {
            _profiler = ProfilerHub.GetEditorProfiler() ?? new EditorProfiler();
            ProfilerHub.SetProfiler(_profiler);
            _isRecording = _profiler.IsEnabled;
            _exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "AbilityKit.Diagnostics");
            InvalidateSnapshot();
        }

        private void OnDisable()
        {
            if (_isRecording)
            {
                StopRecording();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            var snapshot = GetDisplaySnapshot();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_selectedTab)
            {
                case "Overview":
                    DrawOverview(snapshot);
                    break;
                case "Metrics":
                    DrawMetrics(snapshot);
                    break;
                case "Options":
                    DrawOptions(snapshot);
                    break;
                case "Sessions":
                    DrawSessions(snapshot);
                    break;
                case "Counters":
                    DrawCounters(snapshot);
                    break;
                case "Rates":
                    DrawRates(snapshot);
                    break;
                case "Gauges":
                    DrawGauges(snapshot);
                    break;
                case "Durations":
                    DrawDurations(snapshot);
                    break;
                case "Samples":
                    DrawSamples(snapshot);
                    break;
                case "Events":
                    DrawEvents(snapshot);
                    break;
                case "Flame":
                    DrawFlame(snapshot);
                    break;
            }

            EditorGUILayout.EndScrollView();

            if (_isRecording && EditorApplication.timeSinceStartup - _lastUpdateTime > 0.5d)
            {
                _lastUpdateTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            foreach (var tab in _tabs)
            {
                var isSelected = _selectedTab == tab;
                if (GUILayout.Toggle(isSelected, tab, EditorStyles.toolbarButton, GUILayout.Width(72)))
                {
                    _selectedTab = tab;
                }
            }

            GUILayout.FlexibleSpace();

            GUI.backgroundColor = _isRecording ? Color.red : Color.green;
            if (GUILayout.Button(_isRecording ? "Stop" : "Record", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                if (_isRecording)
                {
                    StopRecording();
                }
                else
                {
                    StartRecording();
                }
            }

            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(58)))
            {
                _profiler?.Clear();
                _isRecording = false;
                InvalidateSnapshot();
            }

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(58)))
            {
                _profiler?.SaveSession("manual");
                InvalidateSnapshot();
            }

            if (GUILayout.Button("Export...", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                ShowExportMenu();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void DrawOverview(ProfilerSnapshot? snapshot)
        {
            if (!TryGetSnapshot(snapshot, out var value)) return;

            EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"ID: {value.SessionId}");
            EditorGUILayout.LabelField($"Timestamp: {DateTimeOffset.FromUnixTimeMilliseconds(value.Timestamp):yyyy-MM-dd HH:mm:ss}");
            EditorGUILayout.LabelField($"Duration: {GetDurationSeconds(value):F2}s");
            EditorGUILayout.LabelField($"Recording: {(_isRecording ? "Yes" : "No")}");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"Metrics: {GetCount(value.Metrics)}");
            EditorGUILayout.LabelField($"Sessions: {GetCount(value.Sessions)}");
            EditorGUILayout.LabelField($"Counters: {value.Counters.Count}");
            EditorGUILayout.LabelField($"Gauges: {value.Gauges.Count}");
            EditorGUILayout.LabelField($"Samples: {value.Samples.Count}");
            EditorGUILayout.LabelField($"Rates: {GetCount(value.Rates)}");
            EditorGUILayout.LabelField($"Durations: {GetCount(value.Durations)}");
            EditorGUILayout.LabelField($"Events: {GetCount(value.Events)}");
            EditorGUILayout.LabelField($"Flame Nodes: {CountAllNodes(value.Root)}");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            DrawTopRates(value, 5);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Top 5 Slowest Samples", EditorStyles.boldLabel);
            var topSamples = GetTopSamples(value.Samples, 5);
            if (topSamples.Count == 0)
            {
                EditorGUILayout.HelpBox("No samples recorded", MessageType.Info);
                return;
            }

            foreach (var sample in topSamples)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(sample.name, GUILayout.Width(300));
                EditorGUILayout.LabelField($"avg {sample.mean:F4}ms", GUILayout.Width(120));
                EditorGUILayout.LabelField($"max {sample.max:F4}ms");
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawMetrics(ProfilerSnapshot? snapshot)
        {
            if (!TryGetSnapshot(snapshot, out var value)) return;
            if (value.Metrics == null || value.Metrics.Count == 0)
            {
                EditorGUILayout.HelpBox("No metrics registered", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Metric Registry", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var sorted = new List<KeyValuePair<string, MetricDefinition>>(value.Metrics);
            sorted.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
            foreach (var kvp in sorted)
            {
                var metric = kvp.Value;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(metric.Name, EditorStyles.boldLabel, GUILayout.Width(300));
                EditorGUILayout.LabelField(metric.Kind.ToString(), GUILayout.Width(80));
                EditorGUILayout.LabelField(metric.Category ?? string.Empty, GUILayout.Width(120));
                EditorGUILayout.LabelField(metric.Unit ?? string.Empty, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(metric.Description))
                {
                    EditorGUILayout.LabelField(metric.Description, EditorStyles.wordWrappedMiniLabel);
                }

                if (metric.Tags != null && metric.Tags.Length > 0)
                {
                    EditorGUILayout.LabelField("tags " + string.Join(",", metric.Tags), EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawOptions(ProfilerSnapshot? snapshot)
        {
            if (!TryGetSnapshot(snapshot, out var value)) return;
            var options = value.Options;
            if (options == null)
            {
                EditorGUILayout.HelpBox("No profiler options available", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Profiler Options", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"Enabled: {options.Enabled}");
            EditorGUILayout.LabelField($"Default Sample Rate: {options.DefaultSampleRate:F3}");
            EditorGUILayout.LabelField($"Max Samples / Metric: {options.MaxSamplesPerMetric}");
            EditorGUILayout.LabelField($"Max Diagnostic Events: {options.MaxDiagnosticEvents}");
            EditorGUILayout.LabelField($"Disabled Categories: {GetCount(options.DisabledCategories)}");
            EditorGUILayout.LabelField($"Category Sample Rates: {GetCount(options.CategorySampleRates)}");
            EditorGUILayout.LabelField($"Metric Sample Rates: {GetCount(options.MetricSampleRates)}");
            EditorGUI.indentLevel--;
        }

        private void DrawSessions(ProfilerSnapshot? snapshot)
        {
            if (!TryGetSnapshot(snapshot, out var value)) return;
            if (value.Sessions == null || value.Sessions.Count == 0)
            {
                EditorGUILayout.HelpBox("No saved sessions", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Saved Session Summaries", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            for (var i = value.Sessions.Count - 1; i >= 0; i--)
            {
                var session = value.Sessions[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(string.IsNullOrEmpty(session.Label) ? session.SessionId : session.Label, EditorStyles.boldLabel, GUILayout.Width(240));
                EditorGUILayout.LabelField($"{session.DurationMilliseconds:F1}ms", GUILayout.Width(100));
                EditorGUILayout.LabelField(DateTimeOffset.FromUnixTimeMilliseconds(session.SavedTimestamp).ToLocalTime().ToString("HH:mm:ss"));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"metrics {session.MetricCount}, counters {session.CounterCount}, gauges {session.GaugeCount}, samples {session.SampleCount}, events {session.EventCount}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawCounters(ProfilerSnapshot? snapshot)
        {
            if (!TryGetSnapshot(snapshot, out var value)) return;
            if (value.Counters.Count == 0)
            {
                EditorGUILayout.HelpBox("No counters recorded", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Counters", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            foreach (var kvp in value.Counters)
            {
                var record = kvp.Value;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key, EditorStyles.miniLabel, GUILayout.Width(320));
                EditorGUILayout.LabelField($"value {record.Value}", GUILayout.Width(120));
                EditorGUILayout.LabelField($"delta {record.Delta}", GUILayout.Width(120));
                EditorGUILayout.LabelField($"samples {record.SampleCount}");
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawRates(ProfilerSnapshot? snapshot)
        {
            if (!TryGetSnapshot(snapshot, out var value)) return;
            if (value.Rates == null || value.Rates.Count == 0)
            {
                EditorGUILayout.HelpBox("No rate data recorded", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Rolling Counter Rates", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var sorted = new List<KeyValuePair<string, RateRecord>>(value.Rates);
            sorted.Sort((a, b) => b.Value.Count1Second.CompareTo(a.Value.Count1Second));

            foreach (var kvp in sorted)
            {
                var record = kvp.Value;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key, EditorStyles.miniLabel, GUILayout.Width(300));
                EditorGUILayout.LabelField($"1s {record.Count1Second}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"5s {record.Count5Seconds}", GUILayout.Width(90));
                EditorGUILayout.LabelField($"60s {record.Count60Seconds}", GUILayout.Width(100));
                EditorGUILayout.LabelField($"peak/s {record.PeakPerSecond}", GUILayout.Width(100));
                EditorGUILayout.LabelField($"total {record.TotalCount}");
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawGauges(ProfilerSnapshot? snapshot)
        {
            if (!TryGetSnapshot(snapshot, out var value)) return;
            if (value.Gauges.Count == 0)
            {
                EditorGUILayout.HelpBox("No gauges recorded", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Gauges", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            foreach (var kvp in value.Gauges)
            {
                var record = kvp.Value;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key, EditorStyles.miniLabel, GUILayout.Width(320));
                EditorGUILayout.LabelField(record.Value.ToString(), GUILayout.Width(120));
                EditorGUILayout.LabelField(DateTimeOffset.FromUnixTimeMilliseconds(record.Timestamp).ToLocalTime().ToString("HH:mm:ss.fff"));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDurations(ProfilerSnapshot? snapshot)
        {
            if (!TryGetSnapshot(snapshot, out var value)) return;
            if (value.Durations == null || value.Durations.Count == 0)
            {
                EditorGUILayout.HelpBox("No duration data recorded", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Duration Summaries (milliseconds)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var sorted = new List<KeyValuePair<string, DurationSummaryRecord>>(value.Durations);
            sorted.Sort((a, b) => b.Value.MaxMilliseconds.CompareTo(a.Value.MaxMilliseconds));

            foreach (var kvp in sorted)
            {
                var record = kvp.Value;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key, EditorStyles.miniLabel, GUILayout.Width(300));
                EditorGUILayout.LabelField($"count {record.Count}", GUILayout.Width(90));
                EditorGUILayout.LabelField($"avg {record.MeanMilliseconds:F4}", GUILayout.Width(110));
                EditorGUILayout.LabelField($"max {record.MaxMilliseconds:F4}", GUILayout.Width(110));
                EditorGUILayout.LabelField($"min {record.MinMilliseconds:F4}");
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSamples(ProfilerSnapshot? snapshot)
        {
            if (!TryGetSnapshot(snapshot, out var value)) return;
            if (value.Samples.Count == 0)
            {
                EditorGUILayout.HelpBox("No samples recorded", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Samples (milliseconds)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var sorted = new List<KeyValuePair<string, List<double>>>(value.Samples);
            sorted.Sort((a, b) => GetMean(b.Value).CompareTo(GetMean(a.Value)));

            foreach (var kvp in sorted)
            {
                var list = kvp.Value;
                if (list.Count == 0) continue;

                var mean = GetMean(list);
                var min = GetMin(list);
                var max = GetMax(list);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(300));
                DrawMiniBar(GUILayoutUtility.GetRect(120, 20, GUILayout.ExpandWidth(true)), list, mean);
                EditorGUILayout.LabelField($"avg {mean:F4}", GUILayout.Width(90));
                EditorGUILayout.LabelField($"[{min:F4}, {max:F4}]", GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawEvents(ProfilerSnapshot? snapshot)
        {
            if (!TryGetSnapshot(snapshot, out var value)) return;
            if (value.Events == null || value.Events.Count == 0)
            {
                EditorGUILayout.HelpBox("No diagnostic events recorded", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Recent Diagnostic Events", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            for (var i = value.Events.Count - 1; i >= 0; i--)
            {
                var record = value.Events[i];
                var style = record.Severity == DiagnosticSeverity.Error ? EditorStyles.boldLabel : EditorStyles.label;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(record.Severity.ToString(), style, GUILayout.Width(80));
                EditorGUILayout.LabelField(record.Name, style);
                EditorGUILayout.LabelField(DateTimeOffset.FromUnixTimeMilliseconds(record.Timestamp).ToLocalTime().ToString("HH:mm:ss.fff"), GUILayout.Width(110));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(record.Message, EditorStyles.wordWrappedMiniLabel);
                if (Math.Abs(record.Threshold) > double.Epsilon)
                {
                    EditorGUILayout.LabelField($"value {record.Value:F4}, threshold {record.Threshold:F4}", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawFlame(ProfilerSnapshot? snapshot)
        {
            if (!TryGetSnapshot(snapshot, out var value)) return;
            if (value.Root == null || value.Root.Roots.Count == 0)
            {
                EditorGUILayout.HelpBox("No flame samples recorded", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Flame Graph", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Nodes: {CountAllNodes(value.Root)}", GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            foreach (var categoryKvp in value.Root.Roots)
            {
                DrawFlameNode(categoryKvp.Value, 0);
            }
        }

        private static void DrawFlameNode(FlameNode node, int depth)
        {
            if (node == null || node.HitCount == 0) return;

            var previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = depth;
            var totalMs = node.TotalNanoseconds / 1000000.0d;
            var selfMs = node.SelfNanoseconds / 1000000.0d;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{node.Name} ({node.HitCount})", node.Children.Count > 0 ? EditorStyles.boldLabel : EditorStyles.miniLabel, GUILayout.MinWidth(260));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"self {selfMs:F3}ms", GUILayout.Width(110));
            EditorGUILayout.LabelField($"total {totalMs:F3}ms", GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            if (depth < 8)
            {
                foreach (var child in node.Children.Values)
                {
                    DrawFlameNode(child, depth + 1);
                }
            }

            EditorGUI.indentLevel = previousIndent;
        }

        private static void DrawMiniBar(Rect rect, List<double> values, double mean)
        {
            if (values.Count == 0) return;

            var max = GetMax(values);
            if (max <= 0d) return;

            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
            var count = Math.Min(values.Count, 100);
            var barWidth = rect.width / count;

            for (var i = 0; i < count; i++)
            {
                var value = values[values.Count - count + i];
                var height = (float)(value / max) * rect.height;
                var x = rect.x + i * barWidth;
                var y = rect.y + rect.height - height;
                var color = value > mean * 2d ? Color.red : value > mean ? Color.yellow : Color.green;
                EditorGUI.DrawRect(new Rect(x, y, Math.Max(1f, barWidth - 1f), height), color);
            }
        }

        private void StartRecording()
        {
            _isRecording = true;
            _profiler?.Start();
            InvalidateSnapshot();
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            Debug.Log("[Diagnostics] Recording started");
        }

        private void StopRecording()
        {
            _isRecording = false;
            _profiler?.Stop();
            InvalidateSnapshot();
            Debug.Log("[Diagnostics] Recording stopped");
        }

        private void ShowExportMenu()
        {
            var menu = new GenericMenu();
            foreach (var format in ExporterFactory.GetSupportedFormats())
            {
                var capturedFormat = format;
                menu.AddItem(new GUIContent($"Export as {format.ToUpperInvariant()}"), false, () => ExportSnapshot(capturedFormat));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Export Folder"), false, OpenExportFolder);
            menu.ShowAsContext();
        }

        private void ExportSnapshot(string format)
        {
            if (_profiler == null) return;

            Directory.CreateDirectory(_exportPath);
            var snapshot = _profiler.GetSnapshot();
            _cachedSnapshot = snapshot;
            _cachedSnapshotTime = EditorApplication.timeSinceStartup;
            _snapshotDirty = false;
            var exporter = ExporterFactory.Get(format);
            var fileName = $"abilitykit_{SanitizeFileName(snapshot.SessionId)}{exporter.Extension}";
            var filePath = Path.Combine(_exportPath, fileName);

            exporter.Export(snapshot, filePath);
            Debug.Log($"[Diagnostics] Exported to {filePath}");
            EditorUtility.RevealInFinder(filePath);
        }

        private void OpenExportFolder()
        {
            Directory.CreateDirectory(_exportPath);
            EditorUtility.RevealInFinder(_exportPath);
        }

        private ProfilerSnapshot? GetDisplaySnapshot()
        {
            var now = EditorApplication.timeSinceStartup;
            if (_snapshotDirty || !_cachedSnapshot.HasValue || now - _cachedSnapshotTime >= SnapshotRefreshIntervalSeconds)
            {
                _cachedSnapshot = _profiler?.GetSnapshot();
                _cachedSnapshotTime = now;
                _snapshotDirty = false;
            }

            return _cachedSnapshot;
        }

        private void InvalidateSnapshot()
        {
            _snapshotDirty = true;
        }

        private static bool TryGetSnapshot(ProfilerSnapshot? snapshot, out ProfilerSnapshot value)
        {
            if (!snapshot.HasValue || snapshot.Value.Root == null)
            {
                EditorGUILayout.HelpBox("Profiler not initialized", MessageType.Info);
                value = default;
                return false;
            }

            value = snapshot.Value;
            return true;
        }

        private static int CountNodes(FlameNode node)
        {
            if (node == null) return 0;
            var count = 1;
            foreach (var child in node.Children.Values)
            {
                count += CountNodes(child);
            }

            return count;
        }

        private static int CountAllNodes(FlameRoot root)
        {
            if (root == null) return 0;
            var count = 0;
            foreach (var node in root.Roots.Values)
            {
                count += CountNodes(node);
            }

            return count;
        }

        private static void DrawTopRates(ProfilerSnapshot snapshot, int count)
        {
            EditorGUILayout.LabelField($"Top {count} High Frequency Counters", EditorStyles.boldLabel);
            if (snapshot.Rates == null || snapshot.Rates.Count == 0)
            {
                EditorGUILayout.HelpBox("No rate data recorded", MessageType.Info);
                return;
            }

            var sorted = new List<KeyValuePair<string, RateRecord>>(snapshot.Rates);
            sorted.Sort((a, b) => b.Value.Count1Second.CompareTo(a.Value.Count1Second));
            var visible = Math.Min(count, sorted.Count);
            for (var i = 0; i < visible; i++)
            {
                var record = sorted[i].Value;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(sorted[i].Key, GUILayout.Width(300));
                EditorGUILayout.LabelField($"{record.Count1Second}/s", GUILayout.Width(90));
                EditorGUILayout.LabelField($"peak {record.PeakPerSecond}/s");
                EditorGUILayout.EndHorizontal();
            }
        }

        private static List<(string name, double mean, double max)> GetTopSamples(Dictionary<string, List<double>> samples, int count)
        {
            var result = new List<(string, double, double)>();
            foreach (var kvp in samples)
            {
                if (kvp.Value.Count > 0)
                {
                    result.Add((kvp.Key, GetMean(kvp.Value), GetMax(kvp.Value)));
                }
            }

            result.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return result.GetRange(0, Math.Min(count, result.Count));
        }

        private static double GetMean(List<double> values)
        {
            if (values == null || values.Count == 0) return 0d;
            double sum = 0d;
            foreach (var value in values)
            {
                sum += value;
            }

            return sum / values.Count;
        }

        private static double GetMin(List<double> values)
        {
            if (values == null || values.Count == 0) return 0d;
            var min = double.MaxValue;
            foreach (var value in values)
            {
                if (value < min) min = value;
            }

            return min;
        }

        private static double GetMax(List<double> values)
        {
            if (values == null || values.Count == 0) return 0d;
            var max = double.MinValue;
            foreach (var value in values)
            {
                if (value > max) max = value;
            }

            return max;
        }

        private static int GetCount<T>(ICollection<T> collection)
        {
            return collection == null ? 0 : collection.Count;
        }

        private static int GetCount<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
        {
            return dictionary == null ? 0 : dictionary.Count;
        }

        private static int GetCount<T>(ISet<T> set)
        {
            return set == null ? 0 : set.Count;
        }

        private static double GetDurationSeconds(ProfilerSnapshot snapshot)
        {
            return snapshot.Root == null ? 0d : Math.Max(0L, snapshot.Root.EndTimestamp - snapshot.Root.StartTimestamp) / 1000.0d;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "snapshot";
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }

            return value;
        }
    }
}
