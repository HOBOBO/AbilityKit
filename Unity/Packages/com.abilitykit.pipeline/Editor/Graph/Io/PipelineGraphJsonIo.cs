#if UNITY_EDITOR

using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Pipeline.Editor
{
    internal static class PipelineGraphJsonIo
    {
        [MenuItem("AbilityKit/Pipeline/Graph/Export Selected GraphAsset -> Json")]
        private static void ExportSelected()
        {
            var asset = Selection.activeObject as PipelineGraphAsset;
            if (asset == null)
            {
                Debug.LogError("[PipelineGraphJsonIo] Please select a PipelineGraphAsset.");
                return;
            }

            var defaultName = string.IsNullOrEmpty(asset.GraphId) ? asset.name : asset.GraphId;
            var path = EditorUtility.SaveFilePanel(
                title: "Export Pipeline Graph Json",
                directory: Application.dataPath,
                defaultName: defaultName,
                extension: "json");

            if (string.IsNullOrEmpty(path)) return;

            var dto = asset.ToDto();
            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            File.WriteAllText(path, json);

            AssetDatabase.Refresh();
            Debug.Log($"[PipelineGraphJsonIo] Exported: {path}");
        }

        [MenuItem("AbilityKit/Pipeline/Graph/Import Json -> Selected GraphAsset")]
        private static void ImportToSelected()
        {
            var asset = Selection.activeObject as PipelineGraphAsset;
            if (asset == null)
            {
                Debug.LogError("[PipelineGraphJsonIo] Please select a PipelineGraphAsset.");
                return;
            }

            var path = EditorUtility.OpenFilePanel(
                title: "Import Pipeline Graph Json",
                directory: Application.dataPath,
                extension: "json");

            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path))
            {
                Debug.LogError($"[PipelineGraphJsonIo] File not found: {path}");
                return;
            }

            PipelineGraphDto dto;
            try
            {
                var json = File.ReadAllText(path);
                dto = JsonConvert.DeserializeObject<PipelineGraphDto>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PipelineGraphJsonIo] Failed to parse json: {path}\n{e}");
                return;
            }

            if (dto == null)
            {
                Debug.LogError($"[PipelineGraphJsonIo] Json parsed null dto: {path}");
                return;
            }

            Undo.RecordObject(asset, "Import Pipeline Graph Json");
            asset.ApplyDto(dto);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PipelineGraphJsonIo] Imported into asset: {AssetDatabase.GetAssetPath(asset)}");
        }

        [MenuItem("Assets/Create/AbilityKit/Pipeline Graph Asset")]
        private static void CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<PipelineGraphAsset>();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/PipelineGraph.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }
    }
}

#endif
