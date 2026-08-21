#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
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
                return $"Validated {ModuleCount} module(s) and {TemplateCount} template(s).";

            var lines = new List<string>();
            for (var i = 0; i < Diagnostics.Count; i++)
            {
                var diagnostic = Diagnostics[i];
                lines.Add($"{diagnostic.Severity} {diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}");
            }
            return string.Join(Environment.NewLine, lines);
        }
    }

    internal static class TriggerAuthoringProjectValidator
    {
        public static TriggerAuthoringProjectValidationResult Validate(TriggerAuthoringProjectAsset project)
        {
            var result = new TriggerAuthoringProjectValidationResult();
            if (project == null)
            {
                AddError(result, "TRG3000", "project", "Trigger Authoring Project is null.");
                return result;
            }

            if (project.EventCatalog == null)
                AddError(result, "TRG3001", "project.eventCatalog", "Event Catalog is required.");
            if (project.GlobalBlackboardCatalog == null)
                AddError(result, "TRG3002", "project.globalBlackboardCatalog", "Global Blackboard Catalog is required.");
            if (project.TemplateCatalog == null)
                AddError(result, "TRG3003", "project.templateCatalog", "Template Catalog is required.");

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
                    AddError(result, "TRG3010", path + ".id", "Event ID is required.");
                    continue;
                }
                if (!ids.Add(definition.Id))
                    AddError(result, "TRG3011", path + ".id", $"Duplicate Event ID: {definition.Id}.");
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
                    AddError(result, "TRG3020", path + ".key", "Global Blackboard key is required.");
                    continue;
                }
                if (!names.Add(key.Key))
                    AddError(result, "TRG3021", path + ".key", $"Duplicate Global Blackboard key: {key.Key}.");
                if (string.IsNullOrWhiteSpace(key.Domain))
                    AddError(result, "TRG3022", path + ".domain", "Global Blackboard domain is required.");
                if (key.Type == TriggerValueType.None)
                    AddError(result, "TRG3023", path + ".type", "Global Blackboard type is required.");
                if (key.DefaultValue == null || key.DefaultValue.Source != TriggerValueSource.Constant)
                    AddError(result, "TRG3024", path + ".defaultValue", "Global Blackboard default must be a constant value.");
                else if (key.DefaultValue.Type != key.Type)
                    AddError(result, "TRG3025", path + ".defaultValue.type", $"Default type must be {key.Type}, got {key.DefaultValue.Type}.");
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
                    AddError(result, "TRG3030", path, "Template Asset reference is missing.");
                    continue;
                }

                result.TemplateCount++;
                if (asset.Project != project)
                    AddError(result, "TRG3031", path + ".project", $"Template Asset '{asset.name}' is assigned to a different project.");
                var id = asset.Template != null ? asset.Template.TemplateId : null;
                if (!string.IsNullOrWhiteSpace(id) && !ids.Add(id))
                    AddError(result, "TRG3032", path + ".templateId", $"Duplicate Template ID: {id}.");
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
                AddWarning(result, "TRG3040", "project.modules", "Project contains no Module Assets.");
                return;
            }

            var assets = new HashSet<TriggerAuthoringModuleAsset>();
            var moduleIds = new HashSet<string>(StringComparer.Ordinal);
            var runtimeDocuments = new List<TriggerPlanAggregateCompiler.SourceDocument>();
            for (var i = 0; i < modules.Count; i++)
            {
                var asset = modules[i];
                var path = $"project.modules[{i}]";
                if (asset == null)
                {
                    AddError(result, "TRG3041", path, "Module Asset reference is missing.");
                    continue;
                }
                if (!assets.Add(asset))
                {
                    AddError(result, "TRG3042", path, $"Duplicate Module Asset reference: {asset.name}.");
                    continue;
                }

                result.ModuleCount++;
                if (asset.Project != project)
                    AddError(result, "TRG3043", path + ".project", $"Module Asset '{asset.name}' is assigned to a different project.");
                var moduleId = asset.Module != null ? asset.Module.ModuleId : null;
                if (!string.IsNullOrWhiteSpace(moduleId) && !moduleIds.Add(moduleId))
                    AddError(result, "TRG3044", path + ".moduleId", $"Duplicate Module ID: {moduleId}.");

                var compile = TriggerAuthoringRuntimeExporter.Build(asset);
                AddDiagnostics(result, path, compile.Diagnostics);
                if (!compile.Success) continue;
                runtimeDocuments.Add(new TriggerPlanAggregateCompiler.SourceDocument(
                    moduleId ?? asset.name,
                    TriggerAuthoringRuntimeExporter.Serialize(compile.Database)));
            }

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
                throw new BuildFailedException("Trigger Authoring project validation failed." + Environment.NewLine + string.Join(Environment.NewLine, failures));
        }
    }

    internal static class TriggerAuthoringProjectValidationMenu
    {
        private const string ValidateSelectedMenu = "Assets/AbilityKit/Trigger Authoring/Validate Project";

        [MenuItem(ValidateSelectedMenu)]
        private static void ValidateSelected()
        {
            var project = Selection.activeObject as TriggerAuthoringProjectAsset;
            if (project == null) return;
            var result = TriggerAuthoringProjectValidator.Validate(project);
            Log(project, result);
            EditorUtility.DisplayDialog(
                "Trigger Authoring Project Validation",
                result.BuildMessage(),
                "OK");
        }

        [MenuItem(ValidateSelectedMenu, true)]
        private static bool CanValidateSelected()
        {
            return Selection.activeObject is TriggerAuthoringProjectAsset;
        }

        [MenuItem("Tools/AbilityKit/Trigger Authoring/Validate All Projects")]
        private static void ValidateAll()
        {
            var failures = ValidateAllProjects(logResults: true);
            EditorUtility.DisplayDialog(
                "Trigger Authoring Project Validation",
                failures.Count == 0 ? "All Trigger Authoring projects are valid." : string.Join(Environment.NewLine, failures),
                "OK");
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
