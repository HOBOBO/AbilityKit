using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Export;
using AbilityKit.Editor.Platform.UI;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 授权资产 Inspector：树概要、黑板 schema、校验、源同步、运行时导出入口。
    /// 图的节点级编辑在 <see cref="BtAuthoringGraphWindow"/> 中进行。
    /// 注意基类须写全限定名——本命名空间以 .Editor 结尾，简单名 Editor 会解析到命名空间。
    /// </summary>
    [CustomEditor(typeof(BtAuthoringAsset))]
    public sealed class BtAuthoringAssetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (BtAuthoringAsset)target;
            var document = asset.LoadDocument();

            EditorGUILayout.LabelField("Behavior Tree Authoring", EditorStyles.boldLabel);

            DrawTreeSummary(document);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Blackboard Schema", EditorStyles.boldLabel);
            DrawBlackboard(document.Tree.Blackboard.Keys);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Graph Editor"))
            {
                BtAuthoringGraphWindow.Open(asset);
            }
            if (GUILayout.Button("Open Runtime Observation"))
            {
                EditorWindow.GetWindow<BtDebugObservationWindow>().Show();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            DrawSync(asset);
            DrawRuntimeExport(asset);
        }

        private static void DrawTreeSummary(BtAuthoringSourceDocument document)
        {
            var tree = document.Tree;
            EditorGUILayout.LabelField("Tree Id", tree.TreeId);
            EditorGUILayout.LabelField("Root", tree.RootNodeId);
            EditorGUILayout.LabelField("Nodes", tree.Nodes.Count.ToString());
            EditorGUILayout.LabelField("Layout Entries", document.Layout.Count.ToString());
            EditorGUILayout.LabelField("Groups", document.Groups.Count.ToString());
        }

        private static void DrawBlackboard(List<BtBlackboardKeyDefinition> keys)
        {
            if (keys.Count == 0)
            {
                EditorGUILayout.LabelField("(no keys)", EditorStyles.miniLabel);
                return;
            }
            foreach (var key in keys)
            {
                EditorGUILayout.LabelField(key.Name, key.Type.ToString(), EditorStyles.miniLabel);
            }
        }

        private static void DrawSync(BtAuthoringAsset asset)
        {
            var inspection = BtAuthoringSourceSync.Inspect(asset);
            var sourcePath = inspection.SourcePath;
            EditorImGuiControls.DrawSourceSyncCard(
                new EditorSourceSyncCardModel(
                    inspection.PlatformInspection,
                    import: () => ImportSource(asset),
                    export: () => ExportSource(asset),
                    copyPath: string.IsNullOrWhiteSpace(sourcePath)
                        ? null
                        : () => EditorGUIUtility.systemCopyBuffer = sourcePath,
                    revealPath: CanReveal(sourcePath)
                        ? () => EditorUtility.RevealInFinder(
                            BtAuthoringSourceSync.ResolvePath(sourcePath))
                        : null));
        }

        private static void ImportSource(BtAuthoringAsset asset)
        {
            var path = PickImportPath(asset);
            if (string.IsNullOrEmpty(path)) return;
            RunSyncOperation(
                "Import Source",
                () => BtAuthoringSourceSync.Import(asset, path),
                () => BtAuthoringSourceSync.Import(asset, path, force: true));
        }

        private static void ExportSource(BtAuthoringAsset asset)
        {
            var path = PickExportPath(asset);
            if (string.IsNullOrEmpty(path)) return;
            RunSyncOperation(
                "Export Source",
                () => BtAuthoringSourceSync.Export(asset, path),
                () => BtAuthoringSourceSync.Export(asset, path, force: true));
        }

        private static bool CanReveal(string sourcePath)
        {
            return !string.IsNullOrWhiteSpace(sourcePath) &&
                System.IO.File.Exists(
                    BtAuthoringSourceSync.ResolvePath(sourcePath));
        }

        private static void DrawRuntimeExport(BtAuthoringAsset asset)
        {
            EditorGUILayout.LabelField("Runtime Export", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Path", asset.ResolveRuntimeExportPath(asset.LoadDocument().Tree.TreeId));
            if (GUILayout.Button("Export Runtime JSON"))
            {
                var report = BtAuthoringRuntimeExporter.Export(asset);
                var outputs = report.Artifacts.Select(artifact => artifact.Path).ToArray();
                if (report.Success)
                {
                    var verb = report.ExportedCount > 0 ? "Exported" : "Unchanged";
                    Debug.Log("[BtAuthoring] Runtime export succeeded: " + string.Join(", ", outputs));
                    EditorUtility.DisplayDialog(
                        "Runtime Export",
                        verb + ":\n" + string.Join("\n", outputs),
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Runtime Export Failed",
                        string.Join("\n", report.Messages),
                        "OK");
                }
            }
        }

        private static string ExistingSourcePath(BtAuthoringAsset asset)
        {
            var existing = BtAuthoringSourceSync.ResolvePath(asset.SourceJsonPath);
            if (!string.IsNullOrEmpty(asset.SourceJsonPath) && System.IO.File.Exists(existing))
            {
                return asset.SourceJsonPath;
            }
            return "";
        }

        private static string PickImportPath(BtAuthoringAsset asset)
        {
            var existing = ExistingSourcePath(asset);
            if (!string.IsNullOrEmpty(existing)) return existing;
            var chosen = EditorUtility.OpenFilePanel("Behavior Tree Authoring JSON", Application.dataPath, "json");
            return string.IsNullOrEmpty(chosen) ? "" : chosen;
        }

        private static string PickExportPath(BtAuthoringAsset asset)
        {
            var existing = ExistingSourcePath(asset);
            if (!string.IsNullOrEmpty(existing)) return existing;
            var treeId = asset.LoadDocument().Tree.TreeId;
            var fileName = string.IsNullOrWhiteSpace(treeId) ? asset.name : treeId;
            var chosen = EditorUtility.SaveFilePanel(
                "Behavior Tree Authoring JSON", Application.dataPath, fileName, "json");
            return string.IsNullOrEmpty(chosen) ? "" : chosen;
        }

        private static void RunSyncOperation(
            string operation,
            System.Func<BtAuthoringSyncResult> execute,
            System.Func<BtAuthoringSyncResult> force)
        {
            var result = execute();
            if (!result.Success && result.CanForce
                && EditorUtility.DisplayDialog(
                    operation + " Conflict",
                    result.Message,
                    "Overwrite",
                    "Cancel"))
            {
                result = force();
            }
            ShowResult(result);
        }

        private static void ShowResult(BtAuthoringSyncResult result)
        {
            if (result.Success)
            {
                EditorUtility.DisplayDialog("Source Sync", result.Message, "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Source Sync Failed", result.Message, "OK");
            }
        }
    }
}
