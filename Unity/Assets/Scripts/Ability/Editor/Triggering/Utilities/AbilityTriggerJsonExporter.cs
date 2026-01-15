#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering.Runtime;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class AbilityTriggerJsonExporter
    {
        private const string OutputResourcesDir = "ability";
        private const string OutputFileWithoutExt = "ability_triggers";
        private const string DefaultAbilityConfigFolder = "Assets/Configs/Ability";

        [MenuItem("AbilityKit/Ability/Export Trigger Json")]
        public static void ExportSelectedFolder()
        {
            var folder = TryGetSelectedFolderPath();
            ExportFromFolder(folder);
        }

        [MenuItem("AbilityKit/Ability/Export Trigger Json (Configs/Ability)")]
        public static void ExportDefaultFolder()
        {
            ExportFromFolder(DefaultAbilityConfigFolder);
        }

        public static void ExportFromFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) assetFolder = "Assets";

            Debug.Log($"[AbilityTriggerJsonExporter] ExportFromFolder: {assetFolder}");

            var outputDir = Path.Combine(Application.dataPath, "Resources", OutputResourcesDir);
            Directory.CreateDirectory(outputDir);

            var dto = BuildDto(assetFolder, out var moduleCount, out var exportedTriggerCount, out var skippedDisabledCount, out var skippedInvalidIdCount);
            if (assetFolder != "Assets" && (moduleCount == 0 || exportedTriggerCount == 0))
            {
                Debug.Log($"[AbilityTriggerJsonExporter] No triggers exported from '{assetFolder}'. Fallback to scan whole 'Assets'.");
                dto = BuildDto("Assets", out moduleCount, out exportedTriggerCount, out skippedDisabledCount, out skippedInvalidIdCount);
            }

            Debug.Log($"[AbilityTriggerJsonExporter] Modules={moduleCount}, ExportedTriggers={exportedTriggerCount}, SkippedDisabled={skippedDisabledCount}, SkippedTriggerId<=0={skippedInvalidIdCount}");

            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            var outputPath = Path.Combine(outputDir, OutputFileWithoutExt + ".json");
            File.WriteAllText(outputPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[AbilityTriggerJsonExporter] Exported to: {outputPath}");
        }

        private static AbilityTriggerDatabaseDTO BuildDto(
            string assetFolder,
            out int moduleCount,
            out int exportedTriggerCount,
            out int skippedDisabledCount,
            out int skippedInvalidIdCount)
        {
            var db = new AbilityTriggerDatabaseDTO();
            moduleCount = 0;
            exportedTriggerCount = 0;
            skippedDisabledCount = 0;
            skippedInvalidIdCount = 0;

            var guids = AssetDatabase.FindAssets("t:AbilityModuleSO", new[] { assetFolder });
            if (guids == null || guids.Length == 0) return db;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<AbilityModuleSO>(path);
                if (asset == null) continue;

                moduleCount++;

                if (asset.Triggers != null)
                {
                    for (int t = 0; t < asset.Triggers.Count; t++)
                    {
                        var tr = asset.Triggers[t];
                        if (tr == null) continue;
                        if (!tr.Enabled)
                        {
                            skippedDisabledCount++;
                            continue;
                        }

                        if (tr.TriggerId <= 0)
                        {
                            skippedInvalidIdCount++;
                            continue;
                        }

                        var triggerDto = new TriggerDTO
                        {
                            TriggerId = tr.TriggerId,
                            EventId = tr.EventId,
                            AllowExternal = tr.AllowExternal,
                            InitialLocalVars = BuildInitialLocalVars(tr),
                            Conditions = BuildConditions(tr),
                            Actions = BuildActions(tr)
                        };
                        db.Triggers.Add(triggerDto);
                        exportedTriggerCount++;
                    }
                }
            }

            return db;
        }

        private static Dictionary<string, object> BuildInitialLocalVars(TriggerEditorConfig editor)
        {
            if (editor == null || editor.LocalVars == null || editor.LocalVars.Count == 0) return null;

            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < editor.LocalVars.Count; i++)
            {
                var e = editor.LocalVars[i];
                if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                dict[e.Key] = e.ToArgRuntimeEntry().GetBoxedValue();
            }
            return dict.Count > 0 ? dict : null;
        }

        private static List<ConditionDTO> BuildConditions(TriggerEditorConfig editor)
        {
            var list = new List<ConditionDTO>();
            if (editor?.ConditionsStrong == null) return list;

            for (int i = 0; i < editor.ConditionsStrong.Count; i++)
            {
                var c = editor.ConditionsStrong[i];
                if (c == null) continue;

                var rt = c.ToRuntimeStrong();
                if (rt == null) continue;
                var def = rt.ToConditionDef();
                list.Add(BuildConditionDto(def));
            }

            return list;
        }

        private static List<ActionDTO> BuildActions(TriggerEditorConfig editor)
        {
            var list = new List<ActionDTO>();
            if (editor?.ActionsStrong == null) return list;

            for (int i = 0; i < editor.ActionsStrong.Count; i++)
            {
                var a = editor.ActionsStrong[i];
                if (a == null) continue;

                var rt = a.ToRuntimeStrong();
                if (rt == null) continue;
                var def = rt.ToActionDef();
                list.Add(BuildActionDto(def));
            }

            return list;
        }

        private static ConditionDTO BuildConditionDto(AbilityKit.Ability.Triggering.Definitions.ConditionDef def)
        {
            if (def == null) return null;

            if (string.Equals(def.Type, TriggerConditionTypes.All, StringComparison.Ordinal) || string.Equals(def.Type, TriggerConditionTypes.Any, StringComparison.Ordinal))
            {
                var node = new ConditionDTO
                {
                    Type = def.Type,
                    Items = new List<ConditionDTO>()
                };

                if (def.Args == null || !def.Args.TryGetValue(TriggerDefArgKeys.Items, out var itemsObj) || !(itemsObj is IList<AbilityKit.Ability.Triggering.Definitions.ConditionDef> items))
                {
                    throw new InvalidOperationException($"Condition '{def.Type}' requires args['items'] as IList<ConditionDef>");
                }

                node.Items = new List<ConditionDTO>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    var child = BuildConditionDto(items[i]);
                    if (child != null) node.Items.Add(child);
                }
                return node;
            }

            if (string.Equals(def.Type, TriggerConditionTypes.Not, StringComparison.Ordinal))
            {
                var node = new ConditionDTO
                {
                    Type = def.Type
                };

                if (def.Args == null || !def.Args.TryGetValue(TriggerDefArgKeys.Item, out var itemObj) || !(itemObj is AbilityKit.Ability.Triggering.Definitions.ConditionDef item))
                {
                    throw new InvalidOperationException("Condition 'not' requires args['item'] as ConditionDef");
                }

                node.Item = BuildConditionDto(item);
                return node;
            }

            var dto = new ConditionDTO
            {
                Type = def.Type,
                Args = CopyArgs(def.Args)
            };
            return dto;
        }

        private static ActionDTO BuildActionDto(AbilityKit.Ability.Triggering.Definitions.ActionDef def)
        {
            if (def == null) return null;

            if (string.Equals(def.Type, TriggerActionTypes.Seq, StringComparison.Ordinal))
            {
                var dto = new ActionDTO
                {
                    Type = def.Type,
                    Items = new List<ActionDTO>()
                };

                if (def.Args == null || !def.Args.TryGetValue(TriggerDefArgKeys.Items, out var itemsObj) || !(itemsObj is IList<AbilityKit.Ability.Triggering.Definitions.ActionDef> items))
                {
                    throw new InvalidOperationException("seq action requires args['items'] as IList<ActionDef>");
                }

                dto.Items = new List<ActionDTO>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    var child = BuildActionDto(items[i]);
                    if (child != null) dto.Items.Add(child);
                }
                return dto;
            }

            var node = new ActionDTO
            {
                Type = def.Type,
                Args = CopyArgs(def.Args)
            };
            return node;
        }

        private static Dictionary<string, object> CopyArgs(IReadOnlyDictionary<string, object> args)
        {
            if (args == null) return null;
            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kv in args)
            {
                if (kv.Key == null) continue;
                dict[kv.Key] = kv.Value;
            }
            return dict.Count > 0 ? dict : null;
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
    }
}
#endif
