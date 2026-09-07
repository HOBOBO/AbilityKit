using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using UnityEditor;
using UnityEngine;

using AbilityKit.BehaviorTree.Editor.Debugging.Contributors;
using AbilityKit.BehaviorTree.Editor.Debugging.Observation;
using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 运行时观察窗口：轮询 <see cref="DebugRegistry"/> 已登记实例，**主动拉取**节点状态、
    /// 运行路径与黑板值。左栏实例列表按树配置分组、以注册序号区分；选中实例后：
    /// 节点树可折叠、双击组合/装饰节点跳转子树（面包屑回跳），黑板值变更高亮并可过滤，
    /// 可一键"在图中查看"把实时状态着色到 GraphView 画布。运行时（逻辑侧/服务端/console）
    /// 不引用任何编辑器类型；本窗口是纯观察者，不修改任何运行时结构。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtDebugObservationWindow")]
    public class DebugObservationWindow : EditorWindow
    {
        private const float LeftPaneWidth = 280f;

        private readonly List<DebugRegistryEntry> _entries = new();
        private readonly Dictionary<string, NodeDebugInfo> _liveNodes = new();
        private readonly HashSet<string> _collapsedNodes = new();
        private readonly Dictionary<string, string> _parentOf = new();
        private readonly Dictionary<string, string> _subtreeRootTree = new();
        private readonly Dictionary<string, AuthoringNodeMetadata> _authoredMetadata = new();
        private readonly ObservationController _controller = new();
        private readonly ObservationContributorRegistry _contributors =
            ObservationContributorRegistry.Default;

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private Vector2 _eventScroll;
        private TreeDefinition _displayDefinition;
        private string _instanceFilter = "";
        private string _nodeFilter = "";
        private string _blackboardFilter = "";
        private long _selectedId;
        private string _focusNodeId = "";
        private string _selectedNodeId = "";
        private bool _showRunningPathOnly;
        private bool _showEventHistory = true;
        private int _historyIndex = -1;
        private int _compareIndexA = -1;
        private double _sampleIntervalSeconds = ObservationSettings.DefaultSampleIntervalSeconds;
        private int _timelineCapacity = ObservationSettings.DefaultTimelineCapacity;
        private ObservationOfflineReplay _offlineReplay;
        private string _lastRecordingPath = "";
        private double _lastReplayTickSeconds;

        [MenuItem("Window/AbilityKit/Behavior Tree Observation")]
        private static void Open()
        {
            var window = GetWindow<DebugObservationWindow>();
            window.titleContent = new GUIContent("BT Observation");
            window.minSize = new Vector2(640f, 420f);
        }

        private void OnGUI()
        {
            PollIfNeeded();
            TickOfflineReplay();

            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawInstanceList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();

            if (EditorApplication.isPlaying)
            {
                Repaint();   // Play 期间持续刷新
            }
        }

        private void PollIfNeeded()
        {
            var previousSelection = _selectedId;
            _controller.Poll(EditorApplication.timeSinceStartup);

            _entries.Clear();
            _entries.AddRange(_controller.Entries);
            _entries.Sort((a, b) =>
            {
                var tree = string.Compare(a.View?.TreeId, b.View?.TreeId, System.StringComparison.Ordinal);
                return tree != 0 ? tree : a.Id.CompareTo(b.Id);
            });

            _selectedId = _controller.SelectedInstanceId;
            if (previousSelection != _selectedId)
            {
                ResetObservedState();
                if (SelectedView is { } selected) PrepareSelectedView(selected);
            }
        }

        private void TickOfflineReplay()
        {
            if (_offlineReplay == null)
            {
                _lastReplayTickSeconds = EditorApplication.timeSinceStartup;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var delta = _lastReplayTickSeconds <= 0d ? 0d : now - _lastReplayTickSeconds;
            _lastReplayTickSeconds = now;
            _offlineReplay.Tick(delta);
            if (_offlineReplay.IsPlaying) Repaint();
        }

        private void ResetObservedState()
        {
            _focusNodeId = "";
            _selectedNodeId = "";
            _collapsedNodes.Clear();
            _parentOf.Clear();
            _subtreeRootTree.Clear();
            _authoredMetadata.Clear();
            _displayDefinition = null;
            _liveNodes.Clear();
            _historyIndex = -1;
            _compareIndexA = -1;
        }

        private void PrepareSelectedView(TreeDebugView view)
        {
            if (_displayDefinition != null) return;
            _displayDefinition = view.TreeDefinition;
            LoadAuthoringMetadata(view);
        }

        private void CaptureSelected(TreeDebugView view)
        {
            PrepareSelectedView(view);
            if (_controller.Latest == null) _controller.Sample();
        }

        private void LoadAuthoringMetadata(TreeDebugView view)
        {
            AuthoringSourceDocument document;
            try
            {
                document = AuthoringDocumentCatalog.BuildObservationDocument(view, EditorNodeCatalog.Registry);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BtObservation] 无法加载 authoring 元数据: " + ex.Message);
                return;
            }
            foreach (var metadata in document.NodeMetadata)
            {
                _authoredMetadata[metadata.NodeId] = metadata;
            }
        }

        private TreeDebugView SelectedView
        {
            get
            {
                foreach (var entry in _entries)
                {
                    if (entry.Id == _selectedId) return entry.View;
                }
                return null;
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Behavior Tree Runtime", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(_offlineReplay != null ? "Offline Replay" : _controller.State.ToString(), EditorStyles.miniLabel);
            GUILayout.Label(_entries.Count + " instance(s)", EditorStyles.miniLabel);
            GUILayout.Label("Interval", EditorStyles.miniLabel);
            var interval = EditorGUILayout.DoubleField(_sampleIntervalSeconds, GUILayout.Width(52f));
            GUILayout.Label("Capacity", EditorStyles.miniLabel);
            var capacity = EditorGUILayout.IntField(_timelineCapacity, GUILayout.Width(58f));
            ApplyToolbarSettings(interval, capacity);
            if (GUILayout.Button(_controller.Paused ? "继续刷新" : "冻结视图", EditorStyles.toolbarButton))
            {
                if (_controller.Paused) _controller.Resume();
                else _controller.Pause();
            }
            if (GUILayout.Button(_controller.Paused ? "单步采样" : "Refresh", EditorStyles.toolbarButton))
            {
                _controller.Sample();
                _historyIndex = -1;
            }
            if (GUILayout.Button("Export Recording", EditorStyles.toolbarButton)) ExportRecording();
            if (GUILayout.Button("Import Replay", EditorStyles.toolbarButton)) ImportReplay();
            if (_offlineReplay != null && GUILayout.Button("Live", EditorStyles.toolbarButton))
            {
                _offlineReplay = null;
                _lastReplayTickSeconds = 0d;
                ResetObservedState();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyToolbarSettings(double interval, int capacity)
        {
            try
            {
                var nextInterval = ObservationSettings.ClampSampleIntervalSeconds(interval);
                if (System.Math.Abs(nextInterval - _sampleIntervalSeconds) > double.Epsilon)
                {
                    _sampleIntervalSeconds = nextInterval;
                    _controller.SampleIntervalSeconds = nextInterval;
                }

                var nextCapacity = ObservationSettings.ClampTimelineCapacity(capacity);
                if (nextCapacity != _timelineCapacity)
                {
                    _timelineCapacity = nextCapacity;
                    _controller.TimelineCapacity = nextCapacity;
                }
            }
            catch (System.ArgumentOutOfRangeException)
            {
                _sampleIntervalSeconds = _controller.SampleIntervalSeconds;
                _timelineCapacity = _controller.TimelineCapacity;
            }
        }

        private void ExportRecording()
        {
            var directory = string.IsNullOrEmpty(_lastRecordingPath)
                ? ""
                : System.IO.Path.GetDirectoryName(_lastRecordingPath);
            var path = EditorUtility.SaveFilePanel(
                "Export Behavior Tree Observation Recording",
                directory,
                "bt-observation-recording",
                "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                ObservationRecording.ExportToFile(path, _controller.Timeline, _controller);
                _lastRecordingPath = path;
                ShowNotification(new GUIContent("Observation recording exported"));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BtObservation] Recording export failed: " + ex.Message);
                ShowNotification(new GUIContent("Recording export failed"));
            }
        }

        private void ImportReplay()
        {
            var directory = string.IsNullOrEmpty(_lastRecordingPath)
                ? ""
                : System.IO.Path.GetDirectoryName(_lastRecordingPath);
            var path = EditorUtility.OpenFilePanel(
                "Import Behavior Tree Observation Recording",
                directory,
                "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                _offlineReplay = ObservationRecording.ImportReplayFromFile(path);
                _lastReplayTickSeconds = EditorApplication.timeSinceStartup;
                _lastRecordingPath = path;
                _historyIndex = -1;
                _compareIndexA = -1;
                ResetObservedState();
                ShowNotification(new GUIContent("Observation replay loaded"));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BtObservation] Recording import failed: " + ex.Message);
                ShowNotification(new GUIContent("Recording import failed"));
            }
        }

        // ------------------------------------------------------------------
        // 左栏：实例列表（树配置分组，注册序号区分）
        // ------------------------------------------------------------------

        private void DrawInstanceList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(LeftPaneWidth));
            _instanceFilter = EditorGUILayout.TextField(
                _instanceFilter ?? "", EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No running behavior trees registered.\n" +
                    "A tree registers itself when created with a non-empty DebugName (TreeRunOptions).",
                    MessageType.Info);
            }

            string currentGroup = null;
            foreach (var entry in _entries)
            {
                var view = entry.View;
                if (view == null) continue;
                if (!MatchesInstanceFilter(entry, view)) continue;
                if (_contributors.Filters.Count > 0
                    && !_contributors.AnyFilterMatches(ObservationFilterContext.ForInstance(entry))) continue;

                if (!string.Equals(currentGroup, view.TreeId, System.StringComparison.Ordinal))
                {
                    currentGroup = view.TreeId;
                    GUILayout.Space(4f);
                    GUILayout.Label(currentGroup, EditorStyles.boldLabel);
                }

                var label = "#" + entry.Id + "  " + view.DisplayName
                            + (string.IsNullOrEmpty(view.OwnerLabel) ? "" : "  ·  " + view.OwnerLabel);
                var oldBackground = GUI.backgroundColor;
                if (entry.Id == _selectedId) GUI.backgroundColor = new Color(0.42f, 0.66f, 0.92f);
                if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(22f)))
                {
                    if (_selectedId != entry.Id && _controller.SelectInstance(entry.Id))
                    {
                        _selectedId = entry.Id;
                        ResetObservedState();
                        PrepareSelectedView(entry.View);
                        _controller.Sample();
                    }
                }
                GUI.backgroundColor = oldBackground;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private bool MatchesInstanceFilter(DebugRegistryEntry entry, TreeDebugView view)
        {
            if (string.IsNullOrWhiteSpace(_instanceFilter)) return true;
            return entry.Id.ToString().IndexOf(_instanceFilter, System.StringComparison.OrdinalIgnoreCase) >= 0
                || view.TreeId.IndexOf(_instanceFilter, System.StringComparison.OrdinalIgnoreCase) >= 0
                || view.DisplayName.IndexOf(_instanceFilter, System.StringComparison.OrdinalIgnoreCase) >= 0
                || view.OwnerLabel.IndexOf(_instanceFilter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ------------------------------------------------------------------
        // 右栏：选中实例详情
        // ------------------------------------------------------------------

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_offlineReplay != null)
            {
                DrawOfflineReplayDetail();
                EditorGUILayout.EndVertical();
                return;
            }

            var view = SelectedView;
            if (view != null && _controller.Latest == null) CaptureSelected(view);
            var snapshot = DisplayedSnapshot;
            if (view == null && snapshot == null)
            {
                GUILayout.FlexibleSpace();
                if (_controller.State == ObservationSessionState.Disconnected)
                {
                    EditorGUILayout.HelpBox(
                        "选中的运行实例已断开。历史采样仍保留为只读数据；实例重新注册后可重新选择并继续观察。",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("请选择一个运行中的行为树实例。", MessageType.Info);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            if (snapshot == null)
            {
                EditorGUILayout.HelpBox("当前实例尚无可用采样。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            var nodes = snapshot.Nodes;
            RefreshDisplayState(snapshot, view);

            EditorGUILayout.LabelField(
                $"#{snapshot.InstanceId}  {snapshot.DisplayName}  ({snapshot.TreeId})"
                + (string.IsNullOrEmpty(snapshot.OwnerLabel) ? "" : $"  —  {snapshot.OwnerLabel}"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"frame {snapshot.Frame}   nodes {snapshot.NodeCount}   state {DescribeRootState(nodes)}"
                + (_controller.Paused ? "   [视图已冻结]" : "")
                + (_controller.State == ObservationSessionState.Disconnected ? "   [Disconnected]" : "")
                + (_historyIndex >= 0 ? "   [历史采样]" : ""),
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("在图中查看", EditorStyles.miniButton))
            {
                AuthoringGraphWindow.OpenObservation(
                    snapshot,
                    _displayDefinition,
                    DisplayedPreviousSnapshot,
                    DisplayedDiff);
            }
            if (GUILayout.Button("复制运行快照", EditorStyles.miniButton)) CopyRuntimeSnapshot(snapshot);
            if (GUILayout.Button("全部展开", EditorStyles.miniButton)) _collapsedNodes.Clear();
            if (GUILayout.Button("全部收起", EditorStyles.miniButton))
            {
                foreach (var node in _displayDefinition?.Nodes ?? EmptyNodes)
                {
                    if (node.ChildIds.Count > 0) _collapsedNodes.Add(node.Id);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("节点", EditorStyles.miniLabel, GUILayout.Width(28f));
            var nextNodeFilter = EditorGUILayout.TextField(
                _nodeFilter ?? "", EditorStyles.toolbarSearchField, GUILayout.Width(220f));
            if (!string.Equals(nextNodeFilter, _nodeFilter, System.StringComparison.Ordinal))
            {
                _nodeFilter = nextNodeFilter;
                FocusFirstNodeMatch();
            }
            _showRunningPathOnly = GUILayout.Toggle(
                _showRunningPathOnly, "仅运行路径", EditorStyles.miniButton, GUILayout.Width(84f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            DrawBreadcrumb();
            GUILayout.Space(2f);
            DrawNodeTree();
            DrawSelectedNodeDetail();

            GUILayout.Space(6f);
            DrawBlackboard(snapshot);
            DrawEventHistory();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawOfflineReplayDetail()
        {
            var snapshot = _offlineReplay.Current;
            if (snapshot == null)
            {
                EditorGUILayout.HelpBox("Imported observation recording has no samples.", MessageType.Info);
                return;
            }
            RefreshDisplayState(snapshot, null);

            EditorGUILayout.LabelField(
                $"Offline replay  {snapshot.DisplayName}  ({snapshot.TreeId})",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"sample {_offlineReplay.CurrentIndex + 1}/{_offlineReplay.Count}   frame {snapshot.Frame}   nodes {snapshot.NodeCount}",
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_offlineReplay.IsPlaying ? "Pause" : "Play", EditorStyles.miniButton, GUILayout.Width(56f)))
                _offlineReplay.TogglePlayback();
            GUI.enabled = _offlineReplay.CurrentIndex > 0;
            if (GUILayout.Button("Previous", EditorStyles.miniButton, GUILayout.Width(72f))) _offlineReplay.StepPrevious();
            GUI.enabled = _offlineReplay.CurrentIndex + 1 < _offlineReplay.Count;
            if (GUILayout.Button("Next", EditorStyles.miniButton, GUILayout.Width(72f))) _offlineReplay.StepNext();
            GUI.enabled = _offlineReplay.Count > 0;
            if (GUILayout.Button("Latest", EditorStyles.miniButton, GUILayout.Width(72f))) _offlineReplay.JumpToLatest();
            GUI.enabled = true;
            GUILayout.Label("Speed", EditorStyles.miniLabel, GUILayout.Width(42f));
            _offlineReplay.PlaybackSpeed = EditorGUILayout.DoubleField(
                _offlineReplay.PlaybackSpeed,
                GUILayout.Width(44f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _offlineReplay.Count > 0;
            var nextIndex = EditorGUILayout.IntSlider(
                _offlineReplay.CurrentIndex,
                0,
                System.Math.Max(0, _offlineReplay.Count - 1));
            if (nextIndex != _offlineReplay.CurrentIndex) _offlineReplay.Seek(nextIndex);
            if (GUILayout.Button("Set A", EditorStyles.miniButton, GUILayout.Width(52f))) _offlineReplay.MarkCompareA();
            if (GUILayout.Button("Set B", EditorStyles.miniButton, GUILayout.Width(52f))) _offlineReplay.MarkCompareB();
            if (GUILayout.Button("Graph", EditorStyles.miniButton, GUILayout.Width(56f)))
                AuthoringGraphWindow.OpenObservation(
                    _offlineReplay.Current,
                    _displayDefinition,
                    _offlineReplay.Previous,
                    _offlineReplay.CompareDiff ?? _offlineReplay.CurrentDiff);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (_offlineReplay.CompareDiff != null)
            {
                var compare = _offlineReplay.CompareDiff;
                EditorGUILayout.LabelField(
                    $"A/B: sample {_offlineReplay.CompareIndexA} -> {_offlineReplay.CompareIndexB}; "
                    + $"nodes {compare.ChangedNodeIds.Count}, keys {compare.ChangedBlackboardKeyIds.Count}",
                    EditorStyles.miniLabel);
            }

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            GUILayout.Label("Nodes", EditorStyles.boldLabel);
            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                var node = snapshot.Nodes[i];
                var oldColor = GUI.color;
                GUI.color = StateColor(node.State);
                EditorGUILayout.LabelField(
                    node.NodeId,
                    node.State + "  " + node.TypeId + (node.OnStackCount > 0 ? "  active" : ""),
                    EditorStyles.miniLabel);
                GUI.color = oldColor;
            }

            GUILayout.Space(6f);
            DrawBlackboard(snapshot);
            DrawOfflineReplayChanges();
            EditorGUILayout.EndScrollView();
        }

        private void DrawOfflineReplayChanges()
        {
            var timeline = _offlineReplay.Timeline;
            GUILayout.Space(6f);
            GUILayout.Label("Replay Timeline (" + timeline.Count + ")", EditorStyles.boldLabel);
            var changes = new List<ObservationChange>(timeline.EnumerateChanges());
            _eventScroll = EditorGUILayout.BeginScrollView(_eventScroll, GUILayout.MaxHeight(150f));
            if (changes.Count == 0) EditorGUILayout.LabelField("No recorded changes.", EditorStyles.miniLabel);
            for (var i = changes.Count - 1; i >= 0; i--)
            {
                var item = changes[i];
                EditorGUILayout.LabelField(
                    $"f{item.Frame}  {item.Kind}  {item.Target}",
                    item.From + " -> " + item.To,
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private void CopyRuntimeSnapshot(ObservationSnapshot snapshot)
        {
            try
            {
                EditorGUIUtility.systemCopyBuffer =
                    ObservationEditorTransport.CreateRuntimeSnapshotJson(snapshot);
                ShowNotification(new GUIContent("运行快照已复制"));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BtObservation] 无法复制运行快照: " + ex.Message);
                ShowNotification(new GUIContent("快照复制失败"));
            }
        }

        private void RefreshDisplayState(ObservationSnapshot snapshot, TreeDebugView view)
        {
            _liveNodes.Clear();
            foreach (var node in snapshot.Nodes)
            {
                _liveNodes[node.NodeId] = node;
            }

            if (_displayDefinition == null)
            {
                _displayDefinition = new ObservationSnapshotDebugView(snapshot).TreeDefinition;
            }

            // 父子映射来自定义（跳子树/面包屑用）；树定义不可变，缓存到切换实例或首次
            if (_parentOf.Count == 0 && _displayDefinition != null)
            {
                foreach (var node in _displayDefinition.Nodes)
                {
                    foreach (var childId in node.ChildIds)
                    {
                        _parentOf[childId] = node.Id;
                    }
                }
            }

            // 子树实例（内联根 -> 被引用 treeId），供节点树标记子树边界
            if (_subtreeRootTree.Count == 0 && view != null && view.SubtreeInstances != null)
            {
                foreach (var instance in view.SubtreeInstances)
                {
                    _subtreeRootTree[instance.InlinedRootNodeId] = instance.ReferencedTreeId;
                }
            }
            if (_subtreeRootTree.Count == 0)
            {
                foreach (var pair in snapshot.SourceTree)
                {
                    _subtreeRootTree[pair.Key] = pair.Value;
                }
            }

            if (string.IsNullOrEmpty(_focusNodeId) && _displayDefinition != null)
            {
                _focusNodeId = _displayDefinition.RootNodeId;
            }
        }

        private static string DescribeRootState(IReadOnlyList<NodeDebugInfo> nodes)
        {
            return nodes.Count > 0 ? nodes[0].State.ToString() : "?";
        }

        /// <summary>面包屑：根 → … → 聚焦节点，点击任意层级跳回。</summary>
        private void DrawBreadcrumb()
        {
            var definition = _displayDefinition;
            if (definition == null || string.IsNullOrEmpty(_focusNodeId)) return;

            var chain = new List<string>();
            var current = _focusNodeId;
            var guard = 0;
            while (!string.IsNullOrEmpty(current) && guard++ < 4096)
            {
                chain.Add(current);
                current = _parentOf.TryGetValue(current, out var parent) ? parent : null;
            }
            chain.Reverse();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("子树:", EditorStyles.miniLabel, GUILayout.Width(30f));
            for (var i = 0; i < chain.Count; i++)
            {
                if (i > 0) GUILayout.Label("›", EditorStyles.miniLabel);
                var nodeId = chain[i];
                var name = NodeDisplayName(nodeId);
                var isFocus = i == chain.Count - 1;
                var style = isFocus ? EditorStyles.boldLabel : EditorStyles.miniButton;
                var oldBackground = GUI.backgroundColor;
                if (!isFocus) GUI.backgroundColor = new Color(0.85f, 0.9f, 1f);
                if (GUILayout.Button(name, style, GUILayout.MaxWidth(160f)))
                {
                    _focusNodeId = nodeId;
                }
                GUI.backgroundColor = oldBackground;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private string NodeDisplayName(string nodeId)
        {
            if (_authoredMetadata.TryGetValue(nodeId, out var metadata)
                && !string.IsNullOrWhiteSpace(metadata.DisplayName)) return metadata.DisplayName;
            if (_liveNodes.TryGetValue(nodeId, out var info)
                && !string.IsNullOrEmpty(info.Name)) return info.Name;
            return nodeId;
        }

        private void DrawNodeTree()
        {
            var definition = _displayDefinition;
            if (definition == null || string.IsNullOrEmpty(_focusNodeId)) return;
            DrawNodeRecursive(definition, _focusNodeId, 0);
        }

        private void DrawNodeRecursive(TreeDefinition definition, string nodeId, int depth)
        {
            _liveNodes.TryGetValue(nodeId, out var info);
            var children = ChildIdsOf(definition, nodeId);
            if (_showRunningPathOnly && !NodeOrDescendantIsRunning(definition, nodeId)) return;
            if (info != null && _contributors.Filters.Count > 0
                && !_contributors.AnyFilterMatches(ObservationFilterContext.ForNode(
                    DisplayedSnapshot, info, DisplayedDiff))) return;
            var collapsed = _collapsedNodes.Contains(nodeId);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6 + depth * 16);

            // 折叠开关（仅父节点）
            if (children.Count > 0)
            {
                if (GUILayout.Button(collapsed ? "▸" : "▾", EditorStyles.miniButton, GUILayout.Width(18f)))
                {
                    if (collapsed) _collapsedNodes.Remove(nodeId);
                    else _collapsedNodes.Add(nodeId);
                }
            }
            else
            {
                GUILayout.Space(20f);
            }

            var state = info?.State ?? NodeState.Inactive;
            var oldColor = GUI.color;
            GUI.color = StateColor(state);
            GUILayout.Label(state.ToString().PadRight(8), EditorStyles.miniLabel, GUILayout.Width(52f));
            GUI.color = oldColor;

            var label = NodeDisplayName(nodeId);
            var display = (string.IsNullOrEmpty(info?.TypeId) ? label : label + "  [" + info.TypeId + "]");
            // 子树内联根：标记来源树（跨树边界可视化）
            if (_subtreeRootTree.TryGetValue(nodeId, out var sourceTree))
            {
                display += $"  ↳ {sourceTree}";
            }
            if (children.Count > 0)
            {
                display += collapsed ? $"  ({children.Count})" : "";
            }
            if (info != null)
            {
                var overlays = _contributors.CollectOverlays(new ObservationOverlayContext(
                    _selectedId, DisplayedSnapshot, info));
                foreach (var overlay in overlays)
                {
                    if (!string.Equals(overlay.NodeId, nodeId, System.StringComparison.Ordinal)) continue;
                    if (overlay.Kind == ObservationOverlayKind.Badge
                        || overlay.Kind == ObservationOverlayKind.Marker)
                        display += "  · " + overlay.Text;
                }
            }

            var rowStyle = (info?.OnStackCount ?? 0) > 0 ? EditorStyles.boldLabel : EditorStyles.miniLabel;
            var rect = GUILayoutUtility.GetRect(new GUIContent(display), rowStyle, GUILayout.ExpandWidth(true));
            if (string.Equals(_selectedNodeId, nodeId, System.StringComparison.Ordinal))
            {
                EditorGUI.DrawRect(rect, new Color(0.25f, 0.5f, 0.85f, 0.22f));
            }
            GUI.Label(rect, display, rowStyle);

            // 单击查看详情，双击父节点聚焦其子树；运行中的父节点显示当前子游标
            if (Event.current.type == EventType.MouseDown
                && rect.Contains(Event.current.mousePosition))
            {
                _selectedNodeId = nodeId;
                if (Event.current.clickCount == 2 && children.Count > 0)
                {
                    _focusNodeId = nodeId;
                }
                Event.current.Use();
            }
            if (children.Count > 0 && info is { RunningChildIndex: >= 0 })
            {
                GUILayout.Label("→" + (info.RunningChildIndex + 1) + "/" + children.Count, EditorStyles.miniLabel);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (collapsed) return;
            foreach (var childId in children)
            {
                DrawNodeRecursive(definition, childId, depth + 1);
            }
        }

        private bool NodeOrDescendantIsRunning(TreeDefinition definition, string nodeId)
        {
            if (_liveNodes.TryGetValue(nodeId, out var info) && info.OnStackCount > 0) return true;
            foreach (var childId in ChildIdsOf(definition, nodeId))
            {
                if (NodeOrDescendantIsRunning(definition, childId)) return true;
            }
            return false;
        }

        private void FocusFirstNodeMatch()
        {
            if (string.IsNullOrWhiteSpace(_nodeFilter)) return;
            foreach (var node in DisplayedSnapshot?.Nodes ?? System.Array.Empty<NodeDebugInfo>())
            {
                if (node.NodeId.IndexOf(_nodeFilter, System.StringComparison.OrdinalIgnoreCase) < 0
                    && NodeDisplayName(node.NodeId).IndexOf(
                        _nodeFilter, System.StringComparison.OrdinalIgnoreCase) < 0
                    && node.TypeId.IndexOf(_nodeFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                _focusNodeId = node.NodeId;
                _selectedNodeId = node.NodeId;
                return;
            }
        }

        private void DrawSelectedNodeDetail()
        {
            if (string.IsNullOrEmpty(_selectedNodeId)
                || !_liveNodes.TryGetValue(_selectedNodeId, out var info)) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(NodeDisplayName(info.NodeId), EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Node ID", info.NodeId, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Type", info.TypeId, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("State", info.State.ToString(), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Depth / Stack", info.Depth + " / " + info.OnStackCount, EditorStyles.miniLabel);
            if (info.RunningChildIndex >= 0)
                EditorGUILayout.LabelField("Running Child", (info.RunningChildIndex + 1).ToString(), EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(info.SourceTreeId))
                EditorGUILayout.LabelField("Source Tree", info.SourceTreeId, EditorStyles.miniLabel);
            if (_authoredMetadata.TryGetValue(info.NodeId, out var metadata)
                && !string.IsNullOrWhiteSpace(metadata.Comment))
            {
                EditorGUILayout.HelpBox(metadata.Comment, MessageType.None);
            }

            var sections = _contributors.CollectSections(new ObservationDetailContext(
                _selectedId, DisplayedSnapshot, info.NodeId));
            foreach (var section in sections)
            {
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField(section.Title, EditorStyles.boldLabel);
                foreach (var row in section.Rows)
                    EditorGUILayout.LabelField(row.Label, row.Value, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private static List<string> ChildIdsOf(TreeDefinition definition, string nodeId)
        {
            foreach (var node in definition.Nodes)
            {
                if (string.Equals(node.Id, nodeId, System.StringComparison.Ordinal)) return node.ChildIds;
            }
            return EmptyChildIds;
        }

        private static readonly List<string> EmptyChildIds = new();
        private static readonly List<NodeDefinition> EmptyNodes = new();

        // ------------------------------------------------------------------
        // 黑板（实时值 + 变更高亮 + 过滤）
        // ------------------------------------------------------------------

        private void DrawBlackboard(ObservationSnapshot snapshot)
        {
            GUILayout.Label("Blackboard", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("过滤", EditorStyles.miniLabel, GUILayout.Width(28f));
            _blackboardFilter = EditorGUILayout.TextField(_blackboardFilter ?? "", EditorStyles.toolbarSearchField, GUILayout.Width(200f));
            EditorGUILayout.EndHorizontal();

            var blackboard = snapshot.Blackboard;
            if (blackboard == null || blackboard.Count == 0)
            {
                EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
                return;
            }

            var diff = DisplayedDiff;
            for (var i = 0; i < blackboard.Count; i++)
            {
                var key = blackboard.KeyName(i);
                var raw = blackboard.GetDisplayValue(i);
                if (_blackboardFilter.Length > 0
                    && key.IndexOf(_blackboardFilter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (_contributors.Filters.Count > 0
                    && !_contributors.AnyFilterMatches(
                        ObservationFilterContext.ForBlackboardKey(snapshot, key, raw, diff))) continue;

                var oldColor = GUI.backgroundColor;
                if (diff?.ContainsChangedBlackboardKey(key) == true)
                    GUI.backgroundColor = new Color(1f, 0.85f, 0.45f);
                EditorGUILayout.LabelField(key, raw, EditorStyles.miniLabel);
                GUI.backgroundColor = oldColor;
            }
        }

        private void DrawEventHistory()
        {
            var timeline = _controller.Timeline;
            GUILayout.Space(6f);
            _showEventHistory = EditorGUILayout.Foldout(
                _showEventHistory, "结构化时间线 (" + timeline.Count + ")", true);
            if (!_showEventHistory) return;

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = timeline.Count > 0;
            if (GUILayout.Button("最新", EditorStyles.miniButton, GUILayout.Width(48f))) _historyIndex = -1;
            if (GUILayout.Button("上一帧", EditorStyles.miniButton, GUILayout.Width(56f)))
                _historyIndex = _historyIndex < 0 ? timeline.Count - 2 : System.Math.Max(0, _historyIndex - 1);
            if (GUILayout.Button("下一帧", EditorStyles.miniButton, GUILayout.Width(56f)))
                _historyIndex = _historyIndex < 0 ? -1 : (_historyIndex + 1 >= timeline.Count ? -1 : _historyIndex + 1);
            if (GUILayout.Button("设为 A", EditorStyles.miniButton, GUILayout.Width(52f)))
                _compareIndexA = EffectiveHistoryIndex;
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48f)))
            {
                _controller.ClearHistory();
                _historyIndex = -1;
                _compareIndexA = -1;
            }
            EditorGUILayout.EndHorizontal();

            if (_compareIndexA >= 0 && EffectiveHistoryIndex >= 0)
            {
                var compare = timeline.Compare(_compareIndexA, EffectiveHistoryIndex);
                EditorGUILayout.LabelField(
                    $"A/B: sample {_compareIndexA} → {EffectiveHistoryIndex}; "
                    + $"nodes {compare.ChangedNodeIds.Count}, keys {compare.ChangedBlackboardKeyIds.Count}",
                    EditorStyles.miniLabel);
            }

            var changes = new List<ObservationChange>(timeline.EnumerateChanges());
            _eventScroll = EditorGUILayout.BeginScrollView(_eventScroll, GUILayout.MaxHeight(150f));
            if (changes.Count == 0) EditorGUILayout.LabelField("等待下一次采样差异", EditorStyles.miniLabel);
            for (var i = changes.Count - 1; i >= 0; i--)
            {
                var item = changes[i];
                if (_contributors.Filters.Count > 0
                    && !_contributors.AnyFilterMatches(ObservationFilterContext.ForChange(item))) continue;
                EditorGUILayout.LabelField(
                    $"f{item.Frame}  {item.Kind}  {item.Target}",
                    item.From + " → " + item.To,
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private int EffectiveHistoryIndex =>
            _historyIndex >= 0 ? _historyIndex : _controller.Timeline.Count - 1;

        private ObservationSnapshot DisplayedSnapshot =>
            _offlineReplay != null
                ? _offlineReplay.Current
                : _historyIndex >= 0
                ? _controller.Timeline.SampleAt(_historyIndex)
                : _controller.Latest;

        private ObservationSnapshot DisplayedPreviousSnapshot =>
            _offlineReplay != null
                ? _offlineReplay.Previous
                : EffectiveHistoryIndex > 0
                    ? _controller.Timeline.SampleAt(EffectiveHistoryIndex - 1)
                    : null;

        private ObservationDiff DisplayedDiff =>
            _offlineReplay != null
                ? _offlineReplay.CurrentDiff
                : _historyIndex >= 0
                ? _controller.Timeline.DiffAt(_historyIndex)
                : _controller.Timeline.LatestDiff;

        private static Color StateColor(NodeState state) => state switch
        {
            NodeState.Running => new Color(0.9f, 0.8f, 0.35f),
            NodeState.Success => new Color(0.45f, 0.85f, 0.5f),
            NodeState.Failure => new Color(0.95f, 0.5f, 0.45f),
            _ => Color.gray,
        };

    }
}
