using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Game.Battle;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    /// <summary>
    /// Displays live temporary objects and continuous effects with their immutable provenance data.
    /// </summary>
    internal sealed class BattleDebugActiveObjectsPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        private readonly List<MobaProjectileLinkDiagnostics> _projectiles = new List<MobaProjectileLinkDiagnostics>(32);
        private readonly List<MobaAreaRuntimeInfo> _areas = new List<MobaAreaRuntimeInfo>(32);
        private readonly List<MobaParameterResolutionTrace> _recentParameters =
            new List<MobaParameterResolutionTrace>(128);
        private IReadOnlyList<MobaContinuousRuntimeView> _continuous = Array.Empty<MobaContinuousRuntimeView>();
        private readonly HashSet<string> _expandedModifiers = new HashSet<string>();
        private Vector2 _scroll;
        private double _nextRefreshAt;

        public string Name => "活跃对象";
        public int Order => 395;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => true;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (ctx.IsOffline)
            {
                EditorGUILayout.HelpBox(
                    "离线 Artifact 不包含可继续检查的活跃临时对象。请在实时会话暂停后查看投射物、区域和持续效果。",
                    MessageType.Info);
                return;
            }

            if (!TryResolveServices(
                    ctx.Facade,
                    out var links,
                    out var areas,
                    out var continuous,
                    out var parameters))
            {
                EditorGUILayout.HelpBox(
                    "当前实时世界未就绪，无法解析活跃对象运行时服务。",
                    MessageType.Info);
                return;
            }

            DrawToolbar(links, areas, continuous, ctx.RequestRepaint);
            RefreshIfNeeded(links, areas, continuous, force: false);
            DrawSummary();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawProjectiles(in ctx);
            DrawAreas(in ctx);
            DrawContinuous(in ctx);
            DrawRecentResolutions(parameters, in ctx);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar(
            MobaProjectileLinkService links,
            MobaAreaRuntimeService areas,
            IMobaContinuousRuntimeQueryService continuous,
            Action requestRepaint)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("实时活跃对象", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshIfNeeded(links, areas, continuous, force: true);
                requestRepaint?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void RefreshIfNeeded(
            MobaProjectileLinkService links,
            MobaAreaRuntimeService areas,
            IMobaContinuousRuntimeQueryService continuous,
            bool force)
        {
            var now = EditorApplication.timeSinceStartup;
            if (!force && now < _nextRefreshAt) return;

            _nextRefreshAt = now + 0.15d;
            _projectiles.Clear();
            links?.CopyDiagnosticsTo(_projectiles);
            _projectiles.Sort((left, right) => right.ProjectileId.Value.CompareTo(left.ProjectileId.Value));

            _areas.Clear();
            areas?.TryGetAreas(_areas);
            _areas.Sort((left, right) => right.AreaId.CompareTo(left.AreaId));

            _continuous = continuous?.GetAllContinuous() ?? Array.Empty<MobaContinuousRuntimeView>();
        }

        private void DrawSummary()
        {
            EditorGUILayout.LabelField(
                $"投射物={_projectiles.Count}  区域={_areas.Count}  持续效果={_continuous.Count}",
                EditorStyles.miniLabel);

            if (_projectiles.Count == 0 && _areas.Count == 0 && _continuous.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "当前没有可检查的活跃临时对象或持续效果。可暂停在技能飞行、范围生效或持续状态期间后刷新。",
                    MessageType.Info);
            }
        }

        private void DrawProjectiles(in BattleDebugContext ctx)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("投射物", EditorStyles.boldLabel);
            if (_projectiles.Count == 0)
            {
                EditorGUILayout.LabelField("（无活跃投射物）", EditorStyles.miniLabel);
                return;
            }

            for (var i = 0; i < _projectiles.Count; i++)
            {
                var entry = _projectiles[i];
                var source = entry.Source;
                var skillRuntimeHandle = source.SkillRuntimeHandle;
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField(
                    $"投射物 #{entry.ProjectileId.Value}  Actor #{entry.ActorId}  配置={source.ProjectileConfigId}",
                    EditorStyles.miniBoldLabel);
                DrawLineage(
                    source.SourceActorId,
                    source.InitialTargetActorId,
                    source.SourceContextId,
                    source.RootContextId,
                    source.OwnerContextId,
                    in skillRuntimeHandle,
                    in ctx);
                DrawConfigButton(BattleDebugConfigKind.Projectile, source.ProjectileConfigId, in ctx);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawAreas(in BattleDebugContext ctx)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("区域", EditorStyles.boldLabel);
            if (_areas.Count == 0)
            {
                EditorGUILayout.LabelField("（无活跃区域）", EditorStyles.miniLabel);
                return;
            }

            for (var i = 0; i < _areas.Count; i++)
            {
                var area = _areas[i];
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField(
                    $"区域 #{area.AreaId}  模板={area.TemplateId}  所有者 #{area.OwnerActorId}",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(
                    $"中心={area.Center}  半径={area.Radius:0.###}  生成帧={area.SpawnFrame}  延迟触发帧={area.DelayTriggerFrame}",
                    EditorStyles.miniLabel);
                var noSkillRuntime = default(MobaSkillCastRuntimeHandle);
                DrawLineage(
                    area.OwnerActorId,
                    0,
                    area.SourceContextId,
                    area.RootContextId,
                    area.OwnerContextId,
                    in noSkillRuntime,
                    in ctx);
                DrawConfigButton(BattleDebugConfigKind.Area, area.TemplateId, in ctx);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawContinuous(in BattleDebugContext ctx)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("持续效果", EditorStyles.boldLabel);
            if (_continuous == null || _continuous.Count == 0)
            {
                EditorGUILayout.LabelField("（无活跃持续效果）", EditorStyles.miniLabel);
                return;
            }

            for (var i = 0; i < _continuous.Count; i++)
            {
                var runtime = _continuous[i];
                if (runtime == null) continue;

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField(
                    $"{BattleDebugDisplayText.ContinuousKind(runtime.Kind)}  ID={runtime.Id}  配置={runtime.ConfigId}",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(
                    $"状态={BattleDebugDisplayText.ContinuousState(runtime.State)}  活跃={BattleDebugDisplayText.Bool(runtime.IsActive)}  已暂停={BattleDebugDisplayText.Bool(runtime.IsPaused)}  已持续={runtime.ElapsedSeconds:0.###}  剩余={runtime.RemainingSeconds:0.###}",
                    EditorStyles.miniLabel);
                var skillRuntimeHandle = runtime.SkillRuntimeHandle;
                DrawLineage(
                    runtime.SourceActorId,
                    runtime.TargetActorId,
                    runtime.SourceContextId,
                    runtime.RootContextId,
                    runtime.OwnerContextId,
                    in skillRuntimeHandle,
                    in ctx);
                DrawTagsAndRules(runtime);
                DrawModifiers(runtime);
                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawTagsAndRules(MobaContinuousRuntimeView runtime)
        {
            if (runtime.Tags != null && runtime.Tags.Count > 0)
            {
                EditorGUILayout.LabelField("授予标签", EditorStyles.miniBoldLabel);
                foreach (var tag in runtime.Tags)
                    EditorGUILayout.LabelField($"{tag.Name}  来源={tag.Source}", EditorStyles.miniLabel);
            }

            var result = runtime.CurrentTagRuleResult;
            if (result.HasResult)
            {
                EditorGUILayout.LabelField(
                    $"标签规则：{result.Action}  {(result.Allowed ? "通过" : "阻断")}",
                    EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(result.Reason))
                    EditorGUILayout.LabelField(result.Reason, EditorStyles.wordWrappedMiniLabel);
                foreach (var check in result.Checks)
                    EditorGUILayout.LabelField(
                        $"{check.Category}：{(check.Satisfied ? "通过" : "未通过")}  " +
                        $"必需={check.RequiredSatisfied}  阻断={check.BlockedSatisfied}",
                        EditorStyles.miniLabel);
            }
            var last = runtime.LastTagRuleResult;
            if (last.HasResult && (!result.HasResult || last.Action != result.Action || last.Reason != result.Reason))
                EditorGUILayout.LabelField(
                    $"最近判定：{last.Action}  {last.Reason}", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawModifiers(MobaContinuousRuntimeView runtime)
        {
            var modifiers = runtime.Modifiers;
            if (modifiers == null || modifiers.Count == 0) return;

            var key = $"{runtime.OwnerActorId}:{modifiers[0].ModifierSourceId}";
            var expanded = _expandedModifiers.Contains(key);
            if (EditorGUILayout.Foldout(expanded, $"修改器 ({modifiers.Count})", true) != expanded)
            {
                if (expanded) _expandedModifiers.Remove(key);
                else _expandedModifiers.Add(key);
            }
            if (!_expandedModifiers.Contains(key)) return;

            if (!runtime.IsActive)
                EditorGUILayout.LabelField("当前未生效；以下显示配置声明。", EditorStyles.miniLabel);

            for (var i = 0; i < modifiers.Count; i++)
                BattleDebugModifierDetails.Draw(runtime, i);
        }

        private void DrawRecentResolutions(MobaSkillParamModifierService parameters,
            in BattleDebugContext ctx)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("最近参数解析", EditorStyles.boldLabel);
            if (parameters == null)
            {
                EditorGUILayout.LabelField("（参数服务未就绪）", EditorStyles.miniLabel);
                return;
            }
            parameters.CopyRecentResolutionsTo(_recentParameters);
            if (GUILayout.Button(new GUIContent("导出", "导出最近解析与当前来源投影"),
                    EditorStyles.miniButton, GUILayout.Width(48)))
            {
                var path = EditorUtility.SaveFilePanel("导出修改器诊断", string.Empty,
                    $"moba-modifiers-{DateTime.Now:yyyyMMdd-HHmmss}", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        File.WriteAllText(path, MobaModifierResolutionArtifactCodec.Export(parameters),
                            new UTF8Encoding(false));
                    }
                    catch (Exception error)
                    {
                        Debug.LogError($"[BattleDebug] Modifier artifact export failed: {error}");
                    }
                }
            }
            var shown = 0;
            for (var i = _recentParameters.Count - 1; i >= 0 && shown < 24; i--)
            {
                var entry = _recentParameters[i];
                if (ctx.HasSelection && entry.Owner.Scope == MobaModifierOwnerScope.Actor &&
                    entry.Owner.Id != ctx.SelectedId.ActorId) continue;
                MobaSkillParamModifierKeys.TryGetParameterId(entry.Key, out var parameterId);
                var frame = entry.Frame >= 0 ? $"帧 {entry.Frame}  " : string.Empty;
                EditorGUILayout.LabelField(
                    $"{frame}{entry.Record.Stage}  参数 {parameterId}  Owner {entry.Owner.Scope}#{entry.Owner.Id}  " +
                    $"{entry.BaseValue:0.###} → {entry.FinalValue:0.###}  命中 {entry.MatchedCount}",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(FormatRecord(entry.Record), EditorStyles.miniLabel);
                if (entry.MissingSelectorFields != MobaParameterIdentityFields.None)
                    EditorGUILayout.LabelField($"缺少筛选字段：{entry.MissingSelectorFields}",
                        EditorStyles.wordWrappedMiniLabel);
                shown++;
            }
            if (shown == 0)
                EditorGUILayout.LabelField("（暂无该实体的阶段解析）", EditorStyles.miniLabel);
        }

        private static string FormatRecord(MobaParameterRecord record)
        {
            var text = new StringBuilder();
            AppendSelector(text, "槽位", record.Slot);
            AppendSelector(text, "基础技能", record.BaseSkillId);
            AppendSelector(text, "有效技能", record.EffectiveSkillId);
            AppendSelector(text, "发射器", record.LauncherTemplateId);
            AppendSelector(text, "子弹", record.ProjectileTemplateId);
            AppendSelector(text, "区域", record.AreaTemplateId);
            AppendSelector(text, "Buff", record.BuffTemplateId);
            AppendSelector(text, "召唤物", record.SummonTemplateId);
            return text.ToString();
        }

        private static void AppendSelector(StringBuilder text, string label, int value)
        {
            if (value == 0) return;
            if (text.Length > 0) text.Append("  ·  ");
            text.Append(label).Append(' ').Append(value);
        }

        private static void DrawLineage(
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            in MobaSkillCastRuntimeHandle skillRuntimeHandle,
            in BattleDebugContext ctx)
        {
            EditorGUILayout.LabelField(
                $"Trace：来源={sourceContextId}  根节点={rootContextId}  所有者={ownerContextId}  技能运行时={FormatRuntime(in skillRuntimeHandle)}",
                EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            DrawActorButton("来源", sourceActorId, ctx.SelectActor);
            DrawActorButton("目标", targetActorId, ctx.SelectActor);
            var traceContextId = sourceContextId != 0L ? sourceContextId : rootContextId;
            var effectiveRootId = rootContextId != 0L ? rootContextId : traceContextId;
            EditorGUI.BeginDisabledGroup(effectiveRootId <= 0L || traceContextId <= 0L || ctx.OpenTrace == null);
            if (GUILayout.Button("打开 Trace", GUILayout.Width(90)))
            {
                ctx.OpenTrace?.Invoke(effectiveRootId, traceContextId);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private static string FormatRuntime(in MobaSkillCastRuntimeHandle handle)
        {
            return handle.IsValid ? $"#{handle.RuntimeId}:{handle.Generation}" : "-";
        }

        private static void DrawConfigButton(
            BattleDebugConfigKind kind,
            int configId,
            in BattleDebugContext ctx)
        {
            EditorGUI.BeginDisabledGroup(configId <= 0 || ctx.OpenConfig == null);
            if (GUILayout.Button("打开配置", GUILayout.Width(80)))
            {
                ctx.OpenConfig?.Invoke(new BattleDebugConfigReference(kind, configId));
            }
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawActorButton(string label, int actorId, Action<long> selectActor)
        {
            EditorGUI.BeginDisabledGroup(actorId <= 0 || selectActor == null);
            if (GUILayout.Button($"{label} #{actorId}", GUILayout.Width(100)))
            {
                selectActor?.Invoke(actorId);
            }
            EditorGUI.EndDisabledGroup();
        }

        private static bool TryResolveServices(
            IBattleDebugFacade facade,
            out MobaProjectileLinkService links,
            out MobaAreaRuntimeService areas,
            out IMobaContinuousRuntimeQueryService continuous,
            out MobaSkillParamModifierService parameters)
        {
            links = null;
            areas = null;
            continuous = null;
            parameters = null;
            if (facade == null || !facade.TryGetSession(out var session) || session == null) return false;
            if (!session.TryGetWorld(out var world) || world?.Services == null) return false;

            world.Services.TryResolve(out links);
            world.Services.TryResolve(out areas);
            world.Services.TryResolve(out continuous);
            world.Services.TryResolve(out parameters);
            return links != null || areas != null || continuous != null || parameters != null;
        }
    }

    internal static class BattleDebugModifierDetails
    {
        public static void Draw(MobaContinuousRuntimeView runtime, int index,
            Func<int, string> attributeName = null, Func<int, string> attributeValue = null,
            Action<int> showAttributeChart = null)
        {
            if (runtime?.Modifiers == null || index < 0 || index >= runtime.Modifiers.Count) return;
            var modifier = runtime.Modifiers[index];
            var explanations = runtime.ModifierExplanations;
            var explanation = explanations != null && index < explanations.Count
                ? explanations[index] : default;
            var hasExplanation = runtime.IsActive && explanation.Continuous != null;

            EditorGUILayout.BeginVertical();
            var target = $"{TargetKind(modifier.TargetKind)} #{modifier.TargetId}";
            if (modifier.TargetKind == MobaContinuousModifierTargetKind.Attribute)
            {
                var name = attributeName?.Invoke(modifier.TargetId);
                if (!string.IsNullOrEmpty(name)) target += $"  {name}";
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"{target}  {Operation(modifier.Op)}  优先级 {modifier.Priority}",
                EditorStyles.miniBoldLabel);
            if (modifier.TargetKind == MobaContinuousModifierTargetKind.Attribute &&
                showAttributeChart != null &&
                GUILayout.Button(new GUIContent("曲线", "查看此修改器目标属性的采样曲线"),
                    EditorStyles.miniButton, GUILayout.Width(38)))
                showAttributeChart(modifier.TargetId);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                $"来源 ID {modifier.ModifierSourceId}  层数 {modifier.Stack}  策略 {(modifier.EvaluationPolicy == MobaContinuousModifierEvaluationPolicy.OnApplySnapshot ? "应用时快照" : "实时")}",
                EditorStyles.miniLabel);

            if (modifier.TargetKind == MobaContinuousModifierTargetKind.SkillSlotCandidate)
                EditorGUILayout.LabelField($"候选技能 ID {modifier.CandidateSkillId}", EditorStyles.miniLabel);
            else if (modifier.TargetKind == MobaContinuousModifierTargetKind.ParameterReferenceCandidate)
                EditorGUILayout.LabelField($"候选引用 ID {modifier.CandidateReferenceId}", EditorStyles.miniLabel);
            else
                EditorGUILayout.LabelField(
                    $"数值来源 {modifier.MagnitudeType}  声明值 {modifier.MagnitudeBaseValue:0.#####}  系数 {modifier.MagnitudeCoefficient:0.#####}",
                    EditorStyles.miniLabel);

            if (modifier.HasFormula)
                EditorGUILayout.LabelField("属性公式（应用时捕获）：" + modifier.FormulaDescription,
                    EditorStyles.wordWrappedMiniLabel);
            if (modifier.TargetKind == MobaContinuousModifierTargetKind.SkillParameter ||
                modifier.TargetKind == MobaContinuousModifierTargetKind.ParameterReferenceCandidate)
            {
                if (MobaSkillParamModifierKeys.TryResolve(modifier.TargetId, out var key) &&
                    MobaSkillParamModifierKeys.TryGetResolveStage(key, out var stage))
                    EditorGUILayout.LabelField($"消费阶段 {stage}", EditorStyles.miniLabel);
            }
            if (!modifier.Selector.IsEmpty)
                EditorGUILayout.LabelField("阶段筛选：" + FormatSelector(modifier.Selector),
                    EditorStyles.wordWrappedMiniLabel);
            if (modifier.TargetKind == MobaContinuousModifierTargetKind.Attribute)
            {
                var value = attributeValue?.Invoke(modifier.TargetId);
                if (!string.IsNullOrEmpty(value))
                    EditorGUILayout.LabelField(value, EditorStyles.miniLabel);
            }

            if (hasExplanation)
            {
                EditorGUILayout.LabelField(
                    $"声明计算 {FormatValue(explanation.DeclaredMagnitude)}  叠层 {FormatValue(explanation.StackedMagnitude)}  当前 {(explanation.HasCurrentValue ? explanation.CurrentValue.ToString("0.#####") : "未取得")}  投影 {FormatValue(explanation.ProjectedMagnitude)}",
                    EditorStyles.wordWrappedMiniLabel);
                if (explanation.HasCapturedValue)
                    EditorGUILayout.LabelField($"捕获值 {explanation.CapturedValue:0.#####}  {explanation.CaptureMode}",
                        EditorStyles.miniLabel);
                if (explanation.DeclaredMagnitude.Type == AbilityKit.Modifiers.MagnitudeSourceType.Attribute)
                    EditorGUILayout.LabelField(
                        $"引用属性键 {explanation.DeclaredMagnitude.AttributeKind}:{explanation.DeclaredMagnitude.AttributeSubCategory}:{explanation.DeclaredMagnitude.AttributeCustomId}",
                        EditorStyles.miniLabel);
                if (explanation.DeclaredMagnitude.IsTimeVarying)
                {
                    EditorGUILayout.LabelField(
                        $"时间曲线 {runtime.ElapsedSeconds:0.###}/{explanation.DeclaredMagnitude.Duration:0.###} 秒  衰减类型 {explanation.DeclaredMagnitude.DecayType}",
                        EditorStyles.miniLabel);
                    var samples = explanation.DeclaredMagnitude.CurveSamples;
                    if (samples != null && samples.Count > 0)
                        EditorGUILayout.LabelField("曲线采样 " + FormatCurve(samples),
                            EditorStyles.wordWrappedMiniLabel);
                }
                if (!string.IsNullOrEmpty(explanation.Reason))
                    EditorGUILayout.LabelField(explanation.Reason, EditorStyles.wordWrappedMiniLabel);
            }
            else if (runtime.IsActive)
                EditorGUILayout.LabelField("当前计算说明未取得", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private static string FormatValue(MobaContinuousModifierMagnitudeView value) =>
            value.HasCalculatedValue ? value.CalculatedValue.ToString("0.#####") : "未取得";

        private static string FormatCurve(IReadOnlyList<float> samples)
        {
            var text = new StringBuilder();
            for (var i = 0; i < samples.Count; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(samples[i].ToString("0.###"));
            }
            return text.ToString();
        }

        private static string TargetKind(int kind)
        {
            switch (kind)
            {
                case MobaContinuousModifierTargetKind.Attribute: return "属性";
                case MobaContinuousModifierTargetKind.StateMachineParameter: return "状态机参数";
                case MobaContinuousModifierTargetKind.StateFlag: return "状态标记";
                case MobaContinuousModifierTargetKind.SkillParameter: return "领域参数";
                case MobaContinuousModifierTargetKind.SkillSlotCandidate: return "技能槽位";
                case MobaContinuousModifierTargetKind.ParameterReferenceCandidate: return "引用候选";
                default: return $"目标类型 {kind}";
            }
        }

        private static string Operation(int operation)
        {
            switch (operation)
            {
                case 0: return "加";
                case 1: return "乘";
                case 2: return "覆盖";
                case 3: return "百分比加";
                default: return $"操作 {operation}";
            }
        }

        private static string FormatSelector(MobaParameterSelector selector)
        {
            var text = new StringBuilder();
            Append(text, "槽位", selector.Slot);
            Append(text, "基础技能", selector.BaseSkillId);
            Append(text, "有效技能", selector.EffectiveSkillId);
            Append(text, "发射器", selector.LauncherTemplateId);
            Append(text, "子弹", selector.ProjectileTemplateId);
            Append(text, "区域", selector.AreaTemplateId);
            Append(text, "Buff", selector.BuffTemplateId);
            Append(text, "召唤物", selector.SummonTemplateId);
            return text.ToString();
        }

        private static void Append(StringBuilder text, string label, int value)
        {
            if (value == 0) return;
            if (text.Length > 0) text.Append("  ·  ");
            text.Append(label).Append(' ').Append(value);
        }
    }
}
