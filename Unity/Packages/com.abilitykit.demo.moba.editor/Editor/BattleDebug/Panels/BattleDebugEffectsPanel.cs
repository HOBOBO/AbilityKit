using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugEffectsPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        public string Name => "效果";
        public int Order => 200;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Actor;
        public bool OwnsScrollView => true;

        private readonly BattleDebugDiagnosticEffectsViewModel _viewModel =
            new BattleDebugDiagnosticEffectsViewModel();
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
                    subject: "实体效果"));
                return;
            }

            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "诊断会话不可用。请启动战斗或打开包含战斗诊断的 Artifact。",
                    MessageType.Info);
                return;
            }

            if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorEffects))
            {
                var unsupported = BattleDiagnosticQueryStatus.Unavailable(
                    0,
                    session.ActorEffectStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported);
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    in unsupported,
                    subject: "实体效果"));
                return;
            }

            DrawToolbar(in ctx);
            _viewModel.RefreshIfNeeded(session, ctx.SelectedId.ActorId);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var effects = _viewModel.Effects;
            if (effects == null || effects.Count == 0)
            {
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    _viewModel.QueryStatus,
                    subject: "实体效果"));
            }
            else
            {
                for (var i = 0; i < effects.Count; i++)
                {
                    DrawEffect(in ctx, effects[i]);
                }
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
                $"效果存储版本={_viewModel.StoreRevision}",
                EditorStyles.miniLabel);
        }

        private static void DrawEffect(in BattleDebugContext ctx, in BattleDiagnosticActorEffect effect)
        {
            var remaining = effect.HasRemainingTime
                ? effect.RemainingSeconds.ToString("0.###")
                : "不适用";
            var nextTick = effect.HasPeriodicTick
                ? effect.NextTickInSeconds.ToString("0.###")
                : "不适用";

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(
                $"#{effect.InstanceId} 层数={effect.StackCount} 持续策略={BattleDebugDisplayText.EffectDurationPolicy(effect.DurationPolicy)}",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"已持续={effect.ElapsedSeconds:0.###} 剩余={remaining} 下次周期={nextTick}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"时长={effect.DurationSeconds:0.###} 周期={effect.PeriodSeconds:0.###} " +
                $"组件数={effect.ComponentCount} 应用时执行周期={BattleDebugDisplayText.Bool(effect.ExecutePeriodicOnApply)}",
                EditorStyles.miniLabel);
            BattleDebugOfflineRelations.DrawActor(in ctx, effect.Scope, "宿主", effect.ActorId, effect.Frame);
            var application = effect.Application;
            if (!application.IsCaptured)
            {
                EditorGUILayout.LabelField("来源：此效果实例未采集应用证据。", EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"应用帧 F{application.Frame} · 来源上下文 #{application.SourceContextId}",
                    EditorStyles.wordWrappedMiniLabel);
                if (!BattleDebugOfflineRelations.Draw(in ctx, effect.Scope, "施加来源", application.SourceActor, application.Frame))
                    EditorGUILayout.LabelField(application.SourceActor.HasRuntimeId ? $"施加来源：{application.SourceActor}" :
                        "施加来源：未记录实体引用。", EditorStyles.wordWrappedMiniLabel);
                if (application.SourceRootContextId > 0 && ctx.OpenTrace != null &&
                    GUILayout.Button("定位来源上下文", GUILayout.Width(110)))
                    ctx.OpenTrace.Invoke(application.SourceRootContextId, application.SourceContextId);
            }
            EditorGUILayout.EndVertical();
        }
    }
}
