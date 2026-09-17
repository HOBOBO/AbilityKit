using System.Collections.Generic;
using System.Text;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal enum BattleDebugDiagnosticEventsPresentation
    {
        Overview = 0,
        TriggerFlow = 1,
        SkillCasts = 2,
        Events = 3
    }

    /// <summary>
    /// 有界历史事件面板：通过 <see cref="BattleDebugDiagnosticEventsViewModel"/>
    /// 查询并展示伤害、治疗、效果和其他运行事件，支持时间窗口、Actor 关系和文本检索。
    /// 只消费已定义的诊断查询契约，不建立旁路数据源。
    /// </summary>
    [BattleDebugModule(
        BattleDebugModuleIds.DiagnosticEvents,
        "调查",
        RequiredCapabilities = BattleDiagnosticCapabilities.Events,
        Selections = BattleDebugModuleSelectionSupport.Frame |
                     BattleDebugModuleSelectionSupport.Actor |
                     BattleDebugModuleSelectionSupport.Event |
                     BattleDebugModuleSelectionSupport.Trace |
                     BattleDebugModuleSelectionSupport.Config)]
    internal sealed class BattleDebugDiagnosticEventsPanel :
        IBattleDebugPanel,
        IBattleDebugPanelLayout,
        IBattleDebugEventsTarget,
        IBattleDebugWidgetProvider
    {
        private static readonly string[] EventScopeLabels =
        {
            "伤害与效果", "伤害与治疗", "效果", "技能", "Buff", "临时实体", "警告", "触发", "全部事件", "目标搜索", "输入命令"
        };
        private static readonly string[] ActorRelationLabels = { "任意关系", "来源", "目标", "来源或目标" };
        private static readonly string[] TriggerStageLabels = { "全部阶段", "预算", "条件", "计划", "执行" };
        private static readonly string[] TriggerResultLabels = { "全部结果", "通过", "失败", "阻止", "跳过" };
        private static readonly string[] InvestigationConfidenceLabels = { "全部置信度", "已确认", "推断", "证据不足" };
        private static readonly string[] InvestigationCauseLabels =
        {
            "全部原因", "技能失败", "触发条件失败", "触发预算阻止", "触发计划被拒绝",
            "触发执行失败", "效果执行失败", "运行时失败", "未知原因"
        };

        public string Name => "诊断事件";
        public int Order => 400;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => true;

        private readonly BattleDebugDiagnosticEventsViewModel _viewModel = new BattleDebugDiagnosticEventsViewModel();
        private Vector2 _scroll;
        private BattleDiagnosticEvent? _selectedEvent;
        internal BattleDiagnosticEvent? SelectedEvent => _selectedEvent;
        private BattleDebugSkillInvestigationCase? _selectedInvestigation;
        private BattleDebugInvestigationConfidenceFilter _investigationConfidenceFilter;
        private BattleDebugInvestigationCauseFilter _investigationCauseFilter;
        private string _actionStatus = string.Empty;
        private readonly IBattleDebugWidget[] _widgets;
        private readonly List<BattleDebugHistogramSample> _histogramSamples =
            new List<BattleDebugHistogramSample>(256);
        private readonly List<BattleDiagnosticEvent> _timeRangeItems =
            new List<BattleDiagnosticEvent>(256);
        private readonly List<BattleDebugTimelineOverviewItem> _overviewItems =
            new List<BattleDebugTimelineOverviewItem>(256);
        private readonly BattleDebugTimelineOverviewBuffer _overviewBuffer =
            new BattleDebugTimelineOverviewBuffer();
        private Vector2 _flowScroll;
        private Vector2 _castScroll;
        private string _expandedSkillCastKey = string.Empty;
        private BattleDebugDiagnosticEventsPresentation _presentation =
            BattleDebugDiagnosticEventsPresentation.Overview;
        private readonly BattleDebugHistogramSeries[] _histogramSeries =
        {
            new BattleDebugHistogramSeries("成功", new Color(0.3f, 0.72f, 0.42f, 0.9f)),
            new BattleDebugHistogramSeries("其他", new Color(0.48f, 0.62f, 0.78f, 0.9f)),
            new BattleDebugHistogramSeries("问题", new Color(0.9f, 0.38f, 0.32f, 0.95f))
        };
        private readonly BattleDebugLegendItem[] _histogramLegend =
        {
            new BattleDebugLegendItem("成功", new Color(0.3f, 0.72f, 0.42f, 0.9f)),
            new BattleDebugLegendItem("其他", new Color(0.48f, 0.62f, 0.78f, 0.9f)),
            new BattleDebugLegendItem("失败 / 取消 / 中断", new Color(0.9f, 0.38f, 0.32f, 0.95f))
        };
        private bool _showAdvancedFilters;
        private bool _showWidgetAdvancedFilters;
        private bool _triggerFlowWidgetInitialized;

        public BattleDebugDiagnosticEventsPanel()
        {
            _widgets = new IBattleDebugWidget[]
            {
                new EventsWidget(this, EventsWidgetKind.Overview),
                new EventsWidget(this, EventsWidgetKind.TriggerFlow),
                new EventsWidget(this, EventsWidgetKind.List),
                new EventsWidget(this, EventsWidgetKind.Details)
            };
        }

        public IReadOnlyList<IBattleDebugWidget> Widgets => _widgets;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void OpenForActor(long actorId)
        {
            _viewModel.ClearCorrelationFocus();
            _viewModel.FilterBySelectedActor = actorId > 0;
            _viewModel.ActorRelation = BattleDiagnosticActorRelation.Either;
            _viewModel.FailuresOnly = false;
            _viewModel.SearchText = string.Empty;
            _viewModel.InvalidateCache();
            _presentation = BattleDebugDiagnosticEventsPresentation.Overview;
            ClearSelection();
        }

        public void OpenEvent(
            in BattleDiagnosticEvent diagnosticEvent,
            BattleDiagnosticWorkspaceState workspaceState)
        {
            _selectedEvent = diagnosticEvent;
            _selectedInvestigation = null;
            _actionStatus = string.Empty;
            _scroll = Vector2.zero;
            _presentation = BattleDebugDiagnosticEventsPresentation.Events;
            workspaceState?.Select(CreateEventSelection(in diagnosticEvent));
        }

        public void OpenRecentFailures()
        {
            _viewModel.FocusRecentFailures();
            _presentation = BattleDebugDiagnosticEventsPresentation.Overview;
            ClearSelection();
        }

        private void ClearSelection()
        {
            _selectedEvent = null;
            _selectedInvestigation = null;
            _actionStatus = string.Empty;
            _scroll = Vector2.zero;
        }

        public void Draw(in BattleDebugContext ctx)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "诊断会话不可用。请启动战斗或打开包含战斗诊断的 Artifact。",
                    MessageType.Info);
                return;
            }

            DrawFilterBar(ctx, session);
            EditorGUILayout.Space(4);

            var selectedActorId = ctx.HasSelection ? ctx.SelectedId.ActorId : 0;
            var workspaceFilter = ctx.WorkspaceState.Filter;
            var items = _viewModel.RefreshIfNeeded(
                session,
                selectedActorId,
                ctx.HasSelection,
                in workspaceFilter);
            RestoreWorkspaceEventSelection(in ctx, items);
            DrawWorksetControls(in ctx, session, selectedActorId);
            items = ApplySharedTimeRange(in ctx, _viewModel.Items);
            DrawPresentationTabs(in ctx, items);
            var useSplitLayout = (_selectedEvent.HasValue || _selectedInvestigation.HasValue) &&
                                 EditorGUIUtility.currentViewWidth >= 900f;
            if (useSplitLayout)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.MinWidth(420f));
                DrawPrimaryContent(in ctx, items, expandHeight: true);
                EditorGUILayout.EndVertical();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(380f));
                DrawSelectionDetails(in ctx, items);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawPrimaryContent(in ctx, items, expandHeight: false);
                DrawSelectionDetails(in ctx, items);
            }
        }

        private void DrawOverviewWidget(in BattleDebugContext ctx)
        {
            if (!TryRefresh(in ctx, out _, out var items)) return;

            DrawFrameHistogram(in ctx, _viewModel.Items, items);
            DrawInvestigations(in ctx, items);
            DrawIssueGroups();
        }

        private void DrawListWidget(in BattleDebugContext ctx)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                DrawSessionUnavailable();
                return;
            }

            if (ctx.AvailableContentWidth >= 980f)
            {
                DrawFilterBar(ctx, session);
            }
            else
            {
                DrawCompactWidgetFilterBar(in ctx);
            }
            EditorGUILayout.Space(3f);
            if (!TryRefresh(in ctx, session, out var items)) return;

            var selectedActorId = ctx.HasSelection ? ctx.SelectedId.ActorId : 0;
            DrawWorksetControls(in ctx, session, selectedActorId);
            DrawEventList(in ctx, items, expandHeight: true);
        }

        private void DrawTriggerFlowWidget(in BattleDebugContext ctx)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                DrawSessionUnavailable();
                return;
            }

            if (!_triggerFlowWidgetInitialized ||
                (_viewModel.EventScope != BattleDebugDiagnosticEventScope.Triggers &&
                 _viewModel.EventScope != BattleDebugDiagnosticEventScope.All))
            {
                _triggerFlowWidgetInitialized = true;
                _viewModel.FocusTriggerFlows();
            }

            if (ctx.AvailableContentWidth >= 980f)
            {
                DrawFilterBar(ctx, session);
            }
            else
            {
                DrawCompactWidgetFilterBar(in ctx);
            }
            EditorGUILayout.Space(3f);
            if (!TryRefresh(in ctx, session, out var items)) return;

            var selectedActorId = ctx.HasSelection ? ctx.SelectedId.ActorId : 0;
            DrawWorksetControls(in ctx, session, selectedActorId);
            DrawTriggerFlowWorkspace(in ctx, items, expandHeight: true);
        }

        private void DrawCompactWidgetFilterBar(in BattleDebugContext ctx)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var nextScope = (BattleDebugDiagnosticEventScope)EditorGUILayout.Popup(
                (int)_viewModel.EventScope,
                EventScopeLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(92f));
            if (nextScope != _viewModel.EventScope)
            {
                _viewModel.EventScope = nextScope;
                _viewModel.InvalidateCache();
            }

            var nextRecentFrames = Mathf.Max(0, EditorGUILayout.IntField(
                _viewModel.RecentFrameCount,
                EditorStyles.toolbarTextField,
                GUILayout.Width(48f)));
            if (nextRecentFrames != _viewModel.RecentFrameCount)
            {
                _viewModel.RecentFrameCount = nextRecentFrames;
                _viewModel.InvalidateCache();
            }

            var nextActor = GUILayout.Toggle(
                _viewModel.FilterBySelectedActor,
                new GUIContent("Actor", "仅显示与当前选中 Actor 相关的事件"),
                EditorStyles.toolbarButton,
                GUILayout.Width(48f));
            if (nextActor != _viewModel.FilterBySelectedActor)
            {
                _viewModel.FilterBySelectedActor = nextActor;
                _viewModel.InvalidateCache();
            }

            var nextFailures = GUILayout.Toggle(
                _viewModel.FailuresOnly,
                new GUIContent("失败", "仅显示失败、取消或中断的事件"),
                EditorStyles.toolbarButton,
                GUILayout.Width(42f));
            if (nextFailures != _viewModel.FailuresOnly)
            {
                _viewModel.FailuresOnly = nextFailures;
                _viewModel.InvalidateCache();
            }

            DrawTriggerValueMode(104f);

            GUILayout.FlexibleSpace();
            _showWidgetAdvancedFilters = GUILayout.Toggle(
                _showWidgetAdvancedFilters,
                new GUIContent("高级", "显示配置与触发分析过滤条件"),
                EditorStyles.toolbarButton,
                GUILayout.Width(42f));
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(42f)))
            {
                _viewModel.InvalidateCache();
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("搜索", GUILayout.Width(30f));
            var nextSearch = GUILayout.TextField(
                _viewModel.SearchText ?? string.Empty,
                EditorStyles.toolbarSearchField,
                GUILayout.MinWidth(80f));
            if (!string.Equals(nextSearch, _viewModel.SearchText, System.StringComparison.Ordinal))
            {
                _viewModel.SearchText = nextSearch;
                _viewModel.InvalidateCache();
            }
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_viewModel.SearchText));
            if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                _viewModel.SearchText = string.Empty;
                _viewModel.InvalidateCache();
                GUI.FocusControl(null);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (!_showWidgetAdvancedFilters) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("配置", GUILayout.Width(30f));
            var nextConfigId = Mathf.Max(0, EditorGUILayout.IntField(
                _viewModel.ConfigId,
                EditorStyles.toolbarTextField,
                GUILayout.Width(52f)));
            if (nextConfigId != _viewModel.ConfigId)
            {
                _viewModel.ConfigId = nextConfigId;
                _viewModel.InvalidateCache();
            }

            var nextStage = (BattleDiagnosticTriggerAnalysisStage)EditorGUILayout.Popup(
                (int)_viewModel.TriggerStage,
                TriggerStageLabels,
                EditorStyles.toolbarPopup,
                GUILayout.MinWidth(70f));
            if (nextStage != _viewModel.TriggerStage)
            {
                _viewModel.TriggerStage = nextStage;
                _viewModel.InvalidateCache();
            }

            var nextResult = (BattleDiagnosticTriggerAnalysisResult)EditorGUILayout.Popup(
                (int)_viewModel.TriggerResult,
                TriggerResultLabels,
                EditorStyles.toolbarPopup,
                GUILayout.MinWidth(70f));
            if (nextResult != _viewModel.TriggerResult)
            {
                _viewModel.TriggerResult = nextResult;
                _viewModel.InvalidateCache();
            }

            EditorGUI.BeginDisabledGroup(!_viewModel.HasActiveFilter);
            if (GUILayout.Button("清除", EditorStyles.toolbarButton, GUILayout.Width(42f)))
            {
                _viewModel.ClearLocalFilters();
                GUI.FocusControl(null);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDetailsWidget(in BattleDebugContext ctx)
        {
            if (!TryRefresh(in ctx, out _, out var items)) return;
            if (!_selectedEvent.HasValue && !_selectedInvestigation.HasValue)
            {
                EditorGUILayout.HelpBox("从事件列表或调查结果中选择一项以查看详情。", MessageType.Info);
                return;
            }

            if (_selectedInvestigation.HasValue)
            {
                DrawInvestigations(in ctx, items);
            }
            DrawSelectionDetails(in ctx, items);
        }

        private bool TryRefresh(
            in BattleDebugContext ctx,
            out IBattleDiagnosticReadOnlySession session,
            out IReadOnlyList<BattleDiagnosticEvent> items)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out session))
            {
                items = System.Array.Empty<BattleDiagnosticEvent>();
                DrawSessionUnavailable();
                return false;
            }

            return TryRefresh(in ctx, session, out items);
        }

        private bool TryRefresh(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session,
            out IReadOnlyList<BattleDiagnosticEvent> items)
        {
            var selectedActorId = ctx.HasSelection ? ctx.SelectedId.ActorId : 0;
            var workspaceFilter = ctx.WorkspaceState?.Filter ?? BattleDiagnosticFilter.Default;
            items = _viewModel.RefreshIfNeeded(
                session,
                selectedActorId,
                ctx.HasSelection,
                in workspaceFilter);
            RestoreWorkspaceEventSelection(in ctx, items);
            items = ApplySharedTimeRange(in ctx, items);
            return true;
        }

        private IReadOnlyList<BattleDiagnosticEvent> ApplySharedTimeRange(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items)
        {
            var timeRange = ctx.WorkspaceState?.TimeRange ??
                            BattleDiagnosticTimeRange.Auto();
            if (!timeRange.IsFixed || items == null || items.Count == 0)
            {
                return items ?? System.Array.Empty<BattleDiagnosticEvent>();
            }

            _timeRangeItems.Clear();
            var range = timeRange.Range;
            for (var i = 0; i < items.Count; i++)
            {
                if (range.Contains(items[i].Frame))
                {
                    _timeRangeItems.Add(items[i]);
                }
            }
            return _timeRangeItems;
        }

        private static void DrawSessionUnavailable()
        {
            EditorGUILayout.HelpBox(
                "诊断会话不可用。请启动战斗或打开包含战斗诊断的 Artifact。",
                MessageType.Info);
        }

        private void DrawFilterBar(in BattleDebugContext ctx, IBattleDiagnosticReadOnlySession session)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("类别", GUILayout.Width(30));
            var newScope = (BattleDebugDiagnosticEventScope)EditorGUILayout.Popup(
                (int)_viewModel.EventScope,
                EventScopeLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(112));
            if (newScope != _viewModel.EventScope)
            {
                _viewModel.EventScope = newScope;
                _viewModel.InvalidateCache();
            }

            var newFilterActor = GUILayout.Toggle(
                _viewModel.FilterBySelectedActor,
                new GUIContent("选中 Actor", "仅显示与当前选中 Actor 相关的事件"),
                EditorStyles.toolbarButton,
                GUILayout.Width(72f));
            if (newFilterActor != _viewModel.FilterBySelectedActor)
            {
                _viewModel.FilterBySelectedActor = newFilterActor;
                _viewModel.InvalidateCache();
            }

            if (_viewModel.FilterBySelectedActor)
            {
                var newRelation = (BattleDiagnosticActorRelation)EditorGUILayout.Popup(
                    (int)_viewModel.ActorRelation,
                    ActorRelationLabels,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(65));
                if (newRelation == BattleDiagnosticActorRelation.Any)
                {
                    newRelation = BattleDiagnosticActorRelation.Either;
                }

                if (newRelation != _viewModel.ActorRelation)
                {
                    _viewModel.ActorRelation = newRelation;
                    _viewModel.InvalidateCache();
                }
            }

            var newFailures = GUILayout.Toggle(
                _viewModel.FailuresOnly,
                new GUIContent("仅失败", "只显示失败、取消或中断事件"),
                EditorStyles.toolbarButton,
                GUILayout.Width(52f));
            if (newFailures != _viewModel.FailuresOnly)
            {
                _viewModel.FailuresOnly = newFailures;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("最近", GUILayout.Width(30f));
            var newRecentFrameCount = Mathf.Max(0, EditorGUILayout.IntField(
                _viewModel.RecentFrameCount,
                EditorStyles.toolbarTextField,
                GUILayout.Width(52f)));
            if (newRecentFrameCount != _viewModel.RecentFrameCount)
            {
                _viewModel.RecentFrameCount = newRecentFrameCount;
                _viewModel.InvalidateCache();
            }
            GUILayout.Label("帧", GUILayout.Width(16f));

            GUILayout.Label("搜索", GUILayout.Width(30));
            var newSearch = GUILayout.TextField(
                _viewModel.SearchText ?? string.Empty,
                EditorStyles.toolbarSearchField,
                GUILayout.MinWidth(120f));
            if (!string.Equals(newSearch, _viewModel.SearchText, System.StringComparison.Ordinal))
            {
                _viewModel.SearchText = newSearch;
                _viewModel.InvalidateCache();
            }
            GUILayout.FlexibleSpace();
            _showAdvancedFilters = GUILayout.Toggle(
                _showAdvancedFilters,
                new GUIContent("高级", "显示配置和触发模块的强类型过滤字段"),
                EditorStyles.toolbarButton,
                GUILayout.Width(48f));
            if (GUILayout.Button(
                    new GUIContent("↻", "刷新当前调查工作集"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(28f)))
            {
                _viewModel.InvalidateCache();
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("快捷", EditorStyles.miniLabel, GUILayout.Width(30f));
            if (GUILayout.Button(new GUIContent("最近失败", "查看最近 600 帧内所有通道的失败事件"), EditorStyles.toolbarButton, GUILayout.Width(62)))
            {
                _viewModel.FocusRecentFailures();
                _presentation = BattleDebugDiagnosticEventsPresentation.Overview;
                _selectedEvent = null;
                _selectedInvestigation = null;
                _actionStatus = "已切换到最近失败调查。";
            }
            if (GUILayout.Button(new GUIContent("条件失败", "查看条件判定未通过的触发分析事件"), EditorStyles.toolbarButton, GUILayout.Width(62)))
            {
                _viewModel.FocusConditionFailures();
                _presentation = BattleDebugDiagnosticEventsPresentation.Overview;
                _selectedEvent = null;
                _selectedInvestigation = null;
                _actionStatus = "已切换到触发条件失败调查。";
            }
            if (GUILayout.Button(new GUIContent("预算阻断", "查看因递归、帧或根触发预算被阻断的事件"), EditorStyles.toolbarButton, GUILayout.Width(62)))
            {
                _viewModel.FocusTriggerBlocks();
                _presentation = BattleDebugDiagnosticEventsPresentation.Overview;
                _selectedEvent = null;
                _selectedInvestigation = null;
                _actionStatus = "已切换到触发预算阻断调查。";
            }
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(!_viewModel.HasActiveFilter);
            if (GUILayout.Button(
                    new GUIContent("清除", "清除事件面板调查条件，不修改共享筛选"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(46f)))
            {
                _viewModel.ClearLocalFilters();
                GUI.FocusControl(null);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!_viewModel.HasActiveFilter);
            if (GUILayout.Button(
                    new GUIContent(
                        "设为共享",
                        "将当前事件面板的可共享条件设为工作区筛选；最近帧仅是本面板工作集窗口，不会共享"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(62f)))
            {
                var hadRecentFrameWindow = _viewModel.RecentFrameCount > 0;
                var localFilter = _viewModel.BuildLocalFilter(
                    ctx.HasSelection ? ctx.SelectedId.ActorId : 0,
                    ctx.HasSelection);
                ctx.WorkspaceState.SetFilter(localFilter);
                _viewModel.ClearLocalFilters();
                _actionStatus = hadRecentFrameWindow
                    ? "已共享可共享条件；最近帧工作集窗口已清除且未写入共享筛选。"
                    : "已将事件面板的可共享条件设为共享筛选。";
                GUI.FocusControl(null);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(ctx.WorkspaceState.Filter.ActiveFilterCount == 0);
            if (GUILayout.Button(
                    new GUIContent("清除共享", "清除工作区共享筛选，保留事件面板局部调查条件"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(62f)))
            {
                ctx.WorkspaceState.SetFilter(BattleDiagnosticFilter.Default);
                _viewModel.InvalidateCache();
                _actionStatus = "已清除工作区共享筛选。";
                GUI.FocusControl(null);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (_showAdvancedFilters)
            {
                DrawAdvancedFilterBar();
            }

            if (_viewModel.HasCorrelationFocus)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label("关联调查", EditorStyles.miniBoldLabel, GUILayout.Width(55));
                GUILayout.Label(_viewModel.CorrelationFocusLabel, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("退出聚焦", EditorStyles.miniButton, GUILayout.Width(70)))
                {
                    _viewModel.ClearCorrelationFocus();
                    _actionStatus = string.Empty;
                }
                EditorGUILayout.EndHorizontal();
            }

            var sharedFilter = ctx.WorkspaceState.Filter;
            var actorScope = _viewModel.FilterBySelectedActor
                ? (ctx.HasSelection
                    ? $"Actor={ctx.SelectedId.ActorId}({BattleDebugDisplayText.ActorRelation(_viewModel.ActorRelation)})"
                    : "Actor=未选中")
                : "全部 Actor";
            var frameScope = _viewModel.RecentFrameCount > 0
                ? $"最近 {_viewModel.RecentFrameCount} 帧"
                : "全部保留历史";
            var triggerScope = FormatTriggerFilterSummary();
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label($"{frameScope}  {BattleDebugDisplayText.EventScope(_viewModel.EventScope)}  {actorScope}  {triggerScope}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                $"共享 {sharedFilter.ActiveFilterCount}  实时版本 {_viewModel.StoreRevision}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAdvancedFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("配置", EditorStyles.miniLabel, GUILayout.Width(30f));
            var newConfigId = Mathf.Max(0, EditorGUILayout.IntField(
                _viewModel.ConfigId,
                EditorStyles.toolbarTextField,
                GUILayout.Width(56f)));
            if (newConfigId != _viewModel.ConfigId)
            {
                _viewModel.ConfigId = newConfigId;
                _viewModel.InvalidateCache();
            }

            GUILayout.Space(8f);
            GUILayout.Label("触发事件", EditorStyles.miniLabel, GUILayout.Width(52f));
            DrawTriggerValueMode(112f);
            GUILayout.Space(8f);
            GUILayout.Label("阶段", EditorStyles.miniLabel, GUILayout.Width(30f));
            var newTriggerStage = (BattleDiagnosticTriggerAnalysisStage)EditorGUILayout.Popup(
                (int)_viewModel.TriggerStage,
                TriggerStageLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(90f));
            if (newTriggerStage != _viewModel.TriggerStage)
            {
                _viewModel.TriggerStage = newTriggerStage;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("结果", EditorStyles.miniLabel, GUILayout.Width(30f));
            var newTriggerResult = (BattleDiagnosticTriggerAnalysisResult)EditorGUILayout.Popup(
                (int)_viewModel.TriggerResult,
                TriggerResultLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(90f));
            if (newTriggerResult != _viewModel.TriggerResult)
            {
                _viewModel.TriggerResult = newTriggerResult;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("上下文", EditorStyles.miniLabel, GUILayout.Width(46f));
            var newTriggerContextKind = Mathf.Max(0, EditorGUILayout.IntField(
                _viewModel.TriggerContextKind,
                EditorStyles.toolbarTextField,
                GUILayout.Width(44f)));
            if (newTriggerContextKind != _viewModel.TriggerContextKind)
            {
                _viewModel.TriggerContextKind = newTriggerContextKind;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("来源", EditorStyles.miniLabel, GUILayout.Width(36f));
            var newTriggerOriginKind = Mathf.Max(0, EditorGUILayout.IntField(
                _viewModel.TriggerOriginKind,
                EditorStyles.toolbarTextField,
                GUILayout.Width(44f)));
            if (newTriggerOriginKind != _viewModel.TriggerOriginKind)
            {
                _viewModel.TriggerOriginKind = newTriggerOriginKind;
                _viewModel.InvalidateCache();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTriggerValueMode(float width)
        {
            var valuable = _viewModel.TriggerValueFilter ==
                           BattleDiagnosticTriggerValueFilter.Valuable;
            var halfWidth = Mathf.Max(42f, width * 0.5f);
            if (GUILayout.Toggle(
                    valuable,
                    new GUIContent("高价值", "隐藏逐帧条件判定噪音，保留预算阻断、执行结果和执行失败"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(halfWidth)) && !valuable)
            {
                _viewModel.TriggerValueFilter = BattleDiagnosticTriggerValueFilter.Valuable;
                _viewModel.InvalidateCache();
            }

            if (GUILayout.Toggle(
                    !valuable,
                    new GUIContent("原始", "显示所有触发分析事件，包括逐帧条件通过与未通过"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(halfWidth)) && valuable)
            {
                _viewModel.TriggerValueFilter = BattleDiagnosticTriggerValueFilter.All;
                _viewModel.InvalidateCache();
            }
        }

        private void DrawWorksetControls(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session,
            long selectedActorId)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("调查工作集", EditorStyles.miniBoldLabel, GUILayout.Width(66));
            GUILayout.Label(
                $"{_viewModel.LoadedCount} 条  快照版本={_viewModel.WorksetRevision}",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                _viewModel.HasMore ? "仍有更早结果" : "已到当前快照末尾",
                EditorStyles.miniLabel,
                GUILayout.Width(92));
            EditorGUI.BeginDisabledGroup(!_viewModel.HasMore);
            if (GUILayout.Button(
                    new GUIContent("加载更多", "从当前固定快照追加下一页，不混入新的实时版本数据"),
                    EditorStyles.miniButton,
                    GUILayout.Width(72)))
            {
                var selectedCaseKey = _selectedInvestigation.HasValue
                    ? _selectedInvestigation.Value.Key
                    : string.Empty;
                var workspaceFilter = ctx.WorkspaceState.Filter;
                if (_viewModel.LoadMore(
                        session,
                        selectedActorId,
                        ctx.HasSelection,
                        in workspaceFilter))
                {
                    RestoreInvestigationSelection(selectedCaseKey, _viewModel.Items);
                    _actionStatus = $"调查工作集已扩展到 {_viewModel.LoadedCount} 条。";
                }
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_viewModel.PagingStatusMessage))
            {
                EditorGUILayout.HelpBox(
                    _viewModel.PagingStatusMessage,
                    _viewModel.HasMore ? MessageType.Info : MessageType.Warning);
            }
        }

        private void RestoreInvestigationSelection(
            string selectedCaseKey,
            IReadOnlyList<BattleDiagnosticEvent> items)
        {
            if (string.IsNullOrEmpty(selectedCaseKey)) return;

            var cases = BattleDebugSkillInvestigationModel.Build(
                items,
                _investigationConfidenceFilter,
                _investigationCauseFilter);
            for (var i = 0; i < cases.Count; i++)
            {
                if (!string.Equals(
                        cases[i].Key,
                        selectedCaseKey,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                _selectedInvestigation = cases[i];
                return;
            }

            _selectedInvestigation = null;
        }

        private void DrawPresentationTabs(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items)
        {
            var flows = BattleDebugDiagnosticEventsViewModel.BuildTriggerFlows(items);
            var casts = ResolveSkillCastFlows(items);
            var groups = _viewModel.IssueGroups;
            var issueCount = groups?.Count ?? 0;
            var eventCount = items?.Count ?? 0;
            var compact = ctx.AvailableContentWidth < 620f;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("视图", EditorStyles.miniLabel, GUILayout.Width(30f));
            var next = _presentation;
            if (GUILayout.Toggle(
                    next == BattleDebugDiagnosticEventsPresentation.Overview,
                    new GUIContent(compact ? "概览" : $"概览  {issueCount}", "事件分布、失败调查和问题聚合"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(compact ? 58f : 86f)))
            {
                next = BattleDebugDiagnosticEventsPresentation.Overview;
            }
            if (GUILayout.Toggle(
                    next == BattleDebugDiagnosticEventsPresentation.TriggerFlow,
                    new GUIContent(compact ? "触发" : $"触发流程  {flows.Count}", "按根节点和触发器查看四阶段触发链路"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(compact ? 58f : 104f)))
            {
                next = BattleDebugDiagnosticEventsPresentation.TriggerFlow;
            }
            if (GUILayout.Toggle(
                    next == BattleDebugDiagnosticEventsPresentation.SkillCasts,
                    new GUIContent(compact ? "施法" : $"施法链路  {casts.Count}", "按命令、Trace 和技能运行时聚合一次施法的完整事件链"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(compact ? 58f : 104f)))
            {
                next = BattleDebugDiagnosticEventsPresentation.SkillCasts;
            }
            if (GUILayout.Toggle(
                    next == BattleDebugDiagnosticEventsPresentation.Events,
                    new GUIContent(compact ? "事件" : $"事件  {eventCount}", "查看当前工作集中的原始事件与聚合摘要"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(compact ? 58f : 86f)))
            {
                next = BattleDebugDiagnosticEventsPresentation.Events;
            }

            GUILayout.FlexibleSpace();
            if (_selectedEvent.HasValue)
            {
                GUILayout.Label(
                    $"已选 #{_selectedEvent.Value.Sequence}  F{_selectedEvent.Value.Frame}",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            if (next == _presentation) return;

            _presentation = next;
            _scroll = Vector2.zero;
            _flowScroll = Vector2.zero;
            _castScroll = Vector2.zero;
            if (next == BattleDebugDiagnosticEventsPresentation.TriggerFlow)
            {
                _viewModel.FocusTriggerFlows();
            }
            else if (next == BattleDebugDiagnosticEventsPresentation.SkillCasts)
            {
                _viewModel.FocusSkillCasts();
            }
            ctx.RequestRepaint?.Invoke();
        }

        private void DrawPrimaryContent(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items,
            bool expandHeight)
        {
            switch (_presentation)
            {
                case BattleDebugDiagnosticEventsPresentation.TriggerFlow:
                    DrawTriggerFlowWorkspace(in ctx, items, expandHeight);
                    break;
                case BattleDebugDiagnosticEventsPresentation.SkillCasts:
                    DrawSkillCastWorkspace(in ctx, items, expandHeight);
                    break;
                case BattleDebugDiagnosticEventsPresentation.Events:
                    DrawEventList(in ctx, items, expandHeight);
                    break;
                default:
                    DrawFrameHistogram(in ctx, _viewModel.Items, items);
                    DrawInvestigations(in ctx, items);
                    DrawIssueGroups();
                    break;
            }
        }

        private void DrawSkillCastWorkspace(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items,
            bool expandHeight)
        {
            var casts = ResolveSkillCastFlows(items);
            var comparisons = ResolveSkillCastComparisons(casts);
            if (casts.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "当前工作集没有可关联的技能施法。可扩大事件工作集或清除共享过滤条件后重试。",
                    MessageType.Info);
                return;
            }

            _castScroll = expandHeight
                ? EditorGUILayout.BeginScrollView(
                    _castScroll,
                    GUILayout.MinHeight(240f),
                    GUILayout.ExpandHeight(true))
                : EditorGUILayout.BeginScrollView(
                    _castScroll,
                    GUILayout.MinHeight(180f),
                    GUILayout.MaxHeight(Mathf.Max(300f, EditorGUIUtility.currentViewWidth * 0.55f)));
            DrawSkillCastComparisonSummary(in ctx, casts, comparisons);
            DrawSkillCastFlows(in ctx, casts, comparisons);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSkillCastComparisonSummary(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDebugSkillCastFlow> casts,
            IReadOnlyList<BattleDebugSkillCastComparison> comparisons)
        {
            if (comparisons == null || comparisons.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("施法对比", EditorStyles.miniBoldLabel, GUILayout.Width(58f));
            GUILayout.Label("数值为逻辑帧跨度，不代表 CPU 耗时。", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{comparisons.Count} 组", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            for (var i = 0; i < comparisons.Count; i++)
            {
                var comparison = comparisons[i];
                var skillLabel = comparison.SkillId != 0
                    ? $"技能 {comparison.SkillId} Lv{comparison.SkillLevel}"
                    : $"槽位 {comparison.SkillSlot} Lv{comparison.SkillLevel}";
                var baselineLabel = comparison.HasReliableBaseline
                    ? $"成功基线 {comparison.BaselineSampleCount} 次，中位总跨度 {comparison.MedianTotalFrames} 帧"
                    : $"基线不足 {comparison.BaselineSampleCount}/2";
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(skillLabel, EditorStyles.miniBoldLabel, GUILayout.Width(150f));
                GUILayout.Label(
                    $"样本 {comparison.CastCount}  成功 {comparison.SuccessCount}  问题 {comparison.ProblemCount}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(150f));
                GUILayout.Label(baselineLabel, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (comparison.Deviations.Count > 0)
                    GUILayout.Label($"慢阶段 {comparison.Deviations.Count}", EditorStyles.miniBoldLabel);
                EditorGUILayout.EndHorizontal();

                var visibleDeviationCount = Mathf.Min(3, comparison.Deviations.Count);
                for (var deviationIndex = 0; deviationIndex < visibleDeviationCount; deviationIndex++)
                {
                    var deviation = comparison.Deviations[deviationIndex];
                    var castIndex = FindSkillCastIndex(casts, deviation.CastKey);
                    if (castIndex < 0) continue;
                    var cast = casts[castIndex];
                    var label =
                        $"{BattleDebugDisplayText.SkillCastPhase(deviation.Phase)}  " +
                        $"{deviation.ActualFrames} 帧 / 基线 {deviation.BaselineFrames}  " +
                        $"(+{deviation.ExcessFrames})  F{cast.FirstFrame}";
                    var oldColor = GUI.color;
                    GUI.color = new Color(1f, 0.68f, 0.32f, 1f);
                    if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.MinWidth(260f)))
                    {
                        ExpandSkillCast(in ctx, in cast);
                        _actionStatus = $"已定位慢阶段：{BattleDebugDisplayText.SkillCastPhase(deviation.Phase)}。";
                    }
                    GUI.color = oldColor;
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private void DrawSkillCastFlows(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDebugSkillCastFlow> casts,
            IReadOnlyList<BattleDebugSkillCastComparison> comparisons)
        {
            for (var i = 0; i < casts.Count; i++)
            {
                var cast = casts[i];
                var expanded = string.Equals(
                    _expandedSkillCastKey,
                    cast.Key,
                    System.StringComparison.Ordinal);
                var skillLabel = cast.SkillId != 0
                    ? $"技能 {cast.SkillId}"
                    : cast.SkillSlot > 0
                        ? $"槽位 {cast.SkillSlot}"
                        : "技能未识别";
                var status = cast.IsComplete
                    ? BattleDebugDisplayText.EventOutcome(cast.Outcome)
                    : "进行中";
                var hasDeviation = TryFindWorstSkillCastDeviation(
                    comparisons,
                    cast.Key,
                    out var worstDeviation);
                var header = $"{(expanded ? "▼" : "▶")}  {skillLabel}  F{cast.FirstFrame}-F{cast.LastFrame}  " +
                             $"{cast.DurationFrames} 帧  {cast.EventCount} 事件" +
                             (hasDeviation ? $"  慢 +{worstDeviation.ExcessFrames}" : string.Empty);
                var tooltip =
                    $"{skillLabel}，槽位={cast.SkillSlot}，等级={cast.SkillLevel}，序列={cast.CastSequence}\n" +
                    $"命令={cast.CommandId}，Trace={cast.RootContextId}，Runtime={cast.SkillRuntime}\n" +
                    $"来源={cast.SourceActorId}，目标={cast.TargetActorId}";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                var oldColor = GUI.color;
                GUI.color = GetOutcomeColor(cast.Outcome);
                if (GUILayout.Button(
                        new GUIContent(header, tooltip),
                        EditorStyles.miniButton,
                        GUILayout.MinWidth(280f),
                        GUILayout.Height(24f)))
                {
                    _expandedSkillCastKey = expanded ? string.Empty : cast.Key;
                    if (cast.Events.Count > 0)
                    {
                        var latest = cast.Events[cast.Events.Count - 1];
                        SelectEvent(in ctx, in latest);
                    }
                    _actionStatus = expanded ? "已收起施法链路。" : $"已展开 {skillLabel} 的施法链路。";
                }
                GUI.color = oldColor;
                GUILayout.Label(status, EditorStyles.miniBoldLabel, GUILayout.Width(54f));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(
                    $"命令 {FormatCorrelationId(cast.CommandId)}  ·  Trace {FormatCorrelationId(cast.RootContextId)}  ·  " +
                    $"Runtime {(cast.SkillRuntime.IsValid ? cast.SkillRuntime.ToString() : "-")}",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"输入 {cast.InputCount}  选目标 {cast.TargetSearchCount}  阶段 {cast.SkillStageCount}  " +
                    $"经济 {cast.EconomyCount}  后续 {cast.ConsequenceCount}",
                    EditorStyles.miniLabel);

                if (expanded)
                {
                    DrawSkillCastPhaseTimings(in cast, hasDeviation ? worstDeviation : default);
                    for (var eventIndex = 0; eventIndex < cast.Events.Count; eventIndex++)
                    {
                        var item = cast.Events[eventIndex];
                        DrawSkillCastTimelineEvent(in ctx, in item);
                    }

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("复制链路", EditorStyles.miniButton, GUILayout.Width(76f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildSkillCastClipboardText(in cast);
                        _actionStatus = "施法链路已复制到剪贴板。";
                    }
                    EditorGUI.BeginDisabledGroup(cast.RootContextId == 0L || ctx.OpenTrace == null);
                    if (GUILayout.Button("打开 Trace", EditorStyles.miniButton, GUILayout.Width(82f)))
                    {
                        var contextId = cast.Events.Count > 0
                            ? cast.Events[cast.Events.Count - 1].ContextId
                            : 0L;
                        ctx.OpenTrace?.Invoke(cast.RootContextId, contextId);
                    }
                    EditorGUI.EndDisabledGroup();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2f);
            }
        }

        private static void DrawSkillCastPhaseTimings(
            in BattleDebugSkillCastFlow cast,
            BattleDebugSkillCastDeviation worstDeviation)
        {
            if (cast.Timings.Count == 0) return;
            var builder = new StringBuilder();
            for (var i = 0; i < cast.Timings.Count; i++)
            {
                var timing = cast.Timings[i];
                if (timing.Phase == BattleDebugSkillCastPhase.Total) continue;
                var isWorst = !string.IsNullOrEmpty(worstDeviation.CastKey) &&
                              worstDeviation.Phase == timing.Phase;
                if (builder.Length > 0) builder.Append("  ·  ");
                if (isWorst) builder.Append("[慢] ");
                builder.Append(BattleDebugDisplayText.SkillCastPhase(timing.Phase));
                builder.Append(' ');
                builder.Append(timing.DurationFrames);
            }
            EditorGUILayout.LabelField("阶段跨度", builder.ToString(), EditorStyles.wordWrappedMiniLabel);
        }

        private void ExpandSkillCast(
            in BattleDebugContext ctx,
            in BattleDebugSkillCastFlow cast)
        {
            _expandedSkillCastKey = cast.Key;
            if (cast.Events.Count == 0) return;
            var latest = cast.Events[cast.Events.Count - 1];
            SelectEvent(in ctx, in latest);
        }

        private static int FindSkillCastIndex(
            IReadOnlyList<BattleDebugSkillCastFlow> casts,
            string key)
        {
            if (casts == null || string.IsNullOrEmpty(key)) return -1;
            for (var i = 0; i < casts.Count; i++)
            {
                if (string.Equals(casts[i].Key, key, System.StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private static bool TryFindWorstSkillCastDeviation(
            IReadOnlyList<BattleDebugSkillCastComparison> comparisons,
            string castKey,
            out BattleDebugSkillCastDeviation deviation)
        {
            deviation = default;
            if (comparisons == null || string.IsNullOrEmpty(castKey)) return false;
            for (var comparisonIndex = 0; comparisonIndex < comparisons.Count; comparisonIndex++)
            {
                var candidates = comparisons[comparisonIndex].Deviations;
                for (var deviationIndex = 0; deviationIndex < candidates.Count; deviationIndex++)
                {
                    var candidate = candidates[deviationIndex];
                    if (!string.Equals(candidate.CastKey, castKey, System.StringComparison.Ordinal)) continue;
                    if (string.IsNullOrEmpty(deviation.CastKey) ||
                        candidate.ExcessFrames > deviation.ExcessFrames)
                    {
                        deviation = candidate;
                    }
                }
            }
            return !string.IsNullOrEmpty(deviation.CastKey);
        }

        private void DrawSkillCastTimelineEvent(
            in BattleDebugContext ctx,
            in BattleDiagnosticEvent item)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"F{item.Frame}", EditorStyles.miniLabel, GUILayout.Width(48f));
            var selected = _selectedEvent.HasValue && _selectedEvent.Value.Sequence == item.Sequence;
            var oldColor = GUI.color;
            GUI.color = GetOutcomeColor(item.Outcome);
            var stage = GetSkillCastTimelineLabel(in item);
            var summary = string.IsNullOrEmpty(item.Summary) ? string.Empty : "  " + item.Summary;
            if (GUILayout.Button(
                    new GUIContent($"#{item.Sequence}  {stage}{summary}", BuildEventTooltip(in item)),
                    selected ? EditorStyles.toolbarButton : EditorStyles.miniButton,
                    GUILayout.MinWidth(240f),
                    GUILayout.Height(22f)))
            {
                SelectEvent(in ctx, in item);
            }
            GUI.color = oldColor;
            GUILayout.Label(
                BattleDebugDisplayText.EventOutcome(item.Outcome),
                EditorStyles.miniLabel,
                GUILayout.Width(48f));
            EditorGUILayout.EndHorizontal();
        }

        private static string GetSkillCastTimelineLabel(in BattleDiagnosticEvent item)
        {
            if (item.Payload.TryGetSkillExecution(out var execution))
                return BattleDebugDisplayText.SkillExecutionStage(execution.Stage);
            if (item.Kind == BattleDiagnosticEventKind.InputCommand) return "输入命令";
            if (item.Kind == BattleDiagnosticEventKind.TargetSearch) return "目标搜索";
            return BattleDebugDisplayText.EventKind(item.Kind);
        }

        private static string BuildEventTooltip(in BattleDiagnosticEvent item)
        {
            return $"F{item.Frame}  #{item.Sequence}\n" +
                   $"{BattleDebugDisplayText.EventKind(item.Kind)} / {BattleDebugDisplayText.EventOutcome(item.Outcome)}\n" +
                   $"来源={item.SourceActorId}，目标={item.TargetActorId}，配置={item.ConfigId}\n" +
                   item.Summary;
        }

        private static string FormatCorrelationId(long value)
        {
            return value != 0L ? value.ToString() : "-";
        }

        internal static string BuildSkillCastClipboardText(in BattleDebugSkillCastFlow cast)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"CommandId={cast.CommandId}");
            builder.AppendLine($"RootContextId={cast.RootContextId}");
            builder.AppendLine($"SkillRuntime={cast.SkillRuntime}");
            builder.AppendLine($"SkillId={cast.SkillId}");
            builder.AppendLine($"SkillSlot={cast.SkillSlot}");
            builder.AppendLine($"SkillLevel={cast.SkillLevel}");
            builder.AppendLine($"CastSequence={cast.CastSequence}");
            builder.AppendLine($"SourceActorId={cast.SourceActorId}");
            builder.AppendLine($"TargetActorId={cast.TargetActorId}");
            builder.AppendLine($"Frames={cast.FirstFrame}-{cast.LastFrame}");
            builder.AppendLine($"Outcome={cast.Outcome}");
            builder.AppendLine($"EventCount={cast.EventCount}");
            builder.AppendLine(
                $"StageCounts=Input:{cast.InputCount},TargetSearch:{cast.TargetSearchCount}," +
                $"Skill:{cast.SkillStageCount},Economy:{cast.EconomyCount},Consequence:{cast.ConsequenceCount}");
            for (var i = 0; i < cast.Events.Count; i++)
            {
                var item = cast.Events[i];
                builder.AppendLine(
                    $"Event=F{item.Frame} #{item.Sequence} {item.Kind} {item.Outcome} " +
                    $"Context:{item.ContextId} Config:{item.ConfigId} Summary:{item.Summary}");
            }
            return builder.ToString();
        }

        private IReadOnlyList<BattleDebugSkillCastFlow> ResolveSkillCastFlows(
            IReadOnlyList<BattleDiagnosticEvent> items)
        {
            return ReferenceEquals(items, _viewModel.Items) && _viewModel.SkillCastFlows != null
                ? _viewModel.SkillCastFlows
                : BattleDebugDiagnosticEventsViewModel.BuildSkillCastFlows(items);
        }

        private IReadOnlyList<BattleDebugSkillCastComparison> ResolveSkillCastComparisons(
            IReadOnlyList<BattleDebugSkillCastFlow> casts)
        {
            return ReferenceEquals(casts, _viewModel.SkillCastFlows) &&
                   _viewModel.SkillCastComparisons != null
                ? _viewModel.SkillCastComparisons
                : BattleDebugDiagnosticEventsViewModel.BuildSkillCastComparisons(casts);
        }

        private void DrawTriggerFlowWorkspace(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items,
            bool expandHeight)
        {
            var flows = BattleDebugDiagnosticEventsViewModel.BuildTriggerFlows(items);
            if (flows.Count == 0)
            {
                EditorGUILayout.HelpBox("当前工作集没有触发流程。", MessageType.Info);
                return;
            }

            _flowScroll = expandHeight
                ? EditorGUILayout.BeginScrollView(
                    _flowScroll,
                    GUILayout.MinHeight(240f),
                    GUILayout.ExpandHeight(true))
                : EditorGUILayout.BeginScrollView(
                    _flowScroll,
                    GUILayout.MinHeight(180f),
                    GUILayout.MaxHeight(Mathf.Max(280f, EditorGUIUtility.currentViewWidth * 0.5f)));
            DrawTriggerFlows(in ctx, items);
            EditorGUILayout.EndScrollView();
        }

        private void DrawInvestigations(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items)
        {
            var cases = BattleDebugSkillInvestigationModel.Build(
                items,
                _investigationConfidenceFilter,
                _investigationCauseFilter);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("失败调查", EditorStyles.miniBoldLabel, GUILayout.Width(58));
            GUILayout.Label("置信度", EditorStyles.miniLabel, GUILayout.Width(42));
            _investigationConfidenceFilter =
                (BattleDebugInvestigationConfidenceFilter)EditorGUILayout.Popup(
                    (int)_investigationConfidenceFilter,
                    InvestigationConfidenceLabels,
                    EditorStyles.miniPullDown,
                    GUILayout.Width(125));
            GUILayout.Label("根因", EditorStyles.miniLabel, GUILayout.Width(30));
            _investigationCauseFilter =
                (BattleDebugInvestigationCauseFilter)EditorGUILayout.Popup(
                    (int)_investigationCauseFilter,
                    InvestigationCauseLabels,
                    EditorStyles.miniPullDown,
                    GUILayout.Width(170));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{cases.Count} 个案例", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            var selectedIndex = FindInvestigationIndex(cases, _selectedInvestigation);
            if (_selectedInvestigation.HasValue && selectedIndex < 0)
            {
                _selectedInvestigation = null;
            }
            else if (selectedIndex >= 0)
            {
                _selectedInvestigation = cases[selectedIndex];
            }

            if (cases.Count == 0)
            {
                EditorGUILayout.LabelField("当前案例筛选下没有匹配项。", EditorStyles.miniLabel);
            }

            for (var i = 0; i < cases.Count; i++)
            {
                var investigation = cases[i];
                EditorGUILayout.BeginHorizontal();
                var label = $"F{investigation.FirstFrame}-F{investigation.LastFrame}  {investigation.Conclusion}";
                var style = selectedIndex == i ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                if (GUILayout.Button(label, style, GUILayout.MinWidth(280f)))
                {
                    SelectInvestigation(in investigation);
                    selectedIndex = i;
                }

                GUILayout.Label(BattleDebugDisplayText.InvestigationConfidence(investigation.Confidence), EditorStyles.miniLabel, GUILayout.Width(125));
                GUILayout.Label($"{investigation.Evidence.Count} 条", EditorStyles.miniLabel, GUILayout.Width(36));
                if (investigation.RootContextId > 0)
                {
                    GUILayout.Label($"Trace={investigation.RootContextId}", EditorStyles.miniLabel, GUILayout.Width(90));
                }
                EditorGUILayout.EndHorizontal();
            }

            if (_selectedInvestigation.HasValue)
            {
                var selected = _selectedInvestigation.Value;
                selectedIndex = FindInvestigationIndex(cases, _selectedInvestigation);
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label(
                    selectedIndex >= 0 ? $"案例 {selectedIndex + 1}/{cases.Count}" : "案例已固定",
                    EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                EditorGUI.BeginDisabledGroup(selectedIndex < 0 || selectedIndex >= cases.Count - 1);
                if (GUILayout.Button(new GUIContent("▲", "选择更早的调查案例"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
                {
                    var previous = cases[selectedIndex + 1];
                    SelectInvestigation(in previous);
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(selectedIndex <= 0);
                if (GUILayout.Button(new GUIContent("▼", "选择更新的调查案例"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
                {
                    var next = cases[selectedIndex - 1];
                    SelectInvestigation(in next);
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("调查结论", selected.Conclusion);
                EditorGUILayout.LabelField("证据摘要", selected.EvidenceSummary, EditorStyles.wordWrappedMiniLabel);
                DrawInvestigationEvidence(selected.Evidence);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("复制调查摘要", GUILayout.Width(100)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildInvestigationClipboardText(in selected);
                    _actionStatus = "调查摘要已复制到剪贴板。";
                }

                EditorGUI.BeginDisabledGroup(selected.SourceActorId <= 0 || ctx.SelectActor == null);
                if (GUILayout.Button("选择来源 Actor", GUILayout.Width(110)))
                {
                    ctx.SelectActor?.Invoke(selected.SourceActorId);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(!HasCorrelatedEvidence(selected.Evidence));
                if (GUILayout.Button("聚焦证据链", GUILayout.Width(100)))
                {
                    FocusInvestigationEvidence(selected.Evidence);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(!selected.CanOpenTrace || ctx.OpenTrace == null);
                if (GUILayout.Button("打开 Trace", GUILayout.Width(90)))
                {
                    ctx.OpenTrace?.Invoke(selected.RootContextId, selected.ContextId);
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("取消调查", GUILayout.Width(80)))
                {
                    _selectedInvestigation = null;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawTriggerFlows(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items)
        {
            var flows = BattleDebugDiagnosticEventsViewModel.BuildTriggerFlows(items);
            if (flows == null || flows.Count == 0) return;

            var availableWidth = Mathf.Max(280f, ctx.AvailableContentWidth);
            var compact = availableWidth < 760f;
            var compactStageWidth = Mathf.Clamp(
                (availableWidth - 36f) / 4f,
                68f,
                90f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("触发流程", EditorStyles.miniBoldLabel, GUILayout.Width(58));
            GUILayout.Label($"{flows.Count} 条", EditorStyles.miniLabel, GUILayout.Width(42));
            GUILayout.FlexibleSpace();
            if (!compact)
            {
                GUILayout.Label("预算", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(90));
                GUILayout.Label("条件", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(90));
                GUILayout.Label("计划", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(90));
                GUILayout.Label("执行", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(90));
            }
            EditorGUILayout.EndHorizontal();

            for (var i = 0; i < flows.Count; i++)
            {
                var flow = flows[i];
                if (compact)
                {
                    DrawTriggerFlowIdentity(in ctx, items, in flow, expandWidth: true);
                    EditorGUILayout.BeginHorizontal();
                    DrawTriggerStageCell(in ctx, items, flow.Budget, compactStageWidth, showStageName: true);
                    DrawTriggerStageCell(in ctx, items, flow.Conditions, compactStageWidth, showStageName: true);
                    DrawTriggerStageCell(in ctx, items, flow.Plan, compactStageWidth, showStageName: true);
                    DrawTriggerStageCell(in ctx, items, flow.Execution, compactStageWidth, showStageName: true);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    DrawTriggerFlowIdentity(in ctx, items, in flow, expandWidth: false);
                    GUILayout.FlexibleSpace();
                    DrawTriggerStageCell(in ctx, items, flow.Budget, 90f, showStageName: false);
                    DrawTriggerStageCell(in ctx, items, flow.Conditions, 90f, showStageName: false);
                    DrawTriggerStageCell(in ctx, items, flow.Plan, 90f, showStageName: false);
                    DrawTriggerStageCell(in ctx, items, flow.Execution, 90f, showStageName: false);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawTriggerFlowIdentity(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items,
            in BattleDebugTriggerFlow flow,
            bool expandWidth)
        {
            var rootLabel = flow.SpansMultipleRoots
                ? $"根节点={flow.FirstRootContextId}-{flow.LastRootContextId}"
                : flow.RootContextId != 0
                    ? $"Trace={flow.RootContextId}"
                    : "Trace=?";
            var label = $"触发器={flow.TriggerId}  {rootLabel}  F{flow.FirstFrame}-F{flow.LastFrame}";
            var tooltip =
                $"触发器 {flow.TriggerId}\n{rootLabel}\n" +
                $"来源={flow.SourceActorId}，目标={flow.TargetActorId}\n" +
                $"最新事件 #{flow.LatestSequence}";
            var selected = _selectedEvent.HasValue &&
                           _selectedEvent.Value.Sequence == flow.LatestSequence;
            var style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
            var options = expandWidth
                ? new[] { GUILayout.MinWidth(220f), GUILayout.ExpandWidth(true) }
                : new[] { GUILayout.Width(270f) };
            if (!GUILayout.Button(new GUIContent(label, tooltip), style, options)) return;

            var index = FindEventIndex(items, flow.LatestSequence);
            if (index >= 0)
            {
                var diagnosticEvent = items[index];
                SelectEvent(in ctx, in diagnosticEvent);
            }
        }

        private void DrawTriggerStageCell(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items,
            BattleDebugTriggerStageSummary stage,
            float width,
            bool showStageName)
        {
            var countSuffix = stage.OccurrenceCount > 1 ? $" x{stage.OccurrenceCount}" : string.Empty;
            var label = stage.IsObserved
                ? showStageName
                    ? $"{BattleDebugDisplayText.TriggerStage(stage.Stage)}\n{BattleDebugDisplayText.TriggerResult(stage.LatestResult)}{countSuffix}\nF{stage.FirstFrame}-F{stage.LastFrame}"
                    : $"{BattleDebugDisplayText.TriggerResult(stage.LatestResult)}{countSuffix}\nF{stage.FirstFrame}-F{stage.LastFrame}"
                : showStageName
                    ? $"{BattleDebugDisplayText.TriggerStage(stage.Stage)}\n未记录"
                    : "未记录";
            var tooltip = stage.IsObserved
                ? $"{BattleDebugDisplayText.TriggerStage(stage.Stage)}：最新结果={BattleDebugDisplayText.TriggerResult(stage.LatestResult)}\n" +
                  $"发生次数={stage.OccurrenceCount}，事件数={stage.EventCount}\n" +
                  $"通过={stage.PassedCount}，失败={stage.FailedCount}，" +
                  $"阻止={stage.BlockedCount}，跳过={stage.SkippedCount}\n" +
                  $"失败键={stage.FailureKey}\n{stage.Reason}"
                : $"{BattleDebugDisplayText.TriggerStage(stage.Stage)}：已加载工作集中没有高价值事件";

            var oldColor = GUI.color;
            if (stage.IsObserved) GUI.color = GetTriggerResultColor(stage.LatestResult);
            var selected = stage.IsObserved &&
                           _selectedEvent.HasValue &&
                           _selectedEvent.Value.Sequence == stage.LatestSequence;
            var style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
            EditorGUI.BeginDisabledGroup(!stage.IsObserved);
            if (GUILayout.Button(
                    new GUIContent(label, tooltip),
                    style,
                    GUILayout.Width(width),
                    GUILayout.Height(showStageName ? 48f : 34f)))
            {
                var index = FindEventIndex(items, stage.LatestSequence);
                if (index >= 0)
                {
                    var diagnosticEvent = items[index];
                    SelectEvent(in ctx, in diagnosticEvent);
                }
            }
            EditorGUI.EndDisabledGroup();
            GUI.color = oldColor;
        }

        private void SelectInvestigation(in BattleDebugSkillInvestigationCase investigation)
        {
            _selectedInvestigation = investigation;
            _selectedEvent = investigation.Evidence.Count > 0
                ? investigation.Evidence[0]
                : default(BattleDiagnosticEvent?);
            _actionStatus = string.Empty;
        }

        private static int FindInvestigationIndex(
            IReadOnlyList<BattleDebugSkillInvestigationCase> cases,
            BattleDebugSkillInvestigationCase? selected)
        {
            if (cases == null || !selected.HasValue) return -1;
            for (var i = 0; i < cases.Count; i++)
            {
                if (string.Equals(cases[i].Key, selected.Value.Key, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void DrawInvestigationEvidence(IReadOnlyList<BattleDiagnosticEvent> evidence)
        {
            if (evidence == null || evidence.Count == 0) return;

            const float buttonWidth = 58f;
            const float buttonSpacing = 4f;
            var availableWidth = Mathf.Max(buttonWidth, EditorGUIUtility.currentViewWidth - 70f);
            var buttonsPerRow = Mathf.Max(
                1,
                Mathf.FloorToInt(availableWidth / (buttonWidth + buttonSpacing)));

            EditorGUILayout.LabelField("证据事件", EditorStyles.miniBoldLabel);
            for (var rowStart = 0; rowStart < evidence.Count; rowStart += buttonsPerRow)
            {
                EditorGUILayout.BeginHorizontal();
                var rowEnd = Mathf.Min(evidence.Count, rowStart + buttonsPerRow);
                for (var i = rowStart; i < rowEnd; i++)
                {
                    var item = evidence[i];
                    var tooltip = $"F{item.Frame} {BattleDebugDisplayText.EventKind(item.Kind)}：{item.Summary}";
                    if (GUILayout.Button(
                            new GUIContent($"#{item.Sequence}", tooltip),
                            EditorStyles.miniButton,
                            GUILayout.Width(buttonWidth)))
                    {
                        _selectedEvent = item;
                        _actionStatus = $"已选择案例证据 #{item.Sequence}。";
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private static bool HasCorrelatedEvidence(IReadOnlyList<BattleDiagnosticEvent> evidence)
        {
            if (evidence == null) return false;
            for (var i = 0; i < evidence.Count; i++)
            {
                var item = evidence[i];
                if (HasCorrelation(in item)) return true;
            }

            return false;
        }

        private void FocusInvestigationEvidence(IReadOnlyList<BattleDiagnosticEvent> evidence)
        {
            if (evidence == null) return;
            for (var i = 0; i < evidence.Count; i++)
            {
                var item = evidence[i];
                if (!_viewModel.FocusRelated(in item)) continue;

                _selectedEvent = item;
                _scroll = Vector2.zero;
                _actionStatus = $"正在调查 {_viewModel.CorrelationFocusLabel}。";
                return;
            }
        }

        private static string BuildInvestigationClipboardText(in BattleDebugSkillInvestigationCase investigation)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Conclusion={investigation.Conclusion}");
            builder.AppendLine($"Cause={investigation.Cause}");
            builder.AppendLine($"Confidence={investigation.Confidence}");
            builder.AppendLine($"Frames={investigation.FirstFrame}-{investigation.LastFrame}");
            builder.AppendLine($"RootContextId={investigation.RootContextId}");
            builder.AppendLine($"ContextId={investigation.ContextId}");
            builder.AppendLine($"SourceActorId={investigation.SourceActorId}");
            builder.AppendLine($"TargetActorId={investigation.TargetActorId}");
            builder.AppendLine($"ConfigId={investigation.ConfigId}");
            builder.AppendLine($"SkillRuntime={investigation.SkillRuntime}");
            builder.AppendLine($"Evidence={investigation.EvidenceSummary}");
            for (var i = 0; i < investigation.Evidence.Count; i++)
            {
                builder.AppendLine($"EventSequence={investigation.Evidence[i].Sequence}");
            }
            return builder.ToString();
        }

        private void DrawIssueGroups()
        {
            var groups = _viewModel.IssueGroups;
            if (groups == null || groups.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("问题聚合（已加载）", EditorStyles.miniBoldLabel, GUILayout.Width(108));
            GUILayout.Label("按完整已加载调查工作集归并，不受共享时间窗口影响；点击可收敛到同类事件。", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{groups.Count} 个簇", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(group.Label, EditorStyles.miniButton, GUILayout.MinWidth(280f)))
                {
                    _viewModel.FocusIssueGroup(in group);
                    _selectedEvent = null;
                    _scroll = Vector2.zero;
                    _actionStatus = $"已收敛到问题簇：{group.Label}";
                }

                GUILayout.Label($"{group.Count} 次", EditorStyles.miniLabel, GUILayout.Width(42));
                GUILayout.Label($"首次 F{group.FirstFrame}", EditorStyles.miniLabel, GUILayout.Width(72));
                GUILayout.Label($"最近 F{group.LatestFrame}", EditorStyles.miniLabel, GUILayout.Width(72));
                GUILayout.Label($"跨度 {group.FrameSpan}", EditorStyles.miniLabel, GUILayout.Width(62));
                if (group.ConfigId != 0)
                {
                    GUILayout.Label($"配置={group.ConfigId}", EditorStyles.miniLabel, GUILayout.Width(66));
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawEventList(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items,
            bool expandHeight)
        {
            _scroll = expandHeight
                ? EditorGUILayout.BeginScrollView(
                    _scroll,
                    GUILayout.MinHeight(240f),
                    GUILayout.ExpandHeight(true))
                : EditorGUILayout.BeginScrollView(
                    _scroll,
                    GUILayout.MinHeight(180f),
                    GUILayout.MaxHeight(Mathf.Max(260f, EditorGUIUtility.currentViewWidth * 0.42f)));

            if (items == null || items.Count == 0)
            {
                var workspaceFilter = ctx.WorkspaceState.Filter;
                var emptyState = BattleDebugEmptyStateProjector.Project(
                    _viewModel.QueryStatus,
                    requiresSelection: _viewModel.FilterBySelectedActor &&
                                       !workspaceFilter.HasActorFilter,
                    hasSelection: ctx.HasSelection,
                    hasActiveFilter: _viewModel.HasEffectiveFilter(in workspaceFilter),
                    subject: "诊断事件");
                DrawEmptyState(in emptyState);
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    DrawEventRow(in ctx, items[i]);
                }
            }

            if (items != null && items.Count > 0 && !string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.None);
            }

            EditorGUILayout.EndScrollView();
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

        private void DrawEventRow(
            in BattleDebugContext ctx,
            in BattleDiagnosticEvent evt)
        {
            var outcomeColor = GetOutcomeColor(evt.Outcome);
            var oldColor = GUI.color;
            var selected = _selectedEvent.HasValue && evt.Sequence == _selectedEvent.Value.Sequence;

            EditorGUILayout.BeginHorizontal(GUI.skin.box);

            GUI.color = outcomeColor;
            var style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
            if (GUILayout.Button($"#{evt.Sequence}", style, GUILayout.Width(70)))
            {
                SelectEvent(in ctx, in evt);
            }
            GUI.color = oldColor;

            GUILayout.Label($"F{evt.Frame}", GUILayout.Width(50));
            GUILayout.Label(
                evt.Kind == BattleDiagnosticEventKind.TriggerAnalysisAggregate
                    ? "触发汇总"
                    : BattleDebugDisplayText.EventKind(evt.Kind),
                GUILayout.Width(120));
            GUILayout.Label(BattleDebugDisplayText.EventOutcome(evt.Outcome), GUILayout.Width(70));

            if (evt.Payload.TryGetTriggerAnalysis(out var triggerPayload))
            {
                GUILayout.Label($"触发器={triggerPayload.TriggerId}", EditorStyles.miniLabel, GUILayout.Width(78));
                GUILayout.Label($"{BattleDebugDisplayText.TriggerStage(triggerPayload.Stage)}/{BattleDebugDisplayText.TriggerResult(triggerPayload.Result)}", EditorStyles.miniLabel, GUILayout.Width(135));
            }
            else if (evt.Payload.TryGetTriggerAnalysisAggregate(out var triggerAggregate))
            {
                GUILayout.Label($"触发器={triggerAggregate.TriggerId}", EditorStyles.miniLabel, GUILayout.Width(78));
                GUILayout.Label(
                    $"{BattleDebugDisplayText.TriggerStage(triggerAggregate.Stage)}/{BattleDebugDisplayText.TriggerResult(triggerAggregate.Result)} x{triggerAggregate.OccurrenceCount}",
                    EditorStyles.miniBoldLabel,
                    GUILayout.Width(160));
                GUILayout.Label(
                    $"F{triggerAggregate.FirstFrame}-F{triggerAggregate.LastFrame}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(90));
            }
            else if (evt.Payload.TryGetSkillFailure(out var skillFailure))
            {
                GUILayout.Label(skillFailure.Code, EditorStyles.miniLabel, GUILayout.Width(180));
            }
            else if (evt.Payload.TryGetBuffLifecycle(out var buffLifecycle))
            {
                GUILayout.Label(BattleDebugDisplayText.BuffLifecycleStage(buffLifecycle.Stage), EditorStyles.miniLabel, GUILayout.Width(85));
                GUILayout.Label(
                    buffLifecycle.Stage == BattleDiagnosticBuffLifecycleStage.StackChanged
                        ? $"层数={buffLifecycle.PreviousStackCount}->{buffLifecycle.StackCount}"
                        : $"层数={buffLifecycle.StackCount}/{buffLifecycle.MaxStacks}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(90));
            }

            if (evt.ConfigId != 0)
            {
                GUILayout.Label($"配置={evt.ConfigId}", EditorStyles.miniLabel, GUILayout.Width(75));
            }

            if (evt.SourceActorId != 0)
            {
                GUILayout.Label($"来源={evt.SourceActorId}", EditorStyles.miniLabel, GUILayout.Width(70));
            }

            if (evt.TargetActorId != 0)
            {
                GUILayout.Label($"目标={evt.TargetActorId}", EditorStyles.miniLabel, GUILayout.Width(70));
            }

            if (evt.RootContextId != 0)
            {
                GUILayout.Label($"Trace={evt.RootContextId}", EditorStyles.miniLabel, GUILayout.Width(90));
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(evt.Summary, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectionDetails(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items)
        {
            if (!_selectedEvent.HasValue) return;

            var evt = _selectedEvent.Value;
            var selectedIndex = FindEventIndex(items, evt.Sequence);
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(
                selectedIndex >= 0
                    ? $"事件详情  {selectedIndex + 1}/{items.Count}"
                    : "事件详情  已固定",
                EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(selectedIndex < 0 || selectedIndex >= items.Count - 1);
            if (GUILayout.Button(new GUIContent("▲", "选择当前结果中的上一条事件"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
            {
                SelectResult(in ctx, items, selectedIndex + 1);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(selectedIndex <= 0);
            if (GUILayout.Button(new GUIContent("▼", "选择当前结果中的下一条事件"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
            {
                SelectResult(in ctx, items, selectedIndex - 1);
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button(new GUIContent("复制", "复制该事件的完整诊断字段"), EditorStyles.toolbarButton, GUILayout.Width(42f)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildClipboardText(in evt);
                _actionStatus = "事件详情已复制到剪贴板。";
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("序列 / 帧", $"{evt.Sequence} / {evt.Frame}");
            EditorGUILayout.LabelField("类型 / 通道 / 结果", $"{BattleDebugDisplayText.EventKind(evt.Kind)} / {BattleDebugDisplayText.EventChannel(evt.Channel)} / {BattleDebugDisplayText.EventOutcome(evt.Outcome)}");
            EditorGUILayout.LabelField("根节点 / 上下文", $"{evt.RootContextId} / {evt.ContextId}");
            EditorGUILayout.LabelField("配置 / 攻击", $"{evt.ConfigId} / {evt.AttackId}");
            EditorGUILayout.LabelField("技能运行时", evt.SkillRuntime.ToString());
            if (evt.Kind == BattleDiagnosticEventKind.TargetSearch)
            {
                EditorGUILayout.LabelField("目标搜索", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(evt.Summary, EditorStyles.wordWrappedLabel,
                    GUILayout.Height(Mathf.Min(560f, 24f + 17f * evt.Summary.Split('\n').Length)));
            }
            else
            {
                EditorGUILayout.LabelField("摘要", evt.Summary);
            }

            if (evt.Payload.TryGetTriggerAnalysis(out var triggerPayload))
            {
                DrawTriggerPayloadDetails(in triggerPayload, evt.Payload.SchemaVersion);
            }
            else if (evt.Payload.TryGetTriggerAnalysisAggregate(out var triggerAggregate))
            {
                DrawTriggerAggregatePayloadDetails(in triggerAggregate, evt.Payload.SchemaVersion);
            }
            else if (evt.Payload.TryGetSkillFailure(out var skillFailure))
            {
                DrawSkillFailurePayloadDetails(in skillFailure, evt.Payload.SchemaVersion);
            }
            else if (evt.Payload.TryGetBuffLifecycle(out var buffLifecycle))
            {
                DrawBuffLifecyclePayloadDetails(in buffLifecycle, evt.Payload.SchemaVersion);
            }
            else if (evt.Payload.TryGetDamageCalculation(out var damageCalculation))
            {
                DrawDamageCalculationDetails(in damageCalculation);
            }
            else if (evt.Payload.TryGetInputCommand(out var inputCommand))
            {
                DrawInputCommandDetails(in inputCommand);
            }
            else if (evt.Payload.TryGetTargetSearch(out var targetSearch))
            {
                DrawTargetSearchDetails(in targetSearch);
            }
            else if (evt.Payload.TryGetSkillExecution(out var skillExecution))
            {
                DrawSkillExecutionDetails(in skillExecution);
            }
            else if (evt.Payload.TryGetSyncSnapshotReceived(out var syncPayload))
            {
                EditorGUILayout.LabelField(
                    "载荷",
                    $"同步快照接收 v{evt.Payload.SchemaVersion}：" +
                    $"帧={syncPayload.AuthoritativeFrame}，哈希={syncPayload.StateHash}");
            }
            else
            {
                EditorGUILayout.LabelField(
                    "载荷",
                    evt.Payload.HasValue
                        ? $"{BattleDebugDisplayText.PayloadKind(evt.Payload.Kind)} v{evt.Payload.SchemaVersion}"
                        : "（无）");
            }

            if (!string.IsNullOrEmpty(_actionStatus))
            {
                EditorGUILayout.HelpBox(_actionStatus, MessageType.Info);
            }

            var hasConfigReference = BattleDebugConfigReferenceMapper.TryFromEvent(
                in evt,
                out var configReference);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(evt.SourceActorId == 0 || ctx.SelectActor == null);
            if (GUILayout.Button("选择来源 Actor", GUILayout.Width(110)))
            {
                ctx.SelectActor?.Invoke(evt.SourceActorId);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(evt.TargetActorId == 0 || ctx.SelectActor == null);
            if (GUILayout.Button("选择目标 Actor", GUILayout.Width(110)))
            {
                ctx.SelectActor?.Invoke(evt.TargetActorId);
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(!hasConfigReference || ctx.OpenConfig == null);
            if (GUILayout.Button("打开配置", GUILayout.Width(80)))
            {
                ctx.OpenConfig?.Invoke(configReference);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(evt.RootContextId <= 0 || ctx.OpenTrace == null);
            if (GUILayout.Button("打开 Trace", GUILayout.Width(90)))
            {
                ctx.OpenTrace?.Invoke(evt.RootContextId, evt.ContextId);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!HasCorrelation(in evt));
            if (GUILayout.Button("查看关联链", GUILayout.Width(100)))
            {
                if (_viewModel.FocusRelated(in evt))
                {
                    _scroll = Vector2.zero;
                    _actionStatus = $"正在调查 {_viewModel.CorrelationFocusLabel}";
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(ctx.SeekReplayFrame == null);
            if (GUILayout.Button("定位回放帧", GUILayout.Width(100)))
            {
                _actionStatus = ctx.SeekReplayFrame != null && ctx.SeekReplayFrame(evt.Frame)
                    ? $"回放已定位到第 {evt.Frame} 帧并暂停。"
                    : $"无法定位到第 {evt.Frame} 帧。该帧可能超出当前回放范围。";
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消固定", GUILayout.Width(80)))
            {
                _selectedEvent = null;
                _actionStatus = string.Empty;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SelectResult(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items,
            int index)
        {
            if (items == null || index < 0 || index >= items.Count) return;
            var diagnosticEvent = items[index];
            SelectEvent(in ctx, in diagnosticEvent);
        }

        private void SelectEvent(
            in BattleDebugContext ctx,
            in BattleDiagnosticEvent diagnosticEvent)
        {
            _selectedEvent = diagnosticEvent;
            _actionStatus = string.Empty;
            ctx.WorkspaceState?.Select(CreateEventSelection(in diagnosticEvent));
            ctx.RequestRepaint?.Invoke();
        }

        internal static BattleDiagnosticSelection CreateEventSelection(
            in BattleDiagnosticEvent diagnosticEvent)
        {
            return new BattleDiagnosticSelection(
                diagnosticEvent.Scope,
                BattleDiagnosticSelectionKind.Event,
                diagnosticEvent.Sequence,
                diagnosticEvent.Frame,
                diagnosticEvent.RootContextId);
        }

        private void RestoreWorkspaceEventSelection(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items)
        {
            var selection = ctx.WorkspaceState?.Selection ?? default;
            if (selection.Kind != BattleDiagnosticSelectionKind.Event ||
                (_selectedEvent.HasValue && _selectedEvent.Value.Sequence == selection.Id))
            {
                return;
            }

            var index = FindEventIndex(items, selection.Id);
            if (index >= 0)
            {
                _selectedEvent = items[index];
                _selectedInvestigation = null;
                _actionStatus = string.Empty;
                _scroll = Vector2.zero;
            }
            else
            {
                _selectedEvent = null;
                _selectedInvestigation = null;
                _actionStatus = $"事件 #{selection.Id} 不在当前查询窗口中，可能已被过滤或淘汰。";
            }
        }

        private static int FindEventIndex(IReadOnlyList<BattleDiagnosticEvent> items, long sequence)
        {
            if (items == null) return -1;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Sequence == sequence) return i;
            }
            return -1;
        }

        internal static string BuildClipboardText(in BattleDiagnosticEvent evt)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Sequence={evt.Sequence}");
            builder.AppendLine($"Frame={evt.Frame}");
            builder.AppendLine($"Kind={evt.Kind}");
            builder.AppendLine($"Channel={evt.Channel}");
            builder.AppendLine($"Outcome={evt.Outcome}");
            builder.AppendLine($"SourceActorId={evt.SourceActorId}");
            builder.AppendLine($"TargetActorId={evt.TargetActorId}");
            builder.AppendLine($"ConfigId={evt.ConfigId}");
            builder.AppendLine($"RootContextId={evt.RootContextId}");
            builder.AppendLine($"ContextId={evt.ContextId}");
            builder.AppendLine($"SkillRuntime={evt.SkillRuntime}");
            builder.AppendLine($"AttackId={evt.AttackId}");
            builder.AppendLine($"Summary={evt.Summary}");

            if (evt.Payload.TryGetTriggerAnalysis(out var triggerPayload))
            {
                AppendTriggerPayloadClipboard(builder, in triggerPayload, evt.Payload.SchemaVersion);
            }
            else if (evt.Payload.TryGetTriggerAnalysisAggregate(out var triggerAggregate))
            {
                AppendTriggerAggregatePayloadClipboard(
                    builder,
                    in triggerAggregate,
                    evt.Payload.SchemaVersion);
            }
            else if (evt.Payload.TryGetSkillFailure(out var skillFailure))
            {
                AppendSkillFailurePayloadClipboard(builder, in skillFailure, evt.Payload.SchemaVersion);
            }
            else if (evt.Payload.TryGetBuffLifecycle(out var buffLifecycle))
            {
                AppendBuffLifecyclePayloadClipboard(builder, in buffLifecycle, evt.Payload.SchemaVersion);
            }
            else if (evt.Payload.TryGetDamageCalculation(out var damageCalculation))
            {
                AppendDamageCalculationClipboard(builder, in damageCalculation);
            }
            else if (evt.Payload.TryGetInputCommand(out var inputCommand))
            {
                AppendInputCommandClipboard(builder, in inputCommand);
            }
            else if (evt.Payload.TryGetTargetSearch(out var targetSearch))
            {
                AppendTargetSearchClipboard(builder, in targetSearch);
            }
            else if (evt.Payload.TryGetSkillExecution(out var skillExecution))
            {
                AppendSkillExecutionClipboard(builder, in skillExecution);
            }
            else if (evt.Payload.TryGetSyncSnapshotReceived(out var syncPayload))
            {
                builder.AppendLine($"PayloadKind={evt.Payload.Kind}");
                builder.AppendLine($"PayloadSchemaVersion={evt.Payload.SchemaVersion}");
                builder.AppendLine($"SyncAuthoritativeFrame={syncPayload.AuthoritativeFrame}");
                builder.AppendLine($"SyncStateHash={syncPayload.StateHash}");
            }
            else if (evt.Payload.HasValue)
            {
                builder.AppendLine($"PayloadKind={evt.Payload.Kind}");
                builder.AppendLine($"PayloadSchemaVersion={evt.Payload.SchemaVersion}");
            }

            return builder.ToString().TrimEnd();
        }

        private string FormatTriggerFilterSummary()
        {
            var valueLabel = _viewModel.TriggerValueFilter == BattleDiagnosticTriggerValueFilter.Valuable
                ? "高价值"
                : "原始";
            if (!_viewModel.HasTriggerAnalysisFilter) return $"触发={valueLabel}";

            var builder = new StringBuilder($"触发={valueLabel}");
            if (_viewModel.TriggerStage != BattleDiagnosticTriggerAnalysisStage.Unknown)
            {
                builder.Append($" 阶段={BattleDebugDisplayText.TriggerStage(_viewModel.TriggerStage)}");
            }

            if (_viewModel.TriggerResult != BattleDiagnosticTriggerAnalysisResult.Unknown)
            {
                builder.Append($" 结果={BattleDebugDisplayText.TriggerResult(_viewModel.TriggerResult)}");
            }

            if (_viewModel.TriggerContextKind != 0)
            {
                builder.Append($" 上下文={_viewModel.TriggerContextKind}");
            }

            if (_viewModel.TriggerOriginKind != 0)
            {
                builder.Append($" 来源={_viewModel.TriggerOriginKind}");
            }

            return builder.ToString();
        }

        private static void DrawDamageCalculationDetails(in BattleDiagnosticDamageCalculationPayload payload)
        {
            EditorGUILayout.LabelField("Damage stage", payload.Stage.ToString());
            if (!payload.HasCalculation)
            {
                EditorGUILayout.LabelField("Calculation values", "Not captured");
                return;
            }
            EditorGUILayout.LabelField("Base / raw", $"{FormatDamageRaw(payload.BaseDamageRaw)} / {FormatDamageRaw(payload.RawDamageRaw)}");
            EditorGUILayout.LabelField("Mitigated / shield planned", $"{FormatDamageRaw(payload.MitigatedDamageRaw)} / {FormatDamageRaw(payload.ShieldAbsorbRaw)}");
            EditorGUILayout.LabelField("HP planned / applied", $"{FormatDamageRaw(payload.PlannedHpDamageRaw)} / {FormatDamageRaw(payload.AppliedHpDamageRaw)}");
        }

        private static void DrawInputCommandDetails(in BattleDiagnosticInputCommandPayload payload)
        {
            EditorGUILayout.LabelField("命令 / 输入帧", $"{payload.CommandId} / {payload.InputFrame}");
            EditorGUILayout.LabelField("玩家 / 操作码", $"{payload.PlayerId} / {payload.OpCode}");
            EditorGUILayout.LabelField("结果", payload.Succeeded ? "成功" : $"失败 ({payload.FailureCode})");
            if (payload.SkillSlot != 0 || payload.SkillPhase != 0 || payload.TargetActorId != 0)
                EditorGUILayout.LabelField("技能槽位 / 阶段 / 目标",
                    $"{payload.SkillSlot} / {payload.SkillPhase} / {payload.TargetActorId}");
            if (!string.IsNullOrEmpty(payload.Message))
                EditorGUILayout.LabelField("消息", payload.Message, EditorStyles.wordWrappedLabel);
        }

        private static void DrawTargetSearchDetails(in BattleDiagnosticTargetSearchPayload payload)
        {
            EditorGUILayout.LabelField("关联命令", payload.CommandId > 0 ? payload.CommandId.ToString() : "（非玩家输入）");
            EditorGUILayout.LabelField("显式目标", payload.ExplicitTargetActorId.ToString());
            EditorGUILayout.LabelField("候选 / 合格 / 选中",
                $"{payload.CandidateCount} / {payload.EligibleCount} / {payload.SelectedCount}");
            EditorGUILayout.LabelField("选中 Actor", string.IsNullOrEmpty(payload.SelectedActorIds)
                ? "（无）" : payload.SelectedActorIds);
            if (!string.IsNullOrEmpty(payload.DecisionDetails))
                EditorGUILayout.SelectableLabel(payload.DecisionDetails.TrimStart('\r', '\n'),
                    EditorStyles.wordWrappedLabel,
                    GUILayout.Height(Mathf.Min(560f, 24f + 17f * payload.DecisionDetails.Split('\n').Length)));
        }

        private static string FormatDamageRaw(long value) =>
            BattleDiagnosticDamageCalculationPayload.ToDisplayValue(value).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        private static void AppendDamageCalculationClipboard(StringBuilder builder, in BattleDiagnosticDamageCalculationPayload payload)
        {
            builder.AppendLine($"PayloadKind={BattleDiagnosticPayloadKind.DamageCalculation}");
            builder.AppendLine($"PayloadSchemaVersion={BattleDiagnosticDamageCalculationPayload.CurrentSchemaVersion}");
            builder.AppendLine($"DamageStage={payload.Stage}");
            builder.AppendLine($"DamageHasCalculation={payload.HasCalculation}");
            if (!payload.HasCalculation) return;
            builder.AppendLine($"DamageBaseRaw={payload.BaseDamageRaw}");
            builder.AppendLine($"DamageRawRaw={payload.RawDamageRaw}");
            builder.AppendLine($"DamageMitigatedRaw={payload.MitigatedDamageRaw}");
            builder.AppendLine($"DamageShieldRaw={payload.ShieldAbsorbRaw}");
            builder.AppendLine($"DamagePlannedHpRaw={payload.PlannedHpDamageRaw}");
            builder.AppendLine($"DamageAppliedHpRaw={payload.AppliedHpDamageRaw}");
        }

        private static void AppendInputCommandClipboard(StringBuilder builder, in BattleDiagnosticInputCommandPayload payload)
        {
            builder.AppendLine($"PayloadKind={BattleDiagnosticPayloadKind.InputCommand}");
            builder.AppendLine($"PayloadSchemaVersion={BattleDiagnosticInputCommandPayload.CurrentSchemaVersion}");
            builder.AppendLine($"InputCommandId={payload.CommandId}");
            builder.AppendLine($"InputFrame={payload.InputFrame}");
            builder.AppendLine($"InputPlayerId={payload.PlayerId}");
            builder.AppendLine($"InputOpCode={payload.OpCode}");
            builder.AppendLine($"InputSucceeded={payload.Succeeded}");
            builder.AppendLine($"InputFailureCode={payload.FailureCode}");
            builder.AppendLine($"InputSkillSlot={payload.SkillSlot}");
            builder.AppendLine($"InputSkillPhase={payload.SkillPhase}");
            builder.AppendLine($"InputTargetActorId={payload.TargetActorId}");
            builder.AppendLine($"InputMessage={payload.Message}");
        }

        private static void AppendTargetSearchClipboard(StringBuilder builder, in BattleDiagnosticTargetSearchPayload payload)
        {
            builder.AppendLine($"PayloadKind={BattleDiagnosticPayloadKind.TargetSearch}");
            builder.AppendLine($"PayloadSchemaVersion={BattleDiagnosticTargetSearchPayload.CurrentSchemaVersion}");
            builder.AppendLine($"TargetSearchCommandId={payload.CommandId}");
            builder.AppendLine($"TargetSearchExplicitTargetActorId={payload.ExplicitTargetActorId}");
            builder.AppendLine($"TargetSearchCandidateCount={payload.CandidateCount}");
            builder.AppendLine($"TargetSearchEligibleCount={payload.EligibleCount}");
            builder.AppendLine($"TargetSearchSelectedCount={payload.SelectedCount}");
            builder.AppendLine($"TargetSearchSelectedActorIds={payload.SelectedActorIds}");
            builder.AppendLine($"TargetSearchDecisionDetails={payload.DecisionDetails}");
        }

        private static void DrawSkillExecutionDetails(in BattleDiagnosticSkillExecutionPayload payload)
        {
            EditorGUILayout.LabelField("执行阶段", payload.Stage.ToString());
            EditorGUILayout.LabelField("关联命令", payload.CommandId > 0L
                ? payload.CommandId.ToString() : "（非玩家输入）");
            EditorGUILayout.LabelField("槽位 / 等级 / 序列",
                $"{payload.SkillSlot} / {payload.SkillLevel} / {payload.CastSequence}");
            if (payload.ResourceType != 0 || payload.ResourceAmountRaw != 0L ||
                payload.ResourceBeforeRaw != 0L || payload.ResourceAfterRaw != 0L)
            {
                EditorGUILayout.LabelField("资源类型 / 数量",
                    $"{payload.ResourceType} / {FormatDamageRaw(payload.ResourceAmountRaw)}");
                EditorGUILayout.LabelField("资源变化",
                    $"{FormatDamageRaw(payload.ResourceBeforeRaw)} -> {FormatDamageRaw(payload.ResourceAfterRaw)}");
            }
            if (payload.ChargeCost != 0)
                EditorGUILayout.LabelField("充能消耗", payload.ChargeCost.ToString());
            if (payload.CooldownMs != 0 || payload.SharedCooldownMs != 0 || payload.GlobalCooldownMs != 0)
                EditorGUILayout.LabelField("技能 / 共享 / 全局冷却",
                    $"{payload.CooldownMs} / {payload.SharedCooldownMs} / {payload.GlobalCooldownMs} ms");
            if (payload.EndReason != 0 || payload.PendingChildren != 0 || payload.Forced)
                EditorGUILayout.LabelField("结束原因 / 待结束子对象 / 强制",
                    $"{payload.EndReason} / {payload.PendingChildren} / {payload.Forced}");
            if (!string.IsNullOrEmpty(payload.Detail))
                EditorGUILayout.LabelField("详情", payload.Detail, EditorStyles.wordWrappedLabel);
        }

        private static void AppendSkillExecutionClipboard(
            StringBuilder builder,
            in BattleDiagnosticSkillExecutionPayload payload)
        {
            builder.AppendLine($"PayloadKind={BattleDiagnosticPayloadKind.SkillExecution}");
            builder.AppendLine($"PayloadSchemaVersion={BattleDiagnosticSkillExecutionPayload.CurrentSchemaVersion}");
            builder.AppendLine($"SkillExecutionCommandId={payload.CommandId}");
            builder.AppendLine($"SkillExecutionStage={payload.Stage}");
            builder.AppendLine($"SkillExecutionSlot={payload.SkillSlot}");
            builder.AppendLine($"SkillExecutionLevel={payload.SkillLevel}");
            builder.AppendLine($"SkillExecutionSequence={payload.CastSequence}");
            builder.AppendLine($"SkillExecutionEndReason={payload.EndReason}");
            builder.AppendLine($"SkillExecutionResourceType={payload.ResourceType}");
            builder.AppendLine($"SkillExecutionResourceAmountRaw={payload.ResourceAmountRaw}");
            builder.AppendLine($"SkillExecutionResourceBeforeRaw={payload.ResourceBeforeRaw}");
            builder.AppendLine($"SkillExecutionResourceAfterRaw={payload.ResourceAfterRaw}");
            builder.AppendLine($"SkillExecutionChargeCost={payload.ChargeCost}");
            builder.AppendLine($"SkillExecutionCooldownMs={payload.CooldownMs}");
            builder.AppendLine($"SkillExecutionSharedCooldownMs={payload.SharedCooldownMs}");
            builder.AppendLine($"SkillExecutionGlobalCooldownMs={payload.GlobalCooldownMs}");
            builder.AppendLine($"SkillExecutionPendingChildren={payload.PendingChildren}");
            builder.AppendLine($"SkillExecutionForced={payload.Forced}");
            builder.AppendLine($"SkillExecutionDetail={payload.Detail}");
        }

        private static void DrawTriggerPayloadDetails(
            in BattleDiagnosticTriggerAnalysisPayload payload,
            int schemaVersion)
        {
            EditorGUILayout.LabelField(
                "载荷",
                $"触发分析 v{schemaVersion}：触发器={payload.TriggerId}，" +
                $"阶段={BattleDebugDisplayText.TriggerStage(payload.Stage)}，结果={BattleDebugDisplayText.TriggerResult(payload.Result)}");
            EditorGUILayout.LabelField("触发上下文", $"上下文类型={payload.ContextKind}，来源类型={payload.OriginKind}，详情={payload.DetailCode}");
            EditorGUILayout.LabelField(
                "触发预算",
                $"深度={payload.CurrentDepth}，帧内次数={payload.CurrentFrameCount}，" +
                $"根节点次数={payload.CurrentRootCount}，同触发器次数={payload.CurrentSameTriggerCount}");

            if (!string.IsNullOrEmpty(payload.FailureKey) || !string.IsNullOrEmpty(payload.Reason))
            {
                EditorGUILayout.LabelField("失败键", string.IsNullOrEmpty(payload.FailureKey) ? "（无）" : payload.FailureKey);
                EditorGUILayout.LabelField("原因", string.IsNullOrEmpty(payload.Reason) ? "（无）" : payload.Reason);
            }
        }

        private static void DrawTriggerAggregatePayloadDetails(
            in BattleDiagnosticTriggerAnalysisAggregatePayload payload,
            int schemaVersion)
        {
            EditorGUILayout.LabelField(
                "载荷",
                $"触发分析汇总 v{schemaVersion}：触发器={payload.TriggerId}，" +
                $"阶段={BattleDebugDisplayText.TriggerStage(payload.Stage)}，结果={BattleDebugDisplayText.TriggerResult(payload.Result)}");
            EditorGUILayout.LabelField(
                "聚合窗口",
                $"{payload.OccurrenceCount} 次重复，F{payload.FirstFrame}-F{payload.LastFrame}，跨度 {payload.FrameSpan} 帧");
            EditorGUILayout.LabelField(
                "上下文范围",
                $"上下文={payload.FirstContextId}->{payload.LastContextId}，" +
                $"根节点={payload.FirstRootContextId}->{payload.LastRootContextId}");
            EditorGUILayout.LabelField(
                "触发上下文",
                $"上下文类型={payload.ContextKind}，来源类型={payload.OriginKind}，详情={payload.DetailCode}");
            if (!string.IsNullOrEmpty(payload.FailureKey) || !string.IsNullOrEmpty(payload.SampleReason))
            {
                EditorGUILayout.LabelField("失败键", string.IsNullOrEmpty(payload.FailureKey) ? "（无）" : payload.FailureKey);
                EditorGUILayout.LabelField("采样原因", string.IsNullOrEmpty(payload.SampleReason) ? "（无）" : payload.SampleReason);
            }
        }

        private static void DrawSkillFailurePayloadDetails(
            in BattleDiagnosticSkillFailurePayload payload,
            int schemaVersion)
        {
            EditorGUILayout.LabelField(
                "载荷",
                $"技能失败 v{schemaVersion}：代码={payload.Code}，槽位={payload.Slot}");
            EditorGUILayout.LabelField(
                "失败来源 / 阶段",
                $"{BattleDebugDisplayText.SkillFailureSource(payload.Source)} / {BattleDebugDisplayText.SkillFailureStage(payload.Stage)}");
            EditorGUILayout.LabelField("失败消息", payload.Message);
            if (payload.CommandId > 0L)
                EditorGUILayout.LabelField("关联输入命令", payload.CommandId.ToString());
        }

        private static void DrawBuffLifecyclePayloadDetails(
            in BattleDiagnosticBuffLifecyclePayload payload,
            int schemaVersion)
        {
            EditorGUILayout.LabelField(
                "载荷",
                $"Buff 生命周期 v{schemaVersion}：阶段={BattleDebugDisplayText.BuffLifecycleStage(payload.Stage)}，" +
                $"层数={payload.PreviousStackCount}->{payload.StackCount}，上限={payload.MaxStacks}");
            EditorGUILayout.LabelField(
                "Buff 时间（毫秒）",
                $"持续={payload.DurationMilliseconds}，剩余={payload.RemainingMilliseconds}，" +
                $"周期剩余={payload.IntervalRemainingMilliseconds}");
            EditorGUILayout.LabelField(
                "Buff 修改器",
                $"绑定数={payload.ModifierBindingCount}，来源={payload.ModifierSourceId}，" +
                $"移除原因={payload.RemoveReason}");
        }

        private static void AppendBuffLifecyclePayloadClipboard(
            StringBuilder builder,
            in BattleDiagnosticBuffLifecyclePayload payload,
            int schemaVersion)
        {
            builder.AppendLine($"PayloadKind={BattleDiagnosticPayloadKind.BuffLifecycle}");
            builder.AppendLine($"PayloadSchemaVersion={schemaVersion}");
            builder.AppendLine($"BuffLifecycleStage={payload.Stage}");
            builder.AppendLine($"BuffLifecycleStackCount={payload.StackCount}");
            builder.AppendLine($"BuffLifecyclePreviousStackCount={payload.PreviousStackCount}");
            builder.AppendLine($"BuffLifecycleDurationMilliseconds={payload.DurationMilliseconds}");
            builder.AppendLine($"BuffLifecycleRemainingMilliseconds={payload.RemainingMilliseconds}");
            builder.AppendLine($"BuffLifecycleIntervalRemainingMilliseconds={payload.IntervalRemainingMilliseconds}");
            builder.AppendLine($"BuffLifecycleMaxStacks={payload.MaxStacks}");
            builder.AppendLine($"BuffLifecycleModifierBindingCount={payload.ModifierBindingCount}");
            builder.AppendLine($"BuffLifecycleModifierSourceId={payload.ModifierSourceId}");
            builder.AppendLine($"BuffLifecycleRemoveReason={payload.RemoveReason}");
        }

        private static void AppendSkillFailurePayloadClipboard(
            StringBuilder builder,
            in BattleDiagnosticSkillFailurePayload payload,
            int schemaVersion)
        {
            builder.AppendLine($"PayloadKind={BattleDiagnosticPayloadKind.SkillFailure}");
            builder.AppendLine($"PayloadSchemaVersion={schemaVersion}");
            builder.AppendLine($"SkillFailureSlot={payload.Slot}");
            builder.AppendLine($"SkillFailureSource={payload.Source}");
            builder.AppendLine($"SkillFailureStage={payload.Stage}");
            builder.AppendLine($"SkillFailureCode={payload.Code}");
            builder.AppendLine($"SkillFailureMessage={payload.Message}");
            builder.AppendLine($"SkillFailureCommandId={payload.CommandId}");
        }

        private static void AppendTriggerPayloadClipboard(
            StringBuilder builder,
            in BattleDiagnosticTriggerAnalysisPayload payload,
            int schemaVersion)
        {
            builder.AppendLine($"PayloadKind={BattleDiagnosticPayloadKind.TriggerAnalysis}");
            builder.AppendLine($"PayloadSchemaVersion={schemaVersion}");
            builder.AppendLine($"TriggerId={payload.TriggerId}");
            builder.AppendLine($"TriggerContextKind={payload.ContextKind}");
            builder.AppendLine($"TriggerOriginKind={payload.OriginKind}");
            builder.AppendLine($"TriggerStage={payload.Stage}");
            builder.AppendLine($"TriggerResult={payload.Result}");
            builder.AppendLine($"TriggerDetailCode={payload.DetailCode}");
            builder.AppendLine($"TriggerCurrentDepth={payload.CurrentDepth}");
            builder.AppendLine($"TriggerCurrentFrameCount={payload.CurrentFrameCount}");
            builder.AppendLine($"TriggerCurrentRootCount={payload.CurrentRootCount}");
            builder.AppendLine($"TriggerCurrentSameTriggerCount={payload.CurrentSameTriggerCount}");
            builder.AppendLine($"TriggerFailureKey={payload.FailureKey}");
            builder.AppendLine($"TriggerReason={payload.Reason}");
        }

        private static void AppendTriggerAggregatePayloadClipboard(
            StringBuilder builder,
            in BattleDiagnosticTriggerAnalysisAggregatePayload payload,
            int schemaVersion)
        {
            builder.AppendLine($"PayloadKind={BattleDiagnosticPayloadKind.TriggerAnalysisAggregate}");
            builder.AppendLine($"PayloadSchemaVersion={schemaVersion}");
            builder.AppendLine($"TriggerId={payload.TriggerId}");
            builder.AppendLine($"TriggerContextKind={payload.ContextKind}");
            builder.AppendLine($"TriggerOriginKind={payload.OriginKind}");
            builder.AppendLine($"TriggerStage={payload.Stage}");
            builder.AppendLine($"TriggerResult={payload.Result}");
            builder.AppendLine($"TriggerDetailCode={payload.DetailCode}");
            builder.AppendLine($"OccurrenceCount={payload.OccurrenceCount}");
            builder.AppendLine($"FirstFrame={payload.FirstFrame}");
            builder.AppendLine($"LastFrame={payload.LastFrame}");
            builder.AppendLine($"FirstContextId={payload.FirstContextId}");
            builder.AppendLine($"LastContextId={payload.LastContextId}");
            builder.AppendLine($"FirstRootContextId={payload.FirstRootContextId}");
            builder.AppendLine($"LastRootContextId={payload.LastRootContextId}");
            builder.AppendLine($"TriggerFailureKey={payload.FailureKey}");
            builder.AppendLine($"TriggerSampleReason={payload.SampleReason}");
        }

        private static bool HasCorrelation(in BattleDiagnosticEvent diagnosticEvent)
        {
            return diagnosticEvent.RootContextId != 0 ||
                   diagnosticEvent.ContextId != 0 ||
                   diagnosticEvent.SkillRuntime.RuntimeId != 0 ||
                   diagnosticEvent.AttackId != 0;
        }

        private void DrawFrameHistogram(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> loadedItems,
            IReadOnlyList<BattleDiagnosticEvent> visibleItems)
        {
            EditorGUILayout.LabelField("帧事件分布", EditorStyles.boldLabel);
            var automaticRange = ResolveEventRange(loadedItems);
            var visibleRange = ctx.WorkspaceState != null
                ? ctx.WorkspaceState.TimeRange.Resolve(automaticRange)
                : automaticRange;
            if (!visibleRange.IsValid)
            {
                EditorGUILayout.HelpBox(
                    "当前查询没有可汇总的事件。",
                    MessageType.Info);
                return;
            }

            _overviewItems.Clear();
            if (loadedItems != null)
            {
                for (var i = 0; i < loadedItems.Count; i++)
                {
                    _overviewItems.Add(new BattleDebugTimelineOverviewItem(
                        loadedItems[i].Frame,
                        loadedItems[i].Frame));
                }
            }

            var cursorFrame = ctx.WorkspaceState?.FrameCursor.Frame ??
                              BattleDiagnosticFrames.Invalid;
            var overviewInteraction = BattleDebugTimelineOverview.Draw(
                _overviewItems,
                automaticRange,
                visibleRange,
                cursorFrame,
                _overviewBuffer);
            ApplyTimelineInteraction(in ctx, overviewInteraction);

            _histogramSamples.Clear();
            if (visibleItems != null)
            {
                for (var i = 0; i < visibleItems.Count; i++)
                {
                    var item = visibleItems[i];
                    if (!visibleRange.Contains(item.Frame)) continue;
                    _histogramSamples.Add(new BattleDebugHistogramSample(
                        item.Frame,
                        GetHistogramSeriesIndex(item.Outcome)));
                }
            }

            var interaction = BattleDebugHistogram.Draw(
                _histogramSamples,
                visibleRange,
                _histogramSeries,
                cursorFrame);
            BattleDebugLegend.Draw(_histogramLegend);
            ApplyTimelineInteraction(in ctx, interaction);
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

        private static BattleDiagnosticFrameRange ResolveEventRange(
            IReadOnlyList<BattleDiagnosticEvent> items)
        {
            if (items == null || items.Count == 0)
            {
                return new BattleDiagnosticFrameRange(
                    BattleDiagnosticFrames.Invalid,
                    BattleDiagnosticFrames.Invalid);
            }

            var minFrame = items[0].Frame;
            var maxFrame = items[0].Frame;
            for (var i = 1; i < items.Count; i++)
            {
                minFrame = Mathf.Min(minFrame, items[i].Frame);
                maxFrame = Mathf.Max(maxFrame, items[i].Frame);
            }
            return new BattleDiagnosticFrameRange(minFrame, maxFrame);
        }

        private static int GetHistogramSeriesIndex(BattleDiagnosticEventOutcome outcome)
        {
            switch (outcome)
            {
                case BattleDiagnosticEventOutcome.Succeeded:
                    return 0;
                case BattleDiagnosticEventOutcome.Failed:
                case BattleDiagnosticEventOutcome.Cancelled:
                case BattleDiagnosticEventOutcome.Interrupted:
                    return 2;
                default:
                    return 1;
            }
        }

        private enum EventsWidgetKind
        {
            Overview = 0,
            TriggerFlow = 1,
            List = 2,
            Details = 3
        }

        private sealed class EventsWidget : IBattleDebugWidget
        {
            private readonly BattleDebugDiagnosticEventsPanel _owner;
            private readonly EventsWidgetKind _kind;

            public EventsWidget(BattleDebugDiagnosticEventsPanel owner, EventsWidgetKind kind)
            {
                _owner = owner;
                _kind = kind;
                Descriptor = BattleDebugModuleCatalog.Describe(owner);
            }

            public BattleDebugModuleDescriptor Descriptor { get; }
            public string StableId
            {
                get
                {
                    switch (_kind)
                    {
                        case EventsWidgetKind.TriggerFlow:
                            return BattleDebugWidgetIds.EventsTriggerFlow;
                        case EventsWidgetKind.List:
                            return BattleDebugWidgetIds.EventsList;
                        case EventsWidgetKind.Details:
                            return BattleDebugWidgetIds.EventsDetails;
                        default:
                            return BattleDebugWidgetIds.EventsOverview;
                    }
                }
            }

            public string DisplayName
            {
                get
                {
                    switch (_kind)
                    {
                        case EventsWidgetKind.TriggerFlow:
                            return "触发流程";
                        case EventsWidgetKind.List:
                            return "事件列表";
                        case EventsWidgetKind.Details:
                            return "事件详情";
                        default:
                            return "事件概览";
                    }
                }
            }

            public bool OwnsScrollView =>
                _kind == EventsWidgetKind.TriggerFlow ||
                _kind == EventsWidgetKind.List;

            public bool IsAvailable(in BattleDebugContext context)
            {
                return _owner.IsVisible(in context);
            }

            public void Draw(in BattleDebugContext context)
            {
                switch (_kind)
                {
                    case EventsWidgetKind.TriggerFlow:
                        _owner.DrawTriggerFlowWidget(in context);
                        break;
                    case EventsWidgetKind.Overview:
                        _owner.DrawOverviewWidget(in context);
                        break;
                    case EventsWidgetKind.List:
                        _owner.DrawListWidget(in context);
                        break;
                    default:
                        _owner.DrawDetailsWidget(in context);
                        break;
                }
            }
        }

        private static Color GetOutcomeColor(BattleDiagnosticEventOutcome outcome)
        {
            switch (outcome)
            {
                case BattleDiagnosticEventOutcome.Failed:
                    return new Color(1f, 0.6f, 0.6f);
                case BattleDiagnosticEventOutcome.Cancelled:
                case BattleDiagnosticEventOutcome.Interrupted:
                    return new Color(1f, 0.85f, 0.5f);
                case BattleDiagnosticEventOutcome.None:
                    return new Color(0.85f, 0.85f, 0.85f);
                default:
                    return Color.white;
            }
        }

        private static Color GetTriggerResultColor(BattleDiagnosticTriggerAnalysisResult result)
        {
            switch (result)
            {
                case BattleDiagnosticTriggerAnalysisResult.Passed:
                    return new Color(0.72f, 1f, 0.76f);
                case BattleDiagnosticTriggerAnalysisResult.Failed:
                    return new Color(1f, 0.62f, 0.62f);
                case BattleDiagnosticTriggerAnalysisResult.Blocked:
                    return new Color(1f, 0.84f, 0.5f);
                case BattleDiagnosticTriggerAnalysisResult.Skipped:
                    return new Color(0.78f, 0.82f, 0.88f);
                default:
                    return Color.white;
            }
        }
    }
}
