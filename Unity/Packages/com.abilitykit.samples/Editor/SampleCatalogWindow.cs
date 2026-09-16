#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Samples.Logic;
using AbilityKit.Samples.Logic.Infrastructure.Config;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Samples.Editor
{
    /// <summary>
    /// AbilityKit 示例目录的 Unity 宿主。
    ///
    /// 左侧是目录（按分类分组，可搜索），右侧是选中示例的指南、运行按钮与结构化输出。
    /// 输出直接来自 <see cref="BufferedSampleLogger"/> 捕获的 <see cref="SampleLogEntry"/>，
    /// 用最基础的 IMGUI 逐条渲染——不做画布级复刻（见设计文档 §3.2 / §3.3）。
    ///
    /// 运行模式是即时（一次性跑完），与 Console 宿主一致，所以同一份示例两边产出相同文本。
    /// 实时跨帧驱动留给可视化步骤。
    /// </summary>
    public sealed class SampleCatalogWindow : EditorWindow
    {
        private const string MenuPath = "Window/AbilityKit/示例目录";
        private const float ListWidth = 320f;

        private readonly Dictionary<SampleCategory, bool> _categoryFoldouts =
            new Dictionary<SampleCategory, bool>();

        private readonly BufferedSampleLogger _logger = new BufferedSampleLogger();

        // 展开状态跨帧保存在窗口上：自查答案默认折叠，源码片段展开过一次就不再读盘。
        private readonly HashSet<string> _expandedCheckpoints = new HashSet<string>();
        private readonly HashSet<string> _expandedCodes = new HashSet<string>();

        private SampleCatalog? _catalog;
        private SampleExecutionService? _service;
        private SamplePackageResourceProvider? _resourceProvider;
        private SampleCatalogEntry? _selected;
        private int _frameIndex;
        private string _search = string.Empty;
        private string _status = "尚未加载示例目录。";
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private Vector2 _outputScroll;

        [MenuItem(MenuPath, false, 100)]
        public static void Open()
        {
            var window = GetWindow<SampleCatalogWindow>();
            window.titleContent = new GUIContent("AbilityKit 示例");
            window.minSize = new Vector2(760f, 460f);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureCatalog();
        }

        private void OnGUI()
        {
            EnsureCatalog();

            EditorGUILayout.BeginHorizontal();
            DrawCatalogColumn();
            DrawDetailColumn();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
        }

        // --- 左：目录 ---------------------------------------------------------

        private void DrawCatalogColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));

            EditorGUILayout.BeginHorizontal();
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("重载", EditorStyles.miniButton, GUILayout.Width(48f)))
            {
                EnsureCatalog(forceRebuild: true);
            }
            EditorGUILayout.EndHorizontal();

            if (_catalog == null)
            {
                EditorGUILayout.HelpBox(_status, MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll))
            {
                _listScroll = scroll.scrollPosition;

                foreach (var group in _catalog.GroupByCategory())
                {
                    var entries = Filter(group.Value).ToList();
                    if (entries.Count == 0)
                    {
                        continue;
                    }

                    if (!_categoryFoldouts.TryGetValue(group.Key, out var expanded))
                    {
                        expanded = true;
                        _categoryFoldouts[group.Key] = true;
                    }

                    expanded = EditorGUILayout.Foldout(
                        expanded,
                        group.Key.GetDisplayName() + "  (" + entries.Count + ")",
                        true);
                    _categoryFoldouts[group.Key] = expanded;
                    if (!expanded)
                    {
                        continue;
                    }

                    EditorGUI.indentLevel++;
                    foreach (var entry in entries)
                    {
                        DrawCatalogEntry(entry);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCatalogEntry(SampleCatalogEntry entry)
        {
            var isSelected = _selected != null &&
                             string.Equals(_selected.Id, entry.Id, StringComparison.Ordinal);
            var previous = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.35f, 0.6f, 1f, 1f);
            }

            var label = string.IsNullOrWhiteSpace(entry.Level)
                ? entry.Title
                : entry.Title + "   [" + entry.Level + "]";

            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                Select(entry);
            }

            GUI.backgroundColor = previous;
        }

        private IEnumerable<SampleCatalogEntry> Filter(IReadOnlyList<SampleCatalogEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                return entries;
            }

            var needle = _search.Trim();
            return entries.Where(entry =>
                Contains(entry.Title, needle) ||
                Contains(entry.Id, needle) ||
                Contains(entry.Description, needle) ||
                entry.Tags.Any(tag => Contains(tag, needle)));
        }

        private static bool Contains(string? haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack) &&
                   haystack!.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // --- 右：详情 ---------------------------------------------------------

        private void DrawDetailColumn()
        {
            EditorGUILayout.BeginVertical();

            if (_selected == null)
            {
                EditorGUILayout.HelpBox("从左侧选择一条示例。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll))
            {
                _detailScroll = scroll.scrollPosition;
                DrawGuide(_selected);
                SampleLearningRenderer.DrawLearningContract(_selected, NavigateTo);
                DrawRunControls(_selected);
                DrawOutput();
                DrawVisual(_selected);
                SampleLearningRenderer.DrawCheckpoints(_selected, _expandedCheckpoints);
                SampleLearningRenderer.DrawCodeWalkthrough(_selected, _expandedCodes);
                DrawNextSteps(_selected);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGuide(SampleCatalogEntry entry)
        {
            EditorGUILayout.LabelField(entry.Title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("id: " + entry.Id, EditorStyles.miniLabel);

            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(entry.Level))
            {
                meta.Add("level=" + entry.Level);
            }
            if (!string.IsNullOrWhiteSpace(entry.Status))
            {
                meta.Add("status=" + entry.Status);
            }
            if (entry.Modules.Count > 0)
            {
                meta.Add("modules=" + string.Join(", ", entry.Modules));
            }
            if (meta.Count > 0)
            {
                EditorGUILayout.LabelField(string.Join("   ", meta), EditorStyles.miniLabel);
            }

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                EditorGUILayout.LabelField(entry.Description, EditorStyles.wordWrappedLabel);
            }

            var guide = entry.Guide;
            if (!string.IsNullOrWhiteSpace(guide.Purpose))
            {
                EditorGUILayout.LabelField("目的", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(guide.Purpose, EditorStyles.wordWrappedLabel);
            }
            if (!string.IsNullOrWhiteSpace(guide.Observe))
            {
                EditorGUILayout.LabelField("观察", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(guide.Observe, EditorStyles.wordWrappedLabel);
            }
            if (!string.IsNullOrWhiteSpace(guide.Takeaway))
            {
                EditorGUILayout.LabelField("带走", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(guide.Takeaway, EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space();
        }

        private void DrawRunControls(SampleCatalogEntry entry)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("运行", GUILayout.Height(24f), GUILayout.Width(120f)))
            {
                RunSelected(entry);
            }

            if (_logger.Entries.Count > 0 &&
                GUILayout.Button("清空输出", GUILayout.Height(24f), GUILayout.Width(100f)))
            {
                _logger.Clear();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void DrawOutput()
        {
            EditorGUILayout.LabelField("输出（结构化文本）", EditorStyles.boldLabel);
            if (_logger.Entries.Count == 0)
            {
                EditorGUILayout.HelpBox("点「运行」查看该示例的结构化输出。", MessageType.None);
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(
                       _outputScroll,
                       GUILayout.MinHeight(180f)))
            {
                _outputScroll = scroll.scrollPosition;
                for (var i = 0; i < _logger.Entries.Count; i++)
                {
                    DrawLogEntry(_logger.Entries[i]);
                }
            }

            EditorGUILayout.Space();
        }

        private static void DrawLogEntry(SampleLogEntry entry)
        {
            switch (entry.Kind)
            {
                case SampleLogKind.Section:
                    EditorGUILayout.LabelField(entry.Text, EditorStyles.boldLabel);
                    break;
                case SampleLogKind.Divider:
                    EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                    break;
                case SampleLogKind.Line:
                    EditorGUILayout.Space();
                    break;
                case SampleLogKind.Bullet:
                    EditorGUILayout.LabelField("•  " + entry.Text, EditorStyles.wordWrappedLabel);
                    break;
                case SampleLogKind.Numbered:
                    EditorGUILayout.LabelField(
                        (entry.Number ?? 0) + ". " + entry.Text,
                        EditorStyles.wordWrappedLabel);
                    break;
                case SampleLogKind.KeyValue:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.Key, GUILayout.Width(220f));
                    EditorGUILayout.LabelField(entry.Text, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.EndHorizontal();
                    break;
                case SampleLogKind.Warn:
                    EditorGUILayout.HelpBox(entry.Text, MessageType.Warning);
                    break;
                case SampleLogKind.Error:
                    EditorGUILayout.HelpBox(entry.Text, MessageType.Error);
                    break;
                default:
                    EditorGUILayout.LabelField(entry.Text, EditorStyles.wordWrappedLabel);
                    break;
            }
        }

        /// <summary>
        /// 语义图：帧序列是主线（37/37 的示例都有），节点图只在有数据时才画（15/37）。
        /// 两者都没有时给一句明确说明，而不是留一片空白让人以为界面坏了。
        /// </summary>
        private void DrawVisual(SampleCatalogEntry entry)
        {
            var model = entry.VisualModel;
            var hasFrames = entry.VisualFrames.Count > 0;
            var hasGraph = model.Nodes != null && model.Nodes.Length > 0;

            EditorGUILayout.Space();
            if (!hasFrames && !hasGraph)
            {
                SampleVisualRenderer.DrawUnavailableNotice();
                return;
            }

            EditorGUILayout.LabelField("语义图", EditorStyles.boldLabel);
            if (hasFrames)
            {
                _frameIndex = SampleVisualRenderer.DrawFrameStepper(entry, _frameIndex);
                SampleVisualRenderer.DrawStepProgress(entry, _frameIndex);
                SampleVisualRenderer.DrawFrameDetail(entry, _frameIndex);
            }

            if (hasGraph)
            {
                SampleVisualRenderer.DrawGraph(model);
            }

            SampleVisualRenderer.DrawMetrics(model);
        }

        private void DrawNextSteps(SampleCatalogEntry entry)
        {
            if (entry.Next.Count == 0 || _catalog == null)
            {
                return;
            }

            EditorGUILayout.LabelField("下一步", EditorStyles.boldLabel);
            foreach (var nextId in entry.Next)
            {
                if (!_catalog.TryGetById(nextId, out var next))
                {
                    continue;
                }

                if (GUILayout.Button("→ " + next.Title + "   (" + nextId + ")", EditorStyles.miniButton))
                {
                    NavigateTo(nextId);
                }
            }
        }

        /// <summary>
        /// 跳转到指定示例。<c>next</c> 链与学习契约里的"前置"都走这里，
        /// 保证换示例时帧号、输出与展开状态一起复位。
        /// </summary>
        private void NavigateTo(string sampleId)
        {
            if (_catalog == null || !_catalog.TryGetById(sampleId, out var entry))
            {
                _status = "清单里找不到示例：" + sampleId;
                return;
            }

            Select(entry);
        }

        private void Select(SampleCatalogEntry entry)
        {
            _selected = entry;
            _frameIndex = 0;
            _logger.Clear();
            _status = entry.Title;
            GUI.FocusControl(null);
        }

        // --- 目录与执行 -------------------------------------------------------

        private void EnsureCatalog(bool forceRebuild = false)
        {
            if (_catalog != null && !forceRebuild)
            {
                return;
            }

            try
            {
                if (_resourceProvider == null)
                {
                    _resourceProvider = new SamplePackageResourceProvider();
                }
                else if (forceRebuild)
                {
                    _resourceProvider.Rebuild();
                }

                // 示例内容通过 IResourceProvider 取资源；.NET 宿主装文件系统实现，
                // Unity 宿主在这里装自己的包内实现。
                ResourceProviders.Current = _resourceProvider;

                _catalog = SampleCatalogProvider.CreateCatalog();
                _service = new SampleExecutionService(
                    _catalog,
                    _ => new SampleHostEnvironment(),
                    resources: _resourceProvider);

                _selected = null;
                _logger.Clear();
                _status = "已加载 " + _catalog.Entries.Count + " 条示例。";
            }
            catch (Exception ex)
            {
                _catalog = null;
                _service = null;
                _status = "示例目录加载失败：" + ex.Message;
            }
        }

        private void RunSelected(SampleCatalogEntry entry)
        {
            if (_service == null)
            {
                _status = "执行服务不可用。";
                return;
            }

            _logger.Clear();
            try
            {
                var options = new SampleRunOptions
                {
                    ExecutionMode = ExecutionMode.Instant,
                    HostKind = SampleHostKind.Custom,
                    HostCapabilities = SampleHostCapabilities.ForHost(SampleHostKind.Custom),
                };

                var result = _service.Run(entry, _logger, options);
                _status = result.Succeeded
                    ? "已运行：" + entry.Title
                    : "运行失败：" + entry.Title + " —— " + result.ErrorMessage;
            }
            catch (Exception ex)
            {
                _status = "运行异常：" + ex.Message;
            }

            _outputScroll = Vector2.zero;
            Repaint();
        }
    }
}
