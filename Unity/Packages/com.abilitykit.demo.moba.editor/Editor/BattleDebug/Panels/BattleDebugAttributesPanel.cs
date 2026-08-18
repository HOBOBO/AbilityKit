using System;
using System.Collections.Generic;
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
        private readonly BattleDebugDiagnosticBuffsViewModel _buffsViewModel =
            new BattleDebugDiagnosticBuffsViewModel();
        private readonly Dictionary<int, bool> _expandedAttributes = new Dictionary<int, bool>();
        private Vector2 _scroll;
        private string _search = string.Empty;
        private bool _modifiedOnly;
        private bool _expandAll = true;

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

            _viewModel.RefreshIfNeeded(session, ctx.SelectedId.ActorId);
            if (session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorBuffs))
            {
                _buffsViewModel.RefreshIfNeeded(session, ctx.SelectedId.ActorId);
            }
            DrawToolbar(in ctx);

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
                var visibleCount = 0;
                for (var i = 0; i < attributes.Count; i++)
                {
                    var attribute = attributes[i];
                    if (!Matches(attribute)) continue;
                    DrawAttribute(in ctx, in attribute);
                    visibleCount++;
                }

                if (visibleCount == 0)
                {
                    EditorGUILayout.HelpBox("当前筛选条件下没有属性。", MessageType.Info);
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
            _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(100));
            _modifiedOnly = GUILayout.Toggle(_modifiedOnly, "仅修改项", EditorStyles.toolbarButton);
            if (GUILayout.Button(_expandAll ? "全部收起" : "全部展开", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                _expandAll = !_expandAll;
                _expandedAttributes.Clear();
            }
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(48)))
            {
                _viewModel.InvalidateCache();
                _buffsViewModel.InvalidateCache();
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                $"属性 {_viewModel.Attributes.Count} · 修改器 {_viewModel.Modifiers.Count} · Revision {_viewModel.StoreRevision}",
                EditorStyles.miniLabel);
        }

        private bool Matches(in BattleDiagnosticActorAttribute attribute)
        {
            if (_modifiedOnly && attribute.ModifierCount == 0) return false;
            if (string.IsNullOrWhiteSpace(_search)) return true;
            return attribute.AttributeId.ToString().IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (!string.IsNullOrEmpty(attribute.Name) &&
                    attribute.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void DrawAttribute(
            in BattleDebugContext ctx,
            in BattleDiagnosticActorAttribute attribute)
        {
            var displayName = string.IsNullOrEmpty(attribute.Name)
                ? $"Attribute {attribute.AttributeId}"
                : $"{attribute.Name} ({attribute.AttributeId})";
            var expanded = _expandedAttributes.TryGetValue(attribute.AttributeId, out var saved)
                ? saved
                : _expandAll;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            var nextExpanded = EditorGUILayout.Foldout(
                expanded,
                $"{displayName}  [{attribute.ModifierCount}]",
                true,
                EditorStyles.foldoutHeader);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                $"{attribute.BaseValue:0.#####}  →  {attribute.FinalValue:0.#####}",
                EditorStyles.miniLabel,
                GUILayout.Width(170));
            EditorGUILayout.EndHorizontal();
            if (nextExpanded != expanded)
            {
                _expandedAttributes[attribute.AttributeId] = nextExpanded;
            }

            if (nextExpanded)
            {
                DrawAttributeSummary(in attribute);
                DrawModifiers(in ctx, attribute.AttributeId);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawAttributeSummary(in BattleDiagnosticActorAttribute attribute)
        {
            var delta = attribute.FinalValue - attribute.BaseValue;
            EditorGUILayout.LabelField(
                $"基础值 {attribute.BaseValue:0.#####}    最终值 {attribute.FinalValue:0.#####}    差值 {delta:+0.#####;-0.#####;0}",
                EditorStyles.miniLabel);
        }

        private void DrawModifiers(in BattleDebugContext ctx, int attributeId)
        {
            var modifiers = _viewModel.Modifiers;
            var drawn = 0;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier.AttributeId != attributeId) continue;
                DrawModifier(in ctx, in modifier);
                drawn++;
            }

            if (drawn == 0)
            {
                EditorGUILayout.LabelField("无活动修改器", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawModifier(
            in BattleDebugContext ctx,
            in BattleDiagnosticActorAttributeModifier modifier)
        {
            var sourceBuff = FindSourceBuff(modifier.SourceId);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"{ResolveOperationSymbol(modifier.Operation)} {modifier.Magnitude:0.#####}",
                EditorStyles.miniBoldLabel,
                GUILayout.Width(100));
            EditorGUILayout.LabelField(
                $"Op={modifier.Operation} · Priority {modifier.Priority} · MagnitudeType {modifier.MagnitudeType}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            DrawModifierExplanation(in modifier);

            if (sourceBuff.HasValue)
            {
                var buff = sourceBuff.Value;
                var name = string.IsNullOrEmpty(buff.Name) ? $"Buff {buff.BuffId}" : buff.Name;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"来源: {name} ({buff.BuffId}) · Actor #{buff.SourceActorId} · Stack {buff.StackCount}",
                    EditorStyles.miniLabel);
                EditorGUI.BeginDisabledGroup(ctx.OpenConfig == null);
                if (GUILayout.Button("配置", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    ctx.OpenConfig?.Invoke(new BattleDebugConfigReference(BattleDebugConfigKind.Buff, buff.BuffId));
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(
                    $"SourceId {modifier.SourceId} · SourceContext {buff.SourceContextId} · RootContext {buff.RootContextId}",
                    EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField(
                    modifier.SourceId == 0
                        ? "来源: 未提供 SourceId"
                        : $"来源: 未解析的运行时来源 · SourceId {modifier.SourceId}",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private static void DrawModifierExplanation(
            in BattleDiagnosticActorAttributeModifier modifier)
        {
            if (!modifier.HasExplanation)
            {
                EditorGUILayout.LabelField(
                    "Explain: 未采集（运行时服务不可用或旧 Artifact）",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EditorGUILayout.LabelField(
                $"声明值 {modifier.DeclaredValue:0.#####} · 叠层值 {modifier.StackedValue:0.#####} · Stack {modifier.StackCount}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"投影值 {modifier.ProjectedValue:0.#####} · 当前计算值 {FormatOptionalValue(modifier.CurrentValue, modifier.HasCurrentValue)} · 捕获值 {FormatOptionalValue(modifier.CapturedValue, modifier.HasCapturedValue)}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"评估策略 {ResolveEvaluationPolicy(modifier.EvaluationPolicy)} · 捕获模式 {ResolveCaptureMode(modifier.CaptureMode)}",
                EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(modifier.Explanation))
            {
                EditorGUILayout.LabelField(
                    "解释: " + modifier.Explanation,
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static string FormatOptionalValue(float value, bool hasValue)
        {
            return hasValue ? value.ToString("0.#####") : "N/A";
        }

        private static string ResolveEvaluationPolicy(int evaluationPolicy)
        {
            switch (evaluationPolicy)
            {
                case 0:
                    return "Realtime";
                case 1:
                    return "OnApplySnapshot";
                default:
                    return $"Unknown({evaluationPolicy})";
            }
        }

        private static string ResolveCaptureMode(string captureMode)
        {
            return string.IsNullOrEmpty(captureMode) ? "N/A" : captureMode;
        }

        private static string ResolveOperationSymbol(int operation)
        {
            switch (operation)
            {
                case 0: return "+";
                case 1: return "×";
                case 2: return "=";
                case 3: return "+%";
                default: return "?";
            }
        }

        private BattleDiagnosticActorBuff? FindSourceBuff(int sourceId)
        {
            if (sourceId == 0) return null;
            var buffs = _buffsViewModel.Buffs;
            for (var i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].ModifierSourceId == sourceId) return buffs[i];
            }
            return null;
        }
    }
}
