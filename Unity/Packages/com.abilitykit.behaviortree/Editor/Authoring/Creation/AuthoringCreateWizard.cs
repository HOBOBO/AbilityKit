using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using UnityEditor;
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
    /// 快速创建向导：TreeId + 模板 -> 生成授权资产（可选注册进项目目录资产并打开图编辑器）。
    /// 附带"从运行时 JSON 批量导入"：存量导出产物一键转成授权资产并入管线（消灭手工双维护）。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringCreateWizard")]
    public sealed class AuthoringCreateWizard : EditorWindow
    {
        private string _treeId = "new_tree";
        private string _displayName = "新行为树";
        private int _templateIndex = 1;
        private AuthoringProjectAsset? _project;
        private bool _openGraph = true;
        private Vector2 _scroll;

        public static void Open()
        {
            Open(Selection.activeObject as AuthoringProjectAsset);
        }

        public static void Open(AuthoringProjectAsset? project)
        {
            var window = GetWindow<AuthoringCreateWizard>();
            window.titleContent = new GUIContent("创建行为树");
            window.minSize = new Vector2(480f, 430f);
            window._project = project;
            if (project != null)
            {
                window._templateIndex = Mathf.Clamp(
                    project.DefaultTemplateIndex,
                    0,
                    AuthoringTemplates.Catalog().Count - 1);
            }
            window.Repaint();
        }

        private void OnGUI()
        {
            var catalog = AuthoringTemplates.Catalog();
            var names = catalog.Select(t => t.DisplayName).ToArray();
            _templateIndex = Mathf.Clamp(_templateIndex, 0, names.Length - 1);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(10f);
            DrawHeader();
            GUILayout.Space(12f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawSectionTitle("文件");
            _treeId = EditorGUILayout.TextField(new GUIContent("Tree ID", "同时作为运行时 JSON 文件名"), _treeId);
            _displayName = EditorGUILayout.TextField("显示名称", _displayName);
            EditorGUILayout.Space(6f);
            DrawSectionTitle("内容");
            _templateIndex = EditorGUILayout.Popup("初始模板", _templateIndex, names);
            DrawTemplateSummary(catalog[_templateIndex].Build());
            EditorGUILayout.EndVertical();

            GUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawSectionTitle("项目");
            var selectedProject = (AuthoringProjectAsset?)EditorGUILayout.ObjectField(
                "所属项目", _project, typeof(AuthoringProjectAsset), false);
            if (!ReferenceEquals(selectedProject, _project))
            {
                _project = selectedProject;
                if (_project != null)
                {
                    _templateIndex = Mathf.Clamp(
                        _project.DefaultTemplateIndex,
                        0,
                        catalog.Count - 1);
                }
            }
            DrawProjectSummary();
            _openGraph = EditorGUILayout.Toggle("创建后打开编辑器", _openGraph);
            EditorGUILayout.EndVertical();

            var validationMessage = ValidateInput(_project, (_treeId ?? string.Empty).Trim());
            if (!string.IsNullOrEmpty(validationMessage))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(validationMessage, MessageType.Warning);
            }

            GUILayout.Space(12f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawSectionTitle("迁移");
            EditorGUILayout.LabelField("运行时 JSON", EditorStyles.miniLabel);
            if (GUILayout.Button("批量导入...", GUILayout.Height(24f))) ImportFromRuntimeJson();
            EditorGUILayout.EndVertical();
            GUILayout.Space(10f);
            EditorGUILayout.EndScrollView();

            // Primary actions remain reachable even when the content area needs scrolling.
            GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            if (GUILayout.Button("取消", GUILayout.Height(32f), GUILayout.Width(96f))) Close();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!CanCreate(_project, (_treeId ?? string.Empty).Trim())))
            {
                var primaryLabel = _openGraph ? "创建并编辑" : "创建";
                if (GUILayout.Button(primaryLabel, GUILayout.Height(32f), GUILayout.Width(140f)))
                    Create(catalog[_templateIndex].Build);
            }
            GUILayout.Space(8f);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        private void DrawHeader()
        {
            var title = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };
            EditorGUILayout.LabelField("创建行为树", title, GUILayout.Height(24f));
            EditorGUILayout.LabelField(
                _project == null ? "独立资产" : _project.name,
                EditorStyles.miniLabel);
        }

        private static void DrawSectionTitle(string title)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField(title, style, GUILayout.Height(20f));
        }

        private static void DrawTemplateSummary(AuthoringSourceDocument document)
        {
            EditorGUILayout.LabelField(
                $"{document.Tree.Nodes.Count} 个节点  ·  {document.Tree.Blackboard.Keys.Count} 个黑板键",
                EditorStyles.miniLabel);
        }

        private void DrawProjectSummary()
        {
            if (_project == null)
            {
                EditorGUILayout.LabelField("未登记到项目，导出时使用单树配置", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField(
                $"{_project.Trees.Count} 棵树  ·  {_project.ExportTargets.Count} 个导出目标",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(_project.TreeAssetDirectory)
                    ? "Assets"
                    : _project.TreeAssetDirectory,
                EditorStyles.miniLabel);
        }

        internal static bool CanCreate(AuthoringProjectAsset? project, string treeId)
        {
            return string.IsNullOrEmpty(ValidateInput(project, treeId));
        }

        internal static string ValidateInput(AuthoringProjectAsset? project, string treeId)
        {
            if (!IsValidTreeId(treeId))
                return "Tree ID 不能为空，且只允许字母、数字、下划线和连字符。";
            if (project != null && project.ContainsTreeId(treeId))
                return $"项目中已经存在 Tree ID '{treeId}'。";
            return string.Empty;
        }

        private void Create(Func<AuthoringSourceDocument> buildTemplate)
        {
            var treeId = (_treeId ?? "").Trim();
            if (!IsValidTreeId(treeId))
            {
                EditorUtility.DisplayDialog("创建失败", "Tree ID 不能为空，且只允许字母、数字、下划线和连字符。", "确定");
                return;
            }

            if (_project != null && _project.ContainsTreeId(treeId))
            {
                EditorUtility.DisplayDialog("创建失败", $"项目中已经存在 Tree ID '{treeId}'。", "确定");
                return;
            }

            var defaultDirectory = ResolveDefaultAssetDirectory(_project);
            var path = EditorUtility.SaveFilePanelInProject(
                "保存授权资产", treeId, "asset", "", defaultDirectory);
            if (string.IsNullOrEmpty(path)) return;

            AuthoringAsset asset;
            try
            {
                asset = CreateAsset(_project, path, treeId, _displayName, buildTemplate);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("创建失败", ex.Message, "确定");
                return;
            }

            Debug.Log($"[BtProject] 已创建 {path}（模板: {AuthoringTemplates.Catalog()[_templateIndex].DisplayName}）");
            if (_openGraph) AuthoringGraphWindow.Open(asset, _project);
            Close();
        }

        internal static AuthoringAsset CreateAsset(
            AuthoringProjectAsset? project,
            string assetPath,
            string treeId,
            string displayName,
            Func<AuthoringSourceDocument> buildTemplate)
        {
            if (!IsValidTreeId(treeId))
                throw new ArgumentException("Tree ID 不能为空，且只允许字母、数字、下划线和连字符。", nameof(treeId));
            if (string.IsNullOrWhiteSpace(assetPath)
                || !assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                || !assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("行为树资产必须保存为 Assets 下的 .asset 文件。", nameof(assetPath));
            }
            if (buildTemplate == null) throw new ArgumentNullException(nameof(buildTemplate));
            if (project != null && project.ContainsTreeId(treeId))
                throw new InvalidOperationException($"项目中已经存在 Tree ID '{treeId}'。");
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                throw new InvalidOperationException($"资产路径已经存在: {assetPath}");

            var document = buildTemplate();
            document.Tree.TreeId = treeId;
            document.Metadata.Description = displayName ?? string.Empty;
            document.Metadata.Author = "wizard";

            var asset = CreateInstance<AuthoringAsset>();
            try
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                asset.SaveDocument(document);
                project?.Register(asset);
                AssetDatabase.SaveAssets();
                return asset;
            }
            catch
            {
                project?.Unregister(asset);
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);
                throw;
            }
        }

        private static string ResolveDefaultAssetDirectory(AuthoringProjectAsset? project)
        {
            var configured = project?.TreeAssetDirectory;
            if (string.IsNullOrWhiteSpace(configured)) return "Assets";

            var normalized = configured.Trim().Replace('\\', '/');
            if (!(normalized.Equals("Assets", StringComparison.Ordinal)
                || normalized.StartsWith("Assets/", StringComparison.Ordinal))
                || normalized.Split('/').Any(segment => segment is "." or ".."))
            {
                return "Assets";
            }

            var current = "Assets";
            foreach (var segment in normalized.Split('/').Skip(1))
            {
                var next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
            return current;
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
                "选择运行时 JSON 目录", Path.Combine(AuthoringMenuUtility.RepositoryRoot, "Unity"), "");
            if (string.IsNullOrEmpty(sourceDir)) return;

            var files = Directory.GetFiles(sourceDir, "*.json")
                .Where(f => !f.EndsWith(".meta", StringComparison.Ordinal))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            if (files.Count == 0)
            {
                EditorUtility.DisplayDialog("导入", "该目录下没有 JSON 文件。", "确定");
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
                    var definition = TreeJson.Load(File.ReadAllText(file));
                    var document = TreeExporter.Import(definition);
                    document.Tree.TreeId = definition.TreeId.Length > 0 ? definition.TreeId : treeId;
                    document.Metadata.Description = "从运行时 JSON 导入。";

                    var assetPath = relativeTarget + "/" + treeId + ".asset";
                    var asset = CreateInstance<AuthoringAsset>();
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
            EditorUtility.DisplayDialog("导入完成", message, "确定");
        }

        private static AuthoringProjectAsset? PickOrCreateProject()
        {
            var projects = AuthoringMenuUtility.FindAllProjects();
            if (projects.Count == 1) return projects[0];

            if (projects.Count > 1)
            {
                var names = projects.Select(p => p.name).ToArray();
                // 简版：多项目时默认注册到第一个，如需其他项目在 Inspector 手动调整
                EditorUtility.DisplayDialog("多个项目", $"存在 {projects.Count} 个项目资产，默认注册到 '{names[0]}'。如需其他项目请在检查器中手动调整。", "确定");
                return projects[0];
            }

            var path = EditorUtility.SaveFilePanelInProject("新建行为树项目", "BtAuthoringProject", "asset", "");
            if (string.IsNullOrEmpty(path)) return null;
            var project = CreateInstance<AuthoringProjectAsset>();
            AssetDatabase.CreateAsset(project, path);
            return project;
        }
    }
}
