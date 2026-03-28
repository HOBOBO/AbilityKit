#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class VfxJsonExporter
    {
        private const string OutputResourcesDir = "vfx";
        private const string OutputFileWithoutExt = "vfx";

        [MenuItem("AbilityKit/Vfx/Export Vfx Json")]
        public static void ExportSelected()
        {
            var folder = TryGetSelectedFolderPath();
            ExportFromFolder(folder);
        }

        [MenuItem("AbilityKit/Vfx/Export Vfx Json (Assets)")]
        public static void ExportAll()
        {
            ExportFromFolder("Assets");
        }

        public static void ExportFromFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) assetFolder = "Assets";

            var outputDir = Path.Combine(Application.dataPath, "Resources", OutputResourcesDir);
            Directory.CreateDirectory(outputDir);

            var entries = LoadEntries(assetFolder);
            ValidateUniqueById(entries);

            var json = JsonConvert.SerializeObject(entries ?? Array.Empty<VfxDTO>(), Formatting.Indented);
            var outputPath = Path.Combine(outputDir, OutputFileWithoutExt + ".json");
            File.WriteAllText(outputPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[VfxJsonExporter] Exported to: {outputPath} (count={(entries != null ? entries.Length : 0)})");
        }

        private static string TryGetSelectedFolderPath()
        {
            var obj = Selection.activeObject;
            if (obj == null) return "Assets";

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return "Assets";

            if (AssetDatabase.IsValidFolder(path)) return path;

            var dir = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(dir) ? "Assets" : dir.Replace('\\', '/');
        }

        private static VfxDTO[] LoadEntries(string assetFolder)
        {
            var list = new List<VfxDTO>(64);

            var guids = AssetDatabase.FindAssets("t:VfxSO", new[] { assetFolder });
            if (guids == null || guids.Length == 0) return Array.Empty<VfxDTO>();

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<VfxSO>(path);
                if (asset == null || asset.dataList == null) continue;

                for (int j = 0; j < asset.dataList.Length; j++)
                {
                    var dto = asset.dataList[j];
                    if (dto == null) continue;
                    if (dto.Id <= 0) continue;
                    list.Add(dto);
                }
            }

            return list.ToArray();
        }

        private static void ValidateUniqueById(VfxDTO[] entries)
        {
            if (entries == null || entries.Length == 0) return;

            var set = new HashSet<int>();
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e == null) continue;
                if (!set.Add(e.Id))
                {
                    throw new InvalidOperationException($"Duplicate VfxDTO.Id: {e.Id}");
                }
            }
        }
    }
}
#endif
