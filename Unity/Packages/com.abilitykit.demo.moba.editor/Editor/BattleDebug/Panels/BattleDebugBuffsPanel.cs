using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugBuffsPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        public string Name => "Buff";
        public int Order => 250;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Actor;
        public bool OwnsScrollView => true;

        private readonly BattleDebugDiagnosticBuffsViewModel _viewModel =
            new BattleDebugDiagnosticBuffsViewModel();
        private Vector2 _scroll;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (!ctx.HasSelection)
            {
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    default,
                    requiresSelection: true,
                    hasSelection: false,
                    subject: "实体 Buff"));
                return;
            }

            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "诊断会话不可用。请启动战斗或打开包含 Battle Diagnostics 的 Artifact。",
                    MessageType.Info);
                return;
            }

            if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorBuffs))
            {
                var unsupported = BattleDiagnosticQueryStatus.Unavailable(
                    0,
                    session.ActorBuffStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported);
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    in unsupported,
                    subject: "实体 Buff"));
                return;
            }

            DrawToolbar(in ctx);
            _viewModel.RefreshIfNeeded(session, ctx.SelectedId.ActorId);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var buffs = _viewModel.Buffs;
            if (buffs == null || buffs.Count == 0)
            {
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    _viewModel.QueryStatus,
                    subject: "实体 Buff"));
            }
            else
            {
                for (var i = 0; i < buffs.Count; i++)
                {
                    DrawBuff(in ctx, buffs[i]);
                }
            }

            EditorGUILayout.Space(8f);
            DrawTimeline(in ctx, session);
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

        private void DrawToolbar(in BattleDebugContext ctx)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Actor #{ctx.SelectedId.ActorId}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _viewModel.InvalidateCache();
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                $"BuffStoreRevision={_viewModel.StoreRevision}  EventStoreRevision={_viewModel.EventStoreRevision}",
                EditorStyles.miniLabel);
        }

        private void DrawTimeline(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session)
        {
            EditorGUILayout.LabelField("生命周期时间线", EditorStyles.boldLabel);
            if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.Events))
            {
                EditorGUILayout.HelpBox(
                    "当前诊断会话不包含事件轨道。",
                    MessageType.Info);
                return;
            }

            if (!string.IsNullOrEmpty(_viewModel.EventStatusMessage))
            {
                EditorGUILayout.HelpBox(
                    _viewModel.EventStatusMessage,
                    _viewModel.EventQueryStatus.CanDisplayResults
                        ? MessageType.Info
                        : MessageType.Warning);
            }

            var events = _viewModel.TimelineEvents;
            if (events == null || events.Count == 0) return;

            for (var i = 0; i < events.Count; i++)
            {
                DrawTimelineEvent(in ctx, events[i]);
            }
        }

        private static void DrawTimelineEvent(
            in BattleDebugContext ctx,
            in BattleDiagnosticEvent diagnosticEvent)
        {
            if (!diagnosticEvent.Payload.TryGetBuffLifecycle(out var payload)) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"F{diagnosticEvent.Frame}  {payload.Stage}",
                EditorStyles.miniBoldLabel,
                GUILayout.Width(150f));
            EditorGUILayout.LabelField(
                $"Buff {diagnosticEvent.ConfigId}",
                EditorStyles.miniLabel,
                GUILayout.Width(100f));
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(ctx.OpenEvent == null);
            if (GUILayout.Button(
                    new GUIContent($"Seq {diagnosticEvent.Sequence}", "在诊断事件面板中查看完整事件"),
                    EditorStyles.miniButton,
                    GUILayout.Width(90f)))
            {
                ctx.OpenEvent?.Invoke(diagnosticEvent);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            var stackText = payload.Stage == BattleDiagnosticBuffLifecycleStage.StackChanged
                ? $"{payload.PreviousStackCount} -> {payload.StackCount}"
                : payload.MaxStacks > 0
                    ? $"{payload.StackCount}/{payload.MaxStacks}"
                    : payload.StackCount.ToString();
            EditorGUILayout.LabelField(
                $"Stack={stackText}  Duration={FormatMilliseconds(payload.DurationMilliseconds)}  " +
                $"Remaining={FormatMilliseconds(payload.RemainingMilliseconds)}  " +
                $"Interval={FormatMilliseconds(payload.IntervalRemainingMilliseconds)}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"SourceActor={diagnosticEvent.SourceActorId}  RootContext={diagnosticEvent.RootContextId}  " +
                $"Context={diagnosticEvent.ContextId}  SkillRuntime={diagnosticEvent.SkillRuntime}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"ModifierBindings={payload.ModifierBindingCount}  ModifierSource={payload.ModifierSourceId}" +
                (payload.RemoveReason != 0 ? $"  RemoveReason={payload.RemoveReason}" : string.Empty),
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private static string FormatMilliseconds(int milliseconds)
        {
            return milliseconds <= 0 ? "0s" : $"{milliseconds / 1000f:0.###}s";
        }

        private static void DrawBuff(
            in BattleDebugContext ctx,
            in BattleDiagnosticActorBuff buff)
        {
            var displayName = string.IsNullOrEmpty(buff.Name)
                ? $"Buff {buff.BuffId}"
                : $"{buff.Name} ({buff.BuffId})";
            var stack = buff.MaxStacks > 0
                ? $"{buff.StackCount}/{buff.MaxStacks}"
                : buff.StackCount.ToString();

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(displayName, EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Stack={stack}", EditorStyles.miniLabel, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                $"Remaining={buff.RemainingSeconds:0.###}  Interval={buff.IntervalRemainingSeconds:0.###}  " +
                $"SourceActor={buff.SourceActorId}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"SourceContext={buff.SourceContextId}  RuntimeContext={buff.RuntimeContextId}:{buff.RuntimeContextVersion}  " +
                $"RootContext={buff.RootContextId}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"SkillRuntime={buff.SkillRuntime}  ModifierBindings={buff.ModifierBindingCount}  " +
                $"ModifierSource={buff.ModifierSourceId}",
                EditorStyles.miniLabel);
            EditorGUI.BeginDisabledGroup(buff.BuffId <= 0 || ctx.OpenConfig == null);
            if (GUILayout.Button("打开配置", GUILayout.Width(80)))
            {
                ctx.OpenConfig?.Invoke(new BattleDebugConfigReference(
                    BattleDebugConfigKind.Buff,
                    buff.BuffId));
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }
    }
}
