using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugTagsPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        public string Name => "标签";
        public int Order => 100;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Actor;
        public bool OwnsScrollView => true;

        private readonly BattleDebugDiagnosticTagsViewModel _viewModel =
            new BattleDebugDiagnosticTagsViewModel();
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

            if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorTags))
            {
                EditorGUILayout.HelpBox("当前诊断会话不支持实体标签查询。", MessageType.Info);
                return;
            }

            DrawToolbar(in ctx);
            _viewModel.RefreshIfNeeded(session, ctx.SelectedId.ActorId);

            if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.None);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var tags = _viewModel.Tags;
            if (tags == null || tags.Count == 0)
            {
                EditorGUILayout.LabelField("（空）", EditorStyles.miniLabel);
            }
            else
            {
                for (var i = 0; i < tags.Count; i++)
                {
                    DrawTag(tags[i]);
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
                $"TagStoreRevision={_viewModel.StoreRevision}",
                EditorStyles.miniLabel);
        }

        private static void DrawTag(in BattleDiagnosticActorTag tag)
        {
            var displayName = string.IsNullOrEmpty(tag.Name)
                ? $"Tag {tag.TagId}"
                : $"{tag.Name} ({tag.TagId})";
            EditorGUILayout.LabelField(displayName, EditorStyles.miniLabel);
        }
    }
}
