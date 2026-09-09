using System.Text;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    [BattleDebugModule(
        BattleDebugModuleIds.RuntimeObjects,
        "运行时",
        RequiredCapabilities = BattleDiagnosticCapabilities.RuntimeObjects,
        Selections = BattleDebugModuleSelectionSupport.Frame |
                     BattleDebugModuleSelectionSupport.Actor |
                     BattleDebugModuleSelectionSupport.Event |
                     BattleDebugModuleSelectionSupport.Trace |
                     BattleDebugModuleSelectionSupport.RuntimeObject |
                     BattleDebugModuleSelectionSupport.Config)]
    internal sealed class BattleDebugRuntimeObjectsPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        private static readonly string[] KindLabels = { "全部类型", "Actor", "投射物", "区域", "召唤物" };
        private static readonly string[] StateLabels = { "全部状态", "活跃", "已结束" };
        private static readonly string[] CompletenessLabels = { "全部完整度", "完整", "部分完整", "不可靠" };

        private readonly BattleDebugRuntimeObjectsViewModel _viewModel =
            new BattleDebugRuntimeObjectsViewModel();
        private Vector2 _listScroll;

        public string Name => "运行时对象";
        public int Order => 407;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => true;

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

            var contentWidth = ctx.AvailableContentWidth > 0f
                ? ctx.AvailableContentWidth
                : EditorGUIUtility.currentViewWidth;
            DrawFilterToolbar(in ctx, contentWidth);
            _viewModel.RefreshIfNeeded(session);

            if (!_viewModel.IsSupported)
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.Info);
                return;
            }

            DrawPanelHeader();
            DrawSummary(contentWidth);
            DrawCatalogWarnings();
            EditorGUILayout.Space(4f);
            var useSplitLayout = _viewModel.Selected.HasValue &&
                                 contentWidth >= 900f;
            if (useSplitLayout)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.MinWidth(480f));
                DrawObjectList(in ctx, session, expandHeight: true, compact: true);
                EditorGUILayout.EndVertical();
                GUILayout.Space(8f);
                EditorGUILayout.BeginVertical(GUILayout.Width(340f));
                DrawSelectionDetails(in ctx, session);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                var compact = contentWidth < 820f;
                DrawObjectList(in ctx, session, expandHeight: false, compact: compact);
                DrawSelectionDetails(in ctx, session);
            }
        }

        private void DrawFilterToolbar(in BattleDebugContext ctx, float contentWidth)
        {
            var compact = contentWidth < 620f;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("类型", GUILayout.Width(30f));
            var kind = (BattleDiagnosticRuntimeObjectKind)EditorGUILayout.Popup(
                (int)_viewModel.Kind,
                KindLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(90f));
            if (kind != _viewModel.Kind)
            {
                _viewModel.Kind = kind;
                _viewModel.Invalidate();
                _listScroll = Vector2.zero;
            }

            GUILayout.Label("状态", GUILayout.Width(34f));
            var state = (BattleDiagnosticRuntimeObjectState)EditorGUILayout.Popup(
                (int)_viewModel.State,
                StateLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(82f));
            if (state != _viewModel.State)
            {
                _viewModel.State = state;
                _viewModel.Invalidate();
                _listScroll = Vector2.zero;
            }

            GUILayout.Label("完整度", GUILayout.Width(48f));
            var completeness = (BattleDiagnosticDataCompleteness)EditorGUILayout.Popup(
                (int)_viewModel.Completeness,
                CompletenessLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(112f));
            if (completeness != _viewModel.Completeness)
            {
                _viewModel.Completeness = completeness;
                _viewModel.Invalidate();
                _listScroll = Vector2.zero;
            }

            if (!compact)
            {
                DrawFilterActions(in ctx);
            }
            EditorGUILayout.EndHorizontal();

            if (compact)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                DrawFilterActions(in ctx);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawFilterActions(in BattleDebugContext ctx)
        {
            var unreliableActive = _viewModel.Completeness == BattleDiagnosticDataCompleteness.Unreliable;
            var newUnreliableActive = GUILayout.Toggle(
                unreliableActive,
                "不可靠",
                EditorStyles.toolbarButton,
                GUILayout.Width(78f));
            if (newUnreliableActive != unreliableActive)
            {
                _viewModel.Completeness = newUnreliableActive
                    ? BattleDiagnosticDataCompleteness.Unreliable
                    : BattleDiagnosticDataCompleteness.Unknown;
                _viewModel.Invalidate();
                _listScroll = Vector2.zero;
            }

            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(!HasActiveFilter());
            if (GUILayout.Button(
                    new GUIContent("清除", "清除所有对象目录过滤条件"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(44f)))
            {
                ClearFilters();
                _listScroll = Vector2.zero;
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button(
                    new GUIContent("刷新", "按最新版本刷新对象目录"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(58f)))
            {
                _viewModel.Invalidate();
                ctx.RequestRepaint?.Invoke();
            }
        }

        private void DrawPanelHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("运行时对象目录", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var total = _viewModel.Summary.HasValue
                ? _viewModel.Summary.Value.TotalCount.ToString()
                : "?";
            GUILayout.Label(
                $"显示 {_viewModel.LoadedCount} / {total}   版本 {_viewModel.WorksetRevision}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary(float contentWidth)
        {
            if (!_viewModel.Summary.HasValue)
            {
                if (_viewModel.SummaryQueryStatus.Phase == BattleDiagnosticQueryPhase.Unavailable ||
                    _viewModel.SummaryQueryStatus.Phase == BattleDiagnosticQueryPhase.Error)
                {
                    EditorGUILayout.HelpBox(
                        "对象汇总不可用：" + _viewModel.SummaryQueryStatus.Message,
                        MessageType.Warning);
                }
                return;
            }

            var summary = _viewModel.Summary.Value;
            var compact = contentWidth < 620f;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (DrawSummaryButton(
                    "全部",
                    summary.TotalCount,
                    !HasActiveFilter(),
                    Color.white,
                    72f))
            {
                ClearFilters();
            }
            if (DrawSummaryButton(
                    "完整",
                    summary.CompleteCount,
                    _viewModel.Completeness == BattleDiagnosticDataCompleteness.Complete,
                    new Color(0.7f, 0.92f, 0.76f),
                    88f))
            {
                ToggleCompleteness(BattleDiagnosticDataCompleteness.Complete);
            }
            if (DrawSummaryButton(
                    "部分",
                    summary.PartialCount,
                    _viewModel.Completeness == BattleDiagnosticDataCompleteness.Partial,
                    new Color(1f, 0.86f, 0.55f),
                    76f))
            {
                ToggleCompleteness(BattleDiagnosticDataCompleteness.Partial);
            }
            if (DrawSummaryButton(
                    "不可靠",
                    summary.UnreliableCount,
                    _viewModel.Completeness == BattleDiagnosticDataCompleteness.Unreliable,
                    new Color(1f, 0.58f, 0.58f),
                    94f))
            {
                ToggleCompleteness(BattleDiagnosticDataCompleteness.Unreliable);
            }
            if (compact)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label("生命周期", EditorStyles.miniLabel, GUILayout.Width(58f));
            }
            if (DrawSummaryButton(
                    "活跃",
                    summary.ActiveCount,
                    _viewModel.State == BattleDiagnosticRuntimeObjectState.Active,
                    new Color(0.65f, 0.88f, 1f),
                    74f))
            {
                ToggleState(BattleDiagnosticRuntimeObjectState.Active);
            }
            if (DrawSummaryButton(
                    "已结束",
                    summary.EndedCount,
                    _viewModel.State == BattleDiagnosticRuntimeObjectState.Ended,
                    Color.white,
                    74f))
            {
                ToggleState(BattleDiagnosticRuntimeObjectState.Ended);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"目录 {BattleDebugDisplayText.Completeness(summary.Completeness)}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static bool DrawSummaryButton(
            string label,
            int count,
            bool active,
            Color color,
            float width)
        {
            var oldColor = GUI.color;
            GUI.color = active ? color : Color.Lerp(Color.white, color, 0.42f);
            var clicked = GUILayout.Button(
                new GUIContent($"{label} {count}", $"只显示{label}对象"),
                active ? EditorStyles.toolbarButton : EditorStyles.miniButton,
                GUILayout.Width(width),
                GUILayout.Height(18f));
            GUI.color = oldColor;
            return clicked;
        }

        private void DrawCatalogWarnings()
        {
            if (_viewModel.Summary.HasValue)
            {
                var summary = _viewModel.Summary.Value;
                if (summary.BackfillFailureCount > 0L)
                {
                    EditorGUILayout.HelpBox(
                        $"{summary.BackfillAttemptCount} 次对象回填中有 {summary.BackfillFailureCount} 次失败。" +
                        "部分运行时 ID 无法可靠溯源。",
                        MessageType.Error);
                }
                else if (summary.Truncated)
                {
                    EditorGUILayout.HelpBox(
                        "运行时对象目录已截断，缺失对象可能已被淘汰。",
                        MessageType.Warning);
                }
                else if (summary.PartialCount > 0 || summary.UnreliableCount > 0)
                {
                    EditorGUILayout.HelpBox(
                        $"目录包含 {summary.PartialCount} 个部分完整对象和 " +
                        $"{summary.UnreliableCount} 个不可靠对象。",
                        MessageType.Warning);
                }
            }

            if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                var type = _viewModel.QueryStatus.Phase == BattleDiagnosticQueryPhase.Error
                    ? MessageType.Error
                    : _viewModel.QueryStatus.Phase == BattleDiagnosticQueryPhase.Partial
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, type);
            }
        }

        private void DrawObjectList(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session,
            bool expandHeight,
            bool compact)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("对象", EditorStyles.boldLabel, GUILayout.Width(64f));
            if (HasActiveFilter())
                GUILayout.Label(BuildFilterLabel(), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            DrawColumnHeader(compact);
            _listScroll = expandHeight
                ? EditorGUILayout.BeginScrollView(
                    _listScroll,
                    GUILayout.MinHeight(240f),
                    GUILayout.ExpandHeight(true))
                : EditorGUILayout.BeginScrollView(
                    _listScroll,
                    GUILayout.MinHeight(180f),
                    GUILayout.MaxHeight(420f));

            var items = _viewModel.Items;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                DrawObjectRow(in ctx, in item, compact);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(
                _viewModel.HasMore
                    ? $"已加载 {_viewModel.LoadedCount} 个，还有更多"
                    : $"已加载 {_viewModel.LoadedCount} 个",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (_viewModel.HasMore)
            {
                if (GUILayout.Button("加载更多", GUILayout.Width(100f)))
                {
                    _viewModel.LoadMore(session);
                    ctx.RequestRepaint?.Invoke();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_viewModel.PagingStatusMessage))
                EditorGUILayout.LabelField(_viewModel.PagingStatusMessage, EditorStyles.miniLabel);
        }

        private static void DrawColumnHeader(bool compact)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("类型", GUILayout.Width(compact ? 66f : 72f));
            GUILayout.Label("运行时 ID", GUILayout.Width(compact ? 104f : 112f));
            GUILayout.Label("名称", GUILayout.MinWidth(90f), GUILayout.MaxWidth(150f));
            if (!compact)
            {
                GUILayout.Label("定义", GUILayout.Width(125f));
            }
            GUILayout.Label("状态", GUILayout.Width(62f));
            GUILayout.Label("完整度", GUILayout.Width(78f));
            if (!compact)
            {
                GUILayout.Label("帧范围", GUILayout.Width(104f));
                GUILayout.Label("来源 / 所有者", GUILayout.Width(118f));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawObjectRow(
            in BattleDebugContext ctx,
            in BattleDiagnosticRuntimeObject runtimeObject,
            bool compact)
        {
            var selected = _viewModel.Selected.HasValue &&
                           SameIdentity(_viewModel.Selected.Value, runtimeObject);
            var oldColor = GUI.color;
            GUI.color = GetCompletenessColor(runtimeObject.Completeness);
            EditorGUILayout.BeginHorizontal(
                selected ? EditorStyles.helpBox : GUIStyle.none,
                GUILayout.Height(22f));
            GUILayout.Label(BattleDebugDisplayText.RuntimeObjectKind(runtimeObject.Kind), GUILayout.Width(compact ? 66f : 72f));
            if (GUILayout.Button(
                    new GUIContent(
                        $"{runtimeObject.RuntimeId}:{runtimeObject.Generation}",
                        "选择此运行时对象"),
                    selected ? EditorStyles.toolbarButton : EditorStyles.miniButton,
                    GUILayout.Width(compact ? 104f : 112f)))
            {
                _viewModel.Select(in runtimeObject);
                ctx.RequestRepaint?.Invoke();
            }
            GUILayout.Label(
                EmptyAsDash(runtimeObject.DisplayName),
                EditorStyles.miniLabel,
                GUILayout.MinWidth(90f),
                GUILayout.MaxWidth(150f));
            if (!compact)
            {
                GUILayout.Label(
                    $"{BattleDebugDisplayText.DefinitionKind(runtimeObject.DefinitionKind)}:{runtimeObject.DefinitionId}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(125f));
            }
            GUILayout.Label(BattleDebugDisplayText.RuntimeObjectState(runtimeObject.State), GUILayout.Width(62f));
            GUILayout.Label(BattleDebugDisplayText.Completeness(runtimeObject.Completeness), GUILayout.Width(78f));
            if (!compact)
            {
                GUILayout.Label(FormatFrames(in runtimeObject), EditorStyles.miniLabel, GUILayout.Width(104f));
                GUILayout.Label(
                    $"{runtimeObject.SourceActorId} / {runtimeObject.OwnerActorId}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(118f));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUI.color = oldColor;
        }

        private void DrawSelectionDetails(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session)
        {
            if (!_viewModel.Selected.HasValue) return;
            var runtimeObject = _viewModel.Selected.Value;
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("已选对象", EditorStyles.boldLabel, GUILayout.Width(105f));
            var selectedIndex = _viewModel.SelectedIndex;
            GUILayout.Label(
                selectedIndex >= 0 ? $"{selectedIndex + 1} / {_viewModel.LoadedCount}" : string.Empty,
                EditorStyles.miniLabel,
                GUILayout.Width(58f));
            EditorGUI.BeginDisabledGroup(selectedIndex <= 0);
            if (GUILayout.Button(new GUIContent("<", "选择上一个对象"), EditorStyles.toolbarButton, GUILayout.Width(24f)))
                SelectAdjacent(in ctx, -1);
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(selectedIndex < 0 || selectedIndex >= _viewModel.LoadedCount - 1);
            if (GUILayout.Button(new GUIContent(">", "选择下一个对象"), EditorStyles.toolbarButton, GUILayout.Width(24f)))
                SelectAdjacent(in ctx, 1);
            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("复制", "复制对象的全部诊断字段"), EditorStyles.toolbarButton, GUILayout.Width(42f)))
                EditorGUIUtility.systemCopyBuffer = BuildClipboardText(in runtimeObject);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("标识", runtimeObject.Reference.ToString());
            EditorGUILayout.LabelField("显示名称", EmptyAsDash(runtimeObject.DisplayName));
            EditorGUILayout.LabelField(
                "定义",
                $"{BattleDebugDisplayText.DefinitionKind(runtimeObject.DefinitionKind)}:{runtimeObject.DefinitionId}");
            EditorGUILayout.LabelField("状态 / 完整度", $"{BattleDebugDisplayText.RuntimeObjectState(runtimeObject.State)} / {BattleDebugDisplayText.Completeness(runtimeObject.Completeness)}");
            EditorGUILayout.LabelField("发现方式", BattleDebugDisplayText.DiscoveryKind(runtimeObject.DiscoveryKind));
            EditorGUILayout.LabelField("帧范围", FormatFrames(in runtimeObject));
            if (runtimeObject.WasBackfilled)
                EditorGUILayout.LabelField("回填帧", runtimeObject.BackfilledFrame.ToString());
            EditorGUILayout.LabelField(
                "关联 / 来源 / 所有者 / 目标",
                $"{runtimeObject.RelatedActorId} / {runtimeObject.SourceActorId} / " +
                $"{runtimeObject.OwnerActorId} / {runtimeObject.TargetActorId}");
            EditorGUILayout.LabelField(
                "根节点 / 上下文",
                $"{runtimeObject.RootContextId} / {runtimeObject.ContextId}");
            if (runtimeObject.EndReason != 0)
                EditorGUILayout.LabelField("结束原因", runtimeObject.EndReason.ToString());

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(ctx.OpenEvent == null);
            if (GUILayout.Button("关联事件", GUILayout.Width(100f)))
            {
                if (_viewModel.TryFindRelatedEvent(session, in runtimeObject, out var diagnosticEvent))
                    ctx.OpenEvent?.Invoke(diagnosticEvent);
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(
                ctx.OpenTrace == null || runtimeObject.RootContextId == 0L);
            if (GUILayout.Button("打开 Trace", GUILayout.Width(88f)))
                ctx.OpenTrace?.Invoke(runtimeObject.RootContextId, runtimeObject.ContextId);
            EditorGUI.EndDisabledGroup();

            var actorId = _viewModel.GetPreferredActorId();
            EditorGUI.BeginDisabledGroup(ctx.SelectActor == null || actorId == 0L);
            if (GUILayout.Button("选择 Actor", GUILayout.Width(88f)))
                ctx.SelectActor?.Invoke(actorId);
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_viewModel.RelatedEventStatusMessage))
                EditorGUILayout.HelpBox(_viewModel.RelatedEventStatusMessage, MessageType.Info);
        }

        private void SelectAdjacent(in BattleDebugContext ctx, int offset)
        {
            if (!_viewModel.SelectAdjacent(offset)) return;
            var selectedIndex = _viewModel.SelectedIndex;
            if (selectedIndex >= 0)
            {
                _listScroll.y = Mathf.Max(0f, selectedIndex * 22f - 44f);
            }
            ctx.RequestRepaint?.Invoke();
        }

        private bool HasActiveFilter()
        {
            return _viewModel.Kind != BattleDiagnosticRuntimeObjectKind.Unknown ||
                   _viewModel.State != BattleDiagnosticRuntimeObjectState.Unknown ||
                   _viewModel.Completeness != BattleDiagnosticDataCompleteness.Unknown;
        }

        private string BuildFilterLabel()
        {
            var text = new StringBuilder(64);
            if (_viewModel.Kind != BattleDiagnosticRuntimeObjectKind.Unknown)
                text.Append(BattleDebugDisplayText.RuntimeObjectKind(_viewModel.Kind));
            if (_viewModel.State != BattleDiagnosticRuntimeObjectState.Unknown)
            {
                if (text.Length > 0) text.Append(" / ");
                text.Append(BattleDebugDisplayText.RuntimeObjectState(_viewModel.State));
            }
            if (_viewModel.Completeness != BattleDiagnosticDataCompleteness.Unknown)
            {
                if (text.Length > 0) text.Append(" / ");
                text.Append(BattleDebugDisplayText.Completeness(_viewModel.Completeness));
            }
            return text.ToString();
        }

        private void ClearFilters()
        {
            _viewModel.Kind = BattleDiagnosticRuntimeObjectKind.Unknown;
            _viewModel.State = BattleDiagnosticRuntimeObjectState.Unknown;
            _viewModel.Completeness = BattleDiagnosticDataCompleteness.Unknown;
            _viewModel.Invalidate();
        }

        private void ToggleCompleteness(BattleDiagnosticDataCompleteness completeness)
        {
            _viewModel.Completeness = _viewModel.Completeness == completeness
                ? BattleDiagnosticDataCompleteness.Unknown
                : completeness;
            _viewModel.Invalidate();
            _listScroll = Vector2.zero;
        }

        private void ToggleState(BattleDiagnosticRuntimeObjectState state)
        {
            _viewModel.State = _viewModel.State == state
                ? BattleDiagnosticRuntimeObjectState.Unknown
                : state;
            _viewModel.Invalidate();
            _listScroll = Vector2.zero;
        }

        private static bool SameIdentity(
            in BattleDiagnosticRuntimeObject left,
            in BattleDiagnosticRuntimeObject right)
        {
            return left.Kind == right.Kind &&
                   left.RuntimeId == right.RuntimeId &&
                   left.Generation == right.Generation;
        }

        private static string FormatFrames(in BattleDiagnosticRuntimeObject runtimeObject)
        {
            var start = BattleDiagnosticFrames.IsValid(runtimeObject.CreatedFrame)
                ? runtimeObject.CreatedFrame.ToString()
                : "?";
            var end = BattleDiagnosticFrames.IsValid(runtimeObject.DestroyedFrame)
                ? runtimeObject.DestroyedFrame.ToString()
                : runtimeObject.State == BattleDiagnosticRuntimeObjectState.Active ? "进行中" : "?";
            return start + " -> " + end;
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private static Color GetCompletenessColor(BattleDiagnosticDataCompleteness completeness)
        {
            switch (completeness)
            {
                case BattleDiagnosticDataCompleteness.Partial:
                    return new Color(1f, 0.86f, 0.55f);
                case BattleDiagnosticDataCompleteness.Unreliable:
                    return new Color(1f, 0.58f, 0.58f);
                default:
                    return Color.white;
            }
        }

        private static string BuildClipboardText(in BattleDiagnosticRuntimeObject runtimeObject)
        {
            var text = new StringBuilder(256);
            text.Append("Object=").Append(runtimeObject.Reference)
                .Append(" Name=").Append(runtimeObject.DisplayName)
                .Append(" Definition=").Append(runtimeObject.DefinitionKind).Append(':').Append(runtimeObject.DefinitionId)
                .Append(" State=").Append(runtimeObject.State)
                .Append(" Completeness=").Append(runtimeObject.Completeness)
                .Append(" Discovery=").Append(runtimeObject.DiscoveryKind)
                .Append(" Frames=").Append(FormatFrames(in runtimeObject))
                .Append(" RelatedActor=").Append(runtimeObject.RelatedActorId)
                .Append(" SourceActor=").Append(runtimeObject.SourceActorId)
                .Append(" OwnerActor=").Append(runtimeObject.OwnerActorId)
                .Append(" TargetActor=").Append(runtimeObject.TargetActorId)
                .Append(" RootContext=").Append(runtimeObject.RootContextId)
                .Append(" Context=").Append(runtimeObject.ContextId);
            return text.ToString();
        }
    }
}
