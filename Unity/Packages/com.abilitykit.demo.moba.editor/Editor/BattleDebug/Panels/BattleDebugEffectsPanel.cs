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
                EditorGUILayout.HelpBox("请先选择一个实体。", MessageType.Info);
                return;
            }

            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "诊断会话不可用。请确认战斗已启动且诊断 Local Session 已注册。",
                    MessageType.Info);
                return;
            }

            if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorEffects))
            {
                EditorGUILayout.HelpBox("当前诊断会话不支持实体 Effect 查询。", MessageType.Info);
                return;
            }

            DrawToolbar(in ctx);
            _viewModel.RefreshIfNeeded(session, ctx.SelectedId.ActorId);

            if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.None);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var effects = _viewModel.Effects;
            if (effects == null || effects.Count == 0)
            {
                EditorGUILayout.LabelField("（空）", EditorStyles.miniLabel);
            }
            else
            {
                for (var i = 0; i < effects.Count; i++)
                {
                    DrawEffect(effects[i]);
                }
            }
            EditorGUILayout.EndScrollView();
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
                $"EffectStoreRevision={_viewModel.StoreRevision}",
                EditorStyles.miniLabel);
        }

        private static void DrawEffect(in BattleDiagnosticActorEffect effect)
        {
            var remaining = effect.HasRemainingTime
                ? effect.RemainingSeconds.ToString("0.###")
                : "N/A";
            var nextTick = effect.HasPeriodicTick
                ? effect.NextTickInSeconds.ToString("0.###")
                : "N/A";

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(
                $"#{effect.InstanceId} stack={effect.StackCount} duration={effect.DurationPolicy}",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"elapsed={effect.ElapsedSeconds:0.###} remaining={remaining} nextTick={nextTick}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"duration={effect.DurationSeconds:0.###} period={effect.PeriodSeconds:0.###} " +
                $"components={effect.ComponentCount} periodicOnApply={effect.ExecutePeriodicOnApply}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }
    }
}
