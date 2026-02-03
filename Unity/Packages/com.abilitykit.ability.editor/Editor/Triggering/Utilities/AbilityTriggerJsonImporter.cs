#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering.Runtime;
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

                editor.ConditionsStrong = new List<ConditionEditorConfigBase>();
                if (dto.Conditions != null)
                {
                    for (int c = 0; c < dto.Conditions.Count; c++)
                    {
                        var cc = BuildConditionEditorConfig(dto.Conditions[c]);
                        if (cc != null) editor.ConditionsStrong.Add(cc);
                    }
                }

                editor.ActionsStrong = new List<ActionEditorConfigBase>();
                if (dto.Actions != null)
                {
                    for (int a = 0; a < dto.Actions.Count; a++)
                    {
                        var aa = BuildActionEditorConfig(dto.Actions[a]);
                        if (aa != null) editor.ActionsStrong.Add(aa);
                    }
                }
            }

            module.Triggers.Sort((a, b) => (a?.TriggerId ?? 0).CompareTo(b?.TriggerId ?? 0));
        }

        private static ConditionEditorConfigBase BuildConditionEditorConfig(ConditionDTO dto)
        {
            if (dto == null) return null;
            // TODO: map common strong condition configs when they exist.
            return JsonConditionEditorConfig.FromDto(dto);
        }

        private static ActionEditorConfigBase BuildActionEditorConfig(ActionDTO dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Type)) return null;

            // Prefer strong editor configs so parameters become real fields (not just Json Args).
            if (string.Equals(dto.Type, TriggerActionTypes.Seq, StringComparison.Ordinal))
            {
                var seq = new SequenceActionEditorConfig();
                if (dto.Items != null && dto.Items.Count > 0)
                {
                    for (int i = 0; i < dto.Items.Count; i++)
                    {
                        var child = BuildActionEditorConfig(dto.Items[i]);
                        if (child != null) seq.Items.Add(child);
                    }
                }
                return seq;
            }

            if (string.Equals(dto.Type, TriggerActionTypes.DebugLog, StringComparison.Ordinal))
            {
                var a = new DebugLogActionEditorConfig();
                if (dto.Args != null)
                {
                    if (TryReadString(dto.Args, "message", out var msg)) a.Message = msg;
                    if (TryReadBool(dto.Args, "dump_args", out var dump)) a.DumpArgs = dump;
                }
                return a;
            }

            // Fallback: keep exact dto shape.
            return JsonActionEditorConfig.FromDto(dto);
        }

        private static bool TryReadString(Dictionary<string, object> args, string key, out string value)
        {
            value = null;
            if (args == null || string.IsNullOrEmpty(key)) return false;
            if (!args.TryGetValue(key, out var obj) || obj == null) return false;
            value = obj as string ?? obj.ToString();
            return true;
        }

        private static bool TryReadBool(Dictionary<string, object> args, string key, out bool value)
        {
            value = false;
            if (args == null || string.IsNullOrEmpty(key)) return false;
            if (!args.TryGetValue(key, out var obj) || obj == null) return false;

            if (obj is bool b)
            {
                value = b;
                return true;
            }

            try
            {
                value = Convert.ToBoolean(obj);
                return true;
            }
            catch
            {
                return false;
            }
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
