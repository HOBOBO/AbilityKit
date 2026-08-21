using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
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
            EditorGUILayout.LabelField("Source Sync", EditorStyles.boldLabel);
            var inspection = BtAuthoringSourceSync.Inspect(asset);
            switch (inspection.State)
            {
                case BtAuthoringSyncState.JsonChanged:
                    EditorGUILayout.HelpBox("External changes detected. Import to apply.", MessageType.Warning);
                    break;
                case BtAuthoringSyncState.Conflict:
                    EditorGUILayout.HelpBox("Asset and source file have diverged.", MessageType.Error);
                    break;
                case BtAuthoringSyncState.InvalidSource:
                    EditorGUILayout.HelpBox("Source file is missing.", MessageType.Error);
                    break;
            }

            EditorGUILayout.LabelField("Path", string.IsNullOrEmpty(asset.SourceJsonPath) ? "<unbound>" : asset.SourceJsonPath);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import Source"))
            {
                var path = PickSourcePath(asset);
                if (!string.IsNullOrEmpty(path))
                {
                    var result = BtAuthoringSourceSync.Import(asset, path);
                    ShowResult(result);
                }
            }
            if (GUILayout.Button("Export Source"))
            {
                var path = PickSourcePath(asset);
                if (!string.IsNullOrEmpty(path))
                {
                    var result = BtAuthoringSourceSync.Export(asset, path);
                    ShowResult(result);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawRuntimeExport(BtAuthoringAsset asset)
        {
            EditorGUILayout.LabelField("Runtime Export", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Path", asset.ResolveRuntimeExportPath(asset.LoadDocument().Tree.TreeId));
            if (GUILayout.Button("Export Runtime JSON"))
            {
                var ok = BtAuthoringRuntimeExporter.Export(asset, out var outputs, out var errors);
                if (ok)
                {
                    Debug.Log("[BtAuthoring] Runtime export succeeded: " + string.Join(", ", outputs));
                    EditorUtility.DisplayDialog("Runtime Export", "Exported:\n" + string.Join("\n", outputs), "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Runtime Export Failed", string.Join("\n", errors), "OK");
                }
            }
        }

        private static string PickSourcePath(BtAuthoringAsset asset)
        {
            var existing = BtAuthoringSourceSync.ResolvePath(asset.SourceJsonPath);
            if (!string.IsNullOrEmpty(asset.SourceJsonPath) && System.IO.File.Exists(existing))
            {
                return asset.SourceJsonPath;
            }

            var chosen = EditorUtility.OpenFilePanel("Behavior Tree Authoring JSON", Application.dataPath, "json");
            return string.IsNullOrEmpty(chosen) ? "" : chosen;
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
