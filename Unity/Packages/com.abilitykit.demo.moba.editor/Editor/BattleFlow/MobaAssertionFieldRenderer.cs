#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BattleFlow;
using AbilityKit.BattleFlow.Editor;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Demo.Moba.Services;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow
{
    /// <summary>
    /// MOBA 断言积木的字段渲染器：把框架反射兜底（裸 string/int 输入）替换成下拉框 + 必填提示，
    /// 避免策划/测试手写 trace kind / comparator 拼错。
    /// </summary>
    [InitializeOnLoad]
    public sealed class MobaAssertionFieldRenderer : IContextualBattleBlockFieldRenderer
    {
        private static readonly string[] TraceKinds = Enum.GetNames(typeof(MobaTraceKind))
            .Where(k => k != "None")
            .ToArray();

        private static readonly string[] Comparators = { "eq", "ne", "gt", "gte", "lt", "lte", "contains" };

        private static readonly string[] StateProperties =
            { "hp", "mana", "maxhp", "maxmana", "position", "teamid", "buffcount", "exists" };

        static MobaAssertionFieldRenderer()
        {
            BattleBlockFieldRendererRegistry.Renderer = new MobaAssertionFieldRenderer();
        }

        public bool TryDrawFields(BattleBlock block)
        {
            return TryDrawFields(block, null);
        }

        public bool TryDrawFields(BattleBlock block, BattleBlockFieldContext context)
        {
            switch (block)
            {
                case MobaCompleteSkillTestBlock b: DrawCompleteSkillTest(b); return true;
                case DuelSetupBlock b: DrawDuelSetup(b); return true;
                case CastSkillBlock b: DrawCastSkill(b, context); return true;
                case MobaSkillOutcomeTestBlock b: DrawSkillOutcome(b, context); return true;
                case MobaSkillDamageTestBlock b: DrawSkillDamage(b, context); return true;
                case AssertTraceBlock b: DrawAssertTrace(b); return true;
                case AssertNoTraceBlock b: DrawAssertNoTrace(b); return true;
                case AssertStateBlock b: DrawAssertState(b); return true;
                case AssertContextBlock b: DrawAssertContext(b); return true;
                case AssertRelationshipBlock b: DrawAssertRelationship(b); return true;
                default: return false;
            }
        }

        private static void DrawCompleteSkillTest(MobaCompleteSkillTestBlock b)
        {
            EditorGUILayout.LabelField("对战配置", EditorStyles.boldLabel);
            b.EnvironmentProfileId = EditorGUILayout.TextField("Environment", b.EnvironmentProfileId);
            b.CasterHeroId = DrawConfigId(
                "Caster Hero",
                b.CasterHeroId,
                MobaBattleFlowConfigCatalog.Heroes,
                "选择施法者英雄",
                entry =>
                {
                    b.CasterHeroId = entry.Id;
                    if (entry.AttributeTemplateId > 0)
                        b.CasterAttributeTemplateId = entry.AttributeTemplateId;
                });
            b.CasterAttributeTemplateId = DrawConfigId(
                "Caster Attributes",
                b.CasterAttributeTemplateId,
                MobaBattleFlowConfigCatalog.AttributeTemplates,
                "选择施法者属性模板",
                entry => b.CasterAttributeTemplateId = entry.Id);
            b.TargetHeroId = DrawConfigId(
                "Target Hero",
                b.TargetHeroId,
                MobaBattleFlowConfigCatalog.Heroes,
                "选择目标英雄",
                entry =>
                {
                    b.TargetHeroId = entry.Id;
                    if (entry.AttributeTemplateId > 0)
                        b.TargetAttributeTemplateId = entry.AttributeTemplateId;
                });
            b.TargetAttributeTemplateId = DrawConfigId(
                "Target Attributes",
                b.TargetAttributeTemplateId,
                MobaBattleFlowConfigCatalog.AttributeTemplates,
                "选择目标属性模板",
                entry => b.TargetAttributeTemplateId = entry.Id);
            b.TargetDistance = EditorGUILayout.FloatField("Distance", b.TargetDistance);

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("技能与验收", EditorStyles.boldLabel);
            DrawRecipeSkill(b);
            b.AtMs = EditorGUILayout.IntField("AtMs", b.AtMs);
            b.Outcome = (MobaSkillOutcomeKind)EditorGUILayout.EnumPopup("Outcome", b.Outcome);
            if (b.Outcome == MobaSkillOutcomeKind.State)
            {
                b.StateProperty = DrawStringPopup("Property", b.StateProperty, StateProperties);
                b.Comparator = DrawStringPopup("Comparator", b.Comparator, Comparators);
                b.ExpectedValue = DrawRequiredText("Expected Value", b.ExpectedValue);
            }
            else
            {
                b.TraceKind = DrawStringPopup("Trace Kind", b.TraceKind, TraceKinds);
                b.TraceConfigId = DrawTraceConfigId(
                    "ConfigId",
                    b.TraceConfigId,
                    b.TraceKind,
                    b.SkillId,
                    value => b.TraceConfigId = value);
                DrawCompatibilityWarning(
                    MobaBattleFlowConfigCatalog.FindSkill(b.SkillId),
                    b.TraceKind,
                    b.TraceConfigId);
                if (b.Outcome == MobaSkillOutcomeKind.TraceOccurs)
                    b.MinCount = EditorGUILayout.IntField("MinCount", b.MinCount);
            }

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("执行设置", EditorStyles.boldLabel);
            b.TickRate = EditorGUILayout.IntField("TickRate", b.TickRate);
            b.MaxDurationMs = EditorGUILayout.IntField("MaxDurationMs", b.MaxDurationMs);
            b.SettleDurationMs = EditorGUILayout.IntField("SettleDurationMs", b.SettleDurationMs);
        }

        private static void DrawRecipeSkill(MobaCompleteSkillTestBlock b)
        {
            var entries = MobaBattleFlowConfigCatalog.SkillChoices(
                b.CasterHeroId,
                b.CasterAttributeTemplateId);
            var selected = entries.FirstOrDefault(entry => entry.Id == b.SkillId);
            var buttonLabel = selected == null ? "选择技能配置" : selected.Label;

            EditorGUILayout.BeginHorizontal();
            b.SkillId = EditorGUILayout.IntField("Skill Id", b.SkillId);
            if (GUILayout.Button(
                    new GUIContent(buttonLabel, "搜索当前英雄的技能并写入技能 ID 与槽位"),
                    EditorStyles.popup,
                    GUILayout.Width(240f)))
            {
                var rect = GUILayoutUtility.GetLastRect();
                new MobaBattleFlowConfigDropdown(
                    new UnityEditor.IMGUI.Controls.AdvancedDropdownState(),
                    "选择配方技能",
                    entries,
                    entry =>
                    {
                        b.SkillId = entry.Id;
                        b.Slot = entry.Slot;
                        if (b.TraceConfigId <= 0)
                            b.TraceConfigId = MobaBattleFlowConfigCatalog.PreferredTraceConfigId(
                                b.TraceKind,
                                entry.Id);
                    }).Show(rect);
            }
            DrawRefreshButton();
            EditorGUILayout.EndHorizontal();
            b.Slot = EditorGUILayout.IntField("Skill Slot", b.Slot);

            if (b.SkillId > 0 && selected == null)
            {
                EditorGUILayout.HelpBox(
                    $"技能 {b.SkillId} 未在当前英雄/属性模板的 ActiveSkills 中找到；外部 ID 可保留。",
                    MessageType.Warning);
            }
            else if (selected != null && selected.Slot != b.Slot)
            {
                EditorGUILayout.HelpBox(
                    $"技能 {b.SkillId} 配置在槽位 {selected.Slot}，当前填写的是槽位 {b.Slot}。",
                    MessageType.Warning);
            }
        }

        private static void DrawDuelSetup(DuelSetupBlock b)
        {
            b.EnvironmentProfileId = EditorGUILayout.TextField("Environment", b.EnvironmentProfileId);
            b.CasterAlias = EditorGUILayout.TextField("Caster Alias", b.CasterAlias);
            b.CasterHeroId = DrawConfigId(
                "Caster Hero",
                b.CasterHeroId,
                MobaBattleFlowConfigCatalog.Heroes,
                "选择施法者英雄",
                entry =>
                {
                    b.CasterHeroId = entry.Id;
                    if (entry.AttributeTemplateId > 0)
                        b.CasterAttributeTemplateId = entry.AttributeTemplateId;
                });
            b.CasterAttributeTemplateId = DrawConfigId(
                "Caster Attributes",
                b.CasterAttributeTemplateId,
                MobaBattleFlowConfigCatalog.AttributeTemplates,
                "选择施法者属性模板",
                entry => b.CasterAttributeTemplateId = entry.Id);

            b.TargetAlias = EditorGUILayout.TextField("Target Alias", b.TargetAlias);
            b.TargetHeroId = DrawConfigId(
                "Target Hero",
                b.TargetHeroId,
                MobaBattleFlowConfigCatalog.Heroes,
                "选择目标英雄",
                entry =>
                {
                    b.TargetHeroId = entry.Id;
                    if (entry.AttributeTemplateId > 0)
                        b.TargetAttributeTemplateId = entry.AttributeTemplateId;
                });
            b.TargetAttributeTemplateId = DrawConfigId(
                "Target Attributes",
                b.TargetAttributeTemplateId,
                MobaBattleFlowConfigCatalog.AttributeTemplates,
                "选择目标属性模板",
                entry => b.TargetAttributeTemplateId = entry.Id);
            b.TargetDistance = EditorGUILayout.FloatField("Distance", b.TargetDistance);
        }

        private static void DrawCastSkill(CastSkillBlock b, BattleBlockFieldContext context)
        {
            b.CasterAlias = DrawRequiredText("Caster", b.CasterAlias);
            b.TargetAlias = DrawRequiredText("Target", b.TargetAlias);
            b.Slot = DrawSkillSlot("Skill", b.Slot, b.CasterAlias, context, entry => b.Slot = entry.Slot);
            b.AtMs = EditorGUILayout.IntField("AtMs", b.AtMs);
        }

        private static void DrawSkillOutcome(MobaSkillOutcomeTestBlock b, BattleBlockFieldContext context)
        {
            b.CasterAlias = DrawRequiredText("Caster", b.CasterAlias);
            b.TargetAlias = DrawRequiredText("Target", b.TargetAlias);
            b.Slot = DrawSkillSlot(
                "Skill",
                b.Slot,
                b.CasterAlias,
                context,
                entry =>
                {
                    b.Slot = entry.Slot;
                    if (b.TraceConfigId <= 0)
                        b.TraceConfigId = MobaBattleFlowConfigCatalog.PreferredTraceConfigId(b.TraceKind, entry.Id);
                });
            b.AtMs = EditorGUILayout.IntField("AtMs", b.AtMs);
            b.Outcome = (MobaSkillOutcomeKind)EditorGUILayout.EnumPopup("Outcome", b.Outcome);
            if (b.Outcome == MobaSkillOutcomeKind.State)
            {
                b.StateAlias = DrawRequiredText("State Alias", b.StateAlias);
                b.StateProperty = DrawStringPopup("Property", b.StateProperty, StateProperties);
                b.Comparator = DrawStringPopup("Comparator", b.Comparator, Comparators);
                b.ExpectedValue = DrawRequiredText("Expected Value", b.ExpectedValue);
                return;
            }

            b.TraceKind = DrawStringPopup("Trace Kind", b.TraceKind, TraceKinds);
            var selectedSkill = ResolveSelectedSkill(b.CasterAlias, b.Slot, context);
            b.TraceConfigId = DrawTraceConfigId(
                "ConfigId",
                b.TraceConfigId,
                b.TraceKind,
                selectedSkill?.Id ?? 0,
                value => b.TraceConfigId = value);
            DrawCompatibilityWarning(selectedSkill, b.TraceKind, b.TraceConfigId);
            if (b.Outcome == MobaSkillOutcomeKind.TraceOccurs)
                b.MinCount = EditorGUILayout.IntField("MinCount", b.MinCount);
        }

        private static void DrawSkillDamage(MobaSkillDamageTestBlock b, BattleBlockFieldContext context)
        {
            b.CasterAlias = DrawRequiredText("Caster", b.CasterAlias);
            b.TargetAlias = DrawRequiredText("Target", b.TargetAlias);
            b.Slot = DrawSkillSlot(
                "Skill",
                b.Slot,
                b.CasterAlias,
                context,
                entry =>
                {
                    b.Slot = entry.Slot;
                    if (b.DamageConfigId <= 0)
                        b.DamageConfigId = MobaBattleFlowConfigCatalog.PreferredTraceConfigId(
                            "DamageApply",
                            entry.Id);
                });
            b.AtMs = EditorGUILayout.IntField("AtMs", b.AtMs);
            var selectedSkill = ResolveSelectedSkill(b.CasterAlias, b.Slot, context);
            b.DamageConfigId = DrawConfigId(
                "Damage Config",
                b.DamageConfigId,
                selectedSkill == null
                    ? MobaBattleFlowConfigCatalog.Effects
                    : MobaBattleFlowConfigCatalog.TraceConfigChoices("DamageApply", selectedSkill.Id),
                "选择伤害/效果配置",
                entry => b.DamageConfigId = entry.Id);
            DrawCompatibilityWarning(selectedSkill, "DamageApply", b.DamageConfigId);
            b.MinCount = EditorGUILayout.IntField("MinCount", b.MinCount);
        }

        private static void DrawAssertTrace(AssertTraceBlock b)
        {
            b.Kind = DrawKind(b.Kind, required: true);
            b.ConfigId = DrawTraceConfigId("ConfigId", b.ConfigId, b.Kind, 0, value => b.ConfigId = value);
            b.MinCount = EditorGUILayout.IntField("MinCount", b.MinCount);
            b.MaxCount = EditorGUILayout.IntField("MaxCount", b.MaxCount);
            b.UnderEffectId = DrawConfigId(
                "UnderEffectId",
                b.UnderEffectId,
                MobaBattleFlowConfigCatalog.Effects,
                "选择父效果配置",
                entry => b.UnderEffectId = entry.Id);
        }

        private static void DrawAssertNoTrace(AssertNoTraceBlock b)
        {
            b.Kind = DrawKind(b.Kind, required: true);
            b.ConfigId = DrawTraceConfigId("ConfigId", b.ConfigId, b.Kind, 0, value => b.ConfigId = value);
            b.UnderEffectId = DrawConfigId(
                "UnderEffectId",
                b.UnderEffectId,
                MobaBattleFlowConfigCatalog.Effects,
                "选择父效果配置",
                entry => b.UnderEffectId = entry.Id);
        }

        private static void DrawAssertState(AssertStateBlock b)
        {
            b.Alias = DrawRequiredText("Alias", b.Alias);
            b.Property = DrawStringPopup("Property", b.Property, StateProperties);
            b.Comparator = DrawStringPopup("Comparator", b.Comparator, Comparators);
            b.ExpectedValue = EditorGUILayout.TextField("ExpectedValue", b.ExpectedValue);
        }

        private static void DrawAssertContext(AssertContextBlock b)
        {
            b.Alias = DrawRequiredText("Alias", b.Alias);
            b.Kind = DrawKind(b.Kind, required: false);
            b.Property = DrawStringPopup("Property", b.Property, StateProperties);
            b.Comparator = DrawStringPopup("Comparator", b.Comparator, Comparators);
            b.ExpectedValue = EditorGUILayout.TextField("ExpectedValue", b.ExpectedValue);
        }

        private static void DrawAssertRelationship(AssertRelationshipBlock b)
        {
            b.ParentKind = DrawKind(b.ParentKind, required: true);
            b.ParentConfigId = DrawTraceConfigId(
                "ParentConfigId",
                b.ParentConfigId,
                b.ParentKind,
                0,
                value => b.ParentConfigId = value);
            b.ChildKind = DrawKind(b.ChildKind, required: true);
            b.ChildConfigId = DrawTraceConfigId(
                "ChildConfigId",
                b.ChildConfigId,
                b.ChildKind,
                0,
                value => b.ChildConfigId = value);
        }

        private static int DrawTraceConfigId(
            string label,
            int current,
            string traceKind,
            int skillId,
            Action<int> onSelected)
        {
            return DrawConfigId(
                label,
                current,
                MobaBattleFlowConfigCatalog.TraceConfigChoices(traceKind, skillId),
                "选择事件配置",
                entry => onSelected(entry.Id));
        }

        private static int DrawSkillSlot(
            string label,
            int current,
            string casterAlias,
            BattleBlockFieldContext context,
            Action<MobaBattleFlowConfigEntry> onSelected)
        {
            ResolveActorConfig(context, casterAlias, out var heroId, out var attributeTemplateId);
            var entries = MobaBattleFlowConfigCatalog.SkillChoices(heroId, attributeTemplateId);
            var selected = MobaBattleFlowConfigCatalog.FindSkillChoice(heroId, attributeTemplateId, current);
            var buttonLabel = selected == null ? "选择技能配置" : selected.Label;

            EditorGUILayout.BeginHorizontal();
            var next = EditorGUILayout.IntField(label + " Slot", current);
            if (GUILayout.Button(new GUIContent(buttonLabel, "搜索技能配置并写入对应槽位"), EditorStyles.popup, GUILayout.Width(240f)))
            {
                var rect = GUILayoutUtility.GetLastRect();
                new MobaBattleFlowConfigDropdown(
                    new UnityEditor.IMGUI.Controls.AdvancedDropdownState(),
                    "选择技能",
                    entries,
                    onSelected).Show(rect);
            }
            DrawRefreshButton();
            EditorGUILayout.EndHorizontal();
            if ((heroId > 0 || attributeTemplateId > 0) && current > 0 && selected == null)
            {
                EditorGUILayout.HelpBox(
                    $"槽位 {current} 未在当前英雄/属性模板的 ActiveSkills 中找到。",
                    MessageType.Warning);
            }
            return next;
        }

        private static MobaBattleFlowConfigEntry ResolveSelectedSkill(
            string casterAlias,
            int slot,
            BattleBlockFieldContext context)
        {
            ResolveActorConfig(context, casterAlias, out var heroId, out var attributeTemplateId);
            return MobaBattleFlowConfigCatalog.FindSkillChoice(heroId, attributeTemplateId, slot);
        }

        private static void DrawCompatibilityWarning(
            MobaBattleFlowConfigEntry skill,
            string traceKind,
            int configId)
        {
            if (skill == null || configId <= 0 ||
                MobaBattleFlowConfigCatalog.IsTraceConfigCompatible(traceKind, skill.Id, configId)) return;

            var flowIds = MobaBattleFlowConfigCatalog.SkillFlowIds(skill.Id);
            var flowText = flowIds.Count == 0 ? "未找到 SkillFlow" : "SkillFlow " + string.Join(", ", flowIds);
            EditorGUILayout.HelpBox(
                $"配置 {configId} 不在技能 {skill.Label} 的 {flowText} 可达范围内；外部或运行时派生 ID 可保留。",
                MessageType.Warning);
        }

        private static int DrawConfigId(
            string label,
            int current,
            IReadOnlyList<MobaBattleFlowConfigEntry> entries,
            string pickerTitle,
            Action<MobaBattleFlowConfigEntry> onSelected)
        {
            var selected = entries.FirstOrDefault(entry => entry.Id == current);
            var buttonLabel = selected != null
                ? selected.Label
                : current > 0 ? $"未收录 [{current}]" : "选择配置";

            EditorGUILayout.BeginHorizontal();
            var next = EditorGUILayout.IntField(label, current);
            if (GUILayout.Button(
                    new GUIContent(buttonLabel, "可搜索选择；左侧数值可继续手动输入外部 ID"),
                    EditorStyles.popup,
                    GUILayout.Width(220f)))
            {
                var rect = GUILayoutUtility.GetLastRect();
                new MobaBattleFlowConfigDropdown(
                    new UnityEditor.IMGUI.Controls.AdvancedDropdownState(),
                    pickerTitle,
                    entries,
                    onSelected).Show(rect);
            }
            DrawRefreshButton();
            EditorGUILayout.EndHorizontal();
            return next;
        }

        private static void DrawRefreshButton()
        {
            var content = EditorGUIUtility.IconContent("Refresh");
            content.tooltip = "刷新 MOBA 配置目录";
            if (GUILayout.Button(content, GUILayout.Width(24f), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                MobaBattleFlowConfigCatalog.Refresh();
        }

        private static void ResolveActorConfig(
            BattleBlockFieldContext context,
            string alias,
            out int heroId,
            out int attributeTemplateId)
        {
            heroId = 0;
            attributeTemplateId = 0;
            if (context == null || string.IsNullOrWhiteSpace(alias)) return;

            foreach (var block in EnumerateBlocks(context.Authoring))
            {
                if (!(block is DuelSetupBlock duel)) continue;
                if (string.Equals(duel.CasterAlias, alias, StringComparison.Ordinal))
                {
                    heroId = duel.CasterHeroId;
                    attributeTemplateId = duel.CasterAttributeTemplateId;
                    return;
                }
                if (string.Equals(duel.TargetAlias, alias, StringComparison.Ordinal))
                {
                    heroId = duel.TargetHeroId;
                    attributeTemplateId = duel.TargetAttributeTemplateId;
                    return;
                }
            }

            foreach (var block in context.Setup)
            {
                if (!(block is SpawnActorBlock spawn) ||
                    !string.Equals(spawn.Alias, alias, StringComparison.Ordinal)) continue;
                heroId = spawn.HeroId;
                attributeTemplateId = spawn.AttributeTemplateId;
                return;
            }
        }

        private static IEnumerable<BattleBlock> EnumerateBlocks(IEnumerable<BattleBlock> blocks)
        {
            foreach (var block in blocks)
            {
                if (block == null) continue;
                yield return block;
                if (!(block is BattleCompositeBlock composite)) continue;
                foreach (var child in EnumerateBlocks(composite.Children)) yield return child;
            }
        }

        /// <summary>trace kind 下拉框；required 时空值标红。</summary>
        private static string DrawKind(string current, bool required)
        {
            var value = DrawStringPopup("Kind", current, TraceKinds);
            if (required && string.IsNullOrEmpty(value))
                EditorGUILayout.HelpBox("Kind 必填", MessageType.Warning);
            return value;
        }

        private static string DrawRequiredText(string label, string current)
        {
            var value = EditorGUILayout.TextField(label, current);
            if (string.IsNullOrEmpty(value))
                EditorGUILayout.HelpBox(label + " 必填", MessageType.Warning);
            return value;
        }

        private static string DrawStringPopup(string label, string current, string[] options)
        {
            var list = new List<string>(options);
            if (!string.IsNullOrEmpty(current) && !list.Contains(current))
                list.Insert(0, current);
            var index = Math.Max(0, list.IndexOf(current ?? string.Empty));
            index = EditorGUILayout.Popup(label, index, list.ToArray());
            return list[index];
        }
    }
}
#endif
