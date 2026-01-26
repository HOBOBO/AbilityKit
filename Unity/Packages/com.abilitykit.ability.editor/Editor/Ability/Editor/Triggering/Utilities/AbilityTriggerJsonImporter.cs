#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Share.CoreDtos;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class AbilityTriggerJsonImporter
    {
        private const string ResourcesDir = "ability";
        private const string FileWithoutExt = "ability_triggers";
        private const string DefaultAbilityConfigFolder = "Assets/Configs/Ability";

        private const string GeneratedModuleDir = "Assets/Configs/Ability/Generated";
        private const string GeneratedModuleName = "ability_triggers.generated";

        [MenuItem("AbilityKit/Ability/Import Trigger Json -> Generated Module")]
        public static void ImportFromDefaultJson()
        {
            var jsonPath = Path.Combine(Application.dataPath, "Resources", ResourcesDir, FileWithoutExt + ".json");
            ImportFromFile(jsonPath);
        }

        public static void ImportFromFile(string absoluteJsonPath)
        {
            if (string.IsNullOrEmpty(absoluteJsonPath)) throw new ArgumentException(nameof(absoluteJsonPath));
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[AbilityTriggerJsonImporter] Json file not found: {absoluteJsonPath}");
                return;
            }

            var json = File.ReadAllText(absoluteJsonPath);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"[AbilityTriggerJsonImporter] Json file empty: {absoluteJsonPath}");
                return;
            }

            AbilityTriggerDatabaseDTO dto;
            try
            {
                dto = JsonConvert.DeserializeObject<AbilityTriggerDatabaseDTO>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AbilityTriggerJsonImporter] Failed to parse json: {absoluteJsonPath}\n{e}");
                return;
            }

            if (dto == null || dto.Triggers == null)
            {
                Debug.LogError($"[AbilityTriggerJsonImporter] Triggers list is null. file={absoluteJsonPath}");
                return;
            }

            Directory.CreateDirectory(ToAbsoluteAssetPath(GeneratedModuleDir));

            var assetPath = $"{GeneratedModuleDir}/{GeneratedModuleName}.asset";
            var module = AssetDatabase.LoadAssetAtPath<AbilityModuleSO>(assetPath);
            if (module == null)
            {
                module = ScriptableObject.CreateInstance<AbilityModuleSO>();
                module.AbilityId = GeneratedModuleName;
                AssetDatabase.CreateAsset(module, assetPath);
            }

            UpsertTriggers(module, dto.Triggers);

            EditorUtility.SetDirty(module);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AbilityTriggerJsonImporter] Imported {dto.Triggers.Count} triggers into module: {assetPath}");
            Selection.activeObject = module;
        }

        private static void UpsertTriggers(AbilityModuleSO module, List<TriggerDTO> triggers)
        {
            if (module.Triggers == null) module.Triggers = new List<TriggerEditorConfig>();

            var map = new Dictionary<int, TriggerEditorConfig>();
            for (int i = 0; i < module.Triggers.Count; i++)
            {
                var t = module.Triggers[i];
                if (t == null) continue;
                var id = t.TriggerId;
                if (id <= 0) continue;
                map[id] = t;
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                var dto = triggers[i];
                if (dto == null) continue;
                if (dto.TriggerId <= 0) continue;

                if (!map.TryGetValue(dto.TriggerId, out var editor))
                {
                    editor = new TriggerEditorConfig();
                    module.Triggers.Add(editor);
                    map[dto.TriggerId] = editor;
                }

                if (editor.Core == null) editor.Core = new TriggerHeaderDTO();
                editor.Enabled = true;
                editor.TriggerId = dto.TriggerId;
                editor.EventId = dto.EventId;
                editor.AllowExternal = dto.AllowExternal;

                editor.ConditionsStrong = new List<ConditionEditorConfigBase>();
                if (dto.Conditions != null)
                {
                    for (int c = 0; c < dto.Conditions.Count; c++)
                    {
                        var cc = JsonConditionEditorConfig.FromDto(dto.Conditions[c]);
                        if (cc != null) editor.ConditionsStrong.Add(cc);
                    }
                }

                editor.ActionsStrong = new List<ActionEditorConfigBase>();
                if (dto.Actions != null)
                {
                    for (int a = 0; a < dto.Actions.Count; a++)
                    {
                        var aa = JsonActionEditorConfig.FromDto(dto.Actions[a]);
                        if (aa != null) editor.ActionsStrong.Add(aa);
                    }
                }
            }

            module.Triggers.Sort((a, b) => (a?.TriggerId ?? 0).CompareTo(b?.TriggerId ?? 0));
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            assetPath = (assetPath ?? string.Empty).Replace('\\', '/');
            if (assetPath == "Assets") return Application.dataPath;
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Expected Assets path: {assetPath}");
            }

            var rel = assetPath.Substring("Assets/".Length);
            return Path.Combine(Application.dataPath, rel);
        }
    }
}
#endif
