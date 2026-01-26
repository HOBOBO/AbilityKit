#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class MobaConfigJsonFolderBatchSync
    {
        [MenuItem("AbilityKit/Moba/Config Json/Import Folder -> All SOs In Selected Folder")]
        public static void ImportAllInSelectedFolder()
        {
            var folder = TryGetSelectedFolderPath();
            var tables = LoadTables(folder);

            var ok = 0;
            for (var i = 0; i < tables.Count; i++)
            {
                var t = tables[i];
                try
                {
                    MobaConfigJsonFolderSync.ImportInto(t);
                    ok++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[MobaConfigJsonFolderBatchSync] Import failed. asset={AssetDatabase.GetAssetPath(t)}\n{e}");
                    return;
                }
            }

            Debug.Log($"[MobaConfigJsonFolderBatchSync] Imported {ok} tables from folder: {folder}");
        }

        [MenuItem("AbilityKit/Moba/Config Json/Export Folder -> All SOs In Selected Folder")]
        public static void ExportAllInSelectedFolder()
        {
            var folder = TryGetSelectedFolderPath();
            var tables = LoadTables(folder);

            var ok = 0;
            for (var i = 0; i < tables.Count; i++)
            {
                var t = tables[i];
                try
                {
                    MobaConfigJsonFolderSync.ExportFrom(t);
                    MobaConfigJsonFolderSync.TryExportArrayJsonToFolder(t);
                    ok++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[MobaConfigJsonFolderBatchSync] Export failed. asset={AssetDatabase.GetAssetPath(t)}\n{e}");
                    return;
                }
            }

            Debug.Log($"[MobaConfigJsonFolderBatchSync] Exported {ok} tables from folder: {folder}");
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

        private static List<MobaConfigTableAssetSO> LoadTables(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) assetFolder = "Assets";

            var result = new List<MobaConfigTableAssetSO>(32);
            var types = MobaConfigTableRegistry.TableAssetTypes;
            for (var i = 0; i < types.Length; i++)
            {
                var t = types[i];
                var guids = AssetDatabase.FindAssets($"t:{t.Name}", new[] { assetFolder });
                if (guids == null || guids.Length == 0) continue;

                for (var j = 0; j < guids.Length; j++)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guids[j]);
                    var obj = AssetDatabase.LoadAssetAtPath(assetPath, t);
                    if (obj is MobaConfigTableAssetSO table)
                    {
                        result.Add(table);
                    }
                }
            }

            return result;
        }
    }
}
#endif
