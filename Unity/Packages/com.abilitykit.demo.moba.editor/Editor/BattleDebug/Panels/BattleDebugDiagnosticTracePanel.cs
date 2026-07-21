using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugDiagnosticTracePanel :
        IBattleDebugPanel,
        IBattleDebugPanelLayout,
        IBattleDebugTraceTarget
    {
        public string Name => "Trace";
        public int Order => 405;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => true;

        private readonly BattleDebugDiagnosticTraceViewModel _viewModel =
            new BattleDebugDiagnosticTraceViewModel();
        private string _rootContextIdText = string.Empty;
        private long _pendingRootContextId;
        private long _pendingContextId;
        private Vector2 _treeScroll;
        private const float TraceRowHeight = 21f;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "诊断会话不可用。请确认战斗已启动且诊断 Local Session 已注册。",
                    MessageType.Info);
                return;
            }

            if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.Trace))
            {
                EditorGUILayout.HelpBox("当前诊断会话不支持 Trace 查询。", MessageType.Info);
                return;
            }

            if (_pendingRootContextId > 0)
            {
                _viewModel.InvalidateCache();
                _viewModel.RefreshIfNeeded(session, _pendingRootContextId);
                _pendingRootContextId = 0;
            }

            DrawToolbar(in ctx, session);
            if (_viewModel.RootContextId == 0)
            {
                EditorGUILayout.HelpBox(
                    "输入事件中的 RootContextId，然后点击“加载”。",
                    MessageType.Info);
                return;
            }

            _viewModel.RefreshIfNeeded(session, _viewModel.RootContextId);
            if (_pendingContextId > 0 && _viewModel.SelectContext(_pendingContextId))
            {
                _pendingContextId = 0;
                ScrollToSelection();
            }
            EditorGUILayout.LabelField(
                $"TraceStoreRevision={_viewModel.StoreRevision}  " +
                $"节点={_viewModel.Rows.Count}  可见={_viewModel.VisibleRows.Count}",
                EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.None);
            }

            DrawTree(in ctx);
            DrawSelectionDetails();
        }

        private void DrawToolbar(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Root Context", GUILayout.Width(80));
            _rootContextIdText = GUILayout.TextField(
                _rootContextIdText ?? string.Empty,
                GUILayout.MinWidth(100));

            var hasValidRoot = long.TryParse(_rootContextIdText, out var rootContextId) &&
                               rootContextId > 0;
            EditorGUI.BeginDisabledGroup(!hasValidRoot);
            if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _viewModel.InvalidateCache();
                _viewModel.RefreshIfNeeded(session, rootContextId);
                GUI.FocusControl(null);
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(_viewModel.RootContextId == 0);
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _viewModel.InvalidateCache();
                ctx.RequestRepaint?.Invoke();
            }
            if (GUILayout.Button("清除", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _viewModel.Clear();
                _rootContextIdText = string.Empty;
                _treeScroll = Vector2.zero;
                GUI.FocusControl(null);
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("搜索", GUILayout.Width(35));
            var searchText = GUILayout.TextField(
                _viewModel.SearchText,
                GUI.skin.textField,
                GUILayout.MinWidth(100));
            if (!string.Equals(searchText, _viewModel.SearchText, System.StringComparison.Ordinal))
            {
                _viewModel.SetSearchText(searchText);
                _treeScroll = Vector2.zero;
            }

            if (!string.IsNullOrEmpty(_viewModel.SearchText))
            {
                GUILayout.Label($"{_viewModel.SearchMatchCount} 命中", EditorStyles.miniLabel, GUILayout.Width(55));
                EditorGUI.BeginDisabledGroup(_viewModel.SearchMatchCount == 0);
                if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    _viewModel.SelectSearchMatch(-1);
                    ScrollToSelection();
                }
                if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    _viewModel.SelectSearchMatch(1);
                    ScrollToSelection();
                }
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("清除搜索", EditorStyles.toolbarButton, GUILayout.Width(65)))
                {
                    _viewModel.SetSearchText(string.Empty);
                    GUI.FocusControl(null);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(_viewModel.Rows.Count == 0 || !string.IsNullOrEmpty(_viewModel.SearchText));
            if (GUILayout.Button("全部折叠", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _viewModel.CollapseAllPreservingSelection();
                ScrollToSelection();
            }
            EditorGUI.BeginDisabledGroup(_viewModel.CollapsedBranchCount == 0);
            if (GUILayout.Button("全部展开", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _viewModel.ExpandAll();
                ScrollToSelection();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(_viewModel.SelectedContextId == 0);
            if (GUILayout.Button("Pin", EditorStyles.toolbarButton, GUILayout.Width(38)))
            {
                _viewModel.PinSelection();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!_viewModel.IsPinnedContextAvailable);
            if (GUILayout.Button("返回 Pin", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _viewModel.SelectPinned();
                ScrollToSelection();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(_viewModel.PinnedContextId == 0);
            if (GUILayout.Button("清除 Pin", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _viewModel.ClearPin();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTree(in BattleDebugContext ctx)
        {
            EditorGUILayout.LabelField("Trace Tree", EditorStyles.boldLabel);
            _treeScroll = EditorGUILayout.BeginScrollView(
                _treeScroll,
                GUILayout.MinHeight(180),
                GUILayout.MaxHeight(360));

            var rows = _viewModel.VisibleRows;
            if (rows.Count == 0)
            {
                EditorGUILayout.LabelField(
                    string.IsNullOrEmpty(_viewModel.SearchText) ? "（无节点）" : "（无匹配节点）",
                    EditorStyles.miniLabel);
            }
            else
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    DrawTraceRow(in ctx, rows[i]);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTraceRow(
            in BattleDebugContext ctx,
            in BattleDebugDiagnosticTraceRow row)
        {
            var node = row.Node;
            var selected = node.ContextId == _viewModel.SelectedContextId;
            var pinned = node.ContextId == _viewModel.PinnedContextId;
            var searchMatch = _viewModel.IsSearchMatch(node.ContextId);
            var hasChildren = _viewModel.HasChildren(node.ContextId);
            var oldColor = GUI.color;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(row.Depth * 16f);
            EditorGUI.BeginDisabledGroup(!hasChildren || !string.IsNullOrEmpty(_viewModel.SearchText));
            if (GUILayout.Button(
                    hasChildren ? (_viewModel.IsCollapsed(node.ContextId) ? "+" : "-") : string.Empty,
                    EditorStyles.miniButton,
                    GUILayout.Width(20),
                    GUILayout.Height(20)))
            {
                _viewModel.ToggleCollapsed(node.ContextId);
            }
            EditorGUI.EndDisabledGroup();

            GUI.color = searchMatch
                ? new Color(1f, 0.9f, 0.45f)
                : GetStateColor(node.State);
            var label = (pinned ? "[PIN] " : string.Empty) + BuildNodeLabel(in row);
            var style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
            if (GUILayout.Button(label, style, GUILayout.Height(20)))
            {
                _viewModel.SelectContext(node.ContextId);
                ctx.RequestRepaint?.Invoke();
            }
            GUI.color = oldColor;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectionDetails()
        {
            var path = _viewModel.SelectedPath;
            if (path.Count == 0) return;

            var selected = path[path.Count - 1];
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Context", selected.ContextId.ToString());
            EditorGUILayout.LabelField("Parent", selected.ParentContextId.ToString());
            EditorGUILayout.LabelField("Kind", selected.Kind);
            EditorGUILayout.LabelField("State", selected.State.ToString());
            EditorGUILayout.LabelField(
                "Frames",
                selected.EndFrame >= 0
                    ? $"{selected.StartFrame} -> {selected.EndFrame}"
                    : $"{selected.StartFrame} -> active");
            EditorGUILayout.LabelField("Actor / Config", $"{selected.ActorId} / {selected.ConfigId}");
            if (_viewModel.PinnedContextId != 0)
            {
                EditorGUILayout.LabelField(
                    "Pinned Context",
                    _viewModel.IsPinnedContextAvailable
                        ? _viewModel.PinnedContextId.ToString()
                        : $"{_viewModel.PinnedContextId}（当前 Trace 中不可用）");
            }
            if (!string.IsNullOrEmpty(selected.EndReason))
            {
                EditorGUILayout.LabelField("End Reason", selected.EndReason);
            }

            EditorGUILayout.LabelField("Root Path", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                BuildPathText(path),
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        public void OpenTrace(long rootContextId, long contextId)
        {
            if (rootContextId <= 0) return;

            _rootContextIdText = rootContextId.ToString();
            _pendingRootContextId = rootContextId;
            _pendingContextId = contextId > 0 ? contextId : rootContextId;
            _treeScroll = Vector2.zero;
            _viewModel.Clear();
        }

        private void ScrollToSelection()
        {
            var rowIndex = _viewModel.GetVisibleRowIndex(_viewModel.SelectedContextId);
            if (rowIndex >= 0)
            {
                _treeScroll.y = Mathf.Max(0f, rowIndex * TraceRowHeight - TraceRowHeight * 2f);
            }
        }

        private static string BuildNodeLabel(in BattleDebugDiagnosticTraceRow row)
        {
            var node = row.Node;
            var orphan = row.IsOrphan ? " [orphan]" : string.Empty;
            var actor = node.ActorId != 0 ? $"  actor={node.ActorId}" : string.Empty;
            var config = node.ConfigId != 0 ? $"  config={node.ConfigId}" : string.Empty;
            return $"{node.Kind}  #{node.ContextId}  [{node.State}]{actor}{config}{orphan}";
        }

        private static string BuildPathText(
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticTraceNodeSummary> path)
        {
            var parts = new string[path.Count];
            for (var i = 0; i < path.Count; i++)
            {
                var node = path[i];
                parts[i] = $"{node.Kind}#{node.ContextId}";
            }

            return string.Join(" > ", parts);
        }

        private static Color GetStateColor(BattleDiagnosticTraceNodeState state)
        {
            switch (state)
            {
                case BattleDiagnosticTraceNodeState.Active:
                    return new Color(0.65f, 0.9f, 1f);
                case BattleDiagnosticTraceNodeState.Failed:
                    return new Color(1f, 0.55f, 0.55f);
                case BattleDiagnosticTraceNodeState.ForceEnded:
                    return new Color(1f, 0.8f, 0.45f);
                default:
                    return Color.white;
            }
        }
    }
}
