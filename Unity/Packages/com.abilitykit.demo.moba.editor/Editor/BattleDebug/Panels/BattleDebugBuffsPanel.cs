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
                EditorGUILayout.HelpBox("请先选择一个实体。", MessageType.Info);
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
                EditorGUILayout.HelpBox("当前诊断会话不支持实体 Buff 查询。", MessageType.Info);
                return;
            }

            DrawToolbar(in ctx);
            _viewModel.RefreshIfNeeded(session, ctx.SelectedId.ActorId);

            if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.None);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var buffs = _viewModel.Buffs;
            if (buffs == null || buffs.Count == 0)
            {
                EditorGUILayout.LabelField("（空）", EditorStyles.miniLabel);
            }
            else
            {
                for (var i = 0; i < buffs.Count; i++)
                {
                    DrawBuff(in ctx, buffs[i]);
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
                $"BuffStoreRevision={_viewModel.StoreRevision}",
                EditorStyles.miniLabel);
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
                $"SkillRuntime={buff.SkillRuntime}  ModifierBindings={buff.ModifierBindingCount}",
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
