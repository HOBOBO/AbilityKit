#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
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

            var characters = LoadAndMerge<CharacterSO, CharacterSO.CharacterData>(assetFolder, x => x.dataList);
            var skills = LoadAndMerge<SkillSO, SkillSO.SkillData>(assetFolder, x => x.dataList);
            var attributes = LoadAndMerge<BattleAttributeTemplateSO, BattleAttributeTemplateSO.BattleAttributeTemplateData>(assetFolder, x => x.dataList);
            var models = LoadAndMerge<ModelSO, ModelSO.ModelData>(assetFolder, x => x.dataList);

            ValidateUnique(characters, nameof(characters));
            ValidateUnique(skills, nameof(skills));
            ValidateUnique(attributes, nameof(attributes));
            ValidateUnique(models, nameof(models));

            WriteArray(outputDir, MobaConfigPaths.CharactersFile, Convert(characters));
            WriteArray(outputDir, MobaConfigPaths.SkillsFile, Convert(skills));
            WriteArray(outputDir, MobaConfigPaths.AttributeTemplatesFile, Convert(attributes));
            WriteArray(outputDir, MobaConfigPaths.ModelsFile, Convert(models));

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

        private static TEntry[] LoadAndMerge<TAsset, TEntry>(string assetFolder, Func<TAsset, TEntry[]> getList)
            where TAsset : UnityEngine.Object
            where TEntry : class
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}", new[] { assetFolder });
            if (guids == null || guids.Length == 0) return Array.Empty<TEntry>();

            var list = new List<TEntry>(64);
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var table = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
                if (table == null) continue;

                var arr = getList(table);
                if (arr == null) continue;
                for (var j = 0; j < arr.Length; j++)
                {
                    if (arr[j] == null) continue;
                    list.Add(arr[j]);
                }
            }

            return list.ToArray();
        }

        private static void ValidateUnique<T>(T[] items, string name) where T : IKeyedSO<int>
        {
            if (items == null) return;
            var set = new HashSet<int>();
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                if (!set.Add(items[i].Key))
                {
                    throw new InvalidOperationException($"Duplicate key in {name}: {items[i].Key}");
                }
            }
        }

        private static void WriteArray<T>(string outputDir, string fileWithoutExt, T[] data)
        {
            var json = JsonConvert.SerializeObject(data ?? Array.Empty<T>(), Formatting.Indented);
            var outputPath = Path.Combine(outputDir, $"{fileWithoutExt}.json");
            File.WriteAllText(outputPath, json);
        }

        private static CharacterDTO[] Convert(CharacterSO.CharacterData[] arr)
        {
            if (arr == null) return Array.Empty<CharacterDTO>();
            var list = new CharacterDTO[arr.Length];
            for (var i = 0; i < arr.Length; i++)
            {
                var so = arr[i];
                if (so == null) continue;
                list[i] = new CharacterDTO
                {
                    Id = so.Key,
                    Name = so.Name,
                    ModelId = so.ModelId,
                    AttributeTemplateId = so.AttributeTemplateId,
                    SkillIds = so.SkillIds
                };
            }
            return list;
        }

        private static SkillDTO[] Convert(SkillSO.SkillData[] arr)
        {
            if (arr == null) return Array.Empty<SkillDTO>();
            var list = new SkillDTO[arr.Length];
            for (var i = 0; i < arr.Length; i++)
            {
                var so = arr[i];
                if (so == null) continue;
                list[i] = new SkillDTO
                {
                    Id = so.Key,
                    Name = so.Name,
                    CooldownMs = so.CooldownMs,
                    Range = so.Range,
                    IconId = so.IconId,
                    Category = so.Category,
                    Tags = so.GetTagsCopy()
                };
            }
            return list;
        }

        private static BattleAttributeTemplateDTO[] Convert(BattleAttributeTemplateSO.BattleAttributeTemplateData[] arr)
        {
            if (arr == null) return Array.Empty<BattleAttributeTemplateDTO>();
            var list = new BattleAttributeTemplateDTO[arr.Length];
            for (var i = 0; i < arr.Length; i++)
            {
                var so = arr[i];
                if (so == null) continue;
                list[i] = new BattleAttributeTemplateDTO
                {
                    Id = so.Key,
                    MaxHp = so.MaxHp,
                    Attack = so.Attack,
                    Defense = so.Defense,
                    MoveSpeed = so.MoveSpeed
                };
            }
            return list;
        }

        private static ModelDTO[] Convert(ModelSO.ModelData[] arr)
        {
            if (arr == null) return Array.Empty<ModelDTO>();
            var list = new ModelDTO[arr.Length];
            for (var i = 0; i < arr.Length; i++)
            {
                var so = arr[i];
                if (so == null) continue;
                list[i] = new ModelDTO
                {
                    Id = so.Key,
                    PrefabPath = so.PrefabPath,
                    Scale = so.Scale
                };
            }
            return list;
        }
    }
}
#endif
