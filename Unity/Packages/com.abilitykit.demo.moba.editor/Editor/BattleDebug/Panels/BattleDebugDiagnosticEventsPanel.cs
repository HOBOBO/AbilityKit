using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    /// <summary>
    /// 诊断事件面板（IMGUI 绘制层）：通过 <see cref="BattleDebugDiagnosticEventsViewModel"/>
    /// 持有查询/状态逻辑，本类只负责将 ViewModel 暴露的 DTO 渲染为 IMGUI。
    /// 支持按选中实体 ActorId 过滤、仅看失败、文本搜索。
    /// 只消费已定义的诊断查询契约，不建立旁路数据源。
    /// </summary>
    internal sealed class BattleDebugDiagnosticEventsPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        public string Name => "诊断事件";
        public int Order => 400;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => true;

        private readonly BattleDebugDiagnosticEventsViewModel _viewModel = new BattleDebugDiagnosticEventsViewModel();
        private Vector2 _scroll;
        private long _selectedSequence;

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

            DrawFilterBar(ctx, session);
            EditorGUILayout.Space(4);

            var selectedActorId = ctx.HasSelection ? ctx.SelectedId.ActorId : 0;
            var items = _viewModel.RefreshIfNeeded(session, selectedActorId, ctx.HasSelection);
            var selectedEvent = FindSelectedEvent(items);
            DrawEventList(in ctx, items);
            DrawSelectionDetails(in ctx, selectedEvent);
        }

        private void DrawFilterBar(in BattleDebugContext ctx, IBattleDiagnosticReadOnlySession session)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var newFilterActor = GUILayout.Toggle(_viewModel.FilterBySelectedActor, "仅选中实体", EditorStyles.toolbarButton);
            if (newFilterActor != _viewModel.FilterBySelectedActor)
            {
                _viewModel.FilterBySelectedActor = newFilterActor;
                _viewModel.InvalidateCache();
            }

            if (_viewModel.FilterBySelectedActor && !ctx.HasSelection)
            {
                GUILayout.Label("（未选中）", EditorStyles.miniLabel, GUILayout.Width(60));
            }

            var newFailures = GUILayout.Toggle(_viewModel.FailuresOnly, "仅失败", EditorStyles.toolbarButton);
            if (newFailures != _viewModel.FailuresOnly)
            {
                _viewModel.FailuresOnly = newFailures;
                _viewModel.InvalidateCache();
            }

            GUILayout.Label("搜索", GUILayout.Width(35));
            var newSearch = GUILayout.TextField(_viewModel.SearchText ?? string.Empty, GUI.skin.textField, GUILayout.MinWidth(80));
            if (!string.Equals(newSearch, _viewModel.SearchText, System.StringComparison.Ordinal))
            {
                _viewModel.SearchText = newSearch;
                _viewModel.InvalidateCache();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _viewModel.InvalidateCache();
                ctx.RequestRepaint?.Invoke();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"StoreRevision={_viewModel.StoreRevision}  事件数={_viewModel.Items?.Count ?? 0}",
                EditorStyles.miniLabel);
        }

        private void DrawEventList(
            in BattleDebugContext ctx,
            IReadOnlyList<BattleDiagnosticEvent> items)
        {
            _scroll = EditorGUILayout.BeginScrollView(
                _scroll,
                GUILayout.MinHeight(160),
                GUILayout.MaxHeight(420));

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
            var selected = evt.Sequence == _selectedSequence;

            EditorGUILayout.BeginHorizontal(GUI.skin.box);

            GUI.color = outcomeColor;
            var style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
            if (GUILayout.Button($"#{evt.Sequence}", style, GUILayout.Width(70)))
            {
                _selectedSequence = evt.Sequence;
                ctx.RequestRepaint?.Invoke();
            }
            GUI.color = oldColor;

            GUILayout.Label($"F{evt.Frame}", GUILayout.Width(50));
            GUILayout.Label(evt.Kind.ToString(), GUILayout.Width(120));
            GUILayout.Label(evt.Outcome.ToString(), GUILayout.Width(70));

            if (evt.SourceActorId != 0)
            {
                GUILayout.Label($"src={evt.SourceActorId}", EditorStyles.miniLabel, GUILayout.Width(70));
            }

            if (evt.TargetActorId != 0)
            {
                GUILayout.Label($"tgt={evt.TargetActorId}", EditorStyles.miniLabel, GUILayout.Width(70));
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(evt.Summary, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private BattleDiagnosticEvent? FindSelectedEvent(IReadOnlyList<BattleDiagnosticEvent> items)
        {
            if (_selectedSequence == 0 || items == null) return null;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Sequence == _selectedSequence) return items[i];
            }

            _selectedSequence = 0;
            return null;
        }

        private static void DrawSelectionDetails(
            in BattleDebugContext ctx,
            BattleDiagnosticEvent? selectedEvent)
        {
            if (!selectedEvent.HasValue) return;

            var evt = selectedEvent.Value;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("事件详情", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("序列 / 帧", $"{evt.Sequence} / {evt.Frame}");
            EditorGUILayout.LabelField("类型 / 通道 / 结果", $"{evt.Kind} / {evt.Channel} / {evt.Outcome}");
            EditorGUILayout.LabelField("Root / Context", $"{evt.RootContextId} / {evt.ContextId}");
            EditorGUILayout.LabelField("Config / Attack", $"{evt.ConfigId} / {evt.AttackId}");
            EditorGUILayout.LabelField("Skill Runtime", evt.SkillRuntime.ToString());
            EditorGUILayout.LabelField("摘要", evt.Summary);

            if (evt.Payload.TryGetSyncSnapshotReceived(out var syncPayload))
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
