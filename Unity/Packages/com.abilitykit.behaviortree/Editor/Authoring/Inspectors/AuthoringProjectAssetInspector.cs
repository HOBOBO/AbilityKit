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
    /// 项目目录资产 Inspector：树清单（扫描添加/移除）、导出目标编辑、项目校验、一键导出 + 报告。
    /// 基类须写全限定名——本命名空间以 .Editor 结尾，简单名 Editor 会解析到命名空间。
    /// </summary>
    [CustomEditor(typeof(AuthoringProjectAsset))]
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringProjectAssetInspector")]
    public sealed class AuthoringProjectAssetInspector : UnityEditor.Editor
    {
        private List<ExportReportEntry>? _lastReport;

        public override void OnInspectorGUI()
        {
            var project = (AuthoringProjectAsset)target;

            EditorGUILayout.LabelField("Behavior Tree Project", EditorStyles.boldLabel);

            DrawTreeList(project);
            EditorGUILayout.Space(6);
            DrawExportTargets(project);
            EditorGUILayout.Space(6);
            DrawActions(project);
        }

        private static void DrawTreeList(AuthoringProjectAsset project)
        {
            EditorGUILayout.LabelField($"Trees ({project.Trees.Count})", EditorStyles.boldLabel);

            for (var i = 0; i < project.Trees.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var tree = (AuthoringAsset)EditorGUILayout.ObjectField(project.Trees[i], typeof(AuthoringAsset), false);
                if (!ReferenceEquals(tree, project.Trees[i]))
                {
                    project.Trees[i] = tree;
                    project.MarkDirty();
                }
                if (tree != null && GUILayout.Button("编辑", EditorStyles.miniButtonLeft, GUILayout.Width(44f)))
                {
                    AuthoringGraphWindow.Open(tree);
                }
                if (GUILayout.Button("移除", EditorStyles.miniButtonRight, GUILayout.Width(44f)))
                {
                    project.Trees.RemoveAt(i);
                    project.MarkDirty();
                    GUIUtility.ExitGUI();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("扫描添加（发现未注册的授权资产）"))
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
            EditorGUILayout.LabelField($"Export Targets ({project.ExportTargets.Count})", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "相对仓库根的目录列表；导出扇出到全部目标（一份源同时落 Unity Resources 与 console Configs）。文件名 = TreeId.json。",
                MessageType.Info);

            for (var i = 0; i < project.ExportTargets.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                project.ExportTargets[i] = EditorGUILayout.TextField(project.ExportTargets[i] ?? "");
                if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(20f)))
                {
                    project.ExportTargets.RemoveAt(i);
                    project.MarkDirty();
                    GUIUtility.ExitGUI();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ 添加目标", EditorStyles.miniButton))
            {
                project.ExportTargets.Add("");
                project.MarkDirty();
            }
        }

        private void DrawActions(AuthoringProjectAsset project)
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("校验", EditorStyles.miniButtonLeft))
            {
                var errors = project.Validate();
                EditorUtility.DisplayDialog(
                    "项目校验",
                    errors.Count == 0 ? "校验通过。" : string.Join("\n", errors),
                    "OK");
            }
            if (GUILayout.Button("导出全部", EditorStyles.miniButtonRight))
            {
                _lastReport = project.ExportAll(AuthoringMenuUtility.RepositoryRoot);
                AssetDatabase.Refresh();
                var failed = _lastReport.Count(r => r.Status == ExportStatus.Error);
                Debug.Log($"[BtProject] 导出完成：{_lastReport.Count(r => r.Status == ExportStatus.Exported)} 导出 / " +
                          $"{_lastReport.Count(r => r.Status == ExportStatus.Unchanged)} 未变 / {failed} 错误。");
            }
            EditorGUILayout.EndHorizontal();

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
                        string.IsNullOrEmpty(entry.Message) ? entry.Status.ToString() : entry.Message,
                        EditorStyles.miniLabel);
                }
            }
        }
    }

    /// <summary>共享工具：仓库根与菜单入口。</summary>
    internal static class AuthoringMenuUtility
    {
        public static string RepositoryRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

        [MenuItem("Assets/AbilityKit/Behavior Tree/Create Tree Wizard")]
        private static void OpenWizard() => AuthoringCreateWizard.Open();

        [MenuItem("Assets/AbilityKit/Behavior Tree/Create Project Asset")]
        private static void CreateProject()
        {
            var path = EditorUtility.SaveFilePanelInProject("创建行为树项目", "BtAuthoringProject", "asset", "");
            if (string.IsNullOrEmpty(path)) return;
            var asset = ScriptableObject.CreateInstance<AuthoringProjectAsset>();
            AssetDatabase.CreateAsset(asset, path);
            Selection.activeObject = asset;
        }

        [MenuItem("Assets/AbilityKit/Behavior Tree/Export All")]
        private static void ExportAll()
        {
            var projects = FindAllProjects();
            if (projects.Count == 0)
            {
                EditorUtility.DisplayDialog("批量导出", "未找到任何 AuthoringProjectAsset。", "OK");
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
            Debug.Log("[BtProject] Export All: " + message);
            EditorUtility.DisplayDialog("批量导出", message, "OK");
        }

        [MenuItem("Assets/AbilityKit/Behavior Tree/Validate All")]
        private static void ValidateAll()
        {
            var projects = FindAllProjects();
            var failures = new List<string>();
            var registered = new HashSet<AuthoringAsset>();
            foreach (var project in projects)
            {
                registered.UnionWith(project.Trees.Where(t => t != null));
                var errors = project.Validate();
                if (errors.Count > 0)
                {
                    failures.Add(project.name + ":\n  " + string.Join("\n  ", errors));
                }
            }
            EditorUtility.DisplayDialog(
                "项目校验",
                failures.Count == 0 ? "全部通过。" : string.Join("\n", failures),
                "OK");
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
    }
}
