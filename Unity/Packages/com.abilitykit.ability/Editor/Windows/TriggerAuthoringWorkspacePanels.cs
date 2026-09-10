#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Inspectors;
using AbilityKit.Ability.Editor.Packages;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.UI;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Windows
{
    internal static class TriggerAuthoringWorkspaceLayout
    {
        internal const float StandardWidthThreshold = 820f;
        internal const float SplitterWidth = 5f;
        internal const float MinimumTriggerListWidth = 260f;
        internal const float MaximumTriggerListWidth = 420f;
        internal const float MinimumNodeOutlineWidth = 220f;
        internal const float MaximumNodeOutlineWidth = 380f;
        internal const float NodeWorkspaceSplitThreshold = 600f;
        internal const float RuleOverviewSplitThreshold = 620f;

        internal static float ClampNavigationWidth(float width, float windowWidth)
        {
            var maximum = Mathf.Max(210f, Mathf.Min(380f, windowWidth * 0.34f));
            return Mathf.Clamp(width, 210f, maximum);
        }

        internal static float ClampTriggerListWidth(float width, float availableWidth)
        {
            var maximum = Mathf.Max(
                MinimumTriggerListWidth,
                Mathf.Min(MaximumTriggerListWidth, availableWidth - 400f));
            return Mathf.Clamp(width, MinimumTriggerListWidth, maximum);
        }

        internal static bool ShouldSplitNodeWorkspace(float availableWidth)
        {
            return availableWidth >= NodeWorkspaceSplitThreshold;
        }

        internal static bool ShouldSplitRuleOverview(float availableWidth)
        {
            return availableWidth >= RuleOverviewSplitThreshold;
        }

        internal static float ClampNodeOutlineWidth(float width, float availableWidth)
        {
            var maximum = Mathf.Max(
                MinimumNodeOutlineWidth,
                Mathf.Min(MaximumNodeOutlineWidth, availableWidth - 300f));
            return Mathf.Clamp(width, MinimumNodeOutlineWidth, maximum);
        }
    }

    internal sealed class TriggerAuthoringRuleOverviewItem
    {
        internal int Index;
        internal TriggerDefinitionData Trigger;
        internal string ConditionSummary;
        internal string ConditionTooltip;
        internal string ActionSummary;
        internal string ActionTooltip;
        internal string SourceLabel;
        internal bool HasResolutionError;
    }

    internal sealed class TriggerAuthoringRuleOverviewResult
    {
        internal string GroupKey = string.Empty;
        internal string GroupLabel = string.Empty;
        internal readonly List<TriggerAuthoringRuleOverviewItem> Rules =
            new List<TriggerAuthoringRuleOverviewItem>();
    }

    internal static class TriggerAuthoringRuleOverviewBuilder
    {
        internal static List<TriggerAuthoringRuleOverviewItem> Build(
            TriggerAuthoringModuleData module,
            string eventId,
            TriggerTypeDescriptorCatalog types,
            TriggerTemplateDescriptorCatalog templates)
        {
            var result = new List<TriggerAuthoringRuleOverviewItem>();
            if (module?.Triggers == null || string.IsNullOrWhiteSpace(eventId)) return result;

            for (var i = 0; i < module.Triggers.Count; i++)
            {
                var trigger = module.Triggers[i];
                var effectiveTrigger = TriggerAuthoringTemplateDefinition.ResolveEffectiveView(trigger, templates);
                if (effectiveTrigger == null || !string.Equals(effectiveTrigger.Event, eventId, StringComparison.Ordinal)) continue;
                result.Add(BuildItem(module, i, trigger, types, templates));
            }
            return result;
        }

        internal static TriggerAuthoringRuleOverviewResult BuildForGroup(
            TriggerAuthoringModuleData module,
            int selectedTriggerIndex,
            TriggerAuthoringTriggerGroupMode groupMode,
            string preferredGroupKey,
            IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics,
            TriggerEventDescriptorCatalog events,
            TriggerTypeDescriptorCatalog types,
            TriggerTemplateDescriptorCatalog templates)
        {
            var result = new TriggerAuthoringRuleOverviewResult();
            if (module?.Triggers == null ||
                selectedTriggerIndex < 0 ||
                selectedTriggerIndex >= module.Triggers.Count)
                return result;

            var groups = TriggerAuthoringTriggerIndex.Build(
                module.Triggers,
                diagnostics,
                events,
                groupMode,
                string.Empty,
                TriggerAuthoringTriggerQuickFilter.All,
                templates);
            TriggerAuthoringTriggerIndex.Group selectedGroup = null;
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null ||
                    !string.Equals(group.Key, preferredGroupKey, StringComparison.Ordinal) ||
                    !ContainsTrigger(group, selectedTriggerIndex))
                    continue;
                selectedGroup = group;
                break;
            }
            if (selectedGroup == null)
            {
                for (var i = 0; i < groups.Count; i++)
                {
                    if (!ContainsTrigger(groups[i], selectedTriggerIndex)) continue;
                    selectedGroup = groups[i];
                    break;
                }
            }
            if (selectedGroup == null) return result;

            result.GroupKey = selectedGroup.Key;
            result.GroupLabel = selectedGroup.Label;
            for (var i = 0; i < selectedGroup.Entries.Count; i++)
            {
                var entry = selectedGroup.Entries[i];
                if (entry.Trigger == null) continue;
                result.Rules.Add(BuildItem(module, entry.Index, entry.Trigger, types, templates));
            }
            return result;
        }

        private static bool ContainsTrigger(TriggerAuthoringTriggerIndex.Group group, int triggerIndex)
        {
            if (group == null) return false;
            for (var i = 0; i < group.Entries.Count; i++)
                if (group.Entries[i].Index == triggerIndex)
                    return true;
            return false;
        }

        private static TriggerAuthoringRuleOverviewItem BuildItem(
            TriggerAuthoringModuleData module,
            int index,
            TriggerDefinitionData trigger,
            TriggerTypeDescriptorCatalog types,
            TriggerTemplateDescriptorCatalog templates)
        {
            var condition = trigger.Condition;
            var actions = trigger.Actions;
            TriggerAuthoringTemplateData template = null;
            var sourceLabel = "本地";
            var templateMissing = false;
            if (trigger.Template != null)
            {
                sourceLabel = "模板";
                if (templates != null &&
                    templates.TryGet(trigger.Template.TemplateId, out var templateAsset) &&
                    templateAsset?.Template != null)
                {
                    template = templateAsset.Template;
                    var definition = TriggerAuthoringTemplateDefinition.Get(template);
                    condition = definition.Condition;
                    actions = definition.Actions;
                }
                else
                {
                    templateMissing = true;
                }
            }

            var conditionSummary = templateMissing
                ? LogicSummary.Error("模板无法解析")
                : SummarizeLogic(module, condition, TriggerNodeKind.Condition, trigger, template, types);
            var actionSummary = templateMissing
                ? LogicSummary.Error("模板无法解析")
                : SummarizeLogic(module, actions, TriggerNodeKind.Action, trigger, template, types);
            return new TriggerAuthoringRuleOverviewItem
            {
                Index = index,
                Trigger = trigger,
                ConditionSummary = conditionSummary.Text,
                ConditionTooltip = conditionSummary.Tooltip,
                ActionSummary = actionSummary.Text,
                ActionTooltip = actionSummary.Tooltip,
                SourceLabel = sourceLabel,
                HasResolutionError = conditionSummary.HasError || actionSummary.HasError
            };
        }

        private static LogicSummary SummarizeLogic(
            TriggerAuthoringModuleData module,
            TriggerNodeData root,
            TriggerNodeKind kind,
            TriggerDefinitionData trigger,
            TriggerAuthoringTemplateData template,
            TriggerTypeDescriptorCatalog types)
        {
            if (root == null)
                return new LogicSummary(
                    kind == TriggerNodeKind.Condition ? "无条件" : "未配置行为",
                    kind == TriggerNodeKind.Condition ? "此规则没有条件，将直接执行行为。" : "此规则尚未配置行为。",
                    kind == TriggerNodeKind.Action);
            if (!root.Enabled) return new LogicSummary("已停用", "根节点已停用。", false);
            if (!TriggerAuthoringGroupResolver.TryExpand(module, root, kind, out var expanded, out var failure))
                return LogicSummary.Error(failure != null ? failure.Message : "逻辑无法解析");

            var leaves = new List<string>();
            CollectLeafSummaries(module, expanded, trigger, template, types, leaves);
            if (leaves.Count == 0)
            {
                var empty = kind == TriggerNodeKind.Condition ? "无有效条件" : "无有效行为";
                return new LogicSummary(empty, empty, kind == TriggerNodeKind.Action);
            }

            var rootName = GetNodeName(expanded, types);
            var separator = kind == TriggerNodeKind.Condition ? "、" : " → ";
            var summary = string.Join(separator, leaves);
            if (kind == TriggerNodeKind.Condition && expanded.Children != null && expanded.Children.Count > 0)
                summary = rootName + "：" + summary;
            else if (kind == TriggerNodeKind.Action &&
                     expanded.Children != null && expanded.Children.Count > 0 &&
                     !string.Equals(expanded.Type, "seq", StringComparison.OrdinalIgnoreCase))
                summary = rootName + "：" + summary;

            return new LogicSummary(summary, summary, false);
        }

        private static void CollectLeafSummaries(
            TriggerAuthoringModuleData module,
            TriggerNodeData node,
            TriggerDefinitionData trigger,
            TriggerAuthoringTemplateData template,
            TriggerTypeDescriptorCatalog types,
            ICollection<string> output)
        {
            if (node == null || !node.Enabled) return;
            if (node.Kind == TriggerNodeKind.Action &&
                string.Equals(node.Type, "conditional", StringComparison.OrdinalIgnoreCase))
            {
                output.Add(SummarizeConditionalChain(module, node, trigger, template, types));
                return;
            }
            var children = node.Children;
            if (children != null && children.Count > 0)
            {
                for (var i = 0; i < children.Count; i++)
                    CollectLeafSummaries(module, children[i], trigger, template, types, output);
                return;
            }

            var title = GetNodeName(node, types);
            var arguments = node.Arguments;
            if (arguments != null && arguments.Count > 0)
            {
                var parts = new List<string>();
                for (var i = 0; i < arguments.Count; i++)
                {
                    var argument = arguments[i];
                    if (argument == null || string.IsNullOrWhiteSpace(argument.Name)) continue;
                    parts.Add(
                        TriggerAuthoringEditorLabels.Parameter(argument.Name) + "=" +
                        SummarizeValue(ResolveTemplateValue(argument.Value, trigger, template)));
                }
                if (parts.Count > 0) title += "（" + string.Join("，", parts) + "）";
            }
            output.Add(title);
        }

        private static string SummarizeConditionalChain(
            TriggerAuthoringModuleData module,
            TriggerNodeData root,
            TriggerDefinitionData trigger,
            TriggerAuthoringTemplateData template,
            TriggerTypeDescriptorCatalog types)
        {
            var parts = new List<string>();
            var visited = new HashSet<TriggerNodeData>();
            var current = root;
            var branchIndex = 0;
            while (current != null && visited.Add(current))
            {
                var condition = current.Condition == null
                    ? "未配置判断条件"
                    : SummarizeLogic(
                        module,
                        current.Condition,
                        TriggerNodeKind.Condition,
                        trigger,
                        template,
                        types).Text;
                var actions = SummarizeActionBranch(
                    module,
                    current.Children,
                    trigger,
                    template,
                    types,
                    "未配置成立行为");
                parts.Add((branchIndex == 0 ? "如果[" : "否则如果[") + condition + "]：" + actions);
                branchIndex++;

                if (TriggerAuthoringConditionalChain.TryGetElseIf(current, out var next))
                {
                    current = next;
                    continue;
                }

                parts.Add("否则：" + SummarizeActionBranch(
                    module,
                    current.ElseChildren,
                    trigger,
                    template,
                    types,
                    "不执行其他行为"));
                break;
            }
            return string.Join("；", parts);
        }

        private static string SummarizeActionBranch(
            TriggerAuthoringModuleData module,
            IReadOnlyList<TriggerNodeData> nodes,
            TriggerDefinitionData trigger,
            TriggerAuthoringTemplateData template,
            TriggerTypeDescriptorCatalog types,
            string emptyText)
        {
            var summaries = new List<string>();
            if (nodes != null)
                for (var i = 0; i < nodes.Count; i++)
                    CollectLeafSummaries(module, nodes[i], trigger, template, types, summaries);
            return summaries.Count > 0 ? string.Join(" → ", summaries) : emptyText;
        }

        private static TriggerValueRefData ResolveTemplateValue(
            TriggerValueRefData value,
            TriggerDefinitionData trigger,
            TriggerAuthoringTemplateData template)
        {
            if (value == null || value.Source != TriggerValueSource.LocalBlackboard || template == null) return value;
            if (!TriggerAuthoringLocalBlackboardPath.TryParse(value.Path, out var scope, out var localKey) ||
                scope == TriggerAuthoringLocalBlackboardScope.Module) return value;
            var parameters = template.Parameters;
            TriggerAuthoringTemplateParameterData input = null;
            if (parameters != null)
                for (var i = 0; i < parameters.Count; i++)
                    if (parameters[i] != null && string.Equals(parameters[i].LocalVariableKey, localKey, StringComparison.Ordinal))
                    {
                        input = parameters[i];
                        break;
                    }
            if (input == null) return value;

            var parameterName = input.Name;
            var bindings = trigger?.Template?.Bindings;
            if (bindings != null)
            {
                for (var i = 0; i < bindings.Count; i++)
                    if (bindings[i] != null && string.Equals(bindings[i].Name, parameterName, StringComparison.Ordinal))
                        return bindings[i].Value;
            }

            if (parameters != null)
            {
                for (var i = 0; i < parameters.Count; i++)
                    if (parameters[i] != null &&
                        string.Equals(parameters[i].Name, parameterName, StringComparison.Ordinal) &&
                        parameters[i].HasDefault)
                        return parameters[i].DefaultValue;
            }
            return value;
        }

        private static string GetNodeName(TriggerNodeData node, TriggerTypeDescriptorCatalog types)
        {
            TriggerTypeDescriptor descriptor = null;
            types?.TryGet(node.Kind, node.Type, out descriptor);
            return TriggerAuthoringEditorLabels.Node(node.Type, descriptor != null ? descriptor.DisplayName : null);
        }

        private static string SummarizeValue(TriggerValueRefData value)
        {
            if (value == null) return "未设置";
            if (value.Source == TriggerValueSource.Expression) return "表达式：" + value.Expression;
            if (value.Source != TriggerValueSource.Constant)
                return TriggerAuthoringEditorLabels.Source(value.Source) + "：" + value.Path;
            switch (value.Type)
            {
                case TriggerValueType.Integer:
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId: return value.IntegerValue.ToString();
                case TriggerValueType.Number: return value.NumberValue.ToString("G");
                case TriggerValueType.Boolean: return value.BooleanValue ? "是" : "否";
                case TriggerValueType.String: return value.StringValue ?? string.Empty;
                case TriggerValueType.IntegerList:
                    return value.IntegerListValue != null ? string.Join(",", value.IntegerListValue) : string.Empty;
                case TriggerValueType.Vector3:
                    return value.Vector3Value == null
                        ? "(0, 0, 0)"
                        : $"({value.Vector3Value.X:G}, {value.Vector3Value.Y:G}, {value.Vector3Value.Z:G})";
                default: return TriggerAuthoringEditorLabels.ValueType(value.Type);
            }
        }

        private readonly struct LogicSummary
        {
            internal LogicSummary(string text, string tooltip, bool hasError)
            {
                Text = text;
                Tooltip = tooltip;
                HasError = hasError;
            }

            internal string Text { get; }
            internal string Tooltip { get; }
            internal bool HasError { get; }

            internal static LogicSummary Error(string message)
            {
                return new LogicSummary("无法解析", message, true);
            }
        }
    }

    /// <summary>
    /// Ability-owned project/module navigation panel. It binds concrete Ability authoring assets;
    /// the generic search and layout primitives remain owned by Editor.Platform.
    /// </summary>
    internal sealed class TriggerAuthoringProjectTreePanel
    {
        private Vector2 _scroll;
        private string _search = string.Empty;
        private readonly HashSet<int> _collapsedProjects = new HashSet<int>();
        private readonly HashSet<string> _collapsedDomains = new HashSet<string>(StringComparer.Ordinal);

        internal void Draw(
            IReadOnlyList<TriggerAuthoringProjectAsset> projects,
            IReadOnlyList<TriggerAuthoringModuleAsset> unassignedModules,
            TriggerAuthoringModuleAsset selectedModule,
            Action<TriggerAuthoringModuleAsset> selectModule,
            Action<TriggerAuthoringProjectAsset, string> createPackage,
            Action showTemplates,
            float width)
        {
            if (projects == null) throw new ArgumentNullException(nameof(projects));
            if (unassignedModules == null) throw new ArgumentNullException(nameof(unassignedModules));
            if (selectModule == null) throw new ArgumentNullException(nameof(selectModule));
            if (createPackage == null) throw new ArgumentNullException(nameof(createPackage));
            if (showTemplates == null) throw new ArgumentNullException(nameof(showTemplates));

            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            if (GUILayout.Toolbar(0, new[] { "内容包", "函数库" }, EditorStyles.toolbarButton) == 1)
            {
                showTemplates();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(new GUIContent("内容包", "参与触发器构建的业务域和内容包。"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(CountModules(projects, unassignedModules).ToString(), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _search = EditorGUILayout.TextField(_search ?? string.Empty, EditorStyles.toolbarSearchField);
            if (GUILayout.Button(new GUIContent("×", "清除内容包搜索"), EditorStyles.toolbarButton, GUILayout.Width(22f)) &&
                !string.IsNullOrEmpty(_search))
            {
                _search = string.Empty;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var filter = (_search ?? string.Empty).Trim();
            var visibleModules = 0;
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null) continue;
                var modules = project.Modules;
                var projectMatches = Matches(project.name, filter);
                var groups = TriggerAuthoringPackageCatalog.Build(
                    modules,
                    package => projectMatches || MatchesModule(package, filter));
                var matchingModules = CountPackages(groups);
                if (!projectMatches && matchingModules == 0) continue;

                var projectKey = project.GetInstanceID();
                var expanded = !_collapsedProjects.Contains(projectKey) || filter.Length > 0;
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                var nextExpanded = EditorGUILayout.Foldout(
                    expanded,
                    new GUIContent(project.name, AssetDatabase.GetAssetPath(project)),
                    true,
                    EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                GUILayout.Label(matchingModules.ToString(), EditorStyles.miniLabel, GUILayout.Width(28f));
                if (GUILayout.Button(new GUIContent("+", "在此项目中创建内容包"), EditorStyles.toolbarButton, GUILayout.Width(22f)))
                    createPackage(project, null);
                EditorGUILayout.EndHorizontal();
                if (nextExpanded) _collapsedProjects.Remove(projectKey);
                else _collapsedProjects.Add(projectKey);
                if (!nextExpanded)
                {
                    visibleModules += matchingModules;
                    continue;
                }

                for (var g = 0; g < groups.Count; g++)
                    visibleModules += DrawDomainGroup(
                        project,
                        groups[g],
                        selectedModule,
                        selectModule,
                        createPackage,
                        filter.Length > 0);
            }

            if (unassignedModules.Count > 0)
            {
                GUILayout.Space(6f);
                EditorGUILayout.LabelField($"未归属项目（{unassignedModules.Count}）", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "这些内容包尚未注册到项目，因此不会参与构建校验和运行时导出。",
                    MessageType.Warning);
                for (var i = 0; i < unassignedModules.Count; i++)
                    if (DrawModuleRow(unassignedModules[i], selectedModule, filter, false, selectModule))
                        visibleModules++;
            }

            if (visibleModules == 0)
                EditorGUILayout.HelpBox("没有内容包匹配当前搜索条件。", MessageType.Info);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(filter.Length == 0 ? "全部内容包" : "匹配 " + visibleModules + " 个", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(projects.Count + " 个项目", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private int DrawDomainGroup(
            TriggerAuthoringProjectAsset project,
            TriggerAuthoringDomainGroup group,
            TriggerAuthoringModuleAsset selectedModule,
            Action<TriggerAuthoringModuleAsset> selectModule,
            Action<TriggerAuthoringProjectAsset, string> createPackage,
            bool searching)
        {
            var key = project.GetInstanceID() + ":" + group.DomainId;
            var expanded = searching || !_collapsedDomains.Contains(key);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(10f);
            var nextExpanded = EditorGUILayout.Foldout(
                expanded,
                new GUIContent(group.DisplayName, group.DomainId),
                true);
            GUILayout.FlexibleSpace();
            GUILayout.Label(group.Packages.Count + " 包 / " + group.TriggerCount + " 条", EditorStyles.miniLabel);
            if (GUILayout.Button(new GUIContent("+", "在此业务域创建内容包"), EditorStyles.toolbarButton, GUILayout.Width(22f)))
                createPackage(project, group.DomainId);
            EditorGUILayout.EndHorizontal();
            if (nextExpanded) _collapsedDomains.Remove(key);
            else _collapsedDomains.Add(key);
            if (!nextExpanded) return group.Packages.Count;

            var count = 0;
            for (var i = 0; i < group.Packages.Count; i++)
                if (DrawModuleRow(group.Packages[i], selectedModule, string.Empty, true, selectModule)) count++;
            return count;
        }

        private static bool DrawModuleRow(
            TriggerAuthoringModuleAsset module,
            TriggerAuthoringModuleAsset selectedModule,
            string filter,
            bool parentMatches,
            Action<TriggerAuthoringModuleAsset> selectModule)
        {
            if (module == null) return false;
            var moduleId = module.Module != null ? module.Module.ModuleId : null;
            var summary = string.IsNullOrWhiteSpace(moduleId) ? module.name : moduleId;
            var displayName = module.Module != null ? module.Module.DisplayName : string.Empty;
            var triggerCount = module.Module != null && module.Module.Triggers != null
                ? module.Module.Triggers.Count
                : 0;
            if (!parentMatches && !MatchesModule(module, filter)) return false;

            var oldBackground = GUI.backgroundColor;
            if (module == selectedModule) GUI.backgroundColor = new Color(0.42f, 0.66f, 0.92f);
            var title = string.IsNullOrWhiteSpace(displayName) || string.Equals(displayName, summary, StringComparison.Ordinal)
                ? summary
                : displayName;
            var metadata = module.PackageMetadata;
            var tooltip = summary + "\n业务域：" + TriggerAuthoringPackageCatalog.ResolveDomainId(module) +
                          (string.IsNullOrWhiteSpace(metadata.ContentKey) ? string.Empty : "\n内容标识：" + metadata.ContentKey) +
                          "\n" + AssetDatabase.GetAssetPath(module) + "\n" + triggerCount + " 个触发器";
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(24f);
            if (GUILayout.Button(new GUIContent(title, tooltip), EditorStyles.miniButtonLeft, GUILayout.Height(26f)))
                selectModule(module);
            GUILayout.Label(triggerCount.ToString(), EditorStyles.miniButtonRight, GUILayout.Width(34f), GUILayout.Height(26f));
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = oldBackground;
            return true;
        }

        internal static bool MatchesModule(TriggerAuthoringModuleAsset module, string filter)
        {
            if (module == null) return false;
            if (string.IsNullOrWhiteSpace(filter)) return true;
            var data = module.Module;
            var metadata = module.PackageMetadata;
            if (Matches(module.name, filter) ||
                   Matches(data != null ? data.ModuleId : null, filter) ||
                   Matches(data != null ? data.DisplayName : null, filter) ||
                   Matches(data != null ? data.Kind.ToString() : null, filter) ||
                   Matches(TriggerAuthoringPackageCatalog.ResolveDomainId(module), filter) ||
                   Matches(metadata.ContentKey, filter) ||
                   Matches(metadata.Owner, filter)) return true;
            var tags = metadata.Tags;
            for (var i = 0; i < tags.Count; i++)
                if (Matches(tags[i], filter)) return true;
            return false;
        }

        private static int CountPackages(IReadOnlyList<TriggerAuthoringDomainGroup> groups)
        {
            var count = 0;
            if (groups == null) return count;
            for (var i = 0; i < groups.Count; i++) count += groups[i].Packages.Count;
            return count;
        }

        private static int CountMatchingModules(
            IReadOnlyList<TriggerAuthoringModuleAsset> modules,
            string filter,
            bool parentMatches)
        {
            if (modules == null) return 0;
            var count = 0;
            for (var i = 0; i < modules.Count; i++)
                if (modules[i] != null && (parentMatches || MatchesModule(modules[i], filter))) count++;
            return count;
        }

        private static int CountModules(
            IReadOnlyList<TriggerAuthoringProjectAsset> projects,
            IReadOnlyList<TriggerAuthoringModuleAsset> unassignedModules)
        {
            var count = unassignedModules != null ? unassignedModules.Count : 0;
            if (projects == null) return count;
            for (var i = 0; i < projects.Count; i++)
                if (projects[i] != null && projects[i].Modules != null) count += projects[i].Modules.Count;
            return count;
        }

        private static bool Matches(string value, string filter)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                   (!string.IsNullOrWhiteSpace(value) &&
                    value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    internal sealed class TriggerAuthoringTemplateTreePanel
    {
        private Vector2 _scroll;
        private string _search = string.Empty;
        private readonly HashSet<int> _collapsedProjects = new HashSet<int>();

        internal void Draw(
            IReadOnlyList<TriggerAuthoringProjectAsset> projects,
            IReadOnlyList<TriggerAuthoringTemplateAsset> unassignedTemplates,
            TriggerAuthoringTemplateAsset selectedTemplate,
            Action<TriggerAuthoringTemplateAsset> selectTemplate,
            Action createTemplate,
            Action<TriggerAuthoringProjectAsset> createTemplateForProject,
            Action showModules,
            float width)
        {
            if (projects == null) throw new ArgumentNullException(nameof(projects));
            if (unassignedTemplates == null) throw new ArgumentNullException(nameof(unassignedTemplates));
            if (selectTemplate == null) throw new ArgumentNullException(nameof(selectTemplate));
            if (createTemplate == null) throw new ArgumentNullException(nameof(createTemplate));
            if (createTemplateForProject == null) throw new ArgumentNullException(nameof(createTemplateForProject));
            if (showModules == null) throw new ArgumentNullException(nameof(showModules));

            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            if (GUILayout.Toolbar(1, new[] { "内容包", "函数库" }, EditorStyles.toolbarButton) == 0)
            {
                showModules();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(new GUIContent("模板", "项目内可复用的触发条件与行为模板。"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(CountTemplates(projects, unassignedTemplates).ToString(), EditorStyles.miniLabel);
            if (GUILayout.Button(new GUIContent("+", "创建触发器模板"), EditorStyles.toolbarButton, GUILayout.Width(24f)))
                createTemplate();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _search = EditorGUILayout.TextField(_search ?? string.Empty, EditorStyles.toolbarSearchField);
            if (GUILayout.Button(new GUIContent("×", "清除模板搜索"), EditorStyles.toolbarButton, GUILayout.Width(22f)) &&
                !string.IsNullOrEmpty(_search))
            {
                _search = string.Empty;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var filter = (_search ?? string.Empty).Trim();
            var visibleTemplates = 0;
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null) continue;
                var templates = project.TemplateCatalog != null
                    ? project.TemplateCatalog.Templates
                    : null;
                var projectMatches = Matches(project.name, filter);
                var matchingTemplates = CountMatchingTemplates(templates, filter, projectMatches);
                if (!projectMatches && matchingTemplates == 0) continue;

                var projectKey = project.GetInstanceID();
                var expanded = !_collapsedProjects.Contains(projectKey) || filter.Length > 0;
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                var nextExpanded = EditorGUILayout.Foldout(
                    expanded,
                    new GUIContent(project.name, AssetDatabase.GetAssetPath(project)),
                    true,
                    EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                GUILayout.Label(matchingTemplates.ToString(), EditorStyles.miniLabel, GUILayout.Width(28f));
                using (new EditorGUI.DisabledScope(project.TemplateCatalog == null))
                {
                    if (GUILayout.Button(new GUIContent("+", "在此项目中创建触发器模板"), EditorStyles.toolbarButton, GUILayout.Width(24f)))
                        createTemplateForProject(project);
                }
                EditorGUILayout.EndHorizontal();
                if (nextExpanded) _collapsedProjects.Remove(projectKey);
                else _collapsedProjects.Add(projectKey);
                if (!nextExpanded || templates == null) continue;

                for (var t = 0; t < templates.Count; t++)
                {
                    if (templates[t] != null &&
                        DrawTemplateRow(templates[t], selectedTemplate, filter, projectMatches, selectTemplate))
                        visibleTemplates++;
                }
            }

            if (unassignedTemplates.Count > 0)
            {
                GUILayout.Space(6f);
                EditorGUILayout.LabelField($"未分配（{unassignedTemplates.Count}）", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("这些模板尚未加入任何项目模板目录。", MessageType.Warning);
                for (var i = 0; i < unassignedTemplates.Count; i++)
                    if (DrawTemplateRow(unassignedTemplates[i], selectedTemplate, filter, false, selectTemplate))
                        visibleTemplates++;
            }

            if (visibleTemplates == 0)
                EditorGUILayout.HelpBox("没有模板匹配当前搜索条件。可使用模板工具栏中的 + 创建模板。", MessageType.Info);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(filter.Length == 0 ? "全部模板" : "匹配 " + visibleTemplates + " 个", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(projects.Count + " 个项目", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        internal static bool MatchesTemplate(TriggerAuthoringTemplateAsset asset, string filter)
        {
            if (asset == null) return false;
            if (string.IsNullOrWhiteSpace(filter)) return true;
            var template = asset.Template;
            var definition = TriggerAuthoringTemplateDefinition.Get(template);
            if (Matches(asset.name, filter) ||
                Matches(template != null ? template.TemplateId : null, filter) ||
                Matches(template != null ? template.DisplayName : null, filter) ||
                Matches(definition != null ? definition.Event : null, filter))
                return true;

            var parameters = template?.Parameters;
            if (parameters == null) return false;
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter != null &&
                    (Matches(parameter.Name, filter) || Matches(parameter.Description, filter)))
                    return true;
            }
            return false;
        }

        private static bool DrawTemplateRow(
            TriggerAuthoringTemplateAsset asset,
            TriggerAuthoringTemplateAsset selectedTemplate,
            string filter,
            bool parentMatches,
            Action<TriggerAuthoringTemplateAsset> selectTemplate)
        {
            if (asset == null || !parentMatches && !MatchesTemplate(asset, filter)) return false;
            var template = asset.Template;
            var definition = TriggerAuthoringTemplateDefinition.Get(template);
            var templateId = template != null ? template.TemplateId : null;
            var displayName = template != null ? template.DisplayName : null;
            var title = string.IsNullOrWhiteSpace(displayName)
                ? string.IsNullOrWhiteSpace(templateId) ? asset.name : templateId
                : displayName;
            var parameters = template?.Parameters;
            var parameterCount = parameters != null ? parameters.Count : 0;
            var requiredCount = CountRequiredParameters(parameters);
            var tooltip = (templateId ?? asset.name) + "\n" +
                          (definition != null ? definition.Event ?? string.Empty : string.Empty) + "\n" +
                          BuildParameterTooltip(parameters) + "\n" + AssetDatabase.GetAssetPath(asset);

            var oldBackground = GUI.backgroundColor;
            if (asset == selectedTemplate) GUI.backgroundColor = new Color(0.42f, 0.66f, 0.92f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12f);
            if (GUILayout.Button(new GUIContent(title, tooltip), EditorStyles.miniButtonLeft, GUILayout.Height(30f)))
                selectTemplate(asset);
            var countLabel = requiredCount > 0
                ? parameterCount + " 入参\n" + requiredCount + " 必填"
                : parameterCount + " 入参";
            GUILayout.Label(countLabel, EditorStyles.miniButtonRight, GUILayout.Width(54f), GUILayout.Height(30f));
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = oldBackground;
            return true;
        }

        private static int CountMatchingTemplates(
            IReadOnlyList<TriggerAuthoringTemplateAsset> templates,
            string filter,
            bool parentMatches)
        {
            if (templates == null) return 0;
            var count = 0;
            for (var i = 0; i < templates.Count; i++)
                if (templates[i] != null && (parentMatches || MatchesTemplate(templates[i], filter))) count++;
            return count;
        }

        private static int CountTemplates(
            IReadOnlyList<TriggerAuthoringProjectAsset> projects,
            IReadOnlyList<TriggerAuthoringTemplateAsset> unassignedTemplates)
        {
            var count = unassignedTemplates != null ? unassignedTemplates.Count : 0;
            if (projects == null) return count;
            for (var i = 0; i < projects.Count; i++)
            {
                var templates = projects[i]?.TemplateCatalog?.Templates;
                if (templates != null) count += templates.Count;
            }
            return count;
        }

        private static int CountRequiredParameters(IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            if (parameters == null) return 0;
            var count = 0;
            for (var i = 0; i < parameters.Count; i++)
                if (parameters[i] != null && parameters[i].Required && !parameters[i].HasDefault) count++;
            return count;
        }

        private static string BuildParameterTooltip(IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            if (parameters == null || parameters.Count == 0) return "无需调用输入";
            var lines = new List<string> { "调用输入：" };
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null) continue;
                lines.Add("- " + (parameter.Name ?? "<未命名>") + " : " +
                          TriggerAuthoringEditorLabels.ValueType(parameter.Type) +
                          (parameter.Required && !parameter.HasDefault ? "（必填）" : "（可选）"));
            }
            return string.Join("\n", lines.ToArray());
        }

        private static bool Matches(string value, string filter)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                   (!string.IsNullOrWhiteSpace(value) &&
                    value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    internal sealed class TriggerAuthoringModuleContentPanel
    {
        private Vector2 _scroll;

        internal void Draw(
            TriggerAuthoringModuleAsset selectedModule,
            TriggerAuthoringModuleDrawer drawer,
            float availableWidth,
            bool showEmbeddedDiagnostics)
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (selectedModule == null)
            {
                EditorGUILayout.HelpBox(
                    "请从左侧选择一个模块开始编辑。\n" +
                    "如果还没有项目，请使用工具栏中的“创建项目”（将同时创建目录和起始模块）。",
                    MessageType.Info);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, false, true);
            drawer?.Draw(availableWidth, true, showEmbeddedDiagnostics);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
    }

    internal sealed class TriggerAuthoringTemplateContentPanel : IDisposable
    {
        private TriggerAuthoringTemplateAsset _target;
        private UnityEditor.Editor _editor;

        internal void Draw(TriggerAuthoringTemplateAsset selectedTemplate)
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (selectedTemplate == null)
            {
                EditorGUILayout.HelpBox(
                    "请从左侧选择模板，或使用模板工具栏中的 + 创建模板。",
                    MessageType.Info);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            EnsureEditor(selectedTemplate);
            _editor?.OnInspectorGUI();
            EditorGUILayout.EndVertical();
        }

        public void Dispose()
        {
            if (_editor != null) UnityEngine.Object.DestroyImmediate(_editor);
            _editor = null;
            _target = null;
        }

        private void EnsureEditor(TriggerAuthoringTemplateAsset selectedTemplate)
        {
            if (_target == selectedTemplate && _editor != null) return;
            Dispose();
            _target = selectedTemplate;
            _editor = UnityEditor.Editor.CreateEditor(selectedTemplate);
        }
    }

    internal sealed class TriggerAuthoringSourceSyncPanel
    {
        private TriggerAuthoringSyncInspection _inspection;
        private double _nextInspectionAt;

        internal void Invalidate()
        {
            _inspection = null;
            _nextInspectionAt = 0d;
        }

        internal void Draw(
            TriggerAuthoringModuleAsset selectedModule,
            Action importSource,
            Action exportSource)
        {
            if (selectedModule == null)
            {
                var localization =
                    TriggerAuthoringEditorIntegration
                        .Localization;
                SirenixEditorGUI.BeginBox(
                    localization.Get(
                        "abilitykit.trigger.sourceSync.title"));
                EditorGUILayout.LabelField(
                    localization.Get(
                        "abilitykit.trigger.sourceSync.noModule"),
                    EditorStyles.miniLabel);
                SirenixEditorGUI.EndBox();
                return;
            }

            if (_inspection == null || _nextInspectionAt <= EditorApplication.timeSinceStartup)
            {
                _inspection = TriggerAuthoringSourceSync.Inspect(selectedModule);
                _nextInspectionAt = EditorApplication.timeSinceStartup + 0.5d;
            }

            var inspection = _inspection;
            EditorImGuiControls.DrawSourceSyncCard(
                new EditorSourceSyncCardModel(
                    inspection.PlatformInspection,
                    importSource,
                    exportSource,
                    copyPath: () => EditorGUIUtility.systemCopyBuffer = inspection.SourcePath ?? string.Empty,
                    revealPath: () => EditorUtility.RevealInFinder(inspection.SourcePath),
                    title: TriggerAuthoringEditorIntegration.Localization.Get(
                        "abilitykit.trigger.sourceSync.title"),
                    localization: TriggerAuthoringEditorIntegration.Localization));
        }
    }

    internal sealed class TriggerAuthoringProjectValidationPanel
    {
        internal void Draw(
            TriggerAuthoringModuleAsset selectedModule,
            TriggerAuthoringProjectValidationResult validation,
            TriggerAuthoringProjectAsset validationProject,
            EditorDiagnosticCollection diagnostics,
            EditorCommandRegistry commands,
            UnityEngine.Object commandOwner)
        {
            SirenixEditorGUI.BeginBox("项目校验");
            var project = selectedModule != null ? selectedModule.Project : null;
            if (project == null)
            {
                EditorGUILayout.HelpBox(
                    "所选模块尚未注册到项目。",
                    MessageType.Warning);
                SirenixEditorGUI.EndBox();
                return;
            }

            EditorGUILayout.LabelField(project.name, EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("validate"), EditorStyles.miniButtonLeft))
                commands.Execute(
                    TriggerAuthoringCommandIds.ValidateProject,
                    new EditorCommandContext(commandOwner, project));
            if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("export-runtime"), EditorStyles.miniButtonRight))
                commands.Execute(
                    TriggerAuthoringCommandIds.ExportProject,
                    new EditorCommandContext(commandOwner, project));
            EditorGUILayout.EndHorizontal();

            if (validation != null && validationProject == project)
            {
                EditorGUILayout.LabelField(
                    $"{validation.ModuleCount} 个模块，{validation.TemplateCount} 个模板  " +
                    $"（错误 {diagnostics.ErrorCount}，警告 {diagnostics.WarningCount}）",
                    EditorStyles.miniLabel);
                for (var i = 0; i < diagnostics.Items.Count; i++)
                {
                    var diagnostic = diagnostics.Items[i];
                    var icon = diagnostic.Severity == EditorDiagnosticSeverity.Error
                        ? EditorGUIUtility.IconContent("console.erroricon.sml")
                        : diagnostic.Severity == EditorDiagnosticSeverity.Warning
                            ? EditorGUIUtility.IconContent("console.warnicon.sml")
                            : EditorGUIUtility.IconContent("console.infoicon.sml");
                    var content = new GUIContent(
                        diagnostic.Code + " " + diagnostic.Path + "\n" + diagnostic.Message,
                        icon != null ? icon.image : null,
                        diagnostic.Locate != null ? "定位相关资产" : string.Empty);
                    using (new EditorGUI.DisabledScope(diagnostic.Locate == null))
                    {
                        if (GUILayout.Button(content, EditorStyles.helpBox, GUILayout.MinHeight(38f)))
                            diagnostic.Locate?.Invoke();
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField(TriggerAuthoringEditorIntegration.T("not-validated-yet"), EditorStyles.miniLabel);
            }

            SirenixEditorGUI.EndBox();
        }
    }
}
#endif
