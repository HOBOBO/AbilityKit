using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 运行时观察窗口：轮询 <see cref="BtDebugRegistry"/> 已登记实例，**主动拉取**节点状态、
    /// 运行路径与黑板值。左栏实例列表按树配置分组、以注册序号区分；选中实例后：
    /// 节点树可折叠、双击组合/装饰节点跳转子树（面包屑回跳），黑板值变更高亮并可过滤，
    /// 可一键"在图中查看"把实时状态着色到 GraphView 画布。运行时（逻辑侧/服务端/console）
    /// 不引用任何编辑器类型；本窗口是纯观察者，不修改任何运行时结构。
    /// </summary>
    public sealed class BtDebugObservationWindow : EditorWindow
    {
        private const double PollIntervalSeconds = 0.2d;
        private const float LeftPaneWidth = 280f;

        private readonly List<BtDebugRegistryEntry> _entries = new();
        private readonly Dictionary<string, BtNodeDebugInfo> _liveNodes = new();
        private readonly Dictionary<string, string> _lastBlackboardDisplay = new();
        private readonly HashSet<string> _blackboardChanged = new();
        private readonly HashSet<string> _collapsedNodes = new();
        private readonly Dictionary<string, string> _parentOf = new();
        private readonly Dictionary<string, string> _subtreeRootTree = new();

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private string _blackboardFilter = "";
        private long _selectedId;
        private string _focusNodeId = "";
        private double _nextPollAt;

        [MenuItem("Window/AbilityKit/Behavior Tree Observation")]
        private static void Open()
        {
            var window = GetWindow<BtDebugObservationWindow>();
            window.titleContent = new GUIContent("BT Observation");
            window.minSize = new Vector2(640f, 420f);
        }

        private void OnGUI()
        {
            PollIfNeeded();

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
            if (EditorApplication.timeSinceStartup < _nextPollAt) return;
            Poll();
        }

        private void Poll()
        {
            _nextPollAt = EditorApplication.timeSinceStartup + PollIntervalSeconds;
            _entries.Clear();
            _entries.AddRange(BtDebugRegistry.GetEntries());

            if (_selectedId != 0 && _entries.Exists(e => e.Id == _selectedId)) return;

            // 选中实例已注销：清空观察态
            _selectedId = _entries.Count > 0 ? _entries[0].Id : 0;
            _focusNodeId = "";
            _collapsedNodes.Clear();
            _parentOf.Clear();
            _subtreeRootTree.Clear();
            _liveNodes.Clear();
            _lastBlackboardDisplay.Clear();
        }

        private IBtTreeDebugView SelectedView
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
            GUILayout.Label(_entries.Count + " instance(s)", EditorStyles.miniLabel);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) _nextPollAt = 0d;
            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------
        // 左栏：实例列表（树配置分组，注册序号区分）
        // ------------------------------------------------------------------

        private void DrawInstanceList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(LeftPaneWidth));
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No running behavior trees registered.\n" +
                    "A tree registers itself when created with a non-empty DebugName (BtTreeRunOptions).",
                    MessageType.Info);
            }

            string currentGroup = null;
            foreach (var entry in _entries)
            {
                var view = entry.View;
                if (view == null) continue;

                if (!string.Equals(currentGroup, view.TreeId, System.StringComparison.Ordinal))
                {
                    currentGroup = view.TreeId;
                    GUILayout.Space(4f);
                    GUILayout.Label(currentGroup, EditorStyles.boldLabel);
                }

                var label = "#" + entry.Id
                            + (string.IsNullOrEmpty(view.OwnerLabel) ? "" : "  " + view.OwnerLabel);
                var oldBackground = GUI.backgroundColor;
                if (entry.Id == _selectedId) GUI.backgroundColor = new Color(0.42f, 0.66f, 0.92f);
                if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(22f)))
                {
                    if (_selectedId != entry.Id)
                    {
                        _selectedId = entry.Id;
                        _focusNodeId = "";
                        _collapsedNodes.Clear();
                        _parentOf.Clear();
                        _subtreeRootTree.Clear();
                        _liveNodes.Clear();
                        _lastBlackboardDisplay.Clear();
                    }
                }
                GUI.backgroundColor = oldBackground;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ------------------------------------------------------------------
        // 右栏：选中实例详情
        // ------------------------------------------------------------------

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var view = SelectedView;
            if (view == null)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            var nodes = view.GetNodeStates();
            RefreshLiveState(view, nodes);

            EditorGUILayout.LabelField(
                $"#{_selectedId}  {view.DisplayName}  ({view.TreeId})"
                + (string.IsNullOrEmpty(view.OwnerLabel) ? "" : $"  —  {view.OwnerLabel}"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"frame {view.LastFrame}   nodes {view.NodeCount}   state {DescribeRootState(nodes)}",
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("在图中查看", EditorStyles.miniButton))
            {
                BtAuthoringGraphWindow.OpenObservation(view);
            }
            if (GUILayout.Button("全部展开", EditorStyles.miniButton)) _collapsedNodes.Clear();
            if (GUILayout.Button("全部收起", EditorStyles.miniButton))
            {
                foreach (var pair in _parentOf)
                {
                    _collapsedNodes.Add(pair.Key);
                }
            }
            EditorGUILayout.EndHorizontal();

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            DrawBreadcrumb(view);
            GUILayout.Space(2f);
            DrawNodeTree(view, nodes);

            GUILayout.Space(6f);
            DrawBlackboard(view.GetBlackboard());

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void RefreshLiveState(IBtTreeDebugView view, List<BtNodeDebugInfo> nodes)
        {
            _liveNodes.Clear();
            foreach (var node in nodes)
            {
                _liveNodes[node.NodeId] = node;
            }

            // 父子映射来自定义（跳子树/面包屑用）；树定义不可变，缓存到切换实例或首次
            if (_parentOf.Count == 0 && view.TreeDefinition != null)
            {
                foreach (var node in view.TreeDefinition.Nodes)
                {
                    foreach (var childId in node.ChildIds)
                    {
                        _parentOf[childId] = node.Id;
                    }
                }
            }

            // 子树实例（内联根 -> 被引用 treeId），供节点树标记子树边界
            if (_subtreeRootTree.Count == 0 && view.SubtreeInstances != null)
            {
                foreach (var instance in view.SubtreeInstances)
                {
                    _subtreeRootTree[instance.InlinedRootNodeId] = instance.ReferencedTreeId;
                }
            }

            if (string.IsNullOrEmpty(_focusNodeId) && view.TreeDefinition != null)
            {
                _focusNodeId = view.TreeDefinition.RootNodeId;
            }
        }

        private static string DescribeRootState(List<BtNodeDebugInfo> nodes)
        {
            return nodes.Count > 0 ? nodes[0].State.ToString() : "?";
        }

        /// <summary>面包屑：根 → … → 聚焦节点，点击任意层级跳回。</summary>
        private void DrawBreadcrumb(IBtTreeDebugView view)
        {
            var definition = view.TreeDefinition;
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
            if (_liveNodes.TryGetValue(nodeId, out var info)
                && !string.IsNullOrEmpty(info.Name)) return info.Name;
            return nodeId;
        }

        private void DrawNodeTree(IBtTreeDebugView view, List<BtNodeDebugInfo> nodes)
        {
            var definition = view.TreeDefinition;
            if (definition == null || string.IsNullOrEmpty(_focusNodeId)) return;

            var childIds = ChildIdsOf(definition, _focusNodeId);
            foreach (var childId in childIds)
            {
                DrawNodeRecursive(definition, childId, 0);
            }
        }

        private void DrawNodeRecursive(BtTreeDefinition definition, string nodeId, int depth)
        {
            _liveNodes.TryGetValue(nodeId, out var info);
            var children = ChildIdsOf(definition, nodeId);
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

            var state = info?.State ?? BtNodeState.Inactive;
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

            var rowStyle = (info?.OnStackCount ?? 0) > 0 ? EditorStyles.boldLabel : EditorStyles.miniLabel;
            var rect = GUILayoutUtility.GetRect(new GUIContent(display), rowStyle, GUILayout.ExpandWidth(true));
            GUI.Label(rect, display, rowStyle);

            // 双击父节点跳转子树；运行中的父节点显示当前子游标
            if (Event.current.type == EventType.MouseDown
                && Event.current.clickCount == 2 && rect.Contains(Event.current.mousePosition))
            {
                if (children.Count > 0)
                {
                    _focusNodeId = nodeId;
                    Event.current.Use();
                }
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

        private static List<string> ChildIdsOf(BtTreeDefinition definition, string nodeId)
        {
            foreach (var node in definition.Nodes)
            {
                if (string.Equals(node.Id, nodeId, System.StringComparison.Ordinal)) return node.ChildIds;
            }
            return EmptyChildIds;
        }

        private static readonly List<string> EmptyChildIds = new();

        // ------------------------------------------------------------------
        // 黑板（实时值 + 变更高亮 + 过滤）
        // ------------------------------------------------------------------

        private void DrawBlackboard(BtBlackboardValueSnapshot snapshot)
        {
            GUILayout.Label("Blackboard", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("过滤", EditorStyles.miniLabel, GUILayout.Width(28f));
            _blackboardFilter = EditorGUILayout.TextField(_blackboardFilter ?? "", EditorStyles.toolbarSearchField, GUILayout.Width(200f));
            EditorGUILayout.EndHorizontal();

            if (snapshot == null || snapshot.KeyNames == null || snapshot.KeyNames.Count == 0)
            {
                EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
                return;
            }

            var current = new Dictionary<string, string>();
            for (var i = 0; i < snapshot.KeyNames.Count; i++)
            {
                var raw = FormatValue(snapshot, i);
                current[snapshot.KeyNames[i]] = raw;
                if (_blackboardFilter.Length > 0
                    && snapshot.KeyNames[i].IndexOf(_blackboardFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var changed = _blackboardChanged.Contains(snapshot.KeyNames[i]);
                var oldColor = GUI.backgroundColor;
                if (changed) GUI.backgroundColor = new Color(1f, 0.85f, 0.45f);
                EditorGUILayout.LabelField(snapshot.KeyNames[i], raw, EditorStyles.miniLabel);
                GUI.backgroundColor = oldColor;
            }

            // 本帧值作为下一次比较基线；变更集合只保留一轮
            _blackboardChanged.Clear();
            foreach (var pair in current)
            {
                if (_lastBlackboardDisplay.TryGetValue(pair.Key, out var previous)
                    && !string.Equals(previous, pair.Value, System.StringComparison.Ordinal))
                {
                    _blackboardChanged.Add(pair.Key);
                }
            }
            _lastBlackboardDisplay.Clear();
            foreach (var pair in current)
            {
                _lastBlackboardDisplay[pair.Key] = pair.Value;
            }
        }

        private static string FormatValue(BtBlackboardValueSnapshot snapshot, int index)
        {
            return snapshot.KeyTypes[index] switch
            {
                BtValueType.Bool => snapshot.BoolValues[index].ToString(),
                BtValueType.Int64 => snapshot.Int64Values[index].ToString(),
                BtValueType.Fixed64 => AbilityKit.Deterministic.Fixed64.FromRaw(snapshot.Fixed64RawValues[index]).ToString(),
                BtValueType.String => snapshot.StringValues[index],
                _ => "?",
            };
        }

        private static Color StateColor(BtNodeState state) => state switch
        {
            BtNodeState.Running => new Color(0.9f, 0.8f, 0.35f),
            BtNodeState.Success => new Color(0.45f, 0.85f, 0.5f),
            BtNodeState.Failure => new Color(0.95f, 0.5f, 0.45f),
            _ => Color.gray,
        };
    }
}
