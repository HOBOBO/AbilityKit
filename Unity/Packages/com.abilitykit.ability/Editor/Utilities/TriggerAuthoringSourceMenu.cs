#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerAuthoringSourceMenu
    {
        private const string ExportMenu = "Assets/AbilityKit/Trigger Authoring/Export Source JSON";
        private const string ExportRuntimeMenu = "Assets/AbilityKit/Trigger Authoring/Export Runtime Plan JSON";
        private const string ImportMenu = "Assets/AbilityKit/Trigger Authoring/Import Source JSON";
        private const string ValidateMenu = "Assets/AbilityKit/Trigger Authoring/Validate";
        private const string ExportProjectRuntimeMenu = "Assets/AbilityKit/Trigger Authoring/Export Project Runtime Plans";

        [MenuItem(ExportMenu)]
        private static void Export()
        {
            var asset = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (asset == null)
            {
                ExportTemplate();
                return;
            }

            var path = asset.SourceJsonPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                var defaultName = asset.Module != null && !string.IsNullOrWhiteSpace(asset.Module.ModuleId)
                    ? asset.Module.ModuleId
                    : asset.name;
                path = EditorUtility.SaveFilePanel(
                    "Export Trigger Source JSON", Application.dataPath, defaultName,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringSourceSync.Export(asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Source Conflict",
                    result.Message + "\n\nForce export and overwrite Source JSON?",
                    "Force Export",
                    "Cancel"))
            {
                result = TriggerAuthoringSourceSync.Export(asset, path, true);
            }

            ShowResult("Export", result);
            if (result.Success) AssetDatabase.SaveAssets();
        }

        [MenuItem(ExportRuntimeMenu)]
        private static void ExportRuntime()
        {
            var asset = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (asset == null) return;

            var defaultName = asset.Module != null && !string.IsNullOrWhiteSpace(asset.Module.ModuleId)
                ? asset.Module.ModuleId + ".runtime"
                : asset.name + ".runtime";
            var path = EditorUtility.SaveFilePanel("Export Runtime Plan JSON", Application.dataPath, defaultName, "json");
            if (string.IsNullOrWhiteSpace(path)) return;

            var result = TriggerAuthoringRuntimeExporter.Export(asset, path);
            if (result.Success)
            {
                Debug.Log($"[TriggerAuthoring] Runtime Plan export succeeded. path='{path}', {result.BuildMessage()}");
                AssetDatabase.Refresh();
                return;
            }

            var message = result.BuildMessage();
            Debug.LogError("[TriggerAuthoring] Runtime Plan export failed. " + message);
            EditorUtility.DisplayDialog("Runtime Plan Export Failed", message, "OK");
        }

        [MenuItem(ImportMenu)]
        private static void Import()
        {
            var asset = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (asset == null)
            {
                ImportTemplate();
                return;
            }

            var path = asset.SourceJsonPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = EditorUtility.OpenFilePanel(
                    "Import Trigger Source JSON", Application.dataPath,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringSourceSync.Import(asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Asset Conflict",
                    result.Message + "\n\nForce import and overwrite Asset content?",
                    "Force Import",
                    "Cancel"))
            {
                result = TriggerAuthoringSourceSync.Import(asset, path, true);
            }

            ShowResult("Import", result);
            if (result.Success) AssetDatabase.SaveAssets();
        }

        [MenuItem(ExportProjectRuntimeMenu)]
        private static void ExportProjectRuntime()
        {
            var project = Selection.activeObject as TriggerAuthoringProjectAsset;
            if (project == null) return;
            var result = TriggerAuthoringProjectExport.ExportAll(project);
            var message = "[TriggerAuthoring] Project runtime export " +
                          (result.Success ? "succeeded. " : "failed. ") + result.BuildMessage();
            if (result.Success) Debug.Log(message, project);
            else Debug.LogError(message, project);
            EditorUtility.DisplayDialog("Project Runtime Export", result.BuildMessage(), "OK");
        }

        [MenuItem(ExportProjectRuntimeMenu, true)]
        private static bool CanExportProjectRuntime()
        {
            return Selection.activeObject is TriggerAuthoringProjectAsset;
        }

        [MenuItem(ValidateMenu)]
        private static void Validate()
        {
            var asset = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (asset == null)
            {
                ValidateTemplate();
                return;
            }
            var diagnostics = TriggerAuthoringValidator.Validate(
                asset.Module,
                TriggerAuthoringValidationContext.Create(asset));
            if (diagnostics.Count == 0)
            {
                EditorUtility.DisplayDialog("Trigger Authoring Validation", "No diagnostics.", "OK");
                return;
            }

            var message = string.Empty;
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                message += $"{diagnostic.Severity} {diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}\n";
            }
            EditorUtility.DisplayDialog("Trigger Authoring Validation", message, "OK");
        }

        [MenuItem(ExportMenu, true)]
        [MenuItem(ImportMenu, true)]
        [MenuItem(ValidateMenu, true)]
        private static bool ValidateSelection()
        {
            return Selection.activeObject is TriggerAuthoringModuleAsset ||
                   Selection.activeObject is TriggerAuthoringTemplateAsset;
        }

        [MenuItem(ExportRuntimeMenu, true)]
        private static bool ValidateRuntimeSelection()
        {
            return Selection.activeObject is TriggerAuthoringModuleAsset;
        }

        private static void ExportTemplate()
        {
            var asset = Selection.activeObject as TriggerAuthoringTemplateAsset;
            if (asset == null) return;
            var path = asset.SourceJsonPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                var defaultName = asset.Template != null && !string.IsNullOrWhiteSpace(asset.Template.TemplateId)
                    ? asset.Template.TemplateId
                    : asset.name;
                path = EditorUtility.SaveFilePanel(
                    "Export Trigger Template Source JSON", Application.dataPath, defaultName,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }
            var result = TriggerAuthoringTemplateSourceSync.Export(asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Template Source Conflict",
                    result.Message + "\n\nForce export and overwrite Source JSON?",
                    "Force Export",
                    "Cancel"))
                result = TriggerAuthoringTemplateSourceSync.Export(asset, path, true);
            ShowResult("Template Export", result);
            if (result.Success) AssetDatabase.SaveAssets();
        }

        private static void ImportTemplate()
        {
            var asset = Selection.activeObject as TriggerAuthoringTemplateAsset;
            if (asset == null) return;
            var path = asset.SourceJsonPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = EditorUtility.OpenFilePanel(
                    "Import Trigger Template Source JSON", Application.dataPath,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }
            var result = TriggerAuthoringTemplateSourceSync.Import(asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Template Asset Conflict",
                    result.Message + "\n\nForce import and overwrite Asset content?",
                    "Force Import",
                    "Cancel"))
                result = TriggerAuthoringTemplateSourceSync.Import(asset, path, true);
            ShowResult("Template Import", result);
            if (result.Success) AssetDatabase.SaveAssets();
        }

        private static void ValidateTemplate()
        {
            var asset = Selection.activeObject as TriggerAuthoringTemplateAsset;
            if (asset == null) return;
            var diagnostics = TriggerAuthoringTemplateValidator.Validate(
                asset.Template,
                TriggerAuthoringValidationContext.Create(asset));
            var message = diagnostics.Count == 0 ? "No diagnostics." : TriggerAuthoringTemplateValidator.BuildMessage(diagnostics);
            EditorUtility.DisplayDialog("Trigger Template Validation", message, "OK");
        }

        private static void ShowResult(string operation, TriggerAuthoringSyncResult result)
        {
            if (result.Success)
            {
                Debug.Log($"[TriggerAuthoring] {operation} succeeded. hash={result.ContentHash}");
                return;
            }
            Debug.LogError($"[TriggerAuthoring] {operation} failed. state={result.State}, message={result.Message}");
            EditorUtility.DisplayDialog($"Trigger Source {operation} Failed", result.Message, "OK");
        }
    }
}
#endif
