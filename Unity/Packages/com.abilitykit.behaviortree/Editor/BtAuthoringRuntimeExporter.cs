using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 编辑器侧运行时导出：授权文档 →（剥离布局）→ 运行时 IR → 校验 → 写 JSON 文件。
    /// 校验失败不清空旧产物；成功返回写入的相对路径列表。
    /// </summary>
    public static class BtAuthoringRuntimeExporter
    {
        public static bool Export(BtAuthoringAsset asset, out List<string> outputs, out List<string> errors)
        {
            outputs = new List<string>();
            errors = new List<string>();
            if (asset == null)
            {
                errors.Add("Asset is null.");
                return false;
            }

            var document = asset.LoadDocument();
            var tree = document.Tree;
            if (string.IsNullOrWhiteSpace(tree.TreeId))
            {
                errors.Add("TreeId must not be empty.");
                return false;
            }

            var json = BtTreeExporter.Export(document, BtEditorNodeCatalog.Registry, out var validationErrors);
            if (validationErrors.Count > 0)
            {
                errors.AddRange(validationErrors);
                return false;
            }

            var relativePath = asset.ResolveRuntimeExportPath(tree.TreeId);
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllText(absolutePath, json);
            outputs.Add(relativePath);
            return true;
        }
    }
}
