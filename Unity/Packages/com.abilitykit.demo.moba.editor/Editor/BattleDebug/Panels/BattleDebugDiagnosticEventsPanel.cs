using System.Collections.Generic;
using System.Text;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    /// <summary>
    /// 有界历史事件面板：通过 <see cref="BattleDebugDiagnosticEventsViewModel"/>
    /// 查询并展示伤害、治疗、效果和其他运行事件，支持时间窗口、Actor 关系和文本检索。
    /// 只消费已定义的诊断查询契约，不建立旁路数据源。
    /// </summary>
    internal sealed class BattleDebugDiagnosticEventsPanel :
        IBattleDebugPanel,
        IBattleDebugPanelLayout,
        IBattleDebugEventsTarget
    {
        public string Name => "诊断事件";
        public int Order => 400;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => true;

        private readonly BattleDebugDiagnosticEventsViewModel _viewModel = new BattleDebugDiagnosticEventsViewModel();
        private Vector2 _scroll;
        private BattleDiagnosticEvent? _selectedEvent;
        private string _actionStatus = string.Empty;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void OpenForActor(long actorId)
        {
            if (actorId <= 0) return;

            _viewModel.ClearCorrelationFocus();
            _viewModel.FilterBySelectedActor = true;
            _viewModel.ActorRelation = BattleDiagnosticActorRelation.Either;
            _viewModel.FailuresOnly = false;
            _viewModel.SearchText = string.Empty;
            _viewModel.InvalidateCache();
            _selectedEvent = null;
            _actionStatus = string.Empty;
            _scroll = Vector2.zero;
        }

        public void Draw(in BattleDebugContext ctx)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "诊断会话不可用。请启动战斗或打开包含 Battle Diagnostics 的 Artifact。",
                    MessageType.Info);
                return;
            }

            DrawFilterBar(ctx, session);
            EditorGUILayout.Space(4);

            var selectedActorId = ctx.HasSelection ? ctx.SelectedId.ActorId : 0;
            var items = _viewModel.RefreshIfNeeded(session, selectedActorId, ctx.HasSelection);
            DrawIssueGroups();
            var useSplitLayout = _selectedEvent.HasValue && EditorGUIUtility.currentViewWidth >= 900f;
            if (useSplitLayout)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.MinWidth(420f));
                DrawEventList(in ctx, items, expandHeight: true);
                EditorGUILayout.EndVertical();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(360f));
                DrawSelectionDetails(in ctx, items);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawEventList(in ctx, items, expandHeight: false);
                DrawSelectionDetails(in ctx, items);
            }
        }

        private void DrawFilterBar(in BattleDebugContext ctx, IBattleDiagnosticReadOnlySession session)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("类别", GUILayout.Width(30));
            var newScope = (BattleDebugDiagnosticEventScope)EditorGUILayout.EnumPopup(
                _viewModel.EventScope,
                EditorStyles.toolbarPopup,
                GUILayout.Width(105));
            if (newScope != _viewModel.EventScope)
            {
                _viewModel.EventScope = newScope;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("最近帧", GUILayout.Width(45));
            var newRecentFrameCount = EditorGUILayout.IntField(
                _viewModel.RecentFrameCount,
                EditorStyles.toolbarTextField,
                GUILayout.Width(55));
            newRecentFrameCount = Mathf.Max(0, newRecentFrameCount);
            if (newRecentFrameCount != _viewModel.RecentFrameCount)
            {
                _viewModel.RecentFrameCount = newRecentFrameCount;
                _viewModel.InvalidateCache();
            }

            var newFilterActor = GUILayout.Toggle(_viewModel.FilterBySelectedActor, "选中 Actor", EditorStyles.toolbarButton);
            if (newFilterActor != _viewModel.FilterBySelectedActor)
            {
                _viewModel.FilterBySelectedActor = newFilterActor;
                _viewModel.InvalidateCache();
            }

            if (_viewModel.FilterBySelectedActor)
            {
                var newRelation = (BattleDiagnosticActorRelation)EditorGUILayout.EnumPopup(
                    _viewModel.ActorRelation,
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

            var newFailures = GUILayout.Toggle(_viewModel.FailuresOnly, "仅失败", EditorStyles.toolbarButton);
            if (newFailures != _viewModel.FailuresOnly)
            {
                _viewModel.FailuresOnly = newFailures;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("Cfg", GUILayout.Width(24));
            var newConfigId = Mathf.Max(0, EditorGUILayout.IntField(
                _viewModel.ConfigId,
                EditorStyles.toolbarTextField,
                GUILayout.Width(48)));
            if (newConfigId != _viewModel.ConfigId)
            {
                _viewModel.ConfigId = newConfigId;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("阶段", GUILayout.Width(30));
            var newTriggerStage = (BattleDiagnosticTriggerAnalysisStage)EditorGUILayout.EnumPopup(
                _viewModel.TriggerStage,
                EditorStyles.toolbarPopup,
                GUILayout.Width(86));
            if (newTriggerStage != _viewModel.TriggerStage)
            {
                _viewModel.TriggerStage = newTriggerStage;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("结果", GUILayout.Width(30));
            var newTriggerResult = (BattleDiagnosticTriggerAnalysisResult)EditorGUILayout.EnumPopup(
                _viewModel.TriggerResult,
                EditorStyles.toolbarPopup,
                GUILayout.Width(86));
            if (newTriggerResult != _viewModel.TriggerResult)
            {
                _viewModel.TriggerResult = newTriggerResult;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("Ctx", GUILayout.Width(24));
            var newTriggerContextKind = Mathf.Max(0, EditorGUILayout.IntField(
                _viewModel.TriggerContextKind,
                EditorStyles.toolbarTextField,
                GUILayout.Width(40)));
            if (newTriggerContextKind != _viewModel.TriggerContextKind)
            {
                _viewModel.TriggerContextKind = newTriggerContextKind;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("Org", GUILayout.Width(24));
            var newTriggerOriginKind = Mathf.Max(0, EditorGUILayout.IntField(
                _viewModel.TriggerOriginKind,
                EditorStyles.toolbarTextField,
                GUILayout.Width(40)));
            if (newTriggerOriginKind != _viewModel.TriggerOriginKind)
            {
                _viewModel.TriggerOriginKind = newTriggerOriginKind;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("搜索", GUILayout.Width(30));
            var newSearch = GUILayout.TextField(_viewModel.SearchText ?? string.Empty, GUI.skin.textField, GUILayout.MinWidth(100));
            if (!string.Equals(newSearch, _viewModel.SearchText, System.StringComparison.Ordinal))
            {
                _viewModel.SearchText = newSearch;
                _viewModel.InvalidateCache();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("最近失败", "查看最近 600 帧内所有通道的失败事件"), EditorStyles.toolbarButton, GUILayout.Width(62)))
            {
                _viewModel.FocusRecentFailures();
                _selectedEvent = null;
                _actionStatus = "已切换到最近失败调查。";
            }
            if (GUILayout.Button(new GUIContent("条件失败", "查看条件判定未通过的触发分析事件"), EditorStyles.toolbarButton, GUILayout.Width(62)))
            {
                _viewModel.FocusConditionFailures();
                _selectedEvent = null;
                _actionStatus = "已切换到触发条件失败调查。";
            }
            if (GUILayout.Button(new GUIContent("预算阻断", "查看因递归、帧或根触发预算被阻断的事件"), EditorStyles.toolbarButton, GUILayout.Width(62)))
            {
                _viewModel.FocusTriggerBlocks();
                _selectedEvent = null;
                _actionStatus = "已切换到触发预算阻断调查。";
            }

            EditorGUI.BeginDisabledGroup(
                !_viewModel.FilterBySelectedActor &&
                !_viewModel.FailuresOnly &&
                !_viewModel.HasTriggerAnalysisFilter &&
                _viewModel.ConfigId == 0 &&
                _viewModel.EventScope == BattleDebugDiagnosticEventScope.All &&
                _viewModel.RecentFrameCount == 0 &&
                string.IsNullOrEmpty(_viewModel.SearchText));
            if (GUILayout.Button("全部历史", EditorStyles.toolbarButton, GUILayout.Width(65)))
            {
                _viewModel.ClearCorrelationFocus();
                _viewModel.FilterBySelectedActor = false;
                _viewModel.ActorRelation = BattleDiagnosticActorRelation.Either;
                _viewModel.FailuresOnly = false;
                _viewModel.EventScope = BattleDebugDiagnosticEventScope.All;
                _viewModel.RecentFrameCount = 0;
                _viewModel.TriggerStage = BattleDiagnosticTriggerAnalysisStage.Unknown;
                _viewModel.TriggerResult = BattleDiagnosticTriggerAnalysisResult.Unknown;
                _viewModel.TriggerContextKind = 0;
                _viewModel.TriggerOriginKind = 0;
                _viewModel.ConfigId = 0;
                _viewModel.SearchText = string.Empty;
                _viewModel.InvalidateCache();
                GUI.FocusControl(null);
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _viewModel.InvalidateCache();
                ctx.RequestRepaint?.Invoke();
            }

            EditorGUILayout.EndHorizontal();

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

            var actorScope = _viewModel.FilterBySelectedActor
                ? (ctx.HasSelection
                    ? $"Actor={ctx.SelectedId.ActorId}({_viewModel.ActorRelation})"
                    : "Actor=未选中")
                : "全部 Actor";
            var frameScope = _viewModel.RecentFrameCount > 0
                ? $"最近 {_viewModel.RecentFrameCount} 帧"
                : "全部保留历史";
            var triggerScope = FormatTriggerFilterSummary();
            EditorGUILayout.LabelField(
                $"{frameScope}  {_viewModel.EventScope}  {actorScope}  {triggerScope}  Cfg={(_viewModel.ConfigId == 0 ? "全部" : _viewModel.ConfigId.ToString())}  最新优先  EventRevision={_viewModel.StoreRevision}  显示={_viewModel.Items?.Count ?? 0}",
                EditorStyles.miniLabel);
        }

        private void DrawIssueGroups()
        {
            var groups = _viewModel.IssueGroups;
            if (groups == null || groups.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("问题聚合", EditorStyles.miniBoldLabel, GUILayout.Width(58));
            GUILayout.Label("按当前结果归并失败原因；点击可收敛到同类事件。", EditorStyles.miniLabel);
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
                GUILayout.Label($"最近 F{group.LatestFrame}", EditorStyles.miniLabel, GUILayout.Width(72));
                if (group.ConfigId != 0)
                {
                    GUILayout.Label($"cfg={group.ConfigId}", EditorStyles.miniLabel, GUILayout.Width(66));
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

            if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.None);
            }

            if (items == null || items.Count == 0)
            {
                EditorGUILayout.LabelField("（无事件）", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    DrawEventRow(in ctx, items[i]);
                }
            }

            EditorGUILayout.EndScrollView();
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
                _selectedEvent = evt;
                _actionStatus = string.Empty;
                ctx.RequestRepaint?.Invoke();
            }
            GUI.color = oldColor;

            GUILayout.Label($"F{evt.Frame}", GUILayout.Width(50));
            GUILayout.Label(evt.Kind.ToString(), GUILayout.Width(120));
            GUILayout.Label(evt.Outcome.ToString(), GUILayout.Width(70));

            if (evt.Payload.TryGetTriggerAnalysis(out var triggerPayload))
            {
                GUILayout.Label($"trg={triggerPayload.TriggerId}", EditorStyles.miniLabel, GUILayout.Width(70));
                GUILayout.Label($"{triggerPayload.Stage}/{triggerPayload.Result}", EditorStyles.miniLabel, GUILayout.Width(135));
            }

            if (evt.ConfigId != 0)
            {
                GUILayout.Label($"cfg={evt.ConfigId}", EditorStyles.miniLabel, GUILayout.Width(75));
            }

            if (evt.SourceActorId != 0)
            {
                GUILayout.Label($"src={evt.SourceActorId}", EditorStyles.miniLabel, GUILayout.Width(70));
            }

            if (evt.TargetActorId != 0)
            {
                GUILayout.Label($"tgt={evt.TargetActorId}", EditorStyles.miniLabel, GUILayout.Width(70));
            }

            if (evt.RootContextId != 0)
            {
                GUILayout.Label($"trace={evt.RootContextId}", EditorStyles.miniLabel, GUILayout.Width(90));
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
                SelectResult(items, selectedIndex + 1);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(selectedIndex <= 0);
            if (GUILayout.Button(new GUIContent("▼", "选择当前结果中的下一条事件"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
            {
                SelectResult(items, selectedIndex - 1);
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button(new GUIContent("复制", "复制该事件的完整诊断字段"), EditorStyles.toolbarButton, GUILayout.Width(42f)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildClipboardText(in evt);
                _actionStatus = "事件详情已复制到剪贴板。";
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("序列 / 帧", $"{evt.Sequence} / {evt.Frame}");
            EditorGUILayout.LabelField("类型 / 通道 / 结果", $"{evt.Kind} / {evt.Channel} / {evt.Outcome}");
            EditorGUILayout.LabelField("Root / Context", $"{evt.RootContextId} / {evt.ContextId}");
            EditorGUILayout.LabelField("Config / Attack", $"{evt.ConfigId} / {evt.AttackId}");
            EditorGUILayout.LabelField("Skill Runtime", evt.SkillRuntime.ToString());
            EditorGUILayout.LabelField("摘要", evt.Summary);

            if (evt.Payload.TryGetTriggerAnalysis(out var triggerPayload))
            {
                DrawTriggerPayloadDetails(in triggerPayload, evt.Payload.SchemaVersion);
            }
            else if (evt.Payload.TryGetSyncSnapshotReceived(out var syncPayload))
            {
                EditorGUILayout.LabelField(
                    "Payload",
                    $"SyncSnapshotReceived v{evt.Payload.SchemaVersion}: " +
                    $"frame={syncPayload.AuthoritativeFrame}, hash={syncPayload.StateHash}");
            }
            else
            {
                EditorGUILayout.LabelField(
                    "Payload",
                    evt.Payload.HasValue
                        ? $"{evt.Payload.Kind} v{evt.Payload.SchemaVersion}"
                        : "（无）");
            }

            if (!string.IsNullOrEmpty(_actionStatus))
            {
                EditorGUILayout.HelpBox(_actionStatus, MessageType.Info);
            }

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
                    ? $"Replay 已定位到第 {evt.Frame} 帧并暂停。"
                    : $"无法定位到第 {evt.Frame} 帧。该帧可能超出当前 Replay 范围。";
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

        private void SelectResult(IReadOnlyList<BattleDiagnosticEvent> items, int index)
        {
            if (items == null || index < 0 || index >= items.Count) return;
            _selectedEvent = items[index];
            _actionStatus = string.Empty;
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
            if (!_viewModel.HasTriggerAnalysisFilter) return "触发=全部";

            var builder = new StringBuilder("触发");
            if (_viewModel.TriggerStage != BattleDiagnosticTriggerAnalysisStage.Unknown)
            {
                builder.Append($" Stage={_viewModel.TriggerStage}");
            }

            if (_viewModel.TriggerResult != BattleDiagnosticTriggerAnalysisResult.Unknown)
            {
                builder.Append($" Result={_viewModel.TriggerResult}");
            }

            if (_viewModel.TriggerContextKind != 0)
            {
                builder.Append($" Ctx={_viewModel.TriggerContextKind}");
            }

            if (_viewModel.TriggerOriginKind != 0)
            {
                builder.Append($" Org={_viewModel.TriggerOriginKind}");
            }

            return builder.ToString();
        }

        private static void DrawTriggerPayloadDetails(
            in BattleDiagnosticTriggerAnalysisPayload payload,
            int schemaVersion)
        {
            EditorGUILayout.LabelField(
                "Payload",
                $"TriggerAnalysis v{schemaVersion}: trigger={payload.TriggerId}, " +
                $"stage={payload.Stage}, result={payload.Result}");
            EditorGUILayout.LabelField("Trigger Context", $"contextKind={payload.ContextKind}, originKind={payload.OriginKind}, detail={payload.DetailCode}");
            EditorGUILayout.LabelField(
                "Trigger Budget",
                $"depth={payload.CurrentDepth}, frame={payload.CurrentFrameCount}, " +
                $"root={payload.CurrentRootCount}, sameTrigger={payload.CurrentSameTriggerCount}");

            if (!string.IsNullOrEmpty(payload.FailureKey) || !string.IsNullOrEmpty(payload.Reason))
            {
                EditorGUILayout.LabelField("Failure Key", string.IsNullOrEmpty(payload.FailureKey) ? "（无）" : payload.FailureKey);
                EditorGUILayout.LabelField("Reason", string.IsNullOrEmpty(payload.Reason) ? "（无）" : payload.Reason);
            }
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

        private static bool HasCorrelation(in BattleDiagnosticEvent diagnosticEvent)
        {
            return diagnosticEvent.RootContextId != 0 ||
                   diagnosticEvent.ContextId != 0 ||
                   diagnosticEvent.SkillRuntime.RuntimeId != 0 ||
                   diagnosticEvent.AttackId != 0;
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
    }
}
