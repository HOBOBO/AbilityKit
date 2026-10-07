using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugAttributesPanel : IBattleDebugPanel,
        IBattleDebugPanelLayout, IBattleDebugPanelSessionCleanup
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
        private readonly HashSet<string> _expandedSources = new HashSet<string>();
        private readonly HashSet<int> _knownModifierSourceIds = new HashSet<int>();
        private static readonly string[] ModifierKindOptions =
            { "全部", "属性", "状态机参数", "状态标记", "领域参数", "技能槽位", "引用候选", "其他" };
        private IReadOnlyList<MobaContinuousRuntimeView> _modifierRuntimes =
            Array.Empty<MobaContinuousRuntimeView>();
        private Vector2 _scroll;
        private string _search = string.Empty;
        private int _viewMode;
        private int _modifierKindFilter;
        private bool _modifiedOnly;
        private bool _expandAll = true;
        private int _chartAttributeId;
        private bool _recordChart = true;
        private int _samplingIntervalFrames = 1;
        private bool _compareExpected;
        private float _expectedValue;
        private float _expectedTolerance = 0.01f;
        private long _modifierActorId;
        private object _modifierWorld;
        private IMobaBattleDiagnosticAttributeHistory _chartHistoryService;
        private double _nextModifierRefreshAt;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void ClearSessionState()
        {
            ReleaseRuntimeChart();
            _viewModel.ClearHistory();
            _viewModel.InvalidateCache();
            _buffsViewModel.InvalidateCache();
            _modifierRuntimes = Array.Empty<MobaContinuousRuntimeView>();
            _modifierWorld = null;
            _modifierActorId = 0;
            _nextModifierRefreshAt = 0d;
            _expandedSources.Clear();
        }

        public void Draw(in BattleDebugContext ctx)
        {
            if (!ctx.HasSelection)
            {
                ReleaseRuntimeChart();
                DrawEmptyState(BattleDebugEmptyStateProjector.Project(
                    default,
                    requiresSelection: true,
                    hasSelection: false,
                    subject: "实体属性"));
                return;
            }

            DrawToolbar(in ctx);

            if (_viewMode == 1)
            {
                TrySyncRuntimeChart(in ctx);
                if (BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var modifierSession) &&
                    modifierSession.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorAttributes))
                {
                    _viewModel.RefreshIfNeeded(modifierSession, ctx.SelectedId.ActorId);
                    if (modifierSession.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorBuffs))
                        _buffsViewModel.RefreshIfNeeded(modifierSession, ctx.SelectedId.ActorId);
                    else
                        _buffsViewModel.InvalidateCache();
                }
                else
                {
                    _viewModel.InvalidateCache();
                    _buffsViewModel.InvalidateCache();
                }
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                DrawActorModifiers(in ctx);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session))
            {
                ReleaseRuntimeChart();
                EditorGUILayout.HelpBox(
                    "诊断会话不可用。请启动战斗或打开包含战斗诊断的 Artifact。",
                    MessageType.Info);
                return;
            }

            if (!session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorAttributes))
            {
                ReleaseRuntimeChart();
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
            else
            {
                _buffsViewModel.InvalidateCache();
            }
            if (!TrySyncRuntimeChart(in ctx))
                _viewModel.TrackAttribute(session, ctx.SelectedId.ActorId, _chartAttributeId,
                    _recordChart, _samplingIntervalFrames);
            EditorGUILayout.LabelField(
                $"属性 {_viewModel.Attributes.Count} · 属性投影 {_viewModel.Modifiers.Count} · 版本 {_viewModel.StoreRevision}",
                EditorStyles.miniLabel);
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
            GUILayout.FlexibleSpace();
            _viewMode = GUILayout.Toolbar(_viewMode, new[] { "属性", "修改器" },
                EditorStyles.toolbarButton, GUILayout.Width(112));
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(48)))
            {
                _viewModel.InvalidateCache();
                _buffsViewModel.InvalidateCache();
                _nextModifierRefreshAt = 0d;
                ctx.RequestRepaint?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
            if (_viewMode == 0)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField,
                    GUILayout.MinWidth(80));
                _modifiedOnly = GUILayout.Toggle(_modifiedOnly, "仅修改项", EditorStyles.toolbarButton);
                if (GUILayout.Button(_expandAll ? "全部收起" : "全部展开",
                        EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    _expandAll = !_expandAll;
                    _expandedAttributes.Clear();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawActorModifiers(in BattleDebugContext ctx)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("类型", EditorStyles.miniLabel, GUILayout.Width(28));
            _modifierKindFilter = EditorGUILayout.Popup(_modifierKindFilter, ModifierKindOptions,
                GUILayout.Width(110));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (ctx.IsOffline)
            {
                EditorGUILayout.HelpBox("离线数据仅包含属性投影，缺少持续实例和其他参数修改器。",
                    MessageType.Info);
                _knownModifierSourceIds.Clear();
                DrawUnlinkedAttributeModifiers(in ctx, hasRuntimeView: false);
                return;
            }
            if (ctx.SelectedId.ActorId <= 0 || ctx.SelectedId.ActorId > int.MaxValue ||
                ctx.Facade == null || !ctx.Facade.TryGetSession(out var liveSession) ||
                liveSession == null || !liveSession.TryGetWorld(out var world) ||
                world?.Services == null ||
                !world.Services.TryResolve<IMobaContinuousRuntimeQueryService>(out var query) || query == null)
            {
                EditorGUILayout.HelpBox("当前战斗世界没有持续实例查询服务。", MessageType.Info);
                _knownModifierSourceIds.Clear();
                DrawUnlinkedAttributeModifiers(in ctx, hasRuntimeView: false);
                return;
            }

            var actorId = (int)ctx.SelectedId.ActorId;
            var now = EditorApplication.timeSinceStartup;
            if (_modifierActorId != actorId || !ReferenceEquals(_modifierWorld, world))
            {
                _modifierActorId = actorId;
                _modifierWorld = world;
                _expandedSources.Clear();
                _nextModifierRefreshAt = 0d;
            }
            if (now >= _nextModifierRefreshAt)
            {
                _modifierRuntimes = query.GetOwnerContinuous(actorId) ??
                    Array.Empty<MobaContinuousRuntimeView>();
                _nextModifierRefreshAt = now + 0.15d;
            }

            var sourceCount = 0;
            var modifierCount = 0;
            _knownModifierSourceIds.Clear();
            for (var i = 0; i < _modifierRuntimes.Count; i++)
            {
                var runtime = _modifierRuntimes[i];
                if (runtime?.Modifiers == null) continue;
                for (var j = 0; j < runtime.Modifiers.Count; j++)
                    if (runtime.Modifiers[j].TargetKind ==
                        MobaContinuousModifierTargetKind.Attribute)
                        _knownModifierSourceIds.Add(runtime.Modifiers[j].ModifierSourceId);
                var visible = 0;
                for (var j = 0; j < runtime.Modifiers.Count; j++)
                    if (MatchesModifierKind(runtime.Modifiers[j].TargetKind)) visible++;
                if (visible == 0) continue;

                sourceCount++;
                modifierCount += visible;
                var sourceId = runtime.Modifiers[0].ModifierSourceId;
                var key = $"{actorId}:{sourceId}:{runtime.Id}";
                var expanded = _expandedSources.Contains(key);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                var nextExpanded = EditorGUILayout.Foldout(expanded,
                    $"{BattleDebugDisplayText.ContinuousKind(runtime.Kind)}  配置 #{runtime.ConfigId}  来源 #{sourceId}  ({visible})",
                    true, EditorStyles.foldoutHeader);
                if (nextExpanded != expanded)
                {
                    if (nextExpanded) _expandedSources.Add(key);
                    else _expandedSources.Remove(key);
                }
                EditorGUILayout.LabelField(
                    $"{BattleDebugDisplayText.ContinuousState(runtime.State)}  层数 {runtime.Stack}  已持续 {runtime.ElapsedSeconds:0.###} 秒  剩余 {runtime.RemainingSeconds:0.###} 秒",
                    EditorStyles.miniLabel);
                if (nextExpanded)
                {
                    EditorGUILayout.LabelField(
                        $"施加者 Actor #{runtime.SourceActorId}  目标 Actor #{runtime.TargetActorId}  来源上下文 {runtime.SourceContextId}  根上下文 {runtime.RootContextId}",
                        EditorStyles.wordWrappedMiniLabel);
                    if (!runtime.IsActive)
                        EditorGUILayout.LabelField("当前未生效；以下显示配置声明。", EditorStyles.miniLabel);
                    for (var j = 0; j < runtime.Modifiers.Count; j++)
                        if (MatchesModifierKind(runtime.Modifiers[j].TargetKind))
                            BattleDebugModifierDetails.Draw(runtime, j, ResolveAttributeName,
                                ResolveAttributeValue, ShowAttributeChart);
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.LabelField($"持续来源 {sourceCount} · 修改器 {modifierCount}",
                EditorStyles.miniLabel);
            var unlinkedCount = DrawUnlinkedAttributeModifiers(in ctx);
            if (modifierCount == 0 && unlinkedCount == 0)
                EditorGUILayout.LabelField("当前角色没有匹配的修改器。", EditorStyles.centeredGreyMiniLabel);
        }

        private int DrawUnlinkedAttributeModifiers(in BattleDebugContext ctx,
            bool hasRuntimeView = true)
        {
            if (_modifierKindFilter != 0 && _modifierKindFilter != 1) return 0;
            var count = 0;
            for (var i = 0; i < _viewModel.Modifiers.Count; i++)
            {
                var modifier = _viewModel.Modifiers[i];
                if (_knownModifierSourceIds.Contains(modifier.SourceId)) continue;
                if (count++ == 0)
                    EditorGUILayout.LabelField(hasRuntimeView
                        ? "未关联持续实例的属性投影" : "属性投影", EditorStyles.boldLabel);
                DrawModifier(in ctx, in modifier);
            }
            return count;
        }

        private bool MatchesModifierKind(int targetKind)
        {
            switch (_modifierKindFilter)
            {
                case 1: return targetKind == MobaContinuousModifierTargetKind.Attribute;
                case 2: return targetKind == MobaContinuousModifierTargetKind.StateMachineParameter;
                case 3: return targetKind == MobaContinuousModifierTargetKind.StateFlag;
                case 4: return targetKind == MobaContinuousModifierTargetKind.SkillParameter;
                case 5: return targetKind == MobaContinuousModifierTargetKind.SkillSlotCandidate;
                case 6: return targetKind == MobaContinuousModifierTargetKind.ParameterReferenceCandidate;
                case 7: return targetKind != MobaContinuousModifierTargetKind.Attribute &&
                               targetKind != MobaContinuousModifierTargetKind.StateMachineParameter &&
                               targetKind != MobaContinuousModifierTargetKind.StateFlag &&
                               targetKind != MobaContinuousModifierTargetKind.SkillParameter &&
                               targetKind != MobaContinuousModifierTargetKind.SkillSlotCandidate &&
                               targetKind != MobaContinuousModifierTargetKind.ParameterReferenceCandidate;
                default: return true;
            }
        }

        private string ResolveAttributeName(int attributeId)
        {
            var attributes = _viewModel.Attributes;
            for (var i = 0; i < attributes.Count; i++)
                if (attributes[i].AttributeId == attributeId)
                    return attributes[i].Name;
            return null;
        }

        private void ShowAttributeChart(int attributeId)
        {
            _chartAttributeId = attributeId;
            _viewMode = 0;
            _viewModel.ClearHistory();
        }

        private bool TrySyncRuntimeChart(in BattleDebugContext ctx)
        {
            if (_chartAttributeId <= 0 || ctx.SelectedId.ActorId <= 0 ||
                ctx.SelectedId.ActorId > int.MaxValue || ctx.IsOffline || ctx.Facade == null ||
                !ctx.Facade.TryGetSession(out var session) || session == null ||
                !session.TryGetWorld(out var world) || world?.Services == null ||
                !world.Services.TryResolve<IMobaBattleDiagnosticAttributeHistory>(out var history) ||
                history == null)
            {
                ReleaseRuntimeChart();
                return false;
            }

            if (!ReferenceEquals(_chartHistoryService, history))
            {
                ReleaseRuntimeChart();
                _chartHistoryService = history;
            }
            history.Configure(ctx.SelectedId.ActorId, _chartAttributeId,
                _samplingIntervalFrames, _recordChart);
            _viewModel.ReplaceHistory(history.GetSamples());
            return true;
        }

        private void ReleaseRuntimeChart()
        {
            _chartHistoryService?.Stop();
            _chartHistoryService = null;
        }

        private string ResolveAttributeValue(int attributeId)
        {
            var attributes = _viewModel.Attributes;
            for (var i = 0; i < attributes.Count; i++)
                if (attributes[i].AttributeId == attributeId)
                    return $"属性基础 {attributes[i].BaseValue:0.#####}  最终 {attributes[i].FinalValue:0.#####}";
            return null;
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
            {
                _viewModel.ClearHistory(waitForNextRevision: true);
                _chartHistoryService?.ClearSamples();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("采样间隔", "实时世界按战斗帧采样选中属性；离线快照只有单帧数据"),
                EditorStyles.miniLabel, GUILayout.Width(56));
            var interval = EditorGUILayout.IntField(_samplingIntervalFrames, GUILayout.Width(48));
            if (interval != _samplingIntervalFrames)
            {
                _samplingIntervalFrames = Mathf.Clamp(interval, 1, 600);
                _viewModel.ClearHistory(waitForNextRevision: true);
                _chartHistoryService?.Configure(ctx.SelectedId.ActorId, _chartAttributeId,
                    _samplingIntervalFrames, _recordChart);
            }
            GUILayout.Label("帧", EditorStyles.miniLabel, GUILayout.Width(20));
            GUILayout.FlexibleSpace();
            _compareExpected = GUILayout.Toggle(_compareExpected, "预期值", GUILayout.Width(66));
            EditorGUILayout.EndHorizontal();
            if (_compareExpected)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("目标", EditorStyles.miniLabel, GUILayout.Width(28));
                _expectedValue = EditorGUILayout.FloatField(_expectedValue, GUILayout.Width(80));
                GUILayout.Label("容差", EditorStyles.miniLabel, GUILayout.Width(28));
                var tolerance = EditorGUILayout.FloatField(_expectedTolerance, GUILayout.Width(80));
                if (!float.IsNaN(tolerance) && !float.IsInfinity(tolerance))
                    _expectedTolerance = Mathf.Max(0f, tolerance);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

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
            if (_compareExpected && !float.IsNaN(_expectedValue) && !float.IsInfinity(_expectedValue))
            {
                min = Mathf.Min(min, _expectedValue);
                max = Mathf.Max(max, _expectedValue);
            }
            var padding = Mathf.Max((max - min) * 0.08f, 0.001f);
            min -= padding;
            max += padding;

            var rect = GUILayoutUtility.GetRect(180f, 164f, GUILayout.ExpandWidth(true));
            var plot = new Rect(rect.x + 58f, rect.y + 10f, Mathf.Max(1f, rect.width - 70f), 125f);
            var baseColor = new Color(0.18f, 0.72f, 0.67f);
            var finalColor = new Color(0.96f, 0.65f, 0.23f);
            var expectedColor = new Color(0.82f, 0.36f, 0.38f);
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
                if (_compareExpected && !float.IsNaN(_expectedValue) && !float.IsInfinity(_expectedValue))
                {
                    var y = plot.yMax - plot.height * (_expectedValue - min) / (max - min);
                    var previousColor = Handles.color;
                    Handles.color = expectedColor;
                    Handles.DrawLine(new Vector3(plot.x, y), new Vector3(plot.xMax, y));
                    Handles.color = previousColor;
                }
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
            if (_compareExpected) DrawLegend(expectedColor, "预期值");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{samples.Count} 点 · {(_chartHistoryService == null ? "窗口快照" : "运行时帧末尾")}",
                EditorStyles.miniLabel);
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
            if (_compareExpected && !float.IsNaN(_expectedValue) &&
                !float.IsInfinity(_expectedValue) && !float.IsNaN(_expectedTolerance))
            {
                var delta = selectedSample.FinalValue - _expectedValue;
                EditorGUILayout.LabelField(
                    $"预期 {_expectedValue:0.#####} · 偏差 {delta:+0.#####;-0.#####;0} · 容差 {_expectedTolerance:0.#####} · {(Mathf.Abs(delta) <= _expectedTolerance ? "符合" : "超出")}",
                    EditorStyles.wordWrappedMiniLabel);
                var exceeded = 0;
                var maxDeviation = 0f;
                for (var i = 0; i < samples.Count; i++)
                {
                    var deviation = Mathf.Abs(samples[i].FinalValue - _expectedValue);
                    if (deviation > _expectedTolerance) exceeded++;
                    maxDeviation = Mathf.Max(maxDeviation, deviation);
                }
                EditorGUILayout.LabelField(
                    $"已记录点：符合 {samples.Count - exceeded} · 超差 {exceeded} · 最大绝对偏差 {maxDeviation:0.#####}",
                    EditorStyles.wordWrappedMiniLabel);
            }
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
                $"操作={modifier.Operation} · 优先级 {modifier.Priority} · 数值类型 {ResolveMagnitudeType(modifier.MagnitudeType)}",
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

        private static string ResolveMagnitudeType(int magnitudeType)
        {
            switch (magnitudeType)
            {
                case 0: return "固定值";
                case 1: return "等级曲线";
                case 2: return "属性引用";
                case 3: return "时间曲线";
                case 4: return "组合";
                case 5: return "上下文数值";
                default: return $"未知（{magnitudeType}）";
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
