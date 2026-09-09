#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerAuthoringSourceMenu
    {
        private const string ExportMenu = "Assets/AbilityKit/触发器编辑/导出 Source JSON";
        private const string ExportRuntimeMenu = "Assets/AbilityKit/触发器编辑/导出 Runtime Plan JSON";
        private const string ImportMenu = "Assets/AbilityKit/触发器编辑/导入 Source JSON";
        private const string ValidateMenu = "Assets/AbilityKit/触发器编辑/校验";
        private const string ExportProjectRuntimeMenu = "Assets/AbilityKit/触发器编辑/导出项目 Runtime Plan";
        private const string ExportSchemasMenu = "Tools/AbilityKit/触发器编辑/导出 Source Schema";

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
                    "导出触发器 Source JSON", Application.dataPath, defaultName,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringSourceSync.Export(asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器源文件冲突",
                    result.Message + "\n\n是否强制导出并覆盖 Source JSON？",
                    "强制导出",
                    "取消"))
            {
                result = TriggerAuthoringSourceSync.Export(asset, path, true);
            }

            ShowResult("导出", result);
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
            var path = EditorUtility.SaveFilePanel("导出 Runtime Plan JSON", Application.dataPath, defaultName, "json");
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
            EditorUtility.DisplayDialog("Runtime Plan 导出失败", message, "确定");
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
                    "导入触发器 Source JSON", Application.dataPath,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var preview = TriggerAuthoringSourceSync.PreviewImport(asset, path);
            if (!TriggerAuthoringSourceImportPreviewDialog.Confirm(preview)) return;

            var result = TriggerAuthoringSourceSync.Import(asset, path, preview.RequiresForce);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器资产冲突",
                    result.Message + "\n\n是否强制导入并覆盖资产内容？",
                    "强制导入",
                    "取消"))
                result = TriggerAuthoringSourceSync.Import(asset, path, true);

            ShowResult("导入", result);
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
            EditorUtility.DisplayDialog("项目运行时导出", result.BuildMessage(), "确定");
        }

        [MenuItem(ExportProjectRuntimeMenu, true)]
        private static bool CanExportProjectRuntime()
        {
            return Selection.activeObject is TriggerAuthoringProjectAsset;
        }

        [MenuItem(ExportSchemasMenu)]
        private static void ExportSchemas()
        {
            var directory = EditorUtility.OpenFolderPanel(
                "导出触发器 Source Schema",
                Application.dataPath,
                string.Empty);
            if (string.IsNullOrWhiteSpace(directory)) return;

            var result = TriggerAuthoringSourceSchema.ExportAll(directory);
            AssetDatabase.Refresh();
            Debug.Log(
                $"[TriggerAuthoring] Source schema export completed. directory='{result.DirectoryPath}', " +
                $"written={result.WrittenPaths.Count}, unchanged={result.UnchangedPaths.Count}.");
            EditorUtility.DisplayDialog(
                "触发器 Source Schema 导出",
                $"共导出 {result.TotalCount} 个 Schema 文件。\n已写入：{result.WrittenPaths.Count}\n未变化：{result.UnchangedPaths.Count}",
                "确定");
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
                EditorUtility.DisplayDialog("触发器数据校验", "暂无诊断。", "确定");
                return;
            }

            var message = string.Empty;
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                message += $"{diagnostic.Severity} {diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}\n";
            }
            EditorUtility.DisplayDialog("触发器数据校验", message, "确定");
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
                    "导出触发器模板 Source JSON", Application.dataPath, defaultName,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }
            var result = TriggerAuthoringTemplateSourceSync.Export(asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器模板源文件冲突",
                    result.Message + "\n\n是否强制导出并覆盖 Source JSON？",
                    "强制导出",
                    "取消"))
                result = TriggerAuthoringTemplateSourceSync.Export(asset, path, true);
            ShowResult("模板导出", result);
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
                    "导入触发器模板 Source JSON", Application.dataPath,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }
            var preview = TriggerAuthoringTemplateSourceSync.PreviewImport(asset, path);
            if (!TriggerAuthoringSourceImportPreviewDialog.Confirm(preview)) return;

            var result = TriggerAuthoringTemplateSourceSync.Import(asset, path, preview.RequiresForce);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器模板资产冲突",
                    result.Message + "\n\n是否强制导入并覆盖资产内容？",
                    "强制导入",
                    "取消"))
                result = TriggerAuthoringTemplateSourceSync.Import(asset, path, true);
            ShowResult("模板导入", result);
            if (result.Success) AssetDatabase.SaveAssets();
        }

        private static void ValidateTemplate()
        {
            var asset = Selection.activeObject as TriggerAuthoringTemplateAsset;
            if (asset == null) return;
            var diagnostics = TriggerAuthoringTemplateValidator.Validate(
                asset.Template,
                TriggerAuthoringValidationContext.Create(asset));
            var message = diagnostics.Count == 0 ? "暂无诊断。" : TriggerAuthoringTemplateValidator.BuildMessage(diagnostics);
            EditorUtility.DisplayDialog("触发器模板校验", message, "确定");
        }

        private static void ShowResult(string operation, TriggerAuthoringSyncResult result)
        {
            if (result.Success)
            {
                Debug.Log($"[TriggerAuthoring] {operation} succeeded. hash={result.ContentHash}");
                return;
            }
            Debug.LogError($"[TriggerAuthoring] {operation} failed. state={result.State}, message={result.Message}");
            EditorUtility.DisplayDialog($"触发器源文件{operation}失败", result.Message, "确定");
        }
    }
}
#endif
