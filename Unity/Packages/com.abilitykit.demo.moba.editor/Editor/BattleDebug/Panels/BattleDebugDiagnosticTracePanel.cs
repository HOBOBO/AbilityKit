using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    [BattleDebugModule(
        BattleDebugModuleIds.DiagnosticTrace,
        "Investigation",
        RequiredCapabilities = BattleDiagnosticCapabilities.Trace,
        Selections = BattleDebugModuleSelectionSupport.Frame |
                     BattleDebugModuleSelectionSupport.Actor |
                     BattleDebugModuleSelectionSupport.Event |
                     BattleDebugModuleSelectionSupport.Trace |
                     BattleDebugModuleSelectionSupport.Config)]
    internal sealed class BattleDebugDiagnosticTracePanel :
        IBattleDebugPanel,
        IBattleDebugPanelLayout,
        IBattleDebugTraceTarget,
        IBattleDebugWidgetProvider
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
        private Vector2 _waterfallScroll;
        private const float TraceRowHeight = 24f;
        private const float TreeIndentWidth = 14f;
        private const float FoldoutWidth = 18f;
        private const float StateRailWidth = 4f;
        private readonly IBattleDebugWidget[] _widgets;
        private readonly List<BattleDebugWaterfallItem> _waterfallItems =
            new List<BattleDebugWaterfallItem>(256);
        private readonly List<BattleDebugTimelineOverviewItem> _overviewItems =
            new List<BattleDebugTimelineOverviewItem>(256);
        private readonly BattleDebugTimelineOverviewBuffer _overviewBuffer =
            new BattleDebugTimelineOverviewBuffer();
        private readonly BattleDebugLegendItem[] _waterfallLegend =
        {
            new BattleDebugLegendItem("Active", new Color(0.65f, 0.9f, 1f)),
            new BattleDebugLegendItem("Failed", new Color(1f, 0.55f, 0.55f)),
            new BattleDebugLegendItem("Force Ended", new Color(1f, 0.8f, 0.45f)),
            new BattleDebugLegendItem("Ended", new Color(0.48f, 0.76f, 0.52f))
        };

        public BattleDebugDiagnosticTracePanel()
        {
            _widgets = new IBattleDebugWidget[]
            {
                new TraceWidget(this, TraceWidgetKind.Tree),
                new TraceWidget(this, TraceWidgetKind.Waterfall),
                new TraceWidget(this, TraceWidgetKind.Details)
            };
        }

        public IReadOnlyList<IBattleDebugWidget> Widgets => _widgets;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "诊断会话不可用。请启动战斗或打开包含 Battle Diagnostics 的 Artifact。",
                    MessageType.Info);
                return;
            }

            if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.Trace))
            {
                var unsupported = BattleDiagnosticQueryStatus.Unavailable(
                    0,
                    session.TraceStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported);
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    in unsupported,
                    subject: "Trace"));
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
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    default,
                    requiresSelection: true,
                    hasSelection: false,
                    subject: "Trace 树",
                    selectionSubject: "Root Context"));
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

            if (_viewModel.Rows.Count == 0)
            {
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    _viewModel.QueryStatus,
                    hasActiveFilter: !string.IsNullOrEmpty(_viewModel.SearchText),
                    subject: "Trace 节点"));
            }
            else if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.Warning);
            }

            DrawFlowSummary();
            DrawTree(in ctx);
            DrawSelectionDetails(in ctx);
        }

        private void DrawTreeWidget(in BattleDebugContext ctx)
        {
            if (!TryPrepareWidget(in ctx, drawToolbar: true)) return;
            DrawTree(in ctx);
        }

        private void DrawWaterfallWidget(in BattleDebugContext ctx)
        {
            if (!TryPrepareWidget(in ctx, drawToolbar: true)) return;
            DrawWaterfall(in ctx);
        }

        private void DrawDetailsWidget(in BattleDebugContext ctx)
        {
            if (!TryPrepareWidget(in ctx, drawToolbar: false)) return;
            if (_viewModel.SelectedPath.Count == 0)
            {
                EditorGUILayout.HelpBox("Select a node from Trace Tree or Waterfall.", MessageType.Info);
                return;
            }

            DrawSelectionDetails(in ctx);
        }

        private bool TryPrepareWidget(in BattleDebugContext ctx, bool drawToolbar)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "Battle Diagnostics session is unavailable.",
                    MessageType.Info);
                return false;
            }

            if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.Trace))
            {
                var unsupported = BattleDiagnosticQueryStatus.Unavailable(
                    0,
                    session.TraceStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported);
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    in unsupported,
                    subject: "Trace"));
                return false;
            }

            if (_pendingRootContextId > 0)
            {
                _viewModel.InvalidateCache();
                _viewModel.RefreshIfNeeded(session, _pendingRootContextId);
                _pendingRootContextId = 0;
            }

            if (drawToolbar)
            {
                DrawToolbar(in ctx, session);
            }
            if (_viewModel.RootContextId == 0)
            {
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    default,
                    requiresSelection: true,
                    hasSelection: false,
                    subject: "Trace root",
                    selectionSubject: "Root Context"));
                return false;
            }

            _viewModel.RefreshIfNeeded(session, _viewModel.RootContextId);
            if (_pendingContextId > 0 && _viewModel.SelectContext(_pendingContextId))
            {
                _pendingContextId = 0;
                ScrollToSelection();
            }
            EditorGUILayout.LabelField(
                $"TraceStoreRevision={_viewModel.StoreRevision}  " +
                $"Nodes={_viewModel.Rows.Count}  Visible={_viewModel.VisibleRows.Count}",
                EditorStyles.miniLabel);

            if (_viewModel.Rows.Count == 0)
            {
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    _viewModel.QueryStatus,
                    hasActiveFilter: !string.IsNullOrEmpty(_viewModel.SearchText),
                    subject: "Trace nodes"));
                return false;
            }
            if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.Warning);
            }

            DrawFlowSummary();
            return true;
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

            var focusSelectedFlow = GUILayout.Toggle(
                _viewModel.FocusSelectedFlow,
                "聚焦所选流程",
                EditorStyles.toolbarButton,
                GUILayout.Width(96));
            if (focusSelectedFlow != _viewModel.FocusSelectedFlow)
            {
                _viewModel.SetFocusSelectedFlow(focusSelectedFlow);
                ScrollToSelection();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFlowSummary()
        {
            var summary = _viewModel.Summary;
            if (summary.NodeCount == 0) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Root #{_viewModel.RootContextId}", EditorStyles.miniBoldLabel, GUILayout.Width(100));
            GUILayout.Label($"节点 {summary.NodeCount}", EditorStyles.miniLabel, GUILayout.Width(52));
            GUILayout.Label($"效果 {summary.EffectCount}", EditorStyles.miniLabel, GUILayout.Width(52));
            GUILayout.Label($"动作 {summary.ActionCount}", EditorStyles.miniLabel, GUILayout.Width(52));
            var oldColor = GUI.color;
            if (summary.IssueCount > 0) GUI.color = new Color(1f, 0.62f, 0.58f);
            GUILayout.Label($"异常 {summary.IssueCount}", EditorStyles.miniBoldLabel, GUILayout.Width(52));
            GUI.color = oldColor;
            if (summary.ActiveCount > 0)
            {
                GUI.color = new Color(0.65f, 0.9f, 1f);
                GUILayout.Label($"活跃 {summary.ActiveCount}", EditorStyles.miniBoldLabel, GUILayout.Width(52));
                GUI.color = oldColor;
            }
            GUILayout.Label($"最大深度 {summary.MaximumDepth}", EditorStyles.miniLabel, GUILayout.Width(72));
            GUILayout.FlexibleSpace();
            GUILayout.Label(BuildSummaryFrameText(in summary), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTree(in BattleDebugContext ctx)
        {
            EditorGUILayout.LabelField("Trace Tree", EditorStyles.boldLabel);
            _treeScroll = EditorGUILayout.BeginScrollView(
                _treeScroll,
                GUILayout.MinHeight(180),
                GUILayout.MaxHeight(360));

            DrawTraceHeader();

            var rows = _viewModel.VisibleRows;
            if (rows.Count == 0 && _viewModel.Rows.Count > 0)
            {
                var filteredEmpty = BattleDiagnosticQueryStatus.Ready(
                    0,
                    _viewModel.StoreRevision,
                    0,
                    false);
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    in filteredEmpty,
                    hasActiveFilter: true,
                    subject: "Trace 节点"));
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

        private void DrawWaterfall(in BattleDebugContext ctx)
        {
            EditorGUILayout.LabelField("Trace Frame Waterfall", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Bars represent frame spans, not CPU duration.",
                EditorStyles.miniLabel);

            var rows = _viewModel.Rows;
            if (rows.Count == 0) return;

            var cursorFrame = ctx.WorkspaceState?.FrameCursor.Frame ?? BattleDiagnosticFrames.Invalid;
            var automaticRange = ResolveTraceRange(rows, cursorFrame);
            var visibleRange = ctx.WorkspaceState != null
                ? ctx.WorkspaceState.TimeRange.Resolve(automaticRange)
                : automaticRange;
            _waterfallItems.Clear();
            _overviewItems.Clear();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var node = row.Node;
                var effectiveEnd = node.EndFrame >= 0
                    ? node.EndFrame
                    : BattleDiagnosticFrames.IsValid(cursorFrame)
                        ? Mathf.Max(cursorFrame, node.StartFrame)
                        : node.StartFrame;
                _waterfallItems.Add(new BattleDebugWaterfallItem(
                    node.ContextId,
                    $"{node.Kind} #{node.ContextId}",
                    $"Source={node.SourceActorId}, Target={node.TargetActorId}, " +
                    $"Config={node.ConfigId}, Trigger={node.TriggerId}\n" +
                    $"State={BuildResultText(in node)}\n" +
                    $"F{node.StartFrame} -> " +
                    (node.EndFrame >= 0 ? $"F{node.EndFrame}" : "active"),
                    node.StartFrame,
                    effectiveEnd,
                    row.Depth,
                    node.ContextId == _viewModel.SelectedContextId,
                    GetStateColor(node.State)));
                _overviewItems.Add(new BattleDebugTimelineOverviewItem(
                    node.StartFrame,
                    effectiveEnd));
            }

            var overviewInteraction = BattleDebugTimelineOverview.Draw(
                _overviewItems,
                automaticRange,
                visibleRange,
                cursorFrame,
                _overviewBuffer);
            ApplyTimelineInteraction(in ctx, overviewInteraction);

            var waterfallResult = BattleDebugWaterfall.Draw(
                _waterfallItems,
                visibleRange,
                cursorFrame,
                ref _waterfallScroll);
            BattleDebugLegend.Draw(_waterfallLegend);
            ApplyTimelineInteraction(in ctx, waterfallResult.TimelineInteraction);

            var clickedId = waterfallResult.SelectedId;
            if (clickedId == 0L) return;

            for (var i = 0; i < rows.Count; i++)
            {
                var node = rows[i].Node;
                if (node.ContextId != clickedId) continue;

                _viewModel.SelectContext(node.ContextId);
                var kind = node.ContextId == node.RootContextId
                    ? BattleDiagnosticSelectionKind.TraceRoot
                    : BattleDiagnosticSelectionKind.TraceNode;
                ctx.WorkspaceState?.Select(new BattleDiagnosticSelection(
                    node.Scope,
                    kind,
                    node.ContextId,
                    node.StartFrame,
                    node.RootContextId));
                ctx.RequestRepaint?.Invoke();
                break;
            }
        }

        private static void ApplyTimelineInteraction(
            in BattleDebugContext ctx,
            BattleDebugTimelineInteractionResult interaction)
        {
            var changed = BattleDebugTimelineInteraction.Apply(ctx.WorkspaceState, interaction);
            if (interaction.Kind == BattleDebugTimelineInteractionKind.SelectFrame &&
                BattleDiagnosticFrames.IsValid(interaction.Frame))
            {
                ctx.SeekReplayFrame?.Invoke(interaction.Frame);
            }
            if (changed)
            {
                ctx.RequestRepaint?.Invoke();
            }
        }

        private static BattleDiagnosticFrameRange ResolveTraceRange(
            IReadOnlyList<BattleDebugDiagnosticTraceRow> rows,
            int cursorFrame)
        {
            if (rows == null || rows.Count == 0)
            {
                return new BattleDiagnosticFrameRange(
                    BattleDiagnosticFrames.Invalid,
                    BattleDiagnosticFrames.Invalid);
            }

            var minFrame = rows[0].Node.StartFrame;
            var maxFrame = ResolveEffectiveEnd(rows[0].Node, cursorFrame);
            for (var i = 1; i < rows.Count; i++)
            {
                minFrame = Mathf.Min(minFrame, rows[i].Node.StartFrame);
                maxFrame = Mathf.Max(maxFrame, ResolveEffectiveEnd(rows[i].Node, cursorFrame));
            }
            return new BattleDiagnosticFrameRange(minFrame, maxFrame);
        }

        private static int ResolveEffectiveEnd(
            BattleDiagnosticTraceNodeSummary node,
            int cursorFrame)
        {
            if (node.EndFrame >= 0) return node.EndFrame;
            return BattleDiagnosticFrames.IsValid(cursorFrame)
                ? Mathf.Max(cursorFrame, node.StartFrame)
                : node.StartFrame;
        }

        private enum TraceWidgetKind
        {
            Tree = 0,
            Waterfall = 1,
            Details = 2
        }

        private sealed class TraceWidget : IBattleDebugWidget
        {
            private readonly BattleDebugDiagnosticTracePanel _owner;
            private readonly TraceWidgetKind _kind;

            public TraceWidget(BattleDebugDiagnosticTracePanel owner, TraceWidgetKind kind)
            {
                _owner = owner;
                _kind = kind;
                Descriptor = BattleDebugModuleCatalog.Describe(owner);
            }

            public BattleDebugModuleDescriptor Descriptor { get; }
            public string StableId => _kind == TraceWidgetKind.Tree
                ? BattleDebugWidgetIds.TraceTree
                : _kind == TraceWidgetKind.Waterfall
                    ? BattleDebugWidgetIds.TraceWaterfall
                    : BattleDebugWidgetIds.TraceDetails;
            public string DisplayName => _kind == TraceWidgetKind.Tree
                ? "Trace Tree"
                : _kind == TraceWidgetKind.Waterfall
                    ? "Frame Waterfall"
                    : "Trace Details";
            public bool OwnsScrollView => _kind != TraceWidgetKind.Details;

            public bool IsAvailable(in BattleDebugContext context)
            {
                return _owner.IsVisible(in context);
            }

            public void Draw(in BattleDebugContext context)
            {
                switch (_kind)
                {
                    case TraceWidgetKind.Tree:
                        _owner.DrawTreeWidget(in context);
                        break;
                    case TraceWidgetKind.Waterfall:
                        _owner.DrawWaterfallWidget(in context);
                        break;
                    default:
                        _owner.DrawDetailsWidget(in context);
                        break;
                }
            }
        }

        private static void DrawEmptyState(in BattleDebugEmptyStateProjection projection)
        {
            if (!projection.HasValue) return;

            var message = string.IsNullOrEmpty(projection.Message)
                ? projection.Title
                : $"{projection.Title}\n{projection.Message}";
            var messageType = projection.Severity == BattleDebugEmptyStateSeverity.Error
                ? MessageType.Error
                : projection.Severity == BattleDebugEmptyStateSeverity.Warning
                    ? MessageType.Warning
                    : MessageType.Info;
            EditorGUILayout.HelpBox(message, messageType);
        }

        private static void DrawTraceHeader()
        {
            var rect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.12f));
            }
            GUI.Label(
                new Rect(rect.x + 8f, rect.y, Mathf.Max(100f, rect.width * 0.32f), rect.height),
                "流程节点",
                EditorStyles.miniBoldLabel);
            if (rect.width < 720f)
            {
                GUI.Label(
                    new Rect(rect.xMax - 176f, rect.y, 168f, rect.height),
                    "配置 / 实体 / 帧 / 结果",
                    EditorStyles.miniLabel);
                return;
            }

            var contentX = rect.x + Mathf.Clamp(rect.width * 0.30f, 210f, 310f);
            GUI.Label(new Rect(contentX, rect.y, 140f, rect.height), "配置 / Trigger", EditorStyles.miniLabel);
            contentX += 140f;
            GUI.Label(new Rect(contentX, rect.y, 150f, rect.height), "Source -> Target", EditorStyles.miniLabel);
            contentX += Mathf.Clamp(rect.width * 0.22f, 135f, 190f);
            GUI.Label(new Rect(contentX, rect.y, 112f, rect.height), "帧区间", EditorStyles.miniLabel);
            contentX += 112f;
            GUI.Label(new Rect(contentX, rect.y, 90f, rect.height), "结果", EditorStyles.miniLabel);
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
            var rect = GUILayoutUtility.GetRect(0f, TraceRowHeight, GUILayout.ExpandWidth(true));
            var onSelectedPath = _viewModel.IsOnSelectedPath(node.ContextId);
            if (Event.current.type == EventType.Repaint)
            {
                if (selected)
                {
                    EditorGUI.DrawRect(rect, new Color(0.18f, 0.43f, 0.68f, 0.35f));
                }
                else if (searchMatch)
                {
                    EditorGUI.DrawRect(rect, new Color(0.78f, 0.62f, 0.12f, 0.22f));
                }
                else if (onSelectedPath)
                {
                    EditorGUI.DrawRect(rect, new Color(0.22f, 0.48f, 0.66f, 0.10f));
                }

                var connectorColor = new Color(0.45f, 0.48f, 0.52f, onSelectedPath ? 0.75f : 0.38f);
                for (var depth = 0; depth < row.Depth; depth++)
                {
                    var x = rect.x + depth * TreeIndentWidth + TreeIndentWidth * 0.5f;
                    EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height), connectorColor);
                }
                if (row.Depth > 0)
                {
                    var branchX = rect.x + (row.Depth - 1) * TreeIndentWidth + TreeIndentWidth * 0.5f;
                    var branchY = rect.y + rect.height * 0.5f;
                    EditorGUI.DrawRect(
                        new Rect(branchX, branchY, TreeIndentWidth * 0.5f, 1f),
                        connectorColor);
                }
            }

            var xPosition = rect.x + row.Depth * TreeIndentWidth;
            var foldoutRect = new Rect(xPosition, rect.y + 2f, FoldoutWidth, rect.height - 4f);
            if (hasChildren && string.IsNullOrEmpty(_viewModel.SearchText))
            {
                if (GUI.Button(
                        foldoutRect,
                        _viewModel.IsCollapsed(node.ContextId) ? ">" : "v",
                        EditorStyles.miniButton))
                {
                    _viewModel.ToggleCollapsed(node.ContextId);
                }
            }
            else if (row.IsOrphan)
            {
                GUI.Label(foldoutRect, "!", EditorStyles.miniBoldLabel);
            }

            xPosition = foldoutRect.xMax + 2f;
            var stateColor = GetStateColor(node.State);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(
                    new Rect(xPosition, rect.y + 3f, StateRailWidth, rect.height - 6f),
                    stateColor);
            }
            xPosition += StateRailWidth + 5f;

            var contentRect = new Rect(xPosition, rect.y, Mathf.Max(0f, rect.xMax - xPosition), rect.height);
            if (GUI.Button(
                    contentRect,
                    new GUIContent(string.Empty, BuildNodeTooltip(in row)),
                    GUIStyle.none))
            {
                SelectNode(in ctx, in node);
            }

            var compact = contentRect.width < 720f;
            var labelStyle = selected ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel;
            var kindLabel = (pinned ? "[PIN] " : string.Empty) + node.Kind + "  #" + node.ContextId;
            if (hasChildren) kindLabel += "  (" + _viewModel.GetChildCount(node.ContextId) + ")";
            if (row.IsOrphan) kindLabel += "  [orphan]";

            if (compact)
            {
                var firstLine = new Rect(contentRect.x + 3f, contentRect.y, contentRect.width, contentRect.height * 0.55f);
                var secondLine = new Rect(contentRect.x + 3f, contentRect.y + 10f, contentRect.width, contentRect.height * 0.45f);
                GUI.Label(firstLine, kindLabel, labelStyle);
                GUI.Label(
                    secondLine,
                    $"{BuildConfigText(in node)}   {BuildActorRoute(in node)}   " +
                    $"{BuildFrameSpan(in node)}   {BuildResultText(in node)}",
                    EditorStyles.miniLabel);
                return;
            }

            var kindWidth = Mathf.Clamp(contentRect.width * 0.30f, 170f, 270f);
            var configWidth = 140f;
            var actorWidth = Mathf.Clamp(contentRect.width * 0.22f, 135f, 190f);
            var frameWidth = 112f;
            var resultWidth = Mathf.Max(80f, contentRect.width - kindWidth - configWidth - actorWidth - frameWidth);
            var column = new Rect(contentRect.x + 3f, contentRect.y + 2f, kindWidth - 3f, contentRect.height - 4f);
            GUI.Label(column, new GUIContent(kindLabel, BuildNodeTooltip(in row)), labelStyle);
            column.x += kindWidth;
            column.width = configWidth;
            GUI.Label(column, BuildConfigText(in node), EditorStyles.miniLabel);
            column.x += configWidth;
            column.width = actorWidth;
            GUI.Label(column, BuildActorRoute(in node), EditorStyles.miniLabel);
            column.x += actorWidth;
            column.width = frameWidth;
            GUI.Label(column, BuildFrameSpan(in node), EditorStyles.miniLabel);
            column.x += frameWidth;
            column.width = resultWidth;
            DrawResultBadge(column, in node, stateColor);
        }

        private void DrawSelectionDetails(in BattleDebugContext ctx)
        {
            var path = _viewModel.SelectedPath;
            if (path.Count == 0) return;

            var selected = path[path.Count - 1];
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("所选流程节点", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "位置 / 类型",
                $"{path.Count}/{_viewModel.Summary.MaximumDepth + 1}   {selected.Kind} #{selected.ContextId}");
            EditorGUILayout.LabelField(
                "结果",
                BuildResultText(in selected));
            EditorGUILayout.LabelField("帧区间 / 持续", BuildFrameSpan(in selected));
            EditorGUILayout.LabelField("配置", BuildConfigText(in selected));
            if (selected.TriggerId != 0)
            {
                EditorGUILayout.LabelField("Trigger Plan", selected.TriggerId.ToString());
            }
            EditorGUILayout.LabelField(
                "父节点 / 子节点",
                $"{FormatId(selected.ParentContextId)} / {_viewModel.GetChildCount(selected.ContextId)}");
            if (selected.SkillId != 0 || selected.CastFlowId != 0 || !string.IsNullOrEmpty(selected.PhaseId))
            {
                EditorGUILayout.LabelField(
                    "技能 / CastFlow / 阶段",
                    $"{FormatId(selected.SkillId)} / {FormatId(selected.CastFlowId)} / " +
                    (string.IsNullOrEmpty(selected.PhaseId) ? "-" : selected.PhaseId));
            }

            DrawActorRoute(in ctx, in selected);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!BattleDiagnosticFrames.IsValid(selected.StartFrame));
            if (GUILayout.Button("定位起始帧", GUILayout.Width(88)))
            {
                NavigateToFrame(in ctx, selected.StartFrame);
            }
            EditorGUI.EndDisabledGroup();
            if (BattleDiagnosticFrames.IsValid(selected.EndFrame) &&
                GUILayout.Button("定位结束帧", GUILayout.Width(88)))
            {
                NavigateToFrame(in ctx, selected.EndFrame);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            var hasConfigReference = BattleDebugConfigReferenceMapper.TryFromTraceNode(
                in selected,
                out var configReference);
            EditorGUI.BeginDisabledGroup(!hasConfigReference || ctx.OpenConfig == null);
            if (GUILayout.Button("打开配置", GUILayout.Width(80)))
            {
                ctx.OpenConfig?.Invoke(configReference);
            }
            EditorGUI.EndDisabledGroup();
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

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Root Path", EditorStyles.boldLabel);
            if (GUILayout.Button("复制链路", GUILayout.Width(72)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildPathText(path);
            }
            EditorGUILayout.EndHorizontal();
            var pathText = BuildPathText(path);
            var pathWidth = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 36f);
            var pathHeight = Mathf.Clamp(
                EditorStyles.textArea.CalcHeight(new GUIContent(pathText), pathWidth),
                EditorGUIUtility.singleLineHeight,
                80f);
            EditorGUILayout.SelectableLabel(
                pathText,
                EditorStyles.textArea,
                GUILayout.Height(pathHeight));
        }

        private static void DrawActorRoute(
            in BattleDebugContext ctx,
            in BattleDiagnosticTraceNodeSummary node)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Source -> Target");
            DrawActorButton(in ctx, node.SourceActorId, "source");
            GUILayout.Label("->", GUILayout.Width(18));
            DrawActorButton(in ctx, node.TargetActorId, "target");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawActorButton(
            in BattleDebugContext ctx,
            long actorId,
            string role)
        {
            EditorGUI.BeginDisabledGroup(actorId <= 0 || ctx.SelectActor == null);
            if (GUILayout.Button(
                    actorId > 0 ? $"{role} #{actorId}" : $"{role} -",
                    GUILayout.Width(112)))
            {
                ctx.SelectActor?.Invoke(actorId);
            }
            EditorGUI.EndDisabledGroup();
        }

        private void SelectNode(
            in BattleDebugContext ctx,
            in BattleDiagnosticTraceNodeSummary node)
        {
            _viewModel.SelectContext(node.ContextId);
            var selectionKind = node.ContextId == node.RootContextId
                ? BattleDiagnosticSelectionKind.TraceRoot
                : BattleDiagnosticSelectionKind.TraceNode;
            ctx.WorkspaceState?.Select(new BattleDiagnosticSelection(
                node.Scope,
                selectionKind,
                node.ContextId,
                node.StartFrame,
                node.RootContextId));
            ctx.RequestRepaint?.Invoke();
        }

        private static void NavigateToFrame(in BattleDebugContext ctx, int frame)
        {
            if (!BattleDiagnosticFrames.IsValid(frame))
            {
                return;
            }

            ctx.WorkspaceState?.SetFrame(frame);
            ctx.SeekReplayFrame?.Invoke(frame);
            ctx.RequestRepaint?.Invoke();
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

        private static string BuildNodeTooltip(in BattleDebugDiagnosticTraceRow row)
        {
            var node = row.Node;
            return $"{node.Kind} #{node.ContextId}\n" +
                   $"Parent #{node.ParentContextId}, depth {row.Depth}\n" +
                   $"{BuildConfigText(in node)}, trigger {FormatId(node.TriggerId)}\n" +
                   $"{BuildActorRoute(in node)}\n" +
                   $"{BuildFrameSpan(in node)}, {BuildResultText(in node)}";
        }

        private static string BuildActorRoute(in BattleDiagnosticTraceNodeSummary node)
        {
            return $"src {FormatId(node.SourceActorId)} -> tgt {FormatId(node.TargetActorId)}";
        }

        private static string BuildConfigText(in BattleDiagnosticTraceNodeSummary node)
        {
            if (string.Equals(node.Kind, "EffectExecution", System.StringComparison.Ordinal) ||
                string.Equals(node.Kind, "SkillEffect", System.StringComparison.Ordinal))
            {
                return node.TriggerId > 0
                    ? $"effect {FormatId(node.ConfigId)} / trig {node.TriggerId}"
                    : $"effect {FormatId(node.ConfigId)}";
            }
            if (string.Equals(node.Kind, "EffectAction", System.StringComparison.Ordinal))
            {
                return $"action {FormatId(node.ConfigId)}";
            }
            if (string.Equals(node.Kind, "SkillCast", System.StringComparison.Ordinal) ||
                string.Equals(node.Kind, "SkillPhase", System.StringComparison.Ordinal))
            {
                return $"skill {FormatId(node.SkillId != 0 ? node.SkillId : node.ConfigId)}";
            }
            return "cfg " + FormatId(node.ConfigId);
        }

        private static string BuildFrameSpan(in BattleDiagnosticTraceNodeSummary node)
        {
            return node.EndFrame >= 0
                ? $"F{node.StartFrame} - F{node.EndFrame}  [{node.EndFrame - node.StartFrame}f]"
                : $"F{node.StartFrame} - active";
        }

        private static string BuildResultText(in BattleDiagnosticTraceNodeSummary node)
        {
            if (node.State == BattleDiagnosticTraceNodeState.Active) return "ACTIVE";
            if (!string.IsNullOrEmpty(node.EndReason)) return node.EndReason;
            return node.State.ToString();
        }

        private static string BuildSummaryFrameText(in BattleDebugDiagnosticTraceSummary summary)
        {
            if (summary.ActiveCount > 0) return $"F{summary.FirstFrame} -> active";
            return summary.LastFrame >= summary.FirstFrame
                ? $"F{summary.FirstFrame} -> F{summary.LastFrame}  [{summary.LastFrame - summary.FirstFrame}f]"
                : $"F{summary.FirstFrame}";
        }

        private static string FormatId(long value) => value > 0 ? value.ToString() : "-";

        private static void DrawResultBadge(
            Rect rect,
            in BattleDiagnosticTraceNodeSummary node,
            Color stateColor)
        {
            var badge = new Rect(rect.x + 2f, rect.y + 3f, Mathf.Max(0f, rect.width - 4f), rect.height - 6f);
            if (Event.current.type == EventType.Repaint)
            {
                var background = stateColor;
                background.a = 0.20f;
                EditorGUI.DrawRect(badge, background);
            }
            GUI.Label(
                new Rect(badge.x + 4f, rect.y + 2f, Mathf.Max(0f, badge.width - 6f), rect.height - 4f),
                BuildResultText(in node),
                EditorStyles.miniBoldLabel);
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
                    return new Color(0.48f, 0.76f, 0.52f);
            }
        }
    }
}
