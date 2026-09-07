using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Export;
using UnityEngine;

using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 编辑器侧运行时导出：授权文档 →（剥离布局）→ 运行时 IR → 校验 → 写 JSON 文件。
    /// 校验失败不清空旧产物；成功返回写入的相对路径列表。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringRuntimeExporter")]
    public static class AuthoringRuntimeExporter
    {
        public static EditorExportReport Export(AuthoringAsset asset)
        {
            const string jobId = "behaviortree.runtime-json";
            var target = asset == null ? "Behavior Tree" : asset.name;
            var job = new EditorExportJob(
                jobId,
                target,
                "json",
                () => ExportEntry(asset, jobId, target));
            return EditorExportExecutor.Execute(new[] { job });
        }

        public static bool Export(
            AuthoringAsset asset,
            out List<string> outputs,
            out List<string> errors)
        {
            var report = Export(asset);
            outputs = report.Artifacts
                .Select(artifact => artifact.Path)
                .ToList();
            errors = report.Entries
                .Where(entry => entry.Status == EditorExportStatus.Failed)
                .SelectMany(entry => entry.Messages)
                .ToList();
            return report.Success;
        }

        private static EditorExportReportEntry ExportEntry(
            AuthoringAsset asset,
            string jobId,
            string target)
        {
            if (asset == null)
                return EditorExportReportEntry.Failed(jobId, target, "Asset is null.");

            var document = asset.LoadDocument();
            var tree = document.Tree;
            if (string.IsNullOrWhiteSpace(tree.TreeId))
                return EditorExportReportEntry.Failed(jobId, target, "TreeId must not be empty.");

            var json = TreeExporter.Export(
                document,
                EditorNodeCatalog.Registry,
                out var validationErrors);
            if (validationErrors.Count > 0)
            {
                return new EditorExportReportEntry(
                    jobId,
                    target,
                    EditorExportStatus.Failed,
                    messages: validationErrors);
            }

            var relativePath = asset.ResolveRuntimeExportPath(tree.TreeId);
            var absolutePath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", relativePath));
            var writeStatus = EditorAtomicFileWriter.WriteAllText(absolutePath, json);
            var exportStatus = writeStatus == EditorAtomicWriteStatus.Unchanged
                ? EditorExportStatus.Unchanged
                : EditorExportStatus.Exported;
            return new EditorExportReportEntry(
                jobId,
                target,
                exportStatus,
                new[] { new EditorExportArtifact(relativePath, "json") });
        }
    }
}
