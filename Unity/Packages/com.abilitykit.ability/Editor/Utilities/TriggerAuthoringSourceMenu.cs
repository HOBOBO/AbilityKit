#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerAuthoringSourceMenu
    {
        private const string ExportMenu = "Assets/AbilityKit/Trigger Authoring/Export Source JSON";
        private const string ImportMenu = "Assets/AbilityKit/Trigger Authoring/Import Source JSON";
        private const string ValidateMenu = "Assets/AbilityKit/Trigger Authoring/Validate";

        [MenuItem(ExportMenu)]
        private static void Export()
        {
            var asset = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (asset == null) return;

            var path = asset.SourceJsonPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                var defaultName = asset.Module != null && !string.IsNullOrWhiteSpace(asset.Module.ModuleId)
                    ? asset.Module.ModuleId
                    : asset.name;
                path = EditorUtility.SaveFilePanel("Export Trigger Source JSON", Application.dataPath, defaultName, "json");
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

        [MenuItem(ImportMenu)]
        private static void Import()
        {
            var asset = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (asset == null) return;

            var path = asset.SourceJsonPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = EditorUtility.OpenFilePanel("Import Trigger Source JSON", Application.dataPath, "json");
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

        [MenuItem(ValidateMenu)]
        private static void Validate()
        {
            var asset = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (asset == null) return;
            var diagnostics = TriggerAuthoringValidator.Validate(asset.Module);
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
            return Selection.activeObject is TriggerAuthoringModuleAsset;
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
