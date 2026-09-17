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
        private int _chartAttributeId;
        private bool _recordChart = true;

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
                    "诊断会话不可用。请启动战斗或打开包含战斗诊断的 Artifact。",
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
            _viewModel.TrackAttribute(session, ctx.SelectedId.ActorId, _chartAttributeId, _recordChart);
            if (session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorBuffs))
            {
                _buffsViewModel.RefreshIfNeeded(session, ctx.SelectedId.ActorId);
            }
            DrawToolbar(in ctx);
            if (_chartAttributeId != 0) DrawChart(in ctx);

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
                $"属性 {_viewModel.Attributes.Count} · 修改器 {_viewModel.Modifiers.Count} · 版本 {_viewModel.StoreRevision}",
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
                ? $"属性 {attribute.AttributeId}"
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
                GUILayout.Width(125));
            if (GUILayout.Button(
                    new GUIContent("曲线", "查看此属性的基础值和最终值采样曲线"),
                    EditorStyles.miniButton,
                    GUILayout.Width(38)))
            {
                _chartAttributeId = _chartAttributeId == attribute.AttributeId ? 0 : attribute.AttributeId;
                _viewModel.ClearHistory();
                ctx.RequestRepaint?.Invoke();
            }
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

        private void DrawChart(in BattleDebugContext ctx)
        {
            var name = $"属性 {_chartAttributeId}";
            var attributes = _viewModel.Attributes;
            for (var i = 0; i < attributes.Count; i++)
            {
                if (attributes[i].AttributeId != _chartAttributeId) continue;
                if (!string.IsNullOrEmpty(attributes[i].Name)) name = attributes[i].Name;
                break;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(name, EditorStyles.miniBoldLabel, GUILayout.MinWidth(55));
            GUILayout.FlexibleSpace();
            _recordChart = GUILayout.Toggle(
                _recordChart,
                new GUIContent(_recordChart ? "记录中" : "已暂停", "暂停或继续记录属性曲线；不影响诊断采样"),
                EditorStyles.toolbarButton,
                GUILayout.Width(52));
            if (GUILayout.Button(new GUIContent("清空", "清空当前属性的曲线采样"),
                    EditorStyles.toolbarButton, GUILayout.Width(36)))
                _viewModel.ClearHistory(waitForNextRevision: true);
            EditorGUILayout.EndHorizontal();

            var samples = _viewModel.History;
            if (samples.Count == 0)
            {
                EditorGUILayout.LabelField("暂无曲线采样", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            for (var i = 0; i < samples.Count; i++)
            {
                min = Mathf.Min(min, samples[i].BaseValue, samples[i].FinalValue);
                max = Mathf.Max(max, samples[i].BaseValue, samples[i].FinalValue);
            }
            var padding = Mathf.Max((max - min) * 0.08f, 0.001f);
            min -= padding;
            max += padding;

            var rect = GUILayoutUtility.GetRect(180f, 164f, GUILayout.ExpandWidth(true));
            var plot = new Rect(rect.x + 58f, rect.y + 10f, Mathf.Max(1f, rect.width - 70f), 125f);
            var baseColor = new Color(0.18f, 0.72f, 0.67f);
            var finalColor = new Color(0.96f, 0.65f, 0.23f);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                    ? new Color(0.14f, 0.16f, 0.18f) : new Color(0.91f, 0.93f, 0.94f));
                for (var i = 0; i <= 2; i++)
                {
                    var y = plot.y + plot.height * i / 2f;
                    EditorGUI.DrawRect(new Rect(plot.x, y, plot.width, 1f),
                        EditorGUIUtility.isProSkin ? new Color(0.32f, 0.35f, 0.37f) : new Color(0.75f, 0.78f, 0.8f));
                }

                var firstFrame = samples[0].Frame;
                var frameSpan = Mathf.Max(1, samples[samples.Count - 1].Frame - firstFrame);
                Handles.BeginGUI();
                DrawSeries(samples, plot, min, max, firstFrame, frameSpan, baseColor, false);
                DrawSeries(samples, plot, min, max, firstFrame, frameSpan, finalColor, true);
                Handles.EndGUI();
            }
            GUI.Label(new Rect(rect.x + 3f, plot.y - 4f, 55f, 17f), max.ToString("0.###"), EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 3f, plot.yMax - 12f, 55f, 17f), min.ToString("0.###"), EditorStyles.miniLabel);
            GUI.Label(new Rect(plot.x, plot.yMax + 3f, 80f, 16f), $"帧 {samples[0].Frame}", EditorStyles.miniLabel);
            GUI.Label(new Rect(plot.xMax - 85f, plot.yMax + 3f, 85f, 16f),
                $"帧 {samples[samples.Count - 1].Frame}", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            DrawLegend(baseColor, "基础值");
            DrawLegend(finalColor, "最终值");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{samples.Count} 点 · 采样帧", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            var mouse = Event.current.mousePosition;
            var selectedSample = samples[samples.Count - 1];
            if (plot.Contains(mouse))
            {
                var nearest = 0;
                var distance = float.MaxValue;
                var firstFrame = samples[0].Frame;
                var span = Mathf.Max(1, samples[samples.Count - 1].Frame - firstFrame);
                for (var i = 0; i < samples.Count; i++)
                {
                    var x = plot.x + plot.width * (samples[i].Frame - firstFrame) / span;
                    var dx = Mathf.Abs(x - mouse.x);
                    if (dx >= distance) continue;
                    distance = dx;
                    nearest = i;
                }
                selectedSample = samples[nearest];
                if (Event.current.type == EventType.MouseMove) ctx.RequestRepaint?.Invoke();
            }
            EditorGUILayout.LabelField(
                $"帧 {selectedSample.Frame} · 基础 {selectedSample.BaseValue:0.#####} · 最终 {selectedSample.FinalValue:0.#####} · 修改器 {selectedSample.ModifierCount}",
                EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawSeries(
            IReadOnlyList<BattleDebugDiagnosticAttributesViewModel.AttributeSample> samples,
            Rect plot, float min, float max, int firstFrame, int frameSpan, Color color, bool final)
        {
            var points = new Vector3[samples.Count];
            for (var i = 0; i < samples.Count; i++)
            {
                var value = final ? samples[i].FinalValue : samples[i].BaseValue;
                points[i] = new Vector3(
                    plot.x + plot.width * (samples[i].Frame - firstFrame) / frameSpan,
                    plot.yMax - plot.height * (value - min) / (max - min));
            }
            var previousColor = Handles.color;
            Handles.color = color;
            if (points.Length > 1) Handles.DrawAAPolyLine(2f, points);
            else Handles.DrawSolidDisc(points[0], Vector3.forward, 3f);
            Handles.color = previousColor;
        }

        private static void DrawLegend(Color color, string label)
        {
            var rect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.Width(10f));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, color);
            GUILayout.Label(label, EditorStyles.miniLabel);
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
                $"操作={modifier.Operation} · 优先级 {modifier.Priority} · 数值类型 {modifier.MagnitudeType}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            DrawModifierExplanation(in modifier);

            if (sourceBuff.HasValue)
            {
                var buff = sourceBuff.Value;
                var name = string.IsNullOrEmpty(buff.Name) ? $"Buff {buff.BuffId}" : buff.Name;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"来源：{name} ({buff.BuffId}) · Actor #{buff.SourceActorId} · 层数 {buff.StackCount}",
                    EditorStyles.miniLabel);
                EditorGUI.BeginDisabledGroup(ctx.OpenConfig == null);
                if (GUILayout.Button("配置", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    ctx.OpenConfig?.Invoke(new BattleDebugConfigReference(BattleDebugConfigKind.Buff, buff.BuffId));
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(
                    $"来源 ID {modifier.SourceId} · 来源上下文 {buff.SourceContextId} · 根上下文 {buff.RootContextId}",
                    EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField(
                    modifier.SourceId == 0
                        ? "来源：未提供来源 ID"
                        : $"来源：未解析的运行时来源 · 来源 ID {modifier.SourceId}",
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
                    "计算说明：未采集（运行时服务不可用或旧 Artifact）",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EditorGUILayout.LabelField(
                $"声明值 {modifier.DeclaredValue:0.#####} · 叠层值 {modifier.StackedValue:0.#####} · 层数 {modifier.StackCount}",
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
            return hasValue ? value.ToString("0.#####") : "不适用";
        }

        private static string ResolveEvaluationPolicy(int evaluationPolicy)
        {
            switch (evaluationPolicy)
            {
                case 0:
                    return "实时计算";
                case 1:
                    return "应用时快照";
                default:
                    return $"未知（{evaluationPolicy}）";
            }
        }

        private static string ResolveCaptureMode(string captureMode)
        {
            return string.IsNullOrEmpty(captureMode) ? "不适用" : captureMode;
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
