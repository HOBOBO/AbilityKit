using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.UI;
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
    /// 项目目录资产 Inspector：树清单（扫描添加/移除）、导出目标编辑、项目校验、一键导出 + 报告。
    /// 基类须写全限定名——本命名空间以 .Editor 结尾，简单名 Editor 会解析到命名空间。
    /// </summary>
    [CustomEditor(typeof(AuthoringProjectAsset))]
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringProjectAssetInspector")]
    public sealed class AuthoringProjectAssetInspector : UnityEditor.Editor
    {
        private List<ExportReportEntry>? _lastReport;
        private List<string>? _lastValidationErrors;

        public override void OnInspectorGUI()
        {
            var project = (AuthoringProjectAsset)target;

            DrawHeader(project);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawProjectSettings(project);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawTreeList(project);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawExportTargets(project);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10f);
            DrawActions(project);
        }

        private static void DrawHeader(AuthoringProjectAsset project)
        {
            EditorGUILayout.Space(4f);
            var titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
            };
            EditorGUILayout.LabelField(project.name, titleStyle, GUILayout.Height(22f));
            EditorGUILayout.LabelField(
                $"{project.Trees.Count(tree => tree != null)} 棵树  ·  {project.ExportTargets.Count} 个导出目标",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(8f);
        }

        private static void DrawProjectSettings(AuthoringProjectAsset project)
        {
            DrawSectionHeader("创建配置", "新建");

            EditorGUI.BeginChangeCheck();
            var treeAssetDirectory = EditorGUILayout.TextField(
                "树资产目录",
                project.TreeAssetDirectory ?? string.Empty);
            var templates = AuthoringTemplates.Catalog();
            var templateNames = templates.Select(template => template.DisplayName).ToArray();
            var templateIndex = EditorGUILayout.Popup(
                "默认模板",
                Mathf.Clamp(project.DefaultTemplateIndex, 0, templateNames.Length - 1),
                templateNames);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(project, "编辑行为树项目设置");
                project.TreeAssetDirectory = treeAssetDirectory.Trim().Replace('\\', '/');
                project.DefaultTemplateIndex = templateIndex;
                project.MarkDirty();
            }

            if (GUILayout.Button("新建行为树", GUILayout.Height(28f)))
            {
                AuthoringCreateWizard.Open(project);
            }
        }

        private static void DrawTreeList(AuthoringProjectAsset project)
        {
            DrawSectionHeader("行为树", project.Trees.Count.ToString());

            if (project.Trees.Count == 0)
                EditorGUILayout.LabelField("尚未创建行为树", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(28f));

            for (var i = 0; i < project.Trees.Count; i++)
            {
                if (i > 0) EditorGUILayout.Space(2f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label((i + 1).ToString("00"), EditorStyles.miniLabel, GUILayout.Width(22f));
                var tree = (AuthoringAsset)EditorGUILayout.ObjectField(project.Trees[i], typeof(AuthoringAsset), false);
                if (!ReferenceEquals(tree, project.Trees[i]))
                {
                    Undo.RecordObject(project, "修改行为树登记");
                    project.Trees[i] = tree;
                    project.MarkDirty();
                }
                if (tree != null && GUILayout.Button("编辑", EditorStyles.miniButtonLeft, GUILayout.Width(48f)))
                {
                    AuthoringGraphWindow.Open(tree, project);
                }
                if (GUILayout.Button("移除", EditorStyles.miniButtonRight, GUILayout.Width(48f)))
                {
                    Undo.RecordObject(project, "移除行为树");
                    project.Trees.RemoveAt(i);
                    project.MarkDirty();
                    GUIUtility.ExitGUI();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("扫描并登记资产", GUILayout.Height(24f)))
            {
                ScanAndRegister(project);
            }
        }

        private static void ScanAndRegister(AuthoringProjectAsset project)
        {
            var registered = new HashSet<AuthoringAsset>(project.Trees.Where(t => t != null));
            var added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AuthoringAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || registered.Contains(asset)) continue;
                project.Register(asset);
                registered.Add(asset);
                added++;
            }
            project.MarkDirty();
            Debug.Log($"[BtProject] 扫描添加 {added} 棵树。");
        }

        private static void DrawExportTargets(AuthoringProjectAsset project)
        {
            DrawSectionHeader("导出目标", project.ExportTargets.Count.ToString());
            EditorGUILayout.LabelField("相对仓库根", EditorStyles.miniLabel);

            for (var i = 0; i < project.ExportTargets.Count; i++)
            {
                if (i > 0) EditorGUILayout.Space(2f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label((i + 1).ToString("00"), EditorStyles.miniLabel, GUILayout.Width(22f));
                EditorGUI.BeginChangeCheck();
                var target = EditorGUILayout.TextField(project.ExportTargets[i] ?? "");
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(project, "编辑行为树导出目标");
                    project.ExportTargets[i] = target.Trim().Replace('\\', '/');
                    project.MarkDirty();
                }
                if (GUILayout.Button("移除", EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    Undo.RecordObject(project, "移除行为树导出目标");
                    project.ExportTargets.RemoveAt(i);
                    project.MarkDirty();
                    GUIUtility.ExitGUI();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(6f);
            if (GUILayout.Button("添加导出目标", GUILayout.Height(24f)))
            {
                Undo.RecordObject(project, "添加行为树导出目标");
                project.ExportTargets.Add("");
                project.MarkDirty();
            }
        }

        private void DrawActions(AuthoringProjectAsset project)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("校验项目", GUILayout.Height(30f)))
            {
                _lastValidationErrors = project.Validate();
                EditorDiagnosticsWindow.Show("行为树项目校验", EditorDiagnostics.AnalyzeProject(project));
            }
            if (GUILayout.Button("导出全部", GUILayout.Height(30f)))
            {
                _lastValidationErrors = project.Validate();
                _lastReport = project.ExportAll(AuthoringMenuUtility.RepositoryRoot);
                AssetDatabase.Refresh();
                var failed = _lastReport.Count(r => r.Status == ExportStatus.Error);
                Debug.Log($"[BtProject] 导出完成：{_lastReport.Count(r => r.Status == ExportStatus.Exported)} 导出 / " +
                          $"{_lastReport.Count(r => r.Status == ExportStatus.Unchanged)} 未变 / {failed} 错误。");
            }
            EditorGUILayout.EndHorizontal();

            if (_lastValidationErrors is { Count: > 0 })
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(
                    string.Join("\n", _lastValidationErrors.Take(3)),
                    MessageType.Warning);
            }

            if (_lastReport != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("上次导出报告", EditorStyles.miniBoldLabel);
                foreach (var entry in _lastReport)
                {
                    var icon = entry.Status switch
                    {
                        ExportStatus.Exported => "✔",
                        ExportStatus.Unchanged => "＝",
                        ExportStatus.Error => "✘",
                        _ => "－",
                    };
                    EditorGUILayout.LabelField(
                        $"{icon} {entry.TreeId} -> {entry.Target}",
                        string.IsNullOrEmpty(entry.Message) ? EditorDisplayText.ExportStatus(entry.Status) : entry.Message,
                        EditorStyles.miniLabel);
                }
            }
        }

        private static void DrawSectionHeader(string title, string count)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(count, EditorStyles.miniLabel, GUILayout.Width(36f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
        }
    }

    /// <summary>行为树统一入口：项目选择、树列表以及常用创建和导出操作。</summary>
    public sealed class AuthoringHubWindow : EditorWindow
    {
        internal const string MainMenuPath = "Window/AbilityKit/行为树编辑器";

        private List<AuthoringProjectAsset> _projects = new();
        private List<AuthoringAsset> _unregisteredTrees = new();
        private AuthoringProjectAsset? _requestedProject;
        private int _projectIndex;
        private Vector2 _scroll;
        private string _status = string.Empty;

        private AuthoringProjectAsset? CurrentProject =>
            _projects != null && _projectIndex >= 0 && _projectIndex < _projects.Count
                ? _projects[_projectIndex]
                : null;

        [MenuItem(MainMenuPath, false, 200)]
        public static void OpenFromMenu()
        {
            Open(Selection.activeObject as AuthoringProjectAsset);
        }

        public static void Open(AuthoringProjectAsset? project)
        {
            var window = GetWindow<AuthoringHubWindow>();
            window.titleContent = new GUIContent("行为树");
            window.minSize = new Vector2(560f, 420f);
            window._requestedProject = project;
            window.RefreshData();
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("行为树");
            minSize = new Vector2(560f, 420f);
            EditorApplication.projectChanged += OnProjectChanged;
            RefreshData();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnFocus()
        {
            RefreshData();
        }

        private void OnProjectChanged()
        {
            RefreshData();
            Repaint();
        }

        private void RefreshData()
        {
            var selected = _requestedProject ?? CurrentProject;
            _requestedProject = null;

            _projects ??= new List<AuthoringProjectAsset>();
            _unregisteredTrees ??= new List<AuthoringAsset>();
            _projects.Clear();
            _projects.AddRange(AuthoringMenuUtility.FindAllProjects());
            _projects.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));

            _projectIndex = selected == null ? 0 : _projects.IndexOf(selected);
            if (_projectIndex < 0) _projectIndex = 0;

            var registered = new HashSet<AuthoringAsset>(
                _projects.SelectMany(project => project.Trees).Where(tree => tree != null));
            _unregisteredTrees.Clear();
            _unregisteredTrees.AddRange(AuthoringMenuUtility.FindUnregistered(registered));
            _unregisteredTrees.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();
            EditorGUILayout.Space(8f);

            var project = CurrentProject;
            if (project == null)
            {
                EditorGUILayout.HelpBox("尚未创建行为树项目配置。", MessageType.Info);
                if (GUILayout.Button("创建项目配置", GUILayout.Height(34f)))
                    CreateProject();
                return;
            }

            DrawProjectSelector(project);
            project = CurrentProject ?? project;
            EditorGUILayout.Space(6f);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawTreeSection(project);
            if (_unregisteredTrees.Count > 0)
            {
                EditorGUILayout.Space(10f);
                DrawUnregisteredSection();
            }
            EditorGUILayout.EndScrollView();

            DrawFooter(project);
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(8f);
            var title = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("行为树", title, GUILayout.Height(26f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                $"{_projects.Count} 个项目  ·  {_projects.Sum(project => project.Trees.Count)} 棵树",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("新建项目配置", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                CreateProject();
            if (GUILayout.Button("新建行为树", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                AuthoringCreateWizard.Open(CurrentProject);
            if (GUILayout.Button("运行时观察", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                EditorWindow.GetWindow<DebugObservationWindow>().Show();
            GUILayout.FlexibleSpace();
            var refresh = EditorGUIUtility.IconContent("Refresh", "刷新项目和行为树列表");
            if (GUILayout.Button(refresh, EditorStyles.toolbarButton, GUILayout.Width(30f)))
                RefreshData();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProjectSelector(AuthoringProjectAsset project)
        {
            EditorGUILayout.BeginHorizontal();
            if (_projects.Count == 1)
            {
                EditorGUILayout.LabelField("当前项目", GUILayout.Width(64f));
                EditorGUILayout.ObjectField(project, typeof(AuthoringProjectAsset), false);
            }
            else
            {
                var names = _projects.Select(item => $"{item.name}  ({item.Trees.Count})").ToArray();
                _projectIndex = EditorGUILayout.Popup("当前项目", _projectIndex, names);
            }
            if (GUILayout.Button("定位", GUILayout.Width(52f)))
            {
                Selection.activeObject = project;
                EditorGUIUtility.PingObject(project);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                $"{project.Trees.Count} 棵树  ·  {project.ExportTargets.Count} 个导出目标  ·  {project.TreeAssetDirectory}",
                EditorStyles.miniLabel);
        }

        private static void DrawTreeSection(AuthoringProjectAsset project)
        {
            DrawHubSectionHeader("项目行为树", project.Trees.Count);
            if (project.Trees.Count == 0)
            {
                EditorGUILayout.LabelField("尚未创建行为树", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(36f));
                return;
            }

            foreach (var tree in project.Trees.Where(tree => tree != null))
                DrawTreeRow(tree, project);
        }

        private void DrawUnregisteredSection()
        {
            DrawHubSectionHeader("未登记行为树", _unregisteredTrees.Count);
            foreach (var tree in _unregisteredTrees)
                DrawTreeRow(tree, null);
        }

        private static void DrawTreeRow(AuthoringAsset tree, AuthoringProjectAsset? project)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(tree, typeof(AuthoringAsset), false);
            var treeId = tree.LoadDocument().Tree.TreeId;
            GUILayout.Label(
                string.IsNullOrWhiteSpace(treeId) ? "未设置 Tree ID" : treeId,
                EditorStyles.miniLabel,
                GUILayout.Width(150f));
            if (GUILayout.Button("编辑", GUILayout.Width(54f)))
                AuthoringGraphWindow.Open(tree, project);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooter(AuthoringProjectAsset project)
        {
            GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("校验项目", GUILayout.Height(30f), GUILayout.Width(100f)))
            {
                var errors = project.Validate();
                _status = errors.Count == 0 ? "校验通过" : $"发现 {errors.Count} 个配置问题";
                EditorDiagnosticsWindow.Show("行为树项目校验", EditorDiagnostics.AnalyzeProject(project));
            }
            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_status))
                GUILayout.Label(_status, EditorStyles.miniLabel);
            if (GUILayout.Button("导出全部", GUILayout.Height(30f), GUILayout.Width(112f)))
            {
                var report = project.ExportAll(AuthoringMenuUtility.RepositoryRoot);
                AssetDatabase.Refresh();
                var errors = report.Count(entry => entry.Status == ExportStatus.Error);
                _status = errors == 0
                    ? $"导出完成：{report.Count(entry => entry.Status == ExportStatus.Exported)} 个更新"
                    : $"导出失败：{errors} 个错误";
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6f);
        }

        private void CreateProject()
        {
            var project = AuthoringMenuUtility.CreateProjectAsset();
            if (project == null) return;
            _requestedProject = project;
            RefreshData();
        }

        private static void DrawHubSectionHeader(string title, int count)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(count.ToString(), EditorStyles.miniLabel, GUILayout.Width(28f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3f);
        }
    }

    /// <summary>共享工具：仓库根与菜单入口。</summary>
    internal static class AuthoringMenuUtility
    {
        public static string RepositoryRoot =>
            ResolveRepositoryRoot(Application.dataPath);

        internal static string ResolveRepositoryRoot(string startPath)
        {
            var current = new DirectoryInfo(Path.GetFullPath(startPath));
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git"))
                    || File.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return current.FullName;
                }
                current = current.Parent;
            }

            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }

        public static AuthoringProjectAsset? FindOwningProject(AuthoringAsset asset)
        {
            if (asset == null) return null;
            var owners = FindAllProjects()
                .Where(project => project.Trees.Contains(asset))
                .ToList();
            if (owners.Count == 1) return owners[0];
            if (owners.Count > 1)
            {
                Debug.LogWarning(
                    $"[BtProject] '{asset.name}' 同时注册到多个行为树项目；请从目标项目的检查器打开以确定导出配置。");
            }
            return null;
        }

        [MenuItem("Assets/Create/AbilityKit/行为树/项目配置", false, 100)]
        private static void CreateProjectFromMenu()
        {
            CreateProjectAsset();
        }

        internal static AuthoringProjectAsset? CreateProjectAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject("创建行为树项目", "BtAuthoringProject", "asset", "");
            if (string.IsNullOrEmpty(path)) return null;
            var asset = ScriptableObject.CreateInstance<AuthoringProjectAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        [MenuItem("Assets/Create/AbilityKit/行为树/新建行为树...", false, 101)]
        private static void CreateTreeFromMenu()
        {
            AuthoringCreateWizard.Open(Selection.activeObject as AuthoringProjectAsset);
        }

        [MenuItem("Assets/AbilityKit/行为树/导出全部")]
        private static void ExportAll()
        {
            var projects = FindAllProjects();
            if (projects.Count == 0)
            {
                EditorUtility.DisplayDialog("批量导出", "未找到任何行为树项目配置资产。", "确定");
                return;
            }

            var report = new List<ExportReportEntry>();
            var registered = new HashSet<AuthoringAsset>();
            foreach (var project in projects)
            {
                registered.UnionWith(project.Trees.Where(t => t != null));
                report.AddRange(project.ExportAll(RepositoryRoot));
            }

            var unregistered = FindUnregistered(registered);
            AssetDatabase.Refresh();

            var exported = report.Count(r => r.Status == ExportStatus.Exported);
            var unchanged = report.Count(r => r.Status == ExportStatus.Unchanged);
            var errors = report.Count(r => r.Status == ExportStatus.Error);
            var message = $"导出 {exported} / 未变 {unchanged} / 错误 {errors}。";
            if (unregistered.Count > 0)
            {
                message += $"\n\n{unregistered.Count} 棵树未注册进任何项目（不导出）：\n" +
                           string.Join("\n", unregistered.Select(u => AssetDatabase.GetAssetPath(u)).ToArray());
            }
            if (errors > 0)
            {
                message += "\n\n错误明细：\n" + string.Join("\n",
                    report.Where(r => r.Status == ExportStatus.Error)
                        .Select(r => $"{r.TreeId} -> {r.Target}: {r.Message}")
                        .ToArray());
            }
            Debug.Log("[BtProject] 批量导出：" + message);
            EditorUtility.DisplayDialog("批量导出", message, "确定");
        }

        [MenuItem("Assets/AbilityKit/行为树/校验全部")]
        private static void ValidateAll()
        {
            EditorDiagnosticsWindow.Show("行为树全部校验", EditorDiagnostics.AnalyzeProjects(FindAllProjects()));
        }

        public static List<AuthoringProjectAsset> FindAllProjects()
        {
            var result = new List<AuthoringProjectAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:AuthoringProjectAsset"))
            {
                var project = AssetDatabase.LoadAssetAtPath<AuthoringProjectAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (project != null) result.Add(project);
            }
            return result;
        }

        public static List<AuthoringAsset> FindUnregistered(HashSet<AuthoringAsset> registered)
        {
            var result = new List<AuthoringAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:AuthoringAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && !registered.Contains(asset)) result.Add(asset);
            }
            return result;
        }

        [UnityEditor.Callbacks.OnOpenAsset(0)]
        private static bool OpenBehaviorTreeAsset(int instanceId, int line)
        {
            var selected = EditorUtility.InstanceIDToObject(instanceId);
            if (selected is AuthoringAsset tree)
            {
                AuthoringGraphWindow.Open(tree);
                return true;
            }
            if (selected is AuthoringProjectAsset project)
            {
                AuthoringHubWindow.Open(project);
                return true;
            }
            return false;
        }
    }
}
