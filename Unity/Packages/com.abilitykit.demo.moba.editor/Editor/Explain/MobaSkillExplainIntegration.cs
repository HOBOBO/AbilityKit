using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Explain;
using AbilityKit.Ability.Explain.Editor;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Modifiers;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>
    /// 把 Ability Explain 接到 demo.moba 的真实数据上（E2 首个生产接入）：
    /// - Provider：列出 SkillSO 里的技能；
    /// - Resolver：把一个技能解释成森林（元数据 + PreCast/Cast 技能流的相位层级，引用 skills / skill_flows 表行）；
    /// - Navigator：open_table_row → 定位对应配置表 SO 资产；open_asset → Ping 资产；open_file → 外部编辑器打开。
    ///
    /// 红线：Resolver 只读配置资产并投影，不重算任何业务逻辑（伤害公式、buff 效果等仍以运行时/配置为唯一真相）。
    /// </summary>
    [InitializeOnLoad]
    internal static class MobaSkillExplainIntegration
    {
        static MobaSkillExplainIntegration()
        {
            AbilityExplainRegistry.Register(new MobaSkillEntityProvider());
            AbilityExplainRegistry.Register(new MobaSkillResolver());
            AbilityExplainRegistry.Register(new MobaExplainNavigator());
            AbilityExplainRegistry.Register(new MobaModifierContextEditor());
        }

        // ---- 共享查找（懒加载，每次解析时现查，配置表量级小）----

        private static T LoadTable<T>() where T : MobaConfigTableAssetSO
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            foreach (var guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }

            return null;
        }

        private static SkillDTO FindSkill(string id)
        {
            if (!int.TryParse(id, out var skillId)) return null;
            var so = LoadTable<SkillSO>();
            if (so?.dataList == null) return null;
            for (var i = 0; i < so.dataList.Length; i++)
            {
                if (so.dataList[i] != null && so.dataList[i].Id == skillId) return so.dataList[i];
            }

            return null;
        }

        private static SkillFlowDef FindFlow(SkillFlowSO so, int flowId)
        {
            if (so?.dataList == null) return null;
            for (var i = 0; i < so.dataList.Length; i++)
            {
                if (so.dataList[i] != null && so.dataList[i].Id == flowId) return so.dataList[i];
            }

            return null;
        }

        private static MobaConfigTableAssetSO FindTableByFile(string fileWithoutExt)
        {
            if (string.IsNullOrEmpty(fileWithoutExt)) return null;
            foreach (var t in MobaConfigTableRegistry.TableAssetTypes)
            {
                var guids = AssetDatabase.FindAssets($"t:{t.Name}");
                if (guids.Length == 0) continue;
                var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[0]), t) as MobaConfigTableAssetSO;
                if (asset != null && string.Equals(asset.FileWithoutExt, fileWithoutExt, StringComparison.OrdinalIgnoreCase))
                {
                    return asset;
                }
            }

            return null;
        }

        // ---- 效果表（effects.json：无 SO 表，JSON 即真相；跳转直接打开 JSON 文件）----

        private const string EffectsJsonPath = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/effects.json";

        private static List<EffectEntry> _effectCache;

        [Serializable]
        private sealed class EffectEntry
        {
            public int Id;
            public string Name;
            public int EffectType;
            public int BaseDamage;
            public int DamageType;
            public float AttackRatio;
            public int TargetPolicy;
            public float Radius;
        }

        private static List<EffectEntry> LoadEffects()
        {
            if (_effectCache != null) return _effectCache;
            try
            {
                var abs = EffectsJsonAbsolutePath();
                if (File.Exists(abs))
                {
                    _effectCache = JsonConvert.DeserializeObject<List<EffectEntry>>(File.ReadAllText(abs)) ?? new List<EffectEntry>();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MobaSkillExplainIntegration] Failed to load effects.json: {e.Message}");
            }

            _effectCache ??= new List<EffectEntry>();
            return _effectCache;
        }

        private static EffectEntry FindEffect(int effectId)
        {
            var list = LoadEffects();
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Id == effectId) return list[i];
            }

            return null;
        }

        private static string EffectsJsonAbsolutePath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", EffectsJsonPath));
        }

        // ---- Entity Provider ----

        private sealed class MobaSkillEntityProvider : IEntityProvider, IEntityProviderEx, IRegistryPriority
        {
            public int Priority => 10;

            public bool CanProvide(string searchText) => true;

            public IEnumerable<PipelineItemKey> Query(string searchText)
            {
                var so = LoadTable<SkillSO>();
                if (so?.dataList == null) yield break;

                for (var i = 0; i < so.dataList.Length; i++)
                {
                    var s = so.dataList[i];
                    if (s == null) continue;

                    if (!string.IsNullOrEmpty(searchText)
                        && !s.Id.ToString().Contains(searchText)
                        && (s.Name == null || s.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        continue;
                    }

                    yield return new PipelineItemKey("skill", s.Id.ToString());
                }
            }

            public string GetDisplayName(in PipelineItemKey key)
            {
                if (key.Type != "skill") return key.ToString();
                var s = FindSkill(key.Id);
                return s != null ? $"#{s.Id} {s.Name}" : $"skill #{key.Id}";
            }
        }

        // ---- Resolver ----

        private sealed class MobaSkillResolver : IExplainResolver, IExplainResolverEx, IRegistryPriority
        {
            public int Priority => 10;

            public bool CanResolve(ExplainResolveRequest request) => request != null && request.Key.Type == "skill";

            public bool CanExpand(ExplainExpandRequest request) => false;

            public bool TryResolve(ExplainResolveRequest request, out ExplainResolveResult result)
            {
                result = null;
                if (request.Key.Type != "skill") return false;

                var skill = FindSkill(request.Key.Id);
                if (skill == null) return false;

                var root = ExplainNode.Create($"Skill #{skill.Id} {skill.Name}");
                root.NodeId = $"n_s{skill.Id}";
                root.Kind = "skill";
                SetTableRowSource(root, "skills", skill.Id.ToString());
                root.SummaryLines.Add($"Cooldown: {skill.CooldownMs}ms");
                root.SummaryLines.Add($"Range: {skill.Range}");
                root.SummaryLines.Add($"Category: {skill.Category}  Type: {skill.SkillType}");

                var timelineEvents = new List<ExplainTimelineEvent>();

                var flowSo = LoadTable<SkillFlowSO>();
                if (skill.PreCastFlowId > 0 && flowSo != null)
                {
                    var flow = FindFlow(flowSo, skill.PreCastFlowId);
                    if (flow != null) root.Children.Add(BuildFlowNode(flow, "PreCast", $"s{skill.Id}/pre", timelineEvents));
                }

                if (skill.CastFlowId > 0 && flowSo != null)
                {
                    var flow = FindFlow(flowSo, skill.CastFlowId);
                    if (flow != null) root.Children.Add(BuildFlowNode(flow, "Cast", $"s{skill.Id}/cast", timelineEvents));
                }

                if (request.Context?.Modifiers != null && request.Context.Modifiers.Count > 0)
                {
                    root.Children.Add(BuildModifierPreviewNode(skill, request.Context.Modifiers));
                }

                var forest = new ExplainForest();
                forest.Roots.Add(new ExplainTreeRoot
                {
                    Kind = "skill",
                    Key = request.Key,
                    Title = root.Title,
                    Root = root
                });

                result = new ExplainResolveResult
                {
                    Forest = forest,
                    Timeline = timelineEvents.Count > 0 ? new ExplainTimeline { Events = timelineEvents } : null
                };
                return true;
            }

            public bool TryExpandDiscoveredRoot(ExplainExpandRequest request, out ExplainTreeRoot root)
            {
                root = null;
                return false;
            }

            private static readonly ModifierCalculator PreviewCalculator = new ModifierCalculator();

            // 预览用一个占位键即可：ModifierCalculator 的浮点计算只按 op 作用到单一 baseValue，Key 仅作元数据。
            private static readonly ModifierKey PreviewKey = ModifierKey.Create(categoryId: 1);

            private static ExplainNode BuildModifierPreviewNode(SkillDTO skill, List<ModifierPreviewInput> inputs)
            {
                if (inputs == null || inputs.Count == 0) return null;

                var modifiers = new ModifierData[inputs.Count];
                for (var i = 0; i < inputs.Count; i++)
                {
                    modifiers[i] = ToModifierData(inputs[i]);
                }

                // 以技能冷却为演示目标：复用真实 ModifierCalculator，不重算。
                var baseValue = (float)skill.CooldownMs;
                var result = PreviewCalculator.Calculate(modifiers, baseValue);

                var node = ExplainNode.Create("Modifier Preview");
                node.NodeId = $"n_s{skill.Id}/mods";
                node.Kind = "modifier_preview";
                node.SummaryLines.Add($"Cooldown {baseValue:0.#}ms → {result.FinalValue:0.#}ms");
                if (result.HasModifiers)
                {
                    node.SummaryLines.Add(result.ToString());
                }

                for (var i = 0; i < inputs.Count; i++)
                {
                    var input = inputs[i];
                    if (input == null) continue;
                    var child = ExplainNode.Create(string.IsNullOrEmpty(input.Label) ? $"{input.Op} {input.Value}" : input.Label);
                    child.NodeId = $"n_s{skill.Id}/mods/{i}";
                    child.Kind = "modifier";
                    child.SummaryLines.Add($"op={input.Op} value={input.Value}");
                    node.Children.Add(child);
                }

                return node;
            }

            private static ModifierData ToModifierData(ModifierPreviewInput input)
            {
                switch (input?.Op)
                {
                    case "mul":
                        return ModifierData.Mul(PreviewKey, input.Value);
                    case "percent_add":
                        return ModifierData.PercentAdd(PreviewKey, input.Value);
                    case "override":
                        return ModifierData.Override(PreviewKey, input.Value);
                    case "add":
                    default:
                        return ModifierData.Add(PreviewKey, input.Value);
                }
            }

            private static void SetTableRowSource(ExplainNode node, string table, string rowId, string fieldPath = null)
            {
                node.Source = ExplainSourceRef.TableRow(table, rowId, fieldPath);
                node.Actions.Add(ExplainAction.Navigate("Open", NavigationTarget.OpenTableRow(table, rowId, fieldPath)));
            }

            private static ExplainNode BuildFlowNode(SkillFlowDef flow, string label, string path, List<ExplainTimelineEvent> timeline)
            {
                var node = ExplainNode.Create($"{label} Flow #{flow.Id}" + (string.IsNullOrEmpty(flow.Name) ? string.Empty : $" {flow.Name}"));
                node.NodeId = $"n_{path}";
                node.Kind = "skillflow";
                SetTableRowSource(node, "skill_flows", flow.Id.ToString());
                if (flow.PipelineContinuousTagTemplateId > 0)
                {
                    node.SummaryLines.Add($"Pipeline continuous tag template: {flow.PipelineContinuousTagTemplateId}");
                }

                AddPhaseChildren(node, flow.Phases, flow.Id, path, timeline);
                return node;
            }

            private static void AddPhaseChildren(ExplainNode node, IReadOnlyList<SkillPhaseDef> phases, int flowId, string path, List<ExplainTimelineEvent> timeline)
            {
                if (phases == null) return;
                for (var i = 0; i < phases.Count; i++)
                {
                    var child = BuildPhaseNode(phases[i], flowId, path, i, timeline);
                    if (child != null) node.Children.Add(child);
                }
            }

            private static ExplainNode BuildPhaseNode(SkillPhaseDef phase, int flowId, string path, int index, List<ExplainTimelineEvent> timeline)
            {
                if (phase == null) return null;
                var phasePath = $"{path}/p{index}";

                var node = ExplainNode.Create(phase.DisplayTitle);
                node.NodeId = $"n_{phasePath}";
                node.Kind = "phase_" + phase.PhaseType;
                SetTableRowSource(node, "skill_flows", flowId.ToString(), phasePath);

                switch (phase)
                {
                    case SkillSequencePhaseDef seq:
                        AddPhaseChildren(node, seq.Children, flowId, phasePath, timeline);
                        break;
                    case SkillParallelPhaseDef par:
                        AddPhaseChildren(node, par.Children, flowId, phasePath, timeline);
                        break;
                    case SkillRepeatPhaseDef rep:
                        node.SummaryLines.Add($"Repeat x{rep.RepeatCount}, interval {rep.IntervalMs}ms");
                        if (rep.Phase != null)
                        {
                            var child = BuildPhaseNode(rep.Phase, flowId, phasePath, 0, timeline);
                            if (child != null) node.Children.Add(child);
                        }
                        break;
                    case SkillTimelinePhaseDef tl:
                    {
                        var events = tl?.Timeline?.Events;
                        var durationMs = tl?.Timeline?.DurationMs ?? 0;
                        node.SummaryLines.Add($"Timeline phase ({(events != null ? events.Length : 0)} events, duration {durationMs}ms)");
                        if (events == null) break;

                        var effectsPath = EffectsJsonAbsolutePath();
                        for (var i = 0; i < events.Length; i++)
                        {
                            var ev = events[i];
                            if (ev == null) continue;

                            var effect = FindEffect(ev.EffectId);
                            var title = $"t={ev.AtMs}ms · " + (effect != null ? effect.Name : $"Effect {ev.EffectId}");

                            var child = ExplainNode.Create(title);
                            child.NodeId = $"n_{phasePath}/e{i}";
                            child.Kind = "timeline_event";
                            child.Source = ExplainSourceRef.File(effectsPath);
                            child.Actions.Add(ExplainAction.Navigate("Open", NavigationTarget.OpenFile(effectsPath)));

                            if (effect != null)
                            {
                                if (effect.EffectType == 1)
                                {
                                    child.SummaryLines.Add(effect.BaseDamage > 0
                                        ? $"Damage {effect.BaseDamage}" + (effect.AttackRatio > 0 ? $" (+{effect.AttackRatio * 100:0.#}% 攻击)" : string.Empty)
                                        : "Damage");
                                }
                                else
                                {
                                    child.SummaryLines.Add("状态/增益效果");
                                }
                            }
                            else
                            {
                                child.SummaryLines.Add($"EffectId {ev.EffectId} 未在 effects.json 中找到");
                            }

                            node.Children.Add(child);

                            var timelineEvent = ExplainTimelineEvent.Create(node.NodeId, ev.AtMs / 1000f, title);
                            timelineEvent.Source = ExplainSourceRef.File(effectsPath);
                            timelineEvent.NavigateTo = NavigationTarget.OpenFile(effectsPath);
                            timeline.Add(timelineEvent);
                        }

                        break;
                    }
                    case SkillRulePlanPhaseDef rp:
                        node.SummaryLines.Add($"Triggers: {(rp.TriggerIds != null && rp.TriggerIds.Length > 0 ? string.Join(", ", rp.TriggerIds.Select(x => x.ToString())) : "none")}");
                        node.SummaryLines.Add(rp.AbortOnFailure ? "Abort on failure" : "Continue on failure");
                        break;
                    case SkillDelayPhaseDef d:
                        node.SummaryLines.Add($"Delay {d.DelayMs}ms");
                        break;
                    case SkillWaitUntilPhaseDef w:
                        node.SummaryLines.Add($"Wait until {w.Condition} (timeout {w.TimeoutMs}ms)");
                        break;
                    case SkillChecksPhaseDef:
                        node.SummaryLines.Add("Deprecated checks phase");
                        break;
                }

                return node;
            }
        }

        // ---- Navigator ----

        private sealed class MobaExplainNavigator : INavigator
        {
            public bool CanNavigate(NavigationTarget target)
            {
                return target != null
                    && (target.Kind == "open_table_row" || target.Kind == "open_asset" || target.Kind == "open_file");
            }

            public void Navigate(NavigationTarget target)
            {
                switch (target.Kind)
                {
                    case "open_table_row":
                        NavigateTableRow(target);
                        break;
                    case "open_asset":
                        NavigateAsset(target);
                        break;
                    case "open_file":
                        if (!string.IsNullOrEmpty(target.FilePath))
                        {
                            InternalEditorUtility.OpenFileAtLineExternal(target.FilePath, target.Line);
                        }
                        break;
                }
            }

            private static void NavigateTableRow(NavigationTarget target)
            {
                var so = FindTableByFile(target.TableName);
                if (so == null)
                {
                    Debug.LogWarning($"[MobaExplainNavigator] Unknown config table '{target.TableName}'.");
                    return;
                }

                Selection.activeObject = so;
                EditorGUIUtility.PingObject(so);
            }

            private static void NavigateAsset(NavigationTarget target)
            {
                if (string.IsNullOrEmpty(target.AssetGuid)) return;
                var path = AssetDatabase.GUIDToAssetPath(target.AssetGuid);
                if (string.IsNullOrEmpty(path)) return;
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (obj == null) return;
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }

        // ---- Context Editor（强类型修饰输入）----

        private sealed class MobaModifierContextEditor : IExplainContextEditorProvider
        {
            public int Priority => 0;

            public bool CanEdit(in PipelineItemKey key) => key.Type == "skill";

            public string GetButtonText(in PipelineItemKey key) => "强化/构筑";

            public string GetWindowTitle(in PipelineItemKey key) => "技能修饰预览";

            public VisualElement BuildEditor(ExplainContextEditorContext context)
            {
                var root = new VisualElement();
                root.Add(new Label("选择要预览的修饰（作用到技能冷却）："));

                AddModifierToggle(root, context, "冷却 +500ms", "add", 500f);
                AddModifierToggle(root, context, "冷却 -20%", "percent_add", -0.2f);
                AddModifierToggle(root, context, "冷却 ×0.5", "mul", 0.5f);
                AddModifierToggle(root, context, "冷却固定 1000ms", "override", 1000f);

                return root;
            }

            private static void AddModifierToggle(VisualElement root, ExplainContextEditorContext context, string label, string op, float value)
            {
                var toggle = new Toggle(label) { value = Contains(context.ResolveContext.Modifiers, op, value) };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    var modifiers = context.ResolveContext.Modifiers;
                    if (evt.newValue)
                    {
                        if (modifiers == null)
                        {
                            modifiers = new List<ModifierPreviewInput>();
                            context.ResolveContext.Modifiers = modifiers;
                        }

                        if (!Contains(modifiers, op, value))
                        {
                            modifiers.Add(new ModifierPreviewInput { Op = op, Value = value, Label = label });
                        }
                    }
                    else if (modifiers != null)
                    {
                        modifiers.RemoveAll(m => m != null && m.Op == op && Mathf.Approximately(m.Value, value));
                    }

                    context.RequestResolve();
                });
                root.Add(toggle);
            }

            private static bool Contains(List<ModifierPreviewInput> modifiers, string op, float value)
            {
                if (modifiers == null) return false;
                for (var i = 0; i < modifiers.Count; i++)
                {
                    var m = modifiers[i];
                    if (m != null && m.Op == op && Mathf.Approximately(m.Value, value)) return true;
                }

                return false;
            }
        }
    }
}
