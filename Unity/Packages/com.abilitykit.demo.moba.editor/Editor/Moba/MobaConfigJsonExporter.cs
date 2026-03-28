#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Reflection;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Share.Common.TagSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class MobaConfigJsonExporter
    {
        [MenuItem("AbilityKit/Moba/Export Config Json")]
        public static void ExportSelected()
        {
            var folder = TryGetSelectedFolderPath();
            ExportFromFolder(folder);
        }

        public static void ExportFromFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) assetFolder = "Assets";

            var projectAssetsPath = Application.dataPath;
            var outputDir = Path.Combine(projectAssetsPath, "Resources", "moba");
            Directory.CreateDirectory(outputDir);

            var tables = LoadTables(assetFolder);
            for (var i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                var entries = MergeEntries(table);
                PreprocessBeforeExport(entries, table.EntryType, table.FileWithoutExt);
                ValidateUniqueById(entries, table.EntryType, table.FileWithoutExt);
                WriteArray(outputDir, table.FileWithoutExt, entries, table.EntryType);
            }

            AssetDatabase.Refresh();
            Debug.Log($"[MobaConfigJsonExporter] Exported to: {outputDir}");
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

        private static List<IMobaConfigTableAsset> LoadTables(string assetFolder)
        {
            var result = new List<IMobaConfigTableAsset>(16);

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
                    if (obj == null) continue;
                    if (obj is IMobaConfigTableAsset table)
                    {
                        result.Add(table);
                    }
                }
            }

            return result;
        }

        private static Array MergeEntries(IMobaConfigTableAsset table)
        {
            var entryType = table.EntryType;
            var list = new List<object>(64);
            var enumerable = table.GetEntries();
            if (enumerable != null)
            {
                foreach (var e in enumerable)
                {
                    if (e == null) continue;
                    list.Add(e);
                }
            }

            var arr = Array.CreateInstance(entryType, list.Count);
            for (var i = 0; i < list.Count; i++) arr.SetValue(list[i], i);
            return arr;
        }

        private static void PreprocessBeforeExport(Array entries, Type entryType, string fileWithoutExt)
        {
            if (entries == null || entries.Length == 0) return;

            if (entryType == typeof(TagTemplateDTO) || fileWithoutExt == MobaConfigPaths.TagTemplatesFile)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries.GetValue(i) is TagTemplateDTO dto)
                    {
                        ConvertTagNamesToIds(dto);
                    }
                }
            }
        }

        private static void ConvertTagNamesToIds(TagTemplateDTO dto)
        {
            if (dto == null) return;
            GameplayTagLib.RegisterAll();

            dto.RequiredTags = ConvertNames(dto.RequiredTagNames);
            dto.BlockedTags = ConvertNames(dto.BlockedTagNames);
            dto.GrantTags = ConvertNames(dto.GrantTagNames);
            dto.RemoveTags = ConvertNames(dto.RemoveTagNames);
        }

        private static int[] ConvertNames(string[] names)
        {
            if (names == null || names.Length == 0) return Array.Empty<int>();

            var list = new List<int>(names.Length);
            for (var i = 0; i < names.Length; i++)
            {
                var n = names[i];
                if (string.IsNullOrWhiteSpace(n)) continue;
                var tag = GameplayTags.Tag(n);
                if (!tag.IsValid) continue;
                list.Add(tag.Value);
            }
            return list.Count == 0 ? Array.Empty<int>() : list.ToArray();
        }

        private static void ValidateUniqueById(Array entries, Type entryType, string name)
        {
            if (entries == null) return;
            var idGetter = CreateIdGetter(entryType);

            var set = new HashSet<int>();
            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries.GetValue(i);
                if (e == null) continue;
                var id = idGetter(e);
                if (!set.Add(id))
                {
                    throw new InvalidOperationException($"Duplicate Id in {name}: {id}");
                }
            }
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

        private static void WriteArray(string outputDir, string fileWithoutExt, Array data, Type entryType)
        {
            var json = JsonConvert.SerializeObject(data ?? Array.CreateInstance(entryType, 0), Formatting.Indented);
            var outputPath = Path.Combine(outputDir, $"{fileWithoutExt}.json");
            File.WriteAllText(outputPath, json);
        }
    }
}
#endif
