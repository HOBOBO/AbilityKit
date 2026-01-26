#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class MobaConfigJsonFolderSync
    {
        private const string BaseResourcesFolder = "Assets/Resources/moba";

        [MenuItem("AbilityKit/Moba/Config Json/Import Folder -> Selected SO")]
        public static void ImportFolderToSelectedSo()
        {
            var table = Selection.activeObject as MobaConfigTableAssetSO;
            if (table == null)
            {
                Debug.LogError("[MobaConfigJsonFolderSync] Please select a config table ScriptableObject (MobaConfigTableAssetSO).");
                return;
            }

            ImportInto(table);
        }

        [MenuItem("AbilityKit/Moba/Config Json/Export Selected SO -> Folder")]
        public static void ExportSelectedSoToFolder()
        {
            var table = Selection.activeObject as MobaConfigTableAssetSO;
            if (table == null)
            {
                Debug.LogError("[MobaConfigJsonFolderSync] Please select a config table ScriptableObject (MobaConfigTableAssetSO).");
                return;
            }

            ExportFrom(table);
        }

        [MenuItem("AbilityKit/Moba/Config Json/Export Array Json -> Folder (Selected SO Type)")]
        public static void ExportArrayJsonToFolderBySelectedType()
        {
            var table = Selection.activeObject as MobaConfigTableAssetSO;
            if (table == null)
            {
                Debug.LogError("[MobaConfigJsonFolderSync] Please select a config table ScriptableObject (MobaConfigTableAssetSO).");
                return;
            }

            ExportArrayJsonToFolder(table);
        }

        [MenuItem("AbilityKit/Moba/Config Json/Import Folder -> Array Json (Selected SO Type)")]
        public static void ImportFolderToArrayJsonBySelectedType()
        {
            var table = Selection.activeObject as MobaConfigTableAssetSO;
            if (table == null)
            {
                Debug.LogError("[MobaConfigJsonFolderSync] Please select a config table ScriptableObject (MobaConfigTableAssetSO).");
                return;
            }

            ImportFolderToArrayJson(table);
        }

        public static void ImportInto(MobaConfigTableAssetSO table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var entryType = table.EntryType;
            var fileWithoutExt = table.FileWithoutExt;

            var folder = GetFolderAssetPath(fileWithoutExt);
            var absoluteFolder = ToAbsolutePath(folder);
            if (!Directory.Exists(absoluteFolder))
            {
                Debug.LogError($"[MobaConfigJsonFolderSync] Folder not found: {folder}");
                return;
            }

            var idGetter = CreateIdGetter(entryType);

            var dict = new Dictionary<int, object>();
            var enumerable = table.GetEntries();
            if (enumerable != null)
            {
                foreach (var e in enumerable)
                {
                    if (e == null) continue;
                    var id = idGetter(e);
                    dict[id] = e;
                }
            }

            var paths = Directory.GetFiles(absoluteFolder, "*.json", SearchOption.TopDirectoryOnly);
            for (var i = 0; i < paths.Length; i++)
            {
                var p = paths[i];
                try
                {
                    var json = File.ReadAllText(p);
                    if (string.IsNullOrWhiteSpace(json)) continue;

                    var obj = JsonConvert.DeserializeObject(json, entryType);
                    if (obj == null) continue;

                    var id = idGetter(obj);
                    dict[id] = obj;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MobaConfigJsonFolderSync] Failed to read: {p}\n{e}");
                    return;
                }
            }

            var list = new List<object>(dict.Values);
            list.Sort((a, b) => idGetter(a).CompareTo(idGetter(b)));

            var arr = Array.CreateInstance(entryType, list.Count);
            for (var i = 0; i < list.Count; i++) arr.SetValue(list[i], i);

            if (!TrySetEntriesArray(table, entryType, arr))
            {
                Debug.LogError($"[MobaConfigJsonFolderSync] Failed to assign entries array. table={table.GetType().FullName} entryType={entryType.FullName}");
                return;
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MobaConfigJsonFolderSync] Imported {arr.Length} entries into: {AssetDatabase.GetAssetPath(table)}");
        }

        public static void ExportFrom(MobaConfigTableAssetSO table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var entryType = table.EntryType;
            var fileWithoutExt = table.FileWithoutExt;

            var folder = GetFolderAssetPath(fileWithoutExt);
            var absoluteFolder = ToAbsolutePath(folder);
            Directory.CreateDirectory(absoluteFolder);

            var idGetter = CreateIdGetter(entryType);

            var set = new HashSet<int>();
            var entries = new List<object>();
            var enumerable = table.GetEntries();
            if (enumerable != null)
            {
                foreach (var e in enumerable)
                {
                    if (e == null) continue;
                    var id = idGetter(e);
                    if (!set.Add(id))
                    {
                        Debug.LogError($"[MobaConfigJsonFolderSync] Duplicate Id in SO: {id} ({fileWithoutExt})");
                        return;
                    }
                    entries.Add(e);
                }
            }

            entries.Sort((a, b) => idGetter(a).CompareTo(idGetter(b)));

            var count = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var id = idGetter(e);
                var fileName = $"{fileWithoutExt}_{id}.json";
                var p = Path.Combine(absoluteFolder, fileName);
                var json = JsonConvert.SerializeObject(e, Formatting.Indented);
                File.WriteAllText(p, json);
                count++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[MobaConfigJsonFolderSync] Exported {count} entries to: {folder}");
        }

        public static void ExportArrayJsonToFolder(MobaConfigTableAssetSO table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var entryType = table.EntryType;
            var fileWithoutExt = table.FileWithoutExt;

            var folder = GetFolderAssetPath(fileWithoutExt);
            var absoluteFolder = ToAbsolutePath(folder);
            Directory.CreateDirectory(absoluteFolder);

            var arrayPath = GetArrayJsonAssetPath(fileWithoutExt);
            var absoluteArrayPath = ToAbsolutePath(arrayPath);
            if (!File.Exists(absoluteArrayPath))
            {
                Debug.LogError($"[MobaConfigJsonFolderSync] Array json not found: {arrayPath}");
                return;
            }

            var json = File.ReadAllText(absoluteArrayPath);
            var arrayType = entryType.MakeArrayType();
            var arr = JsonConvert.DeserializeObject(json, arrayType) as Array;
            if (arr == null) arr = Array.CreateInstance(entryType, 0);

            var idGetter = CreateIdGetter(entryType);
            var entries = new List<object>(arr.Length);
            for (var i = 0; i < arr.Length; i++)
            {
                var e = arr.GetValue(i);
                if (e != null) entries.Add(e);
            }
            entries.Sort((a, b) => idGetter(a).CompareTo(idGetter(b)));

            var count = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var id = idGetter(e);
                var fileName = $"{fileWithoutExt}_{id}.json";
                var p = Path.Combine(absoluteFolder, fileName);
                var itemJson = JsonConvert.SerializeObject(e, Formatting.Indented);
                File.WriteAllText(p, itemJson);
                count++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[MobaConfigJsonFolderSync] Exported {count} entries from array json to: {folder}");
        }

        public static bool TryExportArrayJsonToFolder(MobaConfigTableAssetSO table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var arrayPath = GetArrayJsonAssetPath(table.FileWithoutExt);
            var absoluteArrayPath = ToAbsolutePath(arrayPath);
            if (!File.Exists(absoluteArrayPath)) return false;

            ExportArrayJsonToFolder(table);
            return true;
        }

        public static void ImportFolderToArrayJson(MobaConfigTableAssetSO table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var entryType = table.EntryType;
            var fileWithoutExt = table.FileWithoutExt;

            var folder = GetFolderAssetPath(fileWithoutExt);
            var absoluteFolder = ToAbsolutePath(folder);
            if (!Directory.Exists(absoluteFolder))
            {
                Debug.LogError($"[MobaConfigJsonFolderSync] Folder not found: {folder}");
                return;
            }

            var idGetter = CreateIdGetter(entryType);
            var dict = new Dictionary<int, object>();

            var paths = Directory.GetFiles(absoluteFolder, "*.json", SearchOption.TopDirectoryOnly);
            for (var i = 0; i < paths.Length; i++)
            {
                var p = paths[i];
                try
                {
                    var json = File.ReadAllText(p);
                    if (string.IsNullOrWhiteSpace(json)) continue;

                    var obj = JsonConvert.DeserializeObject(json, entryType);
                    if (obj == null) continue;

                    var id = idGetter(obj);
                    dict[id] = obj;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MobaConfigJsonFolderSync] Failed to read: {p}\n{e}");
                    return;
                }
            }

            var list = new List<object>(dict.Values);
            list.Sort((a, b) => idGetter(a).CompareTo(idGetter(b)));

            var arr = Array.CreateInstance(entryType, list.Count);
            for (var i = 0; i < list.Count; i++) arr.SetValue(list[i], i);

            var arrayJson = JsonConvert.SerializeObject(arr, Formatting.Indented);
            var arrayPath = GetArrayJsonAssetPath(fileWithoutExt);
            var absoluteArrayPath = ToAbsolutePath(arrayPath);
            File.WriteAllText(absoluteArrayPath, arrayJson);

            AssetDatabase.Refresh();
            Debug.Log($"[MobaConfigJsonFolderSync] Wrote array json ({arr.Length} entries): {arrayPath}");
        }

        private static string GetFolderAssetPath(string fileWithoutExt)
        {
            return $"{BaseResourcesFolder}/{fileWithoutExt}";
        }

        private static string GetArrayJsonAssetPath(string fileWithoutExt)
        {
            return $"{BaseResourcesFolder}/{fileWithoutExt}.json";
        }

        private static bool TrySetEntriesArray(MobaConfigTableAssetSO table, Type entryType, Array arr)
        {
            var t = table.GetType();

            var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                if (f == null) continue;
                if (!f.FieldType.IsArray) continue;
                if (f.FieldType.GetElementType() != entryType) continue;

                f.SetValue(table, arr);
                return true;
            }

            return false;
        }

        private static Func<object, int> CreateIdGetter(Type entryType)
        {
            var field = entryType.GetField("Id", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int))
            {
                return o => (int)field.GetValue(o);
            }

            var prop = entryType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(int) && prop.GetMethod != null)
            {
                return o => (int)prop.GetValue(o);
            }

            throw new InvalidOperationException($"Config entry must have public int Id field/property. type={entryType.FullName}");
        }

        private static string ToAbsolutePath(string assetPath)
        {
            assetPath = (assetPath ?? string.Empty).Replace('\\', '/');
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal) || assetPath == "Assets")
            {
                var rel = assetPath.Length > "Assets".Length ? assetPath.Substring("Assets".Length).TrimStart('/') : string.Empty;
                return Path.Combine(Application.dataPath, rel);
            }

            throw new InvalidOperationException($"Expected an Assets path. path={assetPath}");
        }
    }
}
#endif
