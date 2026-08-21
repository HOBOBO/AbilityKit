using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 快速创建向导：TreeId + 模板 -> 生成授权资产（可选注册进项目目录资产并打开图编辑器）。
    /// 附带"从运行时 JSON 批量导入"：存量导出产物一键转成授权资产并入管线（消灭手工双维护）。
    /// </summary>
    public sealed class BtAuthoringCreateWizard : EditorWindow
    {
        private string _treeId = "new_tree";
        private string _displayName = "New Tree";
        private int _templateIndex = 1;
        private BtAuthoringProjectAsset? _project;
        private bool _openGraph = true;
        private Vector2 _scroll;

        public static void Open()
        {
            var window = GetWindow<BtAuthoringCreateWizard>();
            window.titleContent = new GUIContent("BT Create Wizard");
            window.minSize = new Vector2(420f, 320f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("快速创建行为树", EditorStyles.boldLabel);

            _treeId = EditorGUILayout.TextField("TreeId（=导出文件名）", _treeId);
            _displayName = EditorGUILayout.TextField("显示名", _displayName);

            var catalog = BtAuthoringTemplates.Catalog();
            var names = catalog.Select(t => t.DisplayName).ToArray();
            _templateIndex = EditorGUILayout.Popup("模板", Mathf.Clamp(_templateIndex, 0, names.Length - 1), names);

            _project = (BtAuthoringProjectAsset?)EditorGUILayout.ObjectField(
                "注册到项目（可选）", _project, typeof(BtAuthoringProjectAsset), false);
            _openGraph = EditorGUILayout.Toggle("创建后打开图编辑器", _openGraph);

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("创建", GUILayout.Height(28f)))
            {
                Create(catalog[_templateIndex].Build);
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("存量迁移", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "选择一个包含运行时导出 JSON 的目录（如 Resources/moba/bt），批量为每个 JSON 创建授权资产并注册到项目。", MessageType.Info);
            if (GUILayout.Button("从运行时 JSON 批量导入…"))
            {
                ImportFromRuntimeJson();
            }
        }

        private void Create(Func<BtAuthoringSourceDocument> buildTemplate)
        {
            var treeId = (_treeId ?? "").Trim();
            if (!IsValidTreeId(treeId))
            {
                EditorUtility.DisplayDialog("创建失败", "TreeId 不能为空，且只允许字母/数字/下划线/连字符。", "OK");
                return;
            }

            var path = EditorUtility.SaveFilePanelInProject("保存授权资产", treeId, "asset", "", "Assets");
            if (string.IsNullOrEmpty(path)) return;

            var document = buildTemplate();
            document.Tree.TreeId = treeId;
            document.Metadata.Description = _displayName;
            document.Metadata.Author = "wizard";

            var asset = CreateInstance<BtAuthoringAsset>();
            AssetDatabase.CreateAsset(asset, path);
            asset.SaveDocument(document);
            AssetDatabase.SaveAssets();

            _project?.Register(asset);

            Debug.Log($"[BtProject] 已创建 {path}（模板: {BtAuthoringTemplates.Catalog()[_templateIndex].DisplayName}）");
            if (_openGraph) BtAuthoringGraphWindow.Open(asset);
            Close();
        }

        private static bool IsValidTreeId(string treeId)
        {
            if (string.IsNullOrEmpty(treeId)) return false;
            foreach (var c in treeId)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') return false;
            }
            return true;
        }

        /// <summary>批量导入运行时 JSON：每份 JSON -> 授权资产（自动布局）+ 注册进选定/新建项目。</summary>
        private static void ImportFromRuntimeJson()
        {
            var sourceDir = EditorUtility.OpenFolderPanel(
                "选择运行时 JSON 目录", Path.Combine(BtAuthoringMenuUtility.RepositoryRoot, "Unity"), "");
            if (string.IsNullOrEmpty(sourceDir)) return;

            var files = Directory.GetFiles(sourceDir, "*.json")
                .Where(f => !f.EndsWith(".meta", StringComparison.Ordinal))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            if (files.Count == 0)
            {
                EditorUtility.DisplayDialog("导入", "该目录下没有 .json 文件。", "OK");
                return;
            }

            var project = PickOrCreateProject();
            if (project == null) return;

            var targetDir = EditorUtility.OpenFolderPanel(
                "授权资产保存目录（项目内）", "Assets", "");
            if (string.IsNullOrEmpty(targetDir)) return;
            var relativeTarget = "Assets" + targetDir.Substring(Application.dataPath.Length).Replace('\\', '/');

            var created = new List<string>();
            var failed = new List<string>();
            foreach (var file in files)
            {
                var treeId = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var definition = BtTreeJson.Load(File.ReadAllText(file));
                    var document = BtTreeExporter.Import(definition);
                    document.Tree.TreeId = definition.TreeId.Length > 0 ? definition.TreeId : treeId;
                    document.Metadata.Description = "Imported from runtime JSON.";

                    var assetPath = relativeTarget + "/" + treeId + ".asset";
                    var asset = CreateInstance<BtAuthoringAsset>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                    asset.SaveDocument(document);
                    project.Register(asset);
                    created.Add(assetPath);
                }
                catch (Exception ex)
                {
                    failed.Add($"{treeId}: {ex.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            var message = $"导入成功 {created.Count} 棵：\n" + string.Join("\n", created);
            if (failed.Count > 0) message += "\n\n失败：\n" + string.Join("\n", failed);
            Debug.Log("[BtProject] " + message);
            EditorUtility.DisplayDialog("导入完成", message, "OK");
        }

        private static BtAuthoringProjectAsset? PickOrCreateProject()
        {
            var projects = BtAuthoringMenuUtility.FindAllProjects();
            if (projects.Count == 1) return projects[0];

            if (projects.Count > 1)
            {
                var names = projects.Select(p => p.name).ToArray();
                // 简版：多项目时默认注册到第一个，如需其他项目在 Inspector 手动调整
                EditorUtility.DisplayDialog("多个项目", $"存在 {projects.Count} 个项目资产，默认注册到 '{names[0]}'。如需其他项目请在 Inspector 手动调整。", "OK");
                return projects[0];
            }

            var path = EditorUtility.SaveFilePanelInProject("新建行为树项目", "BtAuthoringProject", "asset", "");
            if (string.IsNullOrEmpty(path)) return null;
            var project = CreateInstance<BtAuthoringProjectAsset>();
            AssetDatabase.CreateAsset(project, path);
            return project;
        }
    }
}
