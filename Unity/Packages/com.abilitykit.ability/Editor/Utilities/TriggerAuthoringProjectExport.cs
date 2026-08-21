#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbilityKit.Ability.Config.Authoring;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal sealed class TriggerAuthoringProjectExportResult
    {
        public bool Success;
        public readonly List<string> ExportedFiles = new List<string>();
        public readonly List<TriggerAuthoringDiagnostic> Diagnostics = new List<TriggerAuthoringDiagnostic>();
        public int ModuleCount;

        public string BuildMessage()
        {
            if (Success)
            {
                return $"Exported {ExportedFiles.Count} runtime plan file(s) for {ModuleCount} module(s) to '{string.Join("', '", ExportedFiles.ToArray())}'.";
            }

            var builder = new StringBuilder("Project runtime export failed:");
            for (var i = 0; i < Diagnostics.Count; i++)
            {
                builder.AppendLine();
                builder.Append(Diagnostics[i].Code).Append(' ').Append(Diagnostics[i].Path).Append(": ")
                    .Append(Diagnostics[i].Message);
            }
            return builder.Length == 0 ? "Project runtime export failed." : builder.ToString();
        }
    }

    /// <summary>
    /// 项目级 Runtime Plan 一键导出：先跑完整构建门禁校验（含跨模块聚合再加载），
    /// 全部通过后按模块逐一原子写出 {moduleId}.runtime.json 到项目的 RuntimeOutputRoot。
    /// 输出目录与 SourceJsonPath 同一约定：相对路径基于 Unity 工程根解析。
    /// </summary>
    internal static class TriggerAuthoringProjectExport
    {
        public static TriggerAuthoringProjectExportResult ExportAll(TriggerAuthoringProjectAsset project)
        {
            var result = new TriggerAuthoringProjectExportResult();
            if (project == null)
            {
                result.Diagnostics.Add(new TriggerAuthoringDiagnostic(
                    "TRG3100", TriggerAuthoringDiagnosticSeverity.Error, "project", "Trigger Authoring Project is null."));
                return result;
            }

            var validation = TriggerAuthoringProjectValidator.Validate(project);
            result.Diagnostics.AddRange(validation.Diagnostics);
            result.ModuleCount = validation.ModuleCount;
            if (!validation.Success) return result;

            var root = ResolveOutputRoot(project.RuntimeOutputRoot);
            if (string.IsNullOrWhiteSpace(root))
            {
                result.Diagnostics.Add(new TriggerAuthoringDiagnostic(
                    "TRG3101", TriggerAuthoringDiagnosticSeverity.Error, "project.runtimeOutputRoot",
                    "Runtime output root is not configured on the project."));
                return result;
            }

            for (var i = 0; i < project.Modules.Count; i++)
            {
                var module = project.Modules[i];
                if (module == null) continue;
                var moduleId = module.Module != null ? module.Module.ModuleId : null;
                var fileName = (string.IsNullOrWhiteSpace(moduleId) ? module.name : moduleId) + ".runtime.json";
                var path = Path.Combine(root, SanitizeFileName(fileName));
                var compile = TriggerAuthoringRuntimeExporter.Export(module, path);
                if (!compile.Success)
                {
                    result.Diagnostics.AddRange(compile.Diagnostics);
                    return result;
                }
                result.ExportedFiles.Add(path);
            }

            result.Success = true;
            AssetDatabase.Refresh();
            return result;
        }

        public static string ResolveOutputRoot(string configuredRoot)
        {
            if (string.IsNullOrWhiteSpace(configuredRoot)) return string.Empty;
            if (Path.IsPathRooted(configuredRoot)) return Path.GetFullPath(configuredRoot);
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, configuredRoot));
        }

        private static string SanitizeFileName(string fileName)
        {
            var builder = new StringBuilder(fileName.Length);
            var invalid = Path.GetInvalidFileNameChars();
            for (var i = 0; i < fileName.Length; i++)
            {
                var c = fileName[i];
                var invalidChar = false;
                for (var k = 0; k < invalid.Length; k++)
                {
                    if (invalid[k] != c) continue;
                    invalidChar = true;
                    break;
                }
                builder.Append(invalidChar ? '_' : c);
            }
            return builder.ToString();
        }
    }
}
#endif
