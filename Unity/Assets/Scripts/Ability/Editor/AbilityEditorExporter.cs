using System.Collections.Generic;
using System.IO;
using AbilityKit.Configs;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    internal static class AbilityEditorExporter
    {
        public static void Export(AbilityModuleSO editor)
        {
            if (editor == null) return;

            if (editor.RuntimeAsset == null)
            {
                var editorPath = AssetDatabase.GetAssetPath(editor);
                var runtimePath = BuildRuntimePath(editorPath, editor.name);

                var runtime = ScriptableObject.CreateInstance<AbilityRuntimeSO>();
                AssetDatabase.CreateAsset(runtime, runtimePath);
                editor.RuntimeAsset = runtime;
                EditorUtility.SetDirty(editor);
            }

            var rtAsset = editor.RuntimeAsset;
            rtAsset.AbilityId = editor.AbilityId;
            rtAsset.Triggers = new List<TriggerRuntimeConfig>();

            if (editor.Triggers != null)
            {
                for (int i = 0; i < editor.Triggers.Count; i++)
                {
                    var t = editor.Triggers[i];
                    if (t == null || !t.Enabled) continue;
                    rtAsset.Triggers.Add(t.ToRuntime());
                }
            }

            EditorUtility.SetDirty(rtAsset);
            AssetDatabase.SaveAssets();
        }

        public static void ExportAll(string rootFolder)
        {
            if (string.IsNullOrEmpty(rootFolder)) rootFolder = "Assets";

            var guids = AssetDatabase.FindAssets("t:AbilityModuleSO", new[] { rootFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<AbilityModuleSO>(path);
                if (asset == null) continue;
                Export(asset);
            }
        }

        private static string BuildRuntimePath(string editorAssetPath, string assetName)
        {
            var dir = Path.GetDirectoryName(editorAssetPath)?.Replace("\\", "/") ?? "Assets";

            string runtimeDir;
            if (dir.EndsWith("/Editor"))
            {
                runtimeDir = dir.Substring(0, dir.Length - "/Editor".Length) + "/Runtime";
            }
            else
            {
                runtimeDir = dir + "/Runtime";
            }

            EnsureFolder(runtimeDir);
            return runtimeDir + "/" + assetName + "_Runtime.asset";
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            var parts = assetFolder.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
