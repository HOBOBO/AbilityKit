using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Export;
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
    /// 授权资产 Inspector：树概要、黑板 schema、校验、源同步、运行时导出入口。
    /// 图的节点级编辑在 <see cref="AuthoringGraphWindow"/> 中进行。
    /// 注意基类须写全限定名——本命名空间以 .Editor 结尾，简单名 Editor 会解析到命名空间。
    /// </summary>
    [CustomEditor(typeof(AuthoringAsset))]
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringAssetInspector")]
    public sealed class AuthoringAssetInspector : UnityEditor.Editor
    {
        private bool _showSourceSync;

        public override void OnInspectorGUI()
        {
            var asset = (AuthoringAsset)target;
            var document = asset.LoadDocument();
            var project = AuthoringMenuUtility.FindOwningProject(asset);

            DrawHeader(asset, document);

            if (GUILayout.Button("打开行为树编辑器", GUILayout.Height(34f)))
                AuthoringGraphWindow.Open(asset, project);

            EditorGUILayout.BeginHorizontal();
            if (project != null && GUILayout.Button("打开项目配置", GUILayout.Height(24f)))
            {
                Selection.activeObject = project;
                EditorGUIUtility.PingObject(project);
            }
            if (GUILayout.Button("运行时观察", GUILayout.Height(24f)))
                EditorWindow.GetWindow<DebugObservationWindow>().Show();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("基本信息", EditorStyles.boldLabel);
            DrawTreeSummary(document, project);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("黑板", EditorStyles.boldLabel);
            DrawBlackboard(document.Tree.Blackboard.Keys);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6f);
            DrawRuntimeExport(asset, project);

            EditorGUILayout.Space(6f);
            _showSourceSync = EditorGUILayout.Foldout(_showSourceSync, "源文件同步", true);
            if (_showSourceSync)
                DrawSync(asset);
        }

        private static void DrawHeader(AuthoringAsset asset, AuthoringSourceDocument document)
        {
            EditorGUILayout.Space(4f);
            var title = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
            };
            EditorGUILayout.LabelField(asset.name, title, GUILayout.Height(22f));
            EditorGUILayout.LabelField(
                $"{document.Tree.TreeId}  ·  {document.Tree.Nodes.Count} 个节点",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(8f);
        }

        private static void DrawTreeSummary(
            AuthoringSourceDocument document,
            AuthoringProjectAsset? project)
        {
            var tree = document.Tree;
            EditorGUILayout.LabelField("Tree ID", tree.TreeId);
            EditorGUILayout.LabelField("根节点", string.IsNullOrWhiteSpace(tree.RootNodeId) ? "未设置" : tree.RootNodeId);
            EditorGUILayout.LabelField("节点", tree.Nodes.Count.ToString());
            EditorGUILayout.LabelField("分组", document.Groups.Count.ToString());
            EditorGUILayout.LabelField("所属项目", project == null ? "未登记" : project.name);
        }

        private static void DrawBlackboard(List<BlackboardKeyDefinition> keys)
        {
            if (keys.Count == 0)
            {
                EditorGUILayout.LabelField("暂无黑板键", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(24f));
                return;
            }
            foreach (var key in keys)
            {
                EditorGUILayout.LabelField(key.Name, key.Type.ToString(), EditorStyles.miniLabel);
            }
        }

        private static void DrawSync(AuthoringAsset asset)
        {
            var inspection = AuthoringSourceSync.Inspect(asset);
            var sourcePath = inspection.SourcePath;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("源文件状态", EditorStyles.boldLabel);

            var previousColor = GUI.color;
            GUI.color = SyncStateColor(inspection.State);
            EditorGUILayout.LabelField(SyncStateLabel(inspection.State), EditorStyles.miniBoldLabel);
            GUI.color = previousColor;

            EditorGUILayout.HelpBox(
                SyncStatusMessage(inspection),
                SyncMessageType(inspection.State));
            EditorGUILayout.LabelField("源文件路径", EditorStyles.miniLabel);
            EditorGUILayout.SelectableLabel(
                string.IsNullOrWhiteSpace(sourcePath) ? "尚未绑定" : sourcePath,
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("从源文件导入", EditorStyles.miniButtonLeft))
                ImportSource(asset);
            if (GUILayout.Button("导出到源文件", EditorStyles.miniButtonRight))
                ExportSource(asset);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(sourcePath)))
            {
                if (GUILayout.Button("复制路径", EditorStyles.miniButtonLeft))
                    EditorGUIUtility.systemCopyBuffer = sourcePath;
            }
            using (new EditorGUI.DisabledScope(!CanReveal(sourcePath)))
            {
                if (GUILayout.Button("在资源管理器中显示", EditorStyles.miniButtonRight))
                    EditorUtility.RevealInFinder(AuthoringSourceSync.ResolvePath(sourcePath));
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static string SyncStateLabel(AuthoringSyncState state) => state switch
        {
            AuthoringSyncState.InSync => "已同步",
            AuthoringSyncState.AssetChanged => "资产有未导出的修改",
            AuthoringSyncState.JsonChanged => "源文件有未导入的修改",
            AuthoringSyncState.Conflict => "资产与源文件存在冲突",
            AuthoringSyncState.InvalidSource => "源文件无效",
            AuthoringSyncState.Untracked => "尚未绑定源文件",
            AuthoringSyncState.SourceMissing => "源文件不存在",
            _ => "未知状态",
        };

        private static string SyncStatusMessage(AuthoringSyncInspection inspection)
        {
            if (inspection.State == AuthoringSyncState.InvalidSource)
            {
                var error = inspection.PlatformInspection.Snapshot.Error;
                return string.IsNullOrWhiteSpace(error)
                    ? "无法读取或解析当前源文件。"
                    : "无法读取或解析当前源文件：" + error;
            }

            return inspection.State switch
            {
                AuthoringSyncState.InSync => "行为树资产与外部 JSON 源文件内容一致。",
                AuthoringSyncState.AssetChanged => "行为树资产已修改，可以导出到源文件。",
                AuthoringSyncState.JsonChanged => "外部 JSON 源文件已修改，可以导入到行为树资产。",
                AuthoringSyncState.Conflict => "两侧都已修改。导入或导出前请确认要保留哪一侧。",
                AuthoringSyncState.Untracked => "选择导入或导出后，会建立行为树资产与外部 JSON 的绑定。",
                AuthoringSyncState.SourceMissing => "绑定的外部 JSON 源文件不存在，可以重新选择文件或导出创建。",
                _ => "无法识别当前同步状态。",
            };
        }

        private static MessageType SyncMessageType(AuthoringSyncState state) => state switch
        {
            AuthoringSyncState.Conflict => MessageType.Error,
            AuthoringSyncState.InvalidSource => MessageType.Error,
            AuthoringSyncState.SourceMissing => MessageType.Error,
            AuthoringSyncState.AssetChanged => MessageType.Warning,
            AuthoringSyncState.JsonChanged => MessageType.Warning,
            AuthoringSyncState.Untracked => MessageType.Warning,
            _ => MessageType.Info,
        };

        private static Color SyncStateColor(AuthoringSyncState state) => state switch
        {
            AuthoringSyncState.InSync => new Color(0.35f, 0.75f, 0.45f),
            AuthoringSyncState.Conflict => new Color(1f, 0.4f, 0.35f),
            AuthoringSyncState.InvalidSource => new Color(1f, 0.4f, 0.35f),
            AuthoringSyncState.SourceMissing => new Color(1f, 0.4f, 0.35f),
            _ => new Color(1f, 0.72f, 0.25f),
        };

        private static void ImportSource(AuthoringAsset asset)
        {
            var path = PickImportPath(asset);
            if (string.IsNullOrEmpty(path)) return;
            RunSyncOperation(
                "导入源文件",
                () => AuthoringSourceSync.Import(asset, path),
                () => AuthoringSourceSync.Import(asset, path, force: true));
        }

        private static void ExportSource(AuthoringAsset asset)
        {
            var path = PickExportPath(asset);
            if (string.IsNullOrEmpty(path)) return;
            RunSyncOperation(
                "导出源文件",
                () => AuthoringSourceSync.Export(asset, path),
                () => AuthoringSourceSync.Export(asset, path, force: true));
        }

        private static bool CanReveal(string sourcePath)
        {
            return !string.IsNullOrWhiteSpace(sourcePath) &&
                System.IO.File.Exists(
                    AuthoringSourceSync.ResolvePath(sourcePath));
        }

        private static void DrawRuntimeExport(AuthoringAsset asset, AuthoringProjectAsset? project)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("运行时导出", EditorStyles.boldLabel);
            if (project != null)
            {
                EditorGUILayout.LabelField(
                    $"使用项目配置  ·  {project.ExportTargets.Count} 个目标",
                    EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField(
                    asset.ResolveRuntimeExportPath(asset.LoadDocument().Tree.TreeId),
                    EditorStyles.miniLabel);
            }

            var label = project == null ? "导出运行时 JSON" : "按项目配置导出";
            if (GUILayout.Button(label, GUILayout.Height(28f)))
            {
                if (project != null)
                {
                    var projectReport = project.ExportTree(asset, AuthoringMenuUtility.RepositoryRoot);
                    AssetDatabase.Refresh();
                    var projectErrors = projectReport.Where(entry => entry.Status == ExportStatus.Error).ToList();
                    EditorUtility.DisplayDialog(
                        projectErrors.Count == 0 ? "运行时导出" : "运行时导出失败",
                        projectErrors.Count == 0
                            ? string.Join("\n", projectReport.Select(entry =>
                                $"{EditorDisplayText.ExportStatus(entry.Status)}：{entry.TreeId} -> {entry.Target}"))
                            : string.Join("\n", projectErrors.Select(entry => entry.Message)),
                        "确定");
                    EditorGUILayout.EndVertical();
                    return;
                }

                var report = AuthoringRuntimeExporter.Export(asset);
                var outputs = report.Artifacts.Select(artifact => artifact.Path).ToArray();
                if (report.Success)
                {
                    var verb = report.ExportedCount > 0 ? "已导出" : "内容未变化";
                    Debug.Log("[BtAuthoring] 运行时配置导出成功：" + string.Join(", ", outputs));
                    EditorUtility.DisplayDialog(
                        "运行时配置导出",
                        verb + "：\n" + string.Join("\n", outputs),
                        "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "运行时配置导出失败",
                        string.Join("\n", report.Messages),
                        "确定");
                }
            }
            EditorGUILayout.EndVertical();
        }

        private static string ExistingSourcePath(AuthoringAsset asset)
        {
            var existing = AuthoringSourceSync.ResolvePath(asset.SourceJsonPath);
            if (!string.IsNullOrEmpty(asset.SourceJsonPath) && System.IO.File.Exists(existing))
            {
                return asset.SourceJsonPath;
            }
            return "";
        }

        private static string PickImportPath(AuthoringAsset asset)
        {
            var existing = ExistingSourcePath(asset);
            if (!string.IsNullOrEmpty(existing)) return existing;
            var chosen = EditorUtility.OpenFilePanel("选择行为树编辑 JSON", Application.dataPath, "json");
            return string.IsNullOrEmpty(chosen) ? "" : chosen;
        }

        private static string PickExportPath(AuthoringAsset asset)
        {
            var existing = ExistingSourcePath(asset);
            if (!string.IsNullOrEmpty(existing)) return existing;
            var treeId = asset.LoadDocument().Tree.TreeId;
            var fileName = string.IsNullOrWhiteSpace(treeId) ? asset.name : treeId;
            var chosen = EditorUtility.SaveFilePanel(
                "保存行为树编辑 JSON", Application.dataPath, fileName, "json");
            return string.IsNullOrEmpty(chosen) ? "" : chosen;
        }

        private static void RunSyncOperation(
            string operation,
            System.Func<AuthoringSyncResult> execute,
            System.Func<AuthoringSyncResult> force)
        {
            var result = execute();
            if (!result.Success && result.CanForce
                && EditorUtility.DisplayDialog(
                    operation + "冲突",
                    result.Message,
                    "覆盖",
                    "取消"))
            {
                result = force();
            }
            ShowResult(result);
        }

        private static void ShowResult(AuthoringSyncResult result)
        {
            if (result.Success)
            {
                EditorUtility.DisplayDialog("源文件同步", result.Message, "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("源文件同步失败", result.Message, "确定");
            }
        }
    }
}
