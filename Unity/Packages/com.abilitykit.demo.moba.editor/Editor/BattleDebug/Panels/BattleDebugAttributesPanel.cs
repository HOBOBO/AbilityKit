using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugAttributesPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        public string Name => "属性";
        public int Order => 200;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Actor;
        public bool OwnsScrollView => true;

        private readonly BattleDebugDiagnosticAttributesViewModel _viewModel =
            new BattleDebugDiagnosticAttributesViewModel();
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
                    subject: "实体属性"));
                return;
            }

            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                EditorGUILayout.HelpBox(
                    "诊断会话不可用。请启动战斗或打开包含 Battle Diagnostics 的 Artifact。",
                    MessageType.Info);
                return;
            }

            if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorAttributes))
            {
                var unsupported = BattleDiagnosticQueryStatus.Unavailable(
                    0,
                    session.ActorAttributeStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported);
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    in unsupported,
                    subject: "实体属性"));
                return;
            }

            DrawToolbar(in ctx);
            _viewModel.RefreshIfNeeded(session, ctx.SelectedId.ActorId);

            var attributes = _viewModel.Attributes;
            if (attributes != null &&
                attributes.Count > 0 &&
                !string.IsNullOrEmpty(_viewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(_viewModel.StatusMessage, MessageType.Warning);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (attributes == null || attributes.Count == 0)
            {
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    _viewModel.AttributeQueryStatus,
                    subject: "实体属性"));
            }
            else
            {
                for (var i = 0; i < attributes.Count; i++)
                {
                    DrawAttribute(attributes[i]);
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
                $"AttributeStoreRevision={_viewModel.StoreRevision}",
                EditorStyles.miniLabel);
        }

        private void DrawAttribute(in BattleDiagnosticActorAttribute attribute)
        {
            var displayName = string.IsNullOrEmpty(attribute.Name)
                ? $"Attribute {attribute.AttributeId}"
                : $"{attribute.Name} ({attribute.AttributeId})";

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(displayName, EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                $"{attribute.BaseValue:0.#####} -> {attribute.FinalValue:0.#####}",
                EditorStyles.miniLabel,
                GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            if (attribute.ModifierCount > 0)
            {
                var modifiers = _viewModel.Modifiers;
                for (var i = 0; i < modifiers.Count; i++)
                {
                    var modifier = modifiers[i];
                    if (modifier.AttributeId != attribute.AttributeId) continue;
                    EditorGUILayout.LabelField(
                        $"Op={modifier.Operation}  Value={modifier.Magnitude:0.#####}  " +
                        $"Priority={modifier.Priority}  Source={modifier.SourceId}  " +
                        $"MagnitudeType={modifier.MagnitudeType}",
                        EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}
