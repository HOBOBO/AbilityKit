using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    [BattleDebugModule(
        BattleDebugModuleIds.DiagnosticTrace,
        "调查",
        RequiredCapabilities = BattleDiagnosticCapabilities.Trace,
        Selections = BattleDebugModuleSelectionSupport.Frame |
                     BattleDebugModuleSelectionSupport.Actor |
                     BattleDebugModuleSelectionSupport.Event |
                     BattleDebugModuleSelectionSupport.Trace |
                     BattleDebugModuleSelectionSupport.Config)]
    internal sealed class BattleDebugDiagnosticTracePanel :
        IBattleDebugPanel,
        IBattleDebugPanelLayout,
        IBattleDebugPanelSessionCleanup,
        IBattleDebugTraceTarget,
        IBattleDebugWidgetProvider
    {
        public string Name => "Trace";
        public int Order => 405;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => true;

        private readonly BattleDebugDiagnosticTraceViewModel _viewModel =
            new BattleDebugDiagnosticTraceViewModel();
        private readonly BattleDebugDiagnosticAttributesViewModel _attributeEvidenceViewModel =
            new BattleDebugDiagnosticAttributesViewModel();
        private readonly BattleDebugRuntimeObjectsViewModel _relatedObjectsViewModel =
            new BattleDebugRuntimeObjectsViewModel();
        private readonly BattleDebugTraceTimelineEvents _timelineEvents = new BattleDebugTraceTimelineEvents();
        private BattleDiagnosticEvent? _timelineEvent;
        private BattleDiagnosticRuntimeObject? _timelineObject;
        private BattleDiagnosticSessionScope _timelineSelectionScope;
        private bool _showTraceTrack = true;
        private bool _showEventTrack = true;
        private bool _showObjectTrack = true;
        private BattleDiagnosticSelection _attributeEvidence;
        private bool _showRelatedObjects;
        private Vector2 _evidenceScroll;
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
            new BattleDebugLegendItem("进行中", new Color(0.65f, 0.9f, 1f)),
            new BattleDebugLegendItem("失败", new Color(1f, 0.55f, 0.55f)),
            new BattleDebugLegendItem("强制结束", new Color(1f, 0.8f, 0.45f)),
            new BattleDebugLegendItem("已结束", new Color(0.48f, 0.76f, 0.52f))
        };
        private static readonly string[] TraceViewModeNames =
        {
            "流程",
            "问题",
            "效果",
            "进行中"
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

        public void ClearSessionState()
        {
            _viewModel.Clear();
            _timelineEvents.Clear();
            ClearTimelineSelection();
            _attributeEvidenceViewModel.InvalidateCache();
            _relatedObjectsViewModel.Invalidate();
            _relatedObjectsViewModel.ClearSelection();
            ResetEvidence();
            _rootContextIdText = string.Empty;
            _pendingRootContextId = 0;
            _pendingContextId = 0;
            _treeScroll = Vector2.zero;
            _waterfallScroll = Vector2.zero;
            _waterfallItems.Clear();
            _overviewItems.Clear();
            System.Array.Clear(_overviewBuffer.Counts, 0, _overviewBuffer.Counts.Length);
        }

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "诊断会话不可用。请启动战斗或打开包含战斗诊断的 Artifact。",
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
                    selectionSubject: "根上下文"));
                return;
            }

            _viewModel.RefreshIfNeeded(session, _viewModel.RootContextId);
            if (_pendingContextId > 0 && _viewModel.SelectContext(_pendingContextId))
            {
                _pendingContextId = 0;
                ScrollToSelection();
            }
            EditorGUILayout.LabelField(
                $"Trace 存储版本={_viewModel.StoreRevision}  " +
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

            DrawFlowSummary(ctx.AvailableContentWidth);
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
            if (DrawTimelineSelection(in ctx)) return;
            if (_viewModel.SelectedPath.Count == 0)
            {
                EditorGUILayout.HelpBox("请从 Trace 树或帧瀑布图中选择一个节点。", MessageType.Info);
                return;
            }

            DrawSelectionDetails(in ctx);
        }

        private bool TryPrepareWidget(in BattleDebugContext ctx, bool drawToolbar)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "战斗诊断会话不可用。",
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
                    subject: "Trace 根节点",
                    selectionSubject: "根上下文"));
                return false;
            }

            _viewModel.RefreshIfNeeded(session, _viewModel.RootContextId);
            if (_pendingContextId > 0 && _viewModel.SelectContext(_pendingContextId))
            {
                _pendingContextId = 0;
                ScrollToSelection();
            }
            EditorGUILayout.LabelField(
                $"Trace 存储版本={_viewModel.StoreRevision}  " +
                $"节点={_viewModel.Rows.Count}  可见={_viewModel.VisibleRows.Count}",
                EditorStyles.miniLabel);

            if (_viewModel.Rows.Count == 0)
            {
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    _viewModel.QueryStatus,
                    hasActiveFilter: !string.IsNullOrEmpty(_viewModel.SearchText),
                    subject: "Trace 节点"));
                return false;
            }
            if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.Warning);
            }

            DrawFlowSummary(ctx.AvailableContentWidth);
            return true;
        }

        private void DrawToolbar(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session)
        {
            _viewModel.RefreshRootIndexIfNeeded(session);
            var compact = ctx.AvailableContentWidth > 0 && ctx.AvailableContentWidth < 700f;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawRootPicker(in ctx, session);
            if (compact)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            }
            GUILayout.Label("根 ID", GUILayout.Width(38));
            _rootContextIdText = GUILayout.TextField(
                _rootContextIdText ?? string.Empty,
                GUILayout.Width(compact ? 60 : 100));

            var hasValidRoot = long.TryParse(_rootContextIdText, out var rootContextId) &&
                               rootContextId > 0;
            EditorGUI.BeginDisabledGroup(!hasValidRoot);
            if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(44)))
            {
                LoadRoot(in ctx, session, rootContextId);
                GUI.FocusControl(null);
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(_viewModel.RootContextId == 0);
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(56)))
            {
                _viewModel.InvalidateCache();
                _viewModel.InvalidateRootIndex();
                ctx.RequestRepaint?.Invoke();
            }
            if (GUILayout.Button("清除", EditorStyles.toolbarButton, GUILayout.Width(44)))
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
            var viewMode = (BattleDebugTraceViewMode)GUILayout.Toolbar(
                (int)_viewModel.ViewMode,
                TraceViewModeNames,
                EditorStyles.toolbarButton,
                GUILayout.Width(230));
            if (viewMode != _viewModel.ViewMode)
            {
                _viewModel.SetViewMode(viewMode);
                _treeScroll = Vector2.zero;
                _waterfallScroll = Vector2.zero;
            }
            if (compact)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            }
            GUILayout.Label("搜索", GUILayout.Width(44));
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
                if (compact)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                }
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

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
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
            if (GUILayout.Button("固定", EditorStyles.toolbarButton, GUILayout.Width(42)))
            {
                _viewModel.PinSelection();
            }
            EditorGUI.EndDisabledGroup();

            if (compact)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            }
            EditorGUI.BeginDisabledGroup(!_viewModel.IsPinnedContextAvailable);
            if (GUILayout.Button("返回固定", EditorStyles.toolbarButton, GUILayout.Width(64)))
            {
                _viewModel.SelectPinned();
                ScrollToSelection();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(_viewModel.PinnedContextId == 0);
            if (GUILayout.Button("清除固定", EditorStyles.toolbarButton, GUILayout.Width(64)))
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

        private void DrawRootPicker(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session)
        {
            var roots = _viewModel.RootSummaries;
            if (roots.Count == 0)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Popup(0, new[] { "没有保留的 Trace 根节点" },
                    GUILayout.MaxWidth(320), GUILayout.ExpandWidth(true));
                EditorGUI.EndDisabledGroup();
                return;
            }

            var labels = new string[roots.Count + 1];
            labels[0] = "选择要排查的 Trace";
            var selectedIndex = 0;
            for (var i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                labels[i + 1] = BuildRootLabel(in root);
                if (root.RootContextId == _viewModel.RootContextId) selectedIndex = i + 1;
            }

            var nextIndex = EditorGUILayout.Popup(selectedIndex, labels,
                GUILayout.MaxWidth(320), GUILayout.ExpandWidth(true));
            if (nextIndex > 0 && nextIndex != selectedIndex)
            {
                LoadRoot(in ctx, session, roots[nextIndex - 1].RootContextId);
            }
        }

        /// <summary>
        /// 切换根目录并把实际保留的节点写入共享导航历史。
        /// </summary>
        internal void LoadRoot(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session,
            long rootContextId)
        {
            ClearTimelineSelection();
            ResetEvidence();
            _rootContextIdText = rootContextId.ToString();
            _treeScroll = Vector2.zero;
            _waterfallScroll = Vector2.zero;
            _viewModel.InvalidateCache();
            _viewModel.RefreshIfNeeded(session, rootContextId);
            _viewModel.SelectContext(rootContextId);
            if (_viewModel.SelectedPath.Count > 0)
            {
                var node = _viewModel.SelectedPath[_viewModel.SelectedPath.Count - 1];
                ctx.WorkspaceState?.Select(BattleDebugNavigation.CreateTraceSelection(in node));
                ScrollToSelection();
            }
            ctx.RequestRepaint?.Invoke();
        }

        private static string BuildRootLabel(in BattleDiagnosticTraceRootSummary root)
        {
            var marker = root.HasIssues ? "[!]" : root.IsActive ? "[*]" : "[正常]";
            var value = root.HasIssues
                ? $"{root.IssueCount} 个问题"
                : root.IsActive
                    ? $"{root.ActiveCount} 个进行中"
                    : $"{root.EffectCount} 个效果";
            return $"{marker} {BattleDebugDisplayText.TraceKind(root.Root.Kind)} #{root.RootContextId} | {value} | " +
                   $"{root.NodeCount} 个节点 | F{root.Root.StartFrame}-F{root.LastFrame}";
        }

        private void DrawFlowSummary(float availableWidth)
        {
            var summary = _viewModel.Summary;
            var visible = _viewModel.VisibleSummary;
            if (summary.NodeCount == 0) return;

            if (availableWidth > 0f && availableWidth < 700f)
            {
                EditorGUILayout.LabelField($"根节点 #{_viewModel.RootContextId} · " +
                    $"节点 {visible.NodeCount}/{summary.NodeCount} · 效果 {visible.EffectCount}/{summary.EffectCount} · " +
                    $"动作 {visible.ActionCount}/{summary.ActionCount} · 问题 {visible.IssueCount}/{summary.IssueCount} · " +
                    $"进行中 {visible.ActiveCount}/{summary.ActiveCount} · 深度 {visible.MaximumDepth}",
                    EditorStyles.wordWrappedMiniLabel);
                if (visible.NodeCount > 0)
                    EditorGUILayout.LabelField(BuildSummaryFrameText(in visible), EditorStyles.wordWrappedMiniLabel);
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"根节点 #{_viewModel.RootContextId}", EditorStyles.miniBoldLabel, GUILayout.Width(100));
            GUILayout.Label($"节点 {visible.NodeCount}/{summary.NodeCount}", EditorStyles.miniLabel, GUILayout.Width(86));
            GUILayout.Label($"效果 {visible.EffectCount}/{summary.EffectCount}", EditorStyles.miniLabel, GUILayout.Width(76));
            GUILayout.Label($"动作 {visible.ActionCount}/{summary.ActionCount}", EditorStyles.miniLabel, GUILayout.Width(76));
            var oldColor = GUI.color;
            if (visible.IssueCount > 0) GUI.color = new Color(1f, 0.62f, 0.58f);
            GUILayout.Label($"问题 {visible.IssueCount}/{summary.IssueCount}", EditorStyles.miniBoldLabel, GUILayout.Width(72));
            GUI.color = oldColor;
            if (visible.ActiveCount > 0)
            {
                GUI.color = new Color(0.65f, 0.9f, 1f);
                GUILayout.Label($"进行中 {visible.ActiveCount}/{summary.ActiveCount}", EditorStyles.miniBoldLabel, GUILayout.Width(72));
                GUI.color = oldColor;
            }
            GUILayout.Label($"深度 {visible.MaximumDepth}", EditorStyles.miniLabel, GUILayout.Width(58));
            GUILayout.FlexibleSpace();
            if (visible.NodeCount > 0) GUILayout.Label(BuildSummaryFrameText(in visible), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTree(in BattleDebugContext ctx)
        {
            EditorGUILayout.LabelField("Trace 树", EditorStyles.boldLabel);
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
            EditorGUILayout.LabelField("执行时间轴", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "共享逻辑帧 · 事件用点表示；对象仅在已知创建和结束帧时显示完整条带。",
                EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _showTraceTrack = GUILayout.Toggle(_showTraceTrack, "Trace", EditorStyles.toolbarButton);
                _showEventTrack = GUILayout.Toggle(_showEventTrack, "事件", EditorStyles.toolbarButton);
                _showObjectTrack = GUILayout.Toggle(_showObjectTrack, "对象", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
            }

            var rows = _viewModel.VisibleRows;

            var cursorFrame = ctx.WorkspaceState?.FrameCursor.Frame ?? BattleDiagnosticFrames.Invalid;
            _waterfallItems.Clear();
            _overviewItems.Clear();
            for (var index = 0; _showTraceTrack && index < rows.Count; index++)
            {
                var row = rows[index];
                var node = row.Node;
                var effectiveEnd = node.EndFrame >= 0
                    ? node.EndFrame
                    : BattleDiagnosticFrames.IsValid(cursorFrame)
                        ? Mathf.Max(cursorFrame, node.StartFrame)
                        : node.StartFrame;
                _waterfallItems.Add(new BattleDebugWaterfallItem(
                    node.ContextId,
                    $"{BattleDebugDisplayText.TraceKind(node.Kind)} #{node.ContextId}  {BuildConfigText(in node)}",
                    $"来源={node.SourceActorId}，目标={node.TargetActorId}，" +
                    $"{BuildConfigText(in node)}\n" +
                    $"状态={BuildResultText(in node)}\n" +
                    $"F{node.StartFrame} -> " +
                    (node.EndFrame >= 0 ? $"F{node.EndFrame}" : "进行中"),
                    node.StartFrame,
                    effectiveEnd,
                    row.Depth,
                    !_timelineEvent.HasValue && !_timelineObject.HasValue && node.ContextId == _viewModel.SelectedContextId,
                    GetStateColor(node.State)));
            }

            AppendTimelineEvidence(in ctx);
            if (_waterfallItems.Count == 0)
            {
                EditorGUILayout.HelpBox("当前启用的轨道没有可定位的记录。", MessageType.Info);
                return;
            }
            var firstFrame = int.MaxValue;
            var lastFrame = BattleDiagnosticFrames.Invalid;
            foreach (var item in _waterfallItems)
            {
                firstFrame = Mathf.Min(firstFrame, item.StartFrame);
                lastFrame = Mathf.Max(lastFrame, item.EndFrame);
                _overviewItems.Add(new BattleDebugTimelineOverviewItem(item.StartFrame, item.EndFrame));
            }
            var automaticRange = new BattleDiagnosticFrameRange(firstFrame, lastFrame);
            var visibleRange = ctx.WorkspaceState != null
                ? ctx.WorkspaceState.TimeRange.Resolve(automaticRange)
                : automaticRange;
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
            if (waterfallResult.SelectedTrack != BattleDebugWaterfallTrack.Trace)
            {
                SelectTimelineEvidence(in ctx, waterfallResult.SelectedTrack, (int)clickedId - 1);
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var node = rows[i].Node;
                if (node.ContextId != clickedId) continue;
                SelectNode(in ctx, in node);
                break;
            }
        }

        private void AppendTimelineEvidence(in BattleDebugContext ctx)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session)) return;
            var rootId = _viewModel.RootContextId;
            if (_timelineSelectionScope.IsValid && (_timelineSelectionScope != session.SessionInfo.Scope ||
                (_timelineEvent.HasValue && _timelineEvent.Value.RootContextId != rootId) ||
                (_timelineObject.HasValue && _timelineObject.Value.RootContextId != rootId)))
                ClearTimelineSelection();
            if (_showEventTrack) AppendTimelineEvents(session, rootId);
            if (_showObjectTrack) AppendTimelineObjects(session, rootId);
        }

        private void AppendTimelineEvents(IBattleDiagnosticReadOnlySession session, long rootId)
        {
            _timelineEvents.Refresh(session, rootId);
            var events = _timelineEvents.Items;
            for (var index = 0; index < events.Count; index++)
            {
                var item = events[index];
                var label = $"#{item.Sequence} {BattleDebugDisplayText.EventKind(item.Kind)}";
                _waterfallItems.Add(new BattleDebugWaterfallItem(index + 1, label,
                    $"{label} · F{item.Frame}\n{item.Summary}", item.Frame, item.Frame, 0,
                    _timelineEvent?.Sequence == item.Sequence, new Color(0.45f, 0.74f, 1f),
                    BattleDebugWaterfallTrack.Event, point: true));
            }
            DrawTimelineTrackStatus("事件", events.Count, _timelineEvents.Status, _timelineEvents.Status.HasMore);
        }

        private void AppendTimelineObjects(IBattleDiagnosticReadOnlySession session, long rootId)
        {
            var objects = _relatedObjectsViewModel;
            if (objects.RootContextId != rootId)
            {
                objects.RootContextId = rootId;
                objects.ClearSelection();
            }
            objects.RefreshIfNeeded(session);
            var undatedCount = 0;
            for (var index = 0; index < objects.Items.Count; index++)
            {
                var item = objects.Items[index];
                if (!BattleDebugTraceObjectSpan.TryProject(item.CreatedFrame, item.DestroyedFrame, item.BackfilledFrame,
                        out var start, out var end, out var point))
                {
                    undatedCount++;
                    continue;
                }

                var label = $"{BattleDebugDisplayText.RuntimeObjectKind(item.Kind)} #{item.RuntimeId}";
                _waterfallItems.Add(new BattleDebugWaterfallItem(index + 1, label,
                    $"{item.Reference} · {item.DisplayName}\n{FormatObjectFrames(in item)}", start, end, 0,
                    _timelineObject.HasValue && _timelineObject.Value.Reference.Equals(item.Reference),
                    new Color(0.79f, 0.62f, 0.93f), BattleDebugWaterfallTrack.RuntimeObject, point));
                if (_timelineObject.HasValue && _timelineSelectionScope == session.SessionInfo.Scope &&
                    _timelineObject.Value.RootContextId == rootId && _timelineObject.Value.Reference.Equals(item.Reference))
                    _timelineObject = item;
            }
            DrawTimelineTrackStatus("对象", objects.Items.Count, objects.QueryStatus, objects.HasMore);
            if (undatedCount > 0)
                EditorGUILayout.LabelField($"{undatedCount} 个对象没有可定位的帧，未绘制。", EditorStyles.miniLabel);
        }

        private static void DrawTimelineTrackStatus(string track, int count, BattleDiagnosticQueryStatus status, bool hasMore)
        {
            if (!status.CanDisplayResults && status.Phase != BattleDiagnosticQueryPhase.Empty)
                EditorGUILayout.LabelField($"{track}轨：{BattleDebugDisplayText.Availability(status.Availability)}",
                    EditorStyles.miniLabel);
            else if (count == 0 || hasMore)
                EditorGUILayout.LabelField(count == 0 ? $"{track}轨：无同根记录。"
                    : $"{track}轨：已加载 {count} 条，尚有更多记录。", EditorStyles.miniLabel);
        }

        private void SelectTimelineEvidence(in BattleDebugContext ctx, BattleDebugWaterfallTrack track, int index)
        {
            ClearTimelineSelection();
            _timelineSelectionScope = ctx.WorkspaceState?.Scope ?? default;
            if (track == BattleDebugWaterfallTrack.Event && index >= 0 && index < _timelineEvents.Items.Count)
            {
                _timelineEvent = _timelineEvents.Items[index];
                ctx.WorkspaceState?.SetFrame(_timelineEvent.Value.Frame);
            }
            else if (track == BattleDebugWaterfallTrack.RuntimeObject && index >= 0 && index < _relatedObjectsViewModel.Items.Count)
            {
                var item = _relatedObjectsViewModel.Items[index];
                _timelineObject = item;
                if (BattleDebugTraceObjectSpan.TryProject(item.CreatedFrame, item.DestroyedFrame, item.BackfilledFrame,
                        out var firstFrame, out _, out _))
                    ctx.WorkspaceState?.SetFrame(firstFrame);
            }
            ctx.RequestRepaint?.Invoke();
        }

        private bool DrawTimelineSelection(in BattleDebugContext ctx)
        {
            var rootId = _viewModel.RootContextId;
            if (_timelineSelectionScope != (ctx.WorkspaceState?.Scope ?? default)) return false;
            if (_timelineEvent.HasValue && _timelineEvent.Value.RootContextId == rootId)
            {
                var item = _timelineEvent.Value;
                EditorGUILayout.LabelField($"事件 #{item.Sequence} · F{item.Frame}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{BattleDebugDisplayText.EventKind(item.Kind)} · {BattleDebugDisplayText.EventOutcome(item.Outcome)}",
                    EditorStyles.wordWrappedLabel);
                if (!BattleDebugOfflineRelations.DrawEvent(in ctx, in item))
                    EditorGUILayout.LabelField($"来源 #{item.SourceActorId} · 目标 #{item.TargetActorId} · 上下文 #{item.ContextId}",
                        EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(item.Summary, EditorStyles.wordWrappedLabel);
                DrawLinkedNode(in ctx, item.Scope, item.RootContextId, item.ContextId);
                if (ctx.OpenEvent != null && GUILayout.Button("打开事件详情")) ctx.OpenEvent.Invoke(item);
                DrawReturnToNodeDetails();
                return true;
            }
            if (_timelineObject.HasValue && _timelineObject.Value.RootContextId == rootId)
            {
                var item = _timelineObject.Value;
                EditorGUILayout.LabelField($"{BattleDebugDisplayText.RuntimeObjectKind(item.Kind)} #{item.RuntimeId}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{item.Reference} · {item.DisplayName} · {BattleDebugDisplayText.RuntimeObjectState(item.State)}",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(FormatObjectFrames(in item), EditorStyles.wordWrappedMiniLabel);
                if (!BattleDebugOfflineRelations.DrawObjectActors(in ctx, in item, BattleDebugOfflineRelations.ObjectFrame(in item)))
                    EditorGUILayout.LabelField($"上下文 #{item.ContextId} · 来源 #{item.SourceActorId} · 所有者 #{item.OwnerActorId} · 目标 #{item.TargetActorId}",
                        EditorStyles.wordWrappedMiniLabel);
                DrawLinkedNode(in ctx, _timelineSelectionScope, item.RootContextId, item.ContextId);
                DrawReturnToNodeDetails();
                return true;
            }
            return false;
        }

        private void DrawLinkedNode(in BattleDebugContext ctx, BattleDiagnosticSessionScope scope,
            long rootContextId, long contextId)
        {
            if (!_viewModel.TryGetLinkedNode(scope, rootContextId, contextId, out var node))
            {
                var message = contextId <= 0 ? "没有记录关联的 Trace 节点。"
                    : $"关联上下文 #{contextId}，当前 Trace 未包含该节点。";
                EditorGUILayout.LabelField(message, EditorStyles.wordWrappedMiniLabel);
                return;
            }

            var caption = BattleDebugDisplayText.TraceKind(node.Kind);
            if (!GUILayout.Button(new GUIContent($"定位关联节点 · {caption} · {FormatKnownFrame(node.StartFrame)}",
                    $"精确定位 #{node.ContextId}；若节点被筛选隐藏，将展开路径或回到完整流程。"))) return;
            SelectNode(in ctx, in node);
            _viewModel.RevealSelection();
            _showTraceTrack = true;
            ScrollToSelection();
        }

        private static string FormatObjectFrames(in BattleDiagnosticRuntimeObject item) =>
            $"创建：{FormatKnownFrame(item.CreatedFrame)} · 结束：{FormatKnownFrame(item.DestroyedFrame)}" +
            (BattleDiagnosticFrames.IsValid(item.BackfilledFrame) ? $" · 回填：F{item.BackfilledFrame}" : string.Empty);

        private static string FormatKnownFrame(int frame) => BattleDiagnosticFrames.IsValid(frame) ? $"F{frame}" : "未记录";

        private void DrawReturnToNodeDetails()
        {
            if (GUILayout.Button("返回 Trace 节点详情")) ClearTimelineSelection();
        }

        private void ClearTimelineSelection()
        {
            _timelineEvent = null;
            _timelineObject = null;
            _timelineSelectionScope = default;
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
                ? "Trace 树"
                : _kind == TraceWidgetKind.Waterfall
                    ? "帧瀑布图"
                    : "Trace 详情";
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
            GUI.Label(new Rect(contentX, rect.y, 140f, rect.height), "配置 / 触发器", EditorStyles.miniLabel);
            contentX += 140f;
            GUI.Label(new Rect(contentX, rect.y, 150f, rect.height), "来源 -> 目标", EditorStyles.miniLabel);
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
            var kindLabel = (pinned ? "[固定] " : string.Empty) + BattleDebugDisplayText.TraceKind(node.Kind) + "  #" + node.ContextId;
            if (hasChildren) kindLabel += "  (" + _viewModel.GetChildCount(node.ContextId) + ")";
            if (row.IsOrphan) kindLabel += "  [孤立节点]";

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
            GUI.Label(column, new GUIContent(kindLabel, null, BuildNodeTooltip(in row)), labelStyle);
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
            if (_attributeEvidence.IsValid &&
                (!_attributeEvidence.BelongsTo(selected.Scope) || _attributeEvidence.RelatedId != selected.ContextId))
                ResetEvidence();
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("所选流程节点", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "位置 / 类型",
                $"{path.Count}/{_viewModel.Summary.MaximumDepth + 1}   {BattleDebugDisplayText.TraceKind(selected.Kind)} #{selected.ContextId}");
            EditorGUILayout.LabelField(
                "结果",
                BuildResultText(in selected));
            EditorGUILayout.LabelField("帧区间 / 持续", BuildFrameSpan(in selected));
            EditorGUILayout.LabelField("配置", BuildConfigText(in selected));
            if (selected.HasOrigin)
            {
                EditorGUILayout.LabelField("直接触发来源", BuildOriginText(in selected));
                var hasOriginConfig = BattleDebugConfigReferenceMapper.TryFromTraceOrigin(
                    in selected,
                    out var originConfig);
                EditorGUI.BeginDisabledGroup(!hasOriginConfig || ctx.OpenConfig == null);
                if (GUILayout.Button("打开来源配置", GUILayout.Width(110)))
                {
                    ctx.OpenConfig?.Invoke(originConfig);
                }
                EditorGUI.EndDisabledGroup();
            }
            else if (selected.Kind == "EffectExecution")
            {
                EditorGUILayout.LabelField("直接触发来源", "未记录");
            }
            if (selected.TriggerId != 0)
            {
                EditorGUILayout.LabelField("触发计划", selected.TriggerId.ToString());
            }
            EditorGUILayout.LabelField(
                "父节点 / 子节点",
                $"{FormatId(selected.ParentContextId)} / {_viewModel.GetChildCount(selected.ContextId)}");
            if (selected.SkillId != 0 || selected.CastFlowId != 0 || !string.IsNullOrEmpty(selected.PhaseId))
            {
                EditorGUILayout.LabelField(
                    "技能 / 施法流程 / 阶段",
                    $"{FormatId(selected.SkillId)} / {FormatId(selected.CastFlowId)} / " +
                    (string.IsNullOrEmpty(selected.PhaseId) ? "-" : selected.PhaseId));
            }

            DrawActorRoute(in ctx, in selected);
            DrawRelatedEvidence(in ctx, in selected);
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
            if (GUILayout.Button(
                    selected.Kind == "EffectAction" ? "打开触发计划" : "打开配置",
                    GUILayout.Width(110)))
            {
                ctx.OpenConfig?.Invoke(configReference);
            }
            EditorGUI.EndDisabledGroup();
            if (_viewModel.PinnedContextId != 0)
            {
                EditorGUILayout.LabelField(
                    "固定上下文",
                    _viewModel.IsPinnedContextAvailable
                        ? _viewModel.PinnedContextId.ToString()
                        : $"{_viewModel.PinnedContextId}（当前 Trace 中不可用）");
            }
            if (!string.IsNullOrEmpty(selected.EndReason))
            {
                EditorGUILayout.LabelField("结束原因", selected.EndReason);
            }

            DrawDefinitionDetails(in selected);
            DrawExecutionFacts(in selected);
            DrawActionFacts(in selected);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("根节点路径", EditorStyles.boldLabel);
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

        private static void DrawActionFacts(in BattleDiagnosticTraceNodeSummary node)
        {
            if (node.Kind != "EffectAction") return;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("动作执行快照", EditorStyles.boldLabel);
            var facts = node.ActionFacts;
            if (!facts.IsCaptured)
            {
                EditorGUILayout.LabelField("状态", facts.Availability == BattleDiagnosticDataAvailability.Evicted ? "已淘汰" : "未采集 / 不可用");
                return;
            }
            EditorGUILayout.LabelField("记录 / 代次", $"{facts.SnapshotId} / {facts.Generation}");
            EditorGUILayout.LabelField("类型 / 版本", $"{facts.TypeId} / {facts.SchemaVersion}");
            EditorGUILayout.LabelField("动作索引 / ID", $"{facts.ActionIndex} / {facts.ActionId}");
            var outcome = facts.Outcome == BattleDiagnosticActionOutcome.Completed ? "调用完成（未抛异常）" :
                facts.Outcome == BattleDiagnosticActionOutcome.Failed ? "调用失败" :
                facts.Outcome == BattleDiagnosticActionOutcome.Aborted ? "作用域中止" : "仅入口，结束未采集";
            EditorGUILayout.LabelField("执行状态", outcome);
            EditorGUILayout.LabelField("入口 / 结束帧", $"{facts.Frame} / {(facts.HasAfter ? facts.EndFrame.ToString() : "未采集")}");
            DrawActionActorValues("来源", facts.SourceBefore, facts.SourceAfter, facts.HasAfter);
            DrawActionActorValues("目标", facts.TargetBefore, facts.TargetAfter, facts.HasAfter);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("HP 实际提交", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("采集状态", facts.CommitsComplete ? "动作期间连续采集" : "未完整采集 / 无事件订阅");
            EditorGUILayout.LabelField("记录数", facts.CommitsTruncated ? $"{facts.Commits.Count}（已截断）" : facts.Commits.Count.ToString());
            if (facts.Commits.Count == 0)
                EditorGUILayout.LabelField("提交结果", "无已记录的 HP 变化");
            foreach (var commit in facts.Commits)
            {
                EditorGUILayout.Space(3);
                var kind = commit.Kind == 0 ? "伤害" : commit.Kind == 1 ? "治疗" : commit.Kind == 2 ? "重生" : commit.Kind.ToString();
                EditorGUILayout.LabelField("类型 / 来源 / 目标", $"{kind} / {commit.SourceActorId} / {commit.TargetActorId}");
                EditorGUILayout.LabelField("请求 / 实际值", $"{commit.RequestedValue:0.###} / {commit.AppliedValue:0.###}");
                EditorGUILayout.LabelField("HP 前 / 后 / 上限", $"{commit.OldHp:0.###} / {commit.TargetHp:0.###} / {commit.TargetMaxHp:0.###}");
                EditorGUILayout.LabelField("数值类型 / 原因", $"{commit.ValueType} / {commit.ReasonKind} / {commit.ReasonParam}");
                EditorGUILayout.LabelField("提交来源节点", commit.OriginContextId.ToString());
            }
            DrawActionDamageResults(in facts);
        }

        private static void DrawActionDamageResults(in BattleDiagnosticActionExecutionFacts facts)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("伤害管线结果", EditorStyles.boldLabel);
            if (facts.DamageAvailability != BattleDiagnosticDataAvailability.Available)
            {
                EditorGUILayout.LabelField("状态", "未采集 / 伤害通道未启用");
                return;
            }
            EditorGUILayout.LabelField("覆盖状态", facts.DamageCoverageContinuous ? "动作期间连续观察" : "覆盖不连续 / 未完整采集");
            EditorGUILayout.LabelField("记录数", facts.DamageResultsTruncated ? $"{facts.DamageResults.Count}（已截断）" : facts.DamageResults.Count.ToString());
            foreach (var result in facts.DamageResults)
            {
                var c = result.Calculation;
                var outcome = result.Outcome == BattleDiagnosticActionDamageOutcome.Applied ? "已提交 HP 伤害" :
                    result.Outcome == BattleDiagnosticActionDamageOutcome.FullyAbsorbed ? "护盾全吸收" :
                    result.Outcome == BattleDiagnosticActionDamageOutcome.NoHpDamage ? "零 HP 伤害" :
                    result.Outcome == BattleDiagnosticActionDamageOutcome.Rejected ? "管线拒绝" :
                    result.Outcome == BattleDiagnosticActionDamageOutcome.ExecutionFailed ? "执行异常，可能已有部分提交" :
                    result.Outcome == BattleDiagnosticActionDamageOutcome.PostCommitNotificationFailed ? "提交完成后的通知异常" : "未知结果";
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("结果", outcome);
                EditorGUILayout.LabelField("阶段", c.Stage.ToString());
                EditorGUILayout.LabelField("事件序号 / 帧", $"{result.Sequence} / {result.Frame}");
                EditorGUILayout.LabelField("来源 / 目标", $"{result.SourceActorId} / {result.TargetActorId}");
                EditorGUILayout.LabelField("来源节点", result.OriginContextId.ToString());
                if (c.HasCalculation)
                {
                    EditorGUILayout.LabelField("基础 / 原始伤害", $"{DamageValue(c.BaseDamageRaw)} / {DamageValue(c.RawDamageRaw)}");
                    EditorGUILayout.LabelField("减免后 / 护盾计划", $"{DamageValue(c.MitigatedDamageRaw)} / {DamageValue(c.ShieldAbsorbRaw)}");
                    EditorGUILayout.LabelField("HP 计划 / 实际", $"{DamageValue(c.PlannedHpDamageRaw)} / {DamageValue(c.AppliedHpDamageRaw)}");
                }
                else EditorGUILayout.LabelField("计算数值", "未采集");
                if (!string.IsNullOrEmpty(result.Detail))
                    EditorGUILayout.LabelField("详情", result.Detail, EditorStyles.wordWrappedLabel);
            }
        }

        private static string DamageValue(long raw) => BattleDiagnosticDamageCalculationPayload.ToDisplayValue(raw).ToString("0.###");

        private static void DrawActionActorValues(string role, BattleDiagnosticActionActorValues before,
            BattleDiagnosticActionActorValues after, bool hasAfter)
        {
            EditorGUILayout.LabelField($"{role} Actor", before.ActorId.ToString());
            EditorGUILayout.LabelField($"{role}绑定 前 / 后", $"{before.BindingId} / {(hasAfter ? after.BindingId.ToString() : "未采集")}");
            var sameBinding = hasAfter && before.IsSameBinding(after);
            if (hasAfter && !sameBinding)
                EditorGUILayout.LabelField($"{role}身份", !before.HasActor || !after.HasActor ? "实体缺失，差值不可用" : "实体绑定已更换，差值不可用");
            DrawActionResource($"{role} HP", before.HasHp, before.Hp, hasAfter && after.HasHp, after.Hp, sameBinding);
            DrawActionResource($"{role} Mana", before.HasMana, before.Mana, hasAfter && after.HasMana, after.Mana, sameBinding);
        }

        private static void DrawActionResource(string label, bool hasBefore, float before, bool hasAfter, float after, bool sameBinding)
        {
            var left = hasBefore ? before.ToString("0.###") : "缺失";
            var right = hasAfter ? after.ToString("0.###") : "缺失 / 未采集";
            var delta = hasBefore && hasAfter && sameBinding ? (after - before).ToString("+0.###;-0.###;0") : "不可用";
            EditorGUILayout.LabelField(label + " 前 / 后 / 差值", $"{left} / {right} / {delta}");
        }

        private static void DrawExecutionFacts(in BattleDiagnosticTraceNodeSummary node)
        {
            if (node.Kind != "EffectExecution") return;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("执行入口快照", EditorStyles.boldLabel);
            var facts = node.ExecutionFacts;
            if (!facts.IsCaptured)
            {
                EditorGUILayout.LabelField("状态", facts.Availability == BattleDiagnosticDataAvailability.Evicted ? "已淘汰" : "未采集 / 不可用");
                return;
            }
            EditorGUILayout.LabelField("记录 / 代次", $"{facts.SnapshotId} / {facts.Generation}");
            EditorGUILayout.LabelField("采集帧", facts.Frame.ToString());
            EditorGUILayout.LabelField("类型", facts.TypeId);
            EditorGUILayout.LabelField("结构版本", facts.SchemaVersion.ToString());
            EditorGUILayout.LabelField("Payload 类型", facts.PayloadTypeName);
            EditorGUILayout.LabelField("效果配置 / 触发计划", $"{facts.EffectConfigId} / {facts.TriggerId}");
            EditorGUILayout.LabelField("Runtime Context", facts.HasRuntimeContext ? facts.RuntimeContextId.ToString() : "未提供");
            if (facts.HasRuntimeContext) EditorGUILayout.LabelField("Runtime 版本", facts.RuntimeContextVersion.ToString());
            if (!facts.HasStageSnapshot)
            {
                EditorGUILayout.LabelField("阶段数值", "未提供");
                return;
            }
            EditorGUILayout.LabelField("层数", facts.StackCount.ToString());
            EditorGUILayout.LabelField("已持续 / 秒", facts.ElapsedSeconds.ToString("0.###"));
            EditorGUILayout.LabelField("剩余 / 秒", facts.RemainingSeconds.ToString("0.###"));
            EditorGUILayout.LabelField("总时长 / 秒", facts.DurationSeconds.ToString("0.###"));
        }

        private void DrawActorRoute(
            in BattleDebugContext ctx,
            in BattleDiagnosticTraceNodeSummary node)
        {
            var offline = BattleDebugOfflineRelations.Draw(in ctx, node.Scope, "来源", node.SourceObject, node.StartFrame);
            DrawActorButtons(in ctx, in node, node.SourceObject, "来源", offline);
            BattleDebugOfflineRelations.Draw(in ctx, node.Scope, "目标", node.TargetObject, node.StartFrame);
            DrawActorButtons(in ctx, in node, node.TargetObject, "目标", offline);
        }

        private void DrawActorButtons(
            in BattleDebugContext ctx,
            in BattleDiagnosticTraceNodeSummary node,
            BattleDiagnosticRuntimeObjectReference reference,
            string role,
            bool offline)
        {
            if (reference.Kind != BattleDiagnosticRuntimeObjectKind.Actor) return;
            var actorId = reference.RuntimeId;
            EditorGUILayout.BeginHorizontal();
            if (!offline)
            {
                using (new EditorGUI.DisabledScope(actorId <= 0 || ctx.SelectActor == null))
                {
                    if (GUILayout.Button(actorId > 0 ? $"{role} #{actorId}" : $"{role} -", GUILayout.Width(112)))
                        ctx.SelectActor?.Invoke(actorId);
                }
            }
            using (new EditorGUI.DisabledScope(actorId <= 0 || actorId > int.MaxValue ||
                       ctx.OpenTraceActorAttributes == null || !BattleDiagnosticFrames.IsValid(node.StartFrame)))
            {
                if (GUILayout.Button(new GUIContent("查看属性", "查询此节点起始帧的属性证据，不移动回放"),
                        GUILayout.Width(76)))
                    ctx.OpenTraceActorAttributes?.Invoke(node, actorId);
            }
            var scope = ctx.WorkspaceState?.Scope ?? default;
            var canPreview = BattleDebugNavigation.TryCreateTraceAttributeRoute(
                scope, in node, actorId, out _, out var evidence);
            using (new EditorGUI.DisabledScope(!canPreview))
            {
                if (GUILayout.Button(new GUIContent("在此查看", "保留 Trace 树，在详情中查看同帧属性"),
                        GUILayout.Width(76)))
                {
                    var close = _attributeEvidence == evidence;
                    ResetEvidence();
                    if (!close) _attributeEvidence = evidence;
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRelatedEvidence(in BattleDebugContext ctx, in BattleDiagnosticTraceNodeSummary node)
        {
            if (GUILayout.Button(_showRelatedObjects ? "收起关联对象" : "关联运行时对象", GUILayout.Width(126)))
            {
                var show = !_showRelatedObjects;
                ResetEvidence();
                _showRelatedObjects = show;
            }

            if (_attributeEvidence.IsValid)
            {
                BattleDebugAttributesPanel.DrawTraceEvidence(in ctx, in _attributeEvidence,
                    _attributeEvidenceViewModel, ref _evidenceScroll, showLatestShortcut: false);
                return;
            }
            if (!_showRelatedObjects) return;

            EditorGUILayout.LabelField($"同根对象 · Trace #{node.RootContextId}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("直接关联仅表示对象记录的上下文等于选中节点；不推断归属。",
                EditorStyles.wordWrappedMiniLabel);
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session) ||
                session.SessionInfo.Scope != node.Scope ||
                !session.SessionInfo.Supports(BattleDiagnosticCapabilities.RuntimeObjects))
            {
                EditorGUILayout.HelpBox("此来源未提供运行时对象目录。", MessageType.Info);
                return;
            }

            var model = _relatedObjectsViewModel;
            if (model.RootContextId != node.RootContextId)
            {
                model.RootContextId = node.RootContextId;
                model.ClearSelection();
                _evidenceScroll = Vector2.zero;
            }
            model.RefreshIfNeeded(session);
            if (model.Items.Count == 0)
            {
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(model.QueryStatus, subject: "同根运行时对象"));
                return;
            }

            _evidenceScroll = EditorGUILayout.BeginScrollView(_evidenceScroll, GUILayout.MaxHeight(200));
            foreach (var item in model.Items)
            {
                var relation = item.ContextId == node.ContextId ? "直接" : "同根";
                var label = $"{relation} · {BattleDebugDisplayText.RuntimeObjectKind(item.Kind)} #{item.RuntimeId} · {item.DisplayName}";
                if (GUILayout.Button(new GUIContent(label, item.Reference.ToString()), EditorStyles.miniButton))
                    model.Select(in item);
            }
            EditorGUILayout.EndScrollView();

            if (model.HasMore && GUILayout.Button("加载更多对象", GUILayout.Width(110)))
                model.LoadMore(session);
            if (!string.IsNullOrEmpty(model.PagingStatusMessage))
                EditorGUILayout.HelpBox(model.PagingStatusMessage, MessageType.Info);
            if (!model.Selected.HasValue) return;

            var selected = model.Selected.Value;
            EditorGUILayout.LabelField($"{selected.Reference} · {BattleDebugDisplayText.RuntimeObjectState(selected.State)} · {BattleDebugDisplayText.Completeness(selected.Completeness)}",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField($"定义 {selected.DefinitionKind}:{selected.DefinitionId} · 上下文 #{selected.ContextId}",
                EditorStyles.wordWrappedMiniLabel);
            if (!BattleDebugOfflineRelations.DrawObjectActors(in ctx, in selected, BattleDebugOfflineRelations.ObjectFrame(in selected)))
                EditorGUILayout.LabelField($"来源 #{selected.SourceActorId} · 所有者 #{selected.OwnerActorId} · 目标 #{selected.TargetActorId}",
                    EditorStyles.wordWrappedMiniLabel);
            DrawLinkedNode(in ctx, session.SessionInfo.Scope, selected.RootContextId, selected.ContextId);
        }

        private void ResetEvidence()
        {
            _attributeEvidence = default;
            _showRelatedObjects = false;
            _evidenceScroll = Vector2.zero;
        }

        private void SelectNode(
            in BattleDebugContext ctx,
            in BattleDiagnosticTraceNodeSummary node)
        {
            ClearTimelineSelection();
            _viewModel.SelectContext(node.ContextId);
            ctx.WorkspaceState?.Select(BattleDebugNavigation.CreateTraceSelection(in node));
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
            ClearTimelineSelection();
            ResetEvidence();
            if (rootContextId <= 0) return;

            _rootContextIdText = rootContextId.ToString();
            _pendingRootContextId = rootContextId;
            _pendingContextId = contextId > 0 ? contextId : rootContextId;
            _treeScroll = Vector2.zero;
            _waterfallScroll = Vector2.zero;
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

        private string BuildNodeTooltip(in BattleDebugDiagnosticTraceRow row)
        {
            var node = row.Node;
            return $"{BattleDebugDisplayText.TraceKind(node.Kind)} #{node.ContextId}\n" +
                   $"父节点 #{node.ParentContextId}，深度 {row.Depth}\n" +
                   $"{BuildConfigText(in node)}，触发器 {FormatId(node.TriggerId)}\n" +
                   (node.HasOrigin ? $"直接来源：{BuildOriginText(in node)}\n" : string.Empty) +
                   $"{BuildActorRoute(in node)}\n" +
                   $"{BuildFrameSpan(in node)}, {BuildResultText(in node)}";
        }

        private static string BuildActorRoute(in BattleDiagnosticTraceNodeSummary node)
        {
            return $"来源 {FormatId(node.SourceActorId)} -> 目标 {FormatId(node.TargetActorId)}";
        }

        private string BuildConfigText(in BattleDiagnosticTraceNodeSummary node)
        {
            if (string.Equals(node.Kind, "EffectExecution", System.StringComparison.Ordinal))
            {
                return node.TriggerId > 0
                    ? $"{FormatDefinition("效果", node.Definition)} / " +
                      FormatDefinition("触发器", node.TriggerDefinition)
                    : FormatDefinition("效果", node.Definition);
            }
            if (string.Equals(node.Kind, "EffectAction", System.StringComparison.Ordinal))
            {
                return FormatDefinition("动作", node.Definition);
            }
            if (string.Equals(node.Kind, "SkillCast", System.StringComparison.Ordinal) ||
                string.Equals(node.Kind, "SkillEffect", System.StringComparison.Ordinal) ||
                string.Equals(node.Kind, "SkillPhase", System.StringComparison.Ordinal))
            {
                var skill = node.SkillDefinition.IsResolved
                    ? node.SkillDefinition
                    : node.Definition;
                return FormatDefinition("技能", skill);
            }
            return FormatDefinition("配置", node.Definition);
        }

        private string FormatDefinition(
            string label,
            BattleDiagnosticDefinitionReference reference)
        {
            if (!reference.HasDefinitionId) return label + " -";
            var displayName = _viewModel.GetDefinitionDisplayName(reference);
            return string.IsNullOrEmpty(displayName)
                ? $"{label} {reference.DefinitionId}"
                : $"{label} {displayName} (#{reference.DefinitionId})";
        }

        private string BuildOriginText(in BattleDiagnosticTraceNodeSummary node)
        {
            var kind = BattleDebugDisplayText.TraceKind(((MobaExecutionKind)node.OriginKind).ToString());
            return kind + " / " + FormatDefinition("来源定义", node.OriginDefinition);
        }

        private void DrawDefinitionDetails(in BattleDiagnosticTraceNodeSummary node)
        {
            BattleDiagnosticDefinition definition;
            BattleDiagnosticDefinition trigger = null;
            BattleDiagnosticDefinition skill = null;
            BattleDiagnosticDefinition origin = null;
            var hasDefinition = _viewModel.TryGetDefinition(
                node.Definition,
                out definition);
            var hasTrigger = node.TriggerDefinition != node.Definition &&
                             _viewModel.TryGetDefinition(
                                 node.TriggerDefinition,
                                 out trigger);
            var hasSkill = node.SkillDefinition != node.Definition &&
                           node.SkillDefinition != node.TriggerDefinition &&
                           _viewModel.TryGetDefinition(
                               node.SkillDefinition,
                               out skill);
            var hasOrigin = node.OriginDefinition != node.Definition &&
                            node.OriginDefinition != node.TriggerDefinition &&
                            node.OriginDefinition != node.SkillDefinition &&
                            _viewModel.TryGetDefinition(node.OriginDefinition, out origin);
            if (!hasDefinition && !hasTrigger && !hasSkill && !hasOrigin) return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                $"定义映射（版本 {_viewModel.DefinitionStoreRevision}）",
                EditorStyles.boldLabel);
            if (hasDefinition) DrawDefinition("节点定义", definition);
            if (hasTrigger) DrawDefinition("触发器定义", trigger);
            if (hasSkill) DrawDefinition("技能定义", skill);
            if (hasOrigin) DrawDefinition("直接来源定义", origin);
        }

        private static void DrawDefinition(
            string label,
            BattleDiagnosticDefinition definition)
        {
            var title = definition.IsResolved
                ? $"{definition.DisplayName}  [{definition.Kind} #{definition.DefinitionId}]"
                : $"未解析  [{definition.Kind} #{definition.DefinitionId}]";
            EditorGUILayout.LabelField(label, title);
            if (!definition.IsResolved) return;
            if (!string.IsNullOrEmpty(definition.SourcePath))
            {
                EditorGUILayout.LabelField("来源", definition.SourcePath);
            }
            for (var i = 0; i < definition.Metadata.Count; i++)
            {
                var item = definition.Metadata[i];
                EditorGUILayout.LabelField("  " + item.Key, FormatMetadataValue(in item));
            }
        }

        private static string FormatMetadataValue(
            in BattleDiagnosticDefinitionMetadataEntry item)
        {
            switch (item.ValueKind)
            {
                case BattleDiagnosticDefinitionMetadataValueKind.String:
                    return item.StringValue;
                case BattleDiagnosticDefinitionMetadataValueKind.Integer:
                    return item.IntegerValue.ToString();
                case BattleDiagnosticDefinitionMetadataValueKind.Number:
                    return item.NumberValue.ToString("0.###");
                case BattleDiagnosticDefinitionMetadataValueKind.Boolean:
                    return item.BooleanValue ? "true" : "false";
                default:
                    return "-";
            }
        }

        private static string BuildFrameSpan(in BattleDiagnosticTraceNodeSummary node)
        {
            return node.EndFrame >= 0
                ? $"F{node.StartFrame} - F{node.EndFrame}  [{node.EndFrame - node.StartFrame} 帧]"
                : $"F{node.StartFrame} - 进行中";
        }

        private static string BuildResultText(in BattleDiagnosticTraceNodeSummary node)
        {
            if (node.State == BattleDiagnosticTraceNodeState.Active) return "进行中";
            if (!string.IsNullOrEmpty(node.EndReason)) return node.EndReason;
            return BattleDebugDisplayText.TraceState(node.State);
        }

        private static string BuildSummaryFrameText(in BattleDebugDiagnosticTraceSummary summary)
        {
            if (summary.ActiveCount > 0) return $"F{summary.FirstFrame} -> 进行中";
            return summary.LastFrame >= summary.FirstFrame
                ? $"F{summary.FirstFrame} -> F{summary.LastFrame}  [{summary.LastFrame - summary.FirstFrame} 帧]"
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
                parts[i] = $"{BattleDebugDisplayText.TraceKind(node.Kind)}#{node.ContextId}";
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
