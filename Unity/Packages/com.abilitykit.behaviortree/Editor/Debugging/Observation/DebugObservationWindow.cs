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
        private readonly ObservationController _controller = new();
        private readonly ObservationContributorRegistry _contributors =
            ObservationContributorRegistry.Default;

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private Vector2 _eventScroll;
        private TreeDefinition _displayDefinition;
        private string _instanceFilter = "";
        private object _autoOpenedFor;
        private string _blackboardFilter = "";
        private long _selectedId;
        private bool _showEventHistory = true;
        private int _historyIndex = -1;
        private int _compareIndexA = -1;
        private double _sampleIntervalSeconds = ObservationSettings.DefaultSampleIntervalSeconds;
        private int _timelineCapacity = ObservationSettings.DefaultTimelineCapacity;
        private ObservationOfflineReplay _offlineReplay;
        private string _lastRecordingPath = "";
        private double _lastReplayTickSeconds;

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
            _displayDefinition = null;
            _liveNodes.Clear();
            _historyIndex = -1;
            _compareIndexA = -1;
        }

        private void PrepareSelectedView(TreeDebugView view)
        {
            if (_displayDefinition != null) return;
            _displayDefinition = view.TreeDefinition;
        }

        private void CaptureSelected(TreeDebugView view)
        {
            PrepareSelectedView(view);
            if (_controller.Latest == null) _controller.Sample();
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
            GUILayout.Label("行为树运行时观察", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(_offlineReplay != null ? "离线回放" : EditorDisplayText.ObservationState(_controller.State), EditorStyles.miniLabel);
            GUILayout.Label(_entries.Count + " 个实例", EditorStyles.miniLabel);
            GUILayout.Label("间隔", EditorStyles.miniLabel);
            var interval = EditorGUILayout.DoubleField(_sampleIntervalSeconds, GUILayout.Width(52f));
            GUILayout.Label("容量", EditorStyles.miniLabel);
            var capacity = EditorGUILayout.IntField(_timelineCapacity, GUILayout.Width(58f));
            ApplyToolbarSettings(interval, capacity);
            if (GUILayout.Button(_controller.Paused ? "继续刷新" : "冻结视图", EditorStyles.toolbarButton))
            {
                if (_controller.Paused) _controller.Resume();
                else _controller.Pause();
            }
            if (GUILayout.Button(_controller.Paused ? "单步采样" : "立即刷新", EditorStyles.toolbarButton))
            {
                _controller.Sample();
                _historyIndex = -1;
            }
            if (GUILayout.Button("导出记录", EditorStyles.toolbarButton)) ExportRecording();
            if (GUILayout.Button("导入回放", EditorStyles.toolbarButton)) ImportReplay();
            if (_offlineReplay != null && GUILayout.Button("返回实时", EditorStyles.toolbarButton))
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
                "导出行为树观察记录",
                directory,
                "bt-observation-recording",
                "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                ObservationRecording.ExportToFile(path, _controller.Timeline, _controller);
                _lastRecordingPath = path;
                ShowNotification(new GUIContent("观察记录已导出"));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BtObservation] 观察记录导出失败：" + ex.Message);
                ShowNotification(new GUIContent("观察记录导出失败"));
            }
        }

        private void ImportReplay()
        {
            var directory = string.IsNullOrEmpty(_lastRecordingPath)
                ? ""
                : System.IO.Path.GetDirectoryName(_lastRecordingPath);
            var path = EditorUtility.OpenFilePanel(
                "导入行为树观察记录",
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
                ShowNotification(new GUIContent("观察回放已加载"));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BtObservation] 观察记录导入失败：" + ex.Message);
                ShowNotification(new GUIContent("观察记录导入失败"));
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
                    "当前没有已登记的运行中行为树。\n" +
                    "创建行为树实例时，请在 TreeRunOptions 中设置非空 DebugName 以启用观察。",
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
                $"帧 {snapshot.Frame}   节点 {snapshot.NodeCount}   状态 {DescribeRootState(nodes)}"
                + (_controller.Paused ? "   [视图已冻结]" : "")
                + (_controller.State == ObservationSessionState.Disconnected ? "   [已断开]" : "")
                + (_historyIndex >= 0 ? "   [历史采样]" : ""),
                EditorStyles.miniLabel);

            // 选中运行实例后，自动打开节点图（GraphView 观察模式）作为主视图；
            // 本面板保留黑板与事件时间线，节点树结构交由节点图展示。
            if (view != null && !ReferenceEquals(_autoOpenedFor, view))
            {
                _autoOpenedFor = view;
                AuthoringGraphWindow.OpenObservation(view);
            }

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
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            EditorGUILayout.HelpBox(
                "节点树结构在“行为树观察图”窗口中展示。此面板保留黑板与事件时间线。",
                MessageType.Info);

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
                EditorGUILayout.HelpBox("导入的观察记录中没有采样。", MessageType.Info);
                return;
            }
            RefreshDisplayState(snapshot, null);

            EditorGUILayout.LabelField(
                $"离线回放  {snapshot.DisplayName}  ({snapshot.TreeId})",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"采样 {_offlineReplay.CurrentIndex + 1}/{_offlineReplay.Count}   帧 {snapshot.Frame}   节点 {snapshot.NodeCount}",
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_offlineReplay.IsPlaying ? "暂停" : "播放", EditorStyles.miniButton, GUILayout.Width(56f)))
                _offlineReplay.TogglePlayback();
            GUI.enabled = _offlineReplay.CurrentIndex > 0;
            if (GUILayout.Button("上一个", EditorStyles.miniButton, GUILayout.Width(72f))) _offlineReplay.StepPrevious();
            GUI.enabled = _offlineReplay.CurrentIndex + 1 < _offlineReplay.Count;
            if (GUILayout.Button("下一个", EditorStyles.miniButton, GUILayout.Width(72f))) _offlineReplay.StepNext();
            GUI.enabled = _offlineReplay.Count > 0;
            if (GUILayout.Button("最新", EditorStyles.miniButton, GUILayout.Width(72f))) _offlineReplay.JumpToLatest();
            GUI.enabled = true;
            GUILayout.Label("速度", EditorStyles.miniLabel, GUILayout.Width(42f));
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
            if (GUILayout.Button("设为 A", EditorStyles.miniButton, GUILayout.Width(52f))) _offlineReplay.MarkCompareA();
            if (GUILayout.Button("设为 B", EditorStyles.miniButton, GUILayout.Width(52f))) _offlineReplay.MarkCompareB();
            if (GUILayout.Button("查看图", EditorStyles.miniButton, GUILayout.Width(56f)))
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
                    $"A/B：采样 {_offlineReplay.CompareIndexA} -> {_offlineReplay.CompareIndexB}；"
                    + $"节点 {compare.ChangedNodeIds.Count}，黑板键 {compare.ChangedBlackboardKeyIds.Count}",
                    EditorStyles.miniLabel);
            }

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            GUILayout.Label("节点", EditorStyles.boldLabel);
            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                var node = snapshot.Nodes[i];
                var oldColor = GUI.color;
                GUI.color = StateColor(node.State);
                EditorGUILayout.LabelField(
                    node.NodeId,
                    EditorDisplayText.NodeState(node.State) + "  " + node.TypeId + (node.OnStackCount > 0 ? "  执行中" : ""),
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
            GUILayout.Label("回放时间线（" + timeline.Count + "）", EditorStyles.boldLabel);
            var changes = new List<ObservationChange>(timeline.EnumerateChanges());
            _eventScroll = EditorGUILayout.BeginScrollView(_eventScroll, GUILayout.MaxHeight(150f));
            if (changes.Count == 0) EditorGUILayout.LabelField("没有记录到变化。", EditorStyles.miniLabel);
            for (var i = changes.Count - 1; i >= 0; i--)
            {
                var item = changes[i];
                EditorGUILayout.LabelField(
                    $"帧 {item.Frame}  {EditorDisplayText.ChangeKind(item.Kind)}  {item.Target}",
                    EditorDisplayText.ChangeValue(item.Kind, item.From) + " -> " + EditorDisplayText.ChangeValue(item.Kind, item.To),
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

        }

        private static string DescribeRootState(IReadOnlyList<NodeDebugInfo> nodes)
        {
            return nodes.Count > 0 ? EditorDisplayText.NodeState(nodes[0].State) : "?";
        }



        private static readonly List<NodeDefinition> EmptyNodes = new();

        // ------------------------------------------------------------------
        // 黑板（实时值 + 变更高亮 + 过滤）
        // ------------------------------------------------------------------

        private void DrawBlackboard(ObservationSnapshot snapshot)
        {
            GUILayout.Label("黑板", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("过滤", EditorStyles.miniLabel, GUILayout.Width(28f));
            _blackboardFilter = EditorGUILayout.TextField(_blackboardFilter ?? "", EditorStyles.toolbarSearchField, GUILayout.Width(200f));
            EditorGUILayout.EndHorizontal();

            var blackboard = snapshot.Blackboard;
            if (blackboard == null || blackboard.Count == 0)
            {
                EditorGUILayout.LabelField("（空）", EditorStyles.miniLabel);
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
                _showEventHistory, "结构化时间线（" + timeline.Count + "）", true);
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
                    $"A/B：采样 {_compareIndexA} → {EffectiveHistoryIndex}；"
                    + $"节点 {compare.ChangedNodeIds.Count}，黑板键 {compare.ChangedBlackboardKeyIds.Count}",
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
                    $"帧 {item.Frame}  {EditorDisplayText.ChangeKind(item.Kind)}  {item.Target}",
                    EditorDisplayText.ChangeValue(item.Kind, item.From) + " → " + EditorDisplayText.ChangeValue(item.Kind, item.To),
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
            NodeState.Faulted => new Color(0.95f, 0.3f, 0.2f),
            _ => Color.gray,
        };

    }
}
