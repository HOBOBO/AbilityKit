using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using AbilityKit.Diagnostics;
using AbilityKit.Diagnostics.Exporters;

namespace AbilityKit.Diagnostics.Editor.Windows
{
    /// <summary>
    /// 诊断窗口
    /// </summary>
    public class DiagnosticsWindow : EditorWindow
    {
        private EditorProfiler _profiler;
        private Vector2 _scrollPosition;
        private string _selectedTab = "Overview";
        private readonly string[] _tabs = { "Overview", "Counters", "Samples", "Flame" };
        private bool _isRecording;
        private double _lastUpdateTime;
        private string _exportPath;

        [MenuItem("Window/AbilityKit/Diagnostics")]
        public static void ShowWindow()
        {
            var window = GetWindow<DiagnosticsWindow>("Diagnostics");
            window.minSize = new Vector2(600, 400);
        }

        private void OnEnable()
        {
            _profiler = new EditorProfiler();
            ProfilerHub.SetProfiler(_profiler);

            // 默认导出路径
            _exportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "AbilityKit.Diagnostics"
            );
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

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_selectedTab)
            {
                case "Overview":
                    DrawOverview();
                    break;
                case "Counters":
                    DrawCounters();
                    break;
                case "Samples":
                    DrawSamples();
                    break;
                case "Flame":
                    DrawFlame();
                    break;
            }

            EditorGUILayout.EndScrollView();

            // 自动刷新
            if (_isRecording && EditorApplication.timeSinceStartup - _lastUpdateTime > 0.5)
            {
                _lastUpdateTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 标签页
            foreach (var tab in _tabs)
            {
                var isSelected = _selectedTab == tab;
                var style = isSelected ? EditorStyles.toolbarButton : EditorStyles.toolbarButton;

                if (GUILayout.Toggle(isSelected, tab, style, GUILayout.Width(80)))
                {
                    _selectedTab = tab;
                }
            }

            GUILayout.FlexibleSpace();

            // 录制控制
            GUI.backgroundColor = _isRecording ? Color.red : Color.green;
            if (GUILayout.Button(_isRecording ? "Stop" : "Record", GUILayout.Width(60)))
            {
                if (_isRecording)
                    StopRecording();
                else
                    StartRecording();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _profiler?.Clear();
            }

            // 导出
            if (GUILayout.Button("Export...", GUILayout.Width(60)))
            {
                ShowExportMenu();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void DrawOverview()
        {
            if (_profiler == null)
            {
                EditorGUILayout.HelpBox("Profiler not initialized", MessageType.Info);
                return;
            }

            var snapshot = _profiler.GetSnapshot();

            EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"ID: {snapshot.SessionId}");
            EditorGUILayout.LabelField($"Timestamp: {DateTimeOffset.FromUnixTimeMilliseconds(snapshot.Timestamp):yyyy-MM-dd HH:mm:ss}");
            EditorGUILayout.LabelField($"Duration: {(snapshot.Root.EndTimestamp - snapshot.Root.StartTimestamp) / 1000.0:F2}s");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // 计数器汇总
            EditorGUILayout.LabelField($"Counters: {snapshot.Counters.Count}");
            EditorGUILayout.LabelField($"Samples: {snapshot.Samples.Count}");

            // 火焰图节点数
            int totalNodes = 0;
            foreach (var root in snapshot.Root.Roots)
            {
                totalNodes += CountNodes(root.Value);
            }
            EditorGUILayout.LabelField($"Flame Nodes: {totalNodes}");

            EditorGUI.indentLevel--;

            // Top 5 耗时
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Top 5 Slowest", EditorStyles.boldLabel);

            var topSamples = GetTopSamples(snapshot.Samples, 5);
            foreach (var sample in topSamples)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(sample.name, GUILayout.Width(200));
                EditorGUILayout.LabelField($"{sample.mean:F4}ms (avg)", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{sample.max:F4}ms (max)");
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCounters()
        {
            if (_profiler == null) return;

            var counters = _profiler.GetCounters();

            if (counters.Count == 0)
            {
                EditorGUILayout.HelpBox("No counters recorded", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Counters", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            foreach (var kvp in counters)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key, EditorStyles.miniLabel, GUILayout.Width(250));

                var record = kvp.Value;
                var deltaColor = record.Delta > 0 ? Color.green : (record.Delta < 0 ? Color.red : Color.gray);
                GUI.backgroundColor = deltaColor;
                EditorGUILayout.LabelField($"+{record.Delta}", GUILayout.Width(50));
                GUI.backgroundColor = Color.white;

                EditorGUILayout.LabelField($"= {record.Value}");
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSamples()
        {
            if (_profiler == null) return;

            var samples = _profiler.GetSamples();

            if (samples.Count == 0)
            {
                EditorGUILayout.HelpBox("No samples recorded", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Samples (milliseconds)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var sorted = new List<KeyValuePair<string, List<double>>>(samples);
            sorted.Sort((a, b) =>
            {
                var meanA = GetMean(a.Value);
                var meanB = GetMean(b.Value);
                return meanB.CompareTo(meanA);
            });

            foreach (var kvp in sorted)
            {
                var list = kvp.Value;
                if (list.Count == 0) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(250));

                var mean = GetMean(list);
                var min = GetMin(list);
                var max = GetMax(list);

                // 绘制迷你柱状图
                var barRect = GUILayoutUtility.GetRect(100, 20, GUILayout.ExpandWidth(true));
                DrawMiniBar(barRect, list);

                EditorGUILayout.LabelField($"{mean:F4}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"[{min:F4}, {max:F4}]");
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawFlame()
        {
            if (_profiler == null || _profiler.GetSnapshot().Root == null) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Flame Graph", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Nodes: {CountAllNodes(_profiler.GetSnapshot().Root)}");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 绘制简化火焰图
            var root = _profiler.GetSnapshot().Root;
            foreach (var categoryKvp in root.Roots)
            {
                DrawFlameNode(categoryKvp.Value, 0);
            }
        }

        private void DrawFlameNode(FlameNode node, int depth)
        {
            if (node.HitCount == 0) return;

            EditorGUI.indentLevel = depth;

            var totalMs = node.TotalNanoseconds / 1_000_000.0;
            var selfMs = node.SelfNanoseconds / 1_000_000.0;

            EditorGUILayout.BeginHorizontal();

            // 节点名称
            var label = string.IsNullOrEmpty(node.Category)
                ? $"{node.Name} ({node.HitCount})"
                : $"{node.Name} ({node.HitCount})";

            var style = node.Children.Count > 0 ? EditorStyles.foldout : EditorStyles.miniLabel;
            EditorGUILayout.LabelField(label, style);

            GUILayout.FlexibleSpace();

            // 耗时
            if (selfMs > 0.01)
            {
                EditorGUILayout.LabelField($"self: {selfMs:F3}ms", GUILayout.Width(80));
            }
            if (totalMs > 0.01)
            {
                EditorGUILayout.LabelField($"total: {totalMs:F3}ms");
            }

            EditorGUILayout.EndHorizontal();

            // 递归绘制子节点
            if (depth < 5) // 限制深度
            {
                foreach (var child in node.Children)
                {
                    DrawFlameNode(child.Value, depth + 1);
                }
            }
        }

        private void DrawMiniBar(Rect rect, List<double> values)
        {
            if (values.Count == 0) return;

            var max = GetMax(values);
            if (max <= 0) return;

            // 绘制背景
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

            // 绘制柱状
            var barWidth = rect.width / Math.Min(values.Count, 100);
            for (int i = 0; i < Math.Min(values.Count, 100); i++)
            {
                var height = (float)(values[i] / max) * rect.height;
                var x = rect.x + i * barWidth;
                var y = rect.y + rect.height - height;

                var color = values[i] > GetMean(values) * 2 ? Color.red :
                           (values[i] > GetMean(values) ? Color.yellow : Color.green);
                EditorGUI.DrawRect(new Rect(x, y, barWidth - 1, height), color);
            }
        }

        private void StartRecording()
        {
            _isRecording = true;
            _profiler?.Start();
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            Debug.Log("[Diagnostics] Recording started");
        }

        private void StopRecording()
        {
            _isRecording = false;
            _profiler?.Stop();
            Debug.Log("[Diagnostics] Recording stopped");
        }

        private void ShowExportMenu()
        {
            var menu = new GenericMenu();

            foreach (var format in ExporterFactory.GetSupportedFormats())
            {
                menu.AddItem(new GUIContent($"Export as {format.ToUpper()}"), false, () => ExportSnapshot(format));
            }

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Open Export Folder"), false, () =>
            {
                if (!Directory.Exists(_exportPath))
                {
                    Directory.CreateDirectory(_exportPath);
                }
                System.Diagnostics.Process.Start("explorer.exe", _exportPath);
            });

            menu.ShowAsContext();
        }

        private void ExportSnapshot(string format)
        {
            if (_profiler == null) return;

            var snapshot = _profiler.GetSnapshot();
            var exporter = ExporterFactory.Get(format);

            var fileName = $"abilitykit_{snapshot.SessionId}{exporter.Extension}";
            var filePath = Path.Combine(_exportPath, fileName);

            exporter.Export(snapshot, filePath);
            Debug.Log($"[Diagnostics] Exported to {filePath}");
        }

        private int CountNodes(FlameNode node)
        {
            int count = 1;
            foreach (var child in node.Children.Values)
            {
                count += CountNodes(child);
            }
            return count;
        }

        private int CountAllNodes(FlameRoot root)
        {
            int count = 0;
            foreach (var kvp in root.Roots)
            {
                count += CountNodes(kvp.Value);
            }
            return count;
        }

        private List<(string name, double mean, double max)> GetTopSamples(Dictionary<string, List<double>> samples, int count)
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

        private double GetMean(List<double> values)
        {
            if (values.Count == 0) return 0;
            double sum = 0;
            foreach (var v in values) sum += v;
            return sum / values.Count;
        }

        private double GetMin(List<double> values)
        {
            if (values.Count == 0) return 0;
            double min = double.MaxValue;
            foreach (var v in values) if (v < min) min = v;
            return min;
        }

        private double GetMax(List<double> values)
        {
            if (values.Count == 0) return 0;
            double max = double.MinValue;
            foreach (var v in values) if (v > max) max = v;
            return max;
        }
    }
}
