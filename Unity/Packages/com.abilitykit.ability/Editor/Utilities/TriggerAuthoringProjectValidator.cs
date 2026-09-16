#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Packages;
using AbilityKit.Triggering.Runtime.Plan.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal sealed class TriggerAuthoringProjectValidationResult
    {
        public readonly List<TriggerAuthoringDiagnostic> Diagnostics = new List<TriggerAuthoringDiagnostic>();
        public int ModuleCount;
        public int TemplateCount;

        public bool Success => !TriggerAuthoringValidator.HasErrors(Diagnostics);

        public string BuildMessage()
        {
            if (Diagnostics.Count == 0)
                return $"已校验 {ModuleCount} 个模块和 {TemplateCount} 个模板。";

            var lines = new List<string>();
            for (var i = 0; i < Diagnostics.Count; i++)
            {
                var diagnostic = Diagnostics[i];
                lines.Add($"{SeverityLabel(diagnostic.Severity)} {diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}");
            }
            return string.Join(Environment.NewLine, lines);
        }

        private static string SeverityLabel(TriggerAuthoringDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case TriggerAuthoringDiagnosticSeverity.Error: return "错误";
                case TriggerAuthoringDiagnosticSeverity.Warning: return "警告";
                default: return "信息";
            }
        }
    }

    internal static class TriggerAuthoringProjectValidator
    {
        private const int MaxExpandedTriggerNodeCount = 512;

        public static TriggerAuthoringProjectValidationResult Validate(TriggerAuthoringProjectAsset project)
        {
            var result = new TriggerAuthoringProjectValidationResult();
            if (project == null)
            {
                AddError(result, "TRG3000", "project", "触发器项目为空。");
                return result;
            }

            if (project.EventCatalog == null)
                AddError(result, "TRG3001", "project.eventCatalog", "必须分配事件目录。");
            if (project.GlobalBlackboardCatalog == null)
                AddError(result, "TRG3002", "project.globalBlackboardCatalog", "必须分配全局黑板目录。");
            if (project.TemplateCatalog == null)
                AddError(result, "TRG3003", "project.templateCatalog", "必须分配模板目录。");

            ValidateEventCatalog(project, result);
            ValidateGlobalBlackboardCatalog(project, result);
            ValidateTemplates(project, result);
            ValidateModules(project, result);
            return result;
        }

        private static void ValidateEventCatalog(
            TriggerAuthoringProjectAsset project,
            TriggerAuthoringProjectValidationResult result)
        {
            var events = project.EventCatalog != null ? project.EventCatalog.Events : null;
            if (events == null) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < events.Count; i++)
            {
                var definition = events[i];
                var path = $"project.eventCatalog.events[{i}]";
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    AddError(result, "TRG3010", path + ".id", "必须填写事件 ID。");
                    continue;
                }
                if (!ids.Add(definition.Id))
                    AddError(result, "TRG3011", path + ".id", $"事件 ID 重复：{definition.Id}。");
            }
        }

        private static void ValidateGlobalBlackboardCatalog(
            TriggerAuthoringProjectAsset project,
            TriggerAuthoringProjectValidationResult result)
        {
            var keys = project.GlobalBlackboardCatalog != null ? project.GlobalBlackboardCatalog.Keys : null;
            if (keys == null) return;
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var path = $"project.globalBlackboardCatalog.keys[{i}]";
                if (key == null || string.IsNullOrWhiteSpace(key.Key))
                {
                    AddError(result, "TRG3020", path + ".key", "必须填写全局黑板 Key。");
                    continue;
                }
                if (!names.Add(key.Key))
                    AddError(result, "TRG3021", path + ".key", $"全局黑板 Key 重复：{key.Key}。");
                if (string.IsNullOrWhiteSpace(key.Domain))
                    AddError(result, "TRG3022", path + ".domain", "必须填写全局黑板域。");
                if (key.Type == TriggerValueType.None)
                    AddError(result, "TRG3023", path + ".type", "必须设置全局黑板类型。");
                else if (key.Type == TriggerValueType.Vector3 ||
                         key.Type == TriggerValueType.IntegerList ||
                         key.Type == TriggerValueType.Object)
                    AddError(result, "TRG3026", path + ".type", $"项目触发器黑板不支持类型 {key.Type}。");
                if (key.DefaultValue == null || key.DefaultValue.Source != TriggerValueSource.Constant)
                    AddError(result, "TRG3024", path + ".defaultValue", "全局黑板默认值必须是常量。");
                else if (key.DefaultValue.Type != key.Type)
                    AddError(result, "TRG3025", path + ".defaultValue.type", $"默认值类型必须为 {key.Type}，当前为 {key.DefaultValue.Type}。");
            }
        }

        private static void ValidateTemplates(
            TriggerAuthoringProjectAsset project,
            TriggerAuthoringProjectValidationResult result)
        {
            var templates = project.TemplateCatalog != null ? project.TemplateCatalog.Templates : null;
            if (templates == null) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < templates.Count; i++)
            {
                var asset = templates[i];
                var path = $"project.templates[{i}]";
                if (asset == null)
                {
                    AddError(result, "TRG3030", path, "模板资产引用缺失。");
                    continue;
                }

                result.TemplateCount++;
                if (asset.Project != project)
                    AddError(result, "TRG3031", path + ".project", $"模板资产“{asset.name}”已分配到其他项目。");
                var id = asset.Template != null ? asset.Template.TemplateId : null;
                if (!string.IsNullOrWhiteSpace(id) && !ids.Add(id))
                    AddError(result, "TRG3032", path + ".templateId", $"Template ID 重复：{id}。");
                AddDiagnostics(result, path, TriggerAuthoringTemplateValidator.Validate(
                    asset.Template,
                    TriggerAuthoringValidationContext.Create(asset)));
            }
        }

        private static void ValidateModules(
            TriggerAuthoringProjectAsset project,
            TriggerAuthoringProjectValidationResult result)
        {
            var modules = project.Modules;
            if (modules == null || modules.Count == 0)
            {
                AddWarning(result, "TRG3040", "project.modules", "项目中没有模块资产。");
                return;
            }

            var assets = new HashSet<TriggerAuthoringModuleAsset>();
            var moduleIds = new HashSet<string>(StringComparer.Ordinal);
            var packageIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var triggerIdOwners = new Dictionary<int, string>();
            var triggerDefinitions = new Dictionary<int, TriggerDefinitionData>();
            var runtimeDocuments = new List<TriggerPlanAggregateCompiler.SourceDocument>();
            for (var i = 0; i < modules.Count; i++)
            {
                var asset = modules[i];
                var path = $"project.modules[{i}]";
                if (asset == null)
                {
                    AddError(result, "TRG3041", path, "模块资产引用缺失。");
                    continue;
                }
                if (!assets.Add(asset))
                {
                    AddError(result, "TRG3042", path, $"模块资产引用重复：{asset.name}。");
                    continue;
                }

                result.ModuleCount++;
                if (asset.Project != project)
                    AddError(result, "TRG3043", path + ".project", $"模块资产“{asset.name}”已分配到其他项目。");
                var moduleId = asset.Module != null ? asset.Module.ModuleId : null;
                if (!string.IsNullOrWhiteSpace(moduleId) && !moduleIds.Add(moduleId))
                    AddError(result, "TRG3044", path + ".moduleId", $"模块 ID 重复：{moduleId}。");
                var contentKey = TriggerAuthoringPackageService.NormalizeIdentifier(asset.PackageMetadata.ContentKey);
                if (!string.IsNullOrWhiteSpace(contentKey))
                {
                    var domainId = TriggerAuthoringPackageCatalog.ResolveDomainId(asset);
                    var identity = domainId + ":" + contentKey;
                    if (!packageIdentities.Add(identity))
                        AddError(result, "TRG3046", path + ".packageMetadata.contentKey",
                            $"内容包标识重复：业务域“{domainId}”中已经存在“{contentKey}”。");
                }
                var triggers = asset.Module?.Triggers;
                if (triggers != null)
                    for (var triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
                    {
                        var triggerId = triggers[triggerIndex] != null ? triggers[triggerIndex].Id : 0;
                        if (triggerId <= 0) continue;
                        if (triggerIdOwners.TryGetValue(triggerId, out var owner))
                        {
                            AddError(
                                result,
                                "TRG3045",
                                path + ".triggers[" + triggerIndex + "].id",
                                $"项目 TriggerId 重复：{triggerId}，首次出现在 {owner}。");
                        }
                        else
                        {
                            triggerIdOwners.Add(triggerId, path + ".triggers[" + triggerIndex + "]");
                            triggerDefinitions.Add(triggerId, triggers[triggerIndex]);
                        }
                    }

                var compile = TriggerAuthoringRuntimeExporter.Build(asset);
                AddDiagnostics(result, path, compile.Diagnostics);
                if (!compile.Success) continue;
                runtimeDocuments.Add(new TriggerPlanAggregateCompiler.SourceDocument(
                    moduleId ?? asset.name,
                    TriggerAuthoringRuntimeExporter.Serialize(compile.Database)));
            }

            ValidateExecuteTriggerGraph(triggerDefinitions, triggerIdOwners, result);

            if (TriggerAuthoringValidator.HasErrors(result.Diagnostics)) return;
            try
            {
                var aggregateJson = TriggerPlanAggregateCompiler.Compile(runtimeDocuments);
                var database = new TriggerPlanJsonDatabase();
                database.LoadFromJson(aggregateJson, project.name + ".runtime");
            }
            catch (Exception ex)
            {
                AddError(result, "TRG3050", "project.runtime", ex.Message);
            }
        }

        private static void ValidateExecuteTriggerGraph(
            Dictionary<int, TriggerDefinitionData> definitions,
            Dictionary<int, string> paths,
            TriggerAuthoringProjectValidationResult result)
        {
            var graph = new Dictionary<int, List<int>>();
            foreach (var pair in definitions)
            {
                var references = new List<int>();
                CollectExecuteTriggerReferences(pair.Value?.Condition, pair.Key, paths, references, result);
                CollectExecuteTriggerReferences(pair.Value?.Actions, pair.Key, paths, references, result);
                graph[pair.Key] = references;
            }

            var states = new Dictionary<int, byte>();
            var stack = new List<int>();
            var reportedCycles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var triggerId in graph.Keys)
                VisitTrigger(triggerId, graph, paths, states, stack, reportedCycles, result);

            if (TriggerAuthoringValidator.HasErrors(result.Diagnostics)) return;
            var budgets = new Dictionary<int, long>();
            foreach (var pair in definitions)
            {
                var count = CountExpandedTriggerNodes(pair.Key, definitions, graph, budgets, MaxExpandedTriggerNodeCount + 1L);
                if (count > MaxExpandedTriggerNodeCount)
                    AddError(result, "TRG3063", paths[pair.Key],
                        $"Trigger {pair.Key} expands to more than {MaxExpandedTriggerNodeCount} nodes through execute_trigger calls.");
            }
        }

        private static void CollectExecuteTriggerReferences(
            TriggerNodeData node,
            int ownerTriggerId,
            Dictionary<int, string> paths,
            List<int> references,
            TriggerAuthoringProjectValidationResult result)
        {
            if (node == null) return;
            if (node.Enabled && node.Kind == TriggerNodeKind.Action &&
                string.Equals(node.Type, "execute_trigger", StringComparison.OrdinalIgnoreCase))
            {
                TriggerArgumentData triggerArgument = null;
                var arguments = node.Arguments;
                if (arguments != null)
                    for (var i = 0; i < arguments.Count; i++)
                        if (arguments[i] != null &&
                            (string.Equals(arguments[i].Name, "trigger_id", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(arguments[i].Name, "triggerId", StringComparison.OrdinalIgnoreCase)))
                        {
                            triggerArgument = arguments[i];
                            break;
                        }

                var value = triggerArgument?.Value;
                var ownerPath = paths.TryGetValue(ownerTriggerId, out var path) ? path : $"trigger.{ownerTriggerId}";
                if (value == null || value.Source != TriggerValueSource.Constant)
                {
                    AddError(result, "TRG3062", ownerPath,
                        "execute_trigger requires a constant trigger_id so references and recursion can be validated statically.");
                }
                else
                {
                    var referencedId = value.IntegerValue > int.MaxValue || value.IntegerValue < int.MinValue
                        ? 0
                        : (int)value.IntegerValue;
                    if (referencedId <= 0 || !paths.ContainsKey(referencedId))
                        AddError(result, "TRG3060", ownerPath,
                            $"execute_trigger references missing TriggerId {value.IntegerValue}.");
                    else
                        references.Add(referencedId);
                }
            }

            CollectExecuteTriggerReferences(node.Condition, ownerTriggerId, paths, references, result);
            CollectExecuteTriggerReferences(node.Children, ownerTriggerId, paths, references, result);
            CollectExecuteTriggerReferences(node.ElseChildren, ownerTriggerId, paths, references, result);
        }

        private static void CollectExecuteTriggerReferences(
            List<TriggerNodeData> nodes,
            int ownerTriggerId,
            Dictionary<int, string> paths,
            List<int> references,
            TriggerAuthoringProjectValidationResult result)
        {
            if (nodes == null) return;
            for (var i = 0; i < nodes.Count; i++)
                CollectExecuteTriggerReferences(nodes[i], ownerTriggerId, paths, references, result);
        }

        private static void VisitTrigger(
            int triggerId,
            Dictionary<int, List<int>> graph,
            Dictionary<int, string> paths,
            Dictionary<int, byte> states,
            List<int> stack,
            HashSet<string> reported,
            TriggerAuthoringProjectValidationResult result)
        {
            if (states.TryGetValue(triggerId, out var state) && state == 2) return;
            if (state == 1)
            {
                var start = stack.IndexOf(triggerId);
                if (start < 0) start = 0;
                var cycle = new List<int>();
                for (var i = start; i < stack.Count; i++) cycle.Add(stack[i]);
                cycle.Add(triggerId);
                var signature = string.Join("->", cycle);
                if (reported.Add(signature))
                    AddError(result, "TRG3061", paths[triggerId], $"execute_trigger cycle detected: {signature}.");
                return;
            }

            states[triggerId] = 1;
            stack.Add(triggerId);
            if (graph.TryGetValue(triggerId, out var dependencies))
                for (var i = 0; i < dependencies.Count; i++)
                    VisitTrigger(dependencies[i], graph, paths, states, stack, reported, result);
            stack.RemoveAt(stack.Count - 1);
            states[triggerId] = 2;
        }

        private static long CountExpandedTriggerNodes(
            int triggerId,
            Dictionary<int, TriggerDefinitionData> definitions,
            Dictionary<int, List<int>> graph,
            Dictionary<int, long> memo,
            long stopAfter)
        {
            if (memo.TryGetValue(triggerId, out var cached)) return cached;
            if (!definitions.TryGetValue(triggerId, out var definition) || definition == null) return 0;
            var count = CountNodes(definition.Condition, stopAfter) + CountNodes(definition.Actions, stopAfter);
            if (graph.TryGetValue(triggerId, out var dependencies))
                for (var i = 0; i < dependencies.Count && count < stopAfter; i++)
                    count = SaturatingAdd(count,
                        CountExpandedTriggerNodes(dependencies[i], definitions, graph, memo, stopAfter), stopAfter);
            count = Math.Min(count, stopAfter);
            memo[triggerId] = count;
            return count;
        }

        private static long CountNodes(TriggerNodeData node, long stopAfter)
        {
            if (node == null) return 0;
            var count = 1L;
            count = SaturatingAdd(count, CountNodes(node.Condition, stopAfter), stopAfter);
            count = SaturatingAdd(count, CountNodes(node.Children, stopAfter), stopAfter);
            return SaturatingAdd(count, CountNodes(node.ElseChildren, stopAfter), stopAfter);
        }

        private static long CountNodes(List<TriggerNodeData> nodes, long stopAfter)
        {
            if (nodes == null) return 0;
            long count = 0;
            for (var i = 0; i < nodes.Count && count < stopAfter; i++)
                count = SaturatingAdd(count, CountNodes(nodes[i], stopAfter), stopAfter);
            return count;
        }

        private static long SaturatingAdd(long left, long right, long maximum)
        {
            if (left >= maximum || right >= maximum || left > maximum - right) return maximum;
            return left + right;
        }

        private static void AddDiagnostics(
            TriggerAuthoringProjectValidationResult result,
            string prefix,
            IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (diagnostics == null) return;
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var item = diagnostics[i];
                result.Diagnostics.Add(new TriggerAuthoringDiagnostic(
                    item.Code,
                    item.Severity,
                    prefix + "." + item.Path,
                    item.Message));
            }
        }

        private static void AddError(TriggerAuthoringProjectValidationResult result, string code, string path, string message)
        {
            result.Diagnostics.Add(new TriggerAuthoringDiagnostic(code, TriggerAuthoringDiagnosticSeverity.Error, path, message));
        }

        private static void AddWarning(TriggerAuthoringProjectValidationResult result, string code, string path, string message)
        {
            result.Diagnostics.Add(new TriggerAuthoringDiagnostic(code, TriggerAuthoringDiagnosticSeverity.Warning, path, message));
        }
    }

    internal sealed class TriggerAuthoringBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var failures = TriggerAuthoringProjectValidationMenu.ValidateAllProjects(logResults: true);
            if (failures.Count > 0)
                throw new BuildFailedException("触发器项目校验失败。" + Environment.NewLine + string.Join(Environment.NewLine, failures));
        }
    }

    internal static class TriggerAuthoringProjectValidationMenu
    {
        private const string ValidateSelectedMenu = "Assets/AbilityKit/触发器编辑/校验项目";

        [MenuItem(ValidateSelectedMenu)]
        private static void ValidateSelected()
        {
            var project = Selection.activeObject as TriggerAuthoringProjectAsset;
            if (project == null) return;
            var result = TriggerAuthoringProjectValidator.Validate(project);
            Log(project, result);
            EditorUtility.DisplayDialog(
                "触发器项目校验",
                result.BuildMessage(),
                "确定");
        }

        [MenuItem(ValidateSelectedMenu, true)]
        private static bool CanValidateSelected()
        {
            return Selection.activeObject is TriggerAuthoringProjectAsset;
        }

        [MenuItem("Tools/AbilityKit/触发器编辑/校验全部项目")]
        private static void ValidateAll()
        {
            var failures = ValidateAllProjects(logResults: true);
            EditorUtility.DisplayDialog(
                "触发器项目校验",
                failures.Count == 0 ? "全部触发器项目均已通过校验。" : string.Join(Environment.NewLine, failures),
                "确定");
        }

        internal static List<string> ValidateAllProjects(bool logResults)
        {
            var failures = new List<string>();
            var guids = AssetDatabase.FindAssets("t:TriggerAuthoringProjectAsset");
            Array.Sort(guids, StringComparer.Ordinal);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var project = AssetDatabase.LoadAssetAtPath<TriggerAuthoringProjectAsset>(path);
                var result = TriggerAuthoringProjectValidator.Validate(project);
                if (logResults) Log(project, result);
                if (!result.Success) failures.Add(path + Environment.NewLine + result.BuildMessage());
            }
            return failures;
        }

        private static void Log(TriggerAuthoringProjectAsset project, TriggerAuthoringProjectValidationResult result)
        {
            var message = $"[TriggerAuthoring] Project '{(project != null ? project.name : "<null>")}' validation: {result.BuildMessage()}";
            if (result.Success) Debug.Log(message, project);
            else Debug.LogError(message, project);
        }
    }
}
#endif
