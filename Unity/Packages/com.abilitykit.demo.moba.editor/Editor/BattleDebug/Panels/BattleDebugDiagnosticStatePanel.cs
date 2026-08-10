using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    /// <summary>
    /// 诊断状态面板（IMGUI 绘制层）：通过 <see cref="BattleDebugDiagnosticStateViewModel"/>
    /// 持有查询/状态逻辑，本类只负责将 ViewModel 暴露的 DTO 渲染为 IMGUI。
    /// 只消费已定义的诊断查询契约，不建立旁路数据源。
    /// </summary>
    internal sealed class BattleDebugDiagnosticStatePanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        public string Name => "诊断状态";
        public int Order => 410;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => true;

        private readonly BattleDebugDiagnosticStateViewModel _viewModel = new BattleDebugDiagnosticStateViewModel();
        private Vector2 _worldScroll;
        private Vector2 _actorScroll;

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

            DrawFrameBar(ctx, session);
            EditorGUILayout.Space(4);

            _viewModel.RefreshIfNeeded(session);

            DrawWorldSummary();
            EditorGUILayout.Space(6);
            DrawActorList(in ctx);
        }

        private void DrawFrameBar(in BattleDebugContext ctx, IBattleDiagnosticReadOnlySession session)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("帧", GUILayout.Width(20));
            var newFrame = EditorGUILayout.IntField(_viewModel.FrameInput, GUI.skin.textField, GUILayout.Width(60));
            if (newFrame != _viewModel.FrameInput)
            {
                _viewModel.FrameInput = newFrame;
                _viewModel.InvalidateCache();
            }

            if (GUILayout.Button("最新", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _viewModel.FrameInput = 0;
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
                $"StoreRevision={_viewModel.StoreRevision}",
                EditorStyles.miniLabel);
        }

        private void DrawWorldSummary()
        {
            EditorGUILayout.LabelField("世界快照", EditorStyles.boldLabel);

            if (!_viewModel.WorldSummary.HasValue)
            {
                var emptyState = BattleDebugEmptyStateProjector.Project(
                    _viewModel.WorldQueryStatus,
                    subject: "世界状态");
                DrawEmptyState(in emptyState);
                return;
            }

            _worldScroll = EditorGUILayout.BeginScrollView(_worldScroll, GUILayout.MaxHeight(120));

            var w = _viewModel.WorldSummary.Value;
            EditorGUILayout.LabelField("帧", w.Frame.ToString());
            EditorGUILayout.LabelField("Actor 数", w.ActorCount.ToString());
            EditorGUILayout.LabelField("活跃技能运行时", w.ActiveSkillRuntimeCount.ToString());
            EditorGUILayout.LabelField("活跃 Trace 根", w.ActiveTraceRootCount.ToString());
            if (!string.IsNullOrEmpty(w.StateHash))
            {
                EditorGUILayout.LabelField("状态哈希", w.StateHash);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawActorList(in BattleDebugContext ctx)
        {
            EditorGUILayout.LabelField("Actor 摘要", EditorStyles.boldLabel);
            _actorScroll = EditorGUILayout.BeginScrollView(_actorScroll);

            var actors = _viewModel.Actors;
            if (actors == null || actors.Count == 0)
            {
                var emptyState = BattleDebugEmptyStateProjector.Project(
                    _viewModel.ActorQueryStatus,
                    subject: "Actor 状态");
                DrawEmptyState(in emptyState);
            }
            else
            {
                for (int i = 0; i < actors.Count; i++)
                {
                    DrawActorRow(in ctx, actors[i]);
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

        private void DrawActorRow(
            in BattleDebugContext ctx,
            in BattleDiagnosticActorSummary actor)
        {
            var aliveColor = actor.IsAlive ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            var oldColor = GUI.color;

            EditorGUILayout.BeginHorizontal(GUI.skin.box);

            GUI.color = aliveColor;
            EditorGUI.BeginDisabledGroup(ctx.SelectActor == null);
            if (GUILayout.Button($"#{actor.ActorId}", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                ctx.SelectActor?.Invoke(actor.ActorId);
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.Label(actor.Kind.ToString(), GUILayout.Width(70));
            GUI.color = oldColor;

            GUILayout.Label(actor.DisplayName, GUILayout.Width(80));
            GUILayout.Label($"HP {actor.Health:0}/{actor.MaximumHealth:0}", EditorStyles.miniLabel, GUILayout.Width(100));
            GUILayout.Label($"team={actor.TeamId}", EditorStyles.miniLabel, GUILayout.Width(60));

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }
    }
}
